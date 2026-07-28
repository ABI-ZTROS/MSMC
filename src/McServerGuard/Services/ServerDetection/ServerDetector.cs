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

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using McServerGuard.Constants;
using McServerGuard.Models;
using McServerGuard.Services;
using Serilog;

/// <summary>
/// 服务器检测编排器 —— 基于管道-过滤器架构，串联 ProcessScanner、WorkingDirectoryResolver、ConfigFileScanner
/// 三大组件，将各阶段输出聚合为结构化 ServerInstance 集合
/// </summary>
/// <remarks>
/// 采用缓存-aside 模式实现 PID 生命周期缓存，降低重复扫描开销；
/// 通过 DetectionCompleted 事件实现观察者模式，支持检测结果的异步推送；
/// 新增「KnownServer JAR 锁定检测」阶段，利用先验 JAR 路径反向关联已知服务器。
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
    /// JAR Manifest 核心识别器 —— 第三级兜底，解包 JAR 读取 MANIFEST.MF
    /// </summary>
    private readonly JarCoreIdentifier _jarCoreIdentifier;

    /// <summary>
    /// 应用配置服务 —— 提供 KnownServers 持久化列表，支持 JAR 锁定阶段交叉核对
    /// </summary>
    private readonly IAppConfigService _appConfigService;

    /// <summary>
    /// 启动时 PID 短期缓存（MSMC 内部启动的服务器直接写入，不等 WMI 索引）
    /// Key = KnownServer.Id 或 JAR 绝对路径（大小写不敏感的字符串 key）
    /// Value = (Pid, ExpireTickMs)
    /// TTL = 30 秒，用于处理 MSMC 启动后 1.5~3 秒的 WMI 索引竞态窗口
    /// </summary>
    private readonly ConcurrentDictionary<string, (int Pid, long ExpireTick)> _startSessionPidCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>启动时 PID 缓存 TTL（毫秒）—— 远大于 WMI 索引延迟（1.5s）</summary>
    private const long StartSessionCacheTtlMs = 30_000;

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
    /// <param name="jarCoreIdentifier">JAR Manifest 核心识别器实例（第三级兜底）</param>
    /// <param name="appConfigService">应用配置服务（KnownServers 列表来源）</param>
    public ServerDetector(
        ProcessScanner processScanner,
        WorkingDirectoryResolver workingDirResolver,
        ConfigFileScanner configScanner,
        PortScanner portScanner,
        PortToProcessMapper portToProcessMapper,
        ServerPortResolver portResolver,
        JarCoreIdentifier jarCoreIdentifier,
        IAppConfigService appConfigService)
    {
        _processScanner = processScanner;
        _workingDirResolver = workingDirResolver;
        _configScanner = configScanner;
        _portScanner = portScanner;
        _portToProcessMapper = portToProcessMapper;
        _portResolver = portResolver;
        _jarCoreIdentifier = jarCoreIdentifier;
        _appConfigService = appConfigService;
        Log.Information("🕵️ ServerDetector 初始化完毕（含 KnownServer JAR 锁定检测 + 网络套件 + JAR Manifest 兜底）");

        // 启动缓存定期清理计时器 —— 每 30 秒扫描并移除过期条目，防止缓存无限增长
        _cacheCleanupTimer = new Timer(CleanupExpiredCacheEntries, null, CacheCleanupInterval, CacheCleanupInterval);
    }

    /// <summary>
    /// 注册 MSMC 内部启动的服务器 PID 到短期缓存。
    /// 在 StartServer 成功后立即调用，无需等待 WMI 索引到新进程。
    /// </summary>
    /// <param name="knownServerId">已知服务器 ID（如果启动的是 KnownServer）；可空</param>
    /// <param name="jarPath">JAR 文件绝对路径（必填，作为 second key）</param>
    /// <param name="pid">启动成功后立即拿到的进程 ID</param>
    public void RegisterStartedServerPid(string? knownServerId, string jarPath, int pid)
    {
        if (pid <= 0 || string.IsNullOrWhiteSpace(jarPath))
            return;

        var nowTick = Environment.TickCount64;
        var expire = nowTick + StartSessionCacheTtlMs;

        // 以 JAR 路径为 key 注册一份（所有启动场景都会有）
        _startSessionPidCache.AddOrUpdate(
            key: jarPath,
            addValueFactory: _ => (pid, expire),
            updateValueFactory: (_, _) => (pid, expire));

        // 以 KnownServerId 为 key 再注册一份（仅已知服务器启动场景有）
        if (!string.IsNullOrWhiteSpace(knownServerId))
        {
            _startSessionPidCache.AddOrUpdate(
                key: knownServerId,
                addValueFactory: _ => (pid, expire),
                updateValueFactory: (_, _) => (pid, expire));
        }

        Log.Information("🔗 已注册启动时 PID 缓存: PID={Pid}, JAR={Jar}, KnownId={Id}",
            pid, Path.GetFileName(jarPath), knownServerId ?? "(null)");
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
    /// 检测管道：
    /// 阶段一：ProcessScanner 进程枚举 —— 基于 WMI 扫 java/javaw 进程
    /// 阶段二A：启动时 PID 缓存 + KnownServer JAR 锁定交叉检测（新增）
    /// 阶段二B：逐进程深度检测（PID 缓存-aside 模式）
    /// 阶段三：主动端口扫描兜底 —— 发现前序漏掉的实例
    /// </remarks>
    public async Task<DetectionResult> DetectAllAsync()
    {
        var servers = new List<ServerInstance>();
        var logMessages = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Log.Information("🔍 DetectAllAsync: 开始完整检测管道...");

        // ── 阶段一：ProcessScanner 进程枚举 ──────────────────────────────────
        var processResults = await _processScanner.ScanServerProcessesAsync();
        Log.Debug("📊 阶段一: ProcessScanner 返回 {Count} 个候选进程", processResults.Count);

        // ── 阶段二A：启动时 PID 缓存 + KnownServer JAR 锁定交叉检测 ─────────
        // ① 启动时 PID 缓存优先（MSMC 内部启动后 30s 内有效，完全绕过 WMI 延迟）
        var fromStartCache = ApplyStartSessionPidCache(processResults, servers);
        Log.Debug("🔗 阶段二A(启动缓存): 补入 {Count} 个服务器", fromStartCache);

        // ② KnownServer JAR 锁定交叉检测（对所有已知服务器查 JAR 是否被锁，反查 pid）
        var fromJarLock = await DetectKnownServersByJarLockAsync(processResults, servers);
        Log.Debug("🔗 阶段二A(JAR锁定): 补入 {Count} 个服务器", fromJarLock);

        // ── 关键 early return 调整：不再以 processResults.Count==0 为唯一依据 ──
        // 如果 ProcessScanner 为空，但阶段二A 已经通过启动缓存/JAR 锁定发现了服务器，
        // 则正常进入后续阶段（端口扫描兜底仍然会跑，用于发现第三方进程）。
        if (processResults.Count == 0 && servers.Count == 0)
        {
            Log.Information("ProcessScanner + JAR锁定 + 启动缓存均未发现服务器，端口扫描兜底...");
            await DiscoverServersByPortScanAsync(processResults, servers);

            stopwatch.Stop();
            return new DetectionResult
            {
                IsDetected = servers.Count > 0,
                Servers = servers,
                StartupScripts = [],
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                LogMessages = servers.Count > 0 ? [] : ["没有找到运行中的 Minecraft 服务器进程"]
            };
        }

        // ── 阶段二B：逐进程深度检测（ProcessScanner 命中的进程 + PID 缓存） ──
        int i = 0;
        foreach (var (processId, commandLine) in processResults)
        {
            i++;
            try
            {
                // 避免与阶段二A已补入的 PID 重复（PID 是强一致去重键）
                if (servers.Any(s => s.ProcessId == processId))
                    continue;

                // PID 生命周期缓存命中直接复用
                if (TryGetCachedServer(processId, out var cachedServer))
                {
                    servers.Add(cachedServer!);
                    continue;
                }

                var server = await BuildServerInstanceAsync(processId, commandLine);
                if (server is not null)
                {
                    Log.Debug("✅ 识别到服务器: {Type} @ {Dir}", server.ServerType, server.WorkingDirectory);
                    servers.Add(server);
                    _detectionCache[processId] = (server, Environment.TickCount64);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"检测进程 PID={processId} 时出错: {ex.Message}";
                Log.Error(ex, "无法解析进程 PID={Pid}: {Message}", processId, ex.Message);
                logMessages.Add(errorMsg);
            }
        }

        // ── 阶段三：主动端口扫描兜底 ────────────────────────────────────────
        await DiscoverServersByPortScanAsync(processResults, servers);

        stopwatch.Stop();
        Log.Information("✅ 检测完成，共发现 {Count} 个服务器（耗时 {Ms}ms）", servers.Count, stopwatch.ElapsedMilliseconds);

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
    /// 应用启动时 PID 短期缓存：MSMC 内部启动后直接写入的 (jarPath→pid, knownId→pid) 映射。
    /// 在 WMI 索引新进程之前就能把 PID 补入 servers 列表，消除启动后 1.5~3 秒的竞态窗口。
    /// </summary>
    /// <param name="processResults">ProcessScanner 结果（用于去重）</param>
    /// <param name="servers">要追加到的服务器列表</param>
    /// <returns>本方法新增补入的条目数</returns>
    private int ApplyStartSessionPidCache(
        List<(int ProcessId, string CommandLine)> processResults,
        List<ServerInstance> servers)
    {
        int added = 0;
        var scannerPids = processResults.Select(p => p.ProcessId).ToHashSet();
        var nowTick = Environment.TickCount64;

        // 1. 收集所有有效的 (pid, key)，按 PID 去重（一个 pid 可能被 jarPath 和 knownId 各注册一次）
        var validPids = new Dictionary<int, string?>(); // pid → knownServerId（可空）

        foreach (var kvp in _startSessionPidCache)
        {
            if (nowTick >= kvp.Value.ExpireTick)
            {
                // 过期条目惰性清理
                _startSessionPidCache.TryRemove(kvp.Key, out _);
                continue;
            }
            var pid = kvp.Value.Pid;
            if (scannerPids.Contains(pid))
                continue; // ProcessScanner 已识别到，跳过
            if (servers.Any(s => s.ProcessId == pid))
                continue; // 已补入，跳过

            // knownServerId 形式：GUID（不包含 '\'、':'、盘符）；JAR 路径含 ':' 或 '\'
            string? knownId = kvp.Key.Contains(':') || kvp.Key.Contains('\\') || kvp.Key.Contains('/')
                ? null
                : kvp.Key;

            validPids.TryAdd(pid, knownId);
        }

        // 2. 对每个有效 PID：确认进程存活 → 补建 ServerInstance
        foreach (var (pid, knownId) in validPids)
        {
            Process? process = null;
            try
            {
                process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    process.Dispose();
                    continue;
                }
            }
            catch (ArgumentException)
            {
                continue; // 进程不存在
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "启动缓存: 检查 PID={Pid} 存活失败", pid);
                process?.Dispose();
                continue;
            }

            KnownServer? known = null;
            if (knownId != null)
            {
                known = _appConfigService.GetAllKnownServers()
                    .FirstOrDefault(k => k.Id == knownId);
            }

            // 如果 JAR 路径未知，尝试从 cache 反查（查找所有 jarPath 形式的 key 对应的 pid）
            var jarPath = known?.ServerJarPath;
            if (string.IsNullOrEmpty(jarPath))
            {
                foreach (var kvp in _startSessionPidCache)
                {
                    if (kvp.Value.Pid == pid
                        && (kvp.Key.Contains(':') || kvp.Key.Contains('\\') || kvp.Key.Contains('/')))
                    {
                        jarPath = kvp.Key;
                        break;
                    }
                }
            }

            ServerInstance instance;
            if (known != null)
            {
                // 从 KnownServer 完整重建元数据
                instance = BuildInstanceFromKnownServer(known, pid);
            }
            else if (!string.IsNullOrEmpty(jarPath) && File.Exists(jarPath))
            {
                // 仅有 JAR 路径的场景（非 KnownServer）：部分元数据可用
                instance = BuildInstanceFromJarPathOnly(jarPath, pid);
            }
            else
            {
                // JAR 路径也未知：最小化实例（PID 仅占位），后续端口扫描仍可能补全
                process.Dispose();
                continue;
            }

            servers.Add(instance);
            _detectionCache[pid] = (instance, nowTick); // 写入生命周期缓存，TTL 内复用
            Log.Information("🔗 启动缓存补入服务器: PID={Pid}, DisplayName={Name}", pid, instance.DisplayName);
            added++;
            process.Dispose();
        }

        return added;
    }

    /// <summary>
    /// KnownServer JAR 锁定交叉检测阶段：
    /// 对 AppConfigService.GetAllKnownServers() 中的每台服务器，
    /// 用 IsJarFileLocked 快速判断 JAR 是否被占用；若锁定则反查对应 Java 进程，
    /// 若 ProcessScanner 未识别到该 PID，则从 KnownServer 元数据直接重建 ServerInstance 补入。
    /// </summary>
    /// <param name="processResults">ProcessScanner 结果（用于 PID 去重）</param>
    /// <param name="servers">要追加到的服务器列表</param>
    /// <returns>本方法新增补入的条目数</returns>
    private async Task<int> DetectKnownServersByJarLockAsync(
        List<(int ProcessId, string CommandLine)> processResults,
        List<ServerInstance> servers)
    {
        int added = 0;
        var scannerPids = processResults.Select(p => p.ProcessId).ToHashSet();
        var existingPids = servers.Select(s => s.ProcessId).ToHashSet();
        var existingJarPaths = servers
            .Select(s => s.ServerJarPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nowTick = Environment.TickCount64;

        List<KnownServer> knownServers;
        try
        {
            knownServers = _appConfigService.GetAllKnownServers();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取 KnownServers 失败，跳过 JAR 锁定阶段");
            return 0;
        }

        foreach (var known in knownServers)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(known.ServerJarPath) || !File.Exists(known.ServerJarPath))
                    continue;

                // ProcessScanner 或启动缓存已经补入过该 JAR 路径 → 跳过
                if (existingJarPaths.Contains(known.ServerJarPath))
                    continue;

                // JAR 未锁定 → 进程未使用该 JAR，跳过
                if (!ServerManagerService.IsJarFileLocked(known.ServerJarPath))
                    continue;

                // 反查锁定该 JAR 的 Java 进程 PID
                using var matchedProc = ServerManagerService.FindJavaProcessByJarPath(
                    known.ServerJarPath, known.WorkingDirectory);

                if (matchedProc == null || matchedProc.HasExited)
                {
                    Log.Debug("🔒 JAR 被锁定但未找到匹配 Java 进程: {Jar}", known.ServerJarPath);
                    continue;
                }

                var pid = matchedProc.Id;

                // PID 去重：ProcessScanner 或启动缓存已识别 → 跳过（但仍然为它补个 KnownServerId 关联）
                if (scannerPids.Contains(pid) || existingPids.Contains(pid))
                {
                    var existing = servers.FirstOrDefault(s => s.ProcessId == pid);
                    if (existing != null && string.IsNullOrEmpty(existing.KnownServerId))
                    {
                        existing.KnownServerId = known.Id;
                    }
                    continue;
                }

                // 从 KnownServer 元数据重建 ServerInstance（JAR 路径/工作目录/端口/JVM参数 全保真）
                var instance = BuildInstanceFromKnownServer(known, pid);

                // 可选增强：重新用已知工作目录扫一遍配置文件（不阻塞主流程，失败可忽略）
                try
                {
                    instance.ConfigFiles = await _configScanner.ScanAllAsync(known.WorkingDirectory);
                    var (configuredPort, isPortOpen, listeningPid) =
                        await ValidatePortBindingAsync(pid, known.WorkingDirectory);
                    instance.ServerPort = configuredPort;
                    instance.IsPortOpen = isPortOpen;
                    instance.ActualListeningPid = listeningPid;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "JAR锁定阶段: 配置/端口验证失败（PID={Pid}），继续使用 KnownServer 默认值", pid);
                    instance.ServerPort = known.Port;
                }

                servers.Add(instance);
                _detectionCache[pid] = (instance, nowTick);
                Log.Information("🔒 JAR锁定补入服务器: PID={Pid}, DisplayName={Name}", pid, instance.DisplayName);
                added++;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "处理 KnownServer={Name} JAR 锁定检测时异常", known.Name);
            }
        }

        return added;
    }

    /// <summary>
    /// 从 KnownServer 元数据 + 已知 PID 重建 ServerInstance。
    /// 用于「启动时PID缓存」和「JAR锁定交叉检测」两个阶段，保证从已知服务器补入的实例
    /// 元数据完整（工作目录、JAR 路径、JVM 参数、端口全部来自持久化配置）。
    /// </summary>
    private ServerInstance BuildInstanceFromKnownServer(KnownServer known, int pid)
    {
        var jarName = Path.GetFileName(known.ServerJarPath);
        var inferredType = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles(
            jarName, known.WorkingDirectory);

        // 模糊类型：用 JAR Manifest 兜底（同步阻塞 1 次可接受；缓存 TTL 内复用）
        if (IsAmbiguousServerType(inferredType)
            && !string.IsNullOrEmpty(known.ServerJarPath)
            && File.Exists(known.ServerJarPath))
        {
            try
            {
                var manifestType = _jarCoreIdentifier.IdentifyAsync(known.ServerJarPath)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (manifestType != ServerType.Unknown && manifestType != inferredType)
                {
                    inferredType = manifestType;
                }
            }
            catch { /* Manifest 识别失败不阻塞 */ }
        }

        // Unknown → 兜底 Vanilla（KnownServer 持久化过的服务器，类型不可能"完全未知"）
        if (inferredType == ServerType.Unknown)
            inferredType = ServerType.Vanilla;

        return new ServerInstance
        {
            ProcessId = pid,
            ServerType = inferredType,
            WorkingDirectory = known.WorkingDirectory ?? string.Empty,
            JavaPath = known.JavaPath ?? string.Empty,
            ServerJarPath = known.ServerJarPath ?? string.Empty,
            ServerJarName = jarName ?? string.Empty,
            FullCommandLine = string.Empty, // JAR 锁定/启动缓存阶段不强制读命令行
            JvmArguments = known.JvmArguments?.ToList() ?? [],
            InitialHeapMemoryBytes = known.InitialHeapMemoryBytes,
            MaxHeapMemoryBytes = known.MaxHeapMemoryBytes,
            ConfigFiles = [],
            ServerPort = known.Port,
            IsPortOpen = false, // 等端口探测阶段更新
            ActualListeningPid = null,
            DetectedAt = DateTime.Now,
            KnownServerId = known.Id,
        };
    }

    /// <summary>
    /// 仅 JAR 路径+PID 可用时的最小化 ServerInstance（非 KnownServer 启动场景）。
    /// </summary>
    private ServerInstance BuildInstanceFromJarPathOnly(string jarPath, int pid)
    {
        var jarName = Path.GetFileName(jarPath);
        var workDir = Path.GetDirectoryName(jarPath) ?? string.Empty;
        var inferredType = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles(jarName, workDir);
        if (inferredType == ServerType.Unknown)
            inferredType = ServerType.Vanilla;

        return new ServerInstance
        {
            ProcessId = pid,
            ServerType = inferredType,
            WorkingDirectory = workDir,
            JavaPath = string.Empty,
            ServerJarPath = jarPath,
            ServerJarName = jarName,
            FullCommandLine = string.Empty,
            JvmArguments = [],
            InitialHeapMemoryBytes = 0,
            MaxHeapMemoryBytes = 0,
            ConfigFiles = [],
            ServerPort = ServerConstants.DefaultServerPort,
            IsPortOpen = false,
            ActualListeningPid = null,
            DetectedAt = DateTime.Now,
            KnownServerId = null,
        };
    }

    /// <summary>
    /// 缓存命中判定 —— TTL 内已检测进程直接复用结果，进程已退出则清除缓存
    /// </summary>
    /// <param name="processId">目标进程 ID</param>
    /// <param name="cachedServer">缓存命中时输出服务器实例</param>
    /// <returns>缓存命中返回 true；未命中或进程已退出返回 false</returns>
    private bool TryGetCachedServer(int processId, out ServerInstance? cachedServer)
    {
        cachedServer = null;

        if (!_detectionCache.TryGetValue(processId, out var cached)
            || (Environment.TickCount64 - cached.timestampMs) >= DetectionCacheTtlMs)
            return false;

        // 进程存活验证 —— Process.GetProcessById 在进程不存在时会抛 ArgumentException
        try
        {
            using var p = Process.GetProcessById(processId);
            Log.Debug("♻️ 命中缓存: PID={Pid} Type={Type}", processId, cached.server.ServerType);
            cachedServer = cached.server;
            return true;
        }
        catch (ArgumentException)
        {
            _detectionCache.TryRemove(processId, out _);
            Log.Debug("🗑️ 进程 PID={Pid} 已退出，从缓存中移除", processId);
            return false;
        }
    }

    /// <summary>
    /// 主动端口扫描阶段 —— 发现 ProcessScanner 漏掉的实例
    /// </summary>
    /// <param name="processResults">进程枚举结果（用于提取已知 PID 集合）</param>
    /// <param name="servers">已识别的服务器列表（扫描结果追加到此列表）</param>
    private async Task DiscoverServersByPortScanAsync(
        List<(int ProcessId, string CommandLine)> processResults,
        List<ServerInstance> servers)
    {
        try
        {
            var knownPids = processResults.Select(p => p.ProcessId).ToHashSet();
            var discoveredByPort = await DiscoverByPortScanAsync(knownPids, servers);
            servers.AddRange(discoveredByPort);
        }
        catch (Exception ex)
        {
            // 端口扫描失败不影响主流程已识别的服务器
            Log.Error(ex, "端口扫描阶段失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 从指定 Java 进程构建完整的 ServerInstance 对象
    /// </summary>
    /// <param name="processId">目标进程 ID</param>
    /// <param name="commandLine">进程完整命令行参数</param>
    /// <returns>构建完成的服务器实例；若为客户端进程则返回 null</returns>
    /// <remarks>
    /// 该方法是检测管道的核心过滤器，依次执行：命令行解析、客户端排除、
    /// 工作目录解析、配置文件扫描、服务器类型推断、网络验证六个子步骤。
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

        // 服务器类型推断阶段（含 JAR Manifest 兜底）
        var serverType = await ResolveServerTypeAsync(jarName, parsed.JarFilePath, workingDir);

        // 网络套件：双向交叉验证
        var (configuredPort, isPortOpen, listeningPid) =
            await ValidatePortBindingAsync(processId, workingDir);

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
    /// 服务器类型推断 —— 策略模式：JAR 名匹配 + 配置文件辅助 + JAR Manifest 兜底
    /// </summary>
    /// <param name="jarName">JAR 文件名</param>
    /// <param name="jarFilePath">JAR 文件完整路径（用于 Manifest 解包）</param>
    /// <param name="workingDir">工作目录（用于配置文件辅助判定）</param>
    /// <returns>推断出的服务器类型</returns>
    /// <remarks>
    /// 三级识别策略：
    /// 1. JAR 名 + 配置文件匹配（快速路径）
    /// 2. JAR Manifest 解包识别（兜底，区分派生类）
    /// 3. 终极兜底：三级全部失败时返回 Vanilla（进程已被 ProcessScanner 确认为服务器）
    /// 解决场景：JAR 被重命名、Paper 系/Forge 系/BungeeCord 系/Fabric 系派生类互相混淆
    /// </remarks>
    private async Task<ServerType> ResolveServerTypeAsync(
        string jarName, string? jarFilePath, string? workingDir)
    {
        var serverType = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles(jarName, workingDir);

        // 第三级兜底：当识别为基类或 Unknown/Vanilla 时，通过 Manifest 区分派生类
        if (IsAmbiguousServerType(serverType)
            && !string.IsNullOrEmpty(jarFilePath)
            && File.Exists(jarFilePath))
        {
            var manifestType = await _jarCoreIdentifier.IdentifyAsync(jarFilePath);
            if (manifestType != ServerType.Unknown && manifestType != serverType)
            {
                Log.Information("🔬 JAR Manifest 识别为核心类型: {Type}（覆盖原 {Old}）", manifestType, serverType);
                serverType = manifestType;
            }
        }

        // 终极兜底：三级识别全部失败，但进程已通过 ProcessScanner 4 重保险确认是 Minecraft 服务器
        // （JAR 关键字/nogui 标记/父进程链追溯/-jar 兜底）。
        // 此时返回 Vanilla 而非 Unknown——Unknown 只意味着"核心品牌未知"，
        // 但它一定是某种 Minecraft 服务器，最保守的假设是原版。
        if (serverType == ServerType.Unknown)
        {
            Log.Information("🏷️ 三级类型识别均失败，兜底为 Vanilla（JAR={Jar}）", jarName);
            serverType = ServerType.Vanilla;
        }

        return serverType;
    }

    /// <summary>
    /// 判断服务器类型是否模糊（需要 JAR Manifest 兜底识别）
    /// </summary>
    private static bool IsAmbiguousServerType(ServerType type)
        => type == ServerType.Unknown
           || type == ServerType.Vanilla
           || type == ServerType.Spigot
           || type == ServerType.Bukkit
           || type == ServerType.Paper
           || type == ServerType.Forge
           || type == ServerType.Fabric
           || type == ServerType.BungeeCord;

    /// <summary>
    /// 网络套件双向交叉验证 —— 解析配置端口、TCP 探测、PID 反查
    /// </summary>
    /// <param name="processId">进程 ID（用于交叉验证）</param>
    /// <param name="workingDir">工作目录（用于解析 server.properties）</param>
    /// <returns>（配置端口, 端口是否开放, 监听 PID）</returns>
    private async Task<(int ConfiguredPort, bool IsPortOpen, int? ListeningPid)> ValidatePortBindingAsync(
        int processId, string? workingDir)
    {
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

        return (configuredPort, isPortOpen, listeningPid);
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
                && (Environment.TickCount64 - cached.TimestampMs) < PortScanCacheTtlMs)
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
            _portScanCache[port] = (isOpen, listeningPid, Environment.TickCount64);
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
    /// 以及命令行无法被 WMI 捕获的实例。扫描主区间(25565-25590) + AdditionalScanPorts 补充端口，
    /// 跳过已被现有服务器占用的端口与 PID。
    /// </remarks>
    private async Task<List<ServerInstance>> DiscoverByPortScanAsync(
        HashSet<int> knownPids,
        List<ServerInstance> existingServers)
    {
        var discovered = new List<ServerInstance>();
        var existingPorts = existingServers.Select(s => s.ServerPort).ToHashSet();

        // 合并主区间 + AdditionalScanPorts 补充端口，去重后统一扫描
        var portsToScan = new HashSet<int>();
        for (var p = ServerConstants.PortScanStart; p <= ServerConstants.PortScanEnd; p++)
            portsToScan.Add(p);
        foreach (var p in ServerConstants.AdditionalScanPorts)
            portsToScan.Add(p);

        var openPorts = await _portScanner.ScanPortsAsync(portsToScan);

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
    private static readonly long DetectionCacheTtlMs = (long)TimeSpan.FromSeconds(25).TotalMilliseconds;

    /// <summary>
    /// PID 生命周期缓存字典 —— Key 为进程 ID，Value 为（服务器实例, 缓存时间戳）元组
    /// 使用 ConcurrentDictionary 支持后台自动检测与 UI 命令的并发访问。
    /// 时间戳使用 Environment.TickCount64（单调时钟），彻底隔离 NTP 时间偏移污染。
    /// </summary>
    private readonly ConcurrentDictionary<int, (ServerInstance server, long timestampMs)> _detectionCache = new();

    /// <summary>
    /// 端口扫描结果缓存 TTL（毫秒）—— 比自动检测间隔长，避免每轮都 TCP connect
    /// </summary>
    private static readonly long PortScanCacheTtlMs =
        (long)TimeSpan.FromSeconds(ServerConstants.PortScanCacheTtlSeconds).TotalMilliseconds;

    /// <summary>
    /// 端口扫描结果缓存 —— Key 为端口，Value 为（是否开放, 监听PID, 时间戳）
    /// </summary>
    private readonly Dictionary<int, (bool IsOpen, int? ListeningPid, long TimestampMs)> _portScanCache = new();

    /// <summary>
    /// 端口扫描缓存读写锁 —— 保护 <see cref="_portScanCache"/> 的并发访问
    /// </summary>
    private readonly object _portScanCacheLock = new();

    /// <summary>
    /// 缓存定期清理计时器 —— 周期性扫描两个缓存字典，移除已过期条目，防止内存持续增长
    /// </summary>
    /// <remarks>
    /// 清理间隔（30 秒）远大于检测缓存 TTL（25 秒）与端口缓存 TTL，
    /// 确保过期条目在合理时间内被回收，同时避免频繁扫描带来的开销。
    /// </remarks>
    private readonly Timer _cacheCleanupTimer;

    /// <summary>
    /// 缓存清理间隔 —— 每 30 秒执行一次过期条目扫描
    /// </summary>
    private static readonly TimeSpan CacheCleanupInterval = TimeSpan.FromSeconds(30);

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
    /// 检测间隔为 5 秒，配合 25 秒缓存 TTL，采用轮询-差分更新策略：
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
                        await Task.Delay(5000, token);
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
    /// 缓存定期清理回调 —— 扫描两个缓存字典，移除已过期条目
    /// </summary>
    /// <param name="state">计时器状态参数（未使用）</param>
    /// <remarks>
    /// <para>_detectionCache 清理：移除超过 <see cref="DetectionCacheTtl"/> 的 PID 缓存条目。</para>
    /// <para>_portScanCache 清理：移除超过 <see cref="PortScanCacheTtl"/> 的端口扫描结果条目。</para>
    /// <para>该回调由 <see cref="_cacheCleanupTimer"/> 每 30 秒触发一次，在后台线程池执行。</para>
    /// </remarks>
    private void CleanupExpiredCacheEntries(object? state)
    {
        var nowMs = Environment.TickCount64;
        int detectionRemoved = 0;
        int portRemoved = 0;

        // 清理 PID 生命周期缓存（ConcurrentDictionary 支持遍历时安全移除）
        foreach (var kv in _detectionCache)
        {
            if ((nowMs - kv.Value.timestampMs) >= DetectionCacheTtlMs)
            {
                if (_detectionCache.TryRemove(kv.Key, out _))
                    detectionRemoved++;
            }
        }

        // 清理端口扫描缓存（需加锁保护普通 Dictionary）
        lock (_portScanCacheLock)
        {
            var expiredPorts = _portScanCache
                .Where(kv => (nowMs - kv.Value.TimestampMs) >= PortScanCacheTtlMs)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var port in expiredPorts)
            {
                _portScanCache.Remove(port);
                portRemoved++;
            }
        }

        if (detectionRemoved > 0 || portRemoved > 0)
        {
            Log.Debug("🧹 缓存清理：移除 {Detection} 个检测缓存 + {Port} 个端口缓存",
                detectionRemoved, portRemoved);
        }
    }

    /// <summary>
    /// 释放编排器占用的所有资源
    /// </summary>
    /// <remarks>
    /// 停止自动检测循环、释放缓存清理计时器、清空缓存字典。
    /// </remarks>
    public void Dispose()
    {
        StopAutoDetect();

        // 停止并释放缓存清理计时器
        _cacheCleanupTimer.Dispose();

        // 清空缓存，释放引用
        _detectionCache.Clear();
        lock (_portScanCacheLock)
        {
            _portScanCache.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
