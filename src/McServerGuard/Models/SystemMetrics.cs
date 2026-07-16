using CommunityToolkit.Mvvm.ComponentModel;

namespace McServerGuard.Models;

/// <summary>📊 系统性能指标快照 —— 每隔几秒给服务器拍一张"体检照"</summary>
public partial class SystemMetrics : ObservableObject
{
    // 🖥️ 系统级指标
    [ObservableProperty] private double _cpuUsagePercent;
    [ObservableProperty] private long _totalMemoryBytes;
    [ObservableProperty] private long _usedMemoryBytes;
    [ObservableProperty] private double _memoryUsagePercent;
    [ObservableProperty] private int _totalThreadCount;
    [ObservableProperty] private int _memorySpeedMHz;
    [ObservableProperty] private string _memoryType = string.Empty;
    [ObservableProperty] private int _memoryModuleCount;

    // 💾 磁盘指标（别让你的服务器把硬盘撑爆了）
    [ObservableProperty] private long _diskTotalBytes;
    [ObservableProperty] private long _diskUsedBytes;
    [ObservableProperty] private long _diskFreeBytes;
    [ObservableProperty] private double _diskUsagePercent;
    [ObservableProperty] private string _diskName = string.Empty;

    // ☕ Java 进程级指标 —— 真正的"嫌疑犯"数据
    [ObservableProperty] private double _javaCpuUsagePercent;
    [ObservableProperty] private long _javaWorkingSetBytes;
    [ObservableProperty] private long _javaPrivateBytes;
    [ObservableProperty] private int _javaThreadCount;
    [ObservableProperty] private int _javaHandleCount;
    [ObservableProperty] private long _javaHeapUsedBytes;
    [ObservableProperty] private long _javaHeapMaxBytes;

    public DateTime Timestamp { get; init; } = DateTime.Now;
}
