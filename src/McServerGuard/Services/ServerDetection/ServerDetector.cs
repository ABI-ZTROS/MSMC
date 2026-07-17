// 🎮 核心检测编排器 —— 把所有检测组件串起来，像指挥家一样指挥全场
// 它自己不干活，但负责调度所有干活的组件 —— 典型的管理层 🤌
namespace McServerGuard.Services.ServerDetection;

using System.Diagnostics;
using System.IO;
using McServerGuard.Constants;
using McServerGuard.Models;
using Serilog;

/// <summary>
/// 服务器检测编排器 —— 坐镇指挥，调度 ProcessScanner、WorkingDirectoryResolver、ConfigFileScanner
/// 它是检测流程的"总导演"，负责把各个组件的输出拼接成完整的 ServerInstance
/// </summary>
public class ServerDetector : IServerDetector
{
    private readonly ProcessScanner _processScanner;
    private readonly WorkingDirectoryResolver _workingDirResolver;
    private readonly ConfigFileScanner _configScanner;

    public ServerDetector(
        ProcessScanner processScanner,
        WorkingDirectoryResolver workingDirResolver,
        ConfigFileScanner configScanner)
    {
        _processScanner = processScanner;
        _workingDirResolver = workingDirResolver;
        _configScanner = configScanner;
        Log.Information("🕵️ ServerDetector 初始化完毕，准备出击");
    }

    /// <summary>
    /// 执行完整的服务器检测流程
    /// 流程：扫描进程 → 解析工作目录 → 扫描配置文件 → 构建 ServerInstance
    /// </summary>
    public async Task<DetectionResult> DetectAllAsync()
    {
        var servers = new List<ServerInstance>();
        var logMessages = new List<string>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Log.Information("🔍 DetectAllAsync: 开始扫描所有 Java 进程...");

        // 第一步：扫描所有 Java 进程
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

        // 第二步：对每个进程执行深度检测
        int i = 0;
        foreach (var (processId, commandLine) in processResults)
        {
            i++;
            Log.Debug("🔄 正在检查第 {Index} 个 Java 进程: PID={Pid}", i, processId);
            try
            {
                var server = await BuildServerInstanceAsync(processId, commandLine);
                if (server is not null)
                {
                    Log.Debug("✅ 识别到服务器: {Type} @ {Dir}", server.ServerType, server.WorkingDirectory);
                    servers.Add(server);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"检测进程 PID={processId} 时出错: {ex.Message}";
                Log.Error(ex, "💥 fuck: 无法解析进程 PID={Pid}: {Message}", processId, ex.Message);
                logMessages.Add(errorMsg);
            }
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
    /// 从一个 Java 进程构建完整的 ServerInstance
    /// 这是最复杂的部分，需要综合多个组件的结果
    /// </summary>
    private async Task<ServerInstance?> BuildServerInstanceAsync(int processId, string commandLine)
    {
        // 使用 CommandLineParser 解析命令行 —— 它是专业的
        var parsed = CommandLineParser.Parse(commandLine);

        // 排除客户端
        if (parsed.HasClientMarkers)
        {
            Log.Debug("进程 PID={Pid} 有客户端标志，已排除", processId);
            return null;
        }

        var jarName = string.IsNullOrEmpty(parsed.JarFileName)
            ? "unknown.jar"
            : parsed.JarFileName;

        // 解析工作目录
        var workingDir = await Task.Run(() =>
            _workingDirResolver.Resolve(processId, commandLine, jarName));

        // 扫描配置文件（使用真正的异步 IO）
        var configFiles = await _configScanner.ScanAllAsync(workingDir);

        // 推断服务器类型（使用已有的 ServerTypeClassifier）
        var serverType = ServerTypeClassifier.ClassifyByJarNameAndConfigFiles(jarName, workingDir);

        Log.Information(
            "构建服务器实例: PID={Pid}, Type={Type}, Jar={Jar}, Dir={Dir}",
            processId, serverType, jarName, workingDir);

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
            DetectedAt = DateTime.Now,
        };
    }

    /// <summary>
    /// 扫描目录中的启动脚本（.bat 和 .sh 文件）
    /// 启动脚本是服务器的"身份证"，里面记录了 JAR 名称、JVM 参数等重要信息
    /// </summary>
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
            catch (Exception ex)
            {
                Log.Error(ex, "💥 fuck: 启动脚本分析失败: {File}: {Message}", file, ex.Message);
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
            catch (Exception ex)
            {
                Log.Error(ex, "💥 fuck: 启动脚本分析失败: {File}: {Message}", file, ex.Message);
            }
        }

        Log.Information("找到 {Count} 个启动脚本", scripts.Count);
        return scripts;
    }

    /// <summary>
    /// 分析单个启动脚本文件（委托给已有的 StartupScriptDetector）
    /// </summary>
    private StartupScriptInfo AnalyzeStartupScript(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var info = StartupScriptDetector.Analyze(content);

        // 补充文件路径和名称信息
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

    // 🔄 自动检测循环控制
    private CancellationTokenSource? _autoDetectCts;
    private Task? _autoDetectTask;
    private readonly object _autoDetectLock = new();

    /// <summary>
    /// 自动检测是否正在运行
    /// </summary>
    public bool IsAutoDetectRunning => _autoDetectTask != null && !_autoDetectTask.IsCompleted;

    /// <summary>
    /// 启动自动检测（每秒一次死循环）
    /// </summary>
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
                Log.Information("⏱️ 自动检测循环已启动，每秒检测一次服务器");
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await DetectServersAsync(token);
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
                        await Task.Delay(1000, token);
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
    /// 停止自动检测
    /// </summary>
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
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        StopAutoDetect();
        GC.SuppressFinalize(this);
    }
}
