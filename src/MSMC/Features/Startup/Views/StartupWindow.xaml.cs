// -----------------------------------------------------------------------------
// 文件名: StartupWindow.xaml.cs
// 命名空间: io.NET.ZTR_OS.Features.Startup.Views
// 功能描述: 启动等待窗口（WebView2 版）—— 承载 React 前端启动页
//           通过桥接事件推送显示启动日志、进度条，支持主题色跟随
// 设计模式: Observer（观察主题服务变更）、消息推送模式
// -----------------------------------------------------------------------------
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using io.NET.ZTR_OS.Features.Settings.Services;
using io.NET.ZTR_OS.Features.WebView2.Frontend;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace io.NET.ZTR_OS.Features.Startup.Views;

/// <summary>
/// 启动等待窗口（WebView2 版）
/// </summary>
public partial class StartupWindow : Window
{
    private readonly IThemeService _themeService;
    private readonly Dispatcher _dispatcher;
    private bool _webViewInitialized;
    private bool _frontendLoaded;
    private readonly Queue<Action> _pendingOperations = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// 是否已失败
    /// </summary>
    public bool IsFailed { get; private set; }

    /// <summary>
    /// 是否已完成
    /// </summary>
    public bool IsCompleted { get; private set; }

    public StartupWindow(IThemeService themeService)
    {
        InitializeComponent();
        _themeService = themeService;
        _dispatcher = Dispatcher;

        _themeService.ThemeChanged += OnThemeChanged;

        Loaded += StartupWindow_Loaded;

        Log.Information("[UI] StartupWindow (WebView2) 已创建");
    }

    private async void StartupWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Information("[Startup-WV2] [BOOT] 启动窗口已加载，开始初始化 WebView2...");

        try
        {
            StartupWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, 0x02, 0x06, 0x17);

            // ──────────────────────────────────────────────────────────────
            // 【关键】显式创建 CoreWebView2Environment，带上允许 file:// 访问的 flags
            // 因为用户环境下 file:// + ES Module 会触发 Chromium 内部 CORS 拦截：
            // 所有 modulepreload / script / stylesheet 都在 8ms 内同时报 "Script error."
            // 这是 Chromium file:// origin= null 被视为跨域。
            // 加这 4 个 flag 把 file:// 的限制打开：
            //   1) --allow-file-access-from-files: file:// 页面可读取其他 file:// 文件
            //   2) --allow-file-access: 老内核兼容
            //   3) --disable-features=SplitCacheByNetworkIsolationKey: 关掉按 NIK 分 cache，
            //      这条和 NIK 有关，是 Chromium M110+ 后 file:// 下出现 CORS 问题的常见根因
            //   4) --disable-web-security: 桌面应用兜底，就算 CORS 检查也放过
            // ──────────────────────────────────────────────────────────────
            var wv2Opts = new CoreWebView2EnvironmentOptions()
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
            var wv2Env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,   // null = 用默认安装的 WebView2 Runtime
                userDataFolder: null,            // null = 用默认目录（%LOCALAPPDATA%\WebView2）
                options: wv2Opts);
            await StartupWebView.EnsureCoreWebView2Async(wv2Env);

            Log.Information("[Startup-WV2] [OK] CoreWebView2 已创建（含 --allow-file-access-from-files）");

            StartupWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            StartupWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            StartupWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            StartupWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            StartupWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            StartupWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            // 额外：允许本地对象（虽然我们没注入 WinRT 对象，但防止某些 WebView2 版本拦截 file://）
            StartupWebView.CoreWebView2.Settings.AreHostObjectsAllowed = true;

            var initScript = GenerateBridgeInitScript();
            await StartupWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript);
            Log.Information("[Startup-WV2] [API] 桥接初始化脚本已注入");

            StartupWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            StartupWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            _webViewInitialized = true;

            await LoadStartupPageAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Startup-WV2-ERR] [ERR] WebView2 初始化失败");
            _ = LoadFallbackPageAsync(ex.Message);
        }
    }

    private async Task LoadStartupPageAsync()
    {
        // 虚拟主机名必须【短 + 纯 ASCII + 不要带点】！
        // 之前用 msmc-startup.local（带 .local），WebView2 某些老版本会真去查 LLMNR/mDNS
        // 导致 30s 超时。直接用 "msmcstartup" 这种单标签名，Chromium 立刻判定为私有主机名。
        const string virtualHost = "msmcstartup";
        var provider = FrontendResourceProviderFactory.Create();

        Log.Information("[Startup-WV2-LOAD] [LOG] 前端资源模式: {Mode}, 可用: {Available}", provider.ModeName, provider.IsAvailable);

        if (!provider.IsAvailable)
        {
            Log.Warning("[Startup-WV2-LOAD] [WARN] 前端资源提供器不可用，使用兜底页面");
            _ = LoadFallbackPageAsync("前端资源未找到");
            return;
        }

        try
        {
            var basePath = await provider.GetBasePathAsync();

            // ──────────────────────────────────────────────────────────────
            // 【优先级翻转】虚拟主机名(http://) 优先，file:// 兜底
            // 因为：http 协议下 <script type=module> / <link rel=modulepreload> 的 CORS 检查 100% 通过；
            // 之前优先 file:// 时 Chromium 把所有资源都拦了（全部 "Script error."）
            // 只有当虚拟主机模式设置失败（basePath==null 即 EmbeddedResource 模式）才用原逻辑。
            // ──────────────────────────────────────────────────────────────
            bool useVirtualHost = basePath != null;   // 有真实磁盘路径就 100% 用虚拟主机
            Uri targetUri;

            if (useVirtualHost)
            {
                targetUri = new Uri($"http://{virtualHost}/startup.html");
                // 先注册映射再导航！注册晚了 URL 可能被 Chrome 识别为不存在
                StartupWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    hostName: virtualHost,
                    folderPath: basePath!,   // 已判空 basePath!=null
                    accessKind: CoreWebView2HostResourceAccessKind.Allow);
                Log.Information(
                    "[Startup-WV2-LOAD] [LINK] 虚拟主机映射已设置（http://{Host}/ → {Folder}）",
                    virtualHost, basePath);
            }
            else
            {
                // EmbeddedResource 模式：basePath=null，走原拦截逻辑，targetUri 由 provider 自己决定
                // （这时 provider 会在 Navigate 时提供，简单兜底：还是虚拟主机 URL，让 provider 的拦截器处理）
                targetUri = new Uri($"http://{virtualHost}/startup.html");
            }

            // 【兼容兜底】如果用户不想用虚拟主机（比如以后调试需要），可以保留一个开关走 file://，
            // 但我们现在已经加了 --allow-file-access-from-files，即使走 file:// 理论上也 OK，
            // 只是为了保险默认用 http 虚拟主机。
            bool forceFileProtocol = false;
            if (forceFileProtocol && basePath != null)
            {
                var directStartupPath = Path.GetFullPath(Path.Combine(basePath, "startup.html"));
                if (File.Exists(directStartupPath))
                {
                    try
                    {
                        targetUri = new UriBuilder("file", string.Empty) { Path = directStartupPath.Replace('\\', '/') }.Uri;
                        useVirtualHost = false;
                        Log.Information("[Startup-WV2-LOAD] [ALT] forceFileProtocol=true → file:// 直读 {Uri}", targetUri.AbsoluteUri);
                    }
                    catch (Exception uriEx)
                    {
                        Log.Warning(uriEx, "[Startup-WV2-LOAD] [WARN] 构造 file:// Uri 失败，保留虚拟主机模式");
                    }
                }
            }

            // 虚拟主机名模式：如果是 EmbeddedResource 模式（basePath==null，无法 folderMapping），
            // 则注册 WebResourceRequested 拦截器让 provider 自己提供资源内容；
            // 反之（Folder/ZipExtract 模式，basePath!=null），我们已经调用了
            // SetVirtualHostNameToFolderMapping，WebView2 内部会直接去 disk 读，
            // 不需要拦截器，更快更稳。
            if (useVirtualHost && basePath == null)
            {
                RegisterWebResourceRequested(provider, virtualHost);
                Log.Information("[Startup-WV2-LOAD] [API] WebResourceRequested 拦截器已注册（EmbeddedResource 模式）");
            }

            Log.Information("[Startup-WV2-LOAD] [NAV] 导航到: {Url} (模式={Mode}, 虚拟主机={UseVH})",
                targetUri.AbsoluteUri, provider.ModeName, useVirtualHost);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (e.IsSuccess)
                {
                    Log.Information("[Startup-WV2-LOAD] [OK] 导航成功 HTTP {Code} (Uri={Uri})",
                        e.HttpStatusCode, targetUri.AbsoluteUri);
                    tcs.TrySetResult(true);
                }
                else
                {
                    Log.Error(
                        "[Startup-WV2-LOAD] [ERR] NavigationCompleted 失败: Status={Status}, HTTP={Code} (Uri={Uri})。" +
                        "常见原因: ① 虚拟主机协议不匹配（已从 https 改为 http）② 文件路径被杀毒拦截 ③ startup.html 实际不存在",
                        e.WebErrorStatus, e.HttpStatusCode, targetUri.AbsoluteUri);
                    tcs.TrySetResult(false);
                }
                if (StartupWebView.CoreWebView2 != null)
                {
                    StartupWebView.CoreWebView2.NavigationCompleted -= OnCompleted;
                }
            }

            if (StartupWebView.CoreWebView2 == null)
            {
                Log.Warning("[Startup-WV2-LOAD] [WARN] StartupWebView.CoreWebView2 订阅前已为 null，跳过订阅直接走兜底");
                _ = LoadFallbackPageAsync("WebView2 已释放");
                return;
            }

            StartupWebView.CoreWebView2.NavigationCompleted += OnCompleted;
            StartupWebView.Source = targetUri;

            int timeoutSeconds = useVirtualHost ? 30 : 20;
            var timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var done = await Task.WhenAny(tcs.Task, timeout);
            if (done == timeout)
            {
                Log.Error(
                    "[Startup-WV2-LOAD] [TIME] 启动页加载超时 ({Sec}s)，模式: {Mode}, Uri: {Uri}, 虚拟主机={UseVH}；若仍超，" +
                    "请检查路径权限/中文路径/杀毒软件拦截，或直接在浏览器打开目标 startup.html 验证。",
                    timeoutSeconds, provider.ModeName, targetUri.AbsoluteUri, useVirtualHost);
                if (StartupWebView.CoreWebView2 != null)
                {
                    StartupWebView.CoreWebView2.NavigationCompleted -= OnCompleted;
                }
                _ = LoadFallbackPageAsync("启动页加载超时");
                return;
            }

            if (!tcs.Task.Result)
            {
                _ = LoadFallbackPageAsync("启动页加载失败");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Startup-WV2-LOAD-ERR] [ERR] 加载启动页失败");
            _ = LoadFallbackPageAsync(ex.Message);
        }
    }

    private void RegisterWebResourceRequested(IFrontendResourceProvider provider, string hostName)
    {
        // 和主窗口保持一致：全部 http:// 过滤器，避免协议/拦截器错位
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
            StartupWebView.CoreWebView2.AddWebResourceRequestedFilter(
                filter,
                CoreWebView2WebResourceContext.All);
        }

        StartupWebView.CoreWebView2.WebResourceRequested += async (sender, args) =>
        {
            try
            {
                await HandleWebResourceRequestedAsync(args, provider, hostName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Startup-WV2] 处理 WebResourceRequested 时出错: {Uri}", args.Request?.Uri);
            }
        };
    }

    private async Task HandleWebResourceRequestedAsync(
        CoreWebView2WebResourceRequestedEventArgs args,
        IFrontendResourceProvider provider,
        string hostName)
    {
        var request = args.Request;
        if (request == null) return;

        if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            return;

        var uri = request.Uri;
        if (string.IsNullOrEmpty(uri)) return;

        var baseUri = $"http://{hostName}";
        if (!uri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase))
            return;

        var relativePath = uri[baseUri.Length..];
        if (string.IsNullOrEmpty(relativePath) || relativePath == "/")
            relativePath = "/startup.html";

        try
        {
            using var resourceStream = await provider.GetResourceAsync(relativePath);
            if (resourceStream == null)
            {
                args.Response = StartupWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 404, "Not Found", "Content-Type: text/plain");
                return;
            }

            var memoryStream = new MemoryStream();
            resourceStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var mimeType = provider.GetMimeType(relativePath);
            var headers = $"Content-Type: {mimeType}\r\nContent-Length: {memoryStream.Length}\r\nCache-Control: public, max-age=3600\r\nAccess-Control-Allow-Origin: *";

            args.Response = StartupWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                memoryStream, 200, "OK", headers);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Startup-WV2] 处理资源请求失败: {Path}", relativePath);
            args.Response = StartupWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                null, 500, "Internal Server Error", "Content-Type: text/plain");
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            Log.Information("[Startup-WV2] [OK] 启动页导航完成");
            _frontendLoaded = true;

            SendInitEvent();

            FlushPendingOperations();
        }
        else
        {
            Log.Error("[Startup-WV2] [ERR] 启动页导航失败: {Status}", e.WebErrorStatus);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            var message = JsonSerializer.Deserialize<BridgeMessage>(json, JsonOptions);

            if (message == null) return;

            var type = message.Type?.Trim().ToLowerInvariant();
            var action = message.Action ?? string.Empty;

            switch (type)
            {
                case "event":
                    HandleJsEvent(action, message.Payload);
                    break;
                case "log":
                    Log.Information("[Startup-WV2-JS] [MSG] {Payload}", message.Payload);
                    break;
                case "request":
                    HandleJsRequest(message);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Startup-WV2] 处理 JS 消息时出错");
        }
    }

    private void HandleJsEvent(string action, object? payload)
    {
        Log.Debug("[Startup-WV2] [MSG] 收到 JS 事件: {Action}", action);

        switch (action)
        {
            case "startup:ready":
                Log.Information("[Startup-WV2] [OK] 前端启动页已就绪");
                _frontendLoaded = true;
                SendInitEvent();
                FlushPendingOperations();
                break;

            case "startup:dragMove":
                _dispatcher.InvokeAsync(() =>
                {
                    if (Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                    {
                        try { DragMove(); } catch { /* 忽略拖动失败 */ }
                    }
                }, DispatcherPriority.Input);
                break;

            case "startup:close":
                _dispatcher.InvokeAsync(Close);
                break;

            case "startup:shutdown":
                _dispatcher.InvokeAsync(() =>
                {
                    if (IsFailed)
                    {
                        Application.Current.Shutdown();
                    }
                });
                break;
        }
    }

    private void SendInitEvent()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var versionStr = version?.ToString(3) ?? "1.0.0";
        var primaryColor = _themeService.PrimaryColor.ToString();

        SendEvent("startup:init", new
        {
            version = versionStr,
            primaryColor,
        });
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_frontendLoaded && _webViewInitialized)
        {
            SendEvent("startup:themeChanged", new
            {
                primaryColor = _themeService.PrimaryColor.ToString(),
                isDarkMode = _themeService.IsDarkMode,
            });
        }
    }

    /// <summary>
    /// 追加日志
    /// </summary>
    public void AppendLog(string message, bool isError = false, bool isSuccess = false)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() => AppendLog(message, isError, isSuccess), DispatcherPriority.Background);
            return;
        }

        if (!_frontendLoaded || !_webViewInitialized)
        {
            _pendingOperations.Enqueue(() => AppendLog(message, isError, isSuccess));
            return;
        }

        SendEvent("startup:log", new
        {
            message,
            isError,
            isSuccess,
        });
    }

    /// <summary>
    /// 设置进度
    /// </summary>
    public void SetProgress(int percent, string status)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() => SetProgress(percent, status), DispatcherPriority.Background);
            return;
        }

        if (!_frontendLoaded || !_webViewInitialized)
        {
            _pendingOperations.Enqueue(() => SetProgress(percent, status));
            return;
        }

        SendEvent("startup:progress", new
        {
            percent = Math.Clamp(percent, 0, 100),
            status,
        });
    }

    /// <summary>
    /// 更新状态文本
    /// </summary>
    public void UpdateStatus(string status)
    {
        AppendLog(status);
    }

    /// <summary>
    /// 标记为失败状态
    /// </summary>
    public void MarkFailed(string errorMessage)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(() => MarkFailed(errorMessage), DispatcherPriority.Background);
            return;
        }

        IsFailed = true;

        if (!_frontendLoaded || !_webViewInitialized)
        {
            _pendingOperations.Enqueue(() => MarkFailed(errorMessage));
            return;
        }

        SendEvent("startup:failed", new
        {
            message = errorMessage,
        });
    }

    /// <summary>
    /// 标记为完成状态
    /// </summary>
    public void MarkCompleted()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.InvokeAsync(MarkCompleted, DispatcherPriority.Background);
            return;
        }

        IsCompleted = true;

        if (!_frontendLoaded || !_webViewInitialized)
        {
            _pendingOperations.Enqueue(MarkCompleted);
            return;
        }

        SendEvent("startup:completed", new
        {
            message = "[OK] 初始化完成，正在启动主界面...",
        });
    }

    private void SendEvent(string action, object? payload = null)
    {
        if (!_webViewInitialized || StartupWebView?.CoreWebView2 == null) return;

        try
        {
            var message = new
            {
                type = "event",
                action,
                payload,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            var json = JsonSerializer.Serialize(message, JsonOptions);
            StartupWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Startup-WV2] 发送事件失败: {Action}", action);
        }
    }

    /// <summary>
    /// 处理来自 JS 的请求（启动页仅支持 log:write，其余返回 not supported）
    /// </summary>
    private void HandleJsRequest(BridgeMessage message)
    {
        var action = message.Action ?? string.Empty;

        if (action == "log:write")
        {
            Log.Information("[FE-LOG] {Payload}", message.Payload);
            SendResponse(message.Id, success: true);
            return;
        }

        Log.Debug("[Startup-WV2] [MSG] 启动页不支持的请求: {Action}", action);
        SendResponse(message.Id, success: false, error: "not supported in startup window");
    }

    /// <summary>
    /// 向 JS 回送请求响应
    /// </summary>
    private void SendResponse(string? id, bool success, string? error = null)
    {
        if (!_webViewInitialized || StartupWebView?.CoreWebView2 == null) return;

        try
        {
            var message = new
            {
                type = "response",
                id,
                success,
                error,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            var json = JsonSerializer.Serialize(message, JsonOptions);
            StartupWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Startup-WV2] 发送响应失败: {Id}", id);
        }
    }

    private void FlushPendingOperations()
    {
        while (_pendingOperations.Count > 0)
        {
            var op = _pendingOperations.Dequeue();
            try { op(); }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Startup-WV2] 执行挂起操作失败");
            }
        }
    }

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
        return 'js_req_' + (++requestIdCounter) + '_' + Date.now();
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
                console.warn('Unsupported request from C# to JS');
                break;

            case 'log':
                console.log('[C#]', data.payload);
                break;
        }
    });

    console.log('[MSMC Startup Bridge] JS端桥接初始化完成');
})();
";
    }

    /// <summary>
    /// 加载兜底启动页（WebView2 前端资源不可用时的最后防线）
    /// 【修复 FTL 多层防御链】
    /// 0. 方法签名从 async void 改为 async Task，外部调用用 _ = LoadFallbackPageAsync(...) 忽略返回值
    ///    （仍然是 fire-and-forget 但允许内层 await）。
    /// 1. 若 CoreWebView2 未初始化：
    ///    - 第一层：尝试 StartupWebView.Source = new Uri("about:blank")，
    ///      WPF WebView2 控件设置 Source 属性时会自动触发 CoreWebView2 初始化，
    ///      比直接 EnsureCoreWebView2Async 兼容性更高（MS 官方 Sample 就是这么做的）。
    ///    - 然后等待 about:blank 的 NavigationCompleted，再 await EnsureCoreWebView2Async
    ///      做双保险。
    ///    - 最后捕获本地变量判 null，一行行判空 + try/catch 设置。
    /// 2. NavigateToString 前再判一次 CoreWebView2 != null，炸了就写日志不冒泡。
    ///    核心：WebView2 对 NavigateToString 要求 Core 内部状态完全一致，比 Source 属性严格得多。
    ///    如果仍然 InvalidOperationException，就退化为 Source = "data:text/html,..."（
    ///    Data URI 也是官方推荐的直传 HTML 方式，和 NavigateToString 效果一致但更宽松）。
    /// </summary>
    private async Task LoadFallbackPageAsync(string errorMessage)
    {
        try
        {
            // 0) 控件存活
            if (StartupWebView == null)
            {
                Log.Warning("[Startup-Fallback] [WARN] StartupWebView 控件已为 null，放弃加载兜底页");
                return;
            }

            // 1) 【关键修复 line 827 InvalidOperationException】如果 CoreWebView2 还没彻底初始化，
            //    不能上来就 EnsureCoreWebView2Async（用户环境下经常出现"Ensure 成功但后续访问
            //    CoreWebView2 属性仍 null + NavigateToString 直接 VerifyCoreWebView2 抛异常"的半初始化状态）。
            //    改用 MS 官方推荐的"设置 Source = about:blank 触发控件内部标准初始化流程"：
            if (StartupWebView.CoreWebView2 == null || !_webViewInitialized)
            {
                Log.Warning("[Startup-Fallback] [WARN] CoreWebView2 未初始化，用 Source=about:blank 触发初始化 (errorMsg={Error})",
                    errorMessage);
                try
                {
                    var aboutBlankTcs = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                    void OnBlankNav(object? s, CoreWebView2NavigationCompletedEventArgs e)
                    {
                        aboutBlankTcs.TrySetResult(e.IsSuccess);
                        if (StartupWebView.CoreWebView2 != null)
                            StartupWebView.CoreWebView2.NavigationCompleted -= OnBlankNav;
                    }

                    // 【兼容保护】Source 设置前 CoreWebView2 可能还没实例化；
                    // 如果设置后立刻能拿到 CoreWebView2，就订阅导航完成；
                    // 如果拿不到，给 3 秒等待 + 再 EnsureCoreWebView2Async 补保险。
                    try
                    {
                        StartupWebView.Source = new Uri("about:blank");
                    }
                    catch (Exception sourceEx)
                    {
                        Log.Warning(sourceEx, "[Startup-Fallback] [WARN] 设置 Source=about:blank 失败（非致命，尝试 EnsureCoreWebView2Async）");
                    }

                    var cwvAfterSource = StartupWebView.CoreWebView2;
                    if (cwvAfterSource != null)
                    {
                        cwvAfterSource.NavigationCompleted += OnBlankNav;
                        var navDone = await Task.WhenAny(aboutBlankTcs.Task, Task.Delay(3000));
                        if (navDone != aboutBlankTcs.Task)
                        {
                            Log.Warning("[Startup-Fallback] [WARN] about:blank 3s 未完成导航，继续尝试补 Ensure");
                            if (StartupWebView.CoreWebView2 != null)
                                StartupWebView.CoreWebView2.NavigationCompleted -= OnBlankNav;
                        }
                    }

                    // 无论 about:blank 导航有没有成功，再显式 Ensure 一次（它幂等，已初始化时瞬时返回）
                    await StartupWebView.EnsureCoreWebView2Async();
                }
                catch (Exception initEx)
                {
                    Log.Error(initEx, "[Startup-Fallback] [ERR] Source=about:blank + EnsureCoreWebView2Async 组合失败，已彻底放弃初始化 CoreWebView2，兜底页也不会显示（没有可用的 WebView2 Core）");
                    return;
                }

                // 2) 之后再捕获本地变量 cwv，逐句设置；单句失败 WARNING 继续
                var cwv = StartupWebView.CoreWebView2;
                if (cwv == null)
                {
                    Log.Warning("[Startup-Fallback] [WARN] 双重初始化后 CoreWebView2 仍为 null，彻底放弃脚本注入，只尝试显示 HTML");
                }
                else
                {
                    try
                    {
                        var initScript = GenerateBridgeInitScript();
                        if (!string.IsNullOrEmpty(initScript))
                        {
                            await cwv.AddScriptToExecuteOnDocumentCreatedAsync(initScript);
                        }
                    }
                    catch (Exception scriptEx)
                    {
                        Log.Warning(scriptEx, "[Startup-Fallback] [WARN] 注入 Bridge Init 脚本失败（不致命，继续）");
                    }

                    try { cwv.WebMessageReceived += OnWebMessageReceived; }
                    catch (Exception subEx) { Log.Warning(subEx, "[Startup-Fallback] [WARN] 订阅 WebMessageReceived 失败（不致命，继续）"); }

                    try { cwv.Settings.AreDevToolsEnabled = false; }
                    catch (Exception devEx) { Log.Warning(devEx, "[Startup-Fallback] [WARN] 关闭 DevTools 失败（不致命，继续）"); }

                    try { cwv.Settings.AreDefaultContextMenusEnabled = false; }
                    catch (Exception ctxEx) { Log.Warning(ctxEx, "[Startup-Fallback] [WARN] 关闭右键菜单失败（不致命，继续）"); }

                    _webViewInitialized = true;
                    Log.Information("[Startup-Fallback] [OK] CoreWebView2 延迟初始化完成（Source=about:blank + Ensure）");
                }
            }

            // 3) 真正写 HTML —— 优先 NavigateToString，失败再退回 Data URI Source（Source 属性对 Core 状态更宽松）
            var fallbackHtml = BuildFallbackHtml(errorMessage);
            try
            {
                if (StartupWebView.CoreWebView2 == null)
                {
                    Log.Warning("[Startup-Fallback] [WARN] NavigateToString 前 CoreWebView2 仍为 null，尝试回退 Data URI 方式");
                    throw new InvalidOperationException("CoreWebView2 is still null before NavigateToString");
                }
                StartupWebView.NavigateToString(fallbackHtml);
                Log.Information("[Startup-Fallback] [OK] NavigateToString 兜底页已触发");
            }
            catch (Exception navEx)
            {
                Log.Warning(navEx, "[Startup-Fallback] [WARN] NavigateToString 失败，回退到 Source=data:... Data URI");
                try
                {
                    // Data URI 方案：和 NavigateToString 等效显示一段 HTML，但触发的是"导航到 URL"的
                    // 常规代码路径，对 WebView2 Core 内部状态半初始化的环境兼容性更高。
                    var htmlEscaped = Uri.EscapeDataString(fallbackHtml);
                    StartupWebView.Source = new Uri($"data:text/html;charset=utf-8,{htmlEscaped}");
                    Log.Information("[Startup-Fallback] [OK] 已通过 Data URI Source 设置兜底页 HTML");
                }
                catch (Exception dataEx)
                {
                    Log.Error(dataEx, "[Startup-Fallback] [ERR] Data URI Source 也失败，已彻底放弃向 WebView2 写任何内容（Core 状态不可用）。用户界面会停留在空白或之前内容。");
                }
            }
        }
        catch (Exception ex)
        {
            // 最后一层保险：任何未预料异常都吞掉写日志，绝对不能冒泡到 UI Dispatcher 变成 FTL。
            Log.Error(ex, "[Startup-Fallback] [ERR] 兜底页整体执行失败，已放弃（不应影响后续主窗口）");
        }
    }

    /// <summary>
    /// 从 LoadFallbackPage 里把"拼 HTML 字符串"单独抽成纯函数，
    /// 避免外层大 try/catch + 方法里混合流程控制让可读性下降。
    /// </summary>
    private static string BuildFallbackHtml(string errorMessage)
    {
        return $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>MSMC 启动中</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Segoe UI', 'Microsoft YaHei UI', sans-serif;
            background: #020617;
            color: #e2e8f0;
            height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 24px;
            -webkit-user-select: none;
            user-select: none;
        }}
        .container {{
            text-align: center;
            max-width: 400px;
            width: 100%;
        }}
        .logo {{
            width: 64px;
            height: 64px;
            border-radius: 50%;
            background: #3B82F6;
            margin: 0 auto 20px;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-size: 24px;
            font-weight: bold;
        }}
        h1 {{
            font-size: 24px;
            font-weight: bold;
            margin-bottom: 4px;
        }}
        .subtitle {{
            font-size: 13px;
            color: #94a3b8;
            margin-bottom: 20px;
        }}
        .loading {{
            width: 32px;
            height: 32px;
            border: 3px solid #334155;
            border-top-color: #3B82F6;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin: 0 auto 16px;
        }}
        @keyframes spin {{
            to {{ transform: rotate(360deg); }}
        }}
        .error {{
            font-size: 12px;
            color: #fb7185;
            margin-top: 12px;
            padding: 12px;
            background: rgba(239, 68, 68, 0.1);
            border-radius: 6px;
            font-family: Consolas, monospace;
            word-break: break-all;
        }}
        .tip {{
            font-size: 11px;
            color: #64748b;
            margin-top: 12px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='logo'>[SEC]</div>
        <h1>MSMC</h1>
        <p class='subtitle'>Minecraft 服务器管理控制台</p>
        <div class='loading'></div>
        <p style='font-size: 12px; color: #94a3b8;'>正在启动...</p>
        <div class='error' id='err-msg' style='display:none;'></div>
        <p class='tip'>WebView2 前端加载失败，使用兜底模式</p>
    </div>
    <script>
        var errDiv = document.getElementById('err-msg');
        if (errDiv && {(!string.IsNullOrEmpty(errorMessage)).ToString().ToLowerInvariant()}) {{
            errDiv.style.display = 'block';
            errDiv.textContent = {JsonSerializer.Serialize(errorMessage)};
        }}
    </script>
</body>
</html>";
    }

    protected override void OnClosed(EventArgs e)
    {
        _themeService.ThemeChanged -= OnThemeChanged;

        try
        {
            if (StartupWebView?.CoreWebView2 != null)
            {
                StartupWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                StartupWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            }
        }
        catch { /* 忽略释放异常 */ }

        // P1 修复：关闭时释放 WebView2 控件，防止 Chromium 子进程残留
        try { StartupWebView.Dispose(); } catch (Exception ex) { Log.Debug(ex, "WebView2 Dispose 异常（可忽略）"); }

        base.OnClosed(e);
    }
}

internal class BridgeMessage
{
    public string? Type { get; set; }
    public string? Action { get; set; }
    public string? Id { get; set; }
    public object? Payload { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
