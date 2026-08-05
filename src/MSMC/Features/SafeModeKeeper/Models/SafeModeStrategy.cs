// -----------------------------------------------------------------------------
// 文件名: SafeModeStrategy.cs
// 命名空间: io.NET.ZTR_OS.Features.SafeModeKeeper.Models
// 功能描述: 安全模式三级降级策略枚举 + 崩溃记录/清单持久化模型
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SafeModeKeeper.Models;

using System.Collections.Generic;

/// <summary>
/// 安全模式降级等级
/// </summary>
public enum SafeModeLevel
{
    /// <summary>未降级</summary>
    None = 0,

    /// <summary>L1 轻度：禁用所有 plugins/*.jar（改名 .disabled）</summary>
    L1 = 1,

    /// <summary>L2 中度：降低 server.properties 渲染参数 + 临时离线</summary>
    L2 = 2,

    /// <summary>L3 重度：保守 JVM 参数（SerialGC + 小堆）</summary>
    L3 = 3,
}

/// <summary>
/// safemode.json 持久化结构：最近启动存活记录 + 当前 streak
/// </summary>
public sealed class SafeModeState
{
    /// <summary>最近 N 次启动的存活时长（ms），最多保留 5 条</summary>
    public List<long> RecentUptimesMs { get; set; } = new();

    /// <summary>当前连续崩溃 streak（均小于阈值才算）</summary>
    public int CurrentCrashStreak { get; set; }

    /// <summary>是否已触发安全模式（streak ≥ 3）</summary>
    public bool SafeModeTriggered { get; set; }
}

/// <summary>
/// safemode_manifest.json：降级动作清单，用于 ExitSafeMode 还原
/// </summary>
public sealed class SafeModeManifest
{
    /// <summary>当前已经应用的等级</summary>
    public SafeModeLevel AppliedLevel { get; set; }

    /// <summary>L1：被改名的 jar 列表（原路径 → 新路径）</summary>
    public Dictionary<string, string> RenamedPlugins { get; set; } = new();

    /// <summary>L2：被修改的 server.properties 原值（key → original value）</summary>
    public Dictionary<string, string> OriginalProperties { get; set; } = new();

    /// <summary>L3：原本是否存在 jvm.args + 其内容</summary>
    public bool HadJvmArgsFile { get; set; }

    /// <summary>L3：原 jvm.args 的原始内容（若存在）</summary>
    public string? OriginalJvmArgsContent { get; set; }
}
