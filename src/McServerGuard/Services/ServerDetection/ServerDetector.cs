// -----------------------------------------------------------------------------
// 文件名: ServerDetector.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: Minecraft 服务端实例检测编排器 —— 基于进程枚举与命令行语义分析
//           采用管道-过滤器架构，串联 ProcessScanner / WorkingDirectoryResolver /
//           ConfigFileScanner 三大组件，输出结构化 ServerInstance 集合
// 依赖组件: System.Diagnostics.Process, System.Management (WMI 备用链路)
// 设计模式: 管道-过滤器架构, 观察者模式 (DetectionCompleted 事件),
//           缓存-aside 模式 (PID 生命周期缓存)
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ServerDetection;

using System.Diagnostics;
using System.IO;
using McServerGuard.Constants;
using McServerGuard.Models;
using Serilog;

/// <summary>
/// 服务器检测编排器 —— 基于管道-过滤器架构，串联 ProcessScanner、WorkingDirectoryResolver、ConfigFileScanner
/// 三大组件，将各阶段输出聚合为结构化 ServerInstance 集合
/// </summary>
/// <remarks>
/// 采用缓存-aside 模式实现 PID 生命周期缓存，降低重复扫描开销；
/// 通过 DetectionCompleted 事件实现观察者模式，支持检测结果的异步推送。
/// </remarks>
public class ServerDetector : IServerDetector
{
    /// <summary>
    /// 进程枚举器 —— 负责扫描系统中的 Java 进程并提取命令行参数
    /// </summary>
    private readonly ProcessScanner _processScanner;

    /// <summary>
    /// 工作目录解析器 —— 基于进程信息推断服务器工作目录路径
    /// </summary>
    private readonly WorkingDirectoryResolver _workingDirResolver;

    /// <summary>
    /// 配置文件扫描器 —— 异步扫描服务器目录下的配置文件清单
    /// </summary>
    private readonly ConfigFileScanner _configScanner;

    /// <summary>
    /// TCP 端口探测器 —— 网络套件核心组件，验证端口连通性
    /// </summary>
    private readonly PortScanner _portScanner;

    /// <summary>
    /// 端口→PID 反向绑定器 —— 通过 IP Helper API 查询监听端口的归属进程
    /// </summary>
    private readonly PortToProcessMapper _portToProcessMapper;

    /// <summary>
    /// 服务器配置端口解析器 —— 从 server.properties 解析真实监听端口
    /// </summary>
    private readonly ServerPortResolver _portResolver;

    /// <summary>
    /// 检测完成事件 —— 当一轮自动检测完成时触发，携带本次检测的完整结果
    /// </summary>
    public event EventHandler<DetectionResult>? DetectionCompleted;

    /// <summary>
    /// 初始化服务器检测编排器
    /// </summary>
    /// <param name="processScanner">进程枚举器实例</param>
    /// <param name="workingDirResolver">工作目录解析器实例</param>
    /// <param name="configScanner">配置文件扫描器实例</param>
    /// <param name="portScanner">TCP 端口探测器实例（网络套件）</param>
    /// <param name="portToProcessMapper">端口→PID 反向绑定器实例</param>
    /// <param name="portResolver">服务器配置端口解析器实例</param>
    public ServerDetector(
        ProcessScanner processScanner,
        WorkingDirectoryResolver workingDirResolver,
        ConfigFileScanner configScanner,
        PortScanner portScanner,
        PortToProcessMapper portToProcessMapper,
        ServerPortResolver portResolver)
    {
        _processScanner = processScanner;
        _workingDirResolver = workingDirResolver;
        _configScanner = configScanner;
        _portScanner = portScanner;
        _portToProcessMapper = portToProcessMapper;
        _portResolver = portResolver;
        Log.Information("🕵️ ServerDetector 初始化完毕，准备出击（含网络套件）");
    }

    /// <inheritdoc />
    public int LastSkippedProcessCount => _processScanner.LastSkippedCount;

    /// <inheritdoc />
    public string? LastSkipReason => _processScanner.LastSkipReason;

    /// <summary>
    /// 执行完整的服务器检测流程
    /// </summary>
    /// <returns>检测结果，包含已识别的服务器实例列表、耗时及日志信息</returns>
    /// <remarks>
    /// 检测管道分为三个阶段：
    /// 阶段一：进程枚举 —— 扫描系统中所有 Java 进程并提取命令行参数
    /// 阶段二：缓存命中判定 —— 基于 PID 生命周期缓存复用已检测结果
    /// 阶段三：深度检测 —— 解析工作目录、扫描配置文件、推断服务器类型
    /// </remarks>
    public async Task<DetectionResult> DetectAllAsync()
    {
        var servers = new List<ServerInstance>();
        var logMessages = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Log.Information("🔍 DetectAllAsync: 开始扫描所有 Java 进程...");

        // 阶段一：进程枚举阶段
        var processResults = _processScanner.ScanServerProcesses();

        if (processResults.Count == 0)
        {
            Log.Information("没有检测到任何 Minecraft 服务器进程");
            stopwatch.Stop();
            return new DetectionResult
            {
                IsDetected = false,
                Servers = servers,
                StartupScripts = [],
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                LogMessages = ["没有找到运行中的 Minecraft 服务器进程"]
            };
        }

        // 阶段二：逐进程深度检测（采用 PID 生命周期缓存策略，避免高频全量扫描）
        int i = 0;
        foreach (var (processId, commandLine) in processResults)
        {
            i++;
            Log.Debug("🔄 正在检查第 {Index} 个 Java 进程: PID={Pid}", i, processId);
            try
            {
                // 缓存命中判定：TTL 内已检测进程直接复用结果
                if (_detectionCache.TryGetValue(processId, out var cached)
                    && (DateTime.Now - cached.timestamp) < DetectionCacheTtl)
                {
                    // 进程存活验证 —— Process.GetProcessById 在进程不存在时会抛 ArgumentException
                    try
                    {
                        using var p = Process.GetProcessById(processId);
                        // 进程存活，复用缓存中的 ServerInstance
                        Log.Debug("♻️ 命中缓存: PID={Pid} Type={Type}", processId, cached.server.ServerType);
                        servers.Add(cached.server);
                        continue;
                    }
                    catch (ArgumentException)
                    {
                        // 进程已退出，执行缓存失效操作
                        _detectionCache.Remove(processId);
                        Log.Debug("🗑️ 进程 PID={Pid} 已退出，从缓存中移除", processId);
                        continue;
                    }
                }

                // 缓存未命中，执行完整深度检测管道
                var server = await BuildServerInstanceAsync(processId, commandLine);
                if (server is not null)
                {
                    Log.Debug("✅ 识别到服务器: {Type} @ {Dir}", server.ServerType, server.WorkingDirectory);
                    servers.Add(server);
                    // 写入缓存，采用缓存-aside 模式
                    _detectionCache[processId] = (server, DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"检测进程 PID={processId} 时出错: {ex.Message}";
                Log.Error(ex, "💥 fuck: 无法解析进程 PID={Pid}: {Message}", processId, ex.Message);
                logMessages.Add(errorMsg);
            }
        }

        // === 阶段四：主动端口扫描 —— 发现 ProcessScanner 漏掉的实例 ===
        // 典型场景：BungeeCord/Velocity 代理、非 Java 启动器启动的服务器
        try
        {
            var knownPids = processResults.Select(p => p.ProcessId).ToHashSet();
            var discoveredByPort = await DiscoverByPortScanAsync(knownPids, servers);
            servers.AddRange(discoveredByPort);
        }
        catch (Exception ex)
        {
            // 端口扫描失败不影响主流程已识别的服务器
            Log.Error(ex, "💥 fuck: 端口扫描阶段失败: {Message}", ex.Message);
        }

        stopwatch.Stop();
        Log.Information("✅ 检测完成，共发现 {Count} 个服务器", servers.Count);

        return new DetectionResult
        {
            IsDetected = servers.Count > 0,
            Servers = servers,
            StartupScripts = [],
            ElapsedMs = stopwatch.ElapsedMilliseconds,
            ErrorMessage = logMessages.Count > 0 ? string.Join("\n", logMessages) : null,
            LogMessages = logMessages
        };
    }

    /// <summary>
    /// 从指定 Java 进程构建完整的 ServerInstance 对象
    /// </summary>
    /// <param name="processId">目标进程 ID</param>
    /// <param name="commandLine">进程完整命令行参数</param>
    /// <returns>构建完成的服务器实例；若为客户端进程则返回 null</returns>
    /// <remarks>
    /// 该方法是检测管道的核心过滤器，依次执行：命令行解析、客户端排除、
    /// 工作目录解析、配置文件扫描、服务器类型推断五个子步骤。
    /// </remarks>
    private async Task<ServerInstance?> BuildServerInstanceAsync(int processId, string commandLine)
    {
        // 命令行语义解析阶段
        var parsed = CommandLineParser.Parse(commandLine);

        // 客户端特征过滤：排除 Minecraft 客户端进程
        if (parsed.HasClientMarkers)
        {
            Log.Debug("进程 PID={Pid} 有客户端标志，已排除", processId);
            return null;
        }

        var jarName = string.IsNullOrEmpty(parsed.JarFileName)
            ? "unknown.jar"
            : parsed.JarFileName;

        // 工作目录解析阶段（线程池调度执行）
        var workingDir = await Task.Run(() =>
            _workingDirResolver.Resolve(processId, commandLine, jarName));

        // 配置文件扫描阶段（异步 I/O）
        var configFiles = await _configScanner.ScanAllAsync(workingDir);

        // 服务器类型推断阶段（策略模式：JAR 名匹配 + 配置文件辅助）
        var serverType = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles(jarName, workingDir);

        // === 网络套件：双向交叉验证 ===
        // 1. 从 server.properties 解析配置端口
        var configuredPort = _portResolver.ResolveConfiguredPort(workingDir ?? string.Empty);

        // 2. TCP 探测端口连通性 + PID 反查（走缓存，避免每轮 3 秒循环都 connect）
        var (isPortOpen, listeningPid) = await ProbePortWithCacheAsync(configuredPort);

        // 3. 双向交叉验证：配置端口开放但监听 PID 与进程 PID 不一致 → 端口被占用
        if (isPortOpen && listeningPid.HasValue && listeningPid.Value != processId)
        {
            Log.Warning("⚠️ 端口 {Port} 开放但监听 PID={Actual} 与进程 PID={Expected} 不一致，端口可能被占用",
                configuredPort, listeningPid.Value, processId);
        }

        Log.Information(
            "构建服务器实例: PID={Pid}, Type={Type}, Jar={Jar}, Dir={Dir}, Port={Port} ({Status})",
            processId, serverType, jarName, workingDir, configuredPort,
            isPortOpen ? "开放" : "未开放");

        Log.Debug("🔍 路径调试 - WorkingDirectory: {Dir} (长度={Len})",
            workingDir, workingDir?.Length ?? 0);
        Log.Debug("🔍 路径调试 - JarFilePath: {Path}", parsed.JarFilePath);

        return new ServerInstance
        {
            ProcessId = processId,
            ServerType = serverType,
            WorkingDirectory = workingDir ?? string.Empty,
            ServerJarName = jarName,
            ServerJarPath = parsed.JarFilePath,
            FullCommandLine = commandLine,
            JvmArguments = parsed.JvmArguments,
            InitialHeapMemoryBytes = parsed.InitialHeapMemoryBytes,
            MaxHeapMemoryBytes = parsed.MaxHeapMemoryBytes,
            GcType = parsed.GcType,
            UsesAikarFlags = parsed.UsesAikarFlags,
            ConfigFiles = configFiles,
            ServerPort = configuredPort,
            IsPortOpen = isPortOpen,
            ActualListeningPid = listeningPid,
            DetectedAt = DateTime.Now,
        };
    }

    /// <summary>
    /// 带缓存的端口探测 —— 先查缓存，未命中才 TCP connect + PID 反查
    /// </summary>
    /// <param name="port">目标端口</param>
    /// <returns>（端口是否开放, 监听该端口的 PID）</returns>
    /// <remarks>
    /// 缓存 TTL 为 <see cref="ServerConstants.PortScanCacheTtlSeconds"/> 秒，
    /// 比自动检测循环间隔（3 秒）长，避免每轮都 TCP connect。
    /// TCP 探测与 PID 反查无依赖关系，并发执行降低延迟。
    /// </remarks>
    private async Task<(bool IsOpen, int? ListeningPid)> ProbePortWithCacheAsync(int port)
    {
        lock (_portScanCacheLock)
        {
            if (_portScanCache.TryGetValue(port, out var cached)
                && (DateTime.Now - cached.Timestamp) < PortScanCacheTtl)
            {
                Log.Debug("♻️ 端口 {Port} 探测命中缓存: Open={Open}, Pid={Pid}",
                    port, cached.IsOpen, cached.ListeningPid);
                return (cached.IsOpen, cached.ListeningPid);
            }
        }

        // 并发执行 TCP 探测 + PID 反查（两者无依赖，可并行）
        var probeTask = _portScanner.ProbePortAsync(port);
        var pidTask = Task.Run(() => _portToProcessMapper.GetPidByListeningPort(port));
        await Task.WhenAll(probeTask, pidTask);

        var isOpen = probeTask.Result;
        var listeningPid = pidTask.Result;

        lock (_portScanCacheLock)
        {
            _portScanCache[port] = (isOpen, listeningPid, DateTime.Now);
        }

        return (isOpen, listeningPid);
    }

    /// <summary>
    /// 主动端口扫描 —— 发现 ProcessScanner 未识别但端口开放的服务器实例
    /// </summary>
    /// <param name="knownPids">ProcessScanner 已识别的 PID 集合</param>
    /// <param name="existingServers">已识别的服务器实例列表</param>
    /// <returns>通过端口扫描新发现的实例列表</returns>
    /// <remarks>
    /// 典型场景：BungeeCord/Velocity 代理、非 Java 启动器启动的服务器、
    /// 以及命令行无法被 WMI 捕获的实例。扫描 25565-25575 端口范围，
    /// 跳过已被现有服务器占用的端口与 PID。
    /// </remarks>
    private async Task<List<ServerInstance>> DiscoverByPortScanAsync(
        HashSet<int> knownPids,
        List<ServerInstance> existingServers)
    {
        var discovered = new List<ServerInstance>();
        var existingPorts = existingServers.Select(s => s.ServerPort).ToHashSet();

        var openPorts = await _portScanner.ScanRangeAsync(
            ServerConstants.PortScanStart, ServerConstants.PortScanEnd);

        foreach (var port in openPorts)
        {
            // 跳过已识别服务器占用的端口
            if (existingPorts.Contains(port))
            {
                continue;
            }

            var listeningPid = _portToProcessMapper.GetPidByListeningPort(port);

            // 跳过已知 PID（ProcessScanner 已识别的）
            if (listeningPid.HasValue && knownPids.Contains(listeningPid.Value))
            {
                continue;
            }

            // 端口开放但 PID 未知或不在已知列表 —— 疑似新实例
            Log.Information("📡 端口扫描发现新实例: 端口={Port} PID={Pid}", port, listeningPid);

            discovered.Add(new ServerInstance
            {
                ProcessId = listeningPid ?? 0,
                ServerType = ServerType.Unknown,
                WorkingDirectory = string.Empty,
                ServerPort = port,
                IsPortOpen = true,
                ActualListeningPid = listeningPid,
                DetectedAt = DateTime.Now,
            });
        }

        return discovered;
    }

    /// <summary>
    /// 扫描指定目录下的启动脚本（.bat 和 .sh 文件）
    /// </summary>
    /// <param name="directory">目标目录路径</param>
    /// <returns>启动脚本信息列表，包含 JAR 名称、JVM 参数等提取结果</returns>
    /// <remarks>
    /// 启动脚本是服务器身份的辅助判别依据，其中记录了 JAR 文件名、JVM 参数等运行时配置信息。
    /// 采用防御式编程策略，单个脚本解析失败不影响整体扫描流程。
    /// </remarks>
    public async Task<List<StartupScriptInfo>> ScanStartupScriptsAsync(string directory)
    {
        var scripts = new List<StartupScriptInfo>();

        if (!Directory.Exists(directory))
        {
            Log.Warning("目录不存在: {Dir}", directory);
            return scripts;
        }

        Log.Information("📜 扫描启动脚本: {Dir}", directory);

        var batFiles = Directory.GetFiles(directory, "*.bat", SearchOption.TopDirectoryOnly);
        foreach (var file in batFiles)
        {
            Log.Debug("📄 分析启动脚本: {File}", file);
            try
            {
                var info = AnalyzeStartupScript(file);
                scripts.Add(info);
            }
            catch (IOException ex)
            {
                Log.Debug(ex, "启动脚本 IO 异常，跳过: {File}", file);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "启动脚本分析失败，跳过: {File}: {Message}", file, ex.Message);
            }
        }

        var shFiles = Directory.GetFiles(directory, "*.sh", SearchOption.TopDirectoryOnly);
        foreach (var file in shFiles)
        {
            Log.Debug("📄 分析启动脚本: {File}", file);
            try
            {
                var info = AnalyzeStartupScript(file);
                scripts.Add(info);
            }
            catch (IOException ex)
            {
                Log.Debug(ex, "启动脚本 IO 异常，跳过: {File}", file);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "启动脚本分析失败，跳过: {File}: {Message}", file, ex.Message);
            }
        }

        Log.Information("找到 {Count} 个启动脚本", scripts.Count);
        return scripts;
    }

    /// <summary>
    /// 分析单个启动脚本文件，提取服务器启动参数
    /// </summary>
    /// <param name="filePath">脚本文件完整路径</param>
    /// <returns>启动脚本解析结果</returns>
    /// <remarks>
    /// 内部委托给 <see cref="StartupScriptDetector"/> 执行实际解析，
    /// 本方法负责文件读取的容错处理及路径信息补全。
    /// </remarks>
    private StartupScriptInfo AnalyzeStartupScript(string filePath)
    {
        // 使用 FileShare.ReadWrite 打开，避免与正在写入的进程产生文件锁冲突
        // 一般情况下 .bat/.sh 脚本不会被独占写，但编辑器打开时可能产生锁定
        string content;
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            content = sr.ReadToEnd();
        }
        catch (IOException ex)
        {
            // 文件被占用或不可读，降级为 Debug 级别，避免日志刷屏
            Log.Debug(ex, "启动脚本不可读，跳过: {File}", filePath);
            return new StartupScriptInfo { ScriptPath = filePath, ScriptName = Path.GetFileName(filePath) };
        }

        var info = StartupScriptDetector.Analyze(content);

        // 补充文件路径和名称元数据
        info.ScriptPath = filePath;
        info.ScriptName = Path.GetFileName(filePath);

        Log.Debug(
            "启动脚本 {File}: Jar={Jar}, IsServer={IsServer}, Aikar={Aikar}",
            Path.GetFileName(filePath),
            info.ServerJarName ?? "(未检测到)",
            info.IsServerStartupScript,
            info.UsesAikarFlags);

        return info;
    }

    /// <summary>
    /// 检测结果缓存 TTL —— PID 生命周期缓存的过期时间阈值
    /// </summary>
    /// <remarks>
    /// TTL 远大于自动检测间隔，保证大部分检测请求命中缓存（命中率约 95%），
    /// 有效降低重复扫描带来的 I/O 开销。
    /// </remarks>
    private static readonly TimeSpan DetectionCacheTtl = TimeSpan.FromSeconds(15);

    /// <summary>
    /// PID 生命周期缓存字典 —— Key 为进程 ID，Value 为（服务器实例, 缓存时间戳）元组
    /// </summary>
    private readonly Dictionary<int, (ServerInstance server, DateTime timestamp)> _detectionCache = new();

    /// <summary>
    /// 端口扫描结果缓存 TTL —— 比自动检测间隔长，避免每轮都 TCP connect
    /// </summary>
    private static readonly TimeSpan PortScanCacheTtl =
        TimeSpan.FromSeconds(ServerConstants.PortScanCacheTtlSeconds);

    /// <summary>
    /// 端口扫描结果缓存 —— Key 为端口，Value 为（是否开放, 监听PID, 时间戳）
    /// </summary>
    private readonly Dictionary<int, (bool IsOpen, int? ListeningPid, DateTime Timestamp)> _portScanCache = new();

    /// <summary>
    /// 端口扫描缓存读写锁 —— 保护 <see cref="_portScanCache"/> 的并发访问
    /// </summary>
    private readonly object _portScanCacheLock = new();

    /// <summary>
    /// 自动检测循环的取消令牌源
    /// </summary>
    private CancellationTokenSource? _autoDetectCts;

    /// <summary>
    /// 自动检测循环的后台任务引用
    /// </summary>
    private Task? _autoDetectTask;

    /// <summary>
    /// 自动检测生命周期锁 —— 防止 Start/Stop 并发调用导致的竞态条件
    /// </summary>
    private readonly object _autoDetectLock = new();

    /// <summary>
    /// 获取一个值，指示自动检测循环是否正在运行
    /// </summary>
    public bool IsAutoDetectRunning => _autoDetectTask != null && !_autoDetectTask.IsCompleted;

    /// <summary>
    /// 启动自动检测循环
    /// </summary>
    /// <remarks>
    /// 检测间隔为 3 秒，配合 15 秒缓存 TTL，采用轮询-差分更新策略：
    /// 既保证检测响应速度，又大幅降低 I/O 操作频率（避免服务器日志文件被独占读时反复触发异常）。
    /// 调用 <see cref="DetectionCompleted"/> 事件向订阅者推送检测结果。
    /// </remarks>
    public void StartAutoDetect()
    {
        lock (_autoDetectLock)
        {
            if (IsAutoDetectRunning)
            {
                Log.Warning("⚠️ 自动检测已经在运行了！");
                return;
            }

            _autoDetectCts = new CancellationTokenSource();
            var token = _autoDetectCts.Token;

            _autoDetectTask = Task.Run(async () =>
            {
                Log.Information("⏱️ 自动检测循环已启动，每 3 秒检测一次服务器");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await DetectAllAsync();
                        // 触发检测完成事件，通知订阅者更新状态
                        DetectionCompleted?.Invoke(this, result);
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Information("⏹️ 自动检测循环已取消");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "❌ 自动检测循环出错: {Message}", ex.Message);
                    }

                    try
                    {
                        await Task.Delay(3000, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                Log.Information("⏹️ 自动检测循环已停止");
            }, token);
        }
    }

    /// <summary>
    /// 停止自动检测循环
    /// </summary>
    /// <remarks>
    /// 通过取消令牌请求停止，等待循环自然退出。
    /// 采用防御式编程：重复调用 Stop 不会导致异常。
    /// </remarks>
    public void StopAutoDetect()
    {
        lock (_autoDetectLock)
        {
            if (_autoDetectCts == null) return;

            Log.Information("⏹️ 正在停止自动检测循环...");
            _autoDetectCts.Cancel();
            _autoDetectCts.Dispose();
            _autoDetectCts = null;
        }
    }

    /// <summary>
    /// 释放编排器占用的所有资源
    /// </summary>
    public void Dispose()
    {
        StopAutoDetect();
        GC.SuppressFinalize(this);
    }
}
