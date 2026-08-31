// -----------------------------------------------------------------------------
// 文件名: StartupScriptAutoDetector.cs
// 功能描述: 优先级链扫描服务器目录下的启动脚本，并组合 StartBatParserService
//          + StartupScriptDetector 进行解析，生成 StartupConfig 快照固化。
// 三链原则:
//   因果链: KnownServer.Startup==null / 导入新服 → 扫描 + 解析
//   执行链: 标准文件名链 → 目录兜底 → 用户覆盖优先级最高
//   返回链: 每步结构化日志 + 解析失败兜底返回 null 不阻塞启动
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using io.NET.ZTR_OS.Features.ServerDetection.Models;
using Serilog;

namespace io.NET.ZTR_OS.Features.ServerDetection.Services;

public static class StartupScriptAutoDetector
{
    private static readonly ILogger Log = Serilog.Log.ForContext<StartupScriptAutoDetector>();

    /// <summary>标准脚本文件名（按优先级）</summary>
    private static readonly string[] StandardPatterns = ["start.bat", "run.bat", "start.cmd", "run.cmd"];

    /// <summary>
    /// 优先级链查找脚本（用户覆盖优先 → 标准名 → 目录兜底）
    /// </summary>
    /// <param name="workingDirectory">服务器工作目录</param>
    /// <param name="userOverridePath">用户手动指定的脚本路径（可空）</param>
    /// <returns>脚本绝对路径；未找到返回 null</returns>
    public static string? FindScript(string workingDirectory, string? userOverridePath = null)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
            return null;

        // 1. 用户手动覆盖（最高优先级）
        if (!string.IsNullOrWhiteSpace(userOverridePath) && File.Exists(userOverridePath))
        {
            Log.Debug("[SCRIPT] 使用用户手动指定脚本: {Path}", userOverridePath);
            return userOverridePath;
        }

        // 2. 标准文件名按优先级
        foreach (var pattern in StandardPatterns)
        {
            var candidate = Path.Combine(workingDirectory, pattern);
            if (File.Exists(candidate))
            {
                Log.Debug("[SCRIPT] 标准命名匹配: {Pattern} → {Path}", pattern, candidate);
                return candidate;
            }
        }

        // 3. 兜底：目录下第一个 .bat / .cmd
        var fallback = Directory.GetFiles(workingDirectory, "*.bat", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(workingDirectory, "*.cmd", SearchOption.TopDirectoryOnly))
            .FirstOrDefault();

        if (fallback != null)
        {
            Log.Debug("[SCRIPT] 兜底匹配目录下脚本: {Path}", fallback);
        }

        return fallback;
    }

    /// <summary>
    /// 组合 StartBatParserService（参数解析）+ StartupScriptDetector（启发式分析）
    /// 生成 StartupConfig 快照。
    /// </summary>
    public static StartupConfig? AutoDetectAndPopulateStartup(string workingDirectory, string? existingScriptPath = null)
    {
        var scriptPath = FindScript(workingDirectory, existingScriptPath);
        if (scriptPath == null)
        {
            Log.Debug("[SCRIPT] 未在目录找到任何启动脚本: {Dir}", workingDirectory);
            return null;
        }

        // 执行链：两个解析器组合使用
        var parserResult = StartBatParserService.ParseFile(scriptPath, workingDirectory);

        var content = string.Empty;
        try { content = File.ReadAllText(scriptPath); }
        catch (Exception ex) { Log.Warning(ex, "[SCRIPT] 读取脚本失败: {Path}", scriptPath); }

        var heuristic = string.IsNullOrEmpty(content)
            ? new StartupScriptInfo { RawContent = content }
            : StartupScriptDetector.Analyze(content);

        // 执行链兜底：两个解析器都不行则返回 null，不阻塞启动
        if (!parserResult.Success && !heuristic.IsServerStartupScript)
        {
            Log.Warning("[SCRIPT] 找到脚本但解析失败: {Path} | ParserErr={ParserErr}", scriptPath, parserResult.ErrorMessage);
            return null;
        }

        var config = new StartupConfig
        {
            Mode = StartupMode.Manual,
            ScriptPath = scriptPath,
            ScriptName = Path.GetFileName(scriptPath),
            LastParseTime = DateTime.Now,
            HasAutoRestart = heuristic.HasAutoRestart,
            ScriptJvmArgs = parserResult.JvmArguments,
            ScriptJarPath = parserResult.JarPath,
            ScriptMaxHeapBytes = parserResult.MaxHeapBytes ?? 0,
            ScriptInitialHeapBytes = parserResult.InitialHeapBytes ?? 0,
        };

        // 返回链：详细结构化日志
        Log.Information(
            "[SCRIPT] ✅ 脚本检测成功: {Path} | HasAutoRestart={AR} | HeapMax={Max} | HeapInit={Init} | Jar={Jar} | JvmArgsCount={Count}",
            scriptPath, config.HasAutoRestart, config.ScriptMaxHeapBytes, config.ScriptInitialHeapBytes,
            config.ScriptJarPath, config.ScriptJvmArgs.Count);

        if (parserResult.UnknownArgs.Count > 0)
        {
            Log.Debug("[SCRIPT] 未识别参数: {Args}", string.Join(", ", parserResult.UnknownArgs));
        }

        return config;
    }

    /// <summary>
    /// 对比 KnownServer 手动配置 vs StartupConfig 脚本快照，生成 DiffReport。
    /// </summary>
    public static DiffReport? ComputeDiff(KnownServer server, StartupConfig script)
    {
        if (server == null || script == null) return null;

        var manualArgs = server.JvmArguments ?? new List<string>();
        var scriptArgs = script.ScriptJvmArgs ?? new List<string>();

        var manualSet = new HashSet<string>(manualArgs, StringComparer.OrdinalIgnoreCase);
        var scriptSet = new HashSet<string>(scriptArgs, StringComparer.OrdinalIgnoreCase);

        var added = manualArgs.Where(a => !scriptSet.Contains(a)).ToList();
        var removed = scriptArgs.Where(a => !manualSet.Contains(a)).ToList();

        bool heapMaxDiff = script.ScriptMaxHeapBytes != 0 && script.ScriptMaxHeapBytes != server.MaxHeapMemoryBytes;
        bool heapInitDiff = script.ScriptInitialHeapBytes != 0 && script.ScriptInitialHeapBytes != server.InitialHeapMemoryBytes;

        return new DiffReport
        {
            JarPathChanged = script.ScriptJarPath != null && !string.Equals(script.ScriptJarPath, server.ServerJarPath, StringComparison.OrdinalIgnoreCase),
            HeapMaxFrom = heapMaxDiff ? FormatBytes(script.ScriptMaxHeapBytes) : null,
            HeapMaxTo = heapMaxDiff ? FormatBytes(server.MaxHeapMemoryBytes) : null,
            HeapInitFrom = heapInitDiff ? FormatBytes(script.ScriptInitialHeapBytes) : null,
            HeapInitTo = heapInitDiff ? FormatBytes(server.InitialHeapMemoryBytes) : null,
            JvmArgsAdded = added,
            JvmArgsRemoved = removed,
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0";
        string[] units = ["B", "KB", "MB", "GB"];
        int i = 0;
        double d = bytes;
        while (d >= 1024 && i < units.Length - 1) { d /= 1024; i++; }
        return $"{d:0.##}{units[i]}";
    }
}

/// <summary>手动配置 vs 脚本快照的 Diff 报告（用于 Bridge 返回）</summary>
public class DiffReport
{
    public bool JarPathChanged { get; set; }
    public string? HeapMaxFrom { get; set; }
    public string? HeapMaxTo { get; set; }
    public string? HeapInitFrom { get; set; }
    public string? HeapInitTo { get; set; }
    public List<string> JvmArgsAdded { get; set; } = [];
    public List<string> JvmArgsRemoved { get; set; } = [];
}
