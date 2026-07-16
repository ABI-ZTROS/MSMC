// 🔮 崩溃预测器 —— 基于系统指标的滑动窗口分析，预测你的服务器还能撑多久
// 原理很简单：如果 CPU/内存/磁盘一直居高不下，那崩溃就是时间问题
// 这不是什么深度学习，是朴素的统计学 —— 但够用了 📈
namespace McServerGuard.Services.AIService;

using System.Collections.Concurrent;
using McServerGuard.Models;
using Serilog;

/// <summary>
/// 崩溃预测器 —— 维护一个 30 点的滑动窗口缓冲区，基于阈值检测异常
/// 当 CPU/内存/磁盘/线程数持续异常时，累加崩溃概率
/// </summary>
public class CrashPredictor
{
    #region 崩溃检测阈值常量

    /// <summary>CPU 使用率超过此百分比视为高负载</summary>
    public const double CpuHighThreshold = 90.0;

    /// <summary>CPU 使用率超过此百分比视为中等负载</summary>
    public const double CpuWarningThreshold = 75.0;

    /// <summary>内存使用率超过此百分比视为危险</summary>
    public const double MemoryDangerThreshold = 90.0;

    /// <summary>内存使用率超过此百分比视为警告</summary>
    public const double MemoryWarningThreshold = 80.0;

    /// <summary>磁盘使用率超过此百分比视为危险</summary>
    public const double DiskDangerThreshold = 95.0;

    /// <summary>磁盘使用率超过此百分比视为警告</summary>
    public const double DiskWarningThreshold = 85.0;

    /// <summary>每核线程数超过此值视为过高</summary>
    public const double ThreadsPerCoreHighThreshold = 50.0;

    /// <summary>滑动窗口大小 —— 保留最近 30 个数据点</summary>
    public const int WindowSize = 30;

    /// <summary>单次异常对崩溃概率的贡献值</summary>
    public const double SingleAnomalyScore = 0.05;

    /// <summary>持续异常（超过窗口一半数据点异常）的额外贡献值</summary>
    public const double SustainedAnomalyScore = 0.15;

    /// <summary>崩溃概率的上限 —— 超过 1.0 没有意义</summary>
    public const double MaxCrashProbability = 1.0;

    #endregion

    /// <summary>
    /// 指标缓冲区 —— 滑动窗口，最多保留 30 个数据点
    /// ConcurrentQueue 是线程安全的，虽然在这个场景下可能不太需要，但有备无患
    /// </summary>
    private readonly ConcurrentQueue<SystemMetrics> _metricsBuffer = new();

    /// <summary>当前累计的崩溃概率</summary>
    private double _currentCrashProbability;

    /// <summary>当前最主要的风险因素</summary>
    private string _primaryRiskFactor = "无";

    /// <summary>当前检测到的风险指标</summary>
    private readonly List<string> _riskIndicators = [];

    /// <summary>
    /// 更新一条新的系统指标数据
    /// 每次调用都会：将数据推入滑动窗口 → 维护窗口大小 → 检测异常 → 更新崩溃概率
    /// </summary>
    /// <param name="metrics">最新的系统指标</param>
    public void Update(SystemMetrics metrics)
    {
        // 日志：推入新指标
        Log.Debug("📥 CrashPredictor.Update: 推入新指标，窗口大小 {Size} / {Window}", _metricsBuffer.Count, WindowSize);

        // 1. 推入缓冲区
        _metricsBuffer.Enqueue(metrics);

        // 2. 维护窗口大小 —— 超出就踢掉最旧的
        while (_metricsBuffer.Count > WindowSize)
        {
            // 日志：窗口满移除
            Log.Debug("🔄 窗口已满，移除最旧数据点");
            _metricsBuffer.TryDequeue(out _);
        }

        // 3. 计算当前窗口内各项指标的异常情况
        var anomalyCount = DetectAnomalies(metrics);

        // 4. 根据异常数量更新崩溃概率
        UpdateCrashProbability(anomalyCount, metrics);
    }

    /// <summary>
    /// 获取当前崩溃预测结果
    /// </summary>
    public CrashPrediction GetCurrentPrediction()
    {
        // 日志：获取当前预测
        Log.Debug("🔮 获取当前崩溃预测...");

        var prediction = new CrashPrediction(
            CrashProbability: Math.Round(_currentCrashProbability, 4),
            PrimaryRiskFactor: _primaryRiskFactor,
            RiskIndicators: [.. _riskIndicators]
        );

        // 高概率预警
        if (prediction.CrashProbability >= 0.5)
        {
            Log.Warning("⚠️ fuck: 崩溃概率 {Prob}% 超过阈值！", prediction.CrashProbability * 100);
        }
        else
        {
            // 日志：正常结果
            Log.Debug("📊 崩溃概率: {Prob}%", prediction.CrashProbability * 100);
        }

        return prediction;
    }

    /// <summary>
    /// 检测单条指标中的异常
    /// </summary>
    private int DetectAnomalies(SystemMetrics metrics)
    {
        var anomalies = 0;
        _riskIndicators.Clear();

        // CPU 异常检测
        if (metrics.CpuUsagePercent > CpuHighThreshold)
        {
            anomalies++;
            _riskIndicators.Add($"CPU 使用率极高 ({metrics.CpuUsagePercent:F1}%)");
            UpdatePrimaryRisk("CPU 超载");
        }
        else if (metrics.CpuUsagePercent > CpuWarningThreshold)
        {
            anomalies++;
            _riskIndicators.Add($"CPU 使用率偏高 ({metrics.CpuUsagePercent:F1}%)");
        }

        // 内存异常检测
        if (metrics.MemoryUsagePercent > MemoryDangerThreshold)
        {
            anomalies++;
            _riskIndicators.Add($"内存使用率危险 ({metrics.MemoryUsagePercent:F1}%)");
            UpdatePrimaryRisk("内存不足");
        }
        else if (metrics.MemoryUsagePercent > MemoryWarningThreshold)
        {
            anomalies++;
            _riskIndicators.Add($"内存使用率偏高 ({metrics.MemoryUsagePercent:F1}%)");
        }

        // 磁盘异常检测
        if (metrics.DiskUsagePercent > DiskDangerThreshold)
        {
            anomalies++;
            _riskIndicators.Add($"磁盘使用率危险 ({metrics.DiskUsagePercent:F1}%)");
            UpdatePrimaryRisk("磁盘空间不足");
        }
        else if (metrics.DiskUsagePercent > DiskWarningThreshold)
        {
            anomalies++;
            _riskIndicators.Add($"磁盘使用率偏高 ({metrics.DiskUsagePercent:F1}%)");
        }

        // 线程数异常检测
        var logicalCores = Environment.ProcessorCount;
        if (logicalCores > 0)
        {
            var threadsPerCore = (double)metrics.TotalThreadCount / logicalCores;
            if (threadsPerCore > ThreadsPerCoreHighThreshold)
            {
                anomalies++;
                _riskIndicators.Add($"每核线程数过高 ({threadsPerCore:F1})");
                UpdatePrimaryRisk("线程数过多");
            }
        }

        return anomalies;
    }

    /// <summary>
    /// 根据异常数量更新崩溃概率
    /// 每次异常增加一点概率，但如果窗口内持续异常就加速增加
    /// 也会自然衰减 —— 如果异常消失了，崩溃概率会慢慢降低（你表现不错 👍）
    /// </summary>
    private void UpdateCrashProbability(int anomalyCount, SystemMetrics metrics)
    {
        if (anomalyCount == 0)
        {
            // 没有异常，缓慢衰减崩溃概率
            _currentCrashProbability *= 0.95; // 每次衰减 5%
            if (_currentCrashProbability < 0.01)
            {
                _currentCrashProbability = 0;
                _primaryRiskFactor = "无";
            }
            return;
        }

        // 有异常：累加崩溃概率
        var scoreIncrease = anomalyCount * SingleAnomalyScore;

        // 检查是否是持续异常（窗口内超过一半数据点异常）
        if (IsSustainedAnomaly())
        {
            scoreIncrease += SustainedAnomalyScore;
            Log.Warning(
                "检测到持续异常！崩溃概率加速上升 (+{Score:P})",
                scoreIncrease);
        }

        _currentCrashProbability = Math.Min(MaxCrashProbability, _currentCrashProbability + scoreIncrease);

        Log.Debug(
            "崩溃概率更新: {Probability:P}, 异常项={Anomalies}",
            _currentCrashProbability, anomalyCount);
    }

    /// <summary>
    /// 检查窗口内是否存在持续异常（超过一半的数据点有异常）
    /// </summary>
    private bool IsSustainedAnomaly()
    {
        if (_metricsBuffer.Count < 5) // 数据太少不判断
            return false;

        var sustainedCount = 0;
        var total = 0;

        foreach (var m in _metricsBuffer)
        {
            total++;
            var hasAnomaly = false;

            if (m.CpuUsagePercent > CpuWarningThreshold)
                hasAnomaly = true;
            if (m.MemoryUsagePercent > MemoryWarningThreshold)
                hasAnomaly = true;
            if (m.DiskUsagePercent > DiskWarningThreshold)
                hasAnomaly = true;
            if (Environment.ProcessorCount > 0 &&
                (double)m.TotalThreadCount / Environment.ProcessorCount > ThreadsPerCoreHighThreshold)
                hasAnomaly = true;

            if (hasAnomaly)
                sustainedCount++;
        }

        return sustainedCount > total / 2;
    }

    /// <summary>
    /// 更新主要风险因素（只记录最严重的）
    /// </summary>
    private void UpdatePrimaryRisk(string newRisk)
    {
        // "内存不足" 和 "磁盘空间不足" 优先级最高
        var priorities = new[] { "内存不足", "磁盘空间不足", "CPU 超载", "线程数过多" };
        var currentPriority = Array.IndexOf(priorities, _primaryRiskFactor);
        var newPriority = Array.IndexOf(priorities, newRisk);

        if (newPriority >= 0 && (currentPriority < 0 || newPriority < currentPriority))
        {
            _primaryRiskFactor = newRisk;
        }
    }
}
