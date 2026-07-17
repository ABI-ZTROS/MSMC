// -----------------------------------------------------------------------------
// 文件名: StartupScriptDetector.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 启动脚本检测器，基于正则模式匹配的启发式脚本内容分析引擎
// 依赖组件: System.Text.RegularExpressions, McServerGuard.Models, Serilog
// 设计模式: 规则引擎模式、模式匹配、启发式判定
// -----------------------------------------------------------------------------
using System.Text.RegularExpressions;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>
/// 启动脚本检测器
/// </summary>
/// <remarks>
/// <para>基于内容架构特征而非文件名来判定脚本是否为 Minecraft 服务器启动脚本。
/// 文件名千变万化（start.bat、run.sh、launch.cmd 等），但脚本的内容结构
/// 具有高度可识别的模式特征，本类即通过多规则模式匹配实现智能识别。</para>
/// <para>识别策略：通过九条独立的检测规则进行模式匹配，命中规则数达到阈值
/// （默认 2 条）即认定为 Minecraft 服务器启动脚本。</para>
/// </remarks>
public static partial class StartupScriptDetector
{
    /// <summary>
    /// 检测规则集合 —— 每条规则对应启动脚本的一个特征维度
    /// </summary>
    private static readonly DetectionRule[] DetectionRules =
    [
        new(
            Name: "java-command",
            Pattern: @"(?i)(?:^|\n|&&|;|`)\s*(?:^|\b)(?:java|""[^""]*java(?:\.exe)?""|'[^']*java(?:\.exe)?')\b",
            Description: "包含 java 命令调用"
        ),
        new(
            Name: "jar-argument",
            Pattern: @"(?i)-jar\s+[\w\.\-/\\]+\.jar",
            Description: "包含 -jar 参数和 JAR 文件"
        ),
        new(
            Name: "memory-params",
            Pattern: @"(?i)-X(?:ms|mx)\d+[GgMmKk]?",
            Description: "包含 JVM 内存参数"
        ),
        new(
            Name: "nogui-argument",
            Pattern: @"(?i)(?:^|\s)--?nogui(?:\s|$)",
            Description: "包含 nogui 参数"
        ),
        new(
            Name: "auto-restart-loop",
            Pattern: @"(?i)(?:while\s+true|while\s*\(\s*1\s*\)|\bwhile\s+:)|for\s+.*\b(?:restart|loop)\b.*\bsleep\b",
            Description: "包含自动重启循环结构"
        ),
        new(
            Name: "sleep-java-combo",
            Pattern: @"(?i)sleep\s+\d+.*java|java.*sleep\s+\d+",
            Description: "包含 sleep 和 java 组合（自动重启标志）"
        ),
        new(
            Name: "server-properties-ref",
            Pattern: @"(?i)server\.properties",
            Description: "引用了 server.properties 文件"
        ),
        new(
            Name: "minecraft-jar-name",
            Pattern: @"(?i)\b(?:minecraft_server|spigot|paper|folia|forge|fabric-server-launch|craftbukkit)\b",
            Description: "包含 Minecraft 服务器特征 JAR 名称"
        ),
        new(
            Name: "script-self-directory",
            Pattern: @"(?i)(?:cd\s+/d\s+""%~dp0""|cd\s+""\$\(dirname\s+""\$0""\)"")",
            Description: "脚本切换到自身所在目录"
        ),
    ];

    /// <summary>
    /// 检测规则记录 —— 单条规则的完整描述
    /// </summary>
    /// <param name="Name">规则标识符</param>
    /// <param name="Pattern">正则表达式模式</param>
    /// <param name="Description">规则描述文本</param>
    private record DetectionRule(string Name, string Pattern, string Description);

    /// <summary>
    /// 分析脚本内容，判定是否为 Minecraft 服务器启动脚本
    /// </summary>
    /// <param name="content">脚本文件的原始文本内容</param>
    /// <returns>启动脚本分析结果对象</returns>
    /// <remarks>
    /// 当命中规则数达到 2 条及以上时，判定为 Minecraft 服务器启动脚本。
    /// 确认后将进一步提取 Java 路径、内存配置、JAR 文件名等结构化信息。
    /// </remarks>
    public static StartupScriptInfo Analyze(string content)
    {
        Log.Debug("分析启动脚本内容，长度 {Len} 字符", content?.Length ?? 0);
        var info = new StartupScriptInfo { RawContent = content ?? string.Empty };

        if (string.IsNullOrWhiteSpace(content))
            return info;

        var matchedRules = new List<string>();

        foreach (var rule in DetectionRules)
        {
            if (Regex.IsMatch(content, rule.Pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                matchedRules.Add(rule.Name);
                Log.Debug("匹配规则: {Rule} → {Value}", rule.Description, rule.Name);
            }
        }

        info.MatchedRules = matchedRules;

        Log.Debug("启动脚本分析完成，匹配 {Count} 条规则", matchedRules.Count);

        info.IsServerStartupScript = matchedRules.Count >= 2;

        if (info.IsServerStartupScript)
        {
            info.JavaPath = ExtractJavaPath(content);
            info.MaxHeapMemoryBytes = ExtractMaxMemory(content);
            info.ServerJarName = ExtractServerJarName(content);
            info.HasAutoRestart = DetectAutoRestart(content);
            info.UsesAikarFlags = DetectAikarFlags(content);
        }

        return info;
    }

    /// <summary>
    /// 提取 Java 可执行文件路径
    /// </summary>
    /// <param name="content">脚本文件内容</param>
    /// <returns>Java 路径字符串；未提取到返回 null</returns>
    /// <remarks>
    /// 支持三种匹配模式：
    ///   1. 引号包裹的完整路径（如 "C:\Program Files\Java\bin\java.exe"）
    ///   2. 环境变量前缀路径（如 %JAVA_HOME%\bin\java 或 $JAVA_HOME/bin/java）
    ///   3. 简单 java 命令（依赖系统 PATH）
    /// </remarks>
    private static string? ExtractJavaPath(string content)
    {
        var quotedMatch = QuotedJavaPathRegex().Match(content);
        if (quotedMatch.Success)
            return quotedMatch.Groups["path"].Value;

        var envVarMatch = EnvVarJavaPathRegex().Match(content);
        if (envVarMatch.Success)
            return envVarMatch.Groups["path"].Value;

        var simpleMatch = SimpleJavaPathRegex().Match(content);
        if (simpleMatch.Success)
            return simpleMatch.Groups["path"].Value;

        return null;
    }

    /// <summary>
    /// 提取最大堆内存配置值（-Xmx）
    /// </summary>
    /// <param name="content">脚本文件内容</param>
    /// <returns>最大堆内存字节数；未提取到返回 0</returns>
    private static long ExtractMaxMemory(string content)
    {
        var match = MaxMemoryRegex().Match(content);
        if (!match.Success)
            return 0;

        var valueStr = match.Groups["value"].Value;
        return CommandLineParser.ParseMemoryValue(valueStr);
    }

    /// <summary>
    /// 提取服务器 JAR 文件名
    /// </summary>
    /// <param name="content">脚本文件内容</param>
    /// <returns>JAR 文件名（不含路径）；未提取到返回 null</returns>
    private static string? ExtractServerJarName(string content)
    {
        var match = JarNameRegex().Match(content);
        if (!match.Success)
            return null;

        var jarPath = match.Groups["jar"].Value.Trim('\"', '\'');
        var separator = jarPath.Contains('/') ? '/' : '\\';
        var parts = jarPath.Split(separator);
        return parts[^1];
    }

    /// <summary>
    /// 检测脚本是否包含自动重启逻辑
    /// </summary>
    /// <param name="content">脚本文件内容</param>
    /// <returns>包含自动重启逻辑返回 true</returns>
    /// <remarks>
    /// 判定依据：
    ///   - while true / while(1) / while :（bash 无限循环模式）
    ///   - for 循环 + sleep 组合（重启循环模式）
    ///   - sleep 命令独立出现（简易重启模式）
    /// </remarks>
    private static bool DetectAutoRestart(string content)
    {
        var whileLoopMatch = WhileLoopRegex().Match(content);
        if (whileLoopMatch.Success)
            return true;

        var forLoopMatch = ForLoopRestartRegex().Match(content);
        if (forLoopMatch.Success)
            return true;

        var sleepMatch = SleepAloneRegex().Match(content);
        if (sleepMatch.Success)
            return true;

        return false;
    }

    /// <summary>
    /// 检测脚本是否使用 Aikar 优化标志
    /// </summary>
    /// <param name="content">脚本文件内容</param>
    /// <returns>使用 Aikar 标志返回 true</returns>
    private static bool DetectAikarFlags(string content)
    {
        return content.Contains("-Dusing.aikars.flags=", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("-Daikars.new.flags=true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 匹配引号包裹的 java 路径（支持双引号与单引号）
    /// </summary>
    [GeneratedRegex(@"(?<path>""[^""]*(?:java|javaw)(?:\.exe)?""|'[^']*(?:java|javaw)(?:\.exe)?')")]
    private static partial Regex QuotedJavaPathRegex();

    /// <summary>
    /// 匹配环境变量前缀的 java 路径（%JAVA_HOME% 或 $JAVA_HOME 形式）
    /// </summary>
    [GeneratedRegex(@"(?<path>(?:%[A-Za-z_]+%|\$[A-Za-z_]+)[/\\][^\s""']+java(?:w)?(?:\.exe)?)")]
    private static partial Regex EnvVarJavaPathRegex();

    /// <summary>
    /// 匹配简单 java 命令标识符
    /// </summary>
    [GeneratedRegex(@"(?<path>\bjava(?:w)?(?:\.exe)?\b)")]
    private static partial Regex SimpleJavaPathRegex();

    /// <summary>
    /// 匹配 -Xmx 内存参数值（支持引号包裹与无引号两种形式）
    /// </summary>
    [GeneratedRegex(@"-Xmx\s*[""'']?\s*(?<value>\d+\s*[GgMmKk]?[Bb]?)\s*[""'']?")]
    private static partial Regex MaxMemoryRegex();

    /// <summary>
    /// 匹配 -jar 参数后的 JAR 文件路径（支持变量、行拼接、引号包裹）
    /// </summary>
    [GeneratedRegex(@"-jar\s*[\^\s\r\n]*(?<jar>[\w\.\-/\\""'%$()]+\.jar)", RegexOptions.Multiline)]
    private static partial Regex JarNameRegex();

    /// <summary>
    /// 匹配 while 无限循环结构
    /// </summary>
    [GeneratedRegex(@"(?i)\bwhile\s+(?:true|\(\s*1\s*\)|:)")]
    private static partial Regex WhileLoopRegex();

    /// <summary>
    /// 匹配 for 循环重启模式
    /// </summary>
    [GeneratedRegex(@"(?i)\bfor\s+.*(?:restart|loop).*(?:sleep|java)")]
    private static partial Regex ForLoopRestartRegex();

    /// <summary>
    /// 匹配独立出现的 sleep 命令（通常暗示重启逻辑）
    /// </summary>
    [GeneratedRegex(@"(?im)^\s*sleep\s+\d+")]
    private static partial Regex SleepAloneRegex();
}
