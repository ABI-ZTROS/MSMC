// -----------------------------------------------------------------------------
// IFixExecutor.cs — 修复执行器接口
// 设计约束: 先备份、再执行；失败返回诚实 FixResult
// -----------------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

/// <summary>修复执行器 — 7 个真实 FixAction 的统一执行入口</summary>
public interface IFixExecutor
{
    /// <summary>执行 FixAction。trustMode 决定执行策略：Auto 一步到位；StepByStep 每步返回等确认；DryRun 只模拟</summary>
    Task<FixResult> ExecuteAsync(string serverJarPath, string? worldPath, FixAction fix, FixTrustMode trustMode, CancellationToken ct = default);

    /// <summary>检测目标服务器进程是否运行中（通过 java 进程 MainWindowTitle 匹配 jarName）</summary>
    Task<bool> IsServerRunningAsync(string serverJarPath);

    /// <summary>杀掉目标服务器进程</summary>
    Task<bool> KillServerAsync(string serverJarPath);
}
