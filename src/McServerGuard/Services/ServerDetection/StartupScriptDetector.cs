using System.Text.RegularExpressions;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>🔎 启动脚本检测器 —— 靠"内容架构"而非文件名来判断是否为 MC 服务器启动脚本</summary>
/// <remarks>
/// 有些人的启动脚本叫 start.sh，有些叫 run.bat，还有些叫 launch_me_please.cmd
/// 文件名千奇百怪，但内容结构是有迹可循的 —— 我们就是靠这个来识别的 🔍
/// </remarks>
public static partial class StartupScriptDetector
{
    /// <summary>📐 检测规则 —— 每条规则代表启动脚本的一个"特征"</summary>
    private static readonly DetectionRule[] DetectionRules =
    [
        // ☕ 规则1：包含 java 命令（启动脚本的核心标志）
        new(
            Name: "java-command",
            Pattern: @"(?i)(?:^|\n|&&|;|`)\s*(?:^|\b)(?:java|""[^""]*java(?:\.exe)?""|'[^']*java(?:\.exe)?')\b",
            Description: "包含 java 命令调用"
        ),
        // 🫙 规则2：包含 -jar 参数（启动 Minecraft 服务器必带）
        new(
            Name: "jar-argument",
            Pattern: @"(?i)-jar\s+[\w\.\-/\\]+\.jar",
            Description: "包含 -jar 参数和 JAR 文件"
        ),
        // 💾 规则3：包含内存参数（-Xms 或 -Xmx，谁开服不分配内存啊？）
        new(
            Name: "memory-params",
            Pattern: @"(?i)-X(?:ms|mx)\d+[GgMmKk]?",
            Description: "包含 JVM 内存参数"
        ),
        // 🖥️ 规则4：包含 nogui / --nogui 参数（服务器经典标志）
        new(
            Name: "nogui-argument",
            Pattern: @"(?i)(?:^|\s)--?nogui(?:\s|$)",
            Description: "包含 nogui 参数"
        ),
        // 🔄 规则5：自动重启循环（while/for 循环 + sleep，经典的"崩溃重启"模式）
        new(
            Name: "auto-restart-loop",
            Pattern: @"(?i)(?:while\s+true|while\s*\(\s*1\s*\)|\bwhile\s+:)|for\s+.*\b(?:restart|loop)\b.*\bsleep\b",
            Description: "包含自动重启循环结构"
        ),
        // 🔄 规则6：bash 的 sleep + java 组合（另一种常见的重启模式）
        new(
            Name: "sleep-java-combo",
            Pattern: @"(?i)sleep\s+\d+.*java|java.*sleep\s+\d+",
            Description: "包含 sleep 和 java 组合（自动重启标志）"
        ),
        // 📂 规则7：包含 server.properties 引用（对服务器配置文件有执念的脚本）
        new(
            Name: "server-properties-ref",
            Pattern: @"(?i)server\.properties",
            Description: "引用了 server.properties 文件"
        ),
        // 🎯 规则8：Minecraft 特征 JAR 名称（包含核心关键词的 JAR 文件）
        new(
            Name: "minecraft-jar-name",
            Pattern: @"(?i)\b(?:minecraft_server|spigot|paper|folia|forge|fabric-server-launch|craftbukkit)\b",
            Description: "包含 Minecraft 服务器特征 JAR 名称"
        ),
        // 📂 规则9：脚本自动切换到自身所在目录（start.bat 常见写法）
        new(
            Name: "script-self-directory",
            Pattern: @"(?i)(?:cd\s+/d\s+""%~dp0""|cd\s+""\$\(dirname\s+""\$0""\)"")",
            Description: "脚本切换到自身所在目录"
        ),
    ];

    /// <summary>📐 检测规则记录 —— 一条规则的完整描述</summary>
    private record DetectionRule(string Name, string Pattern, string Description);

    /// <summary>🔬 分析脚本内容，判断是否为 MC 服务器启动脚本</summary>
    /// <param name="content">脚本文件的原始文本内容</param>
    /// <returns>启动脚本分析结果，包含了我们扒出来的所有情报</returns>
    public static StartupScriptInfo Analyze(string content)
    {
        Log.Debug("📜 分析启动脚本内容，长度 {Len} 字符", content?.Length ?? 0);
        var info = new StartupScriptInfo { RawContent = content ?? string.Empty };

        if (string.IsNullOrWhiteSpace(content))
            return info;

        var matchedRules = new List<string>();

        // 🔍 逐条规则匹配 —— 命中越多越可能是启动脚本
        foreach (var rule in DetectionRules)
        {
            if (Regex.IsMatch(content, rule.Pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                matchedRules.Add(rule.Name);
                Log.Debug("✅ 匹配规则: {Rule} → {Value}", rule.Description, rule.Name);
            }
        }

        info.MatchedRules = matchedRules;

        Log.Debug("✅ 启动脚本分析完成，匹配 {Count} 条规则", matchedRules.Count);

        // 📊 至少命中 2 条规则才认定是服务器启动脚本
        // （1条可能是巧合，2条以上就八九不离十了）
        info.IsServerStartupScript = matchedRules.Count >= 2;

        // 🎯 只有确认是启动脚本才继续深入分析（不然白费力气）
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

    /// <summary>☕ 提取 Java 可执行文件路径</summary>
    /// <remarks>
    /// 支持三种写法：
    /// 1. 引号路径："C:\Program Files\Java\bin\java.exe"
    /// 2. 环境变量：%JAVA_HOME%\bin\java 或 $JAVA_HOME/bin/java
    /// 3. 相对路径：java（系统 PATH 里去找吧）
    /// </remarks>
    private static string? ExtractJavaPath(string content)
    {
        // 🎯 尝试匹配引号包裹的 java 路径
        var quotedMatch = QuotedJavaPathRegex().Match(content);
        if (quotedMatch.Success)
            return quotedMatch.Groups["path"].Value;

        // 🎯 尝试匹配环境变量开头的 java 路径（%JAVA_HOME% 或 $JAVA_HOME）
        var envVarMatch = EnvVarJavaPathRegex().Match(content);
        if (envVarMatch.Success)
            return envVarMatch.Groups["path"].Value;

        // 🎯 尝试匹配普通的 java 命令
        var simpleMatch = SimpleJavaPathRegex().Match(content);
        if (simpleMatch.Success)
            return simpleMatch.Groups["path"].Value;

        return null;
    }

    /// <summary>💾 提取最大堆内存（-Xmx）值</summary>
    private static long ExtractMaxMemory(string content)
    {
        // 匹配 -Xmx 后面的值，可能带引号也可能不带
        var match = MaxMemoryRegex().Match(content);
        if (!match.Success)
            return 0;

        var valueStr = match.Groups["value"].Value;
        return CommandLineParser.ParseMemoryValue(valueStr);
    }

    /// <summary>📦 提取服务器 JAR 文件名</summary>
    private static string? ExtractServerJarName(string content)
    {
        // 匹配 -jar 后面的 JAR 文件名（只取文件名部分，不要完整路径）
        var match = JarNameRegex().Match(content);
        if (!match.Success)
            return null;

        var jarPath = match.Groups["jar"].Value.Trim('\"', '\'');
        // 取路径的最后一部分作为文件名
        var separator = jarPath.Contains('/') ? '/' : '\\';
        var parts = jarPath.Split(separator);
        return parts[^1];
    }

    /// <summary>🔄 检测是否包含自动重启逻辑</summary>
    /// <remarks>
    /// 判断依据：脚本中包含 while true 循环、或 sleep + java 组合
    /// 经典的"服务器崩溃了就重启"的暴力的方法，但管用 💪
    /// </remarks>
    private static bool DetectAutoRestart(string content)
    {
        // while true / while (1) / while : (bash 无限循环三兄弟)
        var whileLoopMatch = WhileLoopRegex().Match(content);
        if (whileLoopMatch.Success)
            return true;

        // for 循环 + sleep 的组合（另一种重启写法）
        var forLoopMatch = ForLoopRestartRegex().Match(content);
        if (forLoopMatch.Success)
            return true;

        // 直接在命令行里写了 sleep 的（最简单粗暴的重启方式）
        var sleepMatch = SleepAloneRegex().Match(content);
        if (sleepMatch.Success)
            return true;

        return false;
    }

    /// <summary>⚡ 检测是否使用 Aikar 优化标志</summary>
    private static bool DetectAikarFlags(string content)
    {
        return content.Contains("-Dusing.aikars.flags=", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("-Daikars.new.flags=true", StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────── 正则表达式们（别怕，它们很友好）────────────────

    // ☕ 引号包裹的 java 路径：匹配 "xxx/java.exe" 或 'xxx/java'
    [GeneratedRegex(@"(?<path>""[^""]*(?:java|javaw)(?:\.exe)?""|'[^']*(?:java|javaw)(?:\.exe)?')")]
    private static partial Regex QuotedJavaPathRegex();

    // 🌍 环境变量 java 路径：匹配 %JAVA_HOME%\bin\java 或 $JAVA_HOME/bin/java
    [GeneratedRegex(@"(?<path>(?:%[A-Za-z_]+%|\$[A-Za-z_]+)[/\\][^\s""']+java(?:w)?(?:\.exe)?)")]
    private static partial Regex EnvVarJavaPathRegex();

    // 🚶 简单 java 命令：匹配独立的 java 或 java.exe
    [GeneratedRegex(@"(?<path>\bjava(?:w)?(?:\.exe)?\b)")]
    private static partial Regex SimpleJavaPathRegex();

    // 🫙 -Xmx 内存值（支持引号包裹和无引号两种形式）
    [GeneratedRegex(@"-Xmx\s*[""'']?\s*(?<value>\d+\s*[GgMmKk]?[Bb]?)\s*[""'']?")]
    private static partial Regex MaxMemoryRegex();

    // 📦 -jar 后的 JAR 文件名（支持变量、多行 ^ 拼接、引号包裹）
    [GeneratedRegex(@"-jar\s*[\^\s\r\n]*(?<jar>[\w\.\-/\\""'%$()]+\.jar)", RegexOptions.Multiline)]
    private static partial Regex JarNameRegex();

    // 🔄 while 无限循环
    [GeneratedRegex(@"(?i)\bwhile\s+(?:true|\(\s*1\s*\)|:)")]
    private static partial Regex WhileLoopRegex();

    // 🔄 for 循环重启模式
    [GeneratedRegex(@"(?i)\bfor\s+.*(?:restart|loop).*(?:sleep|java)")]
    private static partial Regex ForLoopRestartRegex();

    // 😴 sleep 命令（独立出现通常意味着重启逻辑）
    [GeneratedRegex(@"(?im)^\s*sleep\s+\d+")]
    private static partial Regex SleepAloneRegex();
}
