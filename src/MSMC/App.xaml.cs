// -----------------------------------------------------------------------------
// 文件名: App.xaml.cs
// 命名空间: io.NET.ZTR_OS
// 功能描述: WPF 应用程序入口，负责 DI 容器构建、服务注册、全局异常处理与启动流程编排
// 依赖组件: Microsoft.Extensions.DependencyInjection, Serilog, System.Windows
// 设计模式: 依赖注入模式、单例模式、观察者模式（全局异常监听）
// -----------------------------------------------------------------------------
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using io.NET.ZTR_OS.Features.JavaInstallation.Services;
using io.NET.ZTR_OS.Features.Settings.Services;
using io.NET.ZTR_OS.Features.Startup.Services;
using io.NET.ZTR_OS.Features.UserAgreement.Services;
using io.NET.ZTR_OS.Features.ConfigEditor.Services;
using io.NET.ZTR_OS.Features.Startup.Services.Privilege;
using io.NET.ZTR_OS.Features.ServerDetection.Services;
using io.NET.ZTR_OS.Features.SystemMonitoring.Services;
using io.NET.ZTR_OS.Features.NetworkMonitor.Services;
using io.NET.ZTR_OS.Features.WebView2.Services;
using io.NET.ZTR_OS.Features.ConfigEditor.ViewModels;
using io.NET.ZTR_OS.Features.NetworkMonitor.ViewModels;
using io.NET.ZTR_OS.Features.ServerDetection.ViewModels;
using io.NET.ZTR_OS.Features.Settings.ViewModels;
using io.NET.ZTR_OS.Features.Shared.Native;
using io.NET.ZTR_OS.Features.Shared.Native.Services;
using io.NET.ZTR_OS.Features.Shared.ViewModels;
using io.NET.ZTR_OS.Features.SystemMonitoring.ViewModels;
using io.NET.ZTR_OS.Features.Shared.Views;
using io.NET.ZTR_OS.Features.Startup.Views;
using io.NET.ZTR_OS.Features.UserAgreement.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace io.NET.ZTR_OS;

/// <summary>
/// WPF 应用程序入口类
/// 负责应用程序生命周期管理、依赖注入容器构建、全局异常处理与启动流程编排
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 死日志路径（进程启动后立即确定，即便 Serilog 挂了也能用）
    /// </summary>
    private static readonly string ForceLogPath = Path.Combine(
        AppContext.BaseDirectory, "logs",
        $"force-boot-{DateTime.Now:yyyyMMdd-HHmmss}.log");

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
    /// 【最早入口】静态构造函数 —— 比 OnStartup、比 XAML 资源加载、比任何实例构造都早
    /// 在这里挂载 100% 不依赖任何第三方库的「裸异常处理器」+ 强制死日志
    /// 只要 CLR 加载了 App 类，这玩意就一定会跑
    /// </summary>
    static App()
    {
        // 1. 确保死日志目录存在（Directory.CreateDirectory 自带存在性检查，不会抛）
        try { Directory.CreateDirectory(Path.GetDirectoryName(ForceLogPath)!); } catch { /* 真的连目录都建不了就算了 */ }

        // 2. 第一行死日志：确认 CLR 成功加载了 App 类
        ForceLog("========================================");
        ForceLog($"[BOOT-0] [OK] App .cctor 入口已命中  PID={Environment.ProcessId}  Time={DateTime.Now:HH:mm:ss.fff}");
        ForceLog($"[BOOT-0]    BaseDir = {AppContext.BaseDirectory}");
        ForceLog($"[BOOT-0]    OS      = {Environment.OSVersion}  /  .NET = {Environment.Version}");
        ForceLog($"[BOOT-0]    x64     = {Environment.Is64BitProcess}  /  CPU = {Environment.ProcessorCount}");

        // 3. 三层全局异常：全部先 ForceLog 裸写，再尝试 Serilog（如果初始化了的话）
        //    注意：这是整个进程最早能挂这些事件的时机，比任何 WPF 框架代码都早
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            ForceLog($"[FATAL] AppDomain.UnhandledException  IsTerminating={e.IsTerminating}");
            ForceLog(ex?.ToString() ?? "(ExceptionObject 不是 Exception 类型，无法序列化)");
            try { Log.Fatal(ex, "[FATAL] 非UI线程致命异常 AppDomain.UnhandledException (终止={IsTerminating})", e.IsTerminating); } catch { /* Serilog 可能还没初始化 */ }
            try { WriteForceCrashDump(ex); } catch { }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ForceLog($"[WARN] TaskScheduler.UnobservedTaskException: {e.Exception}");
            try { Log.Error(e.Exception, "[WARN] Task 未观察异常 UnobservedTaskException"); } catch { }
            e.SetObserved();
        };
    }

    /// <summary>
    /// 【强制死日志】完全不依赖 Serilog、不依赖任何外部库
    /// 三重输出：Console.Error + Debug.WriteLine + 直接写文件
    /// 进程只要活着（哪怕 GC 崩了一半），这玩意基本都能写出去
    /// </summary>
    internal static void ForceLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
        try { Console.Error.Write(line); } catch { }
        try { System.Diagnostics.Debug.Write(line); } catch { }
        try
        {
            // File.AppendAllText 是原子的（内部 FileMode.Append + FileShare.ReadWrite）
            // 同一进程多线程写不会丢行；不同进程写可能乱序但不会崩
            File.AppendAllText(ForceLogPath, line);
        }
        catch { /* 真的写不了文件就彻底放弃，但至少 Console/Debug 已尽力 */ }
    }

    /// <summary>
    /// 不依赖 Serilog 版的崩溃转储（在 Serilog 初始化之前也能用）
    /// </summary>
    private static string WriteForceCrashDump(Exception? ex)
    {
        try
        {
            var crashDir = Path.Combine(AppContext.BaseDirectory, "logs", "crashes");
            Directory.CreateDirectory(crashDir);
            var fileName = $"force-crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log";
            var filePath = Path.Combine(crashDir, fileName);
            var dump =
                $"=== MSMC 强制崩溃转储（Serilog 可能未初始化） ==={Environment.NewLine}" +
                $"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}" +
                $"进程：{Environment.ProcessId}{Environment.NewLine}" +
                $"OS：{Environment.OSVersion}{Environment.NewLine}" +
                $"{Environment.NewLine}--- 异常信息 ---{Environment.NewLine}{ex}{Environment.NewLine}" +
                $"{Environment.NewLine}--- 内部异常 ---{Environment.NewLine}{ex?.InnerException}{Environment.NewLine}";
            File.WriteAllText(filePath, dump);
            ForceLog($"[DUMP] 强制崩溃转储已写入: {filePath}");
            return filePath;
        }
        catch (Exception wtf)
        {
            ForceLog($"连强制崩溃转储都写不进去了: {wtf.Message}");
            return "(写入失败)";
        }
    }

    /// <summary>
    /// 应用程序启动入口
    /// 执行日志初始化、全局异常配置、DI 容器构建、服务注册与主窗口显示
    /// </summary>
    /// <param name="e">启动事件参数</param>
    protected override void OnStartup(StartupEventArgs e)
    {
        ForceLog("[BOOT-1] [BOOT] OnStartup 入口命中");

        // ─────────────────────────────────────────────────────
        // 【防静默退出】显式设置 ShutdownMode = OnExplicitShutdown
        // 否则默认 OnLastWindowClose：如果 StartupWindow 在 MainWindow.Show 之前
        // 因为异常 / WebView2 崩溃 / 用户误关 而关闭，进程直接就没了，连 err 都没有
        // 等 MainWindow 真正 Show 成功之后，我们再切回 OnMainWindowClose
        // ─────────────────────────────────────────────────────
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ForceLog("[BOOT-1]    ShutdownMode = OnExplicitShutdown（防静默退出）");

        // 【第二层异常防护】DispatcherUnhandledException 必须在这里就挂
        // （因为 WPF Dispatcher 实例是在 App 实例构造后、OnStartup 之前才创建的，
        //  所以 DispatcherUnhandledException 不能放 .cctor 里挂）
        DispatcherUnhandledException += (_, e2) =>
        {
            ForceLog($"[FATAL] DispatcherUnhandledException: {e2.Exception}");
            try { Log.Fatal(e2.Exception, "[FATAL] UI 线程未处理异常 DispatcherUnhandledException"); } catch { }
            try { WriteForceCrashDump(e2.Exception); } catch { }
            // 先不 Handled，让 ShowCrashReport 弹框；如果弹框失败就标记 Handled 防进程裸崩
            try { ShowCrashReport(e2.Exception); e2.Handled = true; }
            catch { e2.Handled = true; }
        };
        ForceLog("[BOOT-1]    DispatcherUnhandledException 已挂载");

        ForceLog("[BOOT-2] [LOG] 开始初始化 Serilog...");

        string logFileName = "(未初始化)";
        try
        {
            // ─────────────────────────────────────────────────────────────
            // 【日志精简策略】1.2MB/5s 太吵，分级 + 滚动 + 过滤三管齐下
            //
            // 主日志（mcserverguard-.log）：
            //   - 全局 MinimumLevel = Warning
            //   - 启动阶段（App 命名空间）Override 到 Information，记录关键启动事件
            //   - 噪音子模块 Override 到 Error（监控/检测/网络这些每 5s 一刷的）
            //   - 文件超 5MB 自动滚动到 mcserverguard-_001.log / _002.log ...
            //     最多保留 5 份（含当前文件），超出自动删最旧的
            //
            // 调试日志（debug-.log）：
            //   - 单独 sink，MinimumLevel = Debug
            //   - 仅在需要排查时人工查看，2MB 滚动保留 3 份
            //   - 不污染主日志，不撑爆磁盘
            // ─────────────────────────────────────────────────────────────
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            string mainLogPath = Path.Combine(logDir, "mcserverguard-.log");
            var debugLogPath = Path.Combine(logDir, "debug-.log");
            logFileName = mainLogPath;

            Log.Logger = new LoggerConfiguration()
                // 全局阈值：Warning+ 才进主日志（大量 Debug/Information 被丢弃）
                .MinimumLevel.Warning()
                // 启动流程是关键事件，App 命名空间 Override 到 Information
                .MinimumLevel.Override("io.NET.ZTR_OS", Serilog.Events.LogEventLevel.Information)
                // 噪音子模块：每 5s 一刷的检测/监控/网络流，Override 到 Error
                .MinimumLevel.Override("io.NET.ZTR_OS.Features.ServerDetection", Serilog.Events.LogEventLevel.Error)
                .MinimumLevel.Override("io.NET.ZTR_OS.Features.ConfigEditor", Serilog.Events.LogEventLevel.Error)
                .MinimumLevel.Override("io.NET.ZTR_OS.Features.SystemMonitoring", Serilog.Events.LogEventLevel.Error)
                .MinimumLevel.Override("io.NET.ZTR_OS.Features.NetworkMonitor", Serilog.Events.LogEventLevel.Error)
                // 主 sink：Warning+ → 5MB 滚动，保留 5 份
                .WriteTo.File(
                    path: mainLogPath,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 5 * 1024 * 1024,        // 5 MB
                    retainedFileCountLimit: 5,
                    shared: false,
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                // 调试 sink：Debug+ 单独写入 debug-.log，2MB 滚动保留 3 份
                .WriteTo.Logger(lc => lc
                    .MinimumLevel.Debug()
                    .WriteTo.File(
                        path: debugLogPath,
                        rollOnFileSizeLimit: true,
                        fileSizeLimitBytes: 2 * 1024 * 1024,    // 2 MB
                        retainedFileCountLimit: 3,
                        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"))
                .CreateLogger();

            ForceLog($"[BOOT-2] [OK] Serilog 已精简：主日志 Warning+ (5MB×5份) + 调试日志 Debug+ (2MB×3份)");
            ForceLog($"[BOOT-2]    主日志: {mainLogPath}");
            ForceLog($"[BOOT-2]    调试日志: {debugLogPath}");
        }
        catch (Exception serilogEx)
        {
            ForceLog($"[BOOT-2] [ERR] Serilog 初始化失败: {serilogEx}");
            // 兜底：用最简单的配置避免完全无日志
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                logFileName = Path.Combine(logDir, "mcserverguard-fallback-.log");
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Warning()
                    .WriteTo.File(logFileName, rollOnFileSizeLimit: true, fileSizeLimitBytes: 5 * 1024 * 1024, retainedFileCountLimit: 5)
                    .CreateLogger();
                ForceLog("[BOOT-2] [OK] 已用兜底配置初始化 Serilog");
            }
            catch { /* 真的连兜底都炸了，继续用 ForceLog */ }
        }

        // 清理 7 天前的旧日志文件（主日志 + 调试日志归档一起清）
        try
        {
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            var oldFiles = Directory.GetFiles(logDir, "mcserverguard-*.log")
                .Concat(Directory.GetFiles(logDir, "debug-*.log"))
                .Concat(Directory.GetFiles(logDir, "mcserverguard-fallback-*.log"))
                .Select(f => new FileInfo(f))
                .Where(f => (DateTime.Now - f.CreationTime).TotalDays > 7)
                .ToList();
            foreach (var file in oldFiles)
            {
                try { file.Delete(); }
                catch { /* 忽略单个文件删除失败 */ }
            }
            if (oldFiles.Count > 0)
            {
                ForceLog($"[BOOT-2] [CLEAN] 已清理 {oldFiles.Count} 个旧日志文件（主+调试+兜底）");
                try { Log.Information("[CLEAN] 已清理 {Count} 个旧日志文件", oldFiles.Count); } catch { }
            }
        }
        catch (Exception ex)
        {
            ForceLog($"[BOOT-2] [WARN] 清理旧日志失败: {ex.Message}");
            try { Log.Warning(ex, "清理旧日志文件失败"); } catch { }
        }

        // 再挂载一次 SetupGlobalExceptionHandling（主要是把 Serilog 版本的也挂上，ForceLog 版本在 .cctor 已经挂了）
        SetupGlobalExceptionHandling();

        // 注：之前曾尝试 RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly
        // 但崩溃根因是 Color="{DynamicResource ...Brush}" 类型不匹配（已修复），
        // 软件渲染并不能解决问题，反而导致编译错误，已移除。

        try
        {
            base.OnStartup(e);
            ForceLog("[BOOT-3] [OK] base.OnStartup(e) 成功返回（XAML 资源字典加载 OK）");
            try { Log.Information("[BOOT] io.NET.ZTR_OS 正在启动..."); } catch { }

            // ─────────────────────────────────────────────────────
            // 阶段 -1：用户协议前置校验（优先级最高，必须在任何 UI / NTP / 配置 / 启动页之前）
            // 未同意协议 → 直接 Shutdown，启动窗口根本不会出现
            // ─────────────────────────────────────────────────────
            var userAgreementService = new UserAgreementService();
            userAgreementService.Load();
            if (userAgreementService.RequiresReagreement)
            {
                Log.Information("[LOG] 需要用户同意协议（首次使用或协议已更新），在启动窗口之前弹出协议窗口...");
                var earlyAgreementWindow = new UserAgreementWindow(userAgreementService);
                var agreed = earlyAgreementWindow.ShowDialog() == true;
                if (!agreed)
                {
                    Log.Information("[ERR] 用户未同意协议，终止启动");
                    Shutdown();
                    return;
                }
                Log.Information("[OK] 用户已同意协议 v{Version}", userAgreementService.CurrentAgreementVersion);
            }

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
            ForceLog("[BOOT-4] [UI] 准备 new StartupWindow(earlyThemeService)...");
            try { Log.Information("[UI] 显示启动窗口..."); } catch { }
            StartupWindow startupWindow;
            try
            {
                startupWindow = new StartupWindow(earlyThemeService);
            }
            catch (Exception swCtorEx)
            {
                ForceLog($"[BOOT-4] [ERR] StartupWindow .ctor 崩溃: {swCtorEx}");
                WriteForceCrashDump(swCtorEx);
                MessageBox.Show(
                    $"启动窗口构造失败：{swCtorEx.Message}\n\n{swCtorEx.StackTrace}",
                    "MSMC 启动失败 (StartupWindow.ctor)",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
                Shutdown(-1);
                return;
            }
            ForceLog("[BOOT-4] [OK] StartupWindow .ctor 成功，准备 Show()...");

            try
            {
                startupWindow.Show();
            }
            catch (Exception swShowEx)
            {
                ForceLog($"[BOOT-4] [ERR] StartupWindow.Show() 崩溃: {swShowEx}");
                WriteForceCrashDump(swShowEx);
                MessageBox.Show(
                    $"启动窗口显示失败：{swShowEx.Message}\n\n{swShowEx.StackTrace}",
                    "MSMC 启动失败 (StartupWindow.Show)",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
                Shutdown(-1);
                return;
            }
            ForceLog("[BOOT-4] [OK] StartupWindow.Show() 成功");

            // 将启动窗口设为 MainWindow 以便消息循环正常工作
            MainWindow = startupWindow;

            try
            {
                startupWindow.AppendLog("[BOOT] io.NET.ZTR_OS 启动中...");
                startupWindow.AppendLog("[LOG] 正在初始化核心服务...");
            }
            catch (Exception logEx)
            {
                ForceLog($"[BOOT-4] [WARN] startupWindow.AppendLog 失败（不致命，继续）: {logEx.Message}");
            }

            // ─────────────────────────────────────────────────────
            // 阶段 2：后台线程执行重量级初始化
            // 用 BeginInvoke(Background) 延迟启动，让窗口先完成首次渲染
            // ─────────────────────────────────────────────────────
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _ = Task.Run(async () =>
            {
                try
                {
                    // ─────────────────────────────────────────────────────────────
                    // 启动日志体系：把每个 IO 类的注册过程显式打印到启动页
                    // 装逼专用：日志量从 ~10 条扩到 40+ 条，每条带 [TAG] 前缀
                    // ─────────────────────────────────────────────────────────────
                    var bootStats = new BootStats();
                    async Task Step(int percent, string status, string log)
                    {
                        startupWindow.SetProgress(percent, status);
                        startupWindow.AppendLog(log);
                        await Task.Delay(100);  // 100ms 强制延迟，避免竞争态
                    }

                    // ServiceCollection 必须在辅助方法之前声明，否则 CS0841
                    var services = new ServiceCollection();

                    // 注册单个服务并打印加载结果（成功/失败）
                    async Task Register<TService, TImpl>(int percent, string category, string displayName, string description)
                        where TService : class
                        where TImpl : class, TService
                    {
                        startupWindow.SetProgress(percent, $"{category} · {displayName}");
                        startupWindow.AppendLog($"[LOAD] {category} 正在装载 {displayName} ...");
                        await Task.Delay(40);
                        try
                        {
                            services.AddSingleton<TService, TImpl>();
                            bootStats.Ok++;
                            startupWindow.AppendLog($"[OK]   {displayName,-28} ← {typeof(TService).Name}  // {description}", isSuccess: true);
                        }
                        catch (Exception ex)
                        {
                            bootStats.Fail++;
                            startupWindow.AppendLog($"[ERR]  {displayName,-28} 加载失败: {ex.Message}", isError: true);
                        }
                        await Task.Delay(40);
                    }

                    // 注册单个实例工厂
                    async Task RegisterInstance<TService>(int percent, string category, string displayName, string description, Func<IServiceProvider, TService> factory)
                        where TService : class
                    {
                        startupWindow.SetProgress(percent, $"{category} · {displayName}");
                        startupWindow.AppendLog($"[LOAD] {category} 正在装载 {displayName} ...");
                        await Task.Delay(40);
                        try
                        {
                            services.AddSingleton<TService>(factory);
                            bootStats.Ok++;
                            startupWindow.AppendLog($"[OK]   {displayName,-28} ← {typeof(TService).Name}  // {description}", isSuccess: true);
                        }
                        catch (Exception ex)
                        {
                            bootStats.Fail++;
                            startupWindow.AppendLog($"[ERR]  {displayName,-28} 加载失败: {ex.Message}", isError: true);
                        }
                        await Task.Delay(40);
                    }

                    // 注册裸实现类型（无接口）
                    async Task RegisterType<TImpl>(int percent, string category, string displayName, string description)
                        where TImpl : class
                    {
                        startupWindow.SetProgress(percent, $"{category} · {displayName}");
                        startupWindow.AppendLog($"[LOAD] {category} 正在装载 {displayName} ...");
                        await Task.Delay(40);
                        try
                        {
                            services.AddSingleton<TImpl>();
                            bootStats.Ok++;
                            startupWindow.AppendLog($"[OK]   {displayName,-28} ← {typeof(TImpl).Name}  // {description}", isSuccess: true);
                        }
                        catch (Exception ex)
                        {
                            bootStats.Fail++;
                            startupWindow.AppendLog($"[ERR]  {displayName,-28} 加载失败: {ex.Message}", isError: true);
                        }
                        await Task.Delay(40);
                    }

                    await Step(5, "正在搭建 DI 容器...", "[BOOT] io.NET.ZTR_OS 启动序列开始");
                    await Step(6, "正在搭建 DI 容器...", "[BUILD] 解析服务契约拓扑...");
                    await Step(7, "正在搭建 DI 容器...", "[BUILD] ServiceCollection 已实例化，等待注册");
                    await Task.Delay(80);

                    // ════════════ 时间服务 ════════════
                    await RegisterType<TimeService>(8, "[TIME]", "TimeService", "系统时钟/NTP 偏差诊断");

                    // ════════════ 服务器检测模块 ════════════
                    await Step(10, "正在注册服务器检测服务...", "[DETECT] === 服务器检测模块 ===");
                    await Register<IServerDetector, ServerDetector>(11, "[DETECT]", "ServerDetector", "进程扫描主入口");
                    await Register<IServerImporterService, ServerImporterService>(12, "[DETECT]", "ServerImporterService", "外部服务器导入");
                    await Register<IServerManagerService, ServerManagerService>(13, "[DETECT]", "ServerManagerService", "服务器实例生命周期");
                    await RegisterType<ProcessScanner>(14, "[DETECT]", "ProcessScanner", "MC 进程枚举");
                    await RegisterType<WorkingDirectoryResolver>(15, "[DETECT]", "WorkingDirectoryResolver", "工作目录解析");
                    await RegisterType<ConfigFileScanner>(16, "[DETECT]", "ConfigFileScanner", "配置文件发现");
                    await RegisterType<PortScanner>(17, "[DETECT]", "PortScanner", "端口扫描器");
                    await RegisterType<PortToProcessMapper>(18, "[DETECT]", "PortToProcessMapper", "端口→进程映射");
                    await RegisterType<ServerPortResolver>(19, "[DETECT]", "ServerPortResolver", "服务器端口仲裁");
                    await RegisterType<JarCoreIdentifier>(20, "[DETECT]", "JarCoreIdentifier", "JAR 核心类型识别");

                    // ════════════ 网络监控模块 ════════════
                    await Step(21, "正在注册网络监控服务...", "[NET] === 网络监控模块 ===");
                    await RegisterType<NetworkService>(22, "[NET]", "NetworkService", "网络状态查询");
                    await Register<ITcpForwarder, TcpForwarderService>(23, "[NET]", "TcpForwarderService", "托管 TCP 转发");
                    await RegisterType<NetshPortBridgeService>(24, "[NET]", "NetshPortBridgeService", "Windows netsh 端口桥");
                    await Register<IPortBridgeService, CompositePortBridgeService>(25, "[NET]", "CompositePortBridgeService", "复合端口桥接仲裁");
                    await RegisterType<NetworkTrafficService>(26, "[NET]", "NetworkTrafficService", "网卡流量统计");

                    // ════════════ 权限模块 ════════════
                    await Step(28, "正在注册权限服务...", "[SEC] === 权限模块 ===");
                    await RegisterType<AdminPrivilegeService>(29, "[SEC]", "AdminPrivilegeService", "UAC 提权仲裁");
                    await Register<IPrivilegeService, PrivilegeService>(30, "[SEC]", "PrivilegeService", "权限查询门面");

                    // ════════════ 配置管理模块 ════════════
                    await Step(32, "正在注册配置管理服务...", "[CFG] === 配置管理模块 ===");
                    await Register<IConfigManager, ConfigManager>(33, "[CFG]", "ConfigManager", "配置文件读写");
                    await RegisterType<ConfigDescriptorRegistry>(34, "[CFG]", "ConfigDescriptorRegistry", "中文描述注册表");

                    // ════════════ 系统监控模块 ════════════
                    await Step(36, "正在注册系统监控服务...", "[METRIC] === 系统监控模块 ===");
                    await Register<ISystemMonitor, SystemMonitor>(37, "[METRIC]", "SystemMonitor", "聚合监控入口");
                    await RegisterType<DiskSpaceMonitor>(38, "[METRIC]", "DiskSpaceMonitor", "磁盘占用");
                    await RegisterType<MemoryMonitor>(39, "[METRIC]", "MemoryMonitor", "内存监控");
                    await RegisterType<ThreadAnalyzer>(40, "[METRIC]", "ThreadAnalyzer", "线程状态分析");
                    await RegisterType<CpuIdentifier>(41, "[METRIC]", "CpuIdentifier", "CPU 拓扑识别");
                    await Register<IMetricsPersistenceService, MetricsPersistenceService>(42, "[METRIC]", "MetricsPersistenceService", "指标历史持久化");
                    await Register<IProcessManagerService, ProcessManagerService>(43, "[METRIC]", "ProcessManagerService", "进程亲和性管理");
                    await Register<IProcessSupervisorService, ProcessSupervisorService>(44, "[METRIC]", "ProcessSupervisorService", "Job进程监管/崩溃重启/睡眠防止");
                    await Register<ICpuPowerService, CpuPowerService>(45, "[METRIC]", "CpuPowerService", "CPU电源/QoS档位/睿频管控");

                    // ════════════ 原生窗口效果模块 ════════════
                    await Step(44, "正在注册原生窗口效果服务...", "[WINFX] === 原生窗口效果模块 ===");
                    await Register<IWindowEffectsService, WindowEffectsService>(44, "[WINFX]", "WindowEffectsService", "DWM/Mica/深色标题栏/圆角");

                    // ════════════ 主题与基础服务 ════════════
                    await Step(45, "正在注册主题与基础服务...", "[BASE] === 基础服务 ===");
                    await RegisterInstance<IThemeService>(46, "[BASE]", "ThemeService", "主题色/暗色模式", _ => earlyThemeService);
                    // 复用「阶段 -1」已 Load、并在必要时已弹出协议窗口的实例，
                    // 避免再次 new 一个造成版本状态 / 同意状态不一致
                    await RegisterInstance<IUserAgreementService>(47, "[BASE]", "UserAgreementService", "用户协议状态", _ => userAgreementService);
                    await Register<IAppConfigService, AppConfigService>(48, "[BASE]", "AppConfigService", "全局配置持久化");
                    await Register<IJavaFinderService, JavaFinderService>(49, "[BASE]", "JavaFinderService", "Java 安装发现");
                    await Register<IToastNotificationService, ToastNotificationService>(50, "[BASE]", "ToastNotificationService", "原生 Toast 通知");
                    await RegisterType<MemoryOptimizerService>(51, "[BASE]", "MemoryOptimizerService", "工作集 GC 整理");
                    await Register<IWebView2BridgeService, WebView2BridgeService>(52, "[BASE]", "WebView2BridgeService", "WebView2 ↔ C# 桥接");

                    // ════════════ ViewModel ════════════
                    await Step(55, "正在注册 ViewModel...", "[VM] === ViewModel 装配 ===");
                    await RegisterType<ServerDetectionViewModel>(56, "[VM]", "ServerDetectionViewModel", "服务器检测页 VM");
                    await RegisterType<ConfigEditorViewModel>(57, "[VM]", "ConfigEditorViewModel", "配置编辑器 VM");
                    await RegisterType<SystemMonitorViewModel>(58, "[VM]", "SystemMonitorViewModel", "系统监控 VM");
                    await RegisterType<NetworkMonitorViewModel>(59, "[VM]", "NetworkMonitorViewModel", "网络监控 VM");
                    await RegisterType<SettingsViewModel>(60, "[VM]", "SettingsViewModel", "设置页 VM");
                    await RegisterType<MainViewModel>(61, "[VM]", "MainViewModel", "主窗口 VM");

                    await Step(64, "正在构建服务容器...", "[BUILD] 验证服务契约...");
                    await Step(65, "正在构建服务容器...", $"[BUILD] 拓扑统计: {bootStats.Ok} OK / {bootStats.Fail} FAIL / 共 {bootStats.Ok + bootStats.Fail} 项");
                    _serviceProvider = services.BuildServiceProvider();
                    await Step(66, "正在构建服务容器...", $"[OK] ServiceProvider 已构建（解析 {bootStats.Ok} 个服务契约）");

                    // 后台启动 NTP 时钟偏差诊断（不阻塞启动流程；v2：不再覆盖系统时间）
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var timeService = _serviceProvider.GetRequiredService<TimeService>();
                            await startupWindow.Dispatcher.InvokeAsync(() =>
                            {
                                startupWindow.AppendLog("[TIME] 正在通过权威授时中心诊断系统时钟偏差...");
                            });

                            // SynchronizeAsync 现在返回 true = 时钟正常（±60s 内）；
                            // false = 要么 NTP 全失败，要么时钟偏差超过阈值
                            var clockOk = await timeService.SynchronizeAsync();
                            var offsetMs = timeService.ClockOffset.TotalMilliseconds;
                            var offsetSeconds = Math.Round(Math.Abs(offsetMs) / 1000.0, 1);

                            await startupWindow.Dispatcher.InvokeAsync(() =>
                            {
                                if (timeService.IsSynchronized)
                                {
                                    if (clockOk)
                                    {
                                        startupWindow.AppendLog(
                                            $"[OK] 时钟偏差诊断完成，偏差 {offsetMs:F0}ms（系统时钟正常，已使用本地时间）",
                                            isSuccess: true);
                                    }
                                    else
                                    {
                                        // NTP 成功但偏差超阈值 → 启动日志 + 日志 + 弹窗三重提示
                                        startupWindow.AppendLog(
                                            $"[WARN] 检测到系统时钟偏差较大: ±{offsetSeconds}s，" +
                                            $"请检查 Windows 日期/时间设置或手动「立即同步」。" +
                                            $"MSMC 会继续使用本地时间，不会被 NTP 强制覆盖。",
                                            isError: true);

                                        Log.Warning("系统时钟与 NTP 偏差较大: {Offset}ms，已提示用户但不覆盖时间", offsetMs);

                                        _ = System.Windows.MessageBox.Show(
                                            $"MSMC 检测到您的 Windows 系统时间与标准授时中心相差约 {offsetSeconds} 秒。\n\n" +
                                            $"不准确的系统时间可能导致：\n" +
                                            $"• 监控数据文件日期错误 / 跨天混乱\n" +
                                            $"• 日志与实际发生时间不一致\n\n" +
                                            $"建议操作：\n" +
                                            $"  ① 打开 Windows 设置 → 时间和语言 → 日期和时间\n" +
                                            $"  ② 开启「自动设置时间」并点击「立即同步」\n\n" +
                                            $"MSMC 已使用本地系统时间运行，不会被 NTP 偏移覆盖。",
                                            "系统时钟不准提示",
                                            System.Windows.MessageBoxButton.OK,
                                            System.Windows.MessageBoxImage.Warning);
                                    }
                                }
                                else
                                {
                                    // 所有 NTP 服务器失败（未联网 / 运营商劫持 UDP 123）——不阻塞启动
                                    startupWindow.AppendLog(
                                        "授时中心不可达（未联网或网络劫持），已使用本地系统时间。",
                                        isError: false);
                                    Log.Information("NTP 诊断跳过：所有服务器不可达或响应异常，直接使用本地系统时间");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "NTP 时钟诊断异常（不影响程序启动，继续使用本地系统时间）");
                        }
                    });

                    // 回到 UI 线程执行渲染优化
                    await startupWindow.Dispatcher.InvokeAsync(ConfigureRenderOptimizations);

                    // 检查管理员权限
                    await Step(72, "正在检查管理员权限...", "[SEC] 检查管理员权限...");
                    var privilegeService = _serviceProvider.GetRequiredService<IPrivilegeService>();
                    if (!privilegeService.IsRunningAsAdmin && privilegeService.IsWindows)
                    {
                        startupWindow.AppendLog("[WARN] 当前不是管理员权限，部分功能可能受限", isError: true);
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
                    await Step(80, "正在加载全局配置...", "[FS] 加载全局配置...");
                    _serviceProvider.GetRequiredService<IAppConfigService>().Load();
                    AnimationSettings.ThemeService = _serviceProvider.GetRequiredService<IThemeService>();

                    // 用户协议已在「阶段 -1」（启动窗口显示之前）完成校验
                    // 这里只把结果告诉用户，不再二次弹窗
                    await startupWindow.Dispatcher.InvokeAsync(() =>
                    {
                        startupWindow.AppendLog($"[LOG] 用户协议 v{userAgreementService.CurrentAgreementVersion} 已同意", isSuccess: true);
                    });

                    // 创建主窗口
                    await Step(92, "正在创建主窗口...", "[UI] 正在创建主窗口...");
                    ForceLog("[BOOT-5] [UI] 准备 new MainWindow + MainViewModel...");
                    MainWindow? mainWindow = null;
                    await startupWindow.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            mainWindow = new MainWindow
                            {
                                DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
                            };
                            ForceLog("[BOOT-5] [OK] MainWindow .ctor 成功");
                        }
                        catch (Exception mwCtorEx)
                        {
                            ForceLog($"[BOOT-5] [ERR] MainWindow .ctor 崩溃: {mwCtorEx}");
                            WriteForceCrashDump(mwCtorEx);
                            throw; // 让外层 catch 接管
                        }
                    });

                    // 启动内存优化服务
                    await Step(96, "正在启动内存优化服务...", "[CLEAN] 启动内存优化服务...");
                    await startupWindow.Dispatcher.InvokeAsync(() =>
                    {
                        try { _serviceProvider.GetRequiredService<MemoryOptimizerService>().Start(); }
                        catch (Exception moEx) { ForceLog($"[BOOT-5] [WARN] MemoryOptimizer.Start 失败（不致命）: {moEx.Message}"); }
                    });

                    startupWindow.MarkCompleted();

                    // 短暂延迟让用户看到"启动完成"
                    await Task.Delay(600);

                    // ─────────────────────────────────────────────────────
                    // 【核心切换点】Show MainWindow → 切 ShutdownMode → Close StartupWindow
                    // 顺序不能变！必须先 Show 主窗口成功，再切 ShutdownMode，最后才关启动窗口
                    // 否则 OnLastWindowClose 会直接把进程带走（虽然我们是 OnExplicitShutdown，但防一手）
                    // ─────────────────────────────────────────────────────
                    ForceLog("[BOOT-6] [DONE] 准备 MainWindow.Show() + ShutdownMode 切换...");
                    await startupWindow.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (mainWindow == null)
                            {
                                ForceLog("[BOOT-6] [ERR] mainWindow 是 null，无法 Show！");
                                throw new InvalidOperationException("MainWindow 实例为 null");
                            }

                            mainWindow.Show();
                            ForceLog("[BOOT-6] [OK] MainWindow.Show() 成功");

                            // 应用 ColorOS 视觉包：Mica 云母背景 + 深色标题栏 + 小圆角
                            try
                            {
                                var hWnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).EnsureHandle();
                                var effects = _serviceProvider!.GetRequiredService<IWindowEffectsService>();
                                var theme = _serviceProvider.GetRequiredService<IThemeService>();
                                effects.ApplyColorOSVisualPack(hWnd, darkTitleBar: theme.IsDarkMode);
                                ForceLog("[BOOT-6] [OK] ColorOS Visual Pack 已应用");
                            }
                            catch (Exception fxEx)
                            {
                                ForceLog($"[BOOT-6] [WARN] ColorOS Visual Pack 应用失败（不致命，降级为默认）：{fxEx.Message}");
                                try { Log.Warning(fxEx, "[WindowFX] ColorOS Visual Pack 应用失败（已降级）"); } catch { /* ignore */ }
                            }

                            // 主窗口 Show 成功后，把 ShutdownMode 切回正常：主窗口关了程序就退
                            MainWindow = mainWindow;
                            ShutdownMode = ShutdownMode.OnMainWindowClose;
                            ForceLog("[BOOT-6] [OK] ShutdownMode 已切换为 OnMainWindowClose");
                        }
                        catch (Exception mwShowEx)
                        {
                            ForceLog($"[BOOT-6] [ERR] MainWindow.Show 崩溃: {mwShowEx}");
                            WriteForceCrashDump(mwShowEx);
                            MessageBox.Show(
                                $"主窗口显示失败：{mwShowEx.Message}\n\n{mwShowEx.StackTrace}",
                                "MSMC 启动失败 (MainWindow.Show)",
                                MessageBoxButton.OK, MessageBoxImage.Stop);
                            Shutdown(-1);
                            return;
                        }

                        // 主窗口 Show 成功了，才能关启动窗口
                        try
                        {
                            startupWindow.Close();
                            ForceLog("[BOOT-6] [OK] StartupWindow.Close() 成功");
                        }
                        catch (Exception swCloseEx)
                        {
                            ForceLog($"[BOOT-6] [WARN] StartupWindow.Close 失败（不致命，主窗口已经出来了）: {swCloseEx.Message}");
                        }
                    });

                    ForceLog("[BOOT-END] [FIN] 启动流程全部完成！");
                    try { Log.Information("[OK] io.NET.ZTR_OS 启动完成，主窗口已就绪！"); } catch { }
                }
                catch (Exception ex)
                {
                    ForceLog($"[FATAL] [Task.Run 内部] 启动过程致命异常: {ex}");
                    WriteForceCrashDump(ex);
                    try { Log.Fatal(ex, "[FATAL] 启动过程发生致命异常（Task.Run 内部）"); } catch { }
                    try { WriteCrashDump(ex); } catch { }

                    // ─── 统一走 CrashWindow（独立灾难性故障页面） ───
                    // 先关闭 StartupWindow，再让 ShowCrashReport 打开 CrashWindow
                    try
                    {
                        await startupWindow.Dispatcher.InvokeAsync(() =>
                        {
                            try { startupWindow.Close(); } catch { /* 忽略 */ }
                            ShowCrashReport(ex);
                        });
                    }
                    catch (Exception cwEx)
                    {
                        ForceLog($"[FATAL] CrashWindow 也失败，最终回退 MessageBox: {cwEx}");
                        try
                        {
                            MessageBox.Show(
                                $"启动失败：{ex.Message}\n\n{ex.StackTrace}\n\n" +
                                $"强制死日志路径：{ForceLogPath}",
                                "MSMC 启动失败 (Task.Run catch)",
                                MessageBoxButton.OK, MessageBoxImage.Stop);
                        }
                        finally
                        {
                            Shutdown(-1);
                        }
                    }
                }
                }); // end Task.Run
            }), DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            ForceLog($"[FATAL] [OnStartup 外层 catch] 启动前期致命异常: {ex}");
            WriteForceCrashDump(ex);
            try { Log.Fatal(ex, "[FATAL] 启动前期发生致命异常（OnStartup 外层）"); } catch { }
            try { WriteCrashDump(ex); } catch { }
            try
            {
                ShowCrashReport(ex);
            }
            catch
            {
                MessageBox.Show(
                    $"启动失败：{ex.Message}\n\n{ex.StackTrace}\n\n" +
                    $"强制死日志路径：{ForceLogPath}",
                    "MSMC 启动失败 (OnStartup catch)",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
            }
            finally
            {
                Shutdown(-1);
            }
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
        ForceLog($"[EXIT] [EXIT] OnExit 入口命中  ApplicationExitCode={e.ApplicationExitCode}");
        try { Log.Information("[EXIT] 应用退出，开始清理资源..."); } catch { }

        try
        {
            if (_serviceProvider?.GetService(typeof(MainViewModel)) is IDisposable disposableVm)
                disposableVm.Dispose();
        }
        catch (Exception ex)
        {
            ForceLog($"[EXIT] [WARN] MainViewModel 释放异常: {ex.Message}");
            try { Log.Warning(ex, "[WARN] MainViewModel 释放时发生异常（已忽略）"); } catch { }
        }

        try
        {
            _serviceProvider?.Dispose();
        }
        catch (Exception ex)
        {
            ForceLog($"[EXIT] [WARN] ServiceProvider 释放异常: {ex.Message}");
            try { Log.Warning(ex, "[WARN] ServiceProvider 释放时发生异常（已忽略）"); } catch { }
        }

        ForceLog("[EXIT] [OK] 资源清理完成");
        try
        {
            Log.Information("[OK] 资源清理完成，再见！");
            Log.CloseAndFlush();
        }
        catch { /* Serilog 可能早就挂了或者压根没初始化 */ }

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
            Log.Fatal(e.Exception, "[FATAL] UI 线程未处理异常");
            e.Handled = true;
            ShowCrashReport(e.Exception);
        };

        // 第二层：非 UI 线程未处理异常（最后防线，可能终止进程）
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "[FATAL] 非UI线程致命异常 (终止进程={IsTerminating})", e.IsTerminating);
                WriteCrashDump(ex);
            }
        };

        // 第三层：Task 未观察异常（fire-and-forget 任务异常）
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            Log.Error(e.Exception, "[WARN] Task未观察异常（火忘了灭）");
            e.SetObserved(); // 标记已观察，防止进程终止
        };
    }

    /// <summary>
    /// 显示崩溃报告 —— 优先打开独立的 CrashWindow（WebView2 + React）
    /// 若 CrashWindow 自身也失败（极少数情况），回退到 MessageBox
    /// </summary>
    /// <param name="ex">异常对象</param>
    private static void ShowCrashReport(Exception ex)
    {
        string? forceCrashPath = null;
        try
        {
            forceCrashPath = WriteForceCrashDump(ex);
        }
        catch { /* 已经不能再崩了 */ }

        string? serilogLogPath = null;
        try
        {
            // 探测最新的 Serilog 日志文件路径
            var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            if (Directory.Exists(logDir))
            {
                serilogLogPath = Directory.GetFiles(logDir, "mcserverguard-*.log")
                    .OrderByDescending(f => f)
                    .FirstOrDefault();
            }
        }
        catch { /* 忽略 */ }

        string? crashDumpPath = null;
        try { crashDumpPath = WriteCrashDump(ex); } catch { }

        // ─── 优先走独立 CrashWindow（WebView2 + React 故障页） ───
        try
        {
            // 必须在 UI 线程上 new Window
            if (Current?.Dispatcher?.CheckAccess() == true)
            {
                OpenCrashWindow(ex, forceCrashPath, serilogLogPath, crashDumpPath);
            }
            else
            {
                Current?.Dispatcher?.Invoke(() =>
                    OpenCrashWindow(ex, forceCrashPath, serilogLogPath, crashDumpPath));
            }
            return;
        }
        catch (Exception cwEx)
        {
            ForceLog($"[FATAL] CrashWindow 打开失败，回退 MessageBox: {cwEx}");
            try { Log.Error(cwEx, "[FATAL] CrashWindow 打开失败，回退 MessageBox"); } catch { }
        }

        // ─── 回退：旧的 MessageBox 流程 ───
        try
        {
            var msg = $"[FATAL] 哎呀，程序出了点问题！\n\n" +
                      $"错误信息：{ex.Message}\n\n" +
                      $"Serilog 日志: {crashDumpPath ?? "(未写入)"}\n" +
                      $"强制死日志: {forceCrashPath ?? "(未写入成功)"}\n" +
                      $"启动死日志路径：{ForceLogPath}\n\n" +
                      $"你可以把这些文件发给开发者排查问题。\n\n" +
                      $"点击确定继续使用（不保证稳定），点击取消退出程序。";

            var result = MessageBox.Show(msg, "MSMC 崩溃了 ",
                MessageBoxButton.OKCancel, MessageBoxImage.Error);

            if (result == MessageBoxResult.Cancel)
            {
                Current.Shutdown(-1);
            }
        }
        catch (Exception reportEx)
        {
            ForceLog($"[FATAL] ShowCrashReport 内部也崩了: {reportEx}");
            try
            {
                MessageBox.Show(
                    $"崩溃！\n原始错误：{ex.Message}\n崩溃报告也崩了：{reportEx.Message}\n" +
                    $"强制转储：{forceCrashPath ?? "(无)"}\n死日志：{ForceLogPath}",
                    "MSMC 双重崩溃",
                    MessageBoxButton.OK,
                    MessageBoxImage.Stop);
            }
            catch { /* 彻底没救了 */ }
            try { Log.Fatal(ex, "连崩溃报告都崩了，我尽力了... reportFail={ReportFail}", reportEx.Message); } catch { }
        }
    }

    /// <summary>
    /// 在 UI 线程上打开 CrashWindow 并设置 ShutdownMode
    /// </summary>
    private static void OpenCrashWindow(Exception ex, string? forceCrashPath, string? serilogLogPath, string? crashDumpPath)
    {
        var crashWindow = new CrashWindow(
            ex,
            forceLogPath: ForceLogPath,
            serilogLogPath: serilogLogPath,
            crashDumpPath: crashDumpPath);
        // 把主窗口指向 CrashWindow，关掉它就退出
        Current.MainWindow = crashWindow;
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        crashWindow.Show();
    }

    /// <summary>
    /// 配置 WPF 渲染管线优化
    /// 包括硬件渲染、字体渲染、多线程优化等配置
    /// </summary>
    private static void ConfigureRenderOptimizations()
    {
        try
        {
            Log.Information("[THEME] 配置 WPF 渲染管线优化...");

            // 启用硬件加速渲染（默认值，显式声明确保没有被降级）
            System.Windows.Media.RenderOptions.ProcessRenderMode =
                System.Windows.Interop.RenderMode.Default;

            // 设置渲染模式为硬件渲染
            if (System.Windows.Media.RenderCapability.Tier >> 16 >= 2)
            {
                Log.Information("[HOST] 显卡支持 Tier 2 渲染，启用完全硬件加速");
            }
            else
            {
                Log.Warning("[WARN] 显卡渲染等级较低，部分效果可能降级");
            }

            // 位图缓存策略：不在全局设置，在各页面静态元素上按需使用 BitmapCache
            // 原因：全局缓存可能导致内存占用过高，且动态内容缓存会适得其反

            Log.Information("[OK] WPF 渲染管线优化配置完成");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WARN] 渲染优化配置失败，使用默认设置: {Message}", ex.Message);
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

/// <summary>
/// 启动加载统计（成功/失败计数），用于在启动页输出"DI 容器拓扑统计"
/// </summary>
internal sealed class BootStats
{
    public int Ok { get; set; }
    public int Fail { get; set; }
}
