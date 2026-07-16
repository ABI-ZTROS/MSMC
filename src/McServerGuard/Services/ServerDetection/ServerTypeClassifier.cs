using System.Text.RegularExpressions;
using McServerGuard.Constants;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>🏷️ 服务器类型分类器 —— 通过 JAR 名称和配置文件特征来判断服务器类型</summary>
/// <remarks>
/// 分类策略：
/// 1. 先看 JAR 文件名 —— 名字里带什么就大概率是什么（Vanilla/Paper/Spigot 等）
/// 2. JAR 名称太通用的话（比如就叫 "server.jar"），就看配置文件来辅助判断
/// 3. 两种方法都认不出来就标记为 Unknown（别勉强，承认自己不认识也没啥丢人的）
/// </remarks>
public static partial class ServerTypeClassifier
{
    /// <summary>🏷️ JAR 名称模式到服务器类型的映射表</summary>
    private static readonly (string[] Patterns, ServerType Type)[] JarNameTypeMap =
    [
        (ServerConstants.VanillaJarPatterns, ServerType.Vanilla),
        (ServerConstants.SpigotJarPatterns, ServerType.Spigot),
        (ServerConstants.PaperJarPatterns, ServerType.Paper),
        (ServerConstants.FoliaJarPatterns, ServerType.Folia),
        (ServerConstants.ForgeJarPatterns, ServerType.Forge),
        (ServerConstants.FabricJarPatterns, ServerType.Fabric),
        (ServerConstants.BukkitJarPatterns, ServerType.Bukkit),
    ];

    /// <summary>🏷️ 通过 JAR 名称判断服务器类型 —— 名字说明一切（大部分时候）</summary>
    /// <param name="jarFileName">JAR 文件名（只取文件名部分，不要完整路径）</param>
    /// <returns>识别出的服务器类型，实在认不出来就返回 Unknown</returns>
    public static ServerType ClassifyByJarName(string jarFileName)
    {
        Log.Debug("🏷️ 分类服务器类型: JAR={Jar}", jarFileName);
        if (string.IsNullOrWhiteSpace(jarFileName))
            return ServerType.Unknown;

        // 🎯 按优先级逐一匹配 JAR 名称模式
        // Paper 要排在 Spigot 前面，因为 Paper 是 Spigot 的超集，名称可能同时匹配
        foreach (var (patterns, type) in JarNameTypeMap)
        {
            foreach (var pattern in patterns)
            {
                if (MatchesAny(jarFileName, pattern))
                {
                    Log.Debug("✅ JAR 名称匹配: {Pattern} → {Type}", pattern, type);
                    return type;
                }
            }
        }

        Log.Debug("❓ JAR 名称未匹配，尝试配置文件推断");
        return ServerType.Unknown;
    }

    /// <summary>📁 通过配置文件特征推断服务器类型 —— 当 JAR 名称太通用时的后备方案</summary>
    /// <param name="jarFileName">JAR 文件名（可能为空）</param>
    /// <param name="workingDirectory">服务器工作目录路径</param>
    /// <returns>
    /// 优先使用 JAR 名称匹配；如果 JAR 名称无法判断，则检查目录中的配置文件。
    /// 如果 JAR 名称匹配到了 Vanilla 但配置文件有更具体的证据，会升级判断结果。
    /// </returns>
    public static ServerType ClassifyByJarNameAndConfigFiles(string jarFileName, string workingDirectory)
    {
        Log.Debug("🏷️ 综合分类: JAR={Jar} Dir={Dir}", jarFileName, workingDirectory);

        // 🏷️ 先用 JAR 名称试试
        var type = ClassifyByJarName(jarFileName);

        if (type != ServerType.Unknown && type != ServerType.Vanilla)
        {
            Log.Information("✅ 服务器类型确定为: {Type}", type);
            return type;
        }

        // 📁 如果 JAR 名称太通用（Unknown 或 Vanilla），用配置文件辅助判断
        if (!string.IsNullOrWhiteSpace(workingDirectory) &&
            System.IO.Directory.Exists(workingDirectory))
        {
            var configInferred = InferFromConfigFiles(workingDirectory);
            // 配置文件推断优先级更高 —— 毕竟文件不会撒谎
            if (configInferred != ServerType.Unknown)
            {
                Log.Information("✅ 服务器类型确定为: {Type}", configInferred);
                return configInferred;
            }
        }

        Log.Information("✅ 服务器类型确定为: {Type}", type);
        return type;
    }

    /// <summary>📂 从配置文件推断服务器类型 —— 看看目录里有什么"身份证"文件</summary>
    /// <param name="directory">服务器目录路径</param>
    /// <returns>推断出的服务器类型，配置文件全都没有就返回 Unknown</returns>
    private static ServerType InferFromConfigFiles(string directory)
    {
        // 🎯 第一轮：检查各类型的"独有"指示文件（确定性最高）
        // 必须先检查独有文件，因为部分类型共享指示文件（如 Folia/Paper 共享 paper-global.yml，Fabric/Forge 共享 mods/）
        var uniqueChecks = new[]
        {
            (ServerType.Folia,  new[] { "config/folia-global.yml" }),
            (ServerType.Fabric, new[] { "fabric-server-launch.properties", ".fabric/" }),
            (ServerType.Paper,  new[] { "config/paper-global.yml" }),
            (ServerType.Forge,  new[] { "forge-server.toml" }),
            (ServerType.Spigot, new[] { "spigot.yml" }),
            (ServerType.Bukkit, new[] { "bukkit.yml" }),
        };

        foreach (var (type, indicators) in uniqueChecks)
        {
            if (indicators.Any(indicator => IndicatorExists(directory, indicator)))
                return type;
        }

        // 🎯 第二轮：检查"共享"指示文件（确定性较低，作为兜底）
        // 比如 mods/ 目录 Fabric 和 Forge 都有，但 Forge 更常见
        var sharedChecks = new[]
        {
            (ServerType.Forge, new[] { "mods/" }),
        };

        foreach (var (type, indicators) in sharedChecks)
        {
            if (indicators.Any(indicator => IndicatorExists(directory, indicator)))
                return type;
        }

        return ServerType.Unknown;
    }

    /// <summary>✅ 检查特征文件/目录是否存在</summary>
    /// <param name="directory">服务器目录路径</param>
    /// <param name="indicator">特征文件名或目录名</param>
    /// <returns>true 表示找到了这个"身份证"文件</returns>
    private static bool IndicatorExists(string directory, string indicator)
    {
        // 🔍 以 / 结尾的是目录，否则是文件
        if (indicator.EndsWith('/') || indicator.EndsWith('\\'))
        {
            var dirName = indicator.TrimEnd('/', '\\');
            return System.IO.Directory.Exists(System.IO.Path.Combine(directory, dirName));
        }

        return System.IO.File.Exists(System.IO.Path.Combine(directory, indicator));
    }

    /// <summary>🎯 通配符匹配 —— 把简单的 glob 通配符转成正则表达式来匹配</summary>
    /// <remarks>
    /// 支持 * 和 ? 通配符（但别太贪心，复杂的正则还是交给专业选手）
    /// 示例："paper-*.jar" 能匹配 "paper-1.20.4-439.jar"
    /// </remarks>
    public static bool MatchesAny(string input, string wildcardPattern)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(wildcardPattern))
            return false;

        // 🔧 通配符 → 正则表达式转换
        // * → .*
        // ? → .
        // 其他正则特殊字符需要转义（不然正则引擎会误解它们的意图）
        var regexPattern = "^" + Regex.Escape(wildcardPattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }
}
