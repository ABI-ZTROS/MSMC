// 🚀 应用程序入口 —— 一切从这里开始，像 Minecraft 一样加载世界 🌍
using System.IO;
using System.Windows;
using System.Windows.Threading;
using McServerGuard.Services;
using McServerGuard.Services.AIService;
using McServerGuard.Services.ConfigManagement;
using McServerGuard.Services.ServerDetection;
using McServerGuard.Services.SystemMonitoring;
using McServerGuard.ViewModels;
using McServerGuard.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace McServerGuard;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    /// <summary>全局 DI 容器 —— 供 View 层按需解析服务（如 IThemeService）</summary>
    public static IServiceProvider Services
    {
        get
        {
            var app = Current as App;
            return app?._serviceProvider ?? throw new InvalidOperationException("DI 容器尚未初始化");
        }
    }

    /// <summary>
    /// 应用启动 —— 搭好舞台，请好演员，然后开演 🎬
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // 📝 先把日志系统安排上 —— 出了问题得有人记录现场
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/mcserverguard-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // ⚠️ 再挂全局异常钩子 —— 后面出啥事都有人兜着
        SetupGlobalExceptionHandling();

        try
        {
            base.OnStartup(e);
            Log.Information("🚀 McServerGuard 正在启动...");

            // 🏗️ 搭建 DI 容器 —— 各位服务请排队注册，一个一个来 🎫
            Log.Information("🏗️ 开始搭建 DI 容器...");
            var services = new ServiceCollection();

        // 🎯 服务器检测服务 —— "让我看看你电脑上藏了几个服务器"
        Log.Information("🎯 注册服务器检测服务组...");
        services.AddSingleton<IServerDetector, ServerDetector>();
        services.AddSingleton<IServerImporterService, ServerImporterService>();
        services.AddSingleton<IServerManagerService, ServerManagerService>();
        services.AddSingleton<ProcessScanner>();
        services.AddSingleton<WorkingDirectoryResolver>();
        services.AddSingleton<ConfigFileScanner>();

        // 📋 配置管理服务 —— "server.properties？spigot.yml？通通给我拿来"
        Log.Information("📋 注册配置管理服务组...");
        services.AddSingleton<IConfigManager, ConfigManager>();
        services.AddSingleton<ConfigDescriptorRegistry>();

        // 📊 系统监控服务 —— "你的 CPU 在燃烧你知道吗"
        Log.Information("📊 注册系统监控服务组...");
        services.AddSingleton<ISystemMonitor, SystemMonitor>();
        services.AddSingleton<DiskSpaceMonitor>();
        services.AddSingleton<MemoryMonitor>();
        services.AddSingleton<ThreadAnalyzer>();

        // 🤖 AI 守护服务 —— "我来帮你预测服务器什么时候炸"
        Log.Information("🤖 注册 AI 守护服务组...");
        services.AddSingleton<IAiGuardService, AiGuardOrchestrator>();
        services.AddSingleton<IAiSelfLearningService>(sp => (IAiSelfLearningService)sp.GetRequiredService<IAiGuardService>());
        services.AddSingleton<LogAnomalyDetector>();
        services.AddSingleton<CrashPredictor>();
        services.AddSingleton<ConfigOptimizer>();
        services.AddSingleton<LogRootCauseAnalyzer>();

        // 🎨 主题服务 —— "换个皮肤，换个心情"
        Log.Information("🎨 注册主题服务...");
        services.AddSingleton<IThemeService, ThemeService>();

        // 📜 用户协议服务 —— "先看协议再用软件"
        Log.Information("📜 注册用户协议服务...");
        services.AddSingleton<IUserAgreementService, UserAgreementService>();

        // 📁 全局配置服务 —— "保存已知服务器和应用配置"
        Log.Information("📁 注册全局配置服务...");
        services.AddSingleton<IAppConfigService, AppConfigService>();

        // 🔔 通知服务 —— "嘿，服务器炸了！"
        Log.Information("🔔 注册通知服务...");
        services.AddSingleton<IToastNotificationService, ToastNotificationService>();

        // 🧠 MainViewModel —— 指挥官当然要注册进 DI 啦！
        // 之前忘了注册它，结果把 DI 容器本身当 DataContext 了，绑定全炸 💥
        Log.Information("🧠 注册 MainViewModel...");
        services.AddSingleton<MainViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // 📂 加载全局配置（已知服务器等）
        Log.Information("📂 加载全局配置...");
        _serviceProvider.GetRequiredService<IAppConfigService>().Load();

        // 🎨 加载主题设置（颜色、动画等）
        Log.Information("🎨 加载主题设置...");
        _serviceProvider.GetRequiredService<IThemeService>().LoadSettings();

        // 📜 加载用户协议状态
        Log.Information("📜 加载用户协议状态...");
        var userAgreementService = _serviceProvider.GetRequiredService<IUserAgreementService>();
        userAgreementService.Load();

        // 📜 如果未同意协议，先显示用户协议窗口
        if (!userAgreementService.IsAgreed)
        {
            Log.Information("📜 首次使用，显示用户协议窗口...");
            var agreementWindow = new UserAgreementWindow();
            var result = agreementWindow.ShowDialog();

            if (result != true)
            {
                Log.Information("❌ 用户未同意协议，退出程序");
                Shutdown();
                return;
            }

            Log.Information("✅ 用户已同意协议");
        }

        // 🪟 创建主窗口，从 DI 获取真正的 MainViewModel 作为 DataContext
        Log.Information("🪟 创建主窗口并注入 DI 服务...");
        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };

        mainWindow.Show();

        Log.Information("📦 MainViewModel 已创建并注入 DI");
        Log.Information("✅ McServerGuard 启动完成，主窗口已就绪！");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "💥 启动过程发生致命异常");
            WriteCrashDump(ex);
            MessageBox.Show($"启动失败：{ex.Message}\n\n{ex.StackTrace}",
                "MSMC 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
        }
    }

    /// <summary>
    /// 三层全局异常防护网：UI线程 / 非UI线程 / Task未观察异常
    /// 就像 Minecraft 的盾，挡得住的挡，挡不住的也留个全尸 🛡️
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

        // 第三层：Task 未观察异常（fire-and-forget 任务崩了）
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Error(e.Exception, "⚠️ Task未观察异常（火忘了灭）");
            e.SetObserved(); // 标记已观察，不让进程崩
        };
    }

    /// <summary>
    /// 崩溃报告 —— 温柔地告诉用户"程序跪了"，而不是直接消失
    /// </summary>
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
            // 连崩溃报告都崩了... 那就安静地走吧
            Log.Fatal(ex, "连崩溃报告都崩了，我尽力了...");
        }
    }

    /// <summary>
    /// 写崩溃转储文件 —— 留个现场，方便事后验尸 🔍
    /// </summary>
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

    /// <summary>
    /// 应用退出 —— 记得关灯锁门 🔒
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("👋 McServerGuard 正在退出，拜拜~");
        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
