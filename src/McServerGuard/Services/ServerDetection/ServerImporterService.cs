using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using McServerGuard.Constants;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

public interface IServerImporterService
{
    public ServerInstance? ImportServer(string jarFilePath);
    public ServerInstance? ImportServer(string jarFilePath, List<string> customJvmArguments);
    public bool IsJarFileInUse(string jarFilePath);
    public ServerType DetectServerType(string jarFilePath);
    public string? GetServerWorkingDirectory(string jarFilePath);
}

public class ServerImporterService : IServerImporterService
{
    public ServerInstance? ImportServer(string jarFilePath)
    {
        return ImportServer(jarFilePath, []);
    }

    public ServerInstance? ImportServer(string jarFilePath, List<string> customJvmArguments)
    {
        Log.Information("📥 开始导入服务器: {JarPath}", jarFilePath);

        if (!File.Exists(jarFilePath))
        {
            Log.Error("❌ JAR 文件不存在: {JarPath}", jarFilePath);
            return null;
        }

        var jarFile = new FileInfo(jarFilePath);
        var workingDir = jarFile.Directory?.FullName ?? string.Empty;
        
        if (string.IsNullOrEmpty(workingDir))
        {
            Log.Error("❌ 无法获取 JAR 文件所在目录: {JarPath}", jarFilePath);
            return null;
        }

        var isRunning = IsJarFileInUse(jarFilePath);
        var serverType = DetectServerType(jarFilePath);

        Log.Information("📋 服务器信息: 类型={Type}, 运行中={IsRunning}, 工作目录={Dir}", 
            serverType, isRunning, workingDir);

        int processId = 0;
        string javaPath = string.Empty;
        string fullCommandLine = string.Empty;
        long initialHeapBytes = 0;
        long maxHeapBytes = 0;
        string gcType = string.Empty;
        bool usesAikarFlags = false;
        var configFiles = new List<string>();

        if (isRunning)
        {
            var processInfo = FindRunningJavaProcess(jarFilePath);
            if (processInfo != null)
            {
                processId = processInfo.Id;
                javaPath = processInfo.MainModule?.FileName ?? string.Empty;
                fullCommandLine = GetProcessCommandLine(processInfo.Id);
                
                var parsed = CommandLineParser.Parse(fullCommandLine);
                initialHeapBytes = parsed.InitialHeapMemoryBytes;
                maxHeapBytes = parsed.MaxHeapMemoryBytes;
                gcType = parsed.GcType;
                usesAikarFlags = parsed.UsesAikarFlags;
            }
        }
        else
        {
            initialHeapBytes = ParseMemorySize(customJvmArguments.FirstOrDefault(a => a.StartsWith("-Xms"))?.Replace("-Xms", "") ?? "2G");
            maxHeapBytes = ParseMemorySize(customJvmArguments.FirstOrDefault(a => a.StartsWith("-Xmx"))?.Replace("-Xmx", "") ?? "4G");
            
            if (customJvmArguments.Any(a => a.Contains("UseG1GC")))
                gcType = "G1GC";
            else if (customJvmArguments.Any(a => a.Contains("UseZGC")))
                gcType = "ZGC";
            else
                gcType = "G1GC";
            
            usesAikarFlags = customJvmArguments.Any(a => a.Contains("aikars"));
        }

        var scanner = new ConfigFileScanner();
        configFiles = scanner.ScanAll(workingDir);

        var server = new ServerInstance
        {
            ProcessId = processId,
            ServerType = serverType,
            WorkingDirectory = workingDir,
            JavaPath = javaPath,
            ServerJarPath = jarFilePath,
            ServerJarName = jarFile.Name,
            FullCommandLine = fullCommandLine,
            JvmArguments = customJvmArguments,
            InitialHeapMemoryBytes = initialHeapBytes,
            MaxHeapMemoryBytes = maxHeapBytes,
            ConfigFiles = configFiles,
            UsesAikarFlags = usesAikarFlags,
            GcType = gcType,
            ServerPort = ServerConstants.DefaultServerPort
        };

        Log.Information("✅ 服务器导入成功: {DisplayName}", server.DisplayName);
        return server;
    }

    public bool IsJarFileInUse(string jarFilePath)
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
            Log.Error(ex, "❌ 检查 JAR 文件占用状态失败: {JarPath}", jarFilePath);
            return false;
        }
    }

    public ServerType DetectServerType(string jarFilePath)
    {
        var jarName = Path.GetFileName(jarFilePath);
        return ServerTypeClassifier.ClassifyByJarName(jarName);
    }

    public string? GetServerWorkingDirectory(string jarFilePath)
    {
        var jarFile = new FileInfo(jarFilePath);
        return jarFile.Directory?.FullName;
    }

    private Process? FindRunningJavaProcess(string jarFilePath)
    {
        var jarName = Path.GetFileName(jarFilePath).ToLowerInvariant();
        
        try
        {
            var javaProcesses = Process.GetProcessesByName("java");
            foreach (var process in javaProcesses)
            {
                try
                {
                    var commandLine = GetProcessCommandLine(process.Id);
                    if (!string.IsNullOrEmpty(commandLine) && 
                        commandLine.ToLowerInvariant().Contains(jarName))
                    {
                        return process;
                    }
                }
                catch
                {
                    // 进程可能已退出
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 查找运行中的 Java 进程失败");
        }

        return null;
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

    private long ParseMemorySize(string sizeStr)
    {
        if (string.IsNullOrWhiteSpace(sizeStr))
            return 0;

        sizeStr = sizeStr.Trim().ToUpperInvariant();
        long multiplier = 1;

        if (sizeStr.EndsWith("G"))
        {
            multiplier = 1L << 30;
            sizeStr = sizeStr.TrimEnd('G');
        }
        else if (sizeStr.EndsWith("M"))
        {
            multiplier = 1L << 20;
            sizeStr = sizeStr.TrimEnd('M');
        }
        else if (sizeStr.EndsWith("K"))
        {
            multiplier = 1L << 10;
            sizeStr = sizeStr.TrimEnd('K');
        }

        if (long.TryParse(sizeStr, out var value))
            return value * multiplier;

        return 0;
    }
}