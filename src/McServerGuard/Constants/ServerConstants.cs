namespace McServerGuard.Constants;

// 🎮 Minecraft 服务器类型 —— 咱得知道对方是什么来头
public enum ServerType { Unknown, Vanilla, Spigot, Paper, Forge, Fabric, Bukkit, Folia }

public static class ServerConstants
{
    // 🔍 各大门派的 JAR 文件名特征（用来从进程命令行里辨认身份）
    public static readonly string[] VanillaJarPatterns = ["minecraft_server.*.jar", "server.jar"];
    public static readonly string[] SpigotJarPatterns = ["spigot-*.jar", "spigot.jar"];
    public static readonly string[] PaperJarPatterns = ["paper-*.jar", "paper.jar"];
    public static readonly string[] ForgeJarPatterns = ["forge-*.jar", "forge.jar"];
    public static readonly string[] FabricJarPatterns = ["fabric-server-launch.jar", "fabric-server.jar"];
    public static readonly string[] BukkitJarPatterns = ["craftbukkit-*.jar"];
    public static readonly string[] FoliaJarPatterns = ["folia-*.jar", "folia.jar"];

    // 📋 服务器核心关键词（只要 JAR 名字里包含这些，就判定为服务器而非客户端）
    public static readonly string[] ServerJarKeywords = ["minecraft_server", "server", "spigot", "paper", "forge", "fabric-server-launch", "craftbukkit", "folia"];

    // 🚫 这些是客户端进程的标志 —— 遇到就跳过，别把人家客户端当服务器了
    public static readonly string[] ServerProcessMarkers = ["nogui", "--nogui"];
    public static readonly string[] ClientProcessMarkers = ["--version", "--accessToken", "--userType", "--assetsDir"];

    // 📁 各类型服务器的"身份证"文件组合
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

    // 📄 所有服务器共有的配置文件
    public static readonly string[] CommonConfigFiles = ["server.properties", "eula.txt", "ops.json", "whitelist.json", "banned-players.json", "banned-ips.json", "permissions.yml", "commands.yml"];
    public static readonly string[] SpigotConfigFiles = ["spigot.yml", "bukkit.yml"];
    public static readonly string[] PaperConfigFiles = ["config/paper-global.yml", "config/paper-world-defaults.yml"];
    public static readonly string[] ForgeConfigFiles = ["server.toml", "forge-server.toml", "mods/"];
    public static readonly string[] FabricConfigFiles = ["fabric-server-launch.properties", "mods/", ".fabric/"];

    // 🏷️ 最关键的那个文件 —— 有它就是服务器目录，没它...你走错门了
    public const string ServerValidationFile = "server.properties";
    public const int DefaultServerPort = 25565;
}
