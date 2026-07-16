using System.Text.RegularExpressions;
using McServerGuard.Constants;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>🧠 命令行参数解析器 —— 从一堆乱七八糟的命令行参数里提取有用情报</summary>
/// <remarks>
/// Java 命令行长得跟一坨意大利面似的，我们的任务就是把它理清楚 🍝
/// 支持 "4G"/"1024M"/"4096" 等各种内存写法，以及引号内的空格
/// </remarks>
public static partial class CommandLineParser
{
    /// <summary>📝 解析后的命令行数据 —— 结构化存储，告别字符串地狱</summary>
    public record ParsedCommandLine
    {
        /// <summary>☕ Java 可执行文件路径（带引号的那种也能识别）</summary>
        public string JavaPath { get; init; } = string.Empty;

        /// <summary>📦 JVM 参数列表（就是那些 -XX: 开头的玄学参数）</summary>
        public List<string> JvmArguments { get; init; } = [];

        /// <summary>🏷️ -jar 后面的 JAR 文件名（服务器的心脏）</summary>
        public string JarFileName { get; init; } = string.Empty;

        /// <summary>📂 -jar 后面的 JAR 完整路径（可能包含路径信息）</summary>
        public string JarFilePath { get; init; } = string.Empty;

        /// <summary>📋 -jar 之后、nogui 之前的"服务器参数"（如 nogui、world 名称等）</summary>
        public List<string> ServerArguments { get; init; } = [];

        /// <summary>💾 初始堆内存（字节数）—— -Xms 的值</summary>
        public long InitialHeapMemoryBytes { get; init; }

        /// <summary>💾 最大堆内存（字节数）—— -Xmx 的值，分配少了服务器卡成狗</summary>
        public long MaxHeapMemoryBytes { get; init; }

        /// <summary>🗑️ GC 类型（G1/ZGC/Shenandoah/Parallel），垃圾回收哪家强？</summary>
        public string GcType { get; init; } = string.Empty;

        /// <summary>⚡ 是否使用 Aikar 优化标志（Paper 社区的"大力丸"）</summary>
        public bool UsesAikarFlags { get; init; }

        /// <summary>🖥️ 是否包含 nogui 参数（服务器不需要花里胡哨的图形界面）</summary>
        public bool HasNoGui { get; init; }

        /// <summary>🚫 是否包含客户端标志 —— 有的话说明这是个假服务器（其实是客户端）</summary>
        public bool HasClientMarkers { get; init; }
    }

    /// <summary>🔪 把命令行字符串切成结构化数据</summary>
    /// <param name="commandLine">完整的命令行字符串，可能包含引号</param>
    /// <returns>解析后的命令行数据，保证不会空引用（但字段可能为空字符串）</returns>
    public static ParsedCommandLine Parse(string commandLine)
    {
        Log.Debug("🔧 解析命令行: {Cmd}", commandLine?.Length > 100 ? commandLine[..100] + "..." : commandLine);

        if (string.IsNullOrWhiteSpace(commandLine))
            return new ParsedCommandLine();

        // 第一步：tokenize —— 处理引号内的空格（不然 "C:\Program Files\java.exe" 会被切碎）
        var tokens = Tokenize(commandLine);
        if (tokens.Count == 0)
            return new ParsedCommandLine();

        // 第二步：识别 Java 路径 —— 第一个 token 就是 Java 可执行文件
        var javaPath = tokens[0];

        // 第三步：检查客户端标志 —— 发现就拉响警报 🚨
        var hasClientMarkers = tokens.Any(t =>
            ServerConstants.ClientProcessMarkers.Any(marker =>
                t.Contains(marker, StringComparison.OrdinalIgnoreCase)));

        // 第四步：遍历所有 token，提取有用参数
        var jvmArguments = new List<string>();
        var serverArguments = new List<string>();
        long initialHeapBytes = 0;
        long maxHeapBytes = 0;
        var gcType = string.Empty;
        var usesAikarFlags = false;
        var hasNoGui = false;
        var jarFileName = string.Empty;
        var jarFilePath = string.Empty;

        bool foundJar = false; // 找到 -jar 之后的参数统统归入"服务器参数"

        for (int i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            Log.Debug("  ▶ 解析 Token[{Index}]: {Token}", i, token);

            // 🏷️ 已经找到 -jar 了，后面的参数都是服务器参数
            if (foundJar)
            {
                serverArguments.Add(token);
                // 检查 nogui（服务器经典参数，客户端不会带这个）
                if (token.Equals(JvmArgumentConstants.NoGuiLegacy, StringComparison.OrdinalIgnoreCase) ||
                    token.Equals(JvmArgumentConstants.NoGuiModern, StringComparison.OrdinalIgnoreCase))
                {
                    hasNoGui = true;
                }
                continue;
            }

            // 🫙 内存参数解析 —— 最关键的两个参数
            if (token.StartsWith(JvmArgumentConstants.InitialHeapMemory, StringComparison.OrdinalIgnoreCase))
            {
                initialHeapBytes = ParseMemoryValue(token[JvmArgumentConstants.InitialHeapMemory.Length..]);
                jvmArguments.Add(token);
            }
            else if (token.StartsWith(JvmArgumentConstants.MaxHeapMemory, StringComparison.OrdinalIgnoreCase))
            {
                maxHeapBytes = ParseMemoryValue(token[JvmArgumentConstants.MaxHeapMemory.Length..]);
                jvmArguments.Add(token);
            }
            // 🗑️ GC 类型检测
            else if (token.Equals(JvmArgumentConstants.G1GC, StringComparison.OrdinalIgnoreCase))
            {
                gcType = "G1GC";
                jvmArguments.Add(token);
            }
            else if (token.Equals(JvmArgumentConstants.ZGC, StringComparison.OrdinalIgnoreCase))
            {
                gcType = "ZGC";
                jvmArguments.Add(token);
            }
            else if (token.Equals(JvmArgumentConstants.ShenandoahGC, StringComparison.OrdinalIgnoreCase))
            {
                gcType = "ShenandoahGC";
                jvmArguments.Add(token);
            }
            else if (token.Equals(JvmArgumentConstants.ParallelGC, StringComparison.OrdinalIgnoreCase))
            {
                gcType = "ParallelGC";
                jvmArguments.Add(token);
            }
            // ⚡ Aikar 标志检测
            else if (token.StartsWith(JvmArgumentConstants.AikarFlagIdentifier, StringComparison.OrdinalIgnoreCase) ||
                     token.Equals(JvmArgumentConstants.AikarNewFlagIdentifier, StringComparison.OrdinalIgnoreCase))
            {
                usesAikarFlags = true;
                jvmArguments.Add(token);
            }
            // 🫙 -jar 标志 —— 后面紧跟的就是 JAR 文件名！
            else if (token.Equals(JvmArgumentConstants.JarFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < tokens.Count)
                {
                    i++; // 前进一步，读取 JAR 路径
                    jarFilePath = tokens[i];
                    jarFileName = System.IO.Path.GetFileName(tokens[i]);
                    foundJar = true;
                }
            }
            else
            {
                // 其他 JVM 参数一股脑塞进去，后面再说
                jvmArguments.Add(token);
            }
        }

        var normalizedJvmArgs = JvmArgumentNormalizer.Normalize(jvmArguments);

        if (normalizedJvmArgs.Warnings.Count > 0)
        {
            Log.Debug("📝 命令行解析规范化警告: {Count} 条", normalizedJvmArgs.Warnings.Count);
            foreach (var warning in normalizedJvmArgs.Warnings)
            {
                Log.Debug("  ⚠️ {Warning}", warning);
            }
        }

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

    /// <summary>📏 把 "4G"/"1024M"/"4096" 这样的内存值转换为字节数</summary>
    /// <param name="value">内存字符串，支持 G/M/K 后缀（大小写不敏感），也支持纯数字（默认按 MB）</param>
    /// <returns>转换后的字节数，解析失败返回 0（你的内存参数写得也太离谱了吧？）</returns>
    public static long ParseMemoryValue(string value)
    {
        Log.Debug("📏 解析内存值: {Value}", value);
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        value = value.Trim();
        // 去掉末尾可能带的单位引号或空格，Java 命令行的"惊喜"可真多
        value = value.Trim('\'', '"', ' ');

        // 纯数字 → 默认按 MB 处理（很多脚本偷懒不写单位）
        if (long.TryParse(value, out var numericValue))
            return numericValue * 1024L * 1024L;

        // 带单位的解析 —— 用正则把数字和单位分开
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
            _ => 0 // 未知单位？那这参数你写的也太难伺候了 🤷
        };
    }

    /// <summary>✂️ 命令行分词器 —— 正确处理引号内的空格</summary>
    /// <remarks>
    /// 这个分词器支持双引号和单引号，嵌套引号就别指望了（谁命令行写嵌套引号啊？）
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
                        // 引号本身不加入 token，但它内部的空格要保留
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
                        // 连续空格就直接忽略，毕竟命令行又不是诗歌需要空行
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

        // 🏁 最后一个 token 别忘了（它没有后面的空格来触发加入）
        if (currentToken.Length > 0)
            tokens.Add(currentToken.ToString());

        return tokens;
    }

    // 🎯 内存值正则 —— 匹配 "4G"/"1024M"/"4096" 这类写法
    [GeneratedRegex(@"^(?<number>\d+)\s*(?<unit>[GgMmKk][Bb]?)$")]
    private static partial Regex MemoryValueRegex();
}
