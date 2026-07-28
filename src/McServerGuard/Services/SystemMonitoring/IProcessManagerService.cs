// -----------------------------------------------------------------------------
// 文件名: IProcessManagerService.cs
// 命名空间: McServerGuard.Services.SystemMonitoring
// 功能描述: 进程管理服务接口 —— 提供进程亲和性查询与进程管理操作契约
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.SystemMonitoring;

using McServerGuard.Models;

/// <summary>
/// 进程管理服务接口
/// </summary>
public interface IProcessManagerService
{
    /// <summary>
    /// 获取所有 Java 进程的亲和性信息
    /// </summary>
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
