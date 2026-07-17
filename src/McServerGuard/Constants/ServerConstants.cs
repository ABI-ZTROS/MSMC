// -----------------------------------------------------------------------------
// 文件名: ServerConstants.cs
// 命名空间: McServerGuard.Constants
// 功能描述: 服务器类型与识别常量定义，提供服务端指纹识别与配置文件模式
// 依赖组件: 无
// 设计模式: 常量容器 + 判别式枚举
// -----------------------------------------------------------------------------
namespace McServerGuard.Constants;

/// <summary>
/// Minecraft 服务器类型枚举，作为服务端核心品牌的判别式。
/// 用于标识不同发行版的服务端软件，覆盖主流的 Mod 端与插件端。
/// </summary>
public enum ServerType
{
    /// <summary>
    /// 未知类型，尚未判定或无法识别。
    /// </summary>
    Unknown,

    /// <summary>
    /// 原版（Vanilla）服务端，Mojang 官方发布。
    /// </summary>
    Vanilla,

    /// <summary>
    /// Spigot 服务端，高性能插件服务端。
    /// </summary>
    Spigot,

    /// <summary>
    /// Paper 服务端，基于 Spigot 的优化分支。
    /// </summary>
    Paper,

    /// <summary>
    /// Forge 服务端，Mod 加载器服务端。
    /// </summary>
    Forge,

    /// <summary>
    /// Fabric 服务端，轻量级 Mod 加载器服务端。
    /// </summary>
    Fabric,

    /// <summary>
    /// Bukkit 服务端，经典插件 API 服务端。
    /// </summary>
    Bukkit,

    /// <summary>
    /// Folia 服务端，基于 Paper 的多线程区域化服务端。
    /// </summary>
    Folia
}

/// <summary>
/// 服务器常量容器类，提供服务端识别指纹与配置文件模式。
/// 包含 JAR 文件名模式、进程标识特征、配置文件清单等识别依据。
/// </summary>
public static class ServerConstants
{
    /// <summary>
    /// 各类型服务器核心 JAR 文件的文件名模式。
    /// 用于从进程命令行中识别服务器类型。
    /// 采用通配符匹配规则。
    /// </summary>
    public static readonly string[] VanillaJarPatterns = ["minecraft_server.*.jar", "server.jar"];
    public static readonly string[] SpigotJarPatterns = ["spigot-*.jar", "spigot.jar"];
    public static readonly string[] PaperJarPatterns = ["paper-*.jar", "paper.jar"];
    public static readonly string[] ForgeJarPatterns = ["forge-*.jar", "forge.jar"];
    public static readonly string[] FabricJarPatterns = ["fabric-server-launch.jar", "fabric-server.jar"];
    public static readonly string[] BukkitJarPatterns = ["craftbukkit-*.jar"];
    public static readonly string[] FoliaJarPatterns = ["folia-*.jar", "folia.jar"];

    /// <summary>
    /// 服务器 JAR 文件关键词列表。
    /// 用于从进程命令行中快速判定 JAR 是否为服务端而非客户端。
    /// 只要 JAR 名称包含其中任一关键词即判定为服务器。
    /// </summary>
    public static readonly string[] ServerJarKeywords = ["minecraft_server", "server", "spigot", "paper", "forge", "fabric-server-launch", "craftbukkit", "folia"];

    /// <summary>
    /// 服务端进程标识特征。
    /// 命令行中包含这些参数时，强化服务端判定。
    /// </summary>
    public static readonly string[] ServerProcessMarkers = ["nogui", "--nogui"];

    /// <summary>
    /// 客户端进程标识特征。
    /// 命令行中包含这些参数时，排除服务端判定。
    /// </summary>
    public static readonly string[] ClientProcessMarkers = ["--version", "--accessToken", "--userType", "--assetsDir"];

    /// <summary>
    /// 各类型服务器的特征配置文件映射。
    /// 通过检测目录下是否存在特定文件组合，辅助判定服务器类型。
    /// 键为服务器类型，值为该类型特有的文件或目录路径数组。
    /// </summary>
    public static readonly Dictionary<ServerType, string[]> TypeIndicatorFiles = new()
    {
        [ServerType.Vanilla] = [],
        [ServerType.Spigot] = ["spigot.yml", "bukkit.yml"],
        [ServerType.Paper] = ["config/paper-global.yml"],
        [ServerType.Forge] = ["mods/", "forge-server.toml"],
        [ServerType.Fabric] = ["fabric-server-launch.properties", ".fabric/"],
        [ServerType.Bukkit] = ["bukkit.yml"],
        [ServerType.Folia] = ["config/paper-global.yml", "config/folia-global.yml"],
    };

    /// <summary>
    /// 所有服务器共有的标准配置文件列表。
    /// 包含 server.properties 及权限、封禁等标准配置文件。
    /// </summary>
    public static readonly string[] CommonConfigFiles = ["server.properties", "eula.txt", "ops.json", "whitelist.json", "banned-players.json", "banned-ips.json", "permissions.yml", "commands.yml"];

    /// <summary>
    /// Spigot 特有的配置文件列表。
    /// </summary>
    public static readonly string[] SpigotConfigFiles = ["spigot.yml", "bukkit.yml"];

    /// <summary>
    /// Paper 特有的配置文件列表。
    /// </summary>
    public static readonly string[] PaperConfigFiles = ["config/paper-global.yml", "config/paper-world-defaults.yml"];

    /// <summary>
    /// Forge 特有的配置文件与目录列表。
    /// </summary>
    public static readonly string[] ForgeConfigFiles = ["server.toml", "forge-server.toml", "mods/"];

    /// <summary>
    /// Fabric 特有的配置文件与目录列表。
    /// </summary>
    public static readonly string[] FabricConfigFiles = ["fabric-server-launch.properties", "mods/", ".fabric/"];

    /// <summary>
    /// 服务器目录验证文件名。
    /// 存在该文件的目录被判定为有效服务器目录。
    /// </summary>
    public const string ServerValidationFile = "server.properties";

    /// <summary>
    /// Minecraft 服务器默认监听端口号。
    /// 标准值为 25565。
    /// </summary>
    public const int DefaultServerPort = 25565;
}
