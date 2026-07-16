// 💾 磁盘空间监控器 —— 关注你硬盘的剩余空间
// 磁盘满了可比服务器崩溃可怕多了 —— 至少服务器还能重启 💀
namespace McServerGuard.Services.SystemMonitoring;

using System.IO;
using Serilog;

/// <summary>
/// 磁盘信息 record —— 磁盘使用情况的快照
/// </summary>
/// <param name="DriveName">盘符/挂载点名称</param>
/// <param name="TotalBytes">总空间（字节）</param>
/// <param name="FreeBytes">剩余空间（字节）</param>
/// <param name="UsedBytes">已用空间（字节）</param>
/// <param name="UsagePercent">使用率百分比</param>
public record DiskInfo(
    string DriveName,
    long TotalBytes,
    long FreeBytes,
    long UsedBytes,
    double UsagePercent
);

/// <summary>
/// 磁盘空间监控器 —— 查看指定盘符的存储空间使用情况
/// </summary>
public class DiskSpaceMonitor
{
    /// <summary>
    /// 获取指定磁盘/挂载点的空间信息
    /// 返回一个 DiskInfo record，包含总空间、已用空间、剩余空间和使用率
    /// </summary>
    /// <param name="driveRoot">磁盘根路径，如 "C:\" 或 "/"</param>
    /// <returns>磁盘信息</returns>
    public DiskInfo GetDiskInfo(string driveRoot)
    {
        // 日志：获取磁盘信息入口
        Log.Debug("📀 获取磁盘信息: {Drive}", driveRoot);

        try
        {
            // DriveInfo 是 .NET 内置的，跨平台兼容，不用怕
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

            // 日志：磁盘信息结果
            Log.Debug("📊 磁盘 {Name}: {Used}/{Total} ({Pct}%)",
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
            // 日志：获取磁盘信息失败
            Log.Error(ex, "💥 fuck: 获取磁盘信息失败 {Drive}: {Message}", driveRoot, ex.Message);
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
