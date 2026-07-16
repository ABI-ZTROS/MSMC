// 🧠 内存监控器 —— 使用 Windows kernel32.dll 的 GlobalMemoryStatusEx 来获取精确内存数据
namespace McServerGuard.Services.SystemMonitoring;

using System.Management;
using System.Runtime.InteropServices;
using McServerGuard.Models.Hardware;
using Serilog;

/// <summary>
/// 内存监控器 —— 使用 kernel32.dll 的 GlobalMemoryStatusEx 获取物理内存信息
/// 增强：通过 WMI Win32_PhysicalMemory 获取内存频率、类型、插槽数
/// </summary>
public class MemoryMonitor
{
    private MemorySystemInfo? _cachedMemoryInfo;
    private readonly object _cacheLock = new();

    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 获取总物理内存（字节）
    /// </summary>
    public long GetTotalPhysicalMemory()
    {
        if (!IsWindows) return 0;

        try
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref memStatus))
                return (long)memStatus.ullTotalPhys;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "内存监控失败: {Message}", ex.Message);
        }

        return 0;
    }

    /// <summary>
    /// 获取已使用的物理内存（字节）
    /// </summary>
    public long GetUsedMemory()
    {
        if (!IsWindows) return 0;

        try
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref memStatus))
                return (long)(memStatus.ullTotalPhys - memStatus.ullAvailPhys);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "内存监控失败: {Message}", ex.Message);
        }

        return 0;
    }

    /// <summary>
    /// 获取内存使用率百分比 (0-100)
    /// </summary>
    public double GetMemoryUsagePercent()
    {
        if (!IsWindows) return 0;

        try
        {
            var memStatus = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ref memStatus))
                return memStatus.dwMemoryLoad;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "内存监控失败: {Message}", ex.Message);
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

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    #endregion
}
