// -----------------------------------------------------------------------------
// 文件名: CpuIdentifier.cs
// 命名空间: McServerGuard.Services.HardwareInfo
// 功能描述: 提供 CPU 硬件信息识别服务，支持多厂商 CPU 型号解析与性能评分
// 依赖组件: System.Management, System.Runtime.InteropServices
// 设计模式: 单例模式、缓存模式、策略模式（厂商解析分派）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.HardwareInfo;

using System.Management;
using System.Runtime.InteropServices;
using McServerGuard.Models.Hardware;
using Serilog;

/// <summary>
/// CPU 识别服务
/// 通过 WMI 获取 CPU 详细信息，支持 Intel / AMD / ARM 等主流 CPU 厂商
/// 包含型号规范化、代际识别、架构判断与性能评分
/// </summary>
public class CpuIdentifier
{
    /// <summary>
    /// CPU 信息缓存
    /// </summary>
    private CpuInfo? _cachedInfo;

    /// <summary>
    /// 缓存访问锁
    /// </summary>
    private readonly object _cacheLock = new();

    /// <summary>
    /// 当前操作系统是否为 Windows
    /// </summary>
    private static bool IsWindows =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 获取当前系统 CPU 信息
    /// 首次调用时执行识别，后续调用返回缓存结果
    /// </summary>
    /// <returns>CPU 信息对象</returns>
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

    /// <summary>
    /// 内部实现：获取 CPU 信息
    /// Windows 平台通过 WMI 查询，非 Windows 平台返回降级数据
    /// </summary>
    /// <returns>CPU 信息对象</returns>
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

    /// <summary>
    /// 获取降级 CPU 信息
    /// 当无法通过 WMI 获取信息时使用，基于环境变量估算核心数
    /// </summary>
    /// <returns>CPU 信息对象</returns>
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
    /// 规范化 CPU 名称
    /// 移除多余空格、统一商标符号格式
    /// </summary>
    /// <param name="name">原始 CPU 名称</param>
    /// <returns>规范化后的名称</returns>
    private static string NormalizeCpuName(string name)
    {
        var cleaned = name.Replace("(R)", "®").Replace("(TM)", "™").Trim();
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        return cleaned;
    }

    /// <summary>
    /// 解析 CPU 型号，提取架构、代际与市场定位
    /// </summary>
    /// <param name="modelName">CPU 型号名称</param>
    /// <param name="manufacturer">CPU 厂商</param>
    /// <returns>架构名称、代际、市场定位的三元组</returns>
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
    /// 支持 Core i3/i5/i7/i9 第1-15代、Xeon、Pentium、Celeron、Atom 等系列
    /// </summary>
    /// <param name="lowerName">小写型号名称</param>
    /// <param name="fullName">完整型号名称</param>
    /// <returns>架构名称、代际、市场定位的三元组</returns>
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

    /// <summary>
    /// 根据 Intel CPU 代次获取架构名称
    /// </summary>
    /// <param name="generation">CPU 代次</param>
    /// <returns>架构名称</returns>
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
    /// 支持 Ryzen 3/5/7/9 1000-9000 系列、Threadripper、EPYC、Athlon 等系列
    /// </summary>
    /// <param name="lowerName">小写型号名称</param>
    /// <param name="fullName">完整型号名称</param>
    /// <returns>架构名称、代际、市场定位的三元组</returns>
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

    /// <summary>
    /// 根据 AMD CPU 代次获取架构名称
    /// </summary>
    /// <param name="generation">CPU 代次</param>
    /// <returns>架构名称</returns>
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
    /// 计算 CPU 综合性能评分（0-100）
    /// 基于物理核心数、逻辑核心数、主频与市场定位综合计算
    /// </summary>
    /// <param name="physicalCores">物理核心数</param>
    /// <param name="logicalCores">逻辑核心数</param>
    /// <param name="maxClockMHz">最高主频（MHz）</param>
    /// <param name="tier">市场定位</param>
    /// <returns>性能评分（0-100）</returns>
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
    /// 清除 CPU 信息缓存
    /// 强制下一次获取时重新识别
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
            _cachedInfo = null;
    }
}
