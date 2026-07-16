namespace McServerGuard.Services.HardwareInfo;

using System.Management;
using System.Runtime.InteropServices;
using McServerGuard.Models.Hardware;
using Serilog;

/// <summary>
/// CPU 识别服务 —— 通过 WMI 获取 CPU 详细信息
/// 支持 Intel / AMD / ARM 等主流 CPU 厂商
/// 包含型号解析、代际识别、架构判断
/// </summary>
public class CpuIdentifier
{
    private CpuInfo? _cachedInfo;
    private readonly object _cacheLock = new();

    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 获取当前系统 CPU 信息
    /// </summary>
    public CpuInfo GetCpuInfo()
    {
        lock (_cacheLock)
        {
            if (_cachedInfo != null)
                return _cachedInfo;
        }

        var info = GetCpuInfoInternal();

        lock (_cacheLock)
        {
            _cachedInfo = info;
        }

        Log.Information("🖥️ CPU 识别完成: {Model} ({Cores}核{Threads}线程)",
            info.ModelName, info.PhysicalCores, info.LogicalCores);

        return info;
    }

    private CpuInfo GetCpuInfoInternal()
    {
        if (!IsWindows)
            return GetFallbackCpuInfo();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, " +
                "MaxClockSpeed, CurrentClockSpeed, Architecture, Family, Revision " +
                "FROM Win32_Processor");

            using var collection = searcher.Get();
            foreach (var obj in collection)
            {
                var modelName = obj["Name"]?.ToString()?.Trim() ?? "未知 CPU";
                var manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "未知厂商";
                var physicalCores = (uint)(obj["NumberOfCores"] ?? 0);
                var logicalCores = (uint)(obj["NumberOfLogicalProcessors"] ?? 0);
                var maxClockSpeed = (uint)(obj["MaxClockSpeed"] ?? 0);
                var currentClockSpeed = (uint)(obj["CurrentClockSpeed"] ?? 0);

                var normalizedName = NormalizeCpuName(modelName);
                var (arch, generation, tier) = ParseCpuModel(normalizedName, manufacturer);
                var perfScore = CalculatePerformanceScore((int)physicalCores, (int)logicalCores, maxClockSpeed, tier);

                return new CpuInfo
                {
                    ModelName = normalizedName,
                    Manufacturer = manufacturer,
                    Architecture = arch,
                    Generation = generation,
                    PhysicalCores = (int)physicalCores,
                    LogicalCores = (int)logicalCores,
                    BaseClockGHz = Math.Round(maxClockSpeed / 1000.0, 2),
                    BoostClockGHz = Math.Round(currentClockSpeed / 1000.0, 2),
                    Tier = tier,
                    PerformanceScore = perfScore,
                    IsRecognized = arch != "未知架构"
                };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "💥 WMI CPU 识别失败: {Message}", ex.Message);
        }

        return GetFallbackCpuInfo();
    }

    private static CpuInfo GetFallbackCpuInfo()
    {
        var logicalCores = Environment.ProcessorCount;
        var physicalCores = logicalCores / 2;
        if (physicalCores < 1) physicalCores = 1;

        return new CpuInfo
        {
            ModelName = "未知 CPU",
            Manufacturer = "未知",
            Architecture = "未知架构",
            Generation = 0,
            PhysicalCores = physicalCores,
            LogicalCores = logicalCores,
            BaseClockGHz = 0,
            BoostClockGHz = 0,
            Tier = "未知",
            PerformanceScore = 0,
            IsRecognized = false
        };
    }

    /// <summary>
    /// 规范化 CPU 名称 —— 移除多余空格、统一大小写
    /// </summary>
    private static string NormalizeCpuName(string name)
    {
        var cleaned = name.Replace("(R)", "®").Replace("(TM)", "™").Trim();
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        return cleaned;
    }

    /// <summary>
    /// 解析 CPU 型号，提取架构、代际、定位
    /// </summary>
    private static (string Architecture, int Generation, string Tier) ParseCpuModel(string modelName, string manufacturer)
    {
        var lowerName = modelName.ToLowerInvariant();
        var arch = "未知架构";
        var generation = 0;
        var tier = "主流";

        if (manufacturer.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
            lowerName.Contains("intel") || lowerName.Contains("core"))
        {
            (arch, generation, tier) = ParseIntelCpu(lowerName, modelName);
        }
        else if (manufacturer.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                 lowerName.Contains("amd") || lowerName.Contains("ryzen"))
        {
            (arch, generation, tier) = ParseAmdCpu(lowerName, modelName);
        }
        else if (lowerName.Contains("arm") || lowerName.Contains("snapdragon"))
        {
            arch = "ARM";
            tier = "移动";
        }

        return (arch, generation, tier);
    }

    /// <summary>
    /// 解析 Intel CPU 型号
    /// 支持：Core i3/i5/i7/i9 第1-15代、Xeon、Pentium、Celeron、Atom 等
    /// </summary>
    private static (string Architecture, int Generation, string Tier) ParseIntelCpu(string lowerName, string fullName)
    {
        var arch = "Intel 未知架构";
        var generation = 0;
        var tier = "主流";

        if (lowerName.Contains("core i9") || lowerName.Contains("core™ i9"))
            tier = "旗舰";
        else if (lowerName.Contains("core i7") || lowerName.Contains("core™ i7"))
            tier = "高端";
        else if (lowerName.Contains("core i5") || lowerName.Contains("core™ i5"))
            tier = "主流";
        else if (lowerName.Contains("core i3") || lowerName.Contains("core™ i3"))
            tier = "入门";
        else if (lowerName.Contains("xeon"))
            tier = "工作站";
        else if (lowerName.Contains("pentium"))
            tier = "入门";
        else if (lowerName.Contains("celeron"))
            tier = "入门";
        else if (lowerName.Contains("atom"))
            tier = "低功耗";

        for (int gen = 15; gen >= 1; gen--)
        {
            if (lowerName.Contains($"{gen}th gen") ||
                lowerName.Contains($"{gen}th-gen") ||
                lowerName.Contains($"第{gen}代"))
            {
                generation = gen;
                arch = GetIntelArchName(gen);
                break;
            }
        }

        if (generation == 0)
        {
            var match = System.Text.RegularExpressions.Regex.Match(fullName, @"i[3579]-(\d{2,5})");
            if (match.Success && match.Groups[1].Value.Length >= 4)
            {
                var num = match.Groups[1].Value;
                var firstDigit = int.Parse(num[0].ToString());
                if (firstDigit >= 1 && firstDigit <= 9)
                {
                    generation = num.Length >= 5
                        ? int.Parse(num.Substring(0, 2))
                        : firstDigit;
                    arch = GetIntelArchName(generation);
                }
            }
        }

        if (generation == 0)
        {
            if (lowerName.Contains("arrow lake")) { arch = "Arrow Lake"; generation = 15; }
            else if (lowerName.Contains("meteor lake")) { arch = "Meteor Lake"; generation = 14; }
            else if (lowerName.Contains("raptor lake")) { arch = "Raptor Lake"; generation = 13; }
            else if (lowerName.Contains("alder lake")) { arch = "Alder Lake"; generation = 12; }
            else if (lowerName.Contains("tiger lake")) { arch = "Tiger Lake"; generation = 11; }
            else if (lowerName.Contains("ice lake")) { arch = "Ice Lake"; generation = 10; }
            else if (lowerName.Contains("coffee lake")) { arch = "Coffee Lake"; generation = 9; }
            else if (lowerName.Contains("kaby lake")) { arch = "Kaby Lake"; generation = 7; }
            else if (lowerName.Contains("skylake")) { arch = "Skylake"; generation = 6; }
        }

        return (arch, generation, tier);
    }

    private static string GetIntelArchName(int generation)
    {
        return generation switch
        {
            15 => "Arrow Lake",
            14 => "Meteor Lake",
            13 => "Raptor Lake",
            12 => "Alder Lake",
            11 => "Tiger Lake",
            10 => "Ice Lake",
            9 => "Coffee Lake Refresh",
            8 => "Coffee Lake",
            7 => "Kaby Lake",
            6 => "Skylake",
            5 => "Broadwell",
            4 => "Haswell",
            3 => "Ivy Bridge",
            2 => "Sandy Bridge",
            1 => "Nehalem/Westmere",
            _ => "未知 Intel 架构"
        };
    }

    /// <summary>
    /// 解析 AMD CPU 型号
    /// 支持：Ryzen 3/5/7/9 1000-9000系列、Threadripper、EPYC、Athlon 等
    /// </summary>
    private static (string Architecture, int Generation, string Tier) ParseAmdCpu(string lowerName, string fullName)
    {
        var arch = "AMD 未知架构";
        var generation = 0;
        var tier = "主流";

        if (lowerName.Contains("threadripper") || lowerName.Contains("epyc"))
            tier = "工作站";
        else if (lowerName.Contains("ryzen 9"))
            tier = "旗舰";
        else if (lowerName.Contains("ryzen 7"))
            tier = "高端";
        else if (lowerName.Contains("ryzen 5"))
            tier = "主流";
        else if (lowerName.Contains("ryzen 3"))
            tier = "入门";
        else if (lowerName.Contains("athlon"))
            tier = "入门";

        var match = System.Text.RegularExpressions.Regex.Match(fullName, @"Ryzen\s+\d+\s+(\d{4})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var numStr = match.Groups[1].Value;
            var firstDigit = int.Parse(numStr[0].ToString());
            generation = firstDigit;
            arch = GetAmdArchName(generation);
        }

        if (generation == 0)
        {
            if (lowerName.Contains("zen 5") || lowerName.Contains("zen5")) { arch = "Zen 5"; generation = 9; }
            else if (lowerName.Contains("zen 4") || lowerName.Contains("zen4")) { arch = "Zen 4"; generation = 7; }
            else if (lowerName.Contains("zen 3") || lowerName.Contains("zen3")) { arch = "Zen 3"; generation = 5; }
            else if (lowerName.Contains("zen 2") || lowerName.Contains("zen2")) { arch = "Zen 2"; generation = 3; }
            else if (lowerName.Contains("zen+") || lowerName.Contains("zen +")) { arch = "Zen+"; generation = 2; }
            else if (lowerName.Contains("zen")) { arch = "Zen"; generation = 1; }
        }

        return (arch, generation, tier);
    }

    private static string GetAmdArchName(int generation)
    {
        return generation switch
        {
            9 => "Zen 5",
            8 => "Zen 4 Refresh",
            7 => "Zen 4",
            6 => "Zen 3+",
            5 => "Zen 3",
            4 => "Zen 2 Refresh",
            3 => "Zen 2",
            2 => "Zen+",
            1 => "Zen",
            _ => "未知 AMD 架构"
        };
    }

    /// <summary>
    /// 计算综合性能评分（0-100，基于核心数、频率、定位）
    /// </summary>
    private static double CalculatePerformanceScore(int physicalCores, int logicalCores, uint maxClockMHz, string tier)
    {
        if (physicalCores == 0) physicalCores = logicalCores;

        var coreScore = Math.Min(physicalCores * 5.0, 40.0);
        var threadScore = Math.Min((logicalCores - physicalCores) * 2.0, 15.0);
        var clockScore = Math.Min(maxClockMHz / 1000.0 * 5.0, 15.0);

        var tierMultiplier = tier switch
        {
            "旗舰" => 1.3,
            "高端" => 1.15,
            "工作站" => 1.2,
            "主流" => 1.0,
            "入门" => 0.75,
            "低功耗" => 0.5,
            _ => 1.0
        };

        var score = (coreScore + threadScore + clockScore) * tierMultiplier;
        return Math.Round(Math.Min(score, 100.0), 1);
    }

    /// <summary>
    /// 清除缓存，强制重新识别
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
            _cachedInfo = null;
    }
}
