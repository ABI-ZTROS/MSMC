// -----------------------------------------------------------------------------
// DiagnosticTypes.cs — 疑难解答核心 DTO 类型
// 命名空间: io.NET.ZTR_OS.Features.Troubleshooting.Services
// 设计约束: 严格按 spec §4；不可变 record；System.Text.Json 友好
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>Severity 分级 — 与前端 🟢🟡🟧🔴💀 图标一一对应</summary>
public enum Severity { Ok = 0, Info = 1, Warning = 2, Error = 3, Critical = 4 }

/// <summary>一次性体检完整报告 — DiagnosticEngine.RunDiagnosticAsync 的统一输出</summary>
public sealed record DiagnosticReport(
    DateTime GeneratedAt,
    string MsmcVersion,
    string ServerJarPath,
    string WorldPath,
    ServerInfo Server,
    List<CheckResult> Checks,
    DiagnosticSummary Summary,
    List<Issue> Issues,
    List<PlayerStat> TopPlayers,
    DeepSeekAnalysis? AiAnalysis,
    string? DeepSeekRawResponse,
    bool Succeeded,
    string? ErrorMessage);

public sealed record ServerInfo(
    string JarName,
    string? CoreType,       // Paper / Spigot / Forge / Fabric / Quilt / Bukkit
    string? Version,
    string? JavaVersion,
    int? Port,
    long? TotalMemoryMb,
    int? ProcessId,
    bool IsRunning);

public sealed record DiagnosticSummary(
    int TotalChecks,
    int OkCount,
    int WarningCount,
    int ErrorCount,
    int CriticalCount,
    int AutoFixableCount,
    long ScanDurationMs);

public sealed record CheckResult(
    string CheckId,
    Severity Severity,
    string Category,       // Java / Process / Network / Config / Storage / Player / Region / Log
    string Title,
    string Detail,
    bool AutoFixable,
    FixAction? SuggestedFix,
    object? RawData);

public sealed record Issue(
    string IssueId,
    Severity Severity,
    string Category,
    string Title,
    string Detail,
    string? Hint,
    string? Suggestion,
    FixAction? Fix,
    Dictionary<string, object?> Context);

public sealed record FixAction(
    string FixId,
    string Label,
    bool Dangerous,
    string? DiffPreview,
    double Confidence,
    string? Rationale,
    List<FixStep> Steps);

public sealed record FixStep(
    string Label,
    string ActionType,     // backup / command / config_edit / kill_process / restart / cleanup_region
    bool Dangerous,
    bool ConfirmRequired,
    Dictionary<string, object?> Params);

public sealed record PlayerStat(
    string Uuid,
    string Name,
    int TotalItems,
    double WealthScore,
    int AnomalyCount,
    List<string> AnomalyTypes);

public sealed record DeepSeekAnalysis(
    string Summary,
    List<string> KeyFindings,
    List<FixAction> RecommendedActions,
    bool NeedMoreInfo,
    List<string>? SuggestedQuestions,
    string RawJson);

public sealed record FixResult(
    string FixId,
    bool Succeeded,
    int StepsCompleted,
    int StepsTotal,
    List<FixStepResult> StepResults,
    string? BackupPath,
    string? Error);

public sealed record FixStepResult(
    string Label,
    string ActionType,
    bool Succeeded,
    string? Message);

public enum FixTrustMode { Auto = 0, StepByStep = 1, DryRun = 2 }
