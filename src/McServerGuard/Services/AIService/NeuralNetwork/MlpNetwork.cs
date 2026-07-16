namespace McServerGuard.Services.AIService.NeuralNetwork;

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

/// <summary>
/// 轻量级 MLP 神经网络实现 —— 纯 C# 手写，无外部依赖
/// 用于崩溃预测、性能评分和优化建议排序
/// 结构：输入层 → 隐藏层1(ReLU) → 隐藏层2(ReLU) → 输出层(Sigmoid)
/// </summary>
public class MlpNetwork
{
    private readonly int _inputSize;
    private readonly int[] _hiddenSizes;
    private readonly int _outputSize;

    private double[][] _hiddenWeights1;
    private double[] _hiddenBiases1;
    private double[][] _hiddenWeights2;
    private double[] _hiddenBiases2;
    private double[][] _outputWeights;
    private double[] _outputBiases;

    private readonly double _learningRate;
    private readonly object _lock = new();

    [JsonIgnore]
    public int InputSize => _inputSize;

    [JsonIgnore]
    public int OutputSize => _outputSize;

    public MlpNetwork(int inputSize, int hidden1Size, int hidden2Size, int outputSize, double learningRate = 0.01)
    {
        _inputSize = inputSize;
        _hiddenSizes = [hidden1Size, hidden2Size];
        _outputSize = outputSize;
        _learningRate = learningRate;

        _hiddenWeights1 = CreateMatrix(inputSize, hidden1Size);
        _hiddenBiases1 = new double[hidden1Size];
        _hiddenWeights2 = CreateMatrix(hidden1Size, hidden2Size);
        _hiddenBiases2 = new double[hidden2Size];
        _outputWeights = CreateMatrix(hidden2Size, outputSize);
        _outputBiases = new double[outputSize];

        InitializeWeights();
    }

    private static double[][] CreateMatrix(int rows, int cols)
    {
        var matrix = new double[rows][];
        for (var i = 0; i < rows; i++)
            matrix[i] = new double[cols];
        return matrix;
    }

    /// <summary>
    /// Xavier 初始化权重
    /// </summary>
    private void InitializeWeights()
    {
        var rand = new Random(42);

        InitializeLayer(_hiddenWeights1, _hiddenBiases1, _inputSize, rand);
        InitializeLayer(_hiddenWeights2, _hiddenBiases2, _hiddenSizes[0], rand);
        InitializeLayer(_outputWeights, _outputBiases, _hiddenSizes[1], rand);

        Log.Information("🧠 MLP 网络初始化完成: {In} → {H1} → {H2} → {Out}",
            _inputSize, _hiddenSizes[0], _hiddenSizes[1], _outputSize);
    }

    private static void InitializeLayer(double[][] weights, double[] biases, int fanIn, Random rand)
    {
        var scale = Math.Sqrt(2.0 / fanIn);
        for (var i = 0; i < weights.Length; i++)
            for (var j = 0; j < weights[i].Length; j++)
                weights[i][j] = (rand.NextDouble() * 2 - 1) * scale;

        for (var i = 0; i < biases.Length; i++)
            biases[i] = 0;
    }

    /// <summary>
    /// 前向传播 —— 推理
    /// </summary>
    public double[] Forward(double[] input)
    {
        if (input.Length != _inputSize)
            throw new ArgumentException($"输入维度不匹配: 期望 {_inputSize}, 实际 {input.Length}");

        lock (_lock)
        {
            var hidden1 = ActivateLayer(input, _hiddenWeights1, _hiddenBiases1, ReLU);
            var hidden2 = ActivateLayer(hidden1, _hiddenWeights2, _hiddenBiases2, ReLU);
            var output = ActivateLayer(hidden2, _outputWeights, _outputBiases, Sigmoid);
            return output;
        }
    }

    private static double[] ActivateLayer(double[] input, double[][] weights, double[] biases, Func<double, double> activation)
    {
        var outputCount = biases.Length;
        var output = new double[outputCount];

        for (var j = 0; j < outputCount; j++)
        {
            var sum = biases[j];
            for (var i = 0; i < input.Length; i++)
                sum += input[i] * weights[i][j];
            output[j] = activation(sum);
        }

        return output;
    }

    /// <summary>
    /// 单次训练（反向传播）
    /// </summary>
    public double Train(double[] input, double[] target)
    {
        if (input.Length != _inputSize)
            throw new ArgumentException($"输入维度不匹配");
        if (target.Length != _outputSize)
            throw new ArgumentException($"输出维度不匹配");

        lock (_lock)
        {
            var (hidden1, hidden2, output) = ForwardWithIntermediates(input);

            var outputError = new double[_outputSize];
            var totalError = 0.0;
            for (var i = 0; i < _outputSize; i++)
            {
                outputError[i] = (output[i] - target[i]) * SigmoidDerivative(output[i]);
                totalError += 0.5 * Math.Pow(target[i] - output[i], 2);
            }

            var hidden2Error = new double[_hiddenSizes[1]];
            for (var i = 0; i < _hiddenSizes[1]; i++)
            {
                var error = 0.0;
                for (var j = 0; j < _outputSize; j++)
                    error += outputError[j] * _outputWeights[i][j];
                hidden2Error[i] = error * ReLUDerivative(hidden2[i]);
            }

            var hidden1Error = new double[_hiddenSizes[0]];
            for (var i = 0; i < _hiddenSizes[0]; i++)
            {
                var error = 0.0;
                for (var j = 0; j < _hiddenSizes[1]; j++)
                    error += hidden2Error[j] * _hiddenWeights2[i][j];
                hidden1Error[i] = error * ReLUDerivative(hidden1[i]);
            }

            UpdateWeights(_outputWeights, _outputBiases, hidden2, outputError);
            UpdateWeights(_hiddenWeights2, _hiddenBiases2, hidden1, hidden2Error);
            UpdateWeights(_hiddenWeights1, _hiddenBiases1, input, hidden1Error);

            return totalError / _outputSize;
        }
    }

    private (double[] Hidden1, double[] Hidden2, double[] Output) ForwardWithIntermediates(double[] input)
    {
        var hidden1 = ActivateLayer(input, _hiddenWeights1, _hiddenBiases1, ReLU);
        var hidden2 = ActivateLayer(hidden1, _hiddenWeights2, _hiddenBiases2, ReLU);
        var output = ActivateLayer(hidden2, _outputWeights, _outputBiases, Sigmoid);
        return (hidden1, hidden2, output);
    }

    private void UpdateWeights(double[][] weights, double[] biases, double[] activation, double[] error)
    {
        for (var j = 0; j < biases.Length; j++)
        {
            biases[j] -= _learningRate * error[j];
            for (var i = 0; i < activation.Length; i++)
                weights[i][j] -= _learningRate * activation[i] * error[j];
        }
    }

    /// <summary>
    /// 批量训练
    /// </summary>
    public double TrainBatch(List<(double[] Input, double[] Target)> batch)
    {
        if (batch.Count == 0) return 0;

        var totalError = 0.0;
        foreach (var (input, target) in batch)
            totalError += Train(input, target);

        return totalError / batch.Count;
    }

    #region 激活函数

    private static double ReLU(double x) => Math.Max(0, x);
    private static double ReLUDerivative(double x) => x > 0 ? 1 : 0;

    private static double Sigmoid(double x)
    {
        if (x > 20) return 1.0;
        if (x < -20) return 0.0;
        return 1.0 / (1.0 + Math.Exp(-x));
    }

    private static double SigmoidDerivative(double sigmoidOutput) => sigmoidOutput * (1 - sigmoidOutput);

    #endregion

    #region 序列化

    public string SaveToJson()
    {
        lock (_lock)
        {
            var data = new MlpNetworkData
            {
                InputSize = _inputSize,
                Hidden1Size = _hiddenSizes[0],
                Hidden2Size = _hiddenSizes[1],
                OutputSize = _outputSize,
                HiddenWeights1 = _hiddenWeights1,
                HiddenBiases1 = _hiddenBiases1,
                HiddenWeights2 = _hiddenWeights2,
                HiddenBiases2 = _hiddenBiases2,
                OutputWeights = _outputWeights,
                OutputBiases = _outputBiases
            };
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public static MlpNetwork LoadFromJson(string json)
    {
        var data = JsonSerializer.Deserialize<MlpNetworkData>(json);
        if (data == null)
            throw new InvalidDataException("无法解析网络数据");

        var network = new MlpNetwork(data.InputSize, data.Hidden1Size, data.Hidden2Size, data.OutputSize)
        {
            _hiddenWeights1 = data.HiddenWeights1,
            _hiddenBiases1 = data.HiddenBiases1,
            _hiddenWeights2 = data.HiddenWeights2,
            _hiddenBiases2 = data.HiddenBiases2,
            _outputWeights = data.OutputWeights,
            _outputBiases = data.OutputBiases
        };

        Log.Information("🧠 MLP 网络从 JSON 加载成功");
        return network;
    }

    private class MlpNetworkData
    {
        public int InputSize { get; set; }
        public int Hidden1Size { get; set; }
        public int Hidden2Size { get; set; }
        public int OutputSize { get; set; }
        public double[][] HiddenWeights1 { get; set; } = [];
        public double[] HiddenBiases1 { get; set; } = [];
        public double[][] HiddenWeights2 { get; set; } = [];
        public double[] HiddenBiases2 { get; set; } = [];
        public double[][] OutputWeights { get; set; } = [];
        public double[] OutputBiases { get; set; } = [];
    }

    #endregion
}
