// 🧠 内存监控器 —— 使用多种方式获取内存数据，确保兼容性和可靠性
namespace McServerGuard.Services.SystemMonitoring;

using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using McServerGuard.Models.Hardware;
using Serilog;

/// <summary>
/// 内存监控器
/// 主方案：kernel32.dll 的 GlobalMemoryStatusEx
/// 备用方案A：PerformanceCounter
/// 备用方案B：WMI Win32_OperatingSystem
/// 增强：通过 WMI Win32_PhysicalMemory 获取内存频率、类型、插槽数
/// </summary>
public class MemoryMonitor
{
    private MemorySystemInfo? _cachedMemoryInfo;
    private readonly object _cacheLock = new();
    private PerformanceCounter? _availableMemoryCounter;
    private PerformanceCounter? _committedBytesCounter;
    private readonly object _counterLock = new();

    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 获取总物理内存（字节）
    /// 优先使用 GlobalMemoryStatusEx，失败则降级到 WMI
    /// </summary>
    public long GetTotalPhysicalMemory()
    {
        if (!IsWindows) return 0;

        // 方案1：GlobalMemoryStatusEx
        try
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                Log.Debug("💾 GlobalMemoryStatusEx 成功，总内存: {Total} GB",
                    (double)memStatus.ullTotalPhys / (1024 * 1024 * 1024));
                return (long)memStatus.ullTotalPhys;
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                Log.Warning("💾 GlobalMemoryStatusEx 返回失败，错误码: {ErrorCode}", error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💾 GlobalMemoryStatusEx 调用失败: {Message}", ex.Message);
        }

        // 方案2：WMI
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
                    Log.Information("💾 通过 WMI 获取总内存成功: {Total} GB",
                        (double)bytes / (1024 * 1024 * 1024));
                    return bytes;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💾 WMI 获取总内存失败: {Message}", ex.Message);
        }

        Log.Error("💾 所有获取总内存的方案都失败了");
        return 0;
    }

    /// <summary>
    /// 获取已使用的物理内存（字节）
    /// </summary>
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
    /// 获取可用内存（字节）
    /// 优先使用 GlobalMemoryStatusEx，失败则降级
    /// </summary>
    public long GetAvailableMemory()
    {
        if (!IsWindows) return 0;

        // 方案1：GlobalMemoryStatusEx
        try
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (long)memStatus.ullAvailPhys;
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                Log.Debug("💾 GlobalMemoryStatusEx 返回失败，错误码: {ErrorCode}", error);
            }
        }
        catch (Exception ex)
        {
            Log.Debug("💾 GlobalMemoryStatusEx 调用失败: {Message}", ex.Message);
        }

        // 方案2：PerformanceCounter
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
            Log.Debug("💾 PerformanceCounter 获取可用内存失败: {Message}", ex.Message);
        }

        // 方案3：WMI
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
            Log.Debug("💾 WMI 获取可用内存失败: {Message}", ex.Message);
        }

        return 0;
    }

    /// <summary>
    /// 获取内存使用率百分比 (0-100)
    /// </summary>
    public double GetMemoryUsagePercent()
    {
        if (!IsWindows) return 0;

        // 方案1：GlobalMemoryStatusEx（直接返回 dwMemoryLoad）
        try
        {
            var memStatus = new MEMORYSTATUSEX();
            memStatus.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return memStatus.dwMemoryLoad;
            }
        }
        catch (Exception ex)
        {
            Log.Debug("💾 GlobalMemoryStatusEx 获取使用率失败: {Message}", ex.Message);
        }

        // 方案2：手动计算
        var total = GetTotalPhysicalMemory();
        var free = GetAvailableMemory();
        if (total > 0 && free >= 0)
        {
            return Math.Round((double)(total - free) / total * 100, 2);
        }

        return 0;
    }

    /// <summary>
    /// 获取内存系统详细信息（频率、类型、插槽数等）
    /// 通过 WMI Win32_PhysicalMemory 获取
    /// </summary>
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

        Log.Information("💾 内存识别完成: {Total} GB, {Speed} MHz, {Type}, {Modules} 条",
            Math.Round(info.TotalCapacityBytes / 1024.0 / 1024 / 1024, 1),
            info.SpeedMHz, info.MemoryType, info.ModuleCount);

        return info;
    }

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
            Log.Error(ex, "💥 WMI 内存识别失败: {Message}", ex.Message);
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
    /// 根据 MemoryType 数值获取类型名称
    /// 参考 Win32_PhysicalMemory MemoryType 枚举
    /// </summary>
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    #endregion
}
