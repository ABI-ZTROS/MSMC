// -----------------------------------------------------------------------------
// 文件名: DiskSpaceMonitor.cs
// 命名空间: McServerGuard.Services.SystemMonitoring
// 功能描述: 磁盘空间监控器，采集指定驱动器的存储空间使用情况
// 依赖组件: System.IO, Serilog
// 设计模式: 快照模式、单一职责原则
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.SystemMonitoring;

using System.IO;
using Serilog;

/// <summary>
/// 磁盘信息记录 —— 磁盘存储空间使用情况的瞬时快照
/// </summary>
/// <param name="DriveName">盘符或挂载点名称</param>
/// <param name="TotalBytes">总空间容量（字节）</param>
/// <param name="FreeBytes">剩余可用空间（字节）</param>
/// <param name="UsedBytes">已使用空间（字节）</param>
/// <param name="UsagePercent">空间使用率百分比</param>
public record DiskInfo(
    string DriveName,
    long TotalBytes,
    long FreeBytes,
    long UsedBytes,
    double UsagePercent
);

/// <summary>
/// 磁盘空间监控器
/// </summary>
/// <remarks>
/// 基于 .NET 内置的 <see cref="DriveInfo"/> 类实现，具备跨平台兼容性。
/// 提供指定驱动器的总空间、已用空间、剩余空间及使用率等指标。
/// </remarks>
public class DiskSpaceMonitor
{
    /// <summary>
    /// 获取指定磁盘或挂载点的空间信息
    /// </summary>
    /// <param name="driveRoot">磁盘根路径，如 "C:\" 或 "/"</param>
    /// <returns>磁盘信息快照；驱动器未就绪或获取失败时返回零值对象</returns>
    public DiskInfo GetDiskInfo(string driveRoot)
    {
        Log.Debug("获取磁盘信息: {Drive}", driveRoot);

        try
        {
            var drive = new DriveInfo(driveRoot);

            if (!drive.IsReady)
            {
                Log.Warning("磁盘 {Drive} 未就绪（可能是光驱或未挂载的设备）", driveRoot);
                return new DiskInfo(
                    DriveName: driveRoot,
                    TotalBytes: 0,
                    FreeBytes: 0,
                    UsedBytes: 0,
                    UsagePercent: 0
                );
            }

            var totalBytes = drive.TotalSize;
            var freeBytes = drive.AvailableFreeSpace;
            var usedBytes = totalBytes > freeBytes ? totalBytes - freeBytes : 0;
            var usagePercent = totalBytes > 0
                ? Math.Round((double)usedBytes / totalBytes * 100, 2)
                : 0;

            Log.Debug("磁盘 {Name}: {Used}/{Total} ({Pct}%)",
                driveRoot, usedBytes, totalBytes, usagePercent);

            return new DiskInfo(
                DriveName: driveRoot,
                TotalBytes: totalBytes,
                FreeBytes: freeBytes,
                UsedBytes: usedBytes,
                UsagePercent: usagePercent
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取磁盘信息失败 {Drive}: {Message}", driveRoot, ex.Message);
            return new DiskInfo(
                DriveName: driveRoot,
                TotalBytes: 0,
                FreeBytes: 0,
                UsedBytes: 0,
                UsagePercent: 0
            );
        }
    }
}
