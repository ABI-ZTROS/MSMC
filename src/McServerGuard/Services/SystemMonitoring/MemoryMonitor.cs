// -----------------------------------------------------------------------------
// 文件名: MemoryMonitor.cs
// 命名空间: McServerGuard.Services.SystemMonitoring
// 功能描述: 内存监控器，通过多策略降级方案采集物理内存与系统内存信息
// 依赖组件: System.Diagnostics, System.Management, System.Runtime.InteropServices, Serilog
// 设计模式: 策略模式（多方案降级）、缓存模式（状态缓存优化）、适配器模式
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.SystemMonitoring;

using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using McServerGuard.Models.Hardware;
using Serilog;

/// <summary>
/// 内存监控器
/// </summary>
/// <remarks>
/// <para>采用多级降级策略获取内存指标，确保跨环境兼容性与数据可靠性：</para>
/// <para>主采集方案：kernel32.dll 的 GlobalMemoryStatusEx（带缓存优化）
/// 备用方案 A：PerformanceCounter 性能计数器
/// 备用方案 B：WMI Win32_OperatingSystem
/// 增强能力：通过 WMI Win32_PhysicalMemory 获取内存频率、类型、插槽数等硬件信息</para>
/// <para>注意：ullAvailPhys 包含 Standby 列表，计算结果与任务管理器数据保持一致。</para>
/// </remarks>
public class MemoryMonitor
{
    /// <summary>
    /// 内存系统信息缓存
    /// </summary>
    private MemorySystemInfo? _cachedMemoryInfo;

    /// <summary>
    /// 缓存访问锁对象
    /// </summary>
    private readonly object _cacheLock = new();

    /// <summary>
    /// 可用内存性能计数器
    /// </summary>
    private PerformanceCounter? _availableMemoryCounter;

    /// <summary>
    /// 性能计数器访问锁对象
    /// </summary>
    private readonly object _counterLock = new();

    /// <summary>
    /// 上一次内存状态快照
    /// </summary>
    private MEMORYSTATUSEX _lastMemStatus;

    /// <summary>
    /// 上一次内存状态采集时间戳
    /// </summary>
    private DateTime _lastMemStatusTime = DateTime.MinValue;

    /// <summary>
    /// 内存状态访问锁对象
    /// </summary>
    private readonly object _memStatusLock = new();

    /// <summary>
    /// 内存状态缓存持续时间（500 毫秒）
    /// </summary>
    private static readonly TimeSpan MemStatusCacheDuration = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 获取一个值，指示当前操作系统是否为 Windows 平台
    /// </summary>
    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 获取总物理内存容量（字节）
    /// </summary>
    /// <returns>总物理内存字节数；非 Windows 平台或获取失败返回 0</returns>
    /// <remarks>
    /// 优先使用 GlobalMemoryStatusEx，失败则降级到 WMI Win32_OperatingSystem。
    /// </remarks>
    public long GetTotalPhysicalMemory()
    {
        if (!IsWindows) return 0;

        try
        {
            var memStatus = GetMemoryStatus();
            if (memStatus.ullTotalPhys > 0)
            {
                Log.Debug("GlobalMemoryStatusEx 成功，总内存: {Total} GB",
                    (double)memStatus.ullTotalPhys / (1024 * 1024 * 1024));
                return (long)memStatus.ullTotalPhys;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GlobalMemoryStatusEx 调用失败: {Message}", ex.Message);
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                if (long.TryParse(obj["TotalVisibleMemorySize"]?.ToString(), out var kb))
                {
                    var bytes = kb * 1024;
                    Log.Information("通过 WMI 获取总内存成功: {Total} GB",
                        (double)bytes / (1024 * 1024 * 1024));
                    return bytes;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WMI 获取总内存失败: {Message}", ex.Message);
        }

        Log.Error("所有获取总内存的方案都失败了");
        return 0;
    }

    /// <summary>
    /// 获取已使用的物理内存量（字节）
    /// </summary>
    /// <returns>已使用物理内存字节数</returns>
    /// <remarks>
    /// 计算方式：总内存 - 可用物理内存。
    /// ullAvailPhys 包含 Standby 列表，因此该值与任务管理器的"使用中"一致。
    /// </remarks>
    public long GetUsedMemory()
    {
        if (!IsWindows) return 0;

        var total = GetTotalPhysicalMemory();
        var free = GetAvailableMemory();
        if (total > 0 && free >= 0)
            return Math.Max(0, total - free);

        return 0;
    }

    /// <summary>
    /// 获取可用物理内存量（字节）
    /// </summary>
    /// <returns>可用物理内存字节数；非 Windows 平台或获取失败返回 0</returns>
    /// <remarks>
    /// 优先使用 GlobalMemoryStatusEx，失败则降级到 PerformanceCounter，再降级到 WMI。
    /// </remarks>
    public long GetAvailableMemory()
    {
        if (!IsWindows) return 0;

        try
        {
            var memStatus = GetMemoryStatus();
            if (memStatus.ullTotalPhys > 0)
            {
                return (long)memStatus.ullAvailPhys;
            }
        }
        catch (Exception ex)
        {
            Log.Debug("GlobalMemoryStatusEx 调用失败: {Message}", ex.Message);
        }

        try
        {
            lock (_counterLock)
            {
                _availableMemoryCounter ??= new PerformanceCounter(
                    "Memory", "Available MBytes", true);
                var mb = _availableMemoryCounter.NextValue();
                if (mb > 0)
                {
                    return (long)(mb * 1024 * 1024);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("PerformanceCounter 获取可用内存失败: {Message}", ex.Message);
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                if (long.TryParse(obj["FreePhysicalMemory"]?.ToString(), out var kb))
                {
                    return kb * 1024;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("WMI 获取可用内存失败: {Message}", ex.Message);
        }

        return 0;
    }

    /// <summary>
    /// 获取内存使用率百分比（0-100）
    /// </summary>
    /// <returns>内存使用率百分比；非 Windows 平台或获取失败返回 0</returns>
    /// <remarks>
    /// 优先直接返回 GlobalMemoryStatusEx 的 dwMemoryLoad 字段，失败则手动计算。
    /// </remarks>
    public double GetMemoryUsagePercent()
    {
        if (!IsWindows) return 0;

        try
        {
            var memStatus = GetMemoryStatus();
            if (memStatus.ullTotalPhys > 0)
            {
                return memStatus.dwMemoryLoad;
            }
        }
        catch (Exception ex)
        {
            Log.Debug("GlobalMemoryStatusEx 获取使用率失败: {Message}", ex.Message);
        }

        var total = GetTotalPhysicalMemory();
        var free = GetAvailableMemory();
        if (total > 0 && free >= 0)
        {
            return Math.Round((double)(total - free) / total * 100, 2);
        }

        return 0;
    }

    /// <summary>
    /// 获取提交内存使用量（字节）
    /// </summary>
    /// <returns>提交内存使用字节数；对应任务管理器的"提交大小"</returns>
    public long GetCommitChargeUsed()
    {
        if (!IsWindows) return 0;

        try
        {
            var memStatus = GetMemoryStatus();
            return (long)(memStatus.ullTotalPageFile - memStatus.ullAvailPageFile);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 获取提交内存总量（字节）
    /// </summary>
    /// <returns>提交内存总字节数</returns>
    public long GetCommitChargeTotal()
    {
        if (!IsWindows) return 0;

        try
        {
            var memStatus = GetMemoryStatus();
            return (long)memStatus.ullTotalPageFile;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 获取内存状态（带缓存，避免频繁 P/Invoke 调用）
    /// </summary>
    /// <returns>MEMORYSTATUSEX 结构实例</returns>
    private MEMORYSTATUSEX GetMemoryStatus()
    {
        lock (_memStatusLock)
        {
            if ((DateTime.Now - _lastMemStatusTime) < MemStatusCacheDuration
                && _lastMemStatus.ullTotalPhys > 0)
            {
                return _lastMemStatus;
            }

            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                _lastMemStatus = memStatus;
                _lastMemStatusTime = DateTime.Now;
            }

            return memStatus;
        }
    }

    /// <summary>
    /// 获取内存系统详细信息（频率、类型、插槽数等硬件参数）
    /// </summary>
    /// <returns>内存系统信息对象</returns>
    /// <remarks>
    /// 通过 WMI Win32_PhysicalMemory 查询硬件级内存信息，结果带有缓存。
    /// </remarks>
    public MemorySystemInfo GetMemorySystemInfo()
    {
        lock (_cacheLock)
        {
            if (_cachedMemoryInfo != null)
                return _cachedMemoryInfo;
        }

        var info = GetMemorySystemInfoInternal();

        lock (_cacheLock)
        {
            _cachedMemoryInfo = info;
        }

        Log.Information("内存识别完成: {Total} GB, {Speed} MHz, {Type}, {Modules} 条",
            Math.Round(info.TotalCapacityBytes / 1024.0 / 1024 / 1024, 1),
            info.SpeedMHz, info.MemoryType, info.ModuleCount);

        return info;
    }

    /// <summary>
    /// 内部实现：获取内存系统详细信息
    /// </summary>
    /// <returns>内存系统信息对象</returns>
    private MemorySystemInfo GetMemorySystemInfoInternal()
    {
        if (!IsWindows)
            return new MemorySystemInfo
            {
                TotalCapacityBytes = GetTotalPhysicalMemory(),
                ModuleCount = 0,
                SpeedMHz = 0,
                MemoryType = "未知",
                IsDualChannel = false,
                Modules = []
            };

        try
        {
            var modules = new List<MemoryModuleInfo>();
            long totalCapacity = 0;
            int maxSpeed = 0;
            string memoryType = "未知";

            using var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, Speed, MemoryType, Manufacturer, PartNumber, DeviceLocator " +
                "FROM Win32_PhysicalMemory");

            using var collection = searcher.Get();
            int slot = 0;
            foreach (var obj in collection)
            {
                var capacity = (ulong)(obj["Capacity"] ?? 0UL);
                var speed = (uint)(obj["Speed"] ?? 0U);
                var memType = (ushort)(obj["MemoryType"] ?? 0);
                var manufacturer = obj["Manufacturer"]?.ToString() ?? "未知";
                var partNumber = obj["PartNumber"]?.ToString()?.Trim() ?? string.Empty;
                var deviceLocator = obj["DeviceLocator"]?.ToString() ?? string.Empty;

                var typeName = GetMemoryTypeName(memType);
                if (typeName != "未知" && memoryType == "未知")
                    memoryType = typeName;

                if (speed > maxSpeed)
                    maxSpeed = (int)speed;

                totalCapacity += (long)capacity;
                slot++;

                modules.Add(new MemoryModuleInfo
                {
                    CapacityBytes = (long)capacity,
                    SpeedMHz = (int)speed,
                    MemoryType = typeName,
                    Manufacturer = manufacturer,
                    PartNumber = partNumber,
                    SlotNumber = slot
                });
            }

            if (totalCapacity == 0)
                totalCapacity = GetTotalPhysicalMemory();

            var isDualChannel = modules.Count >= 2;

            return new MemorySystemInfo
            {
                TotalCapacityBytes = totalCapacity,
                ModuleCount = modules.Count,
                SpeedMHz = maxSpeed,
                MemoryType = memoryType,
                IsDualChannel = isDualChannel,
                Modules = modules
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WMI 内存识别失败: {Message}", ex.Message);
            return new MemorySystemInfo
            {
                TotalCapacityBytes = GetTotalPhysicalMemory(),
                ModuleCount = 0,
                SpeedMHz = 0,
                MemoryType = "未知",
                IsDualChannel = false,
                Modules = []
            };
        }
    }

    /// <summary>
    /// 根据 MemoryType 枚举值获取内存类型名称
    /// </summary>
    /// <param name="memoryType">Win32_PhysicalMemory MemoryType 枚举值</param>
    /// <returns>内存类型名称字符串</returns>
    /// <remarks>
    /// 参考 Win32_PhysicalMemory MemoryType 枚举定义。
    /// </remarks>
    private static string GetMemoryTypeName(ushort memoryType)
    {
        return memoryType switch
        {
            0 => "未知",
            1 => "其他",
            2 => "DRAM",
            3 => "同步 DRAM",
            4 => "缓存 DRAM",
            5 => "EDO",
            6 => "EDRAM",
            7 => "VRAM",
            8 => "SRAM",
            9 => "RAM",
            10 => "ROM",
            11 => "Flash",
            12 => "EEPROM",
            13 => "FEPROM",
            14 => "EPROM",
            15 => "CDRAM",
            16 => "3DRAM",
            17 => "SDRAM",
            18 => "SGRAM",
            19 => "RDRAM",
            20 => "DDR",
            21 => "DDR2",
            22 => "DDR2 FB-DIMM",
            24 => "DDR3",
            25 => "FBD2",
            26 => "DDR4",
            27 => "LPDDR",
            28 => "LPDDR2",
            29 => "LPDDR3",
            30 => "LPDDR4",
            31 => "Logical non-volatile device",
            32 => "HBM",
            33 => "HBM2",
            34 => "DDR5",
            35 => "LPDDR5",
            _ => "未知"
        };
    }

    #region P/Invoke 声明

    /// <summary>
    /// MEMORYSTATUSEX 结构体 —— Windows 内存状态信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MEMORYSTATUSEX
    {
        /// <summary>结构体大小</summary>
        public uint dwLength;
        /// <summary>内存使用率百分比</summary>
        public uint dwMemoryLoad;
        /// <summary>总物理内存（字节）</summary>
        public ulong ullTotalPhys;
        /// <summary>可用物理内存（字节）</summary>
        public ulong ullAvailPhys;
        /// <summary>总虚拟内存（字节）</summary>
        public ulong ullTotalVirtual;
        /// <summary>可用虚拟内存（字节）</summary>
        public ulong ullAvailVirtual;
        /// <summary>总页面文件（字节）</summary>
        public ulong ullTotalPageFile;
        /// <summary>可用页面文件（字节）</summary>
        public ulong ullAvailPageFile;
    }

    /// <summary>
    /// kernel32.dll 的 GlobalMemoryStatusEx 函数
    /// </summary>
    /// <param name="lpBuffer">MEMORYSTATUSEX 结构引用</param>
    /// <returns>调用成功返回 true</returns>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    #endregion
}
