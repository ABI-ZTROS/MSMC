using System.Text.RegularExpressions;
using McServerGuard.Constants;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>
/// 服务器类型分类器 —— 基于 JAR 文件名和配置文件特征推断 Minecraft 服务端类型
/// </summary>
/// <remarks>
/// 分类策略采用两级判定机制：
/// 1. JAR 文件名匹配 —— 通过命名模式快速识别服务端类型（主判定链路）
/// 2. 配置文件辅助推断 —— 当 JAR 名称过于通用时，基于目录中的特征文件进行二次判定
/// 两级均无法识别时返回 Unknown，遵循防御式编程原则。
/// </remarks>
public static partial class ServerTypeClassifier
{
    /// <summary>
    /// JAR 名称模式到服务器类型的映射表（按优先级降序排列）
    /// </summary>
    /// <remarks>
    /// 优先级排序依据：派生类优先于基类（如 Paper 优先于 Spigot），
    /// 确保具有继承关系的服务端类型被正确识别。
    /// </remarks>
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

    /// <summary>
    /// 通过 JAR 文件名判断服务器类型
    /// </summary>
    /// <param name="jarFileName">JAR 文件名（仅文件名部分，不含完整路径）</param>
    /// <returns>识别出的服务器类型；无法识别时返回 <see cref="ServerType.Unknown"/></returns>
    /// <remarks>
    /// 按映射表优先级顺序进行通配符匹配，首个匹配项即为判定结果。
    /// Paper 等派生类型排在 Spigot 等基类型之前，确保正确识别。
    /// </remarks>
    public static ServerType ClassifyByJarName(string jarFileName)
    {
        Log.Debug("🏷️ 分类服务器类型: JAR={Jar}", jarFileName);
        if (string.IsNullOrWhiteSpace(jarFileName))
            return ServerType.Unknown;

        // 按优先级逐一匹配 JAR 名称模式
        // Paper 排在 Spigot 前面，因为 Paper 是 Spigot 的超集，名称可能同时匹配
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

    /// <summary>
    /// 通过 JAR 名称和配置文件特征综合推断服务器类型
    /// </summary>
    /// <param name="jarFileName">JAR 文件名（可能为空）</param>
    /// <param name="workingDirectory">服务器工作目录路径</param>
    /// <returns>推断出的服务器类型</returns>
    /// <remarks>
    /// 判定优先级：
    /// 1. 优先使用 JAR 名称匹配；若结果为非 Unknown 且非 Vanilla，则直接返回
    /// 2. 若 JAR 名称过于通用（Unknown 或 Vanilla），则基于配置文件进行辅助推断
    /// 3. 配置文件推断结果具有更高优先级——文件特征比命名更具确定性
    /// </remarks>
    public static ServerType ClassifyByJarNameAndConfigFiles(string jarFileName, string workingDirectory)
    {
        Log.Debug("🏷️ 综合分类: JAR={Jar} Dir={Dir}", jarFileName, workingDirectory);

        // 第一阶段：JAR 名称匹配
        var type = ClassifyByJarName(jarFileName);

        if (type != ServerType.Unknown && type != ServerType.Vanilla)
        {
            Log.Information("✅ 服务器类型确定为: {Type}", type);
            return type;
        }

        // 第二阶段：配置文件辅助推断（当 JAR 名称过于通用时启用）
        if (!string.IsNullOrWhiteSpace(workingDirectory) &&
            System.IO.Directory.Exists(workingDirectory))
        {
            var configInferred = InferFromConfigFiles(workingDirectory);
            // 配置文件推断结果优先
            if (configInferred != ServerType.Unknown)
            {
                Log.Information("✅ 服务器类型确定为: {Type}", configInferred);
                return configInferred;
            }
        }

        Log.Information("✅ 服务器类型确定为: {Type}", type);
        return type;
    }

    /// <summary>
    /// 从配置文件特征推断服务器类型
    /// </summary>
    /// <param name="directory">服务器目录路径</param>
    /// <returns>推断出的服务器类型；无匹配特征时返回 <see cref="ServerType.Unknown"/></returns>
    /// <remarks>
    /// 采用两轮判定策略：
    /// 第一轮：检查各类型的独有指示文件（确定性最高），必须先检查独有文件
    ///         因为部分类型共享指示文件（如 Folia/Paper 共享 paper-global.yml）
    /// 第二轮：检查共享指示文件（确定性较低，作为兜底）
    /// </remarks>
    private static ServerType InferFromConfigFiles(string directory)
    {
        // 第一轮：独有指示文件检查（确定性最高）
        // 必须优先检查独有文件，避免共享文件导致的误判
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

        // 第二轮：共享指示文件检查（确定性较低，作为兜底）
        // 例如 mods/ 目录 Fabric 和 Forge 都有，但 Forge 更常见
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

    /// <summary>
    /// 检查特征文件或目录是否存在
    /// </summary>
    /// <param name="directory">服务器目录路径</param>
    /// <param name="indicator">特征文件名或目录名</param>
    /// <returns>true 表示特征文件存在</returns>
    /// <remarks>
    /// 以路径分隔符结尾的标识符视为目录，否则视为文件。
    /// 同时支持正斜杠和反斜杠作为目录分隔符。
    /// </remarks>
    private static bool IndicatorExists(string directory, string indicator)
    {
        // 路径分隔符结尾表示目录，否则表示文件
        if (indicator.EndsWith('/') || indicator.EndsWith('\\'))
        {
            var dirName = indicator.TrimEnd('/', '\\');
            return System.IO.Directory.Exists(System.IO.Path.Combine(directory, dirName));
        }

        return System.IO.File.Exists(System.IO.Path.Combine(directory, indicator));
    }

    /// <summary>
    /// 通配符模式匹配 —— 将 glob 通配符转换为正则表达式进行匹配
    /// </summary>
    /// <param name="input">待匹配的输入字符串</param>
    /// <param name="wildcardPattern">通配符模式（支持 * 和 ?）</param>
    /// <returns>true 表示输入字符串匹配通配符模式</returns>
    /// <remarks>
    /// 支持的通配符：
    /// * —— 匹配零个或多个任意字符
    /// ? —— 匹配单个任意字符
    /// 匹配不区分大小写。
    /// </remarks>
    public static bool MatchesAny(string input, string wildcardPattern)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(wildcardPattern))
            return false;

        // 通配符到正则表达式的转换
        // * → .*
        // ? → .
        // 其他正则特殊字符需要转义，避免被正则引擎误解释
        var regexPattern = "^" + Regex.Escape(wildcardPattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".") + "$";

        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }
}
