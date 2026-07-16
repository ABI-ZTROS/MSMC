namespace McServerGuard.Models.Hardware;

/// <summary>
/// CPU 详细信息
/// </summary>
public record CpuInfo
{
    public string ModelName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public int Generation { get; init; }
    public int PhysicalCores { get; init; }
    public int LogicalCores { get; init; }
    public double BaseClockGHz { get; init; }
    public double BoostClockGHz { get; init; }
    public int? CinebenchR23Single { get; init; }
    public int? CinebenchR23Multi { get; init; }
    public DateTime? ReleaseDate { get; init; }
    public string Tier { get; init; } = "未知";
    public string Socket { get; init; } = string.Empty;
    public double PerformanceScore { get; init; }
    public bool IsRecognized { get; init; }
}

/// <summary>
/// 内存模块信息
/// </summary>
public record MemoryModuleInfo
{
    public long CapacityBytes { get; init; }
    public int SpeedMHz { get; init; }
    public string MemoryType { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string PartNumber { get; init; } = string.Empty;
    public int SlotNumber { get; init; }
}

/// <summary>
/// 系统内存总览信息
/// </summary>
public record MemorySystemInfo
{
    public long TotalCapacityBytes { get; init; }
    public int ModuleCount { get; init; }
    public int SpeedMHz { get; init; }
    public string MemoryType { get; init; } = string.Empty;
    public bool IsDualChannel { get; init; }
    public List<MemoryModuleInfo> Modules { get; init; } = [];
}
