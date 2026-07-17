using System.Diagnostics;
using System.IO;
using System.Management;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

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
                var process = Process.GetProcessById(server.ProcessId);
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
            var javaExe = string.IsNullOrEmpty(server.JavaPath) 
                ? JavaFinder.FindJava() 
                : server.JavaPath;

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

            var javaInfo = JavaFinder.VerifyJava(javaExe);
            if (javaInfo != null)
            {
                Log.Information("☕ 使用 Java: {Version} ({Vendor})", javaInfo.VersionString, javaInfo.Vendor);

                if (javaInfo.Version != null && javaInfo.Version.Major < 21)
                {
                    Log.Warning("⚠️ Java 版本较低 ({Version})，Folia/Paper 1.20.5+ 推荐使用 Java 21 或更高版本", javaInfo.VersionString);
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
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            var process = Process.Start(processStartInfo);
            if (process != null)
            {
                Log.Information("✅ 服务器进程已启动! PID={Pid}", process.Id);
                server.ProcessId = process.Id;
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
                            catch { return null; }
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
    /// 检测 JAR 文件是否被进程独占锁定
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径</param>
    /// <returns>true 表示文件被锁定（无法以 FileShare.None 打开）</returns>
    /// <remarks>
    /// 原理：尝试以独占读取方式打开文件，若抛出 IOException 则判定为被锁定。
    /// 这是判断 Java 进程是否正在加载该 JAR 的快速检测手段。
    /// </remarks>
    private bool IsJarFileLocked(string jarFilePath)
    {
        try
        {
            using var stream = new FileStream(jarFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 检查 JAR 文件锁定状态失败: {JarPath}", jarFilePath);
            return false;
        }
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
                    var childProcess = Process.GetProcessById(childId);
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
                var parentProcess = Process.GetProcessById(parentProcessId);
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
                if (int.TryParse(obj["ProcessId"]?.ToString(), out var pid))
                {
                    childIds.Add(pid);
                    childIds.AddRange(GetChildProcessIds(pid, depth + 1));
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
                var cmdLine = obj["CommandLine"]?.ToString();
                if (!string.IsNullOrWhiteSpace(cmdLine))
                    return cmdLine;
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
            var process = Process.GetProcessById(processId);
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
            var process = Process.GetProcessById(processId);
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