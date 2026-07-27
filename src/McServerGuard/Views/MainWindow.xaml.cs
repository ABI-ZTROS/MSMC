// -----------------------------------------------------------------------------
// 文件名: MainWindow.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 主窗口代码隐藏类 - WebView2 重构版
//           实现自定义标题栏交互、WebView2 初始化与桥接服务绑定
// 依赖组件: PresentationFramework, MaterialDesignThemes,
//           MahApps.Metro.IconPacks, Microsoft.Web.WebView2.Wpf
// 设计模式: 代码隐藏模式
// -----------------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Services.Colors;
using McServerGuard.Services.Frontend;
using McServerGuard.Services.WebView2;
using McServerGuard.ViewModels;
using McServerGuard.Constants;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace McServerGuard.Views;

/// <summary>
/// 主窗口代码隐藏类 - WebView2 重构版
/// 负责自定义标题栏交互、WebView2 初始化与桥接服务绑定
/// </summary>
public partial class MainWindow : Window
{
    private readonly IThemeService _themeService;
    private readonly IWebView2BridgeService _bridgeService;
    private MainViewModel? _vm;
    private bool _isClosing;

    public MainWindow()
    {
        Log.Information("🏗️ MainWindow (WebView2) 正在初始化...");
        InitializeComponent();

        _themeService = App.Services.GetRequiredService<IThemeService>();
        _bridgeService = App.Services.GetRequiredService<IWebView2BridgeService>();

        Loaded += MainWindow_Loaded;
        DataContextChanged += MainWindow_DataContextChanged;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;

        Log.Information("✅ MainWindow (WebView2) 初始化完成");
    }

    // 窗口 Loaded 事件处理：延迟初始化 WebView2 和桥接服务
    // 使用 ApplicationIdle 优先级，确保窗口框架先完成渲染再启动重量级 WebView2 初始化，
    // 避免用户看到"白屏冻结"——窗口先显示，然后内容逐步加载
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Information("[UI-1] 🌐 MainWindow_Loaded 触发，延迟初始化 WebView2（等待窗口渲染完成）...");

        Dispatcher.BeginInvoke(async () =>
        {
            Log.Information("[UI-2] 🔧 初始化 WebView2 桥接服务...");
            try
            {
                await _bridgeService.InitializeAsync(MainWebView);
                Log.Information("[UI-3] ✅ WebView2 桥接服务初始化完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UI-ERR] ❌ WebView2 桥接服务初始化失败");
                MessageBox.Show($"WebView2 初始化失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // P1 修复：将 RegisterBridgeApis / TryLoadFrontendWithFallbackAsync 也包裹在 try-catch 中
            // 原 async void lambda 中这两段缺乏异常保护，未处理异常会在 Dispatcher 上导致进程不稳定
            try
            {
                Log.Information("[UI-4] 📡 注册桥接 API 处理程序...");
                RegisterBridgeApis();
                Log.Information("[UI-5] ✅ 桥接 API 注册完成");

                const string virtualHost = "msmc.local";
                Log.Information("[UI-6] 🔍 开始加载前端，目标主机: {Host}", virtualHost);
                var loaded = await TryLoadFrontendWithFallbackAsync(virtualHost);
                if (!loaded)
                {
                    Log.Warning("[UI-7] ⚠️ 所有前端加载方式都失败，加载内置测试页面");
                    LoadTestPage();
                }

                Log.Information("[UI-8] ✅ WebView2 初始化全部完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UI-ERR] ❌ 前端加载或 API 注册失败");
            }

            // WebView2 就绪后，延迟启动后台服务（避免与前端加载竞争 CPU）
            _ = Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    _vm?.DetectionPage.DeferStart();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[UI-ERR] ❌ 延迟启动后台服务失败");
                }
            }, DispatcherPriority.ApplicationIdle);
        }, DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// 按优先级尝试加载前端：B模式 -> C模式
    /// </summary>
    private async Task<bool> TryLoadFrontendWithFallbackAsync(string virtualHost)
    {
        Log.Information("[UI-LOAD-1] 🏭 创建前端资源提供器工厂...");

        // 1. 先尝试用工厂选择最优模式（B模式优先）
        var provider = FrontendResourceProviderFactory.Create();
        Log.Information("[UI-LOAD-2] 📋 工厂选择模式: {Mode}, 是否可用: {Available}", provider.ModeName, provider.IsAvailable);

        if (provider.IsAvailable)
        {
            Log.Information("[UI-LOAD-3] 🚀 尝试用模式 {Mode} 加载前端...", provider.ModeName);
            var loaded = await _bridgeService.LoadFrontendAsync(provider, virtualHost);
            Log.Information("[UI-LOAD-4] 📊 模式 {Mode} 加载结果: {Result}", provider.ModeName, loaded ? "成功" : "失败");

            if (loaded)
            {
                Log.Information("[UI-LOAD-5] ✅ 前端加载成功 (模式: {Mode})", provider.ModeName);
                return true;
            }
            Log.Warning("[UI-LOAD-6] ⚠️ 模式 {Mode} 加载失败，尝试降级...", provider.ModeName);
        }

        // 2. 如果 B 模式失败，显式尝试 C 模式（Zip 解压）
        if (provider.ModeName != "ZipExtract")
        {
            try
            {
                Log.Information("[UI-LOAD-7] 🔄 创建 ZipExtract 提供器（C模式兜底）...");
                var zipProvider = new ZipExtractResourceProvider();
                Log.Information("[UI-LOAD-8] 📋 ZipExtract 模式是否可用: {Available}", zipProvider.IsAvailable);

                if (zipProvider.IsAvailable)
                {
                    Log.Information("[UI-LOAD-9] 🚀 尝试用 C 模式 (Zip 解压) 加载前端...");
                    var loaded = await _bridgeService.LoadFrontendAsync(zipProvider, virtualHost);
                    Log.Information("[UI-LOAD-10] 📊 C 模式加载结果: {Result}", loaded ? "成功" : "失败");

                    if (loaded)
                    {
                        Log.Information("[UI-LOAD-11] ✅ 前端加载成功 (模式: {Mode})", zipProvider.ModeName);
                        return true;
                    }
                    Log.Warning("[UI-LOAD-12] ⚠️ C 模式也加载失败");
                }
                else
                {
                    Log.Warning("[UI-LOAD-13] ⚠️ C 模式不可用（zip 资源不存在）");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[UI-LOAD-ERR] ❌ C 模式加载异常");
            }
        }

        Log.Warning("[UI-LOAD-END] ❌ 所有加载方式都失败了");
        return false;
    }

    /// <summary>
    /// 加载内置测试页面（用于前端未构建时的桥接验证）
    /// </summary>
    private void LoadTestPage()
    {
        var testHtml = GenerateTestHtml();
        MainWebView.NavigateToString(testHtml);
    }

    /// <summary>
    /// 生成测试用 HTML 页面
    /// </summary>
    private static string GenerateTestHtml()
    {
        return @"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>MSMC - WebView2 Bridge Test</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: #0f172a;
            color: #e2e8f0;
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }
        .container {
            max-width: 600px;
            width: 100%;
            background: #1e293b;
            border-radius: 12px;
            padding: 32px;
            box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
        }
        h1 {
            font-size: 24px;
            font-weight: 700;
            margin-bottom: 8px;
            background: linear-gradient(135deg, #60a5fa, #a78bfa);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
        }
        .subtitle {
            color: #94a3b8;
            margin-bottom: 24px;
            font-size: 14px;
        }
        .status-card {
            background: #334155;
            border-radius: 8px;
            padding: 16px;
            margin-bottom: 16px;
            border-left: 4px solid #22c55e;
        }
        .status-card.error { border-left-color: #ef4444; }
        .status-card h3 {
            font-size: 14px;
            font-weight: 600;
            margin-bottom: 4px;
            color: #e2e8f0;
        }
        .status-card p {
            font-size: 13px;
            color: #94a3b8;
        }
        .status-dot {
            display: inline-block;
            width: 8px;
            height: 8px;
            border-radius: 50%;
            margin-right: 8px;
            background: #22c55e;
            animation: pulse 2s infinite;
        }
        .status-dot.error { background: #ef4444; }
        @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
        }
        .test-section {
            margin-top: 24px;
            padding-top: 24px;
            border-top: 1px solid #475569;
        }
        .test-section h2 {
            font-size: 16px;
            margin-bottom: 16px;
            color: #e2e8f0;
        }
        .test-btn {
            background: #3b82f6;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            cursor: pointer;
            font-size: 14px;
            font-weight: 500;
            transition: all 0.2s;
            margin-right: 8px;
            margin-bottom: 8px;
        }
        .test-btn:hover { background: #2563eb; transform: translateY(-1px); }
        .test-btn:active { transform: translateY(0); }
        .test-btn.secondary { background: #475569; }
        .test-btn.secondary:hover { background: #64748b; }
        .log-area {
            background: #0f172a;
            border-radius: 6px;
            padding: 12px;
            margin-top: 16px;
            font-family: 'Consolas', 'Monaco', monospace;
            font-size: 12px;
            max-height: 200px;
            overflow-y: auto;
        }
        .log-entry { padding: 4px 0; color: #94a3b8; }
        .log-entry .time { color: #64748b; margin-right: 8px; }
        .log-entry .success { color: #22c55e; }
        .log-entry .error { color: #f87171; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>MSMC WebView2 Bridge</h1>
        <p class='subtitle'>Minecraft Server Management Console - Web UI 桥接测试</p>

        <div class='status-card' id='bridge-status'>
            <h3><span class='status-dot'></span>桥接状态</h3>
            <p id='bridge-text'>正在检测桥接连接...</p>
        </div>

        <div class='status-card' id='app-status'>
            <h3>应用信息</h3>
            <p id='app-info'>等待接收应用初始化事件...</p>
        </div>

        <div class='test-section'>
            <h2>通信测试</h2>
            <button class='test-btn' onclick='testPing()'>测试 Ping</button>
            <button class='test-btn secondary' onclick='testGetTime()'>获取服务器时间</button>
            <button class='test-btn secondary' onclick='testSendEvent()'>发送事件到 C#</button>
            <div class='log-area' id='log-area'></div>
        </div>
    </div>

    <script>
        function log(message, type = 'info') {
            const logArea = document.getElementById('log-area');
            const time = new Date().toLocaleTimeString();
            const entry = document.createElement('div');
            entry.className = 'log-entry';
            entry.innerHTML = '<span class=""time"">[' + time + ']</span>' +
                              '<span class=""' + type + '"">' + message + '</span>';
            logArea.appendChild(entry);
            logArea.scrollTop = logArea.scrollHeight;
        }

        function setBridgeStatus(connected, message) {
            const card = document.getElementById('bridge-status');
            const dot = card.querySelector('.status-dot');
            const text = document.getElementById('bridge-text');
            if (connected) {
                card.classList.remove('error');
                dot.classList.remove('error');
            } else {
                card.classList.add('error');
                dot.classList.add('error');
            }
            text.textContent = message;
        }

        // 检测桥接是否可用
        function checkBridge() {
            if (window.__msmc_bridge__) {
                setBridgeStatus(true, '桥接已连接，JS 端桥接对象已就绪');
                log('桥接对象已就绪', 'success');
                return true;
            }
            setBridgeStatus(false, '桥接对象未找到');
            log('桥接对象未找到', 'error');
            return false;
        }

        // 测试 Ping
        async function testPing() {
            if (!checkBridge()) return;
            log('发送 ping 请求...');
            try {
                const result = await window.__msmc_bridge__.invoke('ping', { hello: 'from JS' });
                log('Ping 成功: ' + JSON.stringify(result), 'success');
            } catch (e) {
                log('Ping 失败: ' + e.message, 'error');
            }
        }

        // 测试获取服务器时间
        async function testGetTime() {
            if (!checkBridge()) return;
            log('请求服务器时间...');
            try {
                const result = await window.__msmc_bridge__.invoke('app:getTime');
                log('服务器时间: ' + result, 'success');
            } catch (e) {
                log('获取时间失败: ' + e.message, 'error');
            }
        }

        // 测试发送事件
        function testSendEvent() {
            if (!checkBridge()) return;
            window.__msmc_bridge__.sendEvent('test:event', { test: true, value: 42 });
            log('已发送测试事件到 C#', 'success');
        }

        // 监听应用就绪事件
        if (window.__msmc_bridge__) {
            window.__msmc_bridge__.on('app:ready', function(data) {
                log('收到 app:ready 事件', 'success');
                document.getElementById('app-info').textContent =
                    '版本: ' + data.version + ' | 管理员: ' + (data.isAdmin ? '是' : '否');
            });
        }

        // 页面加载后延迟检测桥接
        setTimeout(checkBridge, 500);
        setTimeout(checkBridge, 1000);
        setTimeout(checkBridge, 2000);

        log('页面加载完成，等待桥接初始化...');
    </script>
</body>
</html>";
    }

    /// <summary>
    /// 注册桥接 API 处理程序
    /// </summary>
    private void RegisterBridgeApis()
    {
        // Ping - 基础连通性测试
        _bridgeService.RegisterRequestHandler("ping", payload =>
        {
            Log.Debug("🏓 收到 Ping 请求: {Payload}", payload);
            return Task.FromResult<object?>(new
            {
                pong = true,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                message = "Hello from C#!"
            });
        });

        // 前端日志上报：接收 JS 端的 error / warning 日志，写入 Serilog
        _bridgeService.RegisterRequestHandler("log:write", payload =>
        {
            try
            {
                var json = payload as JsonElement?;
                if (json == null)
                {
                    return Task.FromResult<object?>(new { ok = false, reason = "invalid payload" });
                }

                var el = json.Value;
                var level = el.TryGetProperty("level", out var lvlProp) ? lvlProp.GetString() ?? "Information" : "Information";
                var message = el.TryGetProperty("message", out var msgProp) ? msgProp.GetString() ?? "" : "";
                var stack = el.TryGetProperty("stack", out var stackProp) ? stackProp.GetString() ?? "" : "";
                var url = el.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                var ua = el.TryGetProperty("ua", out var uaProp) ? uaProp.GetString() ?? "" : "";

                // 前端来源统一加 [FE] 前缀，便于在日志中检索
                var fullMessage = string.IsNullOrEmpty(url)
                    ? message
                    : $"{message} | url={url}";

                var levelUpper = level?.Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(stack))
                {
                    fullMessage += $"\n--- stack ---\n{stack}";
                }

                switch (levelUpper)
                {
                    case "ERROR":
                    case "ERR":
                    case "FATAL":
                        Log.Error("[FE-LOG] {Message}", fullMessage);
                        break;
                    case "WARNING":
                    case "WARN":
                        Log.Warning("[FE-LOG] {Message}", fullMessage);
                        break;
                    case "DEBUG":
                        Log.Debug("[FE-LOG] {Message}", fullMessage);
                        break;
                    default:
                        Log.Information("[FE-LOG] {Message}", fullMessage);
                        break;
                }

                return Task.FromResult<object?>(new { ok = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[FE-LOG] 处理前端日志上报失败");
                return Task.FromResult<object?>(new { ok = false, reason = ex.Message });
            }
        });

        // 获取当前时间
        _bridgeService.RegisterRequestHandler("app:getTime", _ =>
        {
            return Task.FromResult<object?>(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        });

        // 获取应用信息
        _bridgeService.RegisterRequestHandler("app:getInfo", _ =>
        {
            return Task.FromResult<object?>(new
            {
                version = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                name = "MSMC",
                fullName = "Minecraft Server Management Console",
            });
        });

        // 获取应用就绪状态（JS 端主动拉取，避免时序问题）
        _bridgeService.RegisterRequestHandler("app:getReadyState", _ =>
        {
            return Task.FromResult<object?>(new
            {
                version = typeof(App).Assembly.GetName().Version?.ToString() ?? "0.0.0",
                isAdmin = _vm?.IsAdminMode ?? false,
                theme = new
                {
                    mode = _themeService.IsDarkMode ? "dark" : "light",
                    primaryColor = _themeService.PrimaryColor.ToString(),
                },
                statusMessage = _vm?.StatusMessage ?? string.Empty,
            });
        });

        // === 系统监控相关 API ===

        // 获取当前系统指标快照
        _bridgeService.RegisterRequestHandler("systemMonitor:getMetrics", _ =>
        {
            var metrics = _vm?.MonitorPage?.CurrentMetrics;
            if (metrics == null)
            {
                return Task.FromResult<object?>(new
                {
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    cpuUsagePercent = 0.0,
                    memoryUsagePercent = 0.0,
                    diskUsagePercent = 0.0,
                    totalMemoryBytes = 0L,
                    usedMemoryBytes = 0L,
                    diskTotalBytes = 0L,
                    diskUsedBytes = 0L,
                    diskName = string.Empty,
                    totalThreadCount = 0,
                    javaCpuUsagePercent = 0.0,
                    javaWorkingSetBytes = 0L,
                    javaThreadCount = 0,
                    perCoreCpuUsages = Array.Empty<double>(),
                    isMonitoring = _vm?.MonitorPage?.IsMonitoring ?? false,
                    memoryInfoText = "等待数据...",
                    diskInfoText = "等待数据...",
                });
            }

            return Task.FromResult<object?>(new
            {
                timestamp = metrics.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                cpuUsagePercent = metrics.CpuUsagePercent,
                memoryUsagePercent = metrics.MemoryUsagePercent,
                diskUsagePercent = metrics.DiskUsagePercent,
                totalMemoryBytes = metrics.TotalMemoryBytes,
                usedMemoryBytes = metrics.UsedMemoryBytes,
                diskTotalBytes = metrics.DiskTotalBytes,
                diskUsedBytes = metrics.DiskUsedBytes,
                diskName = metrics.DiskName,
                totalThreadCount = metrics.TotalThreadCount,
                javaCpuUsagePercent = metrics.JavaCpuUsagePercent,
                javaWorkingSetBytes = metrics.JavaWorkingSetBytes,
                javaThreadCount = metrics.JavaThreadCount,
                perCoreCpuUsages = metrics.PerCoreCpuUsages,
                isMonitoring = _vm?.MonitorPage?.IsMonitoring ?? false,
                memoryInfoText = _vm?.MonitorPage?.MemoryInfoText ?? string.Empty,
                diskInfoText = _vm?.MonitorPage?.DiskInfoText ?? string.Empty,
            });
        });

        // 获取历史数据（用于图表）— 当天持久化数据
        _bridgeService.RegisterRequestHandler("systemMonitor:getHistory", _ =>
        {
            try
            {
                var persistence = App.Services.GetRequiredService<Services.SystemMonitoring.IMetricsPersistenceService>();
                var today = persistence.LoadDay(DateTime.Now);
                var result = today.Select(p => new
                {
                    timestamp = p.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    cpuUsagePercent = p.CpuUsagePercent,
                    memoryUsagePercent = p.MemoryUsagePercent,
                }).ToList();

                return Task.FromResult<object?>(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取历史数据失败");
                return Task.FromResult<object?>(Array.Empty<object>());
            }
        });

        // 获取多天历史数据（用于跨天趋势图表）
        _bridgeService.RegisterRequestHandler("systemMonitor:getHistoryRange", payload =>
        {
            try
            {
                var days = 1;
                if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
                {
                    days = el.TryGetProperty("days", out var d) ? d.GetInt32() : 1;
                }
                days = Math.Clamp(days, 1, 30);

                var persistence = App.Services.GetRequiredService<Services.SystemMonitoring.IMetricsPersistenceService>();
                var data = persistence.LoadRecentDays(days);
                var result = data.Select(p => new
                {
                    timestamp = p.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    cpuUsagePercent = p.CpuUsagePercent,
                    memoryUsagePercent = p.MemoryUsagePercent,
                }).ToList();

                return Task.FromResult<object?>(new { points = result, days });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取多天历史数据失败");
                return Task.FromResult<object?>(new { points = Array.Empty<object>(), days = 0, error = ex.Message });
            }
        });

        // 启动监控
        _bridgeService.RegisterRequestHandler("systemMonitor:start", _ =>
        {
            try
            {
                _vm?.MonitorPage?.StartMonitoringCommand.Execute(null);
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "启动监控失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 停止监控
        _bridgeService.RegisterRequestHandler("systemMonitor:stop", _ =>
        {
            try
            {
                _vm?.MonitorPage?.StopMonitoringCommand.Execute(null);
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "停止监控失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 获取 CPU 拓扑信息
        _bridgeService.RegisterRequestHandler("systemMonitor:getCpuInfo", _ =>
        {
            try
            {
                var cpuIdentifier = App.Services.GetRequiredService<Services.HardwareInfo.CpuIdentifier>();
                var cpuInfo = cpuIdentifier.GetCpuInfo();
                return Task.FromResult<object?>(new
                {
                    modelName = cpuInfo.ModelName,
                    manufacturer = cpuInfo.Manufacturer,
                    physicalCores = cpuInfo.PhysicalCores,
                    logicalCores = cpuInfo.LogicalCores,
                    socketCount = cpuInfo.SocketCount,
                    numaNodeCount = cpuInfo.NumaNodeCount,
                    isHyperThreadingEnabled = cpuInfo.IsHyperThreadingEnabled,
                    logicalToPhysicalCoreMap = cpuInfo.LogicalToPhysicalCoreMap,
                    isRecognized = cpuInfo.IsRecognized,
                });
            }
            catch (Exception ex)
            {
                Log.Warning("获取 CPU 拓扑信息失败: {Msg}", ex.Message);
                return Task.FromResult<object?>(new
                {
                    modelName = "未知 CPU",
                    manufacturer = "未知",
                    physicalCores = 0,
                    logicalCores = 0,
                    socketCount = 0,
                    numaNodeCount = 0,
                    isHyperThreadingEnabled = false,
                    logicalToPhysicalCoreMap = Array.Empty<int>(),
                    isRecognized = false,
                });
            }
        });

        // === 服务器管理相关 API ===

        // 获取服务器列表（运行中 + 已知）
        _bridgeService.RegisterRequestHandler("server:list", _ =>
        {
            var running = _vm?.DetectionPage?.DetectionResult?.Servers ?? [];
            var known = _vm?.DetectionPage?.KnownServers ?? [];

            return Task.FromResult<object?>(new
            {
                running = running.Select(s => new
                {
                    processId = s.ProcessId,
                    serverType = s.ServerType.ToString(),
                    workingDirectory = s.WorkingDirectory,
                    serverJarName = s.ServerJarName,
                    serverPort = s.ServerPort,
                    isPortOpen = s.IsPortOpen,
                    portConflict = s.PortConflict,
                    displayName = s.DisplayName,
                    status = "Running",
                    maxHeapMemoryBytes = s.MaxHeapMemoryBytes,
                    initialHeapMemoryBytes = s.InitialHeapMemoryBytes,
                    usesAikarFlags = s.UsesAikarFlags,
                    gcType = s.GcType,
                    configFiles = s.ConfigFiles,
                }).ToList(),
                known = known.Select(k => new
                {
                    id = k.Id,
                    name = k.Name,
                    workingDirectory = k.WorkingDirectory,
                    serverJarPath = k.ServerJarPath,
                    javaPath = k.JavaPath,
                    port = k.Port,
                    initialHeapMemoryBytes = k.InitialHeapMemoryBytes,
                    maxHeapMemoryBytes = k.MaxHeapMemoryBytes,
                    group = k.Group,
                    isFavorite = k.IsFavorite,
                    addedAt = k.AddedAt,
                    lastSeenAt = k.LastSeenAt,
                    status = "Stopped",
                }).ToList(),
                isBusy = _vm?.DetectionPage?.IsBusy ?? false,
                isAutoDetectEnabled = _vm?.DetectionPage?.IsAutoDetectEnabled ?? false,
            });
        });

        // 刷新服务器列表
        _bridgeService.RegisterRequestHandler("server:refresh", async _ =>
        {
            try
            {
                if (_vm?.DetectionPage != null)
                {
                    if (_vm.DetectionPage.DetectCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
                        await asyncCmd.ExecuteAsync(null);
                    else
                        _vm.DetectionPage.DetectCommand.Execute(null);
                }
                return new { success = true };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "刷新服务器列表失败");
                return new { success = false, error = ex.Message };
            }
        });

        // 获取选中的服务器详情
        _bridgeService.RegisterRequestHandler("server:getSelected", _ =>
        {
            // 优先返回运行中的服务器，其次返回选中的已知服务器
            var s = _vm?.DetectionPage?.SelectedServer;
            var known = _vm?.DetectionPage?.SelectedKnownServer;

            // 运行中的服务器
            if (s != null)
            {
                return Task.FromResult<object?>(new
                {
                    processId = s.ProcessId,
                    serverType = s.ServerType.ToString(),
                    workingDirectory = s.WorkingDirectory,
                    serverJarPath = s.ServerJarPath,
                    serverJarName = s.ServerJarName,
                    javaPath = s.JavaPath,
                    fullCommandLine = s.FullCommandLine,
                    serverPort = s.ServerPort,
                    isPortOpen = s.IsPortOpen,
                    portConflict = s.PortConflict,
                    displayName = s.DisplayName,
                    status = "Running",
                    maxHeapMemoryBytes = s.MaxHeapMemoryBytes,
                    initialHeapMemoryBytes = s.InitialHeapMemoryBytes,
                    usesAikarFlags = s.UsesAikarFlags,
                    gcType = s.GcType,
                    configFiles = s.ConfigFiles,
                    networkStatusText = s.NetworkStatusText,
                    formattedMaxMemory = s.FormattedMaxMemory,
                    isKnown = false,
                });
            }

            // 已知服务器（未运行）
            if (known != null)
            {
                // 从 ViewModel 读取当前编辑的 JVM 参数和内存设置
                var initialMem = _vm?.DetectionPage?.InitialMemory ?? "0";
                var maxMem = _vm?.DetectionPage?.MaxMemory ?? "0";
                var selectedArgs = _vm?.DetectionPage?.SelectedArguments ?? [];

                var fullCmd = string.IsNullOrWhiteSpace(known.JavaPath)
                    ? $"java {string.Join(' ', selectedArgs)} -Xms{initialMem} -Xmx{maxMem} -jar \"{known.ServerJarPath}\" nogui"
                    : $"\"{known.JavaPath}\" {string.Join(' ', selectedArgs)} -Xms{initialMem} -Xmx{maxMem} -jar \"{known.ServerJarPath}\" nogui";

                return Task.FromResult<object?>(new
                {
                    processId = 0,
                    serverType = "Unknown",
                    workingDirectory = known.WorkingDirectory,
                    serverJarPath = known.ServerJarPath,
                    serverJarName = Path.GetFileName(known.ServerJarPath),
                    javaPath = known.JavaPath,
                    fullCommandLine = fullCmd,
                    serverPort = known.Port,
                    isPortOpen = false,
                    portConflict = false,
                    displayName = known.Name,
                    status = "Stopped",
                    maxHeapMemoryBytes = known.MaxHeapMemoryBytes,
                    initialHeapMemoryBytes = known.InitialHeapMemoryBytes,
                    usesAikarFlags = selectedArgs.Any(a => a.Contains("G1GC") || a.Contains("ParallelGC")),
                    gcType = selectedArgs.FirstOrDefault(a => a.Contains("GC")) ?? "",
                    configFiles = Array.Empty<string>(),
                    networkStatusText = "未运行",
                    formattedMaxMemory = known.MaxHeapMemoryBytes switch
                    {
                        >= 1024 * 1024 * 1024 => $"{known.MaxHeapMemoryBytes / (1024.0 * 1024 * 1024):F1} GB",
                        >= 1024 * 1024 => $"{known.MaxHeapMemoryBytes / (1024.0 * 1024):F0} MB",
                        >= 1024 => $"{known.MaxHeapMemoryBytes / 1024.0:F0} KB",
                        _ => $"{known.MaxHeapMemoryBytes} B"
                    },
                    isKnown = true,
                });
            }

            return Task.FromResult<object?>(null);
        });

        // 选择服务器
        _bridgeService.RegisterRequestHandler("server:select", payload =>
        {
            try
            {
                var displayName = ExtractStringPayload(payload);
                if (_vm?.DetectionPage != null && !string.IsNullOrEmpty(displayName))
                {
                    var server = _vm.DetectionPage.DetectionResult?.Servers
                        .FirstOrDefault(s => s.DisplayName == displayName);
                    if (server != null)
                    {
                        _vm.DetectionPage.SelectedServer = server;
                        _vm.DetectionPage.SelectedKnownServer = null;
                    }
                    else
                    {
                        var known = _vm.DetectionPage.KnownServers
                            .FirstOrDefault(k => k.Name == displayName);
                        if (known != null)
                        {
                            _vm.DetectionPage.SelectedKnownServer = known;
                            _vm.DetectionPage.SelectedServer = null;
                        }
                    }
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "选择服务器失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 启动当前选中的服务器
        _bridgeService.RegisterRequestHandler("server:start", async _ =>
        {
            try
            {
                if (_vm?.DetectionPage != null)
                {
                    await _vm.DetectionPage.StartCurrentServerCommand.ExecuteAsync(null);
                }
                return new { success = true };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "启动服务器失败");
                return new { success = false, error = ex.Message };
            }
        });

        // 停止当前选中的服务器
        _bridgeService.RegisterRequestHandler("server:stop", async _ =>
        {
            try
            {
                if (_vm?.DetectionPage != null)
                {
                    await _vm.DetectionPage.StopCurrentServerCommand.ExecuteAsync(null);
                }
                return new { success = true };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "停止服务器失败");
                return new { success = false, error = ex.Message };
            }
        });

        // 导入服务器
        _bridgeService.RegisterRequestHandler("server:import", _ =>
        {
            try
            {
                if (_vm?.DetectionPage != null)
                {
                    _vm.DetectionPage.BrowseAndImportServerCommand.Execute(null);
                    var msg = _vm.DetectionPage.OperationMessage;
                    var isSuccess = !msg?.StartsWith("❌") ?? true;
                    return Task.FromResult<object?>(new { success = isSuccess, message = msg });
                }
                return Task.FromResult<object?>(new { success = false, error = "未选择服务器" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "导入服务器失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 切换自动检测
        _bridgeService.RegisterRequestHandler("server:toggleAutoDetect", _ =>
        {
            try
            {
                if (_vm?.DetectionPage != null)
                {
                    _vm.DetectionPage.ToggleAutoDetectCommand.Execute(null);
                }
                return Task.FromResult<object?>(new
                {
                    success = true,
                    isEnabled = _vm?.DetectionPage?.IsAutoDetectEnabled ?? false
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "切换自动检测失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 启动已知服务器
        _bridgeService.RegisterRequestHandler("server:startKnown", async payload =>
        {
            try
            {
                var name = ExtractStringPayload(payload);
                if (_vm?.DetectionPage != null && !string.IsNullOrEmpty(name))
                {
                    var known = _vm.DetectionPage.KnownServers.FirstOrDefault(k => k.Name == name);
                    if (known != null)
                    {
                        await _vm.DetectionPage.StartKnownServerCommand.ExecuteAsync(known);
                        var msg = _vm.DetectionPage.OperationMessage;
                        var isSuccess = !msg?.StartsWith("❌") ?? true;
                        return new { success = isSuccess, message = msg };
                    }
                    return new { success = false, error = "未找到指定的服务器" };
                }
                return new { success = false, error = "未选择服务器" };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "启动已知服务器失败");
                return new { success = false, error = ex.Message };
            }
        });

        // 删除已知服务器
        _bridgeService.RegisterRequestHandler("server:removeKnown", payload =>
        {
            try
            {
                var name = ExtractStringPayload(payload);
                if (_vm?.DetectionPage != null && !string.IsNullOrEmpty(name))
                {
                    var known = _vm.DetectionPage.KnownServers.FirstOrDefault(k => k.Name == name);
                    if (known != null)
                    {
                        _vm.DetectionPage.RemoveKnownServerCommand.Execute(known);
                        var msg = _vm.DetectionPage.OperationMessage;
                        var isSuccess = !msg?.StartsWith("❌") ?? true;
                        return Task.FromResult<object?>(new { success = isSuccess, message = msg });
                    }
                    return Task.FromResult<object?>(new { success = false, error = "未找到指定的服务器" });
                }
                return Task.FromResult<object?>(new { success = false, error = "未选择服务器" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除已知服务器失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 保存为已知服务器
        _bridgeService.RegisterRequestHandler("server:saveAsKnown", _ =>
        {
            try
            {
                if (_vm?.DetectionPage != null)
                {
                    _vm.DetectionPage.SaveAsKnownServerCommand.Execute(null);
                    var msg = _vm.DetectionPage.OperationMessage;
                    var isSuccess = !msg?.StartsWith("❌") ?? true;
                    return Task.FromResult<object?>(new { success = isSuccess, message = msg });
                }
                return Task.FromResult<object?>(new { success = false, error = "未选择服务器" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存为已知服务器失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // ─── JVM 参数相关 API ───

        // 获取所有 JVM 参数定义
        _bridgeService.RegisterRequestHandler("jvm:getDefinitions", _ =>
        {
            var definitions = JvmArgumentConstants.AllArguments.Select(a => new
            {
                flag = a.Flag,
                name = a.Name,
                description = a.Description,
                valueType = a.ValueType.ToString(),
                category = a.Category.ToString(),
                defaultValue = a.DefaultValue,
                minimumValue = a.MinimumValue,
                maximumValue = a.MaximumValue,
                allowedValues = a.AllowedValues,
                recommended = a.Recommended,
                warning = a.Warning,
                requiresExperimentalUnlock = a.RequiresExperimentalUnlock,
            }).ToArray();
            return Task.FromResult<object?>(new { definitions });
        });

        // 获取当前选中已知服务器的 JVM 参数状态
        _bridgeService.RegisterRequestHandler("jvm:getState", _ =>
        {
            var vm = _vm?.DetectionPage;
            if (vm == null)
                return Task.FromResult<object?>(new { hasServer = false });

            var known = vm.SelectedKnownServer;

            return Task.FromResult<object?>(new
            {
                hasServer = known != null,
                isKnownServer = known != null,
                isRunning = vm.SelectedServer != null,
                initialMemory = vm.InitialMemory,
                maxMemory = vm.MaxMemory,
                selectedArguments = vm.SelectedArguments.ToList(),
            });
        });

        // 添加 JVM 参数
        _bridgeService.RegisterRequestHandler("jvm:addArgument", payload =>
        {
            try
            {
                var flag = ExtractStringPayload(payload);
                if (!string.IsNullOrEmpty(flag))
                {
                    _vm?.DetectionPage?.AddArgumentCommand.Execute(flag);
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "添加 JVM 参数失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 移除 JVM 参数
        _bridgeService.RegisterRequestHandler("jvm:removeArgument", payload =>
        {
            try
            {
                var flag = ExtractStringPayload(payload);
                if (!string.IsNullOrEmpty(flag))
                {
                    _vm?.DetectionPage?.RemoveArgumentCommand.Execute(flag);
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "移除 JVM 参数失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 更新 JVM 参数值
        _bridgeService.RegisterRequestHandler("jvm:updateArgument", payload =>
        {
            try
            {
                if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
                {
                    var oldArg = el.GetProperty("oldArg").GetString() ?? string.Empty;
                    var newValue = el.GetProperty("newValue").GetString() ?? string.Empty;

                    var vm = _vm?.DetectionPage;
                    if (vm != null && !string.IsNullOrEmpty(oldArg))
                    {
                        vm.StartEditArgumentCommand.Execute(oldArg);
                        vm.EditingArgumentValue = newValue;
                        vm.SaveEditArgumentCommand.Execute(null);
                    }
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "更新 JVM 参数失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 设置内存
        _bridgeService.RegisterRequestHandler("jvm:setMemory", payload =>
        {
            try
            {
                if (payload is JsonElement el && el.ValueKind == JsonValueKind.Object)
                {
                    var initial = el.GetProperty("initial").GetString();
                    var max = el.GetProperty("max").GetString();

                    var vm = _vm?.DetectionPage;
                    if (vm != null)
                    {
                        if (!string.IsNullOrEmpty(initial))
                            vm.InitialMemory = initial;
                        if (!string.IsNullOrEmpty(max))
                            vm.MaxMemory = max;
                    }
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "设置内存失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 应用预设
        _bridgeService.RegisterRequestHandler("jvm:applyPreset", payload =>
        {
            try
            {
                var preset = ExtractStringPayload(payload).ToLowerInvariant();
                var vm = _vm?.DetectionPage;
                if (vm != null)
                {
                    switch (preset)
                    {
                        case "aikar":
                            vm.ApplyAikarPresetCommand.Execute(null);
                            break;
                        case "g1gc":
                            vm.ApplyG1GCPresetCommand.Execute(null);
                            break;
                        case "zgc":
                            vm.ApplyZgcPresetCommand.Execute(null);
                            break;
                    }
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "应用 JVM 预设失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 添加自定义参数
        _bridgeService.RegisterRequestHandler("jvm:addCustom", payload =>
        {
            try
            {
                var arg = ExtractStringPayload(payload);
                var vm = _vm?.DetectionPage;
                if (vm != null && !string.IsNullOrEmpty(arg))
                {
                    vm.CustomArgument = arg;
                    vm.AddCustomArgumentCommand.Execute(null);
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "添加自定义 JVM 参数失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // === 补齐网络监控、配置编辑、设置三大模块的 API ===
        RegisterNetworkApis();
        RegisterConfigApis();
        RegisterSettingsApis();

        // 订阅来自 JS 的事件（调试用）
        _bridgeService.SubscribeToEvents((action, payload) =>
        {
            Log.Debug("📨 收到 JS 事件: {Action} = {Payload}", action, payload);
        });

        Log.Information("✅ 桥接 API 注册完成");
    }

    /// <summary>
    /// 从桥接消息 payload 中提取字符串值。
    /// 由于 <see cref="BridgeMessage.Payload"/> 类型为 <c>object?</c>，前端发送的
    /// 字符串经 System.Text.Json 反序列化后会变成 <see cref="JsonElement"/>，
    /// 直接调用 <c>ToString()</c> 对字符串值会返回带引号的 JSON 表示
    /// （如 <c>"\"MyServer\""</c> 而非 <c>MyServer</c>），导致按名称查找失败。
    /// 此方法对 <see cref="JsonElement"/> 调用 <c>GetString()</c> 以拿到原始字符串。
    /// </summary>
    private static string ExtractStringPayload(object? payload)
    {
        if (payload is null) return string.Empty;
        if (payload is JsonElement el)
        {
            return el.ValueKind == JsonValueKind.String
                ? el.GetString() ?? string.Empty
                : el.ToString();
        }
        return payload.ToString() ?? string.Empty;
    }

    private static string ArgbToRgb(string? hex) => ColorHelper.NormalizeHex(hex ?? string.Empty);

    // ─────────────────────────────────────────────────────────────────────
    // 网络监控相关桥接 API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 注册网络监控模块的桥接 API
    /// </summary>
    private void RegisterNetworkApis()
    {
        var net = _vm?.NetworkPage;

        // 获取网络状态摘要
        _bridgeService.RegisterRequestHandler("network:getStatus", _ =>
        {
            return Task.FromResult<object?>(new
            {
                totalPorts = net?.TotalPorts ?? 0,
                usedPorts = net?.UsedPorts ?? 0,
                usedPercentage = net?.UsedPercentage ?? 0,
                systemPorts = net?.SystemPorts ?? 0,
                registeredPorts = net?.RegisteredPorts ?? 0,
                dynamicPorts = net?.DynamicPorts ?? 0,
                uploadSpeedMB = net?.UploadSpeedMB ?? 0,
                downloadSpeedMB = net?.DownloadSpeedMB ?? 0,
                speedMaximumMB = net?.SpeedMaximumMB ?? 1.5,
                uploadSpeedText = net?.UploadSpeedText ?? "0 B/s",
                downloadSpeedText = net?.DownloadSpeedText ?? "0 B/s",
                todayUploadText = net?.TodayUploadText ?? "0 B",
                todayDownloadText = net?.TodayDownloadText ?? "0 B",
                dailyAnalysisText = net?.DailyAnalysisText ?? string.Empty,
                isRefreshing = net?.IsRefreshing ?? false,
                currentHour = net?.CurrentHour ?? DateTime.Now.Hour,
            });
        });

        // 获取端口占用列表
        _bridgeService.RegisterRequestHandler("network:getPorts", _ =>
        {
            var ports = net?.ListeningPorts ?? [];
            var result = ports.Select(p => new
            {
                port = p.Port,
                protocol = p.Protocol,
                processId = p.ProcessId,
                processName = p.ProcessName ?? string.Empty,
                isOpen = p.IsOpen,
                portRange = p.PortRange.ToString(),
            }).ToList();

            return Task.FromResult<object?>(new
            {
                ports = result,
                count = result.Count,
            });
        });

        // 获取桥接规则列表
        _bridgeService.RegisterRequestHandler("network:getBridgeRules", _ =>
        {
            var rules = net?.BridgeRules ?? [];
            var result = rules.Select(r => new
            {
                listenAddress = r.ListenAddress,
                listenPort = r.ListenPort,
                connectAddress = r.ConnectAddress,
                connectPort = r.ConnectPort,
                protocol = r.Protocol,
                engine = r.Engine,
            }).ToList();

            return Task.FromResult<object?>(new
            {
                rules = result,
                count = result.Count,
            });
        });

        // 添加桥接规则
        _bridgeService.RegisterRequestHandler("network:addBridge", payload =>
        {
            try
            {
                var json = payload?.ToString() ?? "{}";
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var listenAddress = root.TryGetProperty("listenAddress", out var la) ? la.GetString() ?? "0.0.0.0" : "0.0.0.0";
                var listenPort = root.TryGetProperty("listenPort", out var lp) ? lp.GetInt32() : 0;
                var connectAddress = root.TryGetProperty("connectAddress", out var ca) ? ca.GetString() ?? "127.0.0.1" : "127.0.0.1";
                var connectPort = root.TryGetProperty("connectPort", out var cp) ? cp.GetInt32() : 0;
                var addFirewall = root.TryGetProperty("addFirewall", out var af) && af.GetBoolean();

                if (listenPort <= 0 || connectPort <= 0)
                    return Task.FromResult<object?>(new { success = false, error = "端口必须大于 0" });

                var bridgeService = App.Services.GetRequiredService<Services.Network.IPortBridgeService>();
                var rule = new Models.PortBridgeRule
                {
                    ListenAddress = listenAddress,
                    ListenPort = listenPort,
                    ConnectAddress = connectAddress,
                    ConnectPort = connectPort,
                };

                var success = bridgeService.AddBridgeRule(rule);
                if (success && addFirewall)
                    bridgeService.EnableFirewallRule(listenPort);

                if (success)
                    return Task.FromResult<object?>(new { success });

                return Task.FromResult<object?>(new { success = false, error = bridgeService.LastError ?? "添加桥接规则失败" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "添加桥接规则失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 删除桥接规则
        _bridgeService.RegisterRequestHandler("network:removeBridge", payload =>
        {
            try
            {
                var json = payload?.ToString() ?? "{}";
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var listenAddress = root.TryGetProperty("listenAddress", out var la) ? la.GetString() ?? "0.0.0.0" : "0.0.0.0";
                var listenPort = root.TryGetProperty("listenPort", out var lp) ? lp.GetInt32() : 0;
                var protocol = root.TryGetProperty("protocol", out var p) ? p.GetString() ?? "v4tov4" : "v4tov4";

                if (listenPort <= 0)
                    return Task.FromResult<object?>(new { success = false, error = "端口必须大于 0" });

                var bridgeService = App.Services.GetRequiredService<Services.Network.IPortBridgeService>();
                var success = bridgeService.RemoveBridgeRule(listenAddress, listenPort, protocol);

                if (success)
                    return Task.FromResult<object?>(new { success });

                return Task.FromResult<object?>(new { success = false, error = bridgeService.LastError ?? "删除桥接规则失败" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除桥接规则失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 结束占用端口的进程
        _bridgeService.RegisterRequestHandler("network:killProcess", payload =>
        {
            try
            {
                var json = payload?.ToString() ?? "{}";
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var port = root.TryGetProperty("port", out var pe) ? pe.GetInt32() : 0;
                var protocol = root.TryGetProperty("protocol", out var pr) ? pr.GetString() ?? "TCP" : "TCP";

                if (port <= 0)
                    return Task.FromResult<object?>(new { success = false, error = "无效端口" });

                if (net != null && net.ListeningPorts != null)
                {
                    var portInfo = net.ListeningPorts.FirstOrDefault(p => p.Port == port && p.Protocol == protocol);
                    if (portInfo != null)
                    {
                        var networkService = App.Services.GetRequiredService<Services.Network.NetworkService>();
                        var success = networkService.KillProcessByPort(port);
                        if (success)
                            return Task.FromResult<object?>(new { success });

                        return Task.FromResult<object?>(new { success = false, error = "结束进程失败，可能是权限不足或进程已退出" });
                    }
                }

                return Task.FromResult<object?>(new { success = false, error = "未找到占用该端口的进程" });
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                Log.Error(ex, "结束进程失败：权限不足");
                return Task.FromResult<object?>(new { success = false, error = "权限不足，请以管理员身份运行程序" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "结束进程失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 获取常见端口列表
        _bridgeService.RegisterRequestHandler("network:getCommonPorts", _ =>
        {
            var commonPorts = McServerGuard.Constants.CommonPorts.All;
            var result = commonPorts.Cast<McServerGuard.Models.CommonPort>().Select(p => new
            {
                port = p.Port,
                name = p.Name,
                description = p.Description,
                category = p.Category,
            }).ToList();

            return Task.FromResult<object?>(new { ports = result });
        });

        // 刷新网络数据
        _bridgeService.RegisterRequestHandler("network:refresh", async _ =>
        {
            try
            {
                if (net != null)
                {
                    if (net.RefreshCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
                        await asyncCmd.ExecuteAsync(null);
                    else
                        net.RefreshCommand.Execute(null);
                }
                return new { success = true };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "刷新网络数据失败");
                return new { success = false, error = ex.Message };
            }
        });

        // 获取24小时历史流量数据
        _bridgeService.RegisterRequestHandler("network:getHourlyHistory", _ =>
        {
            return Task.FromResult<object?>(new
            {
                upload = net?.HourlyUploadMBArray ?? new double[24],
                download = net?.HourlyDownloadMBArray ?? new double[24],
                currentHour = net?.CurrentHour ?? DateTime.Now.Hour,
            });
        });

        Log.Information("✅ 网络监控 API 注册完成");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 配置编辑相关桥接 API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 注册配置编辑模块的桥接 API
    /// </summary>
    private void RegisterConfigApis()
    {
        var cfg = _vm?.ConfigPage;

        // 获取可用服务器列表
        _bridgeService.RegisterRequestHandler("config:getAvailableServers", _ =>
        {
            var servers = cfg?.AvailableServers ?? [];
            var result = servers.Select(s => new
            {
                displayName = s.DisplayName,
                workingDirectory = s.WorkingDirectory,
                serverJarName = s.ServerJarName,
                serverJarPath = s.ServerJarPath,
                serverPort = s.ServerPort,
            }).ToList();

            return Task.FromResult<object?>(new { servers = result });
        });

        // 获取配置文件树
        _bridgeService.RegisterRequestHandler("config:getFileTree", _ =>
        {
            var tree = cfg?.ConfigFileTree ?? [];

            object FormatTree(List<McServerGuard.ViewModels.ConfigFileItem> items) =>
                items.Select(i => new
                {
                    fileName = i.FileName,
                    fullPath = i.FullPath,
                    relativePath = i.RelativePath,
                    isDirectory = i.IsDirectory,
                    children = i.Children.Count > 0 ? FormatTree(i.Children) : new List<object>(),
                }).ToList();

            var result = FormatTree(tree);

            return Task.FromResult<object?>(new
            {
                tree = result,
                count = cfg?.ConfigFiles.Count ?? 0,
                configFileCountText = cfg?.ConfigFileCountText ?? "未找到配置文件",
                hasServerDirectory = cfg?.HasServerDirectory ?? false,
                serverWorkingDirectory = cfg?.ServerWorkingDirectory ?? string.Empty,
                selectedServerName = cfg?.SelectedServerName,
            });
        });

        // 选中配置文件（触发加载）
        _bridgeService.RegisterRequestHandler("config:selectFile", payload =>
        {
            try
            {
                var relativePath = ExtractStringPayload(payload);
                if (cfg != null && !string.IsNullOrEmpty(relativePath))
                {
                    cfg.SelectedConfigFile = relativePath;
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 获取当前配置条目（分组）
        _bridgeService.RegisterRequestHandler("config:getEntries", _ =>
        {
            var groups = cfg?.GroupedConfigEntries ?? [];

            object FormatEntry(McServerGuard.Models.ServerConfigEntry e) => new
            {
                key = e.Key,
                value = e.Value,
                originalValue = e.OriginalValue,
                displayName = e.DisplayName,
                friendlyDisplayName = e.FriendlyDisplayName,
                description = e.Description,
                isModified = e.IsModified,
                isValid = e.IsValid,
                errorMessage = e.ErrorMessage,
                requiresRestart = e.RequiresRestart,
                isBoolType = e.IsBoolType,
                isEnumType = e.IsEnumType,
                isNumericType = e.IsNumericType,
                isStringType = e.IsStringType,
                allowedValues = e.Descriptor?.AllowedValues,
                minValue = e.Descriptor?.MinValue,
                maxValue = e.Descriptor?.MaxValue,
                valueType = e.Descriptor?.ValueType ?? "string",
            };

            var result = groups.Select(g => new
            {
                key = g.Key,
                items = g.Items.Select(FormatEntry).ToList(),
            }).ToList();

            var selectedServerName = cfg?.SelectedServerName;
            bool isCurrentServerRunning = false;
            if (!string.IsNullOrEmpty(selectedServerName) && _vm?.DetectionPage?.DetectionResult?.Servers != null)
            {
                isCurrentServerRunning = _vm.DetectionPage.DetectionResult.Servers.Any(s =>
                    s.DisplayName == selectedServerName || s.ServerJarName == selectedServerName);
            }

            var modifiedCount = cfg?.ConfigEntries.Count(e => e.IsModified) ?? 0;

            return Task.FromResult<object?>(new
            {
                groups = result,
                totalCount = cfg?.ConfigEntries.Count ?? 0,
                modifiedCount = modifiedCount,
                hasUnsavedChanges = cfg?.HasUnsavedChanges ?? false,
                isLoading = cfg?.IsLoading ?? false,
                loadProgress = cfg?.LoadProgress ?? 0,
                selectedConfigFile = cfg?.SelectedConfigFile,
                selectedConfigFileName = cfg?.SelectedConfigFileName,
                saveStatusMessage = cfg?.SaveStatusMessage,
                isSaveError = cfg?.IsSaveError ?? false,
                isCurrentServerRunning = isCurrentServerRunning,
            });
        });

        // 更新配置项的值
        _bridgeService.RegisterRequestHandler("config:updateValue", payload =>
        {
            try
            {
                var json = payload?.ToString() ?? "{}";
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var key = root.TryGetProperty("key", out var k) ? k.GetString() : null;
                var value = root.TryGetProperty("value", out var v) ? v.GetString() : null;

                if (cfg != null && key != null && value != null)
                {
                    var entry = cfg.ConfigEntries.FirstOrDefault(e => e.Key == key);
                    if (entry != null)
                    {
                        entry.Value = value;
                    }
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 保存配置
        _bridgeService.RegisterRequestHandler("config:save", async _ =>
        {
            try
            {
                if (cfg != null)
                {
                    if (cfg.SaveConfigCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
                        await asyncCmd.ExecuteAsync(null);
                    else
                        cfg.SaveConfigCommand.Execute(null);
                }

                var isSuccess = !cfg?.IsSaveError ?? false;
                var requiresRestart = cfg?.ConfigEntries.Any(e => e.IsModified && e.RequiresRestart) ?? false;

                string? errorType = null;
                string? errorDetail = null;

                if (!isSuccess)
                {
                    errorType = cfg?.SaveErrorType ?? "Unknown";
                    errorDetail = cfg?.SaveStatusMessage;
                }

                return new
                {
                    success = isSuccess,
                    message = cfg?.SaveStatusMessage,
                    errorType = errorType,
                    errorDetail = errorDetail,
                    requiresRestart = requiresRestart,
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存配置失败");
                return new { success = false, error = ex.Message };
            }
        });

        // 重置变更
        _bridgeService.RegisterRequestHandler("config:reset", _ =>
        {
            try
            {
                cfg?.ResetChangesCommand.Execute(null);
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "重置配置变更失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 撤销
        _bridgeService.RegisterRequestHandler("config:undo", _ =>
        {
            try
            {
                cfg?.UndoCommand.Execute(null);
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "撤销配置变更失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 选择服务器
        _bridgeService.RegisterRequestHandler("config:selectServer", payload =>
        {
            try
            {
                var name = ExtractStringPayload(payload);
                if (cfg != null && !string.IsNullOrEmpty(name))
                {
                    cfg.SelectedServerName = name;
                }
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 重新扫描配置文件
        _bridgeService.RegisterRequestHandler("config:rescan", async _ =>
        {
            try
            {
                if (cfg?.RescanConfigFilesCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
                    await asyncCmd.ExecuteAsync(null);
                else
                    cfg?.RescanConfigFilesCommand.Execute(null);
                return new { success = true };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "重新扫描配置文件失败");
                return new { success = false, error = ex.Message };
            }
        });

        Log.Information("✅ 配置编辑 API 注册完成");
    }

    // ─────────────────────────────────────────────────────────────────────
    // 设置相关桥接 API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 注册设置模块的桥接 API
    /// </summary>
    private void RegisterSettingsApis()
    {
        var settings = _vm?.SettingsPage;

        // 获取所有设置
        _bridgeService.RegisterRequestHandler("settings:get", _ =>
        {
            return Task.FromResult<object?>(new
            {
                primaryColorHex = ArgbToRgb(settings?.PrimaryColorHex),
                accentColorHex = ArgbToRgb(settings?.AccentColorHex),
                backgroundColorHex = ArgbToRgb(settings?.BackgroundColorHex),
                cardColorHex = ArgbToRgb(settings?.CardColorHex),
                textColorHex = ArgbToRgb(settings?.TextColorHex),
                borderColorHex = ArgbToRgb(settings?.BorderColorHex),
                cornerRadius = settings?.CornerRadius ?? 12,
                animationDuration = settings?.AnimationDuration ?? 300,
                enableAnimations = settings?.EnableAnimations ?? true,
                enableWindowsNotifications = settings?.EnableWindowsNotifications ?? true,
                preferJavaw = settings?.PreferJavaw ?? true,
                statusMessage = settings?.StatusMessage ?? string.Empty,
                isDarkMode = _themeService.IsDarkMode,
            });
        });

        // 设置主色
        _bridgeService.RegisterRequestHandler("settings:setPrimaryColor", payload =>
        {
            var hex = ExtractStringPayload(payload);
            if (string.IsNullOrEmpty(hex)) hex = "#3B82F6";

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                settings?.SetPrimaryColorCommand.Execute(hex);
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "设置主色失败: {Hex}", hex);
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 设置强调色
        _bridgeService.RegisterRequestHandler("settings:setAccentColor", payload =>
        {
            var hex = ExtractStringPayload(payload);
            if (string.IsNullOrEmpty(hex)) hex = "#FB7185";

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                settings?.SetAccentColorCommand.Execute(hex);
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "设置强调色失败: {Hex}", hex);
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 应用主题
        _bridgeService.RegisterRequestHandler("settings:applyTheme", _ =>
        {
            try
            {
                settings?.ApplyThemeCommand.Execute(null);
                return Task.FromResult<object?>(new
                {
                    success = true,
                    primaryColorHex = ArgbToRgb(settings?.PrimaryColorHex),
                    isDarkMode = _themeService.IsDarkMode,
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "应用主题失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 保存设置
        _bridgeService.RegisterRequestHandler("settings:save", _ =>
        {
            try
            {
                settings?.SaveSettingsCommand.Execute(null);
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存设置失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 应用预设
        _bridgeService.RegisterRequestHandler("settings:setPreset", payload =>
        {
            try
            {
                var preset = ExtractStringPayload(payload);
                if (string.IsNullOrEmpty(preset)) preset = "SkyBlue";
                settings?.SetPresetCommand.Execute(preset);
                return Task.FromResult<object?>(new
                {
                    success = true,
                    primaryColorHex = ArgbToRgb(settings?.PrimaryColorHex),
                    accentColorHex = ArgbToRgb(settings?.AccentColorHex),
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "应用预设失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 重置为默认
        _bridgeService.RegisterRequestHandler("settings:reset", _ =>
        {
            try
            {
                settings?.ResetToDefaultCommand.Execute(null);
                return Task.FromResult<object?>(new
                {
                    success = true,
                    primaryColorHex = ArgbToRgb(settings?.PrimaryColorHex),
                    accentColorHex = ArgbToRgb(settings?.AccentColorHex),
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "重置设置失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 切换动画开关
        _bridgeService.RegisterRequestHandler("settings:toggleAnimations", _ =>
        {
            try
            {
                if (settings != null)
                {
                    settings.EnableAnimations = !settings.EnableAnimations;
                }
                return Task.FromResult<object?>(new
                {
                    success = true,
                    enableAnimations = settings?.EnableAnimations ?? true,
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "切换动画开关失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 测试通知
        _bridgeService.RegisterRequestHandler("settings:testNotification", _ =>
        {
            try
            {
                settings?.TestNotificationCommand.Execute(null);
                return Task.FromResult<object?>(new { success = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "测试通知失败");
                return Task.FromResult<object?>(new { success = false, error = ex.Message });
            }
        });

        // 获取 Java 列表
        _bridgeService.RegisterRequestHandler("settings:getJavaList", _ =>
        {
            var javas = settings?.JavaInstallations ?? [];
            var result = javas.Select(j => new
            {
                javaPath = j.Installation.JavaPath,
                javaHome = j.Installation.JavaHome,
                versionString = j.Installation.VersionString,
                versionDisplay = j.VersionDisplay,
                isDefault = j.IsDefault,
                isCustom = j.IsCustom,
            }).ToList();

            return Task.FromResult<object?>(new
            {
                javas = result,
                isScanning = settings?.IsScanningJava ?? false,
                selectedJava = settings?.SelectedJava?.Installation.JavaPath,
            });
        });

        // 重新扫描 Java
        _bridgeService.RegisterRequestHandler("settings:rescanJava", async _ =>
        {
            try
            {
                if (settings?.RescanJavaCommand is CommunityToolkit.Mvvm.Input.IAsyncRelayCommand asyncCmd)
                    await asyncCmd.ExecuteAsync(null);
                else
                    settings?.RescanJavaCommand.Execute(null);
                return new { success = true };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "重新扫描 Java 失败");
                return new { success = false, error = ex.Message };
            }
        });

        // 获取预设主题列表
        _bridgeService.RegisterRequestHandler("settings:getPresets", _ =>
        {
            var presets = new[]
            {
                new { key = "SkyBlue", label = "苍穹蓝", primary = "#3B82F6", accent = "#FB7185" },
                new { key = "BlueOrange", label = "科技蓝", primary = "#1565C0", accent = "#FF9800" },
                new { key = "TealPink", label = "清新绿", primary = "#00897B", accent = "#E91E63" },
                new { key = "RedYellow", label = "火焰红", primary = "#C62828", accent = "#FFD600" },
                new { key = "OceanBlue", label = "海洋蓝", primary = "#0097A7", accent = "#FFD740" },
            };
            return Task.FromResult<object?>(new { presets });
        });

        // 获取主色色板
        _bridgeService.RegisterRequestHandler("settings:getPrimarySwatches", _ =>
        {
            var swatches = new[]
            {
                new { color = "#7B1FA2", label = "深紫" },
                new { color = "#1565C0", label = "蓝" },
                new { color = "#00897B", label = "青绿" },
                new { color = "#C62828", label = "红" },
                new { color = "#F57C00", label = "橙" },
                new { color = "#2E7D32", label = "绿" },
                new { color = "#0D47A1", label = "深蓝" },
                new { color = "#4A148C", label = "深紫红" },
            };
            return Task.FromResult<object?>(new { swatches });
        });

        // 获取强调色色板
        _bridgeService.RegisterRequestHandler("settings:getAccentSwatches", _ =>
        {
            var swatches = new[]
            {
                new { color = "#CDDC39", label = "青柠" },
                new { color = "#FF9800", label = "橙" },
                new { color = "#E91E63", label = "粉红" },
                new { color = "#FFD600", label = "黄" },
                new { color = "#00BCD4", label = "青" },
                new { color = "#8BC34A", label = "浅绿" },
                new { color = "#FF5722", label = "深橙" },
                new { color = "#6366F1", label = "靛蓝" },
            };
            return Task.FromResult<object?>(new { swatches });
        });

        _bridgeService.RegisterRequestHandler("about:getTeamInfo", _ =>
        {
            return Task.FromResult<object?>(new
            {
                primaryDevelopers = new[]
                {
                    new
                    {
                        name = "ABI-ZTROS",
                        role = "主要开发 · 项目发起者",
                        github = "github.com/ABI-ZTROS",
                        avatar = "",
                        isClickable = true
                    }
                },
                specialThanks = new[]
                {
                    new
                    {
                        name = "烟蓝湘",
                        role = "情绪支持",
                        note = "Special Thanks 💖",
                        avatar = "",
                        hasHeartIcon = true
                    }
                },
                memorial = new[]
                {
                    new
                    {
                        name = "Gglaoguan",
                        role = "已退役 · 铭记贡献",
                        description = "人生自古谁无死？不幸的，此开发者由于不可控因素已经永远离开了我们。因此无法继续投入到开发工作当中。让我们永远缅怀他。",
                        github = "github.com/Gglaoguan",
                        hasCrossIcon = true,
                        isMemorial = true
                    }
                },
                contributors = new[]
                {
                    new
                    {
                        name = "MochaCello92377",
                        role = "Debug · 功能建议",
                        github = "MochaCello92377",
                        avatar = ""
                    },
                    new
                    {
                        name = "CatStack-pixe",
                        role = "测试环境",
                        github = "CatStack-pixe",
                        avatar = ""
                    }
                }
            });
        });

        Log.Information("✅ 设置 API 注册完成");
    }

    // DataContext 变更事件处理
    private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= Vm_PropertyChanged;

        _vm = e.NewValue as MainViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += Vm_PropertyChanged;
    }

    // ViewModel 属性变更事件处理
    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 当状态消息变化时，通过桥接推送到前端
        if (e.PropertyName == nameof(MainViewModel.StatusMessage) && _bridgeService.IsInitialized)
        {
            // P2 修复：fire-and-forget Task 添加异常处理，避免 unobserved Task exception
            _ = _bridgeService.SendEventAsync("status:update", new
            {
                message = _vm?.StatusMessage ?? string.Empty
            }).ContinueWith(t => Log.Warning(t.Exception, "推送状态消息失败"),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 自定义标题栏交互
    // ─────────────────────────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeIcon != null)
        {
            MaximizeIcon.Kind = WindowState == WindowState.Maximized
                ? MahApps.Metro.IconPacks.PackIconFontAwesome6Kind.WindowRestoreSolid
                : MahApps.Metro.IconPacks.PackIconFontAwesome6Kind.WindowMaximizeSolid;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // 关闭确认
    // ─────────────────────────────────────────────────────────────────────

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
            return;

        // ⚠️ P0 修复：先检查服务器运行状态再做关闭确认，之后才清理 _vm 引用
        // 原代码先置空 _vm 再访问 _vm?.AnyServerRunning，导致关闭确认永远不触发
        if (_vm?.AnyServerRunning == true)
        {
            var result = MessageBox.Show(
                "⚠️ 警告：关闭 MSMC 将导致正在运行的 Minecraft 服务器失去管理，可能直接崩溃或导致数据丢失、存档损坏。确定要关闭吗？",
                "确认关闭",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }
        }

        // 清理事件订阅和桥接服务（在关闭确认通过后）
        if (_vm is not null)
        {
            _vm.PropertyChanged -= Vm_PropertyChanged;
            _vm = null;
        }

        // 取消窗口自身事件订阅（P2 修复：防止动画关闭场景下重复触发）
        Loaded -= MainWindow_Loaded;
        DataContextChanged -= MainWindow_DataContextChanged;
        Closing -= MainWindow_Closing;
        StateChanged -= MainWindow_StateChanged;

        // 关闭桥接服务
        try
        {
            _bridgeService.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "关闭桥接服务时发生异常");
        }

        if (!_themeService.EnableAnimations)
        {
            // P1 修复：无动画时显式释放 WebView2 控件，防止 Chromium 子进程残留
            try { MainWebView.Dispose(); } catch (Exception ex) { Log.Debug(ex, "WebView2 Dispose 异常（可忽略）"); }
            return;
        }

        e.Cancel = true;
        _isClosing = true;

        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new System.Windows.Media.Animation.CubicEase
        { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };

        var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, duration)
        {
            EasingFunction = ease,
            FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
        };
        fadeOut.Completed += (_, _) =>
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, null);
            // P1 修复：动画关闭后释放 WebView2 控件，防止 Chromium 子进程残留
            try { MainWebView.Dispose(); } catch (Exception ex) { Log.Debug(ex, "WebView2 Dispose 异常（可忽略）"); }
            Close();
        };
        BeginAnimation(OpacityProperty, fadeOut, System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace);
    }
}
