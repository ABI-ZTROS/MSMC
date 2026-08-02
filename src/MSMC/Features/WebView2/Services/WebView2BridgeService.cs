// -----------------------------------------------------------------------------
// 文件名: WebView2BridgeService.cs
// 命名空间: io.NET.ZTR_OS.Features.WebView2.Services
// 功能描述: WebView2 桥接服务实现，提供 C# 与 JavaScript 双向通信
// 依赖组件: Microsoft.Web.WebView2.Wpf, System.Text.Json, Serilog
// 设计模式: 单例模式 + 消息模式 + 请求/响应模式 + 观察者模式
// -----------------------------------------------------------------------------
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using io.NET.ZTR_OS.Features.WebView2.Frontend;
using Microsoft.Web.WebView2.Core;
using WpfWebView2 = Microsoft.Web.WebView2.Wpf.WebView2;
using Serilog;

namespace io.NET.ZTR_OS.Features.WebView2.Services;

/// <summary>
/// WebView2 桥接服务实现
/// 通过 WebMessageReceived 和 PostWebMessageAsJson 实现 C# 与 JS 的双向通信
/// </summary>
public class WebView2BridgeService : IWebView2BridgeService, IDisposable
{
    /// <summary>
    /// JSON 序列化选项（驼峰命名，兼容 JS 习惯）
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 当前请求 ID（基于 AsyncLocal，跨 await 流转，供 handler 内部日志关联）
    /// </summary>
    private static readonly AsyncLocal<string?> _currentRequestId = new();

    /// <summary>
    /// 获取当前请求 ID（在 HandleRequestAsync 入口设置，handler 内部可读取用于日志关联）
    /// </summary>
    public static string? CurrentRequestId => _currentRequestId.Value;

    /// <summary>
    /// 绑定的 WebView2 控件引用
    /// </summary>
    private WpfWebView2? _webView;

    /// <summary>
    /// UI 线程调度器（用于跨线程操作 WPF 控件）
    /// </summary>
    private System.Windows.Threading.Dispatcher? _uiDispatcher;

    /// <summary>
    /// 请求处理程序字典（JS → C#）
    /// </summary>
    private readonly ConcurrentDictionary<string, RequestHandler> _requestHandlers = new();

    /// <summary>
    /// 等待中的请求任务源字典（C# → JS → C#）
    /// key: 请求 ID
    /// </summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource<object?>> _pendingRequests = new();

    /// <summary>
    /// 事件订阅者列表
    /// </summary>
    private readonly List<EventHandler> _eventSubscribers = [];

    /// <summary>
    /// 事件订阅锁
    /// </summary>
    private readonly object _eventLock = new();

    /// <summary>
    /// 是否已释放
    /// </summary>
    private bool _disposed;

    /// <inheritdoc />
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 初始化桥接服务，绑定到指定的 WebView2 控件
    /// </summary>
    /// <param name="webView">要绑定的 WebView2 控件</param>
    public async Task InitializeAsync(WpfWebView2 webView)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsInitialized)
        {
            Log.Warning("WebView2BridgeService 已初始化，跳过重复初始化");
            return;
        }

        _webView = webView ?? throw new ArgumentNullException(nameof(webView));

        // 保存 UI 线程调度器（WebView2 回调在后台线程，需要封送回 UI 线程操作 WPF 控件）
        _uiDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        Log.Information("[NOTE] UI 线程调度器已捕获");

        Log.Information("[BRDG] WebView2 桥接服务初始化中...");

        try
        {
            // 设置默认背景色为黑色，防止白屏闪烁
            _webView.DefaultBackgroundColor = System.Drawing.Color.Black;
            Log.Information("[THEME] WebView2 默认背景色已设置为黑色");

            // ──────────────────────────────────────────────────────────────
            // 【关键】显式创建 CoreWebView2Environment，带上允许 file:// 访问的 flags
            // 用户环境下 file:// + ES Module 会触发 Chromium 内部 CORS 拦截：
            // 所有 modulepreload / script / stylesheet 都在 8ms 内同时报 "Script error."
            // 这是 Chromium file:// origin= null 被视为跨域。
            // 加这 4 个 flag 把 file:// 的限制打开。
            // ──────────────────────────────────────────────────────────────
            var wv2Opts = new Microsoft.Web.WebView2.Core.CoreWebView2EnvironmentOptions()
            {
                AdditionalBrowserArguments = string.Join(" ", new[]
                {
                    "--allow-file-access-from-files",
                    "--allow-file-access",
                    "--disable-features=SplitCacheByNetworkIsolationKey,DivideUserContextByNetworkIsolationKey",
                    "--disable-web-security",
                }),
                Language = System.Globalization.CultureInfo.CurrentUICulture.Name,
            };
            var wv2Env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: null,
                options: wv2Opts);
            // 确保 CoreWebView2 已创建
            await _webView.EnsureCoreWebView2Async(wv2Env);

            // 配置桌面应用体验优化
            Log.Information("[CFG] 配置 WebView2 桌面应用体验优化...");

            // 禁用开发者工具（F12）
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Log.Information("   [OK] 已禁用开发者工具");

            // 禁用浏览器快捷键（F5刷新、Ctrl+R刷新、Ctrl+N新窗口等）
            _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            Log.Information("   [OK] 已禁用浏览器快捷键");

            // 禁用默认上下文菜单（右键菜单）
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Log.Information("   [OK] 已禁用默认上下文菜单");

            // 禁用状态条（左下角显示链接地址）
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Log.Information("   [OK] 已禁用状态条");

            // 禁止缩放
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            Log.Information("   [OK] 已禁用缩放控制");

            // 禁用默认脚本对话框（alert/confirm/prompt）
            _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            Log.Information("   [OK] 已禁用默认脚本对话框");

            Log.Information("[CFG] WebView2 桌面应用体验优化配置完成");

            // 注册消息接收事件
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // 注册导航完成事件（用于诊断日志）
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            // 关键：使用 AddScriptToExecuteOnDocumentCreatedAsync 注入桥接脚本
            // 这样脚本会在每次新文档创建时、页面任何脚本之前执行
            // 确保诊断脚本和前端代码执行时 window.__msmc_bridge__ 已就绪
            var initScript = GenerateBridgeInitScript();
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript);
            Log.Information("[API] 桥接脚本已通过 AddScriptToExecuteOnDocumentCreatedAsync 注册（将在页面脚本之前执行）");

            IsInitialized = true;
            Log.Information("[OK] WebView2 桥接服务初始化完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] WebView2 桥接服务初始化失败");
            throw;
        }
    }

    /// <inheritdoc />
    public void SetVirtualHostMapping(string hostName, string folderPath)
    {
        if (_webView?.CoreWebView2 == null)
            throw new InvalidOperationException("桥接未初始化");

        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            hostName,
            folderPath,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

        Log.Information("[NET] 虚拟主机映射已设置: {HostName} -> {FolderPath}", hostName, folderPath);
    }

    /// <inheritdoc />
    public async Task<bool> LoadFrontendAsync(IFrontendResourceProvider provider, string hostName)
    {
        if (_webView?.CoreWebView2 == null)
            throw new InvalidOperationException("桥接未初始化");

        if (!provider.IsAvailable)
        {
            Log.Warning("[WV2-LOAD] [WARN] 前端资源提供器不可用: {Mode}", provider.ModeName);
            return false;
        }

        // ──────────────────────────────────────────────────────────────
        // 【关键修复】hostName 必须短 + 纯 ASCII + 不要带点（不要 .local）
        // 否则 WebView2 老版本可能走 LLMNR/mDNS 解析 .local，导致 30s 超时。
        // 如果调用方传进来的 hostName 含有 ".local" 或 "-"，替换为单标签短名。
        // ──────────────────────────────────────────────────────────────
        string effectiveHost = hostName;
        if (effectiveHost.Contains('.') || effectiveHost.Contains('-'))
        {
            effectiveHost = "msmcapp";   // 单标签 ASCII 短名
            Log.Warning("[WV2-LOAD] [WARN] hostName=\"{Original}\" 含有 . 或 -，替换为安全短名 \"{Safe}\"（避免 30s DNS 解析超时）",
                hostName, effectiveHost);
        }

        Log.Information("[WV2-LOAD] [BOOT] 开始加载前端资源 (模式: {Mode}, host: {Host})", provider.ModeName, effectiveHost);

        try
        {
            var basePath = await provider.GetBasePathAsync();
            Log.Information("[WV2-LOAD] [FS] GetBasePathAsync 返回: {Path}", basePath ?? "(null，将使用拦截模式)");

            // ──────────────────────────────────────────────────────────────
            // 【优先级翻转 2026-07-30】http 虚拟主机优先，file:// 兜底
            // 原因：Chromium 内核将 <script type="module"> 走的 fetch+CORS 通道，
            // 在 file:// 协议下 origin = null，所有本地模块被视为跨域，
            // 表现为 8ms 内所有资源统一报 "Script error."（opaque 错误）。
            // http 协议下（即使是虚拟主机名）没有这个问题。
            // 只有 forceFileProtocol=true（或者以后调试需要）才走 file://。
            // ──────────────────────────────────────────────────────────────
            Uri targetUri;
            bool useVirtualHost = basePath != null;    // Folder/ZipExtract: 有真实路径就用 SetVirtualHostNameToFolderMapping
            const bool forceFileProtocol = false;      // ← 调试开关：强制走 file://（需配合 --allow-file-access-from-files）

            if (useVirtualHost)
            {
                targetUri = new Uri($"http://{effectiveHost}/index.html");
                // 先注册 folder 映射，再导航！顺序反了会找不到
                Log.Information("[WV2-LOAD] [LINK] 设置虚拟主机映射...");
                SetVirtualHostMapping(effectiveHost, basePath!);
                Log.Information("[WV2-LOAD] [OK] 虚拟主机映射设置完成（http://{Host}/ → {Folder}）", effectiveHost, basePath);
            }
            else
            {
                // EmbeddedResource 模式：无真实磁盘路径，走 WebResourceRequested 拦截器
                targetUri = new Uri($"http://{effectiveHost}/index.html");
                Log.Information("[WV2-LOAD] [API] 注册 WebResourceRequested 拦截器（EmbeddedResource 模式）...");
                RegisterWebResourceRequested(provider, effectiveHost);
                Log.Information("[WV2-LOAD] [OK] WebResourceRequested 拦截器注册完成");
            }

            // ──────────────────────────────────────────────────────────────
            // 兼容兜底：forceFileProtocol=true 时，Folder/ZipExtract 模式走 file://
            //（--allow-file-access-from-files 已经在 WV2 启动参数里加过了）
            // ──────────────────────────────────────────────────────────────
            if (forceFileProtocol && basePath != null)
            {
                var directIndexPath = Path.GetFullPath(Path.Combine(basePath, "index.html"));
                if (File.Exists(directIndexPath))
                {
                    try
                    {
                        targetUri = new UriBuilder("file", string.Empty) { Path = directIndexPath.Replace('\\', '/') }.Uri;
                        useVirtualHost = false;
                        Log.Information("[WV2-LOAD] [ALT] forceFileProtocol=true → file:// 直读: {Uri}", targetUri.AbsoluteUri);
                    }
                    catch (Exception uriEx)
                    {
                        Log.Warning(uriEx, "[WV2-LOAD] [WARN] 构造 file:// Uri 失败，保留虚拟主机模式");
                    }
                }
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // 【注意】CoreWebView2 早期 1.x 稳定版没有单独的 NavigationFailed 事件，
            // 所有导航结果统一在 NavigationCompleted 给出 IsSuccess + WebErrorStatus + HttpStatusCode。
            void OnNavCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (e.IsSuccess)
                {
                    Log.Information("[WV2-LOAD] [MSG] NavigationCompleted: 成功 HTTP {Code} (模式={Mode}, Uri={Uri})",
                        e.HttpStatusCode, provider.ModeName, targetUri.AbsoluteUri);
                    tcs.TrySetResult(true);
                }
                else
                {
                    Log.Error(
                        "[WV2-LOAD] [ERR] NavigationCompleted 失败: WebErrorStatus={Status}, HTTP={Code} (模式={Mode}, Uri={Uri})。" +
                        "常见原因: ① https 虚拟主机证书问题（已改 http）② 路径被杀毒/组策略拦截 ③ index.html 不存在 ④ file 协议下相对路径引用出错",
                        e.WebErrorStatus, e.HttpStatusCode, provider.ModeName, targetUri.AbsoluteUri);
                    tcs.TrySetResult(false);
                }
                if (_webView?.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNavCompleted;
                }
            }

            _webView.CoreWebView2.NavigationCompleted += OnNavCompleted;

            // 开始导航
            Log.Information("[WV2-LOAD] [NAV] 开始导航到: {Url} (模式={Mode}, 虚拟主机={UseVH})",
                targetUri.AbsoluteUri, provider.ModeName, useVirtualHost);
            _webView.Source = targetUri;

            // 超时：虚拟主机模式 30s（保守），file 协议模式 20s（直读更快，超时应该更短）
            int timeoutSeconds = useVirtualHost ? 30 : 20;
            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var completed = await Task.WhenAny(tcs.Task, timeout);

            if (completed == timeout)
            {
                Log.Error(
                    "[WV2-LOAD] [TIME] 前端页面加载超时 ({Sec}s)！模式: {Mode}, Uri: {Url}, 虚拟主机={UseVH}。" +
                    "若持续出现，请检查: ① 目标 index.html 是否真实存在 ② 手动在浏览器打开该文件是否能正常渲染 ③ 关闭杀毒软件重试。" +
                    "（注：WebView2 1.x SDK 导航失败只走 NavigationCompleted，若此处超时说明该事件一直未触发，通常是 WebView2 初始化被中断或页面脚本死锁）",
                    timeoutSeconds, provider.ModeName, targetUri.AbsoluteUri, useVirtualHost);
                if (_webView?.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNavCompleted;
                }
                return false;
            }

            var success = tcs.Task.Result;
            Log.Information("[WV2-LOAD] [DONE] 导航完成，结果: {Result} (模式={Mode})",
                success ? "成功" : "失败", provider.ModeName);
            if (!success)
            {
                Log.Error("[WV2-LOAD] [ERR] 前端页面加载失败，模式: {Mode}", provider.ModeName);
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WV2-LOAD-ERR] [ERR] 加载前端资源失败 (模式: {Mode})", provider.ModeName);
            return false;
        }
    }

    /// <summary>
    /// 注册 WebResourceRequested 事件拦截（B 模式：嵌入资源）
    /// </summary>
    private void RegisterWebResourceRequested(IFrontendResourceProvider provider, string hostName)
    {
        if (_webView?.CoreWebView2 == null) return;

        // 注册多层过滤器，确保所有深度的路径都能被拦截
        // WebView2 的 * 通配符不跨路径分隔符，所以需要多层
        // 注意：统一用 http:// 协议（和上面导航的协议保持一致），避免协议不匹配导致拦截器不生效
        var filters = new[]
        {
            $"http://{hostName}",
            $"http://{hostName}/",
            $"http://{hostName}/*",
            $"http://{hostName}/*/*",
            $"http://{hostName}/*/*/*",
            $"http://{hostName}/*/*/*/*",
        };

        foreach (var filter in filters)
        {
            _webView.CoreWebView2.AddWebResourceRequestedFilter(
                filter,
                CoreWebView2WebResourceContext.All);
        }

        _webView.CoreWebView2.WebResourceRequested += async (sender, args) =>
        {
            try
            {
                await HandleWebResourceRequestedAsync(args, provider, hostName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理 WebResourceRequested 时出错: {Uri}", args.Request?.Uri);
            }
        };

        Log.Information("[API] WebResourceRequested 拦截已注册 ({Count} 个过滤器): http://{HostName}", filters.Length, hostName);
    }

    /// <summary>
    /// 处理 WebResourceRequested 事件
    /// </summary>
    /// <remarks>P1 修复：从同步方法改为异步方法，消除 GetAwaiter().GetResult() 死锁风险</remarks>
    private async Task HandleWebResourceRequestedAsync(
        CoreWebView2WebResourceRequestedEventArgs args,
        IFrontendResourceProvider provider,
        string hostName)
    {
        var request = args.Request;
        if (request == null) return;

        // 只处理 GET 请求
        if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            return;

        var uri = request.Uri;
        if (string.IsNullOrEmpty(uri)) return;

        // 解析相对路径
        var baseUri = $"http://{hostName}";
        if (!uri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase))
            return;

        var relativePath = uri[baseUri.Length..];
        if (string.IsNullOrEmpty(relativePath) || relativePath == "/")
            relativePath = "/index.html";

        Log.Information("[MSG] WebResource 请求: {Path}", relativePath);

        try
        {
            // 获取资源流（P1 修复：使用 await 替代 GetAwaiter().GetResult()，消除同步阻塞死锁风险）
            using var resourceStream = await provider.GetResourceAsync(relativePath);
            if (resourceStream == null)
            {
                Log.Warning("[ERR] 资源未找到: {Path}", relativePath);
                args.Response = _webView!.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 404, "Not Found", "Content-Type: text/plain");
                return;
            }

            // 将流读到内存中，避免 ZipArchive 流的生命周期问题
            var memoryStream = new MemoryStream();
            resourceStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            // 获取 MIME 类型
            var mimeType = provider.GetMimeType(relativePath);

            // 构造响应头
            var headers = $"Content-Type: {mimeType}\r\nContent-Length: {memoryStream.Length}\r\nCache-Control: public, max-age=3600\r\nAccess-Control-Allow-Origin: *";

            // 构造响应
            args.Response = _webView!.CoreWebView2.Environment.CreateWebResourceResponse(
                memoryStream,
                200,
                "OK",
                headers);

            Log.Information("[OK] 嵌入资源响应: {Path} ({MimeType}, {Size} bytes)", relativePath, mimeType, memoryStream.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 处理资源请求失败: {Path}", relativePath);
            args.Response = _webView!.CoreWebView2.Environment.CreateWebResourceResponse(
                null, 500, "Internal Server Error", "Content-Type: text/plain");
        }
    }

    /// <summary>
    /// 页面导航完成时触发，记录导航结果（桥接脚本已由 AddScriptToExecuteOnDocumentCreatedAsync 自动注入）
    /// </summary>
    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_webView?.CoreWebView2 == null) return;

        if (e.IsSuccess)
        {
            Log.Information("[LOG] 页面导航完成（成功），桥接脚本已由 AddScript 自动注入");
        }
        else
        {
            // 导航失败用 Error 级别，确保在日志中可见
            Log.Error("[WV2-NAV] [ERR] 页面导航失败: IsSuccess={Success}, WebErrorStatus={Status}",
                e.IsSuccess, e.WebErrorStatus);
        }
    }

    /// <summary>
    /// 生成 JS 端桥接初始化脚本
    /// </summary>
    private static string GenerateBridgeInitScript()
    {
        return @"
(function() {
    if (window.__msmc_bridge__) {
        return;
    }

    const pendingRequests = new Map();
    let requestIdCounter = 0;
    const eventListeners = new Map();

    function generateId() {
        return 'cs_req_' + (++requestIdCounter) + '_' + Date.now();
    }

    function postMessage(message) {
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
            window.chrome.webview.postMessage(message);
        }
    }

    window.__msmc_bridge__ = {
        invoke: function(action, payload) {
            return new Promise((resolve, reject) => {
                const id = generateId();
                const timeout = setTimeout(() => {
                    pendingRequests.delete(id);
                    reject(new Error('Request timeout: ' + action));
                }, 30000);

                pendingRequests.set(id, { resolve, reject, timeout });

                postMessage({
                    type: 'request',
                    id: id,
                    action: action,
                    payload: payload,
                    timestamp: Date.now()
                });
            });
        },

        sendEvent: function(action, payload) {
            postMessage({
                type: 'event',
                action: action,
                payload: payload,
                timestamp: Date.now()
            });
        },

        on: function(action, handler) {
            if (!eventListeners.has(action)) {
                eventListeners.set(action, []);
            }
            eventListeners.get(action).push(handler);
            return () => {
                const listeners = eventListeners.get(action);
                if (listeners) {
                    const idx = listeners.indexOf(handler);
                    if (idx > -1) listeners.splice(idx, 1);
                }
            };
        },

        log: function(message) {
            postMessage({
                type: 'log',
                action: 'log',
                payload: message,
                timestamp: Date.now()
            });
        }
    };

    window.chrome.webview.addEventListener('message', function(event) {
        const data = event.data;
        if (!data || !data.type) return;

        switch (data.type) {
            case 'response':
                const pending = pendingRequests.get(data.id);
                if (pending) {
                    clearTimeout(pending.timeout);
                    pendingRequests.delete(data.id);
                    if (data.success) {
                        pending.resolve(data.payload);
                    } else {
                        pending.reject(new Error(data.error || 'Unknown error'));
                    }
                }
                break;

            case 'event':
                const listeners = eventListeners.get(data.action);
                if (listeners) {
                    listeners.forEach(fn => {
                        try { fn(data.payload); } catch (e) { console.error('Event handler error:', e); }
                    });
                }
                break;

            case 'request':
                console.warn('Unsupported request from C# to JS (not implemented yet)');
                break;

            case 'log':
                console.log('[C#]', data.payload);
                break;
        }
    });

    console.log('[MSMC Bridge] JS端桥接初始化完成');
})();
";
    }

    /// <summary>
    /// 处理来自 JS 的消息
    /// </summary>
    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (_disposed) return;

        try
        {
            var json = e.WebMessageAsJson;
            Log.Information("[WV2-MSG] [MSG] 收到 JS 消息 (原始 JSON 长度: {Len})", json?.Length ?? 0);
            Log.Debug("[WV2-MSG] [MSG] 消息内容: {Json}", json);

            var message = JsonSerializer.Deserialize<BridgeMessage>(json, JsonOptions);

            if (message == null)
            {
                Log.Warning("[WV2-MSG] [WARN] 收到无效的桥接消息: {Json}", json);
                return;
            }

            Log.Information("[WV2-MSG] [LOG] 消息类型: {Type}, Action: {Action}, ID: {Id}",
                message.Type, message.Action, message.Id ?? "(无)");

            switch (message.Type)
            {
                case BridgeMessageType.Request:
                    Log.Information("[WV2-MSG] [REFRESH] 处理请求: {Action}", message.Action);
                    await HandleRequestAsync(message);
                    break;

                case BridgeMessageType.Response:
                    Log.Information("[WV2-MSG] [MSG] 处理响应: {Action}", message.Action);
                    HandleResponse(message);
                    break;

                case BridgeMessageType.Event:
                    Log.Information("[WV2-MSG] [DONE] 处理事件: {Action}", message.Action);
                    HandleJsEvent(message);
                    break;

                case BridgeMessageType.Log:
                    Log.Information("[WV2-JS] [MSG] {Payload}", message.Payload);
                    break;

                default:
                    Log.Warning("[WV2-MSG] [WARN] 未知的消息类型: {Type}", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WV2-MSG-ERR] [ERR] 处理桥接消息时发生异常");
        }
    }

    /// <summary>
    /// 处理来自 JS 的请求（JS → C#）
    /// </summary>
    private async Task HandleRequestAsync(BridgeMessage message)
    {
        Log.Information("[WV2-REQ] [MSG] 处理请求: {Action} (ID={Id})", message.Action, message.Id);

        var previousRequestId = _currentRequestId.Value;
        _currentRequestId.Value = message.Id;
        try
        {
            var response = new BridgeMessage
            {
                Type = BridgeMessageType.Response,
                Id = message.Id,
                Action = message.Action,
            };

            try
            {
                if (_requestHandlers.TryGetValue(message.Action, out var handler))
                {
                    Log.Information("[WV2-REQ] [FIND] 找到处理程序: {Action}", message.Action);

                    object? result;
                    // 封送到 UI 线程执行（防止跨线程访问 WPF 控件导致的外部异常）
                    if (_uiDispatcher != null && !_uiDispatcher.CheckAccess())
                    {
                        Log.Debug("[WV2-REQ] [REFRESH] 封送到 UI 线程执行: {Action}", message.Action);
                        var tcs = new TaskCompletionSource<object?>();
                        _ = _uiDispatcher.BeginInvoke(async () =>
                        {
                            try
                            {
                                var r = await handler(message.Payload);
                                tcs.SetResult(r);
                            }
                            catch (Exception ex)
                            {
                                tcs.SetException(ex);
                            }
                        }, System.Windows.Threading.DispatcherPriority.Normal);
                        result = await tcs.Task;
                    }
                    else
                    {
                        result = await handler(message.Payload);
                    }

                    response.Payload = result;
                    response.Success = true;
                    Log.Information("[WV2-REQ] [OK] 请求处理成功: {Action}", message.Action);
                }
                else
                {
                    response.Success = false;
                    response.Error = $"未找到请求处理程序: {message.Action}";
                    Log.Warning("[WV2-REQ] [WARN] 未找到请求处理程序: {Action}", message.Action);
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Error = ex.Message;
                Log.Error(ex, "[WV2-REQ-ERR] [ERR] 处理请求 {Action} 时发生异常", message.Action);
            }

            Log.Information("[WV2-REQ] [MSG] 发送响应: {Action} (ID={Id}, Success={Success})", message.Action, message.Id, response.Success);
            await SendMessageAsync(response);
        }
        finally
        {
            _currentRequestId.Value = previousRequestId;
        }
    }

    /// <summary>
    /// 处理来自 JS 的响应（C# → JS → C#）
    /// </summary>
    private void HandleResponse(BridgeMessage message)
    {
        if (string.IsNullOrEmpty(message.Id)) return;

        if (_pendingRequests.TryRemove(message.Id, out var tcs))
        {
            if (message.Success)
            {
                tcs.SetResult(message.Payload);
            }
            else
            {
                tcs.SetException(new Exception(message.Error ?? "Unknown error"));
            }
        }
    }

    /// <summary>
    /// 处理来自 JS 的事件推送（JS → C#）
    /// </summary>
    private void HandleJsEvent(BridgeMessage message)
    {
        Log.Debug("收到 JS 事件: {Action}", message.Action);

        lock (_eventLock)
        {
            foreach (var handler in _eventSubscribers)
            {
                try
                {
                    handler(message.Action, message.Payload);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "事件处理程序执行异常: {Action}", message.Action);
                }
            }
        }
    }

    /// <inheritdoc />
    public void RegisterRequestHandler(string action, RequestHandler handler)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("动作名称不能为空", nameof(action));

        _requestHandlers[action] = handler ?? throw new ArgumentNullException(nameof(handler));
        Log.Debug("注册请求处理程序: {Action}", action);
    }

    /// <inheritdoc />
    public void UnregisterRequestHandler(string action)
    {
        _requestHandlers.TryRemove(action, out _);
        Log.Debug("注销请求处理程序: {Action}", action);
    }

    /// <inheritdoc />
    public async Task SendEventAsync(string action, object? payload = null)
    {
        if (_disposed || !IsInitialized)
        {
            Log.Warning("桥接未初始化，无法发送事件: {Action}", action);
            return;
        }

        var message = new BridgeMessage
        {
            Type = BridgeMessageType.Event,
            Action = action,
            Payload = payload,
        };

        await SendMessageAsync(message);
    }

    /// <inheritdoc />
    public Task<object?> SendRequestAsync(string action, object? payload = null, int timeoutMs = 30000)
    {
        if (_disposed || !IsInitialized)
        {
            throw new InvalidOperationException("桥接未初始化");
        }

        var requestId = $"cs_req_{Guid.NewGuid():N}";
        var tcs = new TaskCompletionSource<object?>();

        _pendingRequests[requestId] = tcs;

        // 设置超时（P2 修复：在超时回调中 Dispose CTS，防止内核对象泄漏）
        var cts = new CancellationTokenSource(timeoutMs);
        var ctsRef = cts; // 捕获用于 ContinueWith 清理

        cts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(requestId, out _))
            {
                tcs.TrySetException(new TimeoutException($"请求超时: {action}"));
            }
        });

        // 请求完成（成功或失败）后释放 CTS，防止未超时的请求泄漏内核对象
        _ = tcs.Task.ContinueWith(_ => ctsRef.Dispose(), TaskContinuationOptions.None);

        var message = new BridgeMessage
        {
            Type = BridgeMessageType.Request,
            Id = requestId,
            Action = action,
            Payload = payload,
        };

        _ = SendMessageAsync(message);

        return tcs.Task;
    }

    /// <inheritdoc />
    public void SubscribeToEvents(EventHandler handler)
    {
        lock (_eventLock)
        {
            _eventSubscribers.Add(handler);
        }
    }

    /// <inheritdoc />
    public void UnsubscribeFromEvents(EventHandler handler)
    {
        lock (_eventLock)
        {
            _eventSubscribers.Remove(handler);
        }
    }

    /// <summary>
    /// 向 JS 发送消息
    /// </summary>
    private async Task SendMessageAsync(BridgeMessage message)
    {
        if (_webView?.CoreWebView2 == null) return;

        try
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "发送桥接消息失败: {Action}", message.Action);
            await Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        Dispose();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_webView?.CoreWebView2 != null)
        {
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }

        _webView = null;
        IsInitialized = false;

        // 取消所有等待中的请求
        foreach (var kvp in _pendingRequests)
        {
            kvp.Value.TrySetCanceled();
        }
        _pendingRequests.Clear();

        _requestHandlers.Clear();
        _eventSubscribers.Clear();

        Log.Information("[EXIT] WebView2 桥接服务已关闭");
    }
}
