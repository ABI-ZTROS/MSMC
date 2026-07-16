// 🔍 日志根因分析器 —— 不只是告诉你"有错"，还要告诉你"为什么错"
// 从游戏内到服务器各种原因都要考虑，甚至还要排除自身故障
namespace McServerGuard.Services.AIService;

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

/// <summary>
/// 日志根因分析器
/// 
/// 工作流程：
/// 1. 收到日志分析结果后，启动二次分析
/// 2. 收集上下文（前后几行日志、系统状态、服务器信息）
/// 3. 按优先级调用各领域分析器（插件/世界/性能/网络/JVM/玩家）
/// 4. 排除自身故障（McServerGuard 自己的问题）
/// 5. 综合所有证据，输出置信度最高的根因 + 详细建议
/// </summary>
public class LogRootCauseAnalyzer
{
    private readonly List<IRootCauseAnalyzer> _analyzers = [];
    private readonly ConcurrentQueue<LogContextEntry> _recentLogs = new();
    private const int _contextWindowSize = 50;

    public LogRootCauseAnalyzer()
    {
        Log.Information("🔍 日志根因分析器初始化");

        // 注册各个领域的分析器
        _analyzers.Add(new PluginErrorAnalyzer());
        _analyzers.Add(new WorldErrorAnalyzer());
        _analyzers.Add(new PerformanceErrorAnalyzer());
        _analyzers.Add(new NetworkErrorAnalyzer());
        _analyzers.Add(new JvmErrorAnalyzer());
        _analyzers.Add(new PlayerErrorAnalyzer());
        _analyzers.Add(new SelfFaultAnalyzer());

        Log.Information("✅ 已注册 {Count} 个根因分析器", _analyzers.Count);
    }

    /// <summary>
    /// 向分析器输入一条日志（用于构建上下文窗口）
    /// </summary>
    public void FeedLog(string logLine)
    {
        _recentLogs.Enqueue(new LogContextEntry(DateTime.UtcNow, logLine));
        
        // 保持上下文窗口大小
        while (_recentLogs.Count > _contextWindowSize)
        {
            _recentLogs.TryDequeue(out _);
        }
    }

    /// <summary>
    /// 对一个异常进行根因分析
    /// </summary>
    public RootCauseAnalysis AnalyzeRootCause(LogAnalysisResult primaryResult, string logLine)
    {
        Log.Debug("🔬 开始根因分析: {Category} - {Desc}", primaryResult.Category, primaryResult.Description);

        var context = new AnalysisContext
        {
            TargetLogLine = logLine,
            PrimaryResult = primaryResult,
            RecentLogs = _recentLogs.ToList(),
            ServerInfo = null
        };

        var candidates = new List<RootCauseCandidate>();

        // 调用所有分析器，收集候选根因
        foreach (var analyzer in _analyzers)
        {
            try
            {
                var result = analyzer.Analyze(context);
                if (result != null && result.Confidence > 0)
                {
                    candidates.Add(result);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ 分析器 {Name} 执行出错", analyzer.Name);
            }
        }

        // 按置信度排序
        candidates = candidates
            .OrderByDescending(c => c.Confidence)
            .ToList();

        RootCauseCandidate? best = null;
        if (candidates.Count > 0)
        {
            best = candidates[0];
            Log.Debug("🏆 最佳匹配根因: {Cause} (置信度: {Confidence}%)", 
                best.RootCause, best.Confidence);
        }
        else
        {
            best = new RootCauseCandidate
            {
                RootCause = "Unknown",
                RootCauseDescription = "无法确定具体原因，需要更多上下文信息",
                Confidence = 0,
                Category = "Unknown",
                DetailedSuggestions = ["查看完整的日志堆栈信息", "检查服务器最近的变更", "尝试复现问题并观察规律"]
            };
        }

        return new RootCauseAnalysis(
            IsAnomaly: primaryResult.IsAnomaly,
            Severity: primaryResult.Severity,
            Category: primaryResult.Category,
            Description: primaryResult.Description,
            Suggestions: primaryResult.Suggestions,
            RootCause: best.RootCause,
            RootCauseDescription: best.RootCauseDescription,
            Confidence: best.Confidence,
            RootCauseCategory: best.Category,
            DetailedSuggestions: best.DetailedSuggestions,
            AllCandidates: candidates
        );
    }
}

// ─── 分析上下文 ──────────────────────────────────────────────────

/// <summary>
/// 分析上下文 —— 提供给各个分析器的信息
/// </summary>
public class AnalysisContext
{
    public string TargetLogLine { get; init; } = string.Empty;
    public LogAnalysisResult PrimaryResult { get; init; } = null!;
    public List<LogContextEntry> RecentLogs { get; init; } = [];
    public object? ServerInfo { get; init; }
}

/// <summary>
/// 日志上下文条目
/// </summary>
public record LogContextEntry(DateTime Timestamp, string Line);

// ─── 根因分析结果 ────────────────────────────────────────────────

/// <summary>
/// 根因分析结果（增强版 LogAnalysisResult）
/// </summary>
public record RootCauseAnalysis(
    bool IsAnomaly,
    LogSeverity Severity,
    string Category,
    string Description,
    List<string> Suggestions,
    string RootCause,
    string RootCauseDescription,
    int Confidence,
    string RootCauseCategory,
    List<string> DetailedSuggestions,
    List<RootCauseCandidate> AllCandidates
);

/// <summary>
/// 根因候选
/// </summary>
public class RootCauseCandidate
{
    public string RootCause { get; set; } = string.Empty;
    public string RootCauseDescription { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> DetailedSuggestions { get; set; } = [];
}

// ─── 分析器接口 ──────────────────────────────────────────────────

/// <summary>
/// 根因分析器接口
/// </summary>
public interface IRootCauseAnalyzer
{
    public string Name { get; }
    public RootCauseCandidate? Analyze(AnalysisContext context);
}
