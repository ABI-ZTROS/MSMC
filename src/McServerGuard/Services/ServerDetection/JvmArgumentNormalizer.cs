// -----------------------------------------------------------------------------
// 文件名: JvmArgumentNormalizer.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: JVM 参数规范化器，实现参数去重、空值过滤、冲突消解与排序
// 依赖组件: McServerGuard.Constants, Serilog
// 设计模式: 管道模式、规范化模式、冲突消解策略
// -----------------------------------------------------------------------------
using McServerGuard.Constants;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>
/// JVM 参数规范化器
/// </summary>
/// <remarks>
/// <para>对用户输入的 JVM 参数进行全流程规范化处理，确保启动命令
/// 干净、合法、有序。处理流水线包含四个核心阶段：
/// 空值过滤 → 去重 → 冲突消解 → 排序。</para>
/// <para>核心能力：
///   - 空值参数过滤（如 -XX:MaxGCPauseMillis= 等无值参数）
///   - 重复参数去重（保留最后一个，与 JVM 实际行为一致）
///   - 互斥参数冲突消解（如 GC 回收器多选一）
///   - 实验性参数自动解锁（UnlockExperimentalVMOptions 注入）
///   - 按 JVM 惯例排序（解锁 → 内存 → GC → 性能 → 系统属性 → 其他）
/// </para>
/// </remarks>
public static class JvmArgumentNormalizer
{
    /// <summary>
    /// 互斥参数组集合 —— 同一组内的参数不能同时生效
    /// </summary>
    /// <remarks>
    /// 当前包含 GC 回收器互斥组：G1、ZGC、Shenandoah、Parallel、Serial、CMS
    /// 六者择一，保留最后出现的参数。
    /// </remarks>
    private static readonly string[][] MutexGroups =
    [
        [
            "-XX:+UseG1GC",
            "-XX:+UseZGC",
            "-XX:+UseShenandoahGC",
            "-XX:+UseParallelGC",
            "-XX:+UseSerialGC",
            "-XX:+UseConcMarkSweepGC"
        ]
    ];

    /// <summary>
    /// 规范化结果对象 —— 包含处理后的参数与各类警告信息
    /// </summary>
    public class NormalizationResult
    {
        /// <summary>规范化后的参数列表</summary>
        public List<string> Arguments { get; set; } = [];
        /// <summary>警告信息集合</summary>
        public List<string> Warnings { get; set; } = [];
        /// <summary>被移除的空值参数列表</summary>
        public List<string> RemovedEmpty { get; set; } = [];
        /// <summary>被移除的重复参数列表</summary>
        public List<string> RemovedDuplicates { get; set; } = [];
        /// <summary>被消解的冲突参数对列表</summary>
        public List<string> ResolvedConflicts { get; set; } = [];
    }

    /// <summary>
    /// 执行全流程参数规范化
    /// </summary>
    /// <param name="arguments">原始参数列表</param>
    /// <returns>规范化结果对象，包含处理后参数与警告信息</returns>
    /// <remarks>
    /// 处理流水线：空值过滤 → 去重 → 冲突消解 → 实验性解锁 → 排序
    /// </remarks>
    public static NormalizationResult Normalize(List<string> arguments)
    {
        Log.Debug("开始规范化 JVM 参数，原始数量: {Count}", arguments.Count);

        var result = new NormalizationResult();
        var args = new List<string>(arguments);

        var (filtered, emptyRemoved) = FilterEmptyValueArguments(args);
        args = filtered;
        result.RemovedEmpty = emptyRemoved;
        foreach (var empty in emptyRemoved)
        {
            result.Warnings.Add($"移除空值参数: {empty}（没有设置值，已自动移除）");
            Log.Warning("移除空值参数: {Arg}", empty);
        }

        var (deduplicated, duplicatesRemoved) = DeduplicateArguments(args);
        args = deduplicated;
        result.RemovedDuplicates = duplicatesRemoved;
        foreach (var dup in duplicatesRemoved)
        {
            result.Warnings.Add($"移除重复参数: {dup}（重复出现，已保留最后一个有效值）");
            Log.Debug("移除重复参数: {Arg}", dup);
        }

        var (conflictResolved, conflicts) = ResolveMutexConflicts(args);
        args = conflictResolved;
        result.ResolvedConflicts = conflicts;
        foreach (var conflict in conflicts)
        {
            result.Warnings.Add($"参数冲突: {conflict}（已自动保留最后一个出现的）");
            Log.Warning("参数冲突: {Info}", conflict);
        }

        var (withUnlock, unlockWarning) = EnsureExperimentalUnlock(args);
        args = withUnlock;
        if (unlockWarning != null)
        {
            result.Warnings.Add(unlockWarning);
            Log.Information("自动注入: {Info}", unlockWarning);
        }

        args = SortArguments(args);

        result.Arguments = args;

        Log.Information("JVM 参数规范化完成: {Original} → {Final} 个参数, {WarningCount} 条警告",
            arguments.Count, args.Count, result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// 过滤空值参数
    /// </summary>
    /// <param name="args">原始参数列表</param>
    /// <returns>过滤后的参数列表与被移除的参数列表</returns>
    private static (List<string> Filtered, List<string> Removed) FilterEmptyValueArguments(List<string> args)
    {
        var filtered = new List<string>();
        var removed = new List<string>();

        foreach (var arg in args)
        {
            if (IsEmptyValueArgument(arg))
            {
                removed.Add(arg);
            }
            else
            {
                filtered.Add(arg);
            }
        }

        return (filtered, removed);
    }

    /// <summary>
    /// 判断参数是否为空值参数
    /// </summary>
    /// <param name="arg">待判断的参数字符串</param>
    /// <returns>为空值参数返回 true</returns>
    /// <remarks>
    /// 空值参数的判定条件：
    ///   - 参数以 = 结尾（值部分为空）
    ///   - -Xms / -Xmx 等内存参数仅含标志而无值
    /// </remarks>
    private static bool IsEmptyValueArgument(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return true;

        var trimmed = arg.Trim();

        if (trimmed.EndsWith('='))
            return true;

        if ((trimmed.Equals("-Xms", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Equals("-Xmx", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    /// <summary>
    /// 去除重复参数，保留最后一次出现的值
    /// </summary>
    /// <param name="args">参数列表</param>
    /// <returns>去重后的参数列表与被移除的参数列表</returns>
    /// <remarks>
    /// 保留最后一个出现的参数，与 JVM 的实际参数覆盖行为一致
    /// （后面的参数会覆盖前面的同名参数）。
    /// </remarks>
    private static (List<string> Deduplicated, List<string> Removed) DeduplicateArguments(List<string> args)
    {
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        var removed = new List<string>();

        foreach (var arg in args)
        {
            var key = GetArgumentKey(arg);

            if (seen.ContainsKey(key))
            {
                removed.Add(arg);
                order.Remove(seen[key]);
            }

            seen[key] = arg;
            order.Add(arg);
        }

        return (order, removed);
    }

    /// <summary>
    /// 获取参数的"键"标识，用于判断参数是否为同一配置项
    /// </summary>
    /// <param name="arg">参数字符串</param>
    /// <returns>参数键字符串</returns>
    private static string GetArgumentKey(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return arg;

        var trimmed = arg.Trim();

        if (trimmed.StartsWith("-XX:+", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-XX:-", StringComparison.OrdinalIgnoreCase))
        {
            var nameStart = 5;
            var eqIndex = trimmed.IndexOf('=', nameStart);
            return eqIndex >= 0
                ? trimmed[..eqIndex]
                : trimmed;
        }

        if (trimmed.StartsWith("-XX:", StringComparison.OrdinalIgnoreCase))
        {
            var eqIndex = trimmed.IndexOf('=');
            return eqIndex >= 0
                ? trimmed[..eqIndex]
                : trimmed;
        }

        if (trimmed.StartsWith("-D", StringComparison.OrdinalIgnoreCase))
        {
            var eqIndex = trimmed.IndexOf('=');
            return eqIndex >= 0
                ? "-D" + trimmed[2..eqIndex]
                : trimmed;
        }

        if (trimmed.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xss", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..4];
        }

        if (trimmed.StartsWith("-Xmn", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..4];
        }

        return trimmed;
    }

    /// <summary>
    /// 解决互斥参数冲突，同一互斥组内保留最后出现的参数
    /// </summary>
    /// <param name="args">参数列表</param>
    /// <returns>消解冲突后的参数列表与冲突信息列表</returns>
    private static (List<string> Resolved, List<string> Conflicts) ResolveMutexConflicts(List<string> args)
    {
        var resolved = new List<string>(args);
        var conflicts = new List<string>();

        foreach (var group in MutexGroups)
        {
            var foundInGroup = new List<string>();

            foreach (var arg in resolved)
            {
                var argKey = GetArgumentKey(arg);
                foreach (var mutexArg in group)
                {
                    var mutexKey = GetArgumentKey(mutexArg);
                    if (argKey.Equals(mutexKey, StringComparison.OrdinalIgnoreCase))
                    {
                        foundInGroup.Add(arg);
                        break;
                    }
                }
            }

            if (foundInGroup.Count > 1)
            {
                var keep = foundInGroup[^1];
                for (int i = 0; i < foundInGroup.Count - 1; i++)
                {
                    conflicts.Add($"{foundInGroup[i]} ↔ {keep}");
                    resolved.Remove(foundInGroup[i]);
                }
            }
        }

        return (resolved, conflicts);
    }

    /// <summary>
    /// 确保实验性参数前置解锁开关存在
    /// </summary>
    /// <param name="args">参数列表</param>
    /// <returns>处理后的参数列表与警告信息（若有注入）</returns>
    /// <remarks>
    /// G1NewSizePercent、G1MaxNewSizePercent、UseCompactObjectHeaders 等参数
    /// 属于 JVM 实验性参数，要求 -XX:+UnlockExperimentalVMOptions 必须
    /// 出现在它们之前。若用户未手动配置，则自动注入该开关。
    /// </remarks>
    private static (List<string> Args, string? Warning) EnsureExperimentalUnlock(List<string> args)
    {
        var experimentalFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "G1NewSizePercent",
            "G1MaxNewSizePercent",
            "UseCompactObjectHeaders"
        };

        var hasExperimentalArg = false;
        var hasUnlockFlag = false;

        foreach (var arg in args)
        {
            var trimmed = arg.Trim();

            if (trimmed.Contains("UnlockExperimentalVMOptions", StringComparison.OrdinalIgnoreCase))
            {
                hasUnlockFlag = true;
            }

            foreach (var flag in experimentalFlags)
            {
                if (trimmed.Contains(flag, StringComparison.OrdinalIgnoreCase))
                {
                    hasExperimentalArg = true;
                    break;
                }
            }
        }

        if (hasExperimentalArg && !hasUnlockFlag)
        {
            var result = new List<string>(args)
            {
                "-XX:+UnlockExperimentalVMOptions"
            };
            return (result, "自动注入 -XX:+UnlockExperimentalVMOptions（检测到实验性参数，JVM 要求此开关在前）");
        }

        return (args, null);
    }

    /// <summary>
    /// 按 JVM 惯例对参数进行分类排序
    /// </summary>
    /// <param name="args">参数列表</param>
    /// <returns>排序后的参数列表</returns>
    /// <remarks>
    /// 排序顺序：解锁开关 → 内存参数 → GC 参数 → 性能参数 → 系统属性 → 其他
    /// </remarks>
    private static List<string> SortArguments(List<string> args)
    {
        var unlockArgs = new List<string>();
        var memoryArgs = new List<string>();
        var gcArgs = new List<string>();
        var performanceArgs = new List<string>();
        var systemProps = new List<string>();
        var otherArgs = new List<string>();

        foreach (var arg in args)
        {
            var trimmed = arg.Trim();

            if (trimmed.Contains("UnlockExperimentalVMOptions", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("UnlockDiagnosticVMOptions", StringComparison.OrdinalIgnoreCase))
            {
                unlockArgs.Add(arg);
            }
            else if (trimmed.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("-Xmn", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("-Xss", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("MetaspaceSize", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("MaxMetaspaceSize", StringComparison.OrdinalIgnoreCase))
            {
                memoryArgs.Add(arg);
            }
            else if (trimmed.StartsWith("-XX:+Use", StringComparison.OrdinalIgnoreCase) &&
                     trimmed.Contains("GC", StringComparison.OrdinalIgnoreCase))
            {
                gcArgs.Add(arg);
            }
            else if (trimmed.Contains("G1", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("GC", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("SurvivorRatio", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("MaxTenuringThreshold", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("InitiatingHeapOccupancyPercent", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("ParallelGCThreads", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("ConcGCThreads", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("MaxGCPauseMillis", StringComparison.OrdinalIgnoreCase))
            {
                gcArgs.Add(arg);
            }
            else if (trimmed.StartsWith("-D", StringComparison.OrdinalIgnoreCase))
            {
                systemProps.Add(arg);
            }
            else if (trimmed.Contains("PreTouch", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("DisableExplicitGC", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("StringDeduplication", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("UseNUMA", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("LargePages", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("TLAB", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("FastAccessor", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("OptimizeStringConcat", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("PerfDisableSharedMem", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("CompactObject", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Equals("-server", StringComparison.OrdinalIgnoreCase))
            {
                performanceArgs.Add(arg);
            }
            else
            {
                otherArgs.Add(arg);
            }
        }

        var result = new List<string>();
        result.AddRange(unlockArgs);
        result.AddRange(memoryArgs);
        result.AddRange(gcArgs);
        result.AddRange(performanceArgs);
        result.AddRange(systemProps);
        result.AddRange(otherArgs);

        return result;
    }

    /// <summary>
    /// 验证单个 JVM 参数的格式合法性
    /// </summary>
    /// <param name="arg">待验证的参数字符串</param>
    /// <returns>元组：（是否合法，错误信息）</returns>
    public static (bool IsValid, string? Error) ValidateArgument(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return (false, "参数不能为空");

        var trimmed = arg.Trim();

        if (!trimmed.StartsWith('-'))
            return (false, "JVM 参数必须以 '-' 开头");

        if (IsEmptyValueArgument(trimmed))
            return (false, "参数值为空，请设置一个有效值");

        if (trimmed.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xss", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xmn", StringComparison.OrdinalIgnoreCase))
        {
            var value = trimmed[4..];
            if (!IsValidMemoryValue(value))
                return (false, $"内存值 '{value}' 格式不正确，正确格式如: 4G, 1024M, 512K");
        }

        if (trimmed.StartsWith("-XX:", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.Length > 5 && (trimmed[4] == '+' || trimmed[4] == '-'))
            {
                var name = trimmed[5..];
                if (string.IsNullOrWhiteSpace(name))
                    return (false, "-XX: 参数格式不正确，布尔参数格式应为 -XX:+UseXXX 或 -XX:-UseXXX");
            }
            else if (trimmed.Contains('='))
            {
                var parts = trimmed.Split('=', 2);
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
                    return (false, "-XX: 参数格式不正确，键值对格式应为 -XX:Key=Value");
            }
        }

        if (trimmed.StartsWith("-D", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 2)
        {
            var rest = trimmed[2..];
            if (string.IsNullOrWhiteSpace(rest))
                return (false, "-D 系统属性格式不正确，格式应为 -Dkey=value");
        }

        return (true, null);
    }

    /// <summary>
    /// 验证内存值格式的合法性
    /// </summary>
    /// <param name="value">内存值字符串</param>
    /// <returns>格式合法返回 true</returns>
    /// <remarks>
    /// 支持的格式：
    ///   - 纯数字（字节）
    ///   - 带 G / GB / M / MB / K / KB 后缀
    /// </remarks>
    private static bool IsValidMemoryValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim().ToUpperInvariant();

        if (long.TryParse(value, out _))
            return true;

        var suffixes = new[] { "GB", "G", "MB", "M", "KB", "K" };
        foreach (var suffix in suffixes)
        {
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var numPart = value[..^suffix.Length];
                if (long.TryParse(numPart, out var num) && num > 0)
                    return true;
            }
        }

        return false;
    }
}
