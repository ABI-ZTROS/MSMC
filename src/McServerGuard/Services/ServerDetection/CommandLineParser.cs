// -----------------------------------------------------------------------------
// 文件名: CommandLineParser.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: Java命令行语义解析器，实现Token化分词、JVM参数提取、内存规格解析、GC策略识别及Aikar标志检测，输出结构化命令行数据模型
// 依赖组件: System.Text.RegularExpressions, Serilog, McServerGuard.Constants
// 设计模式: 流水线模式（Tokenize→分类提取→规范化）、值对象（ParsedCommandLine）
// -----------------------------------------------------------------------------
using System.Text.RegularExpressions;
using McServerGuard.Constants;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>
/// Java命令行解析器 —— 将原始命令行字符串解析为结构化数据模型
/// </summary>
/// <remarks>
/// 解析流水线分为三个阶段：
/// 1. 词法分析（Tokenize）：处理引号嵌套，将命令行切分为Token序列
/// 2. 语义提取：遍历Token序列，分类提取JVM参数、JAR路径、服务器参数
/// 3. 规范化处理：调用<see cref="JvmArgumentNormalizer"/>对JVM参数进行去重与规范化
/// 
/// 支持内存规格解析：4G / 1024M / 4096（默认MB）等多种写法
/// </remarks>
public static partial class CommandLineParser
{
    /// <summary>
    /// 解析后的命令行数据结构 —— 以强类型方式存储各维度提取结果
    /// </summary>
    public record ParsedCommandLine
    {
        /// <summary>
        /// Java可执行文件路径（支持带引号的路径格式）
        /// </summary>
        public string JavaPath { get; init; } = string.Empty;

        /// <summary>
        /// JVM参数列表（以-XX:、-X等前缀标识的虚拟机参数）
        /// </summary>
        public List<string> JvmArguments { get; init; } = [];

        /// <summary>
        /// -jar参数指定的JAR文件名（不含路径）
        /// </summary>
        public string JarFileName { get; init; } = string.Empty;

        /// <summary>
        /// -jar参数指定的JAR完整路径（可能包含相对路径信息）
        /// </summary>
        public string JarFilePath { get; init; } = string.Empty;

        /// <summary>
        /// 服务器参数列表（-jar之后的应用层参数，如nogui、world名称等）
        /// </summary>
        public List<string> ServerArguments { get; init; } = [];

        /// <summary>
        /// 初始堆内存（字节数）—— 对应JVM参数-Xms
        /// </summary>
        public long InitialHeapMemoryBytes { get; init; }

        /// <summary>
        /// 最大堆内存（字节数）—— 对应JVM参数-Xmx
        /// </summary>
        public long MaxHeapMemoryBytes { get; init; }

        /// <summary>
        /// 垃圾回收器类型标识（G1GC / ZGC / ShenandoahGC / ParallelGC）
        /// </summary>
        public string GcType { get; init; } = string.Empty;

        /// <summary>
        /// 是否使用Aikar优化标志集（Paper社区推荐的JVM调优参数组合）
        /// </summary>
        public bool UsesAikarFlags { get; init; }

        /// <summary>
        /// 是否包含nogui参数（服务器无图形界面模式标识）
        /// </summary>
        public bool HasNoGui { get; init; }

        /// <summary>
        /// 是否包含客户端特征标志（用于判定进程是否为客户端而非服务器）
        /// </summary>
        public bool HasClientMarkers { get; init; }
    }

    /// <summary>
    /// 解析Java命令行字符串，输出结构化数据模型
    /// </summary>
    /// <param name="commandLine">完整的命令行字符串，可能包含引号包裹的路径</param>
    /// <returns>解析后的<see cref="ParsedCommandLine"/>对象（非空引用，字段可能为空字符串）</returns>
    public static ParsedCommandLine Parse(string commandLine)
    {
        Log.Debug("🔧 解析命令行: {Cmd}", commandLine?.Length > 100 ? commandLine[..100] + "..." : commandLine);

        if (string.IsNullOrWhiteSpace(commandLine))
            return new ParsedCommandLine();

        // 阶段一：词法分析 —— 处理引号内的空格，生成Token序列
        var tokens = Tokenize(commandLine);
        if (tokens.Count == 0)
            return new ParsedCommandLine();

        // 阶段二：语义提取 —— 识别Java可执行文件路径
        var javaPath = tokens[0];

        // 阶段二：客户端特征检测 —— 快速判定是否为客户端进程
        var hasClientMarkers = tokens.Any(t =>
            ServerConstants.ClientProcessMarkers.Any(marker =>
                t.Contains(marker, StringComparison.OrdinalIgnoreCase)));

        // 阶段二：遍历Token序列，分类提取各维度参数
        var jvmArguments = new List<string>();
        var serverArguments = new List<string>();
        long initialHeapBytes = 0;
        long maxHeapBytes = 0;
        var gcType = string.Empty;
        var usesAikarFlags = false;
        var hasNoGui = false;
        var jarFileName = string.Empty;
        var jarFilePath = string.Empty;

        bool foundJar = false; // 状态标记：找到-jar后，后续Token归入服务器参数

        for (int i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            Log.Debug("  ▶ 解析 Token[{Index}]: {Token}", i, token);

            // 已越过-jar参数，后续Token全部归类为服务器参数
            if (foundJar)
            {
                serverArguments.Add(token);
                // 检测nogui标志（服务器经典参数，客户端不会携带）
                if (token.Equals(JvmArgumentConstants.NoGuiLegacy, StringComparison.OrdinalIgnoreCase) ||
                    token.Equals(JvmArgumentConstants.NoGuiModern, StringComparison.OrdinalIgnoreCase))
                {
                    hasNoGui = true;
                }
                continue;
            }

            // 内存参数解析：-Xms
            if (token.StartsWith(JvmArgumentConstants.InitialHeapMemory, StringComparison.OrdinalIgnoreCase))
            {
                initialHeapBytes = ParseMemoryValue(token[JvmArgumentConstants.InitialHeapMemory.Length..]);
                jvmArguments.Add(token);
            }
            // 内存参数解析：-Xmx
            else if (token.StartsWith(JvmArgumentConstants.MaxHeapMemory, StringComparison.OrdinalIgnoreCase))
            {
                maxHeapBytes = ParseMemoryValue(token[JvmArgumentConstants.MaxHeapMemory.Length..]);
                jvmArguments.Add(token);
            }
            // GC类型检测：G1GC
            else if (token.Equals(JvmArgumentConstants.G1GC, StringComparison.OrdinalIgnoreCase))
            {
                gcType = "G1GC";
                jvmArguments.Add(token);
            }
            // GC类型检测：ZGC
            else if (token.Equals(JvmArgumentConstants.ZGC, StringComparison.OrdinalIgnoreCase))
            {
                gcType = "ZGC";
                jvmArguments.Add(token);
            }
            // GC类型检测：ShenandoahGC
            else if (token.Equals(JvmArgumentConstants.ShenandoahGC, StringComparison.OrdinalIgnoreCase))
            {
                gcType = "ShenandoahGC";
                jvmArguments.Add(token);
            }
            // GC类型检测：ParallelGC
            else if (token.Equals(JvmArgumentConstants.ParallelGC, StringComparison.OrdinalIgnoreCase))
            {
                gcType = "ParallelGC";
                jvmArguments.Add(token);
            }
            // Aikar优化标志检测
            else if (token.StartsWith(JvmArgumentConstants.AikarFlagIdentifier, StringComparison.OrdinalIgnoreCase) ||
                     token.Equals(JvmArgumentConstants.AikarNewFlagIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                usesAikarFlags = true;
                jvmArguments.Add(token);
            }
            // -jar参数：后续Token切换为服务器参数语义
            else if (token.Equals(JvmArgumentConstants.JarFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < tokens.Count)
                {
                    i++; // 前进一位，读取JAR路径
                    jarFilePath = tokens[i];
                    jarFileName = System.IO.Path.GetFileName(tokens[i]);
                    foundJar = true;
                }
            }
            else
            {
                // 其他JVM参数直接收集，后续统一处理
                jvmArguments.Add(token);
            }
        }

        // 阶段三：JVM参数规范化 —— 去重、排序、标准化
        var normalizedJvmArgs = JvmArgumentNormalizer.Normalize(jvmArguments);

        if (normalizedJvmArgs.Warnings.Count > 0)
        {
            Log.Debug("📝 命令行解析规范化警告: {Count} 条", normalizedJvmArgs.Warnings.Count);
            foreach (var warning in normalizedJvmArgs.Warnings)
            {
                Log.Debug("  ⚠️ {Warning}", warning);
            }
        }

        // 规范化后参数数量变化时，重新提取内存参数（可能新增了规范化后的Xms/Xmx）
        if (normalizedJvmArgs.Arguments.Count != jvmArguments.Count)
        {
            Log.Debug("🧹 JVM 参数规范化: {Original} → {Final} 个", jvmArguments.Count, normalizedJvmArgs.Arguments.Count);
            jvmArguments = normalizedJvmArgs.Arguments;

            if (initialHeapBytes == 0)
            {
                var xms = jvmArguments.FirstOrDefault(a => a.StartsWith("-Xms", StringComparison.OrdinalIgnoreCase));
                if (xms != null)
                    initialHeapBytes = ParseMemoryValue(xms[4..]);
            }
            if (maxHeapBytes == 0)
            {
                var xmx = jvmArguments.FirstOrDefault(a => a.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase));
                if (xmx != null)
                    maxHeapBytes = ParseMemoryValue(xmx[4..]);
            }
        }

        return new ParsedCommandLine
        {
            JavaPath = javaPath,
            JvmArguments = jvmArguments,
            JarFileName = jarFileName,
            JarFilePath = jarFilePath,
            ServerArguments = serverArguments,
            InitialHeapMemoryBytes = initialHeapBytes,
            MaxHeapMemoryBytes = maxHeapBytes,
            GcType = gcType,
            UsesAikarFlags = usesAikarFlags,
            HasNoGui = hasNoGui,
            HasClientMarkers = hasClientMarkers
        };
    }

    /// <summary>
    /// 将内存规格字符串（如"4G"/"1024M"/"4096"）转换为字节数值
    /// </summary>
    /// <param name="value">内存规格字符串，支持G/M/K后缀（大小写不敏感），纯数字默认按MB解析</param>
    /// <returns>转换后的字节数；解析失败返回0</returns>
    public static long ParseMemoryValue(string value)
    {
        Log.Debug("📏 解析内存值: {Value}", value);
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        value = value.Trim();
        // 清除末尾可能携带的引号或空白字符
        value = value.Trim('\'', '"', ' ');

        // 纯数字 → 默认按MB解析（兼容脚本中省略单位的写法）
        if (long.TryParse(value, out var numericValue))
            return numericValue * 1024L * 1024L;

        // 带单位的解析：使用正则分离数值与单位
        var match = MemoryValueRegex().Match(value);
        if (!match.Success)
            return 0;

        var number = long.Parse(match.Groups["number"].Value);
        var unit = match.Groups["unit"].Value.ToUpperInvariant();

        return unit switch
        {
            "G" or "GB" => number * 1024L * 1024L * 1024L,
            "M" or "MB" => number * 1024L * 1024L,
            "K" or "KB" => number * 1024L,
            _ => 0
        };
    }

    /// <summary>
    /// 命令行词法分析器 —— 正确处理引号内的空格，生成Token序列
    /// </summary>
    /// <param name="commandLine">原始命令行字符串</param>
    /// <returns>切分后的Token列表</returns>
    /// <remarks>
    /// 支持双引号与单引号两种包裹方式，不支持嵌套引号。
    /// 示例：java -Xmx"4G" -jar "my server.jar" → ["java", "-Xmx4G", "-jar", "my server.jar"]
    /// </remarks>
    private static List<string> Tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var currentToken = new System.Text.StringBuilder();
        bool inDoubleQuote = false;
        bool inSingleQuote = false;

        for (int i = 0; i < commandLine.Length; i++)
        {
            var ch = commandLine[i];

            switch (ch)
            {
                case '"':
                    if (!inSingleQuote)
                    {
                        inDoubleQuote = !inDoubleQuote;
                        // 引号本身不进入Token，但引号内部的空格需保留
                    }
                    break;
                case '\'':
                    if (!inDoubleQuote)
                    {
                        inSingleQuote = !inSingleQuote;
                    }
                    break;
                case ' ' or '\t':
                    if (!inDoubleQuote && !inSingleQuote)
                    {
                        if (currentToken.Length > 0)
                        {
                            tokens.Add(currentToken.ToString());
                            currentToken.Clear();
                        }
                        // 连续空格直接跳过
                    }
                    else
                    {
                        currentToken.Append(ch);
                    }
                    break;
                default:
                    currentToken.Append(ch);
                    break;
            }
        }

        // 处理最后一个Token（无末尾空格触发加入）
        if (currentToken.Length > 0)
            tokens.Add(currentToken.ToString());

        return tokens;
    }

    /// <summary>
    /// 内存值正则表达式 —— 匹配"4G"/"1024M"/"4096"等内存规格写法
    /// </summary>
    [GeneratedRegex(@"^(?<number>\d+)\s*(?<unit>[GgMmKk][Bb]?)$")]
    private static partial Regex MemoryValueRegex();
}
