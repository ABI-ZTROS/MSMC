namespace McServerGuard.Services.AIService;

using System.IO;
using McServerGuard.Models;
using McServerGuard.Services.AIService.NeuralNetwork;
using Serilog;

/// <summary>
/// 性能预测器 —— MLP 神经网络的封装
/// 负责：崩溃预测增强、性能评分、瓶颈分析、建议排序
/// 规则引擎兜底，MLP 提供非线性综合评估
/// </summary>
public class PerformancePredictor
{
    private readonly MlpNetwork _network;
    private readonly TrainingDataGenerator _dataGenerator;
    private readonly string _modelPath;
    private bool _isTrained;

    public bool IsTrained => _isTrained;

    public PerformancePredictor(TrainingDataGenerator dataGenerator)
    {
        _dataGenerator = dataGenerator;
        _network = new MlpNetwork(
            inputSize: TrainingDataGenerator.InputDimension,
            hidden1Size: 16,
            hidden2Size: 16,
            outputSize: TrainingDataGenerator.OutputDimension,
            learningRate: 0.005);

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var aiDir = Path.Combine(baseDir, "ai_learning");
        Directory.CreateDirectory(aiDir);
        _modelPath = Path.Combine(aiDir, "mlp_model.json");

        LoadOrTrainInitialModel();
    }

    private void LoadOrTrainInitialModel()
    {
        if (File.Exists(_modelPath))
        {
            try
            {
                var json = File.ReadAllText(_modelPath);
                var loaded = MlpNetwork.LoadFromJson(json);
                CopyNetworkWeights(loaded, _network);
                _isTrained = true;
                Log.Information("🧠 MLP 模型加载成功: {Path}", _modelPath);
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ MLP 模型加载失败，将重新训练: {Message}", ex.Message);
            }
        }

        TrainWithBaselineData();
    }

    private static void CopyNetworkWeights(MlpNetwork source, MlpNetwork target)
    {
        var type = typeof(MlpNetwork);
        var fields = type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.Name.EndsWith("Weights1") || field.Name.EndsWith("Weights2") ||
                field.Name.EndsWith("Biases1") || field.Name.EndsWith("Biases2") ||
                field.Name.StartsWith("_outputWeights") || field.Name.StartsWith("_outputBiases"))
            {
                var val = field.GetValue(source);
                field.SetValue(target, val);
            }
        }
    }

    private void TrainWithBaselineData()
    {
        Log.Information("🧠 开始用基准数据训练 MLP 模型...");

        var samples = _dataGenerator.GenerateBaselineSamples();
        var bestError = double.MaxValue;
        var patience = 0;
        const int maxEpochs = 500;
        const int patienceLimit = 30;

        for (int epoch = 0; epoch < maxEpochs; epoch++)
        {
            var shuffled = samples.OrderBy(_ => Guid.NewGuid()).ToList();
            var totalError = _network.TrainBatch(shuffled);

            if (totalError < bestError)
            {
                bestError = totalError;
                patience = 0;
            }
            else
            {
                patience++;
            }

            if (patience >= patienceLimit)
            {
                Log.Information("🧠 MLP 训练提前收敛: epoch={Epoch}, error={Error:F6}", epoch, bestError);
                break;
            }

            if (epoch % 100 == 0)
                Log.Debug("🧠 MLP 训练 epoch {Epoch}: error={Error:F6}", epoch, totalError);
        }

        _isTrained = true;
        SaveModel();
        Log.Information("🧠 MLP 基准训练完成，最终误差: {Error:F6}", bestError);
    }

    private void SaveModel()
    {
        try
        {
            var json = _network.SaveToJson();
            File.WriteAllText(_modelPath, json);
            Log.Debug("💾 MLP 模型已保存: {Path}", _modelPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "⚠️ MLP 模型保存失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 预测性能指标
    /// 输出：崩溃概率、性能评分、优化优先级
    /// </summary>
    public PerformancePrediction Predict(SystemMetrics metrics)
    {
        _dataGenerator.RecordMetrics(metrics);
        var input = _dataGenerator.MetricsToInput(metrics);
        var output = _network.Forward(input);

        return new PerformancePrediction(
            CrashProbability: Math.Round(output[0], 4),
            PerformanceScore: Math.Round(output[1], 4),
            OptimizationPriority: Math.Round(output[2], 4),
            BottleneckAnalysis: AnalyzeBottlenecks(input, output)
        );
    }

    /// <summary>
    /// 分析性能瓶颈 —— 通过特征贡献度分析
    /// </summary>
    private List<BottleneckInfo> AnalyzeBottlenecks(double[] input, double[] output)
    {
        var bottlenecks = new List<BottleneckInfo>();
        var featureNames = new[]
        {
            "CPU 使用率", "内存使用率", "磁盘使用率",
            "每核线程数", "Java 堆使用率", "Java 线程占比",
            "CPU 增长趋势", "内存增长趋势", "磁盘 I/O 强度",
            "系统负载", "进程数量", "时间因素"
        };

        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] > 0.7)
            {
                bottlenecks.Add(new BottleneckInfo(
                    Feature: featureNames[i],
                    Severity: input[i],
                    ImpactScore: input[i] * 0.8 + output[2] * 0.2
                ));
            }
        }

        return bottlenecks
            .OrderByDescending(b => b.ImpactScore)
            .Take(3)
            .ToList();
    }

    /// <summary>
    /// 在线学习 —— 用真实数据微调网络
    /// </summary>
    public void OnlineLearn(SystemMetrics metrics, double actualCrashOutcome, double actualPerformance)
    {
        var input = _dataGenerator.MetricsToInput(metrics);
        var target = new double[]
        {
            Math.Clamp(actualCrashOutcome, 0, 1),
            Math.Clamp(actualPerformance, 0, 1),
            Math.Clamp(actualCrashOutcome * 0.7 + (1 - actualPerformance) * 0.3, 0, 1)
        };

        _network.Train(input, target);
        SaveModel();
    }

    /// <summary>
    /// 用户反馈学习 —— 调整建议的置信度和权重
    /// </summary>
    public void FeedbackLearn(SystemMetrics metrics, bool suggestionHelped)
    {
        var input = _dataGenerator.MetricsToInput(metrics);
        var current = _network.Forward(input);

        var adjustedPriority = suggestionHelped
            ? Math.Clamp(current[2] + 0.05, 0, 1)
            : Math.Clamp(current[2] - 0.05, 0, 1);

        var target = new double[] { current[0], current[1], adjustedPriority };
        _network.Train(input, target);
        SaveModel();
    }
}

/// <summary>
/// 性能预测结果
/// </summary>
/// <param name="CrashProbability">崩溃概率 (0-1)</param>
/// <param name="PerformanceScore">性能评分 (0-1, 越高越好)</param>
/// <param name="OptimizationPriority">优化优先级 (0-1, 越高越紧急)</param>
/// <param name="BottleneckAnalysis">瓶颈分析列表</param>
public record PerformancePrediction(
    double CrashProbability,
    double PerformanceScore,
    double OptimizationPriority,
    List<BottleneckInfo> BottleneckAnalysis
);

/// <summary>
/// 瓶颈信息
/// </summary>
/// <param name="Feature">特征名称</param>
/// <param name="Severity">严重程度 (0-1)</param>
/// <param name="ImpactScore">影响评分</param>
public record BottleneckInfo(
    string Feature,
    double Severity,
    double ImpactScore
);
