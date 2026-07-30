// -----------------------------------------------------------------------------
// 文件名: CrashWindow.xaml.cs
// 命名空间: io.NET.ZTR_OS.Features.Startup.Views
// 功能描述: 灾难性故障独立窗口（WebView2 + React）
//           把异常的「故障点链 + 内部异常链 + 完整堆栈 + 系统环境 + 日志路径」
//           通过桥接推送到前端 CrashPage 展示，替代原来的 MessageBox
// 设计模式: 推送模式 + 桥接通信
// -----------------------------------------------------------------------------
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using io.NET.ZTR_OS.Features.WebView2.Frontend;
using io.NET.ZTR_OS.Features.WebView2.Services;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace io.NET.ZTR_OS.Features.Startup.Views;

/// <summary>
/// 灾难性故障独立窗口
/// </summary>
public partial class CrashWindow : Window
{
    private readonly Dispatcher _dispatcher;
    private bool _webViewInitialized;
    private bool _frontendLoaded;
    private readonly Queue<Action> _pendingOperations = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        // JS 端发送的是小写枚举字符串（"event"/"log"），用 CamelCase 策略匹配
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    /// <summary>
    /// 故障报告数据（在窗口构造前由 App 准备好）
    /// </summary>
    private readonly CrashReportData _report;

    /// <summary>
    /// 强制死日志路径（用于显示）
    /// </summary>
    private readonly string _forceLogPath;

    /// <summary>
    /// Serilog 日志文件路径（用于显示）
    /// </summary>
    private readonly string? _serilogLogPath;

    /// <summary>
    /// 崩溃转储路径（用于显示）
    /// </summary>
    private readonly string? _crashDumpPath;

    public CrashWindow(
        Exception exception,
        string forceLogPath,
        string? serilogLogPath = null,
        string? crashDumpPath = null,
        string? contextLabel = null)
    {
        InitializeComponent();
        _dispatcher = Dispatcher;
        _forceLogPath = forceLogPath;
        _serilogLogPath = serilogLogPath;
        _crashDumpPath = crashDumpPath;
        _report = BuildReport(exception, contextLabel);

        Loaded += CrashWindow_Loaded;

        Log.Warning("[CRASH-WIN] CrashWindow 已创建，待显示异常: {Type}: {Msg}",
            _report.Type, _report.Message);
    }

    private async void CrashWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            CrashWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, 0x0A, 0x0F, 0x1E);

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
                browserExecutableFolder: null,
                userDataFolder: null,
                options: wv2Opts);
            await CrashWebView.EnsureCoreWebView2Async(wv2Env);

            CrashWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            CrashWebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            CrashWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            CrashWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            CrashWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            CrashWebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            CrashWebView.CoreWebView2.Settings.AreHostObjectsAllowed = true;

            var initScript = GenerateBridgeInitScript();
            await CrashWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(initScript);

            CrashWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            CrashWebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            _webViewInitialized = true;

            await LoadCrashPageAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CRASH-WIN-ERR] CrashWindow WebView2 初始化失败，回退到 MessageBox");
            _ = LoadFallbackPageAsync(ex.Message);
        }
    }

    private async Task LoadCrashPageAsync()
    {
        const string virtualHost = "msmccrash";
        var provider = FrontendResourceProviderFactory.Create();

        if (!provider.IsAvailable)
        {
            _ = LoadFallbackPageAsync("前端资源未找到");
            return;
        }

        try
        {
            var basePath = await provider.GetBasePathAsync();
            Uri targetUri;

            if (basePath != null)
            {
                targetUri = new Uri($"http://{virtualHost}/crash.html");
                CrashWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    hostName: virtualHost,
                    folderPath: basePath!,
                    accessKind: CoreWebView2HostResourceAccessKind.Allow);
            }
            else
            {
                targetUri = new Uri($"http://{virtualHost}/crash.html");
                RegisterWebResourceRequested(provider, virtualHost);
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                tcs.TrySetResult(e.IsSuccess);
                if (CrashWebView.CoreWebView2 != null)
                    CrashWebView.CoreWebView2.NavigationCompleted -= OnCompleted;
            }

            if (CrashWebView.CoreWebView2 == null)
            {
                _ = LoadFallbackPageAsync("WebView2 已释放");
                return;
            }

            CrashWebView.CoreWebView2.NavigationCompleted += OnCompleted;
            CrashWebView.Source = targetUri;

            var timeout = Task.Delay(TimeSpan.FromSeconds(20));
            var done = await Task.WhenAny(tcs.Task, timeout);
            if (done == timeout)
            {
                if (CrashWebView.CoreWebView2 != null)
                    CrashWebView.CoreWebView2.NavigationCompleted -= OnCompleted;
                _ = LoadFallbackPageAsync("故障页加载超时");
            }
            else if (!tcs.Task.Result)
            {
                _ = LoadFallbackPageAsync("故障页加载失败");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CRASH-WIN-ERR] 加载故障页失败");
            _ = LoadFallbackPageAsync(ex.Message);
        }
    }

    private void RegisterWebResourceRequested(IFrontendResourceProvider provider, string hostName)
    {
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
            CrashWebView.CoreWebView2.AddWebResourceRequestedFilter(filter, CoreWebView2WebResourceContext.All);
        }

        CrashWebView.CoreWebView2.WebResourceRequested += async (sender, args) =>
        {
            try { await HandleWebResourceRequestedAsync(args, provider, hostName); }
            catch (Exception ex) { Log.Error(ex, "[CRASH-WIN] 处理 WebResourceRequested 出错"); }
        };
    }

    private async Task HandleWebResourceRequestedAsync(
        CoreWebView2WebResourceRequestedEventArgs args,
        IFrontendResourceProvider provider,
        string hostName)
    {
        var request = args.Request;
        if (request == null) return;
        if (!string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase)) return;

        var uri = request.Uri;
        if (string.IsNullOrEmpty(uri)) return;

        var baseUri = $"http://{hostName}";
        if (!uri.StartsWith(baseUri, StringComparison.OrdinalIgnoreCase)) return;

        var relativePath = uri[baseUri.Length..];
        if (string.IsNullOrEmpty(relativePath) || relativePath == "/")
            relativePath = "/crash.html";

        try
        {
            using var resourceStream = await provider.GetResourceAsync(relativePath);
            if (resourceStream == null)
            {
                args.Response = CrashWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                    null, 404, "Not Found", "Content-Type: text/plain");
                return;
            }

            var memoryStream = new MemoryStream();
            resourceStream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var mimeType = provider.GetMimeType(relativePath);
            var headers = $"Content-Type: {mimeType}\r\nContent-Length: {memoryStream.Length}\r\nCache-Control: public, max-age=3600\r\nAccess-Control-Allow-Origin: *";

            args.Response = CrashWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                memoryStream, 200, "OK", headers);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CRASH-WIN] 处理资源请求失败: {Path}", relativePath);
            args.Response = CrashWebView.CoreWebView2.Environment.CreateWebResourceResponse(
                null, 500, "Internal Server Error", "Content-Type: text/plain");
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _frontendLoaded = true;
            FlushPendingOperations();
        }
        else
        {
            Log.Error("[CRASH-WIN] 故障页导航失败: {Status}", e.WebErrorStatus);
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            var message = JsonSerializer.Deserialize<BridgeMessage>(json, JsonOptions);
            if (message == null) return;

            var action = message.Action ?? string.Empty;

            switch (message.Type)
            {
                case BridgeMessageType.Event:
                    HandleJsEvent(action);
                    break;
                case BridgeMessageType.Log:
                    Log.Information("[CRASH-WIN-JS] {Payload}", message.Payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[CRASH-WIN] 处理 JS 消息出错");
        }
    }

    private void HandleJsEvent(string action)
    {
        switch (action)
        {
            case "crash:ready":
                _frontendLoaded = true;
                SendReport();
                FlushPendingOperations();
                break;

            case "crash:exit":
                _dispatcher.InvokeAsync(() =>
                {
                    Application.Current.Shutdown(-1);
                });
                break;

            case "crash:restart":
                _dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var exePath = Environment.ProcessPath!;
                        System.Diagnostics.Process.Start(exePath);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[CRASH-WIN] 重启失败");
                    }
                    Application.Current.Shutdown(-1);
                });
                break;
        }
    }

    private void SendReport()
    {
        SendEvent("crash:report", _report);
    }

    private void SendEvent(string action, object? payload = null)
    {
        if (!_webViewInitialized || CrashWebView?.CoreWebView2 == null) return;

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
            CrashWebView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[CRASH-WIN] 发送事件失败: {Action}", action);
        }
    }

    private void FlushPendingOperations()
    {
        while (_pendingOperations.Count > 0)
        {
            var op = _pendingOperations.Dequeue();
            try { op(); }
            catch (Exception ex) { Log.Warning(ex, "[CRASH-WIN] 执行挂起操作失败"); }
        }
    }

    /// <summary>
    /// 从 Exception 构建前端可消费的故障报告
    /// </summary>
    private CrashReportData BuildReport(Exception ex, string? contextLabel)
    {
        var frames = new List<CrashFrameData>();
        var current = ex;
        while (current != null)
        {
            // 取堆栈的第一帧作为故障点位置
            var firstFrame = current.StackTrace?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? "(无堆栈帧)";
            frames.Add(new CrashFrameData
            {
                Location = current.GetType().FullName ?? current.GetType().Name,
                Source = TryExtractSourceLine(firstFrame),
                Reason = current.Message,
            });
            current = current.InnerException;
            if (current != null && frames.Count >= 20) break; // 防止异常链过长
        }

        // 反转：最外层在最前，最内层（根本原因）在最后
        frames.Reverse();

        var innerList = new List<CrashInnerData>();
        current = ex.InnerException;
        while (current != null)
        {
            innerList.Add(new CrashInnerData
            {
                Type = current.GetType().FullName ?? current.GetType().Name,
                Message = current.Message,
                Stack = current.StackTrace,
            });
            current = current.InnerException;
            if (innerList.Count >= 20) break;
        }

        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        return new CrashReportData
        {
            Type = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            Stack = ex.StackTrace ?? "(无堆栈)",
            Frames = frames,
            Inner = innerList,
            Env = new CrashEnvData
            {
                Os = Environment.OSVersion.ToString(),
                Net = Environment.Version.ToString(),
                X64 = Environment.Is64BitProcess,
                Cpu = Environment.ProcessorCount,
                Pid = Environment.ProcessId,
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                Version = version,
                BaseDir = AppContext.BaseDirectory,
            },
            ContextLabel = contextLabel,
            CrashDumpPath = _crashDumpPath,
            ForceLogPath = _forceLogPath,
            SerilogLogPath = _serilogLogPath,
        };
    }

    /// <summary>
    /// 从堆栈行中提取 "文件:行号"
    /// </summary>
    private static string TryExtractSourceLine(string stackLine)
    {
        // 形如：   at Foo.Bar() in /path/to/file.cs:line 42
        var idx = stackLine.IndexOf(" in ", StringComparison.Ordinal);
        if (idx < 0) return string.Empty;
        var tail = stackLine[(idx + 4)..];
        return tail.Trim();
    }

    private async Task LoadFallbackPageAsync(string errorMessage)
    {
        try
        {
            if (CrashWebView == null) return;

            if (CrashWebView.CoreWebView2 == null || !_webViewInitialized)
            {
                try { CrashWebView.Source = new Uri("about:blank"); } catch { }
                try { await CrashWebView.EnsureCoreWebView2Async(); } catch { }
            }

            var html = BuildFallbackHtml(errorMessage);
            try
            {
                if (CrashWebView.CoreWebView2 == null)
                    throw new InvalidOperationException("CoreWebView2 is null");
                CrashWebView.NavigateToString(html);
            }
            catch
            {
                try
                {
                    var escaped = Uri.EscapeDataString(html);
                    CrashWebView.Source = new Uri($"data:text/html;charset=utf-8,{escaped}");
                }
                catch (Exception dataEx)
                {
                    Log.Error(dataEx, "[CRASH-WIN] Data URI 也失败");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CRASH-WIN] 兜底页整体执行失败");
        }
    }

    private string BuildFallbackHtml(string errorMessage)
    {
        var ex = _report;
        return $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
<meta charset='UTF-8'>
<title>MSMC 灾难性故障</title>
<style>
* {{ margin:0; padding:0; box-sizing:border-box; }}
body {{ font-family:'Consolas','Microsoft YaHei UI',monospace; background:#0a0f1e; color:#e2e8f0; padding:24px; overflow:auto; }}
h1 {{ color:#f87171; font-size:18px; margin-bottom:8px; }}
.sub {{ color:#94a3b8; font-size:12px; margin-bottom:16px; }}
.card {{ background:#0f172a; border:1px solid #334155; border-left:3px solid #f87171; border-radius:6px; padding:14px; margin-bottom:12px; }}
.label {{ color:#64748b; font-size:11px; }}
.value {{ color:#cbd5e1; font-size:12px; word-break:break-word; }}
pre {{ background:#020617; padding:12px; border-radius:6px; font-size:11px; color:#94a3b8; white-space:pre-wrap; word-break:break-word; max-height:300px; overflow:auto; }}
.path {{ color:#fbbf24; font-size:11px; word-break:break-all; }}
button {{ padding:8px 16px; background:#2563eb; color:#fff; border:none; border-radius:6px; cursor:pointer; font-size:12px; margin-right:8px; }}
</style>
</head>
<body>
<h1>⚠ MSMC 灾难性故障（兜底模式）</h1>
<div class='sub'>前端故障页加载失败 ({System.Net.WebUtility.HtmlEncode(errorMessage)})，使用文本兜底模式。</div>

<div class='card'>
<div class='label'>异常类型</div>
<div class='value'>{System.Net.WebUtility.HtmlEncode(ex.Type)}</div>
<div class='label' style='margin-top:8px;'>异常消息</div>
<div class='value' style='color:#fca5a5;'>{System.Net.WebUtility.HtmlEncode(ex.Message)}</div>
</div>

<div class='card'>
<div class='label'>故障点链（{ex.Frames.Count} 帧）</div>
{string.Join("", ex.Frames.Select((f, i) => $"<div style='margin-top:6px;'><span class='label'>[{i}]</span> <span class='value' style='color:#cbd5e1;'>{System.Net.WebUtility.HtmlEncode(f.Location)}</span>{(!string.IsNullOrEmpty(f.Source) ? $" <span class='label'>({System.Net.WebUtility.HtmlEncode(f.Source)})</span>" : "")}<br/><span class='label'>原因：</span><span class='value' style='color:#fbbf24;'>{System.Net.WebUtility.HtmlEncode(f.Reason)}</span></div>"))}
</div>

<div class='card'>
<div class='label'>完整堆栈</div>
<pre>{System.Net.WebUtility.HtmlEncode(ex.Stack)}</pre>
</div>

<div class='card'>
<div class='label'>日志文件</div>
<div class='path'>强制死日志: {System.Net.WebUtility.HtmlEncode(_forceLogPath)}</div>
<div class='path'>Serilog: {System.Net.WebUtility.HtmlEncode(_serilogLogPath ?? "(无)")}</div>
<div class='path'>崩溃转储: {System.Net.WebUtility.HtmlEncode(_crashDumpPath ?? "(无)")}</div>
</div>

<div style='margin-top:16px;'>
<button onclick='window.__msmc_bridge__ && window.__msmc_bridge__.sendEvent(""crash:exit"", {{}})'>退出</button>
<button onclick='window.__msmc_bridge__ && window.__msmc_bridge__.sendEvent(""crash:restart"", {{}})'>重启</button>
</div>
</body>
</html>";
    }

    private static string GenerateBridgeInitScript()
    {
        return @"
(function() {
    if (window.__msmc_bridge__) return;
    const eventListeners = new Map();
    function postMessage(message) {
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
            window.chrome.webview.postMessage(message);
        }
    }
    window.__msmc_bridge__ = {
        invoke: function(action, payload) {
            return new Promise((resolve, reject) => {
                postMessage({ type:'request', action:action, payload:payload, timestamp:Date.now() });
                // 简化：不等待 response，直接 resolve
                setTimeout(() => resolve(null), 50);
            });
        },
        sendEvent: function(action, payload) {
            postMessage({ type:'event', action:action, payload:payload, timestamp:Date.now() });
        },
        on: function(action, handler) {
            if (!eventListeners.has(action)) eventListeners.set(action, []);
            eventListeners.get(action).push(handler);
        },
        log: function(message) {
            postMessage({ type:'log', action:'log', payload:message, timestamp:Date.now() });
        }
    };
    window.chrome.webview.addEventListener('message', function(event) {
        const data = event.data;
        if (!data || !data.type) return;
        if (data.type === 'event') {
            const listeners = eventListeners.get(data.action);
            if (listeners) listeners.forEach(fn => { try { fn(data.payload); } catch(e) {} });
        }
    });
    console.log('[MSMC Crash Bridge] JS 端桥接初始化完成');
})();
";
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            if (CrashWebView?.CoreWebView2 != null)
            {
                CrashWebView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                CrashWebView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            }
        }
        catch { /* 忽略 */ }
        base.OnClosed(e);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 故障报告数据模型（与前端 CrashPage.tsx 的 CrashReport 接口一一对应）
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class CrashReportData
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Stack { get; set; } = string.Empty;
    public List<CrashFrameData> Frames { get; set; } = new();
    public List<CrashInnerData> Inner { get; set; } = new();
    public CrashEnvData Env { get; set; } = new();
    public string? ContextLabel { get; set; }
    public string? CrashDumpPath { get; set; }
    public string? ForceLogPath { get; set; }
    public string? SerilogLogPath { get; set; }
}

internal sealed class CrashFrameData
{
    public string Location { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string Reason { get; set; } = string.Empty;
}

internal sealed class CrashInnerData
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Stack { get; set; }
}

internal sealed class CrashEnvData
{
    public string Os { get; set; } = string.Empty;
    public string Net { get; set; } = string.Empty;
    public bool X64 { get; set; }
    public int Cpu { get; set; }
    public int Pid { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string BaseDir { get; set; } = string.Empty;
}
