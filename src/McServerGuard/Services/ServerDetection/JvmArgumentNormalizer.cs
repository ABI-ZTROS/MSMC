using McServerGuard.Constants;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>🧹 JVM 参数规范化器 —— 去重、过滤空值、检测冲突、排序一条龙服务</summary>
/// <remarks>
/// 用户输入的参数可能乱七八糟：有重复的、有空值的、有互相打架的...
/// 这个类就是专门收拾这些烂摊子的，保证启动命令干净又卫生 ✨
/// </remarks>
public static class JvmArgumentNormalizer
{
    /// <summary>🚫 互斥参数组 —— 同一组内的参数不能同时出现</summary>
    private static readonly string[][] MutexGroups =
    [
        // GC 回收器四选一（只能有一个）
        [
            "-XX:+UseG1GC",
            "-XX:+UseZGC",
            "-XX:+UseShenandoahGC",
            "-XX:+UseParallelGC",
            "-XX:+UseSerialGC",
            "-XX:+UseConcMarkSweepGC"
        ]
    ];

    /// <summary>📋 规范化结果 —— 包含处理后的参数和各种警告信息</summary>
    public class NormalizationResult
    {
        public List<string> Arguments { get; set; } = [];
        public List<string> Warnings { get; set; } = [];
        public List<string> RemovedEmpty { get; set; } = [];
        public List<string> RemovedDuplicates { get; set; } = [];
        public List<string> ResolvedConflicts { get; set; } = [];
    }

    /// <summary>✨ 一条龙规范化：过滤空值 → 去重 → 检测冲突 → 排序</summary>
    /// <param name="arguments">原始参数列表（可能乱七八糟）</param>
    /// <returns>处理后的干净参数列表，附带各种警告</returns>
    public static NormalizationResult Normalize(List<string> arguments)
    {
        Log.Debug("🧹 开始规范化 JVM 参数，原始数量: {Count}", arguments.Count);

        var result = new NormalizationResult();
        var args = new List<string>(arguments);

        // 第一步：过滤空值参数（如 "-XX:MaxGCPauseMillis=" 后面啥也没有的）
        var (filtered, emptyRemoved) = FilterEmptyValueArguments(args);
        args = filtered;
        result.RemovedEmpty = emptyRemoved;
        foreach (var empty in emptyRemoved)
        {
            result.Warnings.Add($"⚠️ 移除空值参数: {empty}（没有设置值，已自动移除）");
            Log.Warning("⚠️ 移除空值参数: {Arg}", empty);
        }

        // 第二步：去重（同一参数出现多次，保留最后一个）
        var (deduplicated, duplicatesRemoved) = DeduplicateArguments(args);
        args = deduplicated;
        result.RemovedDuplicates = duplicatesRemoved;
        foreach (var dup in duplicatesRemoved)
        {
            result.Warnings.Add($"⚠️ 移除重复参数: {dup}（重复出现，已保留最后一个有效值）");
            Log.Debug("⚠️ 移除重复参数: {Arg}", dup);
        }

        // 第三步：检测并解决互斥参数冲突
        var (conflictResolved, conflicts) = ResolveMutexConflicts(args);
        args = conflictResolved;
        result.ResolvedConflicts = conflicts;
        foreach (var conflict in conflicts)
        {
            result.Warnings.Add($"🚫 参数冲突: {conflict}（已自动保留最后一个出现的）");
            Log.Warning("🚫 参数冲突: {Info}", conflict);
        }

        // 第 3.5 步：自动注入 UnlockExperimentalVMOptions（如果使用了实验性参数但缺少此开关）
        var (withUnlock, unlockWarning) = EnsureExperimentalUnlock(args);
        args = withUnlock;
        if (unlockWarning != null)
        {
            result.Warnings.Add(unlockWarning);
            Log.Information("🔓 自动注入: {Info}", unlockWarning);
        }

        // 第四步：按 JVM 惯例排序
        args = SortArguments(args);

        result.Arguments = args;

        Log.Information("✨ JVM 参数规范化完成: {Original} → {Final} 个参数, {WarningCount} 条警告",
            arguments.Count, args.Count, result.Warnings.Count);

        return result;
    }

    /// <summary>🧹 过滤空值参数 —— 把那些 "=" 后面啥也没有的参数干掉</summary>
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

    /// <summary>🔍 判断一个参数是不是空值的（等号后面啥也没有）</summary>
    private static bool IsEmptyValueArgument(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return true;

        var trimmed = arg.Trim();

        // 以 = 结尾的参数（值为空）
        if (trimmed.EndsWith('='))
            return true;

        // -Xms/-Xmx 后面没有值（只有 flag 没有值）
        if ((trimmed.Equals("-Xms", StringComparison.OrdinalIgnoreCase) ||
             trimmed.Equals("-Xmx", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    /// <summary>🔄 去重 —— 同一参数出现多次时保留最后一个</summary>
    /// <remarks>
    /// 为什么保留最后一个？因为 JVM 就是这么处理的：后面的参数会覆盖前面的
    /// 比如 "-Xmx2G -Xmx4G"，最终生效的是 4G
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

    /// <summary>🏷️ 获取参数的"键"——用于判断两个参数是不是同一个东西</summary>
    private static string GetArgumentKey(string arg)
    {
        if (string.IsNullOrEmpty(arg))
            return arg;

        var trimmed = arg.Trim();

        // -XX:+xxx 或 -XX:-xxx 形式 → 去掉 +/-
        if (trimmed.StartsWith("-XX:+", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-XX:-", StringComparison.OrdinalIgnoreCase))
        {
            var nameStart = 5; // "-XX:+" 或 "-XX:-" 的长度
            var eqIndex = trimmed.IndexOf('=', nameStart);
            return eqIndex >= 0
                ? trimmed[..eqIndex]
                : trimmed;
        }

        // -XX:xxx=yyy 形式 → 取等号前面的部分
        if (trimmed.StartsWith("-XX:", StringComparison.OrdinalIgnoreCase))
        {
            var eqIndex = trimmed.IndexOf('=');
            return eqIndex >= 0
                ? trimmed[..eqIndex]
                : trimmed;
        }

        // -Dxxx=yyy 形式 → 取等号前面的部分
        if (trimmed.StartsWith("-D", StringComparison.OrdinalIgnoreCase))
        {
            var eqIndex = trimmed.IndexOf('=');
            return eqIndex >= 0
                ? "-D" + trimmed[2..eqIndex]
                : trimmed;
        }

        // -Xms/-Xmx/-Xss 等 → 取前4个字符
        if (trimmed.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xss", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..4];
        }

        // -Xmn 等 → 取前4个字符
        if (trimmed.StartsWith("-Xmn", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..4];
        }

        // 其他情况就用整个参数
        return trimmed;
    }

    /// <summary>⚔️ 解决互斥参数冲突 —— 同一组内只能留一个，保留最后出现的</summary>
    private static (List<string> Resolved, List<string> Conflicts) ResolveMutexConflicts(List<string> args)
    {
        var resolved = new List<string>(args);
        var conflicts = new List<string>();

        foreach (var group in MutexGroups)
        {
            var foundInGroup = new List<string>();

            // 找出这一组中所有出现了的参数
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

            // 如果找到多个，保留最后一个，干掉前面的
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

    /// <summary>🔓 确保使用了实验性参数时自动注入 UnlockExperimentalVMOptions</summary>
    /// <remarks>
    /// G1NewSizePercent、G1MaxNewSizePercent、UseCompactObjectHeaders 等参数
    /// 是实验性的，JVM 要求 -XX:+UnlockExperimentalVMOptions 必须出现在它们之前。
    /// 如果用户没手动加，我们就自动加上，避免 "VM option is experimental" 报错。
    /// </remarks>
    private static (List<string> Args, string? Warning) EnsureExperimentalUnlock(List<string> args)
    {
        // 已有的实验性参数 flag 名称（不含前缀），用于匹配
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

            // 检查是否包含实验性参数名
            foreach (var flag in experimentalFlags)
            {
                if (trimmed.Contains(flag, StringComparison.OrdinalIgnoreCase))
                {
                    hasExperimentalArg = true;
                    break;
                }
            }
        }

        // 如果使用了实验性参数但没有 UnlockExperimentalVMOptions，自动注入
        if (hasExperimentalArg && !hasUnlockFlag)
        {
            var result = new List<string>(args)
            {
                "-XX:+UnlockExperimentalVMOptions"
            };
            return (result, "🔓 自动注入 -XX:+UnlockExperimentalVMOptions（检测到实验性参数，JVM 要求此开关在前）");
        }

        return (args, null);
    }

    /// <summary>📊 按 JVM 惯例排序 —— 解锁开关→内存→GC→性能→系统属性→其他</summary>
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

            // 解锁开关（必须在所有参数之前，否则实验性/诊断性参数无效）
            if (trimmed.Contains("UnlockExperimentalVMOptions", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("UnlockDiagnosticVMOptions", StringComparison.OrdinalIgnoreCase))
            {
                unlockArgs.Add(arg);
            }
            // 内存参数
            else if (trimmed.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("-Xmn", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("-Xss", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("MetaspaceSize", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("MaxMetaspaceSize", StringComparison.OrdinalIgnoreCase))
            {
                memoryArgs.Add(arg);
            }
            // GC 参数
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
            // 系统属性（-D）
            else if (trimmed.StartsWith("-D", StringComparison.OrdinalIgnoreCase))
            {
                systemProps.Add(arg);
            }
            // 性能参数
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
            // 其他
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

    /// <summary>✅ 验证单个参数格式是否合法</summary>
    /// <param name="arg">要验证的参数字符串</param>
    /// <returns>(是否合法, 错误信息)</returns>
    public static (bool IsValid, string? Error) ValidateArgument(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg))
            return (false, "参数不能为空");

        var trimmed = arg.Trim();

        // 必须以 - 开头（标准 JVM 参数都是以减号开头的）
        if (!trimmed.StartsWith('-'))
            return (false, "JVM 参数必须以 '-' 开头");

        // 检查是不是空值参数
        if (IsEmptyValueArgument(trimmed))
            return (false, "参数值为空，请设置一个有效值");

        // 内存参数值校验
        if (trimmed.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xss", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("-Xmn", StringComparison.OrdinalIgnoreCase))
        {
            var value = trimmed[4..];
            if (!IsValidMemoryValue(value))
                return (false, $"内存值 '{value}' 格式不正确，正确格式如: 4G, 1024M, 512K");
        }

        // -XX: 开头的参数
        if (trimmed.StartsWith("-XX:", StringComparison.OrdinalIgnoreCase))
        {
            // +xxx 或 -xxx 形式（布尔开关）
            if (trimmed.Length > 5 && (trimmed[4] == '+' || trimmed[4] == '-'))
            {
                var name = trimmed[5..];
                if (string.IsNullOrWhiteSpace(name))
                    return (false, "-XX: 参数格式不正确，布尔参数格式应为 -XX:+UseXXX 或 -XX:-UseXXX");
            }
            // xxx=yyy 形式（键值对）
            else if (trimmed.Contains('='))
            {
                var parts = trimmed.Split('=', 2);
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
                    return (false, "-XX: 参数格式不正确，键值对格式应为 -XX:Key=Value");
            }
        }

        // -D 开头的系统属性
        if (trimmed.StartsWith("-D", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 2)
        {
            var rest = trimmed[2..];
            if (string.IsNullOrWhiteSpace(rest))
                return (false, "-D 系统属性格式不正确，格式应为 -Dkey=value");
        }

        return (true, null);
    }

    /// <summary>📏 验证内存值格式是否合法</summary>
    private static bool IsValidMemoryValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = value.Trim().ToUpperInvariant();

        // 纯数字
        if (long.TryParse(value, out _))
            return true;

        // 带 G/GB/M/MB/K/KB 后缀
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
