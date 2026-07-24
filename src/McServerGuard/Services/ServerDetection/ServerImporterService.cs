// -----------------------------------------------------------------------------
// 文件名: ServerImporterService.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 服务器导入服务，实现 JAR 文件的服务器实例识别与信息提取
// 依赖组件: System.Diagnostics, System.Management, System.IO, McServerGuard.Constants, McServerGuard.Models, Serilog
// 设计模式: 服务模式、工厂模式、适配器模式
// -----------------------------------------------------------------------------
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using McServerGuard.Constants;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>
/// 服务器导入服务接口契约
/// </summary>
public interface IServerImporterService
{
    /// <summary>
    /// 从 JAR 文件导入服务器实例
    /// </summary>
    /// <param name="jarFilePath">服务器 JAR 文件路径</param>
    /// <returns>服务器实例对象；导入失败返回 null</returns>
    public ServerInstance? ImportServer(string jarFilePath);

    /// <summary>
    /// 从 JAR 文件导入服务器实例（附带自定义 JVM 参数）
    /// </summary>
    /// <param name="jarFilePath">服务器 JAR 文件路径</param>
    /// <param name="customJvmArguments">自定义 JVM 参数列表</param>
    /// <returns>服务器实例对象；导入失败返回 null</returns>
    public ServerInstance? ImportServer(string jarFilePath, List<string> customJvmArguments);

    /// <summary>
    /// 异步从 JAR 文件导入服务器实例 —— 将 WMI 查询与配置文件扫描放到线程池执行，避免阻塞 UI 线程
    /// </summary>
    /// <param name="jarFilePath">服务器 JAR 文件路径</param>
    /// <param name="customJvmArguments">自定义 JVM 参数列表</param>
    /// <returns>服务器实例对象；导入失败返回 null</returns>
    public Task<ServerInstance?> ImportServerAsync(string jarFilePath, List<string> customJvmArguments);

    /// <summary>
    /// 异步从 JAR 文件导入服务器实例（使用默认 JVM 参数）
    /// </summary>
    /// <param name="jarFilePath">服务器 JAR 文件路径</param>
    /// <returns>服务器实例对象；导入失败返回 null</returns>
    public Task<ServerInstance?> ImportServerAsync(string jarFilePath);

    /// <summary>
    /// 检测 JAR 文件是否正在被使用（文件被占用）
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径</param>
    /// <returns>文件被占用返回 true</returns>
    public bool IsJarFileInUse(string jarFilePath);

    /// <summary>
    /// 检测服务器类型
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径</param>
    /// <returns>服务器类型枚举值</returns>
    public ServerType DetectServerType(string jarFilePath);

    /// <summary>
    /// 获取服务器工作目录
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径</param>
    /// <returns>工作目录路径；获取失败返回 null</returns>
    public string? GetServerWorkingDirectory(string jarFilePath);
}

/// <summary>
/// 服务器导入服务
/// </summary>
/// <remarks>
/// <para>实现 Minecraft 服务器实例的导入与信息提取。支持从单个 JAR 文件
/// 识别服务器类型、提取运行时参数、扫描配置文件等全流程导入操作。</para>
/// <para>核心能力：
///   - 基于 JAR 文件名的服务器类型识别
///   - 运行中服务器进程信息提取（WMI 查询命令行）
///   - JVM 参数解析（内存、GC、优化标志等）
///   - 配置文件自动扫描
/// </para>
/// </remarks>
public class ServerImporterService : IServerImporterService
{
    /// <summary>
    /// 配置文件扫描器 —— DI 注入，避免手动 new 导致的耦合与生命周期失控
    /// </summary>
    private readonly ConfigFileScanner _configScanner;

    /// <summary>
    /// 初始化服务器导入服务
    /// </summary>
    /// <param name="configScanner">配置文件扫描器（DI 注入）</param>
    public ServerImporterService(ConfigFileScanner configScanner)
    {
        _configScanner = configScanner;
    }

    /// <summary>
    /// 从 JAR 文件导入服务器实例（使用默认 JVM 参数）
    /// </summary>
    /// <param name="jarFilePath">服务器 JAR 文件路径</param>
    /// <returns>服务器实例对象；导入失败返回 null</returns>
    public ServerInstance? ImportServer(string jarFilePath)
    {
        return ImportServer(jarFilePath, []);
    }

    /// <summary>
    /// 从 JAR 文件导入服务器实例（附带自定义 JVM 参数）
    /// </summary>
    /// <param name="jarFilePath">服务器 JAR 文件路径</param>
    /// <param name="customJvmArguments">自定义 JVM 参数列表</param>
    /// <returns>服务器实例对象；导入失败返回 null</returns>
    public ServerInstance? ImportServer(string jarFilePath, List<string> customJvmArguments)
    {
        Log.Information("开始导入服务器: {JarPath}", jarFilePath);

        if (!File.Exists(jarFilePath))
        {
            Log.Error("JAR 文件不存在: {JarPath}", jarFilePath);
            return null;
        }

        var jarFile = new FileInfo(jarFilePath);
        var workingDir = jarFile.Directory?.FullName ?? string.Empty;

        if (string.IsNullOrEmpty(workingDir))
        {
            Log.Error("无法获取 JAR 文件所在目录: {JarPath}", jarFilePath);
            return null;
        }

        var isRunning = IsJarFileInUse(jarFilePath);
        var serverType = DetectServerType(jarFilePath);

        Log.Information("服务器信息: 类型={Type}, 运行中={IsRunning}, 工作目录={Dir}",
            serverType, isRunning, workingDir);

        // 根据运行状态分流提取 JVM 运行时参数
        var (processId, javaPath, fullCommandLine, jvmInfo) = isRunning
            ? ExtractRunningProcessInfo(jarFilePath)
            : (0, string.Empty, string.Empty, ParseJvmArgumentsFromCustom(customJvmArguments));

        // 扫描配置文件（使用 DI 注入的扫描器）
        var configFiles = _configScanner.ScanAll(workingDir);

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
            InitialHeapMemoryBytes = jvmInfo.InitialHeapBytes,
            MaxHeapMemoryBytes = jvmInfo.MaxHeapBytes,
            ConfigFiles = configFiles,
            UsesAikarFlags = jvmInfo.UsesAikarFlags,
            GcType = jvmInfo.GcType,
            ServerPort = ServerConstants.DefaultServerPort
        };

        Log.Information("服务器导入成功: {DisplayName}", server.DisplayName);
        return server;
    }

    /// <summary>
    /// 从运行中进程提取 JVM 运行时参数 —— 拆分自 ImportServer 减少方法复杂度
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径（用于查找运行中进程）</param>
    /// <returns>（进程ID, Java路径, 完整命令行, JVM参数信息）元组</returns>
    private (int ProcessId, string JavaPath, string CommandLine, JvmRuntimeInfo JvmInfo) ExtractRunningProcessInfo(string jarFilePath)
    {
        var processInfo = FindRunningJavaProcess(jarFilePath);
        if (processInfo == null)
            return (0, string.Empty, string.Empty, default);

        try
        {
            int pid = processInfo.Id;
            string javaPath = string.Empty;
            try { javaPath = processInfo.MainModule?.FileName ?? string.Empty; }
            catch (Exception ex) { Log.Debug(ex, "读取 MainModule 失败 PID={Pid}", pid); }

            var fullCommandLine = GetProcessCommandLine(pid);
            var parsed = CommandLineParser.Parse(fullCommandLine);

            return (pid, javaPath, fullCommandLine, new JvmRuntimeInfo(
                parsed.InitialHeapMemoryBytes,
                parsed.MaxHeapMemoryBytes,
                parsed.GcType,
                parsed.UsesAikarFlags));
        }
        finally
        {
            processInfo.Dispose();
        }
    }

    /// <summary>
    /// 从自定义 JVM 参数列表解析运行时信息 —— 拆分自 ImportServer 减少方法复杂度
    /// </summary>
    /// <param name="customJvmArguments">自定义 JVM 参数列表</param>
    /// <returns>JVM 运行时信息结构</returns>
    private JvmRuntimeInfo ParseJvmArgumentsFromCustom(List<string> customJvmArguments)
    {
        var initialHeap = ParseMemorySize(
            customJvmArguments.FirstOrDefault(a => a.StartsWith("-Xms"))?.Replace("-Xms", "") ?? "2G");
        var maxHeap = ParseMemorySize(
            customJvmArguments.FirstOrDefault(a => a.StartsWith("-Xmx"))?.Replace("-Xmx", "") ?? "4G");

        var gcType = customJvmArguments.Any(a => a.Contains("UseG1GC")) ? "G1GC"
            : customJvmArguments.Any(a => a.Contains("UseZGC")) ? "ZGC"
            : "G1GC";

        var usesAikarFlags = customJvmArguments.Any(a => a.Contains("aikars"));

        return new JvmRuntimeInfo(initialHeap, maxHeap, gcType, usesAikarFlags);
    }

    /// <summary>
    /// JVM 运行时参数信息 —— 用于 ImportServer 内部分流提取结果的传递
    /// </summary>
    private readonly struct JvmRuntimeInfo(long initialHeap, long maxHeap, string gcType, bool usesAikarFlags)
    {
        public long InitialHeapBytes { get; } = initialHeap;
        public long MaxHeapBytes { get; } = maxHeap;
        public string GcType { get; } = gcType;
        public bool UsesAikarFlags { get; } = usesAikarFlags;
    }

    /// <summary>
    /// 异步从 JAR 文件导入服务器实例（使用默认 JVM 参数）
    /// </summary>
    /// <param name="jarFilePath">服务器 JAR 文件路径</param>
    /// <returns>服务器实例对象；导入失败返回 null</returns>
    public Task<ServerInstance?> ImportServerAsync(string jarFilePath)
    {
        return ImportServerAsync(jarFilePath, []);
    }

    /// <summary>
    /// 异步从 JAR 文件导入服务器实例 —— 将 WMI 查询与配置文件扫描放到线程池执行，避免阻塞 UI 线程
    /// </summary>
    /// <param name="jarFilePath">服务器 JAR 文件路径</param>
    /// <param name="customJvmArguments">自定义 JVM 参数列表</param>
    /// <returns>服务器实例对象；导入失败返回 null</returns>
    /// <remarks>
    /// ImportServer 内部执行 WMI 查询（FindRunningJavaProcess/GetProcessCommandLine）与
    /// 配置文件扫描（ConfigFileScanner.ScanAll），均为同步阻塞调用。
    /// 通过 Task.Run 将其封送到线程池，防止用户点击"导入"后 UI 卡顿。
    /// </remarks>
    public Task<ServerInstance?> ImportServerAsync(string jarFilePath, List<string> customJvmArguments)
    {
        return Task.Run(() => ImportServer(jarFilePath, customJvmArguments));
    }

    /// <summary>
    /// 检测 JAR 文件是否正在被使用（文件被占用）
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径</param>
    /// <returns>文件被占用返回 true</returns>
    /// <remarks>
    /// 通过尝试以独占读取方式打开文件来判断文件是否被其他进程占用。
    /// 若打开失败（抛出 IOException），则判定文件正在使用中。
    /// </remarks>
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
            Log.Error(ex, "检查 JAR 文件占用状态失败: {JarPath}", jarFilePath);
            return false;
        }
    }

    /// <summary>
    /// 检测服务器类型
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径</param>
    /// <returns>服务器类型枚举值</returns>
    public ServerType DetectServerType(string jarFilePath)
    {
        var jarName = Path.GetFileName(jarFilePath);
        return ServerTypeClassifier.ClassifyByJarName(jarName);
    }

    /// <summary>
    /// 获取服务器工作目录
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径</param>
    /// <returns>工作目录路径；获取失败返回 null</returns>
    public string? GetServerWorkingDirectory(string jarFilePath)
    {
        var jarFile = new FileInfo(jarFilePath);
        return jarFile.Directory?.FullName;
    }

    /// <summary>
    /// 查找运行中引用指定 JAR 文件的 Java 进程
    /// </summary>
    /// <param name="jarFilePath">JAR 文件路径</param>
    /// <returns>进程对象；未找到返回 null（调用方负责 Dispose）</returns>
    /// <remarks>
    /// 遍历到的非匹配 Process 对象在此处立即释放，避免句柄泄漏；
    /// 仅返回匹配进程的所有权给调用方（调用方使用后需 Dispose）。
    /// </remarks>
    private Process? FindRunningJavaProcess(string jarFilePath)
    {
        var jarName = Path.GetFileName(jarFilePath).ToLowerInvariant();

        try
        {
            var javaProcesses = Process.GetProcessesByName("java");
            foreach (var process in javaProcesses)
            {
                bool matched = false;
                try
                {
                    var commandLine = GetProcessCommandLine(process.Id);
                    if (!string.IsNullOrEmpty(commandLine) &&
                        commandLine.ToLowerInvariant().Contains(jarName))
                    {
                        matched = true; // 标记匹配，阻止 finally 释放
                        return process; // 所有权转移给调用方
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "进程访问被拒绝");
                }
                finally
                {
                    // 仅释放非匹配进程，匹配进程所有权已转移给调用方
                    if (!matched)
                    {
                        try { process.Dispose(); } catch { }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "查找运行中的 Java 进程失败");
        }

        return null;
    }

    /// <summary>
    /// 获取指定进程的完整命令行
    /// </summary>
    /// <param name="processId">进程 ID</param>
    /// <returns>命令行字符串；获取失败返回空字符串</returns>
    /// <remarks>
    /// 通过 WMI 查询 Win32_Process 类的 CommandLine 属性获取。
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
            Log.Error(ex, "获取命令行失败 PID={Pid}", processId);
        }

        return string.Empty;
    }

    /// <summary>
    /// 解析内存大小字符串为字节数
    /// </summary>
    /// <param name="sizeStr">内存大小字符串（如 4G、1024M、512K）</param>
    /// <returns>字节数；解析失败返回 0</returns>
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
