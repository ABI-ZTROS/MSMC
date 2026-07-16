// ⚙️ 配置优化推荐器 —— 像老中医一样给你的服务器配置号号脉
// 纯规则引擎，不需要什么 AI 模型 —— 但推荐出来的建议很实用
// 基于服务器配置和系统硬件的实际情况，给出针对性优化建议 💡
namespace McServerGuard.Services.AIService;

using System.IO;
using McServerGuard.Models;
using Serilog;

/// <summary>
/// 配置优化推荐器 —— 基于规则的配置优化建议引擎
/// 根据 ServerInstance 的 JVM 参数和 SystemMetrics 的硬件信息，
/// 给出一套有针对性的优化建议
/// </summary>
public class ConfigOptimizer
{
    /// <summary>
    /// 根据服务器实例和系统指标生成配置优化建议
    /// 每条建议包含当前值、建议值、原因和预期影响
    /// </summary>
    /// <param name="server">服务器实例（包含 JVM 参数等）</param>
    /// <param name="metrics">当前系统指标</param>
    /// <returns>优化建议列表</returns>
    public List<ConfigOptimizationSuggestion> Suggest(ServerInstance server, SystemMetrics metrics)
    {
        // 日志：生成优化建议入口
        Log.Information("💡 ConfigOptimizer.Suggest: 生成优化建议...");
        var suggestions = new List<ConfigOptimizationSuggestion>();

        // 规则 1：view-distance 根据内存调整
        SuggestViewDistance(suggestions, server.MaxHeapMemoryBytes, server.WorkingDirectory);

        // 规则 2：simulation-distance 建议
        SuggestSimulationDistance(suggestions, server.WorkingDirectory);

        // 规则 3：Aikar 标志检查
        CheckAikarFlags(suggestions, server);

        // 规则 4：GC 类型检查
        CheckGcType(suggestions, server);

        // 规则 5：Xms/Xmx 一致性
        CheckXmsXmxConsistency(suggestions, server);

        // 规则 6：线程数过高建议
        SuggestThreadOptimization(suggestions, metrics);

        // 规则 7：内存分配建议
        SuggestMemoryAllocation(suggestions, server, metrics);

        // 日志：每条建议生成
        foreach (var suggestion in suggestions)
        {
            Log.Debug("📝 建议: [{Category}] {Current} → {Suggested}: {Reason} (影响: {Impact})",
                suggestion.Category, suggestion.CurrentValue, suggestion.SuggestedValue,
                suggestion.Reason, suggestion.Impact);
        }

        // 日志：完成
        Log.Information("✅ 生成 {Count} 条优化建议", suggestions.Count);
        return suggestions;
    }

    /// <summary>
    /// 规则 1：view-distance 根据可用内存给出建议
    /// 内存不够就不要开太远的视距啦，你的玩家也没那么远视 😂
    /// </summary>
    private void SuggestViewDistance(List<ConfigOptimizationSuggestion> suggestions, long maxHeapBytes, string workingDirectory)
    {
        if (maxHeapBytes <= 0)
            return;

        var xmxGb = maxHeapBytes / 1024.0 / 1024 / 1024;

        // 简单规则：每 GB 内存大约支持 2 格视距（非常粗略的估计）
        var maxRecommendedViewDist = (int)Math.Min(Math.Floor(xmxGb * 2), 32);

        // 从 server.properties 读取当前 view-distance
        var viewDist = ReadServerProperty("view-distance", -1, workingDirectory);
        if (viewDist > maxRecommendedViewDist)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                Category: "视图距离",
                CurrentValue: $"view-distance={viewDist}",
                SuggestedValue: $"view-distance={maxRecommendedViewDist}",
                Reason: $"当前分配了 {xmxGb:F1}GB 内存，view-distance={viewDist} 可能过高。" +
                        $"建议控制在 {maxRecommendedViewDist} 以内以减少内存压力。",
                Impact: "高"
            ));
            // 日志：每条建议生成
            Log.Debug("📝 建议: 视图距离 - {Desc}", $"view-distance={viewDist} → {maxRecommendedViewDist}");
        }
    }

    /// <summary>
    /// 规则 2：simulation-distance 建议不超过 8
    /// </summary>
    private void SuggestSimulationDistance(List<ConfigOptimizationSuggestion> suggestions, string workingDirectory)
    {
        var simDist = ReadServerProperty("simulation-distance", -1, workingDirectory);
        if (simDist > 8)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                Category: "模拟距离",
                CurrentValue: $"simulation-distance={simDist}",
                SuggestedValue: "simulation-distance=8",
                Reason: "simulation-distance > 8 会显著增加服务器 CPU 负担，大多数场景 4-8 就够了",
                Impact: "中"
            ));
        }
    }

    /// <summary>
    /// 规则 3：检查是否使用了 Aikar 推荐的 JVM 参数
    /// Aikar 的 JVM 参数是 Minecraft 服务器性能优化的"黄金标准"
    /// </summary>
    private void CheckAikarFlags(List<ConfigOptimizationSuggestion> suggestions, ServerInstance server)
    {
        if (!server.UsesAikarFlags)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                Category: "JVM 参数",
                CurrentValue: "未使用 Aikar 推荐参数",
                SuggestedValue: "添加 Aikar's Flags (G1GC + 自定义 NewSize/MaxNewSize)",
                Reason: "Aikar 推荐的 JVM 参数专门针对 Minecraft 服务器进行了优化，" +
                        "能显著减少 GC 停顿时间。参考: https://aikar.co/2018/07/02/tuning-the-jvm-g1gc-garbage-collector-flags-for-minecraft/",
                Impact: "高"
            ));
        }
    }

    /// <summary>
    /// 规则 4：检查 GC 类型
    /// 推荐使用 G1GC，ZGC 也行（但需要 Java 21+），千万别用 CMS（已移除）
    /// </summary>
    private void CheckGcType(List<ConfigOptimizationSuggestion> suggestions, ServerInstance server)
    {
        var gcType = (server.GcType ?? string.Empty).ToLowerInvariant();

        if (gcType.Contains("cms"))
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                Category: "GC 类型",
                CurrentValue: "CMS GC（已移除）",
                SuggestedValue: "UseG1GC",
                Reason: "CMS GC 在 Java 14+ 已被移除，使用它会导致服务器无法启动。" +
                        "请切换到 G1GC（Java 8+）或 ZGC（Java 21+）",
                Impact: "高"
            ));
        }
        else if (string.IsNullOrEmpty(gcType))
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                Category: "GC 类型",
                CurrentValue: "未明确指定 GC",
                SuggestedValue: "-XX:+UseG1GC",
                Reason: "未显式指定 GC 类型会让 JVM 使用默认选择，可能不适合 Minecraft 服务器。" +
                        "推荐使用 G1GC 或 ZGC",
                Impact: "中"
            ));
        }
    }

    /// <summary>
    /// 规则 5：Xms/Xmx 一致性
    /// 推荐将 Xms（初始堆大小）和 Xmx（最大堆大小）设为相同
    /// </summary>
    private void CheckXmsXmxConsistency(List<ConfigOptimizationSuggestion> suggestions, ServerInstance server)
    {
        if (server.InitialHeapMemoryBytes > 0 && server.MaxHeapMemoryBytes > 0 &&
            server.InitialHeapMemoryBytes < server.MaxHeapMemoryBytes)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                Category: "内存配置",
                CurrentValue: $"-Xms{server.InitialHeapMemoryBytes / 1024 / 1024}M / -Xmx{server.MaxHeapMemoryBytes / 1024 / 1024}M",
                SuggestedValue: $"-Xms{server.MaxHeapMemoryBytes / 1024 / 1024}M / -Xmx{server.MaxHeapMemoryBytes / 1024 / 1024}M (Xms = Xmx)",
                Reason: "Xms（初始堆）和 Xmx（最大堆）不一致会导致 JVM 在运行时频繁扩缩堆，" +
                        "触发不必要的 GC。建议将两者设为相同值",
                Impact: "中"
            ));
        }
    }

    /// <summary>
    /// 规则 6：线程数过高时给出优化建议
    /// </summary>
    private void SuggestThreadOptimization(List<ConfigOptimizationSuggestion> suggestions, SystemMetrics metrics)
    {
        var logicalCores = Environment.ProcessorCount;
        if (logicalCores <= 0)
            return;

        var threadsPerCore = (double)metrics.TotalThreadCount / logicalCores;

        if (threadsPerCore > 50)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                Category: "线程优化",
                CurrentValue: $"系统总线程数 {metrics.TotalThreadCount} (每核 {threadsPerCore:F1})",
                SuggestedValue: "排查线程泄漏的插件/mod",
                Reason: "每核线程数过高（>50），可能存在线程泄漏。" +
                        "常见原因：插件异常创建线程、连接池未正确关闭、异步任务堆积",
                Impact: "高"
            ));
        }
    }

    /// <summary>
    /// 规则 7：内存分配建议
    /// 确保分配给 Minecraft 的内存不会占满系统内存
    /// </summary>
    private void SuggestMemoryAllocation(List<ConfigOptimizationSuggestion> suggestions, ServerInstance server, SystemMetrics metrics)
    {
        if (metrics.TotalMemoryBytes <= 0 || server.MaxHeapMemoryBytes <= 0)
            return;

        var ratio = (double)server.MaxHeapMemoryBytes / metrics.TotalMemoryBytes;

        if (ratio > 0.85)
        {
            suggestions.Add(new ConfigOptimizationSuggestion(
                Category: "内存分配",
                CurrentValue: $"-Xmx {server.MaxHeapMemoryBytes / 1024 / 1024}MB (占系统内存 {ratio:P0})",
                SuggestedValue: $"-Xmx {metrics.TotalMemoryBytes * 3 / 4 / 1024 / 1024}MB (约系统内存的 75%)",
                Reason: "分配给 JVM 的内存占比过高！系统和其他进程也需要内存。" +
                        "建议保留至少 20-25% 的内存给操作系统使用，避免系统因内存不足而开始换页",
                Impact: "高"
            ));
        }
    }

    /// <summary>
    /// 从 server.properties 读取指定属性的值
    /// </summary>
    private static int ReadServerProperty(string key, int defaultValue, string workingDirectory)
    {
        // 日志：读取服务器属性
        var dir = string.IsNullOrEmpty(workingDirectory) || workingDirectory == "(无法解析工作目录)"
            ? Environment.CurrentDirectory
            : workingDirectory;
        var propPath = Path.Combine(dir, "server.properties");
        Log.Debug("📂 读取服务器属性: {Path}", propPath);

        if (File.Exists(propPath))
        {
            try
            {
                foreach (var line in File.ReadAllLines(propPath))
                {
                    if (line.TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        var eqIdx = line.IndexOf('=');
                        if (eqIdx > 0 && int.TryParse(line[(eqIdx + 1)..].Trim(), out var val))
                            return val;
                    }
                }
            }
            catch (Exception ex)
            {
                // 日志：读取属性文件失败
                Log.Error(ex, "💥 fuck: 读取属性文件失败: {Path}: {Message}", propPath, ex.Message);
            }
        }

        return defaultValue;
    }
}
