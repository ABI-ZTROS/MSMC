// -----------------------------------------------------------------------------
// 文件名: StartupWindow.xaml.cs
// 命名空间: McServerGuard.Views
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
using McServerGuard.Services;
using McServerGuard.Services.Frontend;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace McServerGuard.Views;

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

        Log.Information("🪟 StartupWindow (WebView2) 已创建");
    }

    private async void StartupWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Information("[Startup-WV2] 🚀 启动窗口已加载，开始初始化 WebView2...");

        try
        {
            StartupWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, 0x02, 0x06, 0x17);

            await StartupWebView.EnsureCoreWebView2Async();

            Log.Information("[Startup-WV2] ✅ CoreWebView2 已创建");

            StartupWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            StartupWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            StartupWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            StartupWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            StartupWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            StartupWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

            var initScript = GenerateBridgeInitScript();
            await StartupWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript);
            Log.Information("[Startup-WV2] 🔌 桥接初始化脚本已注入");

            StartupWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            StartupWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            _webViewInitialized = true;

            await LoadStartupPageAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Startup-WV2-ERR] ❌ WebView2 初始化失败");
            LoadFallbackPage(ex.Message);
        }
    }

    private async Task LoadStartupPageAsync()
    {
        const string virtualHost = "msmc-startup.local";
        var provider = FrontendResourceProviderFactory.Create();

        Log.Information("[Startup-WV2-LOAD] 📋 前端资源模式: {Mode}, 可用: {Available}", provider.ModeName, provider.IsAvailable);

        if (!provider.IsAvailable)
        {
            Log.Warning("[Startup-WV2-LOAD] ⚠️ 前端资源提供器不可用，使用兜底页面");
            LoadFallbackPage("前端资源未找到");
            return;
        }

        try
        {
            var basePath = await provider.GetBasePathAsync();

            if (basePath != null)
            {
                StartupWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    virtualHost,
                    basePath,
                    CoreWebView2HostResourceAccessKind.Allow);
                Log.Information("[Startup-WV2-LOAD] 🔗 虚拟主机映射已设置");
            }
            else
            {
                RegisterWebResourceRequested(provider, virtualHost);
                Log.Information("[Startup-WV2-LOAD] 🔌 WebResourceRequested 拦截器已注册");
            }

            var startupUrl = $"https://{virtualHost}/startup.html";
            Log.Information("[Startup-WV2-LOAD] 🧭 导航到: {Url}", startupUrl);
            StartupWebView.Source = new Uri(startupUrl);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Startup-WV2-LOAD-ERR] ❌ 加载启动页失败");
            LoadFallbackPage(ex.Message);
        }
    }

    private void RegisterWebResourceRequested(IFrontendResourceProvider provider, string hostName)
    {
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

        var baseUri = $"https://{hostName}";
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
            Log.Information("[Startup-WV2] ✅ 启动页导航完成");
            _frontendLoaded = true;

            SendInitEvent();

            FlushPendingOperations();
        }
        else
        {
            Log.Error("[Startup-WV2] ❌ 启动页导航失败: {Status}", e.WebErrorStatus);
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
                    Log.Information("[Startup-WV2-JS] 💬 {Payload}", message.Payload);
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
        Log.Debug("[Startup-WV2] 📨 收到 JS 事件: {Action}", action);

        switch (action)
        {
            case "startup:ready":
                Log.Information("[Startup-WV2] ✅ 前端启动页已就绪");
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
            message = "✅ 初始化完成，正在启动主界面...",
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

    private void LoadFallbackPage(string errorMessage)
    {
        var fallbackHtml = $@"
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
        <div class='logo'>🛡</div>
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

        StartupWebView.NavigateToString(fallbackHtml);
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
