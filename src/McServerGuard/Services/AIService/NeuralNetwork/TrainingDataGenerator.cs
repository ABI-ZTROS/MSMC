namespace McServerGuard.Services.AIService.NeuralNetwork;

using McServerGuard.Models;
using Serilog;

/// <summary>
/// 训练样本生成器 —— 从 SystemMetrics 生成 MLP 训练数据
/// 同时内置一套合成的基准训练样本用于冷启动
/// </summary>
public class TrainingDataGenerator
{
    /// <summary>
    /// 输入特征维度：12
    /// 0. CPU使用率 (归一化 0-1)
    /// 1. 内存使用率 (归一化 0-1)
    /// 2. 磁盘使用率 (归一化 0-1)
    /// 3. 每核线程数 (归一化 0-1, 阈值50)
    /// 4. Java堆使用率 (归一化 0-1)
    /// 5. Java线程占比 (归一化 0-1)
    /// 6. CPU增长趋势 (最近变化率)
    /// 7. 内存增长趋势
    /// 8. 磁盘I/O强度 (估算)
    /// 9. 系统负载等级 (0-1)
    /// 10. 进程数量归一化
    /// 11. 时间因素 (时段影响)
    /// </summary>
    public const int InputDimension = 12;

    /// <summary>
    /// 输出维度：3
    /// 0. 崩溃概率 (0-1)
    /// 1. 性能评分 (0-1, 越高越好)
    /// 2. 优化优先级 (0-1, 越高越紧急)
    /// </summary>
    public const int OutputDimension = 3;

    private readonly List<SystemMetrics> _metricsHistory = new();
    private const int MaxHistorySize = 100;
    private readonly object _historyLock = new();

    /// <summary>
    /// 将 SystemMetrics 转换为 MLP 输入向量（归一化）
    /// </summary>
    public double[] MetricsToInput(SystemMetrics metrics)
    {
        var input = new double[InputDimension];

        input[0] = Math.Clamp(metrics.CpuUsagePercent / 100.0, 0, 1);
        input[1] = Math.Clamp(metrics.MemoryUsagePercent / 100.0, 0, 1);
        input[2] = Math.Clamp(metrics.DiskUsagePercent / 100.0, 0, 1);

        var logicalCores = Environment.ProcessorCount;
        var threadsPerCore = logicalCores > 0 ? (double)metrics.TotalThreadCount / logicalCores : 0;
        input[3] = Math.Clamp(threadsPerCore / 50.0, 0, 1);

        if (metrics.JavaHeapMaxBytes > 0)
            input[4] = Math.Clamp((double)metrics.JavaHeapUsedBytes / metrics.JavaHeapMaxBytes, 0, 1);

        if (metrics.TotalThreadCount > 0)
            input[5] = Math.Clamp((double)metrics.JavaThreadCount / metrics.TotalThreadCount, 0, 1);

        double cpuTrend = 0, memTrend = 0;
        lock (_historyLock)
        {
            if (_metricsHistory.Count >= 3)
            {
                var recent = _metricsHistory.TakeLast(3).ToList();
                cpuTrend = (metrics.CpuUsagePercent - recent[0].CpuUsagePercent) / 100.0;
                memTrend = (metrics.MemoryUsagePercent - recent[0].MemoryUsagePercent) / 100.0;
            }
        }
        input[6] = Math.Clamp(cpuTrend + 0.5, 0, 1);
        input[7] = Math.Clamp(memTrend + 0.5, 0, 1);

        input[8] = Math.Clamp(metrics.DiskUsagePercent / 100.0 * 0.5, 0, 1);

        var loadScore = input[0] * 0.35 + input[1] * 0.3 + input[2] * 0.15 + input[3] * 0.2;
        input[9] = Math.Clamp(loadScore, 0, 1);

        input[10] = Math.Clamp(metrics.TotalThreadCount / 500.0, 0, 1);

        var hourFactor = Math.Sin(DateTime.Now.Hour * Math.PI / 12) * 0.5 + 0.5;
        input[11] = hourFactor;

        return input;
    }

    /// <summary>
    /// 记录历史指标用于趋势计算
    /// </summary>
    public void RecordMetrics(SystemMetrics metrics)
    {
        lock (_historyLock)
        {
            _metricsHistory.Add(metrics);
            if (_metricsHistory.Count > MaxHistorySize)
                _metricsHistory.RemoveAt(0);
        }
    }

    /// <summary>
    /// 生成基准训练样本（冷启动用）
    /// 基于经验规则生成100+条标注样本
    /// </summary>
    public List<(double[] Input, double[] Target)> GenerateBaselineSamples()
    {
        var samples = new List<(double[], double[])>(200);

        for (int i = 0; i < 50; i++)
        {
            var cpu = RandomInRange(0.0, 0.4);
            var mem = RandomInRange(0.2, 0.5);
            var disk = RandomInRange(0.1, 0.5);
            var tpc = RandomInRange(0.0, 0.2);
            var heap = RandomInRange(0.2, 0.5);
            var input = MakeInput(cpu, mem, disk, tpc, heap, 0.1, 0.0, 0.0);
            var target = new double[] { 0.02, 0.9, 0.1 };
            samples.Add((input, target));
        }

        for (int i = 0; i < 40; i++)
        {
            var cpu = RandomInRange(0.4, 0.7);
            var mem = RandomInRange(0.5, 0.75);
            var disk = RandomInRange(0.5, 0.75);
            var tpc = RandomInRange(0.2, 0.5);
            var heap = RandomInRange(0.5, 0.7);
            var input = MakeInput(cpu, mem, disk, tpc, heap, 0.3, 0.1, 0.05);
            var target = new double[] { 0.15, 0.65, 0.4 };
            samples.Add((input, target));
        }

        for (int i = 0; i < 30; i++)
        {
            var cpu = RandomInRange(0.7, 0.9);
            var mem = RandomInRange(0.75, 0.9);
            var disk = RandomInRange(0.75, 0.9);
            var tpc = RandomInRange(0.5, 0.8);
            var heap = RandomInRange(0.7, 0.85);
            var input = MakeInput(cpu, mem, disk, tpc, heap, 0.6, 0.3, 0.2);
            var target = new double[] { 0.5, 0.35, 0.75 };
            samples.Add((input, target));
        }

        for (int i = 0; i < 25; i++)
        {
            var cpu = RandomInRange(0.9, 1.0);
            var mem = RandomInRange(0.9, 1.0);
            var disk = RandomInRange(0.9, 1.0);
            var tpc = RandomInRange(0.8, 1.0);
            var heap = RandomInRange(0.85, 1.0);
            var input = MakeInput(cpu, mem, disk, tpc, heap, 0.85, 0.6, 0.5);
            var target = new double[] { 0.85, 0.1, 0.95 };
            samples.Add((input, target));
        }

        for (int i = 0; i < 20; i++)
        {
            var cpu = RandomInRange(0.3, 0.6);
            var mem = RandomInRange(0.85, 1.0);
            var disk = RandomInRange(0.3, 0.6);
            var tpc = RandomInRange(0.3, 0.6);
            var heap = RandomInRange(0.9, 1.0);
            var input = MakeInput(cpu, mem, disk, tpc, heap, 0.4, 0.7, 0.2);
            var target = new double[] { 0.7, 0.25, 0.85 };
            samples.Add((input, target));
        }

        for (int i = 0; i < 20; i++)
        {
            var cpu = RandomInRange(0.85, 1.0);
            var mem = RandomInRange(0.4, 0.65);
            var disk = RandomInRange(0.3, 0.6);
            var tpc = RandomInRange(0.7, 1.0);
            var heap = RandomInRange(0.4, 0.6);
            var input = MakeInput(cpu, mem, disk, tpc, heap, 0.7, 0.2, 0.1);
            var target = new double[] { 0.55, 0.3, 0.7 };
            samples.Add((input, target));
        }

        Log.Information("🧪 生成基准训练样本: {Count} 条", samples.Count);
        return samples;
    }

    private static double[] MakeInput(double cpu, double mem, double disk,
        double tpc, double heap, double load, double cpuTrend, double memTrend)
    {
        return new double[]
        {
            Math.Clamp(cpu, 0, 1),
            Math.Clamp(mem, 0, 1),
            Math.Clamp(disk, 0, 1),
            Math.Clamp(tpc, 0, 1),
            Math.Clamp(heap, 0, 1),
            Math.Clamp(heap * 0.5, 0, 1),
            Math.Clamp(cpuTrend + 0.5, 0, 1),
            Math.Clamp(memTrend + 0.5, 0, 1),
            Math.Clamp(disk * 0.5, 0, 1),
            Math.Clamp(load, 0, 1),
            Math.Clamp(tpc * 2, 0, 1),
            0.5
        };
    }

    private static readonly Random _rng = new(42);

    private static double RandomInRange(double min, double max)
    {
        return min + _rng.NextDouble() * (max - min);
    }
}
