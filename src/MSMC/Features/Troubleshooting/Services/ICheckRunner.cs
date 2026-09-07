// -----------------------------------------------------------------------------
// ICheckRunner.cs — 检查运行器接口
// -----------------------------------------------------------------------------
using System.Collections.Generic;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>检查运行器 — 无状态，每次调用独立；失败也返回 CheckResult（不吞异常）</summary>
public interface ICheckRunner
{
    /// <summary>所有已知 CheckId</summary>
    IReadOnlyCollection<string> KnownCheckIds { get; }

    /// <summary>跑 P0 所有系统/配置检查，返回 10 个 CheckResult</summary>
    List<CheckResult> RunAll(string serverJarPath, string? worldPath, ServerInfo server);
}
