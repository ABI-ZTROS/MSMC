// -----------------------------------------------------------------------------
// 文件名: WebView2BridgeService.cs
// 命名空间: McServerGuard.Services.WebView2
// 功能描述: WebView2 桥接服务实现，提供 C# 与 JavaScript 双向通信
// 依赖组件: Microsoft.Web.WebView2.Wpf, System.Text.Json, Serilog
// 设计模式: 单例模式 + 消息模式 + 请求/响应模式 + 观察者模式
// -----------------------------------------------------------------------------
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using McServerGuard.Services.Frontend;
using Microsoft.Web.WebView2.Core;
using WpfWebView2 = Microsoft.Web.WebView2.Wpf.WebView2;
using Serilog;

namespace McServerGuard.Services.WebView2;

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
    };

    /// <summary>
    /// 绑定的 WebView2 控件引用
    /// </summary>
    private WpfWebView2? _webView;

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

        Log.Information("🌉 WebView2 桥接服务初始化中...");

        try
        {
            // 确保 CoreWebView2 已创建
            await _webView.EnsureCoreWebView2Async();

            // 注册消息接收事件
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // 注册导航完成事件，用于页面加载后注入桥接初始化脚本
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            IsInitialized = true;
            Log.Information("✅ WebView2 桥接服务初始化完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ WebView2 桥接服务初始化失败");
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

        Log.Information("🌐 虚拟主机映射已设置: {HostName} -> {FolderPath}", hostName, folderPath);
    }

    /// <inheritdoc />
    public async Task<bool> LoadFrontendAsync(IFrontendResourceProvider provider, string hostName)
    {
        if (_webView?.CoreWebView2 == null)
            throw new InvalidOperationException("桥接未初始化");

        if (!provider.IsAvailable)
        {
            Log.Warning("前端资源提供器不可用: {Mode}", provider.ModeName);
            return false;
        }

        Log.Information("🚀 加载前端资源 (模式: {Mode})", provider.ModeName);

        try
        {
            var basePath = await provider.GetBasePathAsync();

            if (basePath != null)
            {
                // 文件夹模式 / Zip 解压模式：用虚拟主机映射
                SetVirtualHostMapping(hostName, basePath);
            }
            else
            {
                // 嵌入资源模式：注册 WebResourceRequested 拦截
                RegisterWebResourceRequested(provider, hostName);
            }

            // 导航到主页
            var appUrl = $"https://{hostName}/index.html";
            var tcs = new TaskCompletionSource<bool>();

            // 注册一次性导航完成事件
            void OnNav(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                if (!e.IsSuccess)
                {
                    tcs.TrySetResult(false);
                }
                else
                {
                    tcs.TrySetResult(true);
                }
                _webView!.CoreWebView2.NavigationCompleted -= OnNav;
            }

            _webView.CoreWebView2.NavigationCompleted += OnNav;

            // 开始导航
            _webView.Source = new Uri(appUrl);
            Log.Information("✅ 前端页面加载中: {Url}", appUrl);

            // 等待加载完成（超时 10 秒）
            var timeout = Task.Delay(10000);
            var completed = await Task.WhenAny(tcs.Task, timeout);

            if (completed == timeout)
            {
                Log.Warning("⏰ 前端页面加载超时 (10s)，模式: {Mode}", provider.ModeName);
                _webView.CoreWebView2.NavigationCompleted -= OnNav;
                return false;
            }

            var success = tcs.Task.Result;
            if (!success)
            {
                Log.Warning("⚠️ 前端页面加载失败，模式: {Mode}", provider.ModeName);
            }

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 加载前端资源失败 (模式: {Mode})", provider.ModeName);
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
        var filters = new[]
        {
            $"https://{hostName}",
            $"https://{hostName}/",
            $"https://{hostName}/*",
            $"https://{hostName}/*/*",
            $"https://{hostName}/*/*/*",
            $"https://{hostName}/*/*/*/*",
        };

        foreach (var filter in filters)
        {
            _webView.CoreWebView2.AddWebResourceRequestedFilter(
                filter,
                CoreWebView2WebResourceContext.All);
        }

        _webView.CoreWebView2.WebResourceRequested += (sender, args) =>
        {
            try
            {
                HandleWebResourceRequested(args, provider, hostName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "处理 WebResourceRequested 时出错: {Uri}", args.Request?.Uri);
            }
        };

        Log.Information("🔌 WebResourceRequested 拦截已注册 ({Count} 个过滤器): https://{HostName}", filters.Length, hostName);
    }

    /// <summary>
    /// 处理 WebResourceRequested 事件
    /// </summary>
    private void HandleWebResourceRequested(
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
        var baseUri = $"https://{hostName}";
        if (!uri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase))
            return;

        var relativePath = uri[baseUri.Length..];
        if (string.IsNullOrEmpty(relativePath) || relativePath == "/")
            relativePath = "/index.html";

        Log.Information("📥 WebResource 请求: {Path}", relativePath);

        try
        {
            // 获取资源流
            using var resourceStream = provider.GetResourceAsync(relativePath).GetAwaiter().GetResult();
            if (resourceStream == null)
            {
                Log.Warning("❌ 资源未找到: {Path}", relativePath);
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

            Log.Information("✅ 嵌入资源响应: {Path} ({MimeType}, {Size} bytes)", relativePath, mimeType, memoryStream.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 处理资源请求失败: {Path}", relativePath);
            args.Response = _webView!.CoreWebView2.Environment.CreateWebResourceResponse(
                null, 500, "Internal Server Error", "Content-Type: text/plain");
        }
    }

    /// <summary>
    /// 页面导航完成时触发，注入桥接初始化脚本
    /// </summary>
    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_webView?.CoreWebView2 == null) return;

        Log.Information("📄 页面加载完成，注入桥接脚本...");

        // 注入 JS 端桥接对象
        var initScript = GenerateBridgeInitScript();
        _ = _webView.CoreWebView2.ExecuteScriptAsync(initScript);
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
            var message = JsonSerializer.Deserialize<BridgeMessage>(json, JsonOptions);

            if (message == null)
            {
                Log.Warning("收到无效的桥接消息: {Json}", json);
                return;
            }

            switch (message.Type)
            {
                case BridgeMessageType.Request:
                    await HandleRequestAsync(message);
                    break;

                case BridgeMessageType.Response:
                    HandleResponse(message);
                    break;

                case BridgeMessageType.Event:
                    HandleJsEvent(message);
                    break;

                case BridgeMessageType.Log:
                    Log.Debug("[JS] {Payload}", message.Payload);
                    break;

                default:
                    Log.Warning("未知的消息类型: {Type}", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "处理桥接消息时发生异常");
        }
    }

    /// <summary>
    /// 处理来自 JS 的请求（JS → C#）
    /// </summary>
    private async Task HandleRequestAsync(BridgeMessage message)
    {
        Log.Debug("收到 JS 请求: {Action} (ID={Id})", message.Action, message.Id);

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
                var result = await handler(message.Payload);
                response.Payload = result;
                response.Success = true;
            }
            else
            {
                response.Success = false;
                response.Error = $"未找到请求处理程序: {message.Action}";
                Log.Warning("未找到请求处理程序: {Action}", message.Action);
            }
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = ex.Message;
            Log.Error(ex, "处理请求 {Action} 时发生异常", message.Action);
        }

        await SendMessageAsync(response);
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

        // 设置超时
        var cts = new CancellationTokenSource(timeoutMs);
        cts.Token.Register(() =>
        {
            if (_pendingRequests.TryRemove(requestId, out _))
            {
                tcs.TrySetException(new TimeoutException($"请求超时: {action}"));
            }
        });

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

        Log.Information("👋 WebView2 桥接服务已关闭");
    }
}
