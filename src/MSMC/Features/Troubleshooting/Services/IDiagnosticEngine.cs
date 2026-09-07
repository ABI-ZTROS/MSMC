// -----------------------------------------------------------------------------
// IDiagnosticEngine.cs — 疑难解答引擎统一入口（扩展后）
// 分阶段扫描 + 修复执行 + 服务器状态查询
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>疑难解答引擎 — 分阶段扫描 + 修复执行统一入口</summary>
public interface IDiagnosticEngine
{
    /// <summary>快速扫描 — 只跑 CheckRunner（12 个系统/配置/日志检查），3-5 秒完成</summary>
    Task<DiagnosticReport> RunQuickAsync(string serverJarPath, string? worldPath, CancellationToken ct = default);

    /// <summary>深度扫描 — Quick + 存档 NBT/Region 分析，可能 10-30 秒</summary>
    Task<DiagnosticReport> RunDeepAsync(string serverJarPath, string? worldPath, CancellationToken ct = default);

    /// <summary>一键全链路（兼容旧调用，内部调 RunQuickAsync）</summary>
    Task<DiagnosticReport> RunDiagnosticAsync(string serverJarPath, string? worldPath, CancellationToken ct = default);

    /// <summary>指定 CheckId 增量体检（预留）</summary>
    Task<DiagnosticReport> RunChecksAsync(string serverJarPath, string? worldPath, IEnumerable<string> checkIds, CancellationToken ct = default);

    /// <summary>执行修复</summary>
    Task<FixResult> ExecuteFixAsync(string serverJarPath, string? worldPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default);

    /// <summary>服务器进程状态查询</summary>
    Task<bool> IsServerRunningAsync(string serverJarPath);

    /// <summary>杀掉服务器进程</summary>
    Task<bool> KillServerAsync(string serverJarPath);
}
