// -----------------------------------------------------------------------------
// IDiagnosticEngine.cs — 疑难解答引擎统一入口
// 设计约束: 接口不可变；返回链诚实（失败必须在 DiagnosticReport.ErrorMessage 可见）
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>疑难解答引擎 — 全链路体检统一入口（P0 只实现 RunDiagnosticAsync）</summary>
public interface IDiagnosticEngine
{
    /// <summary>一键全链路体检（P0 默认）— 返回完整 DiagnosticReport，失败时 Succeeded=false + ErrorMessage 可见</summary>
    Task<DiagnosticReport> RunDiagnosticAsync(string serverJarPath, string? worldPath, CancellationToken ct = default);

    /// <summary>指定 CheckId 增量体检（P2 AI 引导式用，P0 暂不实现）</summary>
    Task<DiagnosticReport> RunChecksAsync(string serverJarPath, string? worldPath, IEnumerable<string> checkIds, CancellationToken ct = default);

    /// <summary>执行修复（P1 实现，P0 暂不实现）</summary>
    Task<FixResult> ExecuteFixAsync(string serverJarPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default);
}
