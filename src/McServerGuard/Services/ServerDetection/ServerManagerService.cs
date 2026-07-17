using System.Diagnostics;
using System.IO;
using System.Management;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

public interface IServerManagerService
{
    public bool IsServerRunning(ServerInstance server);
    public bool IsServerRunningByJarPath(string jarFilePath);
    public Process? StartServer(ServerInstance server);
    public bool StopServer(ServerInstance server);
    public bool StopServerByProcessId(int processId);
    public Process? FindServerProcess(ServerInstance server);
    public int? GetServerProcessId(string jarFilePath);
    public bool AnyServerRunning();
}

public class ServerManagerService : IServerManagerService
{
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
            
            var cmdArguments = $"/c \"\"{javaExe}\" {arguments} & pause\"";
            var fullCommand = $"{javaExe} {arguments}";
            
            Log.Information("📝 启动命令: {Cmd}", fullCommand);
            Log.Information("📁 工作目录: {Dir}", server.WorkingDirectory);
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cmdArguments,
                WorkingDirectory = server.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            var process = Process.Start(processStartInfo);
            if (process != null)
            {
                Log.Information("✅ 服务器进程已启动! PID={Pid}", process.Id);
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

    public bool StopServer(ServerInstance server)
    {
        Log.Information("🛑 尝试停止服务器: {JarName}", server.ServerJarName);

        var process = FindServerProcess(server);
        if (process != null)
        {
            return StopProcessTree(process.Id);
        }

        if (server.ProcessId > 0)
        {
            return StopServerByProcessId(server.ProcessId);
        }

        Log.Warning("⚠️ 未找到运行中的服务器进程");
        return false;
    }

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
                            // 返回一个新的 Process 对象，避免 using 释放
                            try { return Process.GetProcessById(process.Id); }
                            catch { return null; }
                        }
                    }
                    catch
                    {
                        // Process may have exited
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
                        // Process may have exited
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

    private string FormatMemorySize(long bytes)
    {
        if (bytes >= 1L << 30)
            return $"{bytes >> 30}G";
        if (bytes >= 1L << 20)
            return $"{bytes >> 20}M";
        return $"{bytes >> 10}K";
    }

    private bool StopProcessTree(int parentProcessId)
    {
        try
        {
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
                catch (Exception ex)
                {
                    Log.Warning("⚠️ 终止子进程失败 PID={Pid}: {Msg}", childId, ex.Message);
                }
            }

            var parentProcess = Process.GetProcessById(parentProcessId);
            if (!parentProcess.HasExited)
            {
                parentProcess.Kill();
                parentProcess.WaitForExit(5000);
                Log.Information("🔫 已终止父进程: PID={Pid}", parentProcessId);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 终止进程树失败 PID={Pid}", parentProcessId);
        }

        return false;
    }

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
}