// -----------------------------------------------------------------------------
// 文件名: IProcessManagerService.cs
// 命名空间: io.NET.ZTR_OS.Features.SystemMonitoring.Services
// 功能描述: 进程管理服务接口 —— 提供进程亲和性查询与进程管理操作契约
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SystemMonitoring.Services;

using io.NET.ZTR_OS.Features.SystemMonitoring.Models;

/// <summary>
/// 进程管理服务接口
/// </summary>
public interface IProcessManagerService
{
    /// <summary>
    /// 获取所有进程的亲和性信息（按 CPU 占用降序，最多返回 200 个）
    /// </summary>
    List<ProcessAffinityInfo> GetAllProcessAffinities();

    /// <summary>
    /// 获取所有 Java 进程的亲和性信息（保留向后兼容）
    /// </summary>
    [Obsolete("请使用 GetAllProcessAffinities，该方法返回所有进程而不仅是 Java 进程")]
    List<ProcessAffinityInfo> GetJavaProcessAffinities();

    /// <summary>
    /// 获取指定进程的详细信息
    /// </summary>
    ProcessAffinityInfo? GetProcessInfo(int pid);

    /// <summary>
    /// 杀进程（优雅停止 → 3s 超时 → 强杀）
    /// </summary>
    (bool Success, string? Error) KillProcess(int pid, bool graceful = true);

    /// <summary>
    /// 设置进程 CPU 亲和性
    /// </summary>
    (bool Success, string? Error) SetProcessAffinity(int pid, long affinityMask);
}
