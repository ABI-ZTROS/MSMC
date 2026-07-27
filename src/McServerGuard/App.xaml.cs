// -----------------------------------------------------------------------------
// 文件名: App.xaml.cs
// 命名空间: McServerGuard
// 功能描述: WPF 应用程序入口，负责 DI 容器构建、服务注册、全局异常处理与启动流程编排
// 依赖组件: Microsoft.Extensions.DependencyInjection, Serilog, System.Windows
// 设计模式: 依赖注入模式、单例模式、观察者模式（全局异常监听）
// -----------------------------------------------------------------------------
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Services.ConfigManagement;
using McServerGuard.Services.Privilege;
using McServerGuard.Services.ServerDetection;
using McServerGuard.Services.SystemMonitoring;
using McServerGuard.Services.Network;
using McServerGuard.Services.WebView2;
using McServerGuard.ViewModels;
using McServerGuard.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace McServerGuard;

/// <summary>
/// WPF 应用程序入口类
/// 负责应用程序生命周期管理、依赖注入容器构建、全局异常处理与启动流程编排
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 依赖注入服务提供器
    /// </summary>
    private ServiceProvider? _serviceProvider;

    /// <summary>
    /// 全局 DI 容器访问点
    /// 供 View 层按需解析服务实例
    /// </summary>
    public static IServiceProvider Services
    {
        get
        {
            var app = Current as App;
            return app?._serviceProvider ?? throw new InvalidOperationException("DI 容器尚未初始化");
        }
    }

    /// <summary>
    /// 应用程序启动入口
    /// 执行日志初始化、全局异常配置、DI 容器构建、服务注册与主窗口显示
    /// </summary>
    /// <param name="e">启动事件参数</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        // 初始化日志系统
        // 每次启动生成独立日志文件（含启动时间戳），避免单文件过大
        // 使用 AppContext.BaseDirectory 确保日志路径不依赖工作目录
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        var logFileName = Path.Combine(logDir, $"mcserverguard-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("McServerGuard.Services.Server", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("McServerGuard.Services.Configuration", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("McServerGuard.Services.SystemInfo", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("McServerGuard.Services.Network", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("McServerGuard.Services.PortForward", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(logFileName)
            .CreateLogger();

        // 清理 7 天前的旧日志文件
        try
        {
            var oldFiles = Directory.GetFiles(logDir, "mcserverguard-*.log")
                .Select(f => new FileInfo(f))
                .Where(f => (DateTime.Now - f.CreationTime).TotalDays > 7)
                .ToList();
            foreach (var file in oldFiles)
            {
                try { file.Delete(); }
                catch { /* 忽略单个文件删除失败 */ }
            }
            if (oldFiles.Count > 0)
                Log.Information("🧹 已清理 {Count} 个旧日志文件", oldFiles.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理旧日志文件失败");
        }

        // 挂载全局异常处理
        SetupGlobalExceptionHandling();

        // 注：之前曾尝试 RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly
        // 但崩溃根因是 Color="{DynamicResource ...Brush}" 类型不匹配（已修复），
        // 软件渲染并不能解决问题，反而导致编译错误，已移除。

        try
        {
            base.OnStartup(e);
            Log.Information("🚀 McServerGuard 正在启动...");

            // ─────────────────────────────────────────────────────
            // 阶段 0：提前初始化主题服务（用于启动窗口主题）
            // ─────────────────────────────────────────────────────
            var earlyThemeService = new ThemeService();
            try { earlyThemeService.LoadSettings(); }
            catch { /* 忽略加载失败，使用默认主题 */ }
            earlyThemeService.ApplyTheme();

            // ─────────────────────────────────────────────────────
            // 阶段 1：显示启动窗口
            // ─────────────────────────────────────────────────────
            Log.Information("🪟 显示启动窗口...");
            var startupWindow = new StartupWindow(earlyThemeService);
            startupWindow.Show();

            // 将启动窗口设为 MainWindow 以便消息循环正常工作
            MainWindow = startupWindow;

            startupWindow.AppendLog("🚀 McServerGuard 启动中...");
            startupWindow.AppendLog("📋 正在初始化核心服务...");

            // ─────────────────────────────────────────────────────
            // 阶段 2：后台线程执行重量级初始化
            // ─────────────────────────────────────────────────────
            _ = Task.Run(async () =>
            {
                try
                {
                    startupWindow.AppendLog("🏗️ 搭建 DI 容器...");
                    var services = new ServiceCollection();

                    startupWindow.AppendLog("🎯 注册服务器检测服务...");
                    services.AddSingleton<IServerDetector, ServerDetector>();
                    services.AddSingleton<IServerImporterService, ServerImporterService>();
                    services.AddSingleton<IServerManagerService, ServerManagerService>();
                    services.AddSingleton<ProcessScanner>();
                    services.AddSingleton<WorkingDirectoryResolver>();
                    services.AddSingleton<ConfigFileScanner>();
                    services.AddSingleton<PortScanner>();
                    services.AddSingleton<PortToProcessMapper>();
                    services.AddSingleton<ServerPortResolver>();
                    services.AddSingleton<NetworkService>();
                    services.AddSingleton<ITcpForwarder, TcpForwarderService>();
                    services.AddSingleton<NetshPortBridgeService>();
                    services.AddSingleton<IPortBridgeService, CompositePortBridgeService>();
                    services.AddSingleton<NetworkTrafficService>();
                    services.AddSingleton<JarCoreIdentifier>();

                    startupWindow.AppendLog("🔐 注册权限服务...");
                    services.AddSingleton<AdminPrivilegeService>();
                    services.AddSingleton<IPrivilegeService, PrivilegeService>();

                    startupWindow.AppendLog("📋 注册配置管理服务...");
                    services.AddSingleton<IConfigManager, ConfigManager>();
                    services.AddSingleton<ConfigDescriptorRegistry>();

                    startupWindow.AppendLog("📊 注册系统监控服务...");
                    services.AddSingleton<ISystemMonitor, SystemMonitor>();
                    services.AddSingleton<DiskSpaceMonitor>();
                    services.AddSingleton<MemoryMonitor>();
                    services.AddSingleton<ThreadAnalyzer>();
                    services.AddSingleton<Services.HardwareInfo.CpuIdentifier>();
                    services.AddSingleton<IMetricsPersistenceService, MetricsPersistenceService>();

                    startupWindow.AppendLog("🎨 注册主题服务...");
                    services.AddSingleton<IThemeService>(_ =>
                    {
                        // 复用提前初始化的主题服务实例
                        return earlyThemeService;
                    });

                    startupWindow.AppendLog("📜 注册用户协议服务...");
                    services.AddSingleton<IUserAgreementService, UserAgreementService>();

                    startupWindow.AppendLog("📁 注册全局配置服务...");
                    services.AddSingleton<IAppConfigService, AppConfigService>();

                    startupWindow.AppendLog("☕ 注册 Java 查找服务...");
                    services.AddSingleton<IJavaFinderService, JavaFinderService>();

                    startupWindow.AppendLog("🔔 注册通知服务...");
                    services.AddSingleton<IToastNotificationService, ToastNotificationService>();

                    startupWindow.AppendLog("🧹 注册内存优化服务...");
                    services.AddSingleton<MemoryOptimizerService>();

                    startupWindow.AppendLog("🌉 注册 WebView2 桥接服务...");
                    services.AddSingleton<IWebView2BridgeService, WebView2BridgeService>();

                    startupWindow.AppendLog("🧩 注册 ViewModel...");
                    services.AddSingleton<ViewModels.ServerDetectionViewModel>();
                    services.AddSingleton<ViewModels.ConfigEditorViewModel>();
                    services.AddSingleton<ViewModels.SystemMonitorViewModel>();
                    services.AddSingleton<ViewModels.NetworkMonitorViewModel>();
                    services.AddSingleton<ViewModels.SettingsViewModel>();
                    services.AddSingleton<MainViewModel>();

                    startupWindow.AppendLog("📦 构建服务容器...");
                    _serviceProvider = services.BuildServiceProvider();

                    // 回到 UI 线程执行需要 UI 交互的部分
                    await startupWindow.Dispatcher.InvokeAsync(() =>
                    {
                        ConfigureRenderOptimizations();
                    });

                    // 检查管理员权限
                    startupWindow.AppendLog("🔐 检查管理员权限...");
                    var privilegeService = _serviceProvider.GetRequiredService<IPrivilegeService>();
                    if (!privilegeService.IsRunningAsAdmin && privilegeService.IsWindows)
                    {
                        startupWindow.AppendLog("⚠️ 当前不是管理员权限，部分功能可能受限");
                        await startupWindow.Dispatcher.InvokeAsync(() =>
                        {
                            var result = System.Windows.MessageBox.Show(
                                "MSMC 检测到当前未以管理员身份运行。\n\n" +
                                "部分功能（如读取其他进程命令行、完整系统监控）可能无法正常工作。\n\n" +
                                "是否立即以管理员权限重新启动？",
                                "权限提示",
                                System.Windows.MessageBoxButton.YesNo,
                                System.Windows.MessageBoxImage.Warning);

                            if (result == System.Windows.MessageBoxResult.Yes)
                            {
                                if (privilegeService.RequestElevation())
                                {
                                    Shutdown();
                                }
                            }
                        });
                    }

                    // 加载全局配置
                    startupWindow.AppendLog("📂 加载全局配置...");
                    _serviceProvider.GetRequiredService<IAppConfigService>().Load();

                    // 主题设置已在启动前加载

                    // 注入主题服务到动画设置
                    AnimationSettings.ThemeService = _serviceProvider.GetRequiredService<IThemeService>();

                    // 加载用户协议
                    startupWindow.AppendLog("📜 加载用户协议状态...");
                    var userAgreementService = _serviceProvider.GetRequiredService<IUserAgreementService>();
                    userAgreementService.Load();

                    // 首次使用显示用户协议窗口
                    if (!userAgreementService.IsAgreed)
                    {
                        startupWindow.AppendLog("📜 首次使用，等待用户同意协议...");
                        bool agreed = false;
                        await startupWindow.Dispatcher.InvokeAsync(() =>
                        {
                            var agreementWindow = new UserAgreementWindow
                            {
                                Owner = startupWindow,
                                WindowStartupLocation = WindowStartupLocation.CenterOwner
                            };
                            var result = agreementWindow.ShowDialog();
                            agreed = result == true;
                        });

                        if (!agreed)
                        {
                            startupWindow.AppendLog("❌ 用户未同意协议");
                            Shutdown();
                            return;
                        }
                        startupWindow.AppendLog("✅ 用户已同意协议");
                    }

                    // 创建主窗口
                    startupWindow.AppendLog("🪟 正在创建主窗口...");
                    MainWindow? mainWindow = null;
                    await startupWindow.Dispatcher.InvokeAsync(() =>
                    {
                        mainWindow = new MainWindow
                        {
                            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
                        };
                    });

                    // 启动内存优化服务（必须在 UI 线程上解析，构造函数访问了 Application.Current）
                    startupWindow.AppendLog("🧹 启动内存优化服务...");
                    await startupWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _serviceProvider.GetRequiredService<MemoryOptimizerService>().Start();
                    });

                    startupWindow.MarkCompleted();

                    // 短暂延迟让用户看到"启动完成"
                    await Task.Delay(600);

                    // 切换到主窗口
                    await startupWindow.Dispatcher.InvokeAsync(() =>
                    {
                        mainWindow?.Show();
                        MainWindow = mainWindow;
                        startupWindow.Close();
                    });

                    Log.Information("✅ McServerGuard 启动完成，主窗口已就绪！");
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, "💥 启动过程发生致命异常");
                    WriteCrashDump(ex);

                    try
                    {
                        startupWindow.MarkFailed($"{ex.Message}");
                        // MarkFailed 显示退出按钮，用户点击后由 CloseButton_Click 处理退出
                        // 不再自动 Shutdown，让用户看到错误详情
                    }
                    catch
                    {
                        // 启动窗口都没了就直接 MessageBox + Shutdown
                        MessageBox.Show($"启动失败：{ex.Message}\n\n{ex.StackTrace}",
                            "MSMC 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        Current.Shutdown();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 启动前期发生致命异常");
            WriteCrashDump(ex);
            MessageBox.Show($"启动失败：{ex.Message}\n\n{ex.StackTrace}",
                "MSMC 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
        }
    }

    /// <summary>
    /// 应用程序退出入口 —— 释放主视图模型与 DI 容器资源，确保后台计时器/事件订阅/取消令牌正确清理
    /// </summary>
    /// <param name="e">退出事件参数</param>
    /// <remarks>
    /// MainViewModel 作为根 ViewModel 持有时钟计时器与所有子页面的事件订阅，
    /// 级联 Dispose 确保 DispatcherTimer、检测循环、监控循环等后台资源在退出时停止。
    /// ServiceProvider.Dispose 释放所有 Singleton 服务（含 ServerDetector 等 IDisposable 服务）。
    /// </remarks>
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("👋 应用退出，开始清理资源...");

        try
        {
            if (_serviceProvider?.GetService(typeof(MainViewModel)) is IDisposable disposableVm)
                disposableVm.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ MainViewModel 释放时发生异常（已忽略）");
        }

        try
        {
            _serviceProvider?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ ServiceProvider 释放时发生异常（已忽略）");
        }

        Log.Information("✅ 资源清理完成，再见！");
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    /// <summary>
    /// 配置三层全局异常防护机制
    /// 覆盖 UI 线程异常、非 UI 线程未处理异常、Task 未观察异常三个层级
    /// </summary>
    private void SetupGlobalExceptionHandling()
    {
        // 第一层：UI 线程 Dispatcher 未处理异常
        DispatcherUnhandledException += (sender, e) =>
        {
            Log.Fatal(e.Exception, "💥 UI 线程未处理异常");
            e.Handled = true;
            ShowCrashReport(e.Exception);
        };

        // 第二层：非 UI 线程未处理异常（最后防线，可能终止进程）
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "💀 非UI线程致命异常 (终止进程={IsTerminating})", e.IsTerminating);
                WriteCrashDump(ex);
            }
        };

        // 第三层：Task 未观察异常（fire-and-forget 任务异常）
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Error(e.Exception, "⚠️ Task未观察异常（火忘了灭）");
            e.SetObserved(); // 标记已观察，防止进程终止
        };
    }

    /// <summary>
    /// 显示崩溃报告对话框
    /// 向用户展示异常信息并提供崩溃转储文件路径
    /// </summary>
    /// <param name="ex">异常对象</param>
    private static void ShowCrashReport(Exception ex)
    {
        try
        {
            var crashLog = WriteCrashDump(ex);
            var msg = $"💥 哎呀，程序出了点问题！\n\n" +
                      $"错误信息：{ex.Message}\n\n" +
                      $"详细日志已保存到：{crashLog}\n" +
                      $"你可以把这个文件发给开发者排查问题。\n\n" +
                      $"点击确定继续使用（不保证稳定），点击取消退出程序。";

            var result = MessageBox.Show(msg, "MSMC 崩溃了 🫠",
                MessageBoxButton.OKCancel, MessageBoxImage.Error);

            if (result == MessageBoxResult.Cancel)
            {
                Current.Shutdown();
            }
        }
        catch
        {
            // 崩溃报告本身失败时静默处理
            Log.Fatal(ex, "连崩溃报告都崩了，我尽力了...");
        }
    }

    /// <summary>
    /// 配置 WPF 渲染管线优化
    /// 包括硬件渲染、字体渲染、多线程优化等配置
    /// </summary>
    private static void ConfigureRenderOptimizations()
    {
        try
        {
            Log.Information("🎨 配置 WPF 渲染管线优化...");

            // 启用硬件加速渲染（默认值，显式声明确保没有被降级）
            System.Windows.Media.RenderOptions.ProcessRenderMode =
                System.Windows.Interop.RenderMode.Default;

            // 设置渲染模式为硬件渲染
            if (System.Windows.Media.RenderCapability.Tier >> 16 >= 2)
            {
                Log.Information("🖥️ 显卡支持 Tier 2 渲染，启用完全硬件加速");
            }
            else
            {
                Log.Warning("⚠️ 显卡渲染等级较低，部分效果可能降级");
            }

            // 位图缓存策略：不在全局设置，在各页面静态元素上按需使用 BitmapCache
            // 原因：全局缓存可能导致内存占用过高，且动态内容缓存会适得其反

            Log.Information("✅ WPF 渲染管线优化配置完成");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ 渲染优化配置失败，使用默认设置: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 写入崩溃转储文件
    /// 记录异常详情、系统信息与版本号
    /// </summary>
    /// <param name="ex">异常对象</param>
    /// <returns>转储文件路径</returns>
    private static string WriteCrashDump(Exception ex)
    {
        try
        {
            var crashDir = Path.Combine(AppContext.BaseDirectory, "logs", "crashes");
            Directory.CreateDirectory(crashDir);
            var fileName = $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log";
            var filePath = Path.Combine(crashDir, fileName);

            var dump = $"=== MSMC 崩溃报告 ===\n" +
                       $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                       $"版本：{typeof(App).Assembly.GetName().Version}\n" +
                       $"OS：{Environment.OSVersion}\n" +
                       $"\n--- 异常信息 ---\n{ex}\n" +
                       $"\n--- 内部异常 ---\n{ex.InnerException}\n";

            File.WriteAllText(filePath, dump);
            return filePath;
        }
        catch
        {
            return "（崩溃转储写入失败）";
        }
    }
}
