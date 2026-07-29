using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
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
    /// 启动指定的 Minecraft 服务器实例
    /// </summary>
    /// <param name="server">服务器实例，包含启动所需的全部配置</param>
    /// <returns>启动后的进程对象；启动失败返回 null</returns>
    public Process? StartServer(ServerInstance server);

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

    public ServerManagerService(IJavaFinderService javaFinderService)
    {
        _javaFinderService = javaFinderService;
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
                            Log.Warning("⚠️ 进程 PID={Pid} 已退出", runningProcess.Id);
                        }
                        finally
                        {
                            runningProcess.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "⚠️ 检查 JAR 锁定状态时出错: {JarPath}", server.ServerJarPath);
                }
                
                Log.Warning("⚠️ JAR 文件被锁定，但未找到对应的服务器进程 PID={StoredPid}", server.ProcessId);
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
                Log.Error(ex, "⚠️ 查找服务器进程时出错: {JarPath}", server.ServerJarPath);
            }
        }

        if (server.ProcessId > 0)
        {
            try
            {
                using var process = Process.GetProcessById(server.ProcessId);
                if (!process.HasExited)
                    return true;

                Log.Information("⚠️ 进程 PID={Pid} 已退出", server.ProcessId);
            }
            catch (ArgumentException)
            {
                Log.Information("⚠️ 进程 PID={Pid} 不存在", server.ProcessId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "⚠️ 检查进程状态时出错 PID={Pid}", server.ProcessId);
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
            
            Log.Warning("⚠️ JAR 文件被锁定，但未找到对应的服务器进程: {JarPath}", jarFilePath);
            return false;
        }

        if (GetServerProcessId(jarFilePath) != null)
            return true;

        return false;
    }

    /// <summary>
    /// 启动指定的 Minecraft 服务器实例
    /// </summary>
    /// <param name="server">服务器实例，包含启动所需的全部配置</param>
    /// <returns>启动后的进程对象；启动失败返回 null</returns>
    /// <remarks>
    /// 启动流程：
    /// 1. 前置校验 —— 服务器未运行、JAR 文件存在、工作目录存在
    /// 2. Java 环境检测 —— 查找并验证 Java 可执行文件
    /// 3. JVM 参数规范化 —— 校验并标准化启动参数
    /// 4. 进程启动 —— 以指定工作目录启动 Java 进程
    /// 所有异常均被捕获并记录，确保方法不会向上抛出异常。
    /// </remarks>
    public Process? StartServer(ServerInstance server)
    {
        Log.Information("🚀 尝试启动服务器: {JarName}", server.ServerJarName);

        if (IsServerRunning(server))
        {
            Log.Warning("⚠️ 服务器已经在运行中，跳过启动");
            return null;
        }

        if (!File.Exists(server.ServerJarPath))
        {
            Log.Error("❌ JAR 文件不存在: {JarPath}", server.ServerJarPath);
            return null;
        }

        if (!Directory.Exists(server.WorkingDirectory))
        {
            Log.Error("❌ 工作目录不存在: {Dir}", server.WorkingDirectory);
            return null;
        }

        try
        {
            string? javaExe = null;
            JavaInstallation? javaInfo = null;

            // 始终使用 java.exe（而非 javaw.exe），确保服务器控制台窗口可见，
            // 玩家可查看日志输出并输入控制台命令（stop、op、say 等）。
            // javaw.exe 属于 GUI 子系统，会丢弃 stdout/stderr，导致服务器日志完全丢失。
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
                Log.Error("❌ 找不到 Java 可执行文件，请确保已安装 Java 并配置环境变量");
                return null;
            }

            if (!File.Exists(javaExe))
            {
                Log.Error("❌ Java 可执行文件不存在: {JavaPath}", javaExe);
                return null;
            }

            if (javaInfo != null)
            {
                Log.Information("☕ 使用 Java: {Version} ({Vendor})", javaInfo.VersionString, javaInfo.Vendor);

                if (javaInfo.Version != null)
                {
                    var major = javaInfo.Version.Major;
                    if (major < 21)
                    {
                        Log.Warning("⚠️ Java 版本较低 ({Version})，Minecraft 1.20.5+ / Paper 1.20.5+ 需要 Java 21 或更高版本", javaInfo.VersionString);
                        Log.Warning("   如果服务器闪退，请先升级到 Java 21");
                    }
                    else if (major < 17)
                    {
                        Log.Error("❌ Java 版本过低 ({Version})，Minecraft 1.17+ 需要 Java 17 以上", javaInfo.VersionString);
                    }

                    if (major < 11)
                    {
                        Log.Error("❌ Java {Version} 太旧了，几乎所有现代 Minecraft 服务器都无法运行", javaInfo.VersionString);
                    }
                }

                if (!javaInfo.Is64Bit)
                {
                    Log.Warning("⚠️ 检测到 32 位 Java，内存将被限制在 2GB 以内，强烈建议使用 64 位 Java");
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
            {
                Log.Warning("⚠️ 参数警告: {Warning}", warning);
            }

            var arguments = BuildStartupArguments(normalizedServer);
            
            var fullCommand = $"{javaExe} {arguments}";
            
            Log.Information("📝 启动命令: {Cmd}", fullCommand);
            Log.Information("📁 工作目录: {Dir}", server.WorkingDirectory);
            
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
                Log.Information("✅ 服务器进程已启动! PID={Pid}", process.Id);
                server.ProcessId = process.Id;

                _ = Task.Run(() =>
                {
                    try
                    {
                        // 同步等待最多 2 秒，捕获 JVM 启动即崩溃的情况
                        // （原 5 秒延迟过长，且异步 Delay 期间进程可能已被系统回收导致 ExitCode 丢失）
                        if (process.WaitForExit(2000))
                        {
                            var exitCode = process.ExitCode;
                            Log.Warning("⚠️ 服务器进程在 2 秒内异常退出! PID={Pid}, ExitCode={ExitCode}", process.Id, exitCode);

                            if (exitCode == 1)
                            {
                                Log.Error("💥 退出码 1：通常是 JVM 启动失败，常见原因：");
                                Log.Error("   1. Java 版本不兼容（Minecraft 1.20.5+ 需要 Java 21）");
                                Log.Error("   2. JVM 参数有拼写错误或不支持的参数");
                                Log.Error("   3. 内存分配超出系统可用物理内存");
                                Log.Error("   4. JAR 文件损坏或路径不正确");
                            }
                            else if (exitCode == -1 || exitCode == unchecked((int)0xC0000005))
                            {
                                Log.Error("💥 进程崩溃（退出码 {ExitCode}）：可能是 Java 本身故障、系统内存不足或杀毒软件拦截", exitCode);
                            }

                            // 读取服务器日志文件，输出崩溃详情（UseShellExecute=true 时无法重定向 stderr，
                            // 只能从事后写入的日志文件中提取错误信息）
                            LogServerCrashDetails(server.WorkingDirectory);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "监视服务器进程启动状态时出错");
                    }
                });

                return process;
            }
            
            Log.Error("❌ 启动进程返回 null");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 启动服务器失败: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 读取服务器日志文件，输出崩溃详情
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录</param>
    /// <remarks>
    /// 由于 StartServer 使用 UseShellExecute=true（为显示服务器控制台窗口），
    /// 无法重定向 stderr，只能在进程崩溃后从日志文件中提取错误信息。
    /// 检查路径：logs/latest.log, crash-reports/*.txt
    /// </remarks>
    private static void LogServerCrashDetails(string workingDirectory)
    {
        try
        {
            // 1. 读取 logs/latest.log 的最后 30 行
            var latestLogPath = System.IO.Path.Combine(workingDirectory, "logs", "latest.log");
            if (System.IO.File.Exists(latestLogPath))
            {
                var lines = System.IO.File.ReadAllLines(latestLogPath);
                var tail = lines.Length > 30 ? lines[^30..] : lines;
                Log.Error("📋 服务器日志最后 {Count} 行（{Path}）：", tail.Length, latestLogPath);
                foreach (var line in tail)
                {
                    Log.Error("   {Line}", line);
                }
            }

            // 2. 检查 crash-reports 目录中最新的崩溃报告
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
                        Log.Error("💥 检测到最新的崩溃报告（{Path}）：\n{Content}", crashFile, preview);
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
    /// 停止指定的 Minecraft 服务器实例
    /// </summary>
    /// <param name="server">服务器实例</param>
    /// <returns>true 表示停止操作执行成功（或进程本就未运行）</returns>
    /// <remarks>
    /// 停止策略：
    /// 1. 优先通过 JAR 文件名匹配查找当前运行进程并终止
    /// 2. 匹配失败时，使用记录的 PID 直接终止
    /// 3. 若两者均无效，视为目标状态已达成（进程已停止），返回成功
    /// </remarks>
    public bool StopServer(ServerInstance server)
    {
        Log.Information("🛑 尝试停止服务器: {JarName}", server.ServerJarName);

        // 优先通过 JAR 名匹配当前运行中的进程
        var process = FindServerProcess(server);
        if (process != null)
        {
            return StopProcessTree(process.Id);
        }

        // JAR 名匹配失败，降级为 PID 直接终止
        if (server.ProcessId > 0)
        {
            return StopServerByProcessId(server.ProcessId);
        }

        // 未找到运行中的进程 —— 目标状态（服务器停止）已达成，视为成功
        Log.Information("ℹ️ 未找到运行中的服务器进程，视为已停止: {JarName}", server.ServerJarName);
        return true;
    }

    /// <summary>
    /// 通过进程 ID 停止服务器进程及其子进程树
    /// </summary>
    /// <param name="processId">父进程 ID</param>
    /// <returns>true 表示停止操作执行成功</returns>
    /// <remarks>
    /// 终止整个进程树，防止 java.exe 的子进程继续运行。
    /// 采用防御式编程，进程不存在或已退出均视为成功。
    /// </remarks>
    public bool StopServerByProcessId(int processId)
    {
        Log.Information("🛑 尝试终止进程: PID={Pid}", processId);

        try
        {
            return StopProcessTree(processId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 终止进程失败 PID={Pid}", processId);
            return false;
        }
    }

    /// <summary>
    /// 查找与指定服务器实例匹配的运行中进程
    /// </summary>
    /// <param name="server">服务器实例</param>
    /// <returns>匹配的进程对象；未找到返回 null</returns>
    /// <remarks>
    /// 通过枚举所有 java.exe 进程，匹配命令行中包含目标 JAR 文件名的进程。
    /// 返回新的 Process 对象实例，调用方负责释放。
    /// 处理进程枚举过程中的竞态条件——进程可能随时退出。
    /// </remarks>
    public Process? FindServerProcess(ServerInstance server)
    {
        var jarName = Path.GetFileName(server.ServerJarPath).ToLowerInvariant();
        
        try
        {
            foreach (var process in Process.GetProcessesByName("java"))
            {
                using (process)
                {
                    try
                    {
                        var cmdLine = GetProcessCommandLine(process.Id);
                        if (!string.IsNullOrEmpty(cmdLine) && 
                            cmdLine.ToLowerInvariant().Contains(jarName))
                        {
                            // 返回新的 Process 对象，避免 using 块释放
                            try { return Process.GetProcessById(process.Id); }
                            catch (Exception ex) { Log.Debug(ex, "获取进程 PID={Pid} 失败（可能已退出）", process.Id); return null; }
                        }
                    }
                    catch
                    {
                        // 进程可能已退出，跳过
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 查找 Java 进程失败");
        }

        return null;
    }

    /// <summary>
    /// 获取指定 JAR 文件对应的服务器进程 ID
    /// </summary>
    /// <param name="jarFilePath">JAR 文件完整路径</param>
    /// <returns>进程 ID；未找到返回 null</returns>
    /// <remarks>
    /// 通过枚举所有 java.exe 进程，匹配命令行中包含目标 JAR 文件名的进程。
    /// 处理进程枚举过程中的竞态条件。
    /// </remarks>
    public int? GetServerProcessId(string jarFilePath)
    {
        var jarName = Path.GetFileName(jarFilePath).ToLowerInvariant();

        try
        {
            foreach (var process in Process.GetProcessesByName("java"))
            {
                using (process)
                {
                    try
                    {
                        var cmdLine = GetProcessCommandLine(process.Id);
                        if (!string.IsNullOrEmpty(cmdLine) && 
                            cmdLine.ToLowerInvariant().Contains(jarName))
                        {
                            return process.Id;
                        }
                    }
                    catch
                    {
                        // 进程可能已退出，跳过
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 获取服务器进程 ID 失败");
        }

        return null;
    }

    /// <summary>
    /// 检测 JAR 文件是否被进程独占锁定（公共静态版，供 ServerDetector 等其他组件复用）。
    /// </summary>
    /// <param name="jarFilePath">JAR 文件绝对路径</param>
    /// <returns>true 表示文件被其他进程以共享冲突方式打开（典型：Java 加载 JAR）</returns>
    /// <remarks>
    /// 原理：尝试以 FileShare.None 打开文件读取，若抛出 IOException（ERROR_SHARING_VIOLATION）
    /// 则判定为被锁定。仅作为快速存在性检测，不依赖管理员权限。
    /// 文件不存在时返回 false（调用方应预先 File.Exists 判断）。
    /// </remarks>
    public static bool IsJarFileLocked(string jarFilePath)
    {
        if (string.IsNullOrWhiteSpace(jarFilePath))
            return false;
        try
        {
            using var stream = new FileStream(jarFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (IOException)
        {
            // ERROR_SHARING_VIOLATION (0x80070020)：文件被其他进程占用
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 检查 JAR 文件锁定状态失败: {JarPath}", jarFilePath);
            return false;
        }
    }

    /// <summary>
    /// 通过 JAR 文件路径与工作目录，在所有 java/javaw 进程中查找匹配的服务器进程。
    /// 优先用命令行包含 JAR 文件名匹配，其次用 JAR 完整路径匹配，最后降级为工作目录匹配。
    /// （公共静态版，供 ServerDetector 等复用）
    /// </summary>
    /// <param name="jarFilePath">目标 JAR 绝对路径</param>
    /// <param name="workingDirectory">预期工作目录（可空）</param>
    /// <returns>匹配到的 Process 对象（调用方负责 Dispose）；未找到返回 null</returns>
    /// <remarks>
    /// 枚举进程与命令行读取失败均静默跳过，不会向上抛出异常。
    /// </remarks>
    public static Process? FindJavaProcessByJarPath(string jarFilePath, string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(jarFilePath))
            return null;

        var jarName = Path.GetFileName(jarFilePath).ToLowerInvariant();
        var jarFullLower = jarFilePath.ToLowerInvariant();
        var workDirLower = string.IsNullOrWhiteSpace(workingDirectory)
            ? null
            : workingDirectory.TrimEnd('\\', '/').ToLowerInvariant();

        // 同时枚举 java.exe 与 javaw.exe（MSMC 启动时可配置 preferJavaw）
        var processNames = new[] { "java", "javaw" };

        Process? bestMatch = null;
        int bestScore = 0;

        foreach (var procName in processNames)
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(procName); }
            catch (Exception ex)
            {
                Log.Debug(ex, "枚举 {ProcName} 进程失败", procName);
                continue;
            }

            foreach (var proc in procs)
            {
                try
                {
                    if (proc.HasExited)
                    {
                        proc.Dispose();
                        continue;
                    }

                    int score = 0;

                    // 策略 1：命令行包含 JAR 文件名（最强信号）
                    var cmdLine = GetProcessCommandLineStatic(proc.Id);
                    if (!string.IsNullOrEmpty(cmdLine))
                    {
                        var cmdLower = cmdLine.ToLowerInvariant();
                        if (cmdLower.Contains(jarName))
                            score += 100;
                        if (cmdLower.Contains(jarFullLower))
                            score += 200; // 完整路径匹配，优先级最高
                    }

                    // 策略 2：进程工作目录匹配（降级信号）
                    if (workDirLower != null && score == 0)
                    {
                        try
                        {
                            var procWorkDir = proc.StartInfo.WorkingDirectory;
                            if (!string.IsNullOrWhiteSpace(procWorkDir)
                                && procWorkDir.TrimEnd('\\', '/').ToLowerInvariant() == workDirLower)
                            {
                                score += 50;
                            }
                        }
                        catch { /* StartInfo.WorkingDirectory 可能拿不到，忽略 */ }
                    }

                    if (score > bestScore)
                    {
                        bestMatch?.Dispose();
                        bestMatch = proc;
                        bestScore = score;
                    }
                    else
                    {
                        proc.Dispose();
                    }
                }
                catch (InvalidOperationException)
                {
                    // 进程在检查期间退出
                    proc.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "检查进程 PID={Pid} 时出错", proc.Id);
                    proc.Dispose();
                }
            }
        }

        return bestScore > 0 ? bestMatch : null;
    }

    /// <summary>
    /// 获取指定进程的完整命令行（WMI Win32_Process 静态版，供 FindJavaProcessByJarPath 复用）。
    /// </summary>
    private static string? GetProcessCommandLineStatic(int processId)
    {
        try
        {
            // 复用 Windows 专用的 WMI 查询方式（与 ProcessScanner 逻辑一致）
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            using var results = searcher.Get();
            foreach (var mo in results)
            {
                var cmd = mo["CommandLine"]?.ToString();
                if (!string.IsNullOrEmpty(cmd))
                    return cmd;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "读取进程 PID={Pid} 命令行失败", processId);
        }
        return null;
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
                        Log.Information("🔫 已终止子进程: PID={Pid}", childId);
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
                    Log.Information("ℹ️ 进程已退出（无需终止）: PID={Pid}", parentProcessId);
                    return true;
                }

                parentProcess.Kill();
                parentProcess.WaitForExit(5000);
                Log.Information("🔫 已终止父进程: PID={Pid}", parentProcessId);
                return true;
            }
            catch (ArgumentException)
            {
                // GetProcessById 找不到进程会抛 ArgumentException
                // 说明进程已经不在 —— 目标状态已达成，视为成功
                Log.Information("ℹ️ 进程已不存在（视为已停止）: PID={Pid}", parentProcessId);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 终止进程树失败 PID={Pid}", parentProcessId);
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
            Log.Error(ex, "❌ 获取子进程 ID 失败 PID={Pid}", parentProcessId);
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
            Log.Error(ex, "💥 获取命令行失败 PID={Pid}", processId);
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
            Log.Debug("⚠️ 获取内存失败：进程不存在 PID={Pid}", processId);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "⚠️ 获取进程内存失败 PID={Pid}", processId);
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
            Log.Debug("⚠️ 获取 CPU 失败：进程不存在 PID={Pid}", processId);
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "⚠️ 获取进程 CPU 失败 PID={Pid}", processId);
            return null;
        }
    }
}