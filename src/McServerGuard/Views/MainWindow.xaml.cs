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
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Services.Frontend;
using McServerGuard.Services.WebView2;
using McServerGuard.ViewModels;
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

    // 窗口 Loaded 事件处理：初始化 WebView2 和桥接服务
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Information("[UI-1] 🌐 MainWindow_Loaded 触发，开始初始化 WebView2...");

        try
        {
            Log.Information("[UI-2] 🔧 初始化 WebView2 桥接服务...");
            await _bridgeService.InitializeAsync(MainWebView);
            Log.Information("[UI-3] ✅ WebView2 桥接服务初始化完成");

            Log.Information("[UI-4] 📡 注册桥接 API 处理程序...");
            RegisterBridgeApis();
            Log.Information("[UI-5] ✅ 桥接 API 注册完成");

            // 按优先级尝试加载前端：B模式(嵌入zip拦截) -> C模式(zip解压虚拟主机) -> 测试页面
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
            Log.Error(ex, "[UI-ERR] ❌ WebView2 初始化失败");
            MessageBox.Show($"WebView2 初始化失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

        // 订阅来自 JS 的事件（调试用）
        _bridgeService.SubscribeToEvents((action, payload) =>
        {
            Log.Debug("📨 收到 JS 事件: {Action} = {Payload}", action, payload);
        });

        Log.Information("✅ 桥接 API 注册完成");
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
            _ = _bridgeService.SendEventAsync("status:update", new
            {
                message = _vm?.StatusMessage ?? string.Empty
            });
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

        // 清理事件订阅
        if (_vm is not null)
        {
            _vm.PropertyChanged -= Vm_PropertyChanged;
            _vm = null;
        }

        // 关闭桥接服务
        try
        {
            _bridgeService.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "关闭桥接服务时发生异常");
        }

        // 关闭确认
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

        if (!_themeService.EnableAnimations)
            return;

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
            Close();
        };
        BeginAnimation(OpacityProperty, fadeOut, System.Windows.Media.Animation.HandoffBehavior.SnapshotAndReplace);
    }
}
