using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using io.NET.ZTR_OS.Features.JavaInstallation.Services;
using io.NET.ZTR_OS.Features.Settings.Services;
using io.NET.ZTR_OS.Features.Shared.Native.Services;
using io.NET.ZTR_OS.Features.SystemMonitoring.Services;
using JavaInstallationInfo = io.NET.ZTR_OS.Features.JavaInstallation.Services.JavaInstallation;
using io.NET.ZTR_OS.Features.ServerDetection.Models;
using Serilog;

namespace io.NET.ZTR_OS.Features.ServerDetection.Services;

/// <summary>
/// 服务器管理服务契约 —— 定义 Minecraft 服务器进程的生命周期管理接口
/// </summary>
/// <remarks>
/// 涵盖服务器运行状态检测、启动、停止、进程查找、资源指标查询等核心操作。
/// 采用防御式编程策略，所有操作均处理进程退出的竞态条件。
/// </remarks>
public interface IServerManagerService
{
    /// <summary>
    /// 检测指定服务器实例是否正在运行
    /// </summary>
    /// <param name="server">服务器实例</param>
    /// <returns>true 表示服务器进程处于运行状态</returns>
    public bool IsServerRunning(ServerInstance server);

    /// <summary>
    /// 通过 JAR 文件路径检测对应服务器是否正在运行
    /// </summary>
    /// <param name="jarFilePath">JAR 文件完整路径</param>
    /// <returns>true 表示对应服务器进程处于运行状态</returns>
    public bool IsServerRunningByJarPath(string jarFilePath);

    /// <summary>
    /// 启动指定的 Minecraft 服务器实例（旧版兼容同步 API：走 Supervisor，不可用则降级裸 Process.Start）。
    /// </summary>
    /// <param name="server">服务器实例，包含启动所需的全部配置</param>
    /// <returns>启动后的进程对象；启动失败返回 null</returns>
    public Process? StartServer(ServerInstance server);

    /// <summary>
    /// 以「进程监管模式」启动 Minecraft 服务器（推荐异步 API）。
    /// 监管模式：Job Object 绑死子进程树 + 崩溃自动重启 + 优先级 + 内存上限 + 防睡眠。
    /// </summary>
    /// <param name="server">服务器实例</param>
    /// <param name="ct">用户取消令牌（取消=放弃启动尝试，而非停止已启动进程）</param>
    /// <returns>监管句柄（可订阅 StatusChanged 事件监听重启/崩溃）；启动失败返回 null</returns>
    Task<SupervisedProcessHandle?> StartServerSupervisedAsync(ServerInstance server, CancellationToken ct = default);

    /// <summary>
    /// 根据 JAR 路径查询当前正在被监管的服务器句柄（未监管返回 null）。
    /// </summary>
    SupervisedProcessHandle? TryGetSupervisedHandle(string jarFilePath);

    /// <summary>
    /// 当前所有正在被 ProcessSupervisorService 监管的服务器句柄快照（只读）。
    /// Key = 规范化后的 ServerJarPath（ToLowerInvariant）。
    /// </summary>
    IReadOnlyDictionary<string, SupervisedProcessHandle> SupervisedServers { get; }

    /// <summary>
    /// 停止指定的 Minecraft 服务器实例
    /// </summary>
    /// <param name="server">服务器实例</param>
    /// <returns>true 表示停止操作执行成功（或进程本就未运行）</returns>
    public bool StopServer(ServerInstance server);

    /// <summary>
    /// 通过进程 ID 停止服务器进程及其子进程树
    /// </summary>
    /// <param name="processId">父进程 ID</param>
    /// <returns>true 表示停止操作执行成功</returns>
    public bool StopServerByProcessId(int processId);

    /// <summary>
    /// 查找与指定服务器实例匹配的运行中进程
    /// </summary>
    /// <param name="server">服务器实例</param>
    /// <returns>匹配的进程对象；未找到返回 null</returns>
    public Process? FindServerProcess(ServerInstance server);

    /// <summary>
    /// 获取指定 JAR 文件对应的服务器进程 ID
    /// </summary>
    /// <param name="jarFilePath">JAR 文件完整路径</param>
    /// <returns>进程 ID；未找到返回 null</returns>
    public int? GetServerProcessId(string jarFilePath);

    /// <summary>
    /// 检测是否有任何 Minecraft 服务器正在运行
    /// </summary>
    /// <returns>true 表示至少有一台服务器在运行</returns>
    public bool AnyServerRunning();

    /// <summary>
    /// 获取指定进程的内存使用量
    /// </summary>
    /// <param name="processId">进程 ID</param>
    /// <returns>工作集内存字节数；进程不存在或读取失败返回 null</returns>
    public long? GetProcessMemoryUsage(int processId);

    /// <summary>
    /// 获取指定进程的 CPU 使用率
    /// </summary>
    /// <param name="processId">进程 ID</param>
    /// <returns>CPU 使用率百分比近似值；进程不存在或读取失败返回 null</returns>
    /// <remarks>
    /// 注意：准确的 CPU 使用率需要两次采样计算，此处基于内存占比返回近似参考值。
    /// </remarks>
    public double? GetProcessCpuUsage(int processId);
}

/// <summary>
/// 服务器管理服务实现 —— 提供 Minecraft 服务器进程的生命周期管理能力
/// </summary>
/// <remarks>
/// 核心能力包括：
/// 1. 运行状态检测 —— 基于 JAR 文件锁 + 进程枚举的双重校验机制
/// 2. 进程生命周期管理 —— 启动、停止（含子进程树终止）
/// 3. 资源指标查询 —— 内存、CPU 使用率采集
/// 所有操作均处理进程枚举过程中的竞态条件，遵循防御式编程原则。
/// </remarks>
public class ServerManagerService : IServerManagerService
{
    private readonly IJavaFinderService _javaFinderService;
    private readonly IProcessSupervisorService? _supervisor; // 非 Windows / 禁用原生服务时为 null
    private readonly IAppConfigService? _config;             // 可选：若配置服务不可用则走默认策略

    /// <summary>
    /// 当前被监管的服务器句柄字典。
    /// Key = 规范化后的 JAR 完整路径（ToLowerInvariant）。
    /// Value = ProcessSupervisorService 返回的 SupervisedProcessHandle。
    /// </summary>
    private readonly ConcurrentDictionary<string, SupervisedProcessHandle> _supervisedHandles = new();

    public ServerManagerService(
        IJavaFinderService javaFinderService,
        IProcessSupervisorService? supervisor = null,
        IAppConfigService? config = null)
    {
        _javaFinderService = javaFinderService;
        _supervisor = supervisor;
        _config = config;

        if (_supervisor != null)
            Log.Information("[SUP] ProcessSupervisorService 已注入，服务器启动将默认走监管模式");
        else
            Log.Warning("[SUP] ProcessSupervisorService 未注入（非 Windows 平台或服务未注册），启动降级为裸 Process.Start");
    }
    /// <summary>
    /// 检测指定服务器实例是否正在运行
    /// </summary>
    /// <param name="server">服务器实例</param>
    /// <returns>true 表示服务器进程处于运行状态</returns>
    /// <remarks>
    /// 采用双重校验策略：
    /// 1. JAR 文件锁定检测 —— 快速判断文件是否被进程独占
    /// 2. 进程枚举验证 —— 通过命令行匹配确认对应进程存在
    /// 若 JAR 路径不可用，则降级为 PID 直接检测。
    /// </remarks>
    public bool IsServerRunning(ServerInstance server)
    {
        if (!string.IsNullOrEmpty(server.ServerJarPath))
        {
            if (IsJarFileLocked(server.ServerJarPath))
            {
                try
                {
                    var runningProcess = FindServerProcess(server);
                    if (runningProcess != null)
                    {
                        try
                        {
                            if (!runningProcess.HasExited)
                            {
                                server.ProcessId = runningProcess.Id;
                                return true;
                            }
                            Log.Warning("[WARN] 进程 PID={Pid} 已退出", runningProcess.Id);
                        }
                        finally
                        {
                            runningProcess.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[WARN] 检查 JAR 锁定状态时出错: {JarPath}", server.ServerJarPath);
                }
                
                Log.Warning("[WARN] JAR 文件被锁定，但未找到对应的服务器进程 PID={StoredPid}", server.ProcessId);
                return false;
            }
            
            try
            {
                var runningProcess = FindServerProcess(server);
                if (runningProcess != null)
                {
                    try
                    {
                        if (!runningProcess.HasExited)
                            return true;
                    }
                    finally
                    {
                        runningProcess.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WARN] 查找服务器进程时出错: {JarPath}", server.ServerJarPath);
            }
        }

        if (server.ProcessId > 0)
        {
            try
            {
                using var process = Process.GetProcessById(server.ProcessId);
                if (!process.HasExited)
                    return true;

                Log.Information("[WARN] 进程 PID={Pid} 已退出", server.ProcessId);
            }
            catch (ArgumentException)
            {
                Log.Information("[WARN] 进程 PID={Pid} 不存在", server.ProcessId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[WARN] 检查进程状态时出错 PID={Pid}", server.ProcessId);
            }
        }

        return false;
    }

    /// <summary>
    /// 通过 JAR 文件路径检测对应服务器是否正在运行
    /// </summary>
    /// <param name="jarFilePath">JAR 文件完整路径</param>
    /// <returns>true 表示对应服务器进程处于运行状态</returns>
    /// <remarks>
    /// 采用 JAR 文件锁定 + 进程匹配的双重检测机制，
    /// 优先通过文件锁快速判定，再通过进程枚举进行确认。
    /// </remarks>
    public bool IsServerRunningByJarPath(string jarFilePath)
    {
        if (!File.Exists(jarFilePath))
            return false;

        if (IsJarFileLocked(jarFilePath))
        {
            var processId = GetServerProcessId(jarFilePath);
            if (processId != null)
                return true;
            
            Log.Warning("[WARN] JAR 文件被锁定，但未找到对应的服务器进程: {JarPath}", jarFilePath);
            return false;
        }

        if (GetServerProcessId(jarFilePath) != null)
            return true;

        return false;
    }

    /// <summary>
    /// 构建服务器启动参数字符串
    /// </summary>
    /// <param name="server">服务器实例（包含 JVM 参数与 JAR 路径）</param>
    /// <returns>拼接完成的启动参数字符串</returns>
    /// <remarks>
    /// 参数顺序：JVM 参数在前，-jar 选项居中，JAR 路径在最后。
    /// JAR 路径包含空格时自动添加引号。
    /// </remarks>
    private string BuildStartupArguments(ServerInstance server)
    {
        var args = new List<string>();

        args.AddRange(server.JvmArguments);

        args.Add("-jar");
        
        var jarPath = server.ServerJarPath;
        if (jarPath.Contains(" "))
            jarPath = $"\"{jarPath}\"";
        args.Add(jarPath);

        // 添加 nogui 参数：Minecraft 服务器的标准参数，表示不启动图形界面
        // 同时作为服务器进程的标识特征，供 ProcessScanner 识别
        args.Add("nogui");

        return string.Join(" ", args);
    }

    /// <summary>
    /// 将字节数格式化为人类可读的内存大小字符串
    /// </summary>
    /// <param name="bytes">字节数</param>
    /// <returns>格式化后的字符串（G/M/K 单位）</returns>
    private string FormatMemorySize(long bytes)
    {
        if (bytes >= 1L << 30)
            return $"{bytes >> 30}G";
        if (bytes >= 1L << 20)
            return $"{bytes >> 20}M";
        return $"{bytes >> 10}K";
    }

    /// <summary>
    /// 终止指定进程及其整个子进程树
    /// </summary>
    /// <param name="parentProcessId">父进程 ID</param>
    /// <returns>true 表示终止操作成功（或进程本就不存在）</returns>
    /// <remarks>
    /// 终止策略：
    /// 1. 递归获取所有子进程 ID（深度限制 5 层，防止无限递归）
    /// 2. 先终止所有子进程，再终止父进程
    /// 3. 进程已退出或不存在均视为成功（目标状态已达成）
    /// 使用 WMI 查询子进程关系，确保完整终止进程树。
    /// </remarks>
    private bool StopProcessTree(int parentProcessId)
    {
        try
        {
            // 先递归终止子进程，防止 java.exe 的子进程继续运行
            var childProcessIds = GetChildProcessIds(parentProcessId);

            foreach (var childId in childProcessIds)
            {
                try
                {
                    using var childProcess = Process.GetProcessById(childId);
                    if (!childProcess.HasExited)
                    {
                        childProcess.Kill();
                        childProcess.WaitForExit(3000);
                        Log.Information("[KILL] 已终止子进程: PID={Pid}", childId);
                    }
                }
                catch (ArgumentException)
                {
                    // 子进程已退出，GetProcessById 会抛 ArgumentException，属于正常竞态
                    Log.Debug("子进程已退出: PID={Pid}", childId);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "终止子进程跳过 PID={Pid}: {Msg}", childId, ex.Message);
                }
            }

            // 终止父进程
            try
            {
                using var parentProcess = Process.GetProcessById(parentProcessId);
                if (parentProcess.HasExited)
                {
                    // 进程已退出 —— 目标状态已达成，视为成功
                    Log.Information("[INFO] 进程已退出（无需终止）: PID={Pid}", parentProcessId);
                    return true;
                }

                parentProcess.Kill();
                parentProcess.WaitForExit(5000);
                Log.Information("[KILL] 已终止父进程: PID={Pid}", parentProcessId);
                return true;
            }
            catch (ArgumentException)
            {
                // GetProcessById 找不到进程会抛 ArgumentException
                // 说明进程已经不在 —— 目标状态已达成，视为成功
                Log.Information("[INFO] 进程已不存在（视为已停止）: PID={Pid}", parentProcessId);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 终止进程树失败 PID={Pid}", parentProcessId);
        }

        return false;
    }

    /// <summary>
    /// 递归获取指定进程的所有子进程 ID
    /// </summary>
    /// <param name="parentProcessId">父进程 ID</param>
    /// <param name="depth">当前递归深度（用于防止无限递归）</param>
    /// <returns>所有子进程 ID 列表（含多层嵌套）</returns>
    /// <remarks>
    /// 通过 WMI Win32_Process 查询父子进程关系。
    /// 递归深度限制为 5 层，防止异常进程链导致的栈溢出。
    /// </remarks>
    private List<int> GetChildProcessIds(int parentProcessId, int depth = 0)
    {
        var childIds = new List<int>();
        if (depth > 5)
        {
            Log.Debug("子进程链深度超过 5 层，停止追溯 PID={Pid}", parentProcessId);
            return childIds;
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentProcessId}");

            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                using (obj)
                {
                    if (int.TryParse(obj["ProcessId"]?.ToString(), out var pid))
                    {
                        childIds.Add(pid);
                        childIds.AddRange(GetChildProcessIds(pid, depth + 1));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 获取子进程 ID 失败 PID={Pid}", parentProcessId);
        }

        return childIds;
    }

    /// <summary>
    /// 获取指定进程的完整命令行
    /// </summary>
    /// <param name="processId">进程 ID</param>
    /// <returns>进程命令行字符串；获取失败返回空字符串</returns>
    /// <remarks>
    /// 通过 WMI Win32_Process.CommandLine 属性获取进程命令行。
    /// 这是获取 Java 进程启动参数的可靠方式。
    /// </remarks>
    private string GetProcessCommandLine(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");

            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                using (obj)
                {
                    var cmdLine = obj["CommandLine"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(cmdLine))
                        return cmdLine;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FATAL] 获取命令行失败 PID={Pid}", processId);
        }

        return string.Empty;
    }

    /// <summary>
    /// 检测是否有任何 Minecraft 服务器正在运行
    /// </summary>
    /// <returns>true 表示至少有一台服务器在运行</returns>
    /// <remarks>
    /// 通过枚举所有 java.exe 进程，检查命令行中是否包含 "server" 关键字。
    /// 这是一种快速检测手段，用于判断系统中是否存在活跃的 Minecraft 服务端。
    /// 采用防御式编程，枚举失败时返回 false。
    /// </remarks>
    public bool AnyServerRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("java");
            foreach (var process in processes)
            {
                try
                {
                    var cmdLine = GetProcessCommandLine(process.Id);
                    if (!string.IsNullOrEmpty(cmdLine) &&
                        cmdLine.Contains("server", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { /* 跳过无权限的进程 */ }
                finally { process.Dispose(); }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc cref="IServerManagerService.SupervisedServers"/>
    public IReadOnlyDictionary<string, SupervisedProcessHandle> SupervisedServers
        => _supervisedHandles;

    /// <inheritdoc cref="IServerManagerService.TryGetSupervisedHandle"/>
    public SupervisedProcessHandle? TryGetSupervisedHandle(string jarFilePath)
        => string.IsNullOrWhiteSpace(jarFilePath)
            ? null
            : _supervisedHandles.TryGetValue(NormalizeJarKey(jarFilePath), out var h) ? h : null;

    /// <summary>JAR 路径规范化：全小写 + TrimEnd(Path.DirectorySeparatorChar)。</summary>
    private static string NormalizeJarKey(string jarPath)
        => (jarPath ?? string.Empty).Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToLowerInvariant();

    /// <summary>
    /// 合并「全局策略」与「服务器级覆盖策略」，得到最终 ProcessSupervisorOptions。
    /// 每一个字段优先使用服务器级非 null 值，否则走全局。
    /// </summary>
    private ProcessSupervisorOptions MergePolicies(ServerInstance server)
    {
        var globalPolicy = _config?.Config.Supervisor ?? new ProcessSupervisorPolicy();
        PerServerSupervisorPolicy? perServer = null;
        if (!string.IsNullOrWhiteSpace(server.ServerJarPath))
            perServer = _config?.FindByJarPath(server.ServerJarPath)?.Supervisor;

        var enableCrashRestart = perServer?.EnableCrashRestart ?? globalPolicy.EnableCrashRestart;
        var perHour = perServer?.MaxRestartAttemptsPerHour ?? globalPolicy.MaxRestartAttemptsPerHour;
        var coolDownSec = perServer?.RestartCooldownSeconds ?? globalPolicy.RestartCooldownSeconds;
        var preventSleep = perServer?.PreventSystemSleepWhenRunning ?? globalPolicy.PreventSystemSleepWhenRunning;
        var priority = perServer?.ProcessPriority ?? globalPolicy.ProcessPriority;
        var maxMem = perServer?.MaxProcessMemoryBytes ?? globalPolicy.MaxProcessMemoryBytes;
        var maxTotal = perServer?.MaxTotalRestartAttempts ?? globalPolicy.MaxTotalRestartAttempts;

        // 转译 AppConfig 策略 → ProcessSupervisorOptions（后者更底层，只看 MaxAutoRestartCount/RestartCooldownMs）
        // 「无限重启」→ int.MaxValue；「永不重启」→ 0；
        // 「每小时最多 N 次」与「总共最多 N 次」，我们取两者中更小的那个作为 MaxAutoRestartCount，
        // 因为 ProcessSupervisorOptions 目前只有单一的「重启次数上限」维度（跨所有时间窗口）。
        int maxRestartCount;
        if (maxTotal == 0)
            maxRestartCount = 0;                     // 永不重启
        else if (!enableCrashRestart)
            maxRestartCount = 0;                     // 开关禁用
        else if (maxTotal < 0)
            maxRestartCount = perHour > 0 ? perHour : int.MaxValue; // 无限总次数 → 退化为每小时窗口
        else
            maxRestartCount = perHour > 0
                ? Math.Min(maxTotal, perHour)
                : maxTotal;

        // 冷却时间：AppConfig 的 RestartCooldownSeconds × 1000 → ProcessSupervisorOptions 的 RestartCooldownMs
        var cooldownMs = Math.Clamp(coolDownSec, 0, 3600) * 1000;

        return new ProcessSupervisorOptions
        {
            MaxAutoRestartCount = maxRestartCount,
            RestartCooldownMs = cooldownMs,
            AllowBreakaway = true,
            Priority = priority,
            MaxProcessMemoryBytes = Math.Max(0, maxMem),
            PreventSystemSleep = preventSleep,
            BindToJobObject = true,
            // CPU 亲和性：初期不开放全局配置（需要 CPU 拓扑可视化），保持 null = 全核
            PreferredCores = null,
        };
    }

    /// <summary>
    /// 启动服务器（旧版同步兼容实现）。
    /// - Supervisor 可用 → 走监管模式（StartServerSupervisedAsync 同步等待）
    /// - 不可用       → 走裸 Process.Start 旧流程
    /// </summary>
    public Process? StartServer(ServerInstance server)
    {
        // Case A: 监管模式可用
        if (_supervisor != null)
        {
            try
            {
                var handle = StartServerSupervisedAsync(server, CancellationToken.None)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (handle == null) return null;
                try { return handle.Process; }
                catch (ObjectDisposedException) { return null; }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SUP] 监管模式启动失败，降级为裸 Process.Start");
                // Fall through → 旧版实现
            }
        }

        // Case B: 旧版裸 Process.Start 流程（保持与原实现完全一致）
        return StartServerLegacy(server);
    }

    /// <summary>
    /// 原 StartServer 的裸 Process.Start 实现，完整保留用于兼容降级。
    /// </summary>
    private Process? StartServerLegacy(ServerInstance server)
    {
        Log.Information("[BOOT] 尝试启动服务器: {JarName}", server.ServerJarName);

        if (IsServerRunning(server))
        {
            Log.Warning("[WARN] 服务器已经在运行中，跳过启动");
            return null;
        }

        if (!File.Exists(server.ServerJarPath))
        {
            Log.Error("[ERR] JAR 文件不存在: {JarPath}", server.ServerJarPath);
            return null;
        }

        if (!Directory.Exists(server.WorkingDirectory))
        {
            Log.Error("[ERR] 工作目录不存在: {Dir}", server.WorkingDirectory);
            return null;
        }

        try
        {
            string? javaExe = null;
            JavaInstallationInfo? javaInfo = null;

            if (!string.IsNullOrEmpty(server.JavaPath))
            {
                javaInfo = _javaFinderService.Verify(server.JavaPath);
                if (javaInfo != null)
                    javaExe = javaInfo.JavaPath;
            }

            if (javaExe == null)
            {
                javaInfo = _javaFinderService.FindDefault();
                if (javaInfo != null)
                    javaExe = javaInfo.JavaPath;
            }

            if (string.IsNullOrEmpty(javaExe))
            {
                Log.Error("[ERR] 找不到 Java 可执行文件，请确保已安装 Java 并配置环境变量");
                return null;
            }

            if (!File.Exists(javaExe))
            {
                Log.Error("[ERR] Java 可执行文件不存在: {JavaPath}", javaExe);
                return null;
            }

            if (javaInfo != null)
            {
                Log.Information("[JAVA] 使用 Java: {Version} ({Vendor})", javaInfo.VersionString, javaInfo.Vendor);

                if (javaInfo.Version != null)
                {
                    var major = javaInfo.Version.Major;
                    if (major < 21)
                    {
                        Log.Warning("[WARN] Java 版本较低 ({Version})，Minecraft 1.20.5+ / Paper 1.20.5+ 需要 Java 21 或更高版本", javaInfo.VersionString);
                    }
                    else if (major < 17)
                    {
                        Log.Error("[ERR] Java 版本过低 ({Version})，Minecraft 1.17+ 需要 Java 17 以上", javaInfo.VersionString);
                    }

                    if (major < 11)
                    {
                        Log.Error("[ERR] Java {Version} 太旧了，几乎所有现代 Minecraft 服务器都无法运行", javaInfo.VersionString);
                    }
                }

                if (!javaInfo.Is64Bit)
                {
                    Log.Warning("[WARN] 检测到 32 位 Java，内存将被限制在 2GB 以内，强烈建议使用 64 位 Java");
                }
            }

            var normalizationResult = JvmArgumentNormalizer.Normalize(server.JvmArguments);
            var normalizedServer = new ServerInstance
            {
                ProcessId = server.ProcessId,
                ServerType = server.ServerType,
                WorkingDirectory = server.WorkingDirectory,
                JavaPath = javaExe,
                ServerJarPath = server.ServerJarPath,
                ServerJarName = server.ServerJarName,
                FullCommandLine = server.FullCommandLine,
                JvmArguments = normalizationResult.Arguments,
                InitialHeapMemoryBytes = server.InitialHeapMemoryBytes,
                MaxHeapMemoryBytes = server.MaxHeapMemoryBytes,
                ConfigFiles = server.ConfigFiles,
                UsesAikarFlags = server.UsesAikarFlags,
                GcType = server.GcType,
                ServerPort = server.ServerPort
            };

            foreach (var warning in normalizationResult.Warnings)
                Log.Warning("[WARN] 参数警告: {Warning}", warning);

            var arguments = BuildStartupArguments(normalizedServer);
            var fullCommand = $"{javaExe} {arguments}";
            Log.Information("[LOG] 启动命令: {Cmd}", fullCommand);
            Log.Information("[FS] 工作目录: {Dir}", server.WorkingDirectory);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = arguments,
                WorkingDirectory = server.WorkingDirectory,
                UseShellExecute = true,
            };

            var process = Process.Start(processStartInfo);
            if (process != null)
            {
                Log.Information("[OK] 服务器进程已启动! PID={Pid}", process.Id);
                server.ProcessId = process.Id;

                _ = Task.Run(() =>
                {
                    try
                    {
                        if (process.WaitForExit(2000))
                        {
                            var exitCode = process.ExitCode;
                            Log.Warning("[WARN] 服务器进程在 2 秒内异常退出! PID={Pid}, ExitCode={ExitCode}", process.Id, exitCode);
                            ReadServerCrashDetailsLegacy(server.WorkingDirectory);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "监视服务器进程启动状态时出错");
                    }
                });

                return process;
            }

            Log.Error("[ERR] 启动进程返回 null");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ERR] 启动服务器失败: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 读取服务器日志文件输出崩溃详情（降级裸启动时使用；监管模式下由 Supervisor 内部事件处理）。
    /// </summary>
    private static void ReadServerCrashDetailsLegacy(string workingDirectory)
    {
        try
        {
            var latestLogPath = System.IO.Path.Combine(workingDirectory, "logs", "latest.log");
            if (System.IO.File.Exists(latestLogPath))
            {
                var lines = System.IO.File.ReadAllLines(latestLogPath);
                var tail = lines.Length > 30 ? lines[^30..] : lines;
                Log.Error("[LOG] 服务器日志最后 {Count} 行（{Path}）：", tail.Length, latestLogPath);
                foreach (var line in tail)
                    Log.Error("   {Line}", line);
            }

            var crashDir = System.IO.Path.Combine(workingDirectory, "crash-reports");
            if (System.IO.Directory.Exists(crashDir))
            {
                var crashFile = System.IO.Directory.GetFiles(crashDir, "*.txt")
                    .OrderByDescending(System.IO.File.GetLastWriteTime)
                    .FirstOrDefault();
                if (crashFile != null)
                {
                    var crashTime = System.IO.File.GetLastWriteTime(crashFile);
                    if (crashTime > DateTime.Now.AddMinutes(-1))
                    {
                        var crashContent = System.IO.File.ReadAllText(crashFile);
                        var preview = crashContent.Length > 2000 ? crashContent[..2000] + "..." : crashContent;
                        Log.Error("[FATAL] 检测到最新的崩溃报告（{Path}）：\n{Content}", crashFile, preview);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "读取服务器崩溃日志失败");
        }
    }

    /// <summary>
    /// 核心异步 API：以监管模式启动 Minecraft 服务器。
    /// </summary>
    /// <remarks>
    /// 流程：
    /// 1. 复用 Legacy 的 Java 检测 + 参数规范化逻辑（不重复造轮子）
    /// 2. 合并「全局 AppConfig.Supervisor」+「KnownServer.Supervisor」得到最终策略
    /// 3. 调用 IProcessSupervisorService.LaunchSupervisedAsync（Job Object + 崩溃重启 + 优先级 + 内存上限）
    /// 4. 进入 _supervisedHandles 字典，订阅 StatusChanged 实现防睡眠引用计数 & 任务栏闪烁
    /// 5. StopServer 时先 Dispose 监管句柄（会取消重启监控 + Kill Job Object），再走旧版 StopProcessTree 作为兜底
    /// </remarks>
    public async Task<SupervisedProcessHandle?> StartServerSupervisedAsync(ServerInstance server, CancellationToken ct = default)
    {
        if (_supervisor == null)
        {
            Log.Warning("[SUP] ProcessSupervisorService 不可用（非 Windows），返回 null；可调用 StartServer 走裸启动");
            return null;
        }

        Log.Information("[SUP] 尝试以监管模式启动服务器: {JarName}", server.ServerJarName);

        // ---------- 复用 Legacy 版的前置校验 & Java 检测 & 参数规范化 ----------
        if (IsServerRunning(server))
        {
            Log.Warning("[WARN] 服务器已经在运行中，跳过启动");
            return TryGetSupervisedHandle(server.ServerJarPath);
        }

        if (!File.Exists(server.ServerJarPath))
        {
            Log.Error("[ERR] JAR 文件不存在: {JarPath}", server.ServerJarPath);
            return null;
        }

        if (!Directory.Exists(server.WorkingDirectory))
        {
            Log.Error("[ERR] 工作目录不存在: {Dir}", server.WorkingDirectory);
            return null;
        }

        string? javaExe = null;
        JavaInstallationInfo? javaInfo = null;
        if (!string.IsNullOrEmpty(server.JavaPath))
        {
            javaInfo = _javaFinderService.Verify(server.JavaPath);
            if (javaInfo != null) javaExe = javaInfo.JavaPath;
        }
        if (javaExe == null)
        {
            javaInfo = _javaFinderService.FindDefault();
            if (javaInfo != null) javaExe = javaInfo.JavaPath;
        }
        if (string.IsNullOrEmpty(javaExe) || !File.Exists(javaExe))
        {
            Log.Error("[ERR] Java 可执行文件不可用: {JavaPath}", javaExe);
            return null;
        }

        if (javaInfo != null)
        {
            Log.Information("[JAVA] 使用 Java: {Version} ({Vendor})", javaInfo.VersionString, javaInfo.Vendor);
            if (javaInfo.Version != null)
            {
                var major = javaInfo.Version.Major;
                if (major < 17)
                    Log.Warning("[WARN] Java 版本偏低（{Version}），现代 Minecraft 服务器需要 Java 17+，推荐 Java 21", javaInfo.VersionString);
            }
            if (!javaInfo.Is64Bit)
                Log.Warning("[WARN] 32 位 Java 已不推荐，请更换 64 位 Java 以获得完整内存支持");
        }

        var normalizationResult = JvmArgumentNormalizer.Normalize(server.JvmArguments);
        var normalizedServer = new ServerInstance
        {
            ProcessId = server.ProcessId,
            ServerType = server.ServerType,
            WorkingDirectory = server.WorkingDirectory,
            JavaPath = javaExe,
            ServerJarPath = server.ServerJarPath,
            ServerJarName = server.ServerJarName,
            FullCommandLine = server.FullCommandLine,
            JvmArguments = normalizationResult.Arguments,
            InitialHeapMemoryBytes = server.InitialHeapMemoryBytes,
            MaxHeapMemoryBytes = server.MaxHeapMemoryBytes,
            ConfigFiles = server.ConfigFiles,
            UsesAikarFlags = server.UsesAikarFlags,
            GcType = server.GcType,
            ServerPort = server.ServerPort
        };
        foreach (var w in normalizationResult.Warnings) Log.Warning("[WARN] 参数警告: {W}", w);

        var arguments = BuildStartupArguments(normalizedServer);
        Log.Information("[SUP] 启动命令: java.exe {Args}", arguments);

        // ---------- 合并监管策略 ----------
        var options = MergePolicies(server);
        Log.Information("[SUP] 监管策略：MaxRestart={Max}, Cooldown={Cool}ms, Priority={Prio}, MemCap={MemCap}B, PreventSleep={Sleep}, Job={Job}",
            options.MaxAutoRestartCount == int.MaxValue ? "∞" : options.MaxAutoRestartCount.ToString(),
            options.RestartCooldownMs,
            options.Priority,
            options.MaxProcessMemoryBytes == 0 ? "∞" : options.MaxProcessMemoryBytes.ToString(),
            options.PreventSystemSleep,
            options.BindToJobObject);

        // ---------- 启动（走 Supervisor） ----------
        var handle = await _supervisor.LaunchSupervisedAsync(
            executablePath: javaExe,
            arguments: arguments,
            workingDirectory: server.WorkingDirectory,
            options: options,
            ct: ct).ConfigureAwait(false);

        if (handle == null)
        {
            Log.Error("[SUP] LaunchSupervisedAsync 返回 null，启动失败（可查 Serilog 日志定位具体失败步骤）");
            return null;
        }

        server.ProcessId = handle.ProcessId;
        Log.Information("[SUP] 服务器监管已就绪！PID={Pid}（Job Object 已绑定，关闭 MSMC 时子进程会被一起清理）", handle.ProcessId);

        // 订阅崩溃/退出事件
        handle.ProcessCrashedAndWillRestart += (_, exitCode) =>
        {
            Log.Warning("[SUP] 服务器异常崩溃 exitCode={Code}（第 {Count} 次，Max={Max}，将在 {Cool}ms 后自动重启）",
                exitCode, handle.CrashCount, options.MaxAutoRestartCount, options.RestartCooldownMs);
            try { _supervisor?.FlashMainWindowTaskbar(IntPtr.Zero, count: 5, intervalMs: 250); } catch { /* ignore */ }
            ReadServerCrashDetailsLegacy(server.WorkingDirectory);
        };
        handle.ProcessExited += (_, exitCode) =>
        {
            if (exitCode == 0)
                Log.Information("[SUP] 服务器优雅退出 exitCode=0（不会自动重启）");
            else
                Log.Warning("[SUP] 服务器退出 exitCode={Code}，累计崩溃 {Count} 次，到达 MaxAutoRestartCount 后不再重启",
                    exitCode, handle.CrashCount);
        };

        // 进入字典（先移除过期的同 Key 句柄，防止同 JAR 路径重复监管）
        var key = NormalizeJarKey(server.ServerJarPath);
        if (_supervisedHandles.TryRemove(key, out var old))
        {
            try { old.Dispose(); } catch { /* ignore */ }
        }
        _supervisedHandles[key] = handle;

        // 「防睡眠」注意：ProcessSupervisorService 内部已经实现了引用计数 +
        // LaunchSupervisedAsync 时已经按 options.PreventSystemSleep=true 自动调用 PreventSystemSleep(true)，
        // Dispose 时自动反向释放。这里不再额外套一层引用计数，避免双重加锁/释放。
        // 仅在 ServerManagerService 层暴露「当前是否有监管服需要防睡眠」的查询接口方便 UI 展示。

        // 订阅句柄 Disposed → 退出字典（SupervisedProcessHandle 没有公开 event，我们用一个后台 Task 轮询 HasExited 配合字典清理）
        _ = MonitorHandleLifetimeAsync(handle, key, server.ServerJarName);

        return handle;
    }

    /// <summary>
    /// 后台跟踪监管句柄的生命周期：当底层 Process 退出（HasExited）时，
    /// 若它仍在 _supervisedHandles 字典中就移除（保证 UI 层 SupervisedServers 快照的准确性）。
    /// </summary>
    private async Task MonitorHandleLifetimeAsync(SupervisedProcessHandle handle, string key, string displayName)
    {
        try
        {
            // 轮询间隔 1s 足够（Process.HasExited 是内核事件，轮询成本几乎为 0）
            while (!handle.HasExited)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                if (handle.HasExited) break;
            }
        }
        catch (ObjectDisposedException) { /* 正常 Dispose 路径 */ }
        catch (Exception ex)
        {
            Log.Debug(ex, "[SUP] 监管句柄生命周期跟踪异常: {Name}", displayName);
        }
        finally
        {
            if (_supervisedHandles.TryRemove(key, out _))
                Log.Information("[SUP] 服务器监管句柄已从活动字典移除: {Name}", displayName);
        }
    }

    /// <summary>停止服务器（监管模式增强版：先 Terminate 监管句柄取消重启 + Kill Job，再走旧版 StopProcessTree 兜底）。</summary>
    public bool StopServer(ServerInstance server)
    {
        Log.Information("[STOP] 尝试停止服务器: {JarName}", server.ServerJarName);

        // Step 1: 若是监管启动 → 先取消重启 + Kill Job Object（= 整个子进程树一起死，比 WMI 更干净）
        if (!string.IsNullOrWhiteSpace(server.ServerJarPath))
        {
            var handle = TryGetSupervisedHandle(server.ServerJarPath);
            if (handle != null)
            {
                try
                {
                    Log.Information("[STOP] 检测到服务器受监管（PID={Pid}），通过 SupervisedProcessHandle.Terminate 终止（含 Job Object）", handle.ProcessId);
                    handle.Terminate(0);
                    handle.Dispose();
                    Log.Information("[STOP] SupervisedProcessHandle 已 Dispose");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[STOP] SupervisedProcessHandle 终止报错，继续走旧版 StopProcessTree 兜底");
                }
            }
        }

        // Step 2: 旧版停止流程作为兜底（Find → Kill 进程树）
        var process = FindServerProcess(server);
        if (process != null)
        {
            bool ok = StopProcessTree(process.Id);
            Log.Information("[STOP] 进程树终止结果: {OK}", ok ? "成功" : "失败");
            return ok;
        }
        if (server.ProcessId > 0)
        {
            return StopServerByProcessId(server.ProcessId);
        }

        Log.Information("[INFO] 未找到运行中的服务器进程，视为已停止: {JarName}", server.ServerJarName);
        return true;
    }

    /// <summary>
    /// 获取指定进程的内存使用量
    /// </summary>
    /// <param name="processId">进程 ID</param>
    /// <returns>工作集内存字节数；进程不存在或读取失败返回 null</returns>
    public long? GetProcessMemoryUsage(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited) return null;
            return process.WorkingSet64;
        }
        catch (ArgumentException)
        {
            Log.Debug("[WARN] 获取内存失败：进程不存在 PID={Pid}", processId);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WARN] 获取进程内存失败 PID={Pid}", processId);
            return null;
        }
    }

    /// <summary>
    /// 获取指定进程的 CPU 使用率
    /// </summary>
    /// <param name="processId">进程 ID</param>
    /// <returns>CPU 使用率百分比近似值；进程不存在或读取失败返回 null</returns>
    /// <remarks>
    /// 注意：准确的 CPU 使用率需要两次采样计算，
    /// 此处基于工作集内存占总内存的比例返回近似参考值。
    /// </remarks>
    public double? GetProcessCpuUsage(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited) return null;

            // 使用 TotalProcessorTime 计算需要两次采样
            // 此处简单返回 WorkingSet64 占总内存的比例作为参考
            var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (totalMemory > 0)
            {
                return Math.Round((double)process.WorkingSet64 / totalMemory * 100, 2);
            }

            return null;
        }
        catch (ArgumentException)
        {
            Log.Debug("[WARN] 获取 CPU 失败：进程不存在 PID={Pid}", processId);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WARN] 获取进程 CPU 失败 PID={Pid}", processId);
            return null;
        }
    }
}
