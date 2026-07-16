// 🤖 AI 保障服务接口 —— 定义"AI 守护服务器"的能力边界
// 虽然叫 AI，但其实很多功能是基于规则的 —— 没必要搞个深度学习模型来判断 OOM 吧 🤷
namespace McServerGuard.Services.AIService;

using McServerGuard.Models;

/// <summary>
/// 日志严重程度 —— 从"岁月静好"到"天塌了"的四个等级
/// </summary>
public enum LogSeverity
{
    /// <summary>一切正常，岁月静好 🌿</summary>
    Normal,

    /// <summary>信息类日志，正常但值得关注 ℹ️</summary>
    Info,

    /// <summary>有点小问题，但还能苟着 ⚠️</summary>
    Warning,

    /// <summary>出错了！不过服务器还没炸 🧨</summary>
    Error,

    /// <summary>Critical —— 坐好别动，服务器要炸了 💥</summary>
    Critical,
}

/// <summary>
/// 日志分析结果 —— 对一行日志的分析结论
/// </summary>
/// <param name="IsAnomaly">这行日志是不是异常？</param>
/// <param name="Severity">异常严重程度</param>
/// <param name="Category">异常类别（如 OutOfMemory、StackOverflow 等）</param>
/// <param name="Description">人类可读的异常描述</param>
/// <param name="Suggestions">修复建议列表</param>
public record LogAnalysisResult(
    bool IsAnomaly,
    LogSeverity Severity,
    string Category,
    string Description,
    List<string> Suggestions
);

/// <summary>
/// 崩溃预测结果 —— 系统觉得你的服务器还能活多久 🫣
/// </summary>
/// <param name="CrashProbability">崩溃概率 (0.0 ~ 1.0)，越高越危险</param>
/// <param name="PrimaryRiskFactor">最主要的风险因素</param>
/// <param name="RiskIndicators">所有检测到的风险指标列表</param>
public record CrashPrediction(
    double CrashProbability,
    string PrimaryRiskFactor,
    List<string> RiskIndicators
);

/// <summary>
/// 配置优化建议 —— 给你的服务器配置号号脉
/// </summary>
/// <param name="Category">优化类别（内存/GC/线程/视图距离等）</param>
/// <param name="CurrentValue">当前配置值</param>
/// <param name="SuggestedValue">建议配置值</param>
/// <param name="Reason">为什么这么建议</param>
/// <param name="Impact">预期影响（高/中/低）</param>
public record ConfigOptimizationSuggestion(
    string Category,
    string CurrentValue,
    string SuggestedValue,
    string Reason,
    string Impact
);

/// <summary>
/// AI 保障服务接口
/// 日志分析、崩溃预测、配置优化 —— 三大核心能力
/// </summary>
public interface IAiGuardService
{
    /// <summary>
    /// 初始化 AI 服务，加载历史学习数据
    /// </summary>
    public Task InitializeAsync();

    /// <summary>
    /// 分析一行日志，判断是否为异常并给出建议
    /// </summary>
    /// <param name="logLine">日志文本</param>
    /// <returns>日志分析结果</returns>
    public LogAnalysisResult AnalyzeLog(string logLine);

    /// <summary>
    /// 根据当前系统指标预测崩溃风险
    /// </summary>
    /// <param name="metrics">当前系统指标</param>
    /// <returns>崩溃预测结果</returns>
    public Task<CrashPrediction> PredictCrashAsync(SystemMetrics metrics);

    /// <summary>
    /// 根据服务器实例和系统指标给出配置优化建议
    /// </summary>
    /// <param name="server">服务器实例信息</param>
    /// <param name="metrics">当前系统指标</param>
    /// <returns>优化建议列表</returns>
    public List<ConfigOptimizationSuggestion> SuggestOptimizations(ServerInstance server, SystemMetrics metrics);

    /// <summary>
    /// 对异常日志进行根因分析（二次分析）
    /// 不只是告诉你"有错"，还要告诉你"为什么错"
    /// </summary>
    /// <param name="logLine">日志行</param>
    /// <returns>根因分析结果</returns>
    public RootCauseAnalysis AnalyzeRootCause(string logLine);
}

/// <summary>
/// AI 自学习服务接口 —— 扩展的自学习能力
/// 当服务器被导入/检测到后，自动从日志中学习异常模式
/// </summary>
public interface IAiSelfLearningService
{
    /// <summary>
    /// 从服务器的日志目录自动学习异常模式
    /// 不需要用户手动导入模型！自动扫描服务器日志并学习
    /// </summary>
    /// <param name="server">服务器实例</param>
    public Task AutoLearnFromServerAsync(ServerInstance server);
}
