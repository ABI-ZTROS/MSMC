// -----------------------------------------------------------------------------
// 文件名: ConfigDescriptorRegistry.cs
// 命名空间: McServerGuard.Services.ConfigManagement
// 功能描述: 配置描述符注册表，集中管理 Minecraft 服务器各配置文件的配置项元数据
// 依赖组件: System.IO, System.Text.RegularExpressions, Serilog
// 设计模式: 注册表模式、键控查找、多维哈希映射
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ConfigManagement;

using System.IO;
using System.Text.RegularExpressions;
using Serilog;

/// <summary>
/// 服务器配置描述符
/// </summary>
/// <remarks>
/// 封装单个配置项的完整元数据，包括键名、显示名、描述、分类、
/// 默认值、取值范围约束、正则验证模式及重启要求等属性。
/// 作为配置描述符注册表中的基本数据单元。
/// </remarks>
public sealed class ServerConfigDescriptor
{
    /// <summary>配置项的键名标识</summary>
    public required string Key { get; init; }

    /// <summary>所属配置文件的名称</summary>
    public required string ConfigFileName { get; init; }

    /// <summary>配置项的中文显示名称</summary>
    public required string DisplayName { get; init; }

    /// <summary>配置项的中文详细描述</summary>
    public required string Description { get; init; }

    /// <summary>配置项所属的功能分类</summary>
    public required string Category { get; init; }

    /// <summary>配置项的默认值（字符串表示形式）</summary>
    public string? DefaultValue { get; init; }

    /// <summary>数值类型配置项的最小值约束</summary>
    public int? MinValue { get; init; }

    /// <summary>数值类型配置项的最大值约束</summary>
    public int? MaxValue { get; init; }

    /// <summary>枚举类型配置项的允许值集合</summary>
    public string[]? AllowedValues { get; init; }

    /// <summary>字符串类型配置项的正则验证模式</summary>
    public string? RegexPattern { get; init; }

    /// <summary>配置项的值类型标识</summary>
    public string ValueType { get; init; } = "string";

    /// <summary>指示配置项修改后是否需要重启服务器方能生效</summary>
    public bool RequiresRestart { get; init; }

    /// <summary>预编译正则表达式实例，采用延迟初始化策略</summary>
    private Regex? _compiledRegex;

    /// <summary>
    /// 获取预编译的正则表达式实例
    /// </summary>
    /// <returns>预编译的 Regex 实例；若无正则约束则返回 null</returns>
    /// <remarks>采用懒加载模式，首次调用时进行编译并缓存</remarks>
    public Regex? GetCompiledRegex()
    {
        if (RegexPattern is null)
            return null;

        return _compiledRegex ??= new Regex(RegexPattern, RegexOptions.Compiled);
    }
}

/// <summary>
/// 配置描述符注册表
/// </summary>
/// <remarks>
/// 集中管理 Minecraft 服务器各配置文件的配置项元数据，
/// 包括 server.properties、bukkit.yml、spigot.yml、paper-global.yml 等
/// 配置文件的关键配置项描述信息。支持多级键控查找策略：
/// 精确匹配、纯文件名匹配、后缀模糊匹配。
/// </remarks>
public sealed class ConfigDescriptorRegistry
{
    /// <summary>
    /// 内部存储结构：以 (配置文件名, 配置键名) 复合键为索引的多维哈希映射
    /// </summary>
    private readonly Dictionary<(string ConfigFileName, string Key), ServerConfigDescriptor> _descriptors = new();

    /// <summary>
    /// 初始化配置描述符注册表
    /// </summary>
    /// <remarks>构造函数中完成所有预置配置项的注册，构建完成后仅提供只读查询</remarks>
    public ConfigDescriptorRegistry()
    {
        Log.Information("ConfigDescriptorRegistry 初始化，注册配置描述符...");
        RegisterServerProperties();
        RegisterServerPropertiesExtras();
        RegisterBukkitYml();
        RegisterSpigotYml();
        RegisterPaperGlobalYml();
        RegisterPaperWorldDefaultsYml();
        Log.Information("注册表构建完成，共 {Count} 个描述符", _descriptors.Count);
    }

    /// <summary>
    /// 向注册表中注册单个配置描述符
    /// </summary>
    /// <param name="descriptor">待注册的配置描述符实例</param>
    /// <exception cref="ArgumentNullException">当 descriptor 为 null 时抛出</exception>
    private void Register(ServerConfigDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var key = (descriptor.ConfigFileName, descriptor.Key);
        _descriptors[key] = descriptor;

        Log.Debug("注册配置描述符: {Key} → {Name}", descriptor.Key, descriptor.DisplayName);
    }

    /// <summary>
    /// 根据配置键名和配置文件名获取对应的配置描述符
    /// </summary>
    /// <param name="key">配置项键名</param>
    /// <param name="configFileName">配置文件名称（可包含路径）</param>
    /// <returns>匹配的配置描述符；未找到则返回 null</returns>
    /// <remarks>
    /// 采用三级匹配策略：
    /// 1. 精确匹配：使用 (configFileName, key) 复合键进行精确查找
    /// 2. 纯文件名匹配：去除目录前缀后进行匹配（如 config/paper-global.yml → paper-global.yml）
    /// 3. 后缀匹配：针对 YAML 压平后的层级键，用注册键作为后缀进行模糊匹配
    /// </remarks>
    public ServerConfigDescriptor? GetDescriptor(string key, string configFileName)
    {
        if (key is null || configFileName is null)
            return null;

        // 第一级：精确匹配
        if (_descriptors.TryGetValue((configFileName, key), out var desc))
            return desc;

        // 第二级：纯文件名匹配（去除目录前缀）
        var pureFileName = Path.GetFileName(configFileName);
        if (!string.IsNullOrEmpty(pureFileName) && pureFileName != configFileName)
        {
            if (_descriptors.TryGetValue((pureFileName, key), out desc))
                return desc;
        }

        // 第三级：后缀匹配 —— 处理 YAML 层级压平场景
        // 例如键 "world-settings.default.mob-spawn-range" 可匹配注册键 "mob-spawn-range"
        foreach (var kvp in _descriptors)
        {
            if (!kvp.Key.ConfigFileName.Equals(configFileName, StringComparison.OrdinalIgnoreCase) &&
                !kvp.Key.ConfigFileName.Equals(pureFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            var registeredKey = kvp.Key.Key;
            if (key.Length > registeredKey.Length &&
                key.EndsWith(registeredKey, StringComparison.OrdinalIgnoreCase) &&
                key[key.Length - registeredKey.Length - 1] == '.')
            {
                return kvp.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取指定配置文件的所有已注册配置描述符
    /// </summary>
    /// <param name="configFileName">配置文件名称（可包含路径）</param>
    /// <returns>匹配的配置描述符列表</returns>
    /// <remarks>同时支持完整路径匹配和纯文件名匹配</remarks>
    public List<ServerConfigDescriptor> GetDescriptorsForFile(string configFileName)
    {
        if (configFileName is null)
            return [];

        var pureFileName = Path.GetFileName(configFileName);

        return _descriptors
            .Where(kv =>
                kv.Key.ConfigFileName.Equals(configFileName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(pureFileName) &&
                 kv.Key.ConfigFileName.Equals(pureFileName, StringComparison.OrdinalIgnoreCase)))
            .Select(kv => kv.Value)
            .ToList();
    }

    /// <summary>
    /// 生成配置描述符覆盖率报告
    /// </summary>
    /// <returns>包含总描述符数量和各文件统计的覆盖率报告</returns>
    public CoverageReport GetCoverageReport()
    {
        var fileStats = _descriptors
            .GroupBy(d => d.Key.ConfigFileName)
            .Select(g => new FileCoverageStat(g.Key, g.Count()))
            .OrderBy(f => f.ConfigFileName)
            .ToList();

        return new CoverageReport(
            TotalDescriptors: _descriptors.Count,
            FileStats: fileStats
        );
    }

    /// <summary>
    /// 查找指定键名列表中未匹配描述符的键
    /// </summary>
    /// <param name="keys">待检查的配置键名列表</param>
    /// <param name="configFileName">配置文件名称</param>
    /// <returns>未找到对应描述符的键名列表</returns>
    /// <remarks>用于诊断配置描述符的覆盖范围</remarks>
    public List<string> FindUnmatchedKeys(List<string> keys, string configFileName)
    {
        var pureName = Path.GetFileName(configFileName);
        return keys
            .Where(k => GetDescriptor(k, pureName) is null)
            .ToList();
    }

    /// <summary>
    /// 配置描述符覆盖率报告
    /// </summary>
    /// <param name="TotalDescriptors">已注册描述符总数</param>
    /// <param name="FileStats">各配置文件的覆盖率统计列表</param>
    public sealed record CoverageReport(int TotalDescriptors, List<FileCoverageStat> FileStats);

    /// <summary>
    /// 单文件覆盖率统计信息
    /// </summary>
    /// <param name="ConfigFileName">配置文件名称</param>
    /// <param name="DescriptorCount">该文件已注册的描述符数量</param>
    public sealed record FileCoverageStat(string ConfigFileName, int DescriptorCount);

    /// <summary>
    /// 注册 server.properties 配置文件的所有关键配置项
    /// </summary>
    /// <remarks>
    /// server.properties 是 Minecraft 服务器的核心配置文件，
    /// 包含网络、玩家、世界、游戏机制、性能优化等核心配置项。
    /// 数据来源：Minecraft Wiki + Folia 26.1.2 默认配置
    /// </remarks>
    private void RegisterServerProperties()
    {
        const string file = "server.properties";

        // ==================== 网络设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "server-port",
            ConfigFileName = file,
            DisplayName = "服务器端口",
            Description = "服务器监听的端口号。玩家连接时需要指定这个端口。\n范围 1-65533，默认 25565 🌐",
            Category = "网络",
            DefaultValue = "25565",
            MinValue = 1,
            MaxValue = 65533,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "server-ip",
            ConfigFileName = file,
            DisplayName = "服务器 IP",
            Description = "服务器绑定的 IP 地址。留空则绑定所有可用地址（0.0.0.0）。\n多网卡或有公网/内网区分时才需要设置 🖧",
            Category = "网络",
            RegexPattern = @"^(\d{1,3}\.){3}\d{1,3}$|^$",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "network-compression-threshold",
            ConfigFileName = file,
            DisplayName = "网络压缩阈值",
            Description = "数据包压缩的大小阈值（字节）。\n-1=禁用压缩，0=压缩所有数据包，默认 256 📦",
            Category = "网络",
            DefaultValue = "256",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "rate-limit",
            ConfigFileName = file,
            DisplayName = "每玩家数据包速率限制",
            Description = "限制单个玩家每秒的数据包速率。0=禁用限制。\n用于防止玩家通过大量数据包攻击服务器 🛡️",
            Category = "网络",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "use-native-transport",
            ConfigFileName = file,
            DisplayName = "使用原生传输",
            Description = "是否使用 Linux epoll 等原生网络优化。\nLinux 服务器建议开启，可显著提升网络性能 🚀",
            Category = "网络",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        // ==================== 玩家设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "max-players",
            ConfigFileName = file,
            DisplayName = "最大玩家数",
            Description = "服务器同时允许连接的最大玩家数量。\n设太大也没用，还得看你的服务器性能撑不撑得住 👥",
            Category = "玩家",
            DefaultValue = "20",
            MinValue = 0,
            MaxValue = 2147483647,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "online-mode",
            ConfigFileName = file,
            DisplayName = "正版验证",
            Description = "是否启用 Minecraft 正版验证。\ntrue=只允许正版玩家，false=允许离线/盗版玩家。\n⚠️ 关闭正版验证意味着任何人都可以冒充别人登录，注意安全！",
            Category = "玩家",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "white-list",
            ConfigFileName = file,
            DisplayName = "白名单",
            Description = "是否启用白名单。启用后只有白名单里的玩家才能进入服务器。\n私密服必备功能 📝",
            Category = "玩家",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "enforce-whitelist",
            ConfigFileName = file,
            DisplayName = "强制白名单",
            Description = "启用后，如果白名单被重新加载，不在白名单里的在线玩家会被踢出。\n确保白名单即时生效 🚫",
            Category = "玩家",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "enforce-secure-profile",
            ConfigFileName = file,
            DisplayName = "强制安全配置",
            Description = "是否强制 Mojang 签名验证。\n启用后，使用未签名聊天消息的玩家无法连接 🔐",
            Category = "玩家",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "allow-flight",
            ConfigFileName = file,
            DisplayName = "允许飞行",
            Description = "是否允许玩家在生存模式下飞行。\nfalse=检测到飞行的玩家会被踢出。\n如果装了飞行模组或处于创造模式，需要设为 true ✈️",
            Category = "玩家",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "player-idle-timeout",
            ConfigFileName = file,
            DisplayName = "玩家空闲踢出",
            Description = "玩家空闲多久后会被踢出服务器（分钟）。0=永不踢出。\n防止挂机玩家占用服务器资源 💤",
            Category = "玩家",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "hide-online-players",
            ConfigFileName = file,
            DisplayName = "隐藏在线玩家",
            Description = "是否在服务器列表中隐藏在线玩家数量和列表。\n隐私保护选项 🙈",
            Category = "玩家",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "log-ips",
            ConfigFileName = file,
            DisplayName = "记录 IP 地址",
            Description = "是否在日志中记录玩家的 IP 地址。\n隐私敏感选项，不需要排查问题时可以关掉 🔒",
            Category = "玩家",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "accepts-transfers",
            ConfigFileName = file,
            DisplayName = "接受玩家转移",
            Description = "是否接收从其他服务器转入的玩家（transfer 数据包）。\n用于跨服传送场景 🔄",
            Category = "玩家",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "op-permission-level",
            ConfigFileName = file,
            DisplayName = "OP 权限等级",
            Description = "OP 玩家的默认权限等级。\n1=绕过出生保护 2=可以使用所有单玩家命令 3=可以使用所有多人命令 4=可以使用所有命令\n4=最高权限 ⭐",
            Category = "玩家",
            DefaultValue = "4",
            MinValue = 0,
            MaxValue = 4,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "function-permission-level",
            ConfigFileName = file,
            DisplayName = "函数权限等级",
            Description = "函数（function）和命令方块使用的默认权限等级。\n范围 1-4，默认 2 📜",
            Category = "玩家",
            DefaultValue = "2",
            MinValue = 1,
            MaxValue = 4,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 世界设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "level-name",
            ConfigFileName = file,
            DisplayName = "世界名称",
            Description = "主世界的文件夹名称。对应服务器目录下的 level-name 文件夹。\n改名等于换世界，慎操作！🌍",
            Category = "世界",
            DefaultValue = "world",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "level-seed",
            ConfigFileName = file,
            DisplayName = "世界种子",
            Description = "世界生成的种子。留空则随机生成。\n相同的种子会生成相同的世界 🌱",
            Category = "世界",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "level-type",
            ConfigFileName = file,
            DisplayName = "世界类型",
            Description = "世界生成的类型。\nminecraft:normal=默认 minecraft:flat=超平坦 minecraft:large_biomes=大型生物群系 minecraft:amplified=放大化 🗺️",
            Category = "世界",
            DefaultValue = "minecraft:normal",
            AllowedValues = ["minecraft:normal", "minecraft:flat", "minecraft:large_biomes", "minecraft:amplified", "minecraft:single_biome_surface"],
            ValueType = "enum",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "generate-structures",
            ConfigFileName = file,
            DisplayName = "生成结构",
            Description = "是否生成村庄、地牢、要塞等结构。\n关掉的话世界会很空旷，但生成速度更快 🏘️",
            Category = "世界",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "max-world-size",
            ConfigFileName = file,
            DisplayName = "世界大小限制",
            Description = "世界的最大半径（方块）。\n玩家不能越过这个边界，范围 1-29999984 📏",
            Category = "世界",
            DefaultValue = "29999984",
            MinValue = 1,
            MaxValue = 29999984,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "view-distance",
            ConfigFileName = file,
            DisplayName = "视距",
            Description = "服务器向玩家发送的区块渲染范围（单位：区块）。\n值越大看到越远，但服务器和客户端的负担也越重。建议 8-12 🔭",
            Category = "世界",
            DefaultValue = "10",
            MinValue = 3,
            MaxValue = 32,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "simulation-distance",
            ConfigFileName = file,
            DisplayName = "模拟距离",
            Description = "服务器对玩家周围区块进行游戏逻辑模拟的范围（单位：区块）。\n控制红石/实体/农作物计算范围，红石相关最关键参数 🧪",
            Category = "世界",
            DefaultValue = "10",
            MinValue = 3,
            MaxValue = 32,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spawn-protection",
            ConfigFileName = file,
            DisplayName = "出生点保护范围",
            Description = "出生点周围的保护范围。非 OP 玩家不能在保护区域内破坏/放置方块。\n边长 = 2×此值+1，0=禁用 🛡️",
            Category = "世界",
            DefaultValue = "16",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "generator-settings",
            ConfigFileName = file,
            DisplayName = "世界生成设置",
            Description = "自定义世界生成的 JSON 配置。\n用于超平坦等自定义世界类型 ⚙️",
            Category = "世界",
            DefaultValue = "{}",
            ValueType = "string",
            RequiresRestart = true,
        });

        // ==================== 游戏机制 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "difficulty",
            ConfigFileName = file,
            DisplayName = "游戏难度",
            Description = "服务器默认的游戏难度。也可以通过 /difficulty 命令在运行时修改。\npeaceful=和平 easy=简单 normal=普通 hard=困难 ⚔️",
            Category = "游戏机制",
            DefaultValue = "easy",
            AllowedValues = ["peaceful", "easy", "normal", "hard"],
            ValueType = "enum",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "gamemode",
            ConfigFileName = file,
            DisplayName = "游戏模式",
            Description = "新玩家加入时的默认游戏模式。\nsurvival=生存 creative=创造 adventure=冒险 spectator=旁观 🎮",
            Category = "游戏机制",
            DefaultValue = "survival",
            AllowedValues = ["survival", "creative", "adventure", "spectator"],
            ValueType = "enum",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "force-gamemode",
            ConfigFileName = file,
            DisplayName = "强制游戏模式",
            Description = "是否强制所有玩家使用默认游戏模式。\n启用后，玩家每次加入服务器都会被设置为默认游戏模式 🔒",
            Category = "游戏机制",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "hardcore",
            ConfigFileName = file,
            DisplayName = "极限模式",
            Description = "是否启用极限模式。死亡后玩家会被切换为旁观模式（banspec）。\n高难度挑战，死亡即永久旁观 💀",
            Category = "游戏机制",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "pvp",
            ConfigFileName = file,
            DisplayName = "PVP",
            Description = "是否允许玩家互相攻击。false=和平服，true=可以打架。\n想搞生存竞技就开，想搞建筑服就关 🤺",
            Category = "游戏机制",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "enable-command-block",
            ConfigFileName = file,
            DisplayName = "命令方块",
            Description = "是否允许命令方块工作。命令方块可以自动执行命令，是地图制作利器。\n如果不是做地图/红石机器，建议关掉以防滥用 💻",
            Category = "游戏机制",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "max-tick-time",
            ConfigFileName = file,
            DisplayName = "单 tick 最大时间",
            Description = "单个游戏 tick 的最大执行时间（毫秒）。\n超过此时间服务器会崩溃并生成崩溃报告。-1=禁用看门狗超时 ⏱️",
            Category = "游戏机制",
            DefaultValue = "60000",
            MinValue = -1,
            MaxValue = int.MaxValue,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "max-chained-neighbor-updates",
            ConfigFileName = file,
            DisplayName = "最大连锁邻居更新",
            Description = "单个方块更新最多引发多少次连锁邻居更新。\n负数=禁用，红石大规模更新时相关的关键参数 🔴",
            Category = "游戏机制",
            DefaultValue = "1000000",
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "entity-broadcast-range-percentage",
            ConfigFileName = file,
            DisplayName = "实体广播范围百分比",
            Description = "实体元数据广播范围占原始范围的百分比。\n实体元数据发送范围 = 原始范围 × 此值%。\n范围 10-1000，默认 100 📡",
            Category = "游戏机制",
            DefaultValue = "100",
            MinValue = 10,
            MaxValue = 1000,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 服务器信息 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "motd",
            ConfigFileName = file,
            DisplayName = "服务器标语 (MOTD)",
            Description = "Message Of The Day —— 服务器在玩家列表里显示的描述文字。\n支持 Minecraft 颜色代码（如 §a 绿色）和格式代码 ✨",
            Category = "服务器信息",
            DefaultValue = "A Minecraft Server",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "enable-status",
            ConfigFileName = file,
            DisplayName = "在线状态显示",
            Description = "服务器是否在服务器列表中显示为在线。\n关闭后服务器不会响应状态查询 📴",
            Category = "服务器信息",
            DefaultValue = "true",
            ValueType = "bool",
        });

        // ==================== 性能优化 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "sync-chunk-writes",
            ConfigFileName = file,
            DisplayName = "同步区块写入",
            Description = "是否同步写入区块数据。\ntrue=防止崩溃导致数据丢失，false=SSD 可设为 false 提升写入速度 💾",
            Category = "性能优化",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "region-file-compression",
            ConfigFileName = file,
            DisplayName = "区域文件压缩",
            Description = "区域文件（.mca）的压缩算法。\ndeflate=默认压缩 lz4=读写最快 none=不压缩 📦",
            Category = "性能优化",
            DefaultValue = "deflate",
            AllowedValues = ["deflate", "lz4", "none"],
            ValueType = "enum",
            RequiresRestart = true,
        });

        // ==================== 远程控制 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "enable-rcon",
            ConfigFileName = file,
            DisplayName = "启用 RCON",
            Description = "是否启用远程控制台（RCON）。启用后可以通过网络发送服务器命令。\n方便管理面板对接，但要注意设置强密码！🔐",
            Category = "远程控制",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "rcon.port",
            ConfigFileName = file,
            DisplayName = "RCON 端口",
            Description = "RCON 远程控制台监听的端口号。建议设为非标准端口以提高安全性。\n记得在防火墙里放行这个端口 🚪",
            Category = "远程控制",
            DefaultValue = "25575",
            MinValue = 1,
            MaxValue = 65535,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "rcon.password",
            ConfigFileName = file,
            DisplayName = "RCON 密码",
            Description = "RCON 远程控制台的密码。\n务必设置强密码，不要用默认值！🔑",
            Category = "远程控制",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "enable-query",
            ConfigFileName = file,
            DisplayName = "启用 Query",
            Description = "是否启用 GameSpy4 Query 协议。\n用于服务器列表查询服务器信息 📊",
            Category = "远程控制",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "query.port",
            ConfigFileName = file,
            DisplayName = "Query 端口",
            Description = "Query 协议监听的端口号。\n默认与 server-port 相同 🔌",
            Category = "远程控制",
            DefaultValue = "25565",
            MinValue = 1,
            MaxValue = 65535,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "enable-jmx-monitoring",
            ConfigFileName = file,
            DisplayName = "启用 JMX 监控",
            Description = "是否启用 JMX 监控。\n用于监控 Java 虚拟机运行状态 📈",
            Category = "远程控制",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "management-server-enabled",
            ConfigFileName = file,
            DisplayName = "管理服务器启用",
            Description = "是否启用管理服务器（JMX/飞行记录器等）。\n生产环境建议关闭 ⚙️",
            Category = "远程控制",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "management-server-host",
            ConfigFileName = file,
            DisplayName = "管理服务器主机",
            Description = "管理服务器绑定的主机地址。\n默认 localhost 🖥️",
            Category = "远程控制",
            DefaultValue = "localhost",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "management-server-port",
            ConfigFileName = file,
            DisplayName = "管理服务器端口",
            Description = "管理服务器监听的端口号。0=自动选择 🔌",
            Category = "远程控制",
            DefaultValue = "0",
            MinValue = 0,
            MaxValue = 65535,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 资源包 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "resource-pack",
            ConfigFileName = file,
            DisplayName = "资源包 URL",
            Description = "服务器资源包的下载地址。\n玩家可以选择是否使用服务器资源包 🎨",
            Category = "资源包",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "resource-pack-sha1",
            ConfigFileName = file,
            DisplayName = "资源包 SHA1",
            Description = "资源包的 SHA1 哈希值，用于验证文件完整性。\n确保玩家下载的资源包没有被篡改 ✅",
            Category = "资源包",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "resource-pack-id",
            ConfigFileName = file,
            DisplayName = "资源包 UUID",
            Description = "资源包的唯一标识符（UUID）。\n用于标识特定的资源包版本 🆔",
            Category = "资源包",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "require-resource-pack",
            ConfigFileName = file,
            DisplayName = "强制资源包",
            Description = "是否强制玩家使用服务器资源包。\ntrue=玩家必须接受资源包才能进入 🔒",
            Category = "资源包",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "resource-pack-prompt",
            ConfigFileName = file,
            DisplayName = "资源包提示",
            Description = "提示玩家是否使用资源包时显示的文字。\n可以写一些说明文字 💬",
            Category = "资源包",
            ValueType = "string",
        });

        // ==================== 安全 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "prevent-proxy-connections",
            ConfigFileName = file,
            DisplayName = "防止代理连接",
            Description = "是否阻止通过代理/VPN 连接的玩家。\n一定程度上防止恶意玩家换 IP 捣乱 🛡️",
            Category = "安全",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "enable-code-of-conduct",
            ConfigFileName = file,
            DisplayName = "启用行为准则",
            Description = "是否启用社区行为准则。\n用于符合某些平台的要求 📜",
            Category = "安全",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        // ==================== 聊天 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "chat-spam-threshold-seconds",
            ConfigFileName = file,
            DisplayName = "聊天刷屏阈值",
            Description = "聊天消息之间的最小间隔（秒）。\n超过此频率发送消息的玩家会被踢出。0=禁用踢出 💬",
            Category = "聊天",
            DefaultValue = "10",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "command-spam-threshold-seconds",
            ConfigFileName = file,
            DisplayName = "命令刷屏阈值",
            Description = "命令之间的最小间隔（秒）。\n超过此频率发送命令的玩家会被踢出。0=禁用踢出 ⌨️",
            Category = "聊天",
            DefaultValue = "10",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "text-filtering-config",
            ConfigFileName = file,
            DisplayName = "聊天过滤配置",
            Description = "聊天内容过滤服务的配置。\n用于过滤不当言论 🚫",
            Category = "聊天",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "text-filtering-version",
            ConfigFileName = file,
            DisplayName = "聊天过滤版本",
            Description = "聊天过滤的版本号。0 或 1 📋",
            Category = "聊天",
            DefaultValue = "0",
            MinValue = 0,
            MaxValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "broadcast-console-to-ops",
            ConfigFileName = file,
            DisplayName = "控制台广播到 OP",
            Description = "是否将控制台输出广播给所有在线 OP 玩家。\n方便 OP 实时查看服务器状态 📢",
            Category = "聊天",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "broadcast-rcon-to-ops",
            ConfigFileName = file,
            DisplayName = "RCON 广播到 OP",
            Description = "是否将 RCON 命令输出广播给所有在线 OP 玩家。\n方便多人协作管理 📡",
            Category = "聊天",
            DefaultValue = "true",
            ValueType = "bool",
        });

        // ==================== 其他 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "pause-when-empty-seconds",
            ConfigFileName = file,
            DisplayName = "空服暂停延迟",
            Description = "服务器无玩家时多久后暂停游戏 tick（秒）。\n0=不暂停，节省空服时的 CPU 占用 ⏸️",
            Category = "其他",
            DefaultValue = "60",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "initial-enabled-packs",
            ConfigFileName = file,
            DisplayName = "初始启用数据包",
            Description = "初始启用的数据包，逗号分隔。\n默认仅启用 vanilla 📦",
            Category = "其他",
            DefaultValue = "vanilla",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "initial-disabled-packs",
            ConfigFileName = file,
            DisplayName = "初始禁用数据包",
            Description = "初始禁用的数据包，逗号分隔。\n默认为空 📭",
            Category = "其他",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "bug-report-link",
            ConfigFileName = file,
            DisplayName = "Bug 报告链接",
            Description = "玩家崩溃时显示的 Bug 报告链接。\n可以指向你自己的问题追踪页面 🐛",
            Category = "其他",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "status-heartbeat-interval",
            ConfigFileName = file,
            DisplayName = "状态心跳间隔",
            Description = "状态心跳的发送间隔（秒）。0=禁用。\n用于某些服务器列表的在线统计 💓",
            Category = "其他",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });
    }

    /// <summary>
    /// 注册 server.properties 配置文件的补充配置项
    /// </summary>
    /// <remarks>补充注册主方法中可能遗漏的常见配置项</remarks>
    private void RegisterServerPropertiesExtras()
    {
        const string file = "server.properties";

        Register(new ServerConfigDescriptor
        {
            Key = "initial-enabled-packet-type",
            ConfigFileName = file,
            DisplayName = "初始启用数据包类型",
            Description = "服务器启动时默认启用的数据包类型列表。用于细粒度网络协议控制。",
            Category = "网络",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "snooper-enabled",
            ConfigFileName = file,
            DisplayName = "Snooper 数据收集",
            Description = "是否启用 Snooper 匿名数据收集（发送到 Mojang 服务器）。建议关闭以保护隐私。",
            Category = "性能优化",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = false,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "pause-when-empty-seconds",
            ConfigFileName = file,
            DisplayName = "空闲暂停秒数",
            Description = "当服务器无玩家时，等待多少秒后自动暂停 tick 以节省 CPU。0=禁用。",
            Category = "性能优化",
            DefaultValue = "0",
            MinValue = 0,
            MaxValue = 3600,
            ValueType = "int",
            RequiresRestart = false,
        });
    }

    /// <summary>
    /// 注册 bukkit.yml 配置文件的关键配置项
    /// </summary>
    /// <remarks>
    /// Bukkit API 层的基础配置，所有 Bukkit 系服务端核心共享此配置文件。
    /// 数据来源：Bukkit 官方文档 + Spigot 默认配置
    /// </remarks>
    private void RegisterBukkitYml()
    {
        const string file = "bukkit.yml";

        // ==================== 世界设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "settings.allow-end",
            ConfigFileName = file,
            DisplayName = "允许末地",
            Description = "是否允许末地世界。\n关闭后玩家无法进入末地 🌑",
            Category = "世界",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.allow-nether",
            ConfigFileName = file,
            DisplayName = "允许下界",
            Description = "是否允许下界（地狱）世界。\n关闭后玩家无法进入下界 🔥",
            Category = "世界",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.world-container",
            ConfigFileName = file,
            DisplayName = "世界容器目录",
            Description = "存放世界文件夹的目录。默认为服务器根目录。\n可以把世界放到其他磁盘或目录 📁",
            Category = "世界",
            DefaultValue = ".",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.default-world-size",
            ConfigFileName = file,
            DisplayName = "默认世界大小",
            Description = "新创建世界的默认大小限制。0=无限制 📏",
            Category = "世界",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 刷怪设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "spawn-limits.monsters",
            ConfigFileName = file,
            DisplayName = "怪物刷怪上限",
            Description = "每个玩家周围的怪物生成上限。\n值越小怪物越少，服务器越轻松 🧟",
            Category = "刷怪",
            DefaultValue = "70",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spawn-limits.animals",
            ConfigFileName = file,
            DisplayName = "动物刷怪上限",
            Description = "每个玩家周围的动物生成上限。🐄",
            Category = "刷怪",
            DefaultValue = "10",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spawn-limits.water-animals",
            ConfigFileName = file,
            DisplayName = "水生动物刷怪上限",
            Description = "每个玩家周围的水生动物生成上限。🐟",
            Category = "刷怪",
            DefaultValue = "15",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spawn-limits.water-ambient",
            ConfigFileName = file,
            DisplayName = "水环境生物刷怪上限",
            Description = "每个玩家周围的水环境生物生成上限（如鱿鱼）。🐙",
            Category = "刷怪",
            DefaultValue = "20",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spawn-limits.ambient",
            ConfigFileName = file,
            DisplayName = "环境生物刷怪上限",
            Description = "每个玩家周围的环境生物生成上限（如蝙蝠）。🦇",
            Category = "刷怪",
            DefaultValue = "15",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "ticks-per.animal-spawns",
            ConfigFileName = file,
            DisplayName = "动物生成间隔",
            Description = "动物生成的间隔（tick）。值越大生成越慢。\n20 tick = 1 秒 ⏱️",
            Category = "刷怪",
            DefaultValue = "400",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "ticks-per.monster-spawns",
            ConfigFileName = file,
            DisplayName = "怪物生成间隔",
            Description = "怪物生成的间隔（tick）。值越大生成越慢。\n20 tick = 1 秒 👾",
            Category = "刷怪",
            DefaultValue = "1",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 自动保存 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "autosave",
            ConfigFileName = file,
            DisplayName = "自动保存",
            Description = "是否启用自动保存。\n关闭后世界数据只在服务器关闭时保存，有数据丢失风险！💾",
            Category = "自动保存",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "autosave-period-ticks",
            ConfigFileName = file,
            DisplayName = "自动保存间隔",
            Description = "自动保存的间隔（tick）。\n默认 5 分钟（6000 tick）⏰",
            Category = "自动保存",
            DefaultValue = "6000",
            MinValue = 100,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 性能优化 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-gc.period-in-ticks",
            ConfigFileName = file,
            DisplayName = "区块 GC 间隔",
            Description = "区块垃圾回收的间隔（tick）。\n定期回收不需要的区块，释放内存 🗑️",
            Category = "性能优化",
            DefaultValue = "400",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-gc.load-threshold",
            ConfigFileName = file,
            DisplayName = "区块加载阈值",
            Description = "触发区块 GC 的加载区块数阈值。\n当加载的区块数超过此值时触发 GC 📊",
            Category = "性能优化",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 玩家设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "settings.connection-throttle",
            ConfigFileName = file,
            DisplayName = "连接节流",
            Description = "同一 IP 两次连接之间的最小间隔（毫秒）。\n防止玩家快速重连攻击 🔒",
            Category = "玩家",
            DefaultValue = "4000",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.legacy-structure-conversion",
            ConfigFileName = file,
            DisplayName = "旧结构转换",
            Description = "是否转换旧版结构数据。\n从旧版本升级服务器时需要 🏛️",
            Category = "玩家",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.shutdown-timeout",
            ConfigFileName = file,
            DisplayName = "关闭超时",
            Description = "服务器关闭时等待玩家数据保存的超时时间（秒）。\n超时后强制关闭 ⏰",
            Category = "玩家",
            DefaultValue = "30",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });
    }

    /// <summary>
    /// 注册 spigot.yml 配置文件的关键配置项
    /// </summary>
    /// <remarks>
    /// Spigot 是 CraftBukkit 的增强版，提供性能优化和功能扩展配置。
    /// 包含实体激活范围、物品合并、世界设置等性能关键参数。
    /// </remarks>
    private void RegisterSpigotYml()
    {
        const string file = "spigot.yml";

        // ==================== 性能优化 - 刷怪 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "mob-spawn-range",
            ConfigFileName = file,
            DisplayName = "怪物生成范围",
            Description = "怪物在玩家周围生成的最大距离（单位：区块）。\n值越小生成的怪物越少，服务器越轻松，但世界会显得空荡荡的 👾",
            Category = "性能优化",
            DefaultValue = "8",
            MinValue = 2,
            MaxValue = 128,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "entity-activation-range.animals",
            ConfigFileName = file,
            DisplayName = "动物激活范围",
            Description = "动物（牛、羊、猪等）在玩家周围多远内会被激活（开始运行 AI 逻辑）。\n降低此值可以显著减少服务器 CPU 占用 🐄",
            Category = "性能优化",
            DefaultValue = "32",
            MinValue = 1,
            MaxValue = 512,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "entity-activation-range.monsters",
            ConfigFileName = file,
            DisplayName = "怪物激活范围",
            Description = "怪物（僵尸、骷髅、爬行者等）的 AI 激活范围。\n这是影响服务器性能的关键参数之一！降低它能让服务器喘口气 🧟",
            Category = "性能优化",
            DefaultValue = "32",
            MinValue = 1,
            MaxValue = 512,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "entity-activation-range.misc",
            ConfigFileName = file,
            DisplayName = "杂项实体激活范围",
            Description = "其他实体（掉落物、矿车、经验球等）的激活范围。\n如果你的服务器地上的掉落物特别多，降低这个值有奇效 ✨",
            Category = "性能优化",
            DefaultValue = "16",
            MinValue = 1,
            MaxValue = 512,
        });

        // ==================== 性能优化 - 合并 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "merge-radius.item",
            ConfigFileName = file,
            DisplayName = "物品合并半径",
            Description = "地上的掉落物品在多大范围内会自动合并为一堆。\n值越大，地面越干净，同时也减少实体数量 📦",
            Category = "性能优化",
            DefaultValue = "2.5",
            MinValue = 0,
            MaxValue = 64,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "merge-radius.exp",
            ConfigFileName = file,
            DisplayName = "经验球合并半径",
            Description = "经验球在多大范围内会自动合并。\n打怪之后满地经验球的罪魁祸首就是这个值太小了 💫",
            Category = "性能优化",
            DefaultValue = "3.0",
            MinValue = 0,
            MaxValue = 64,
        });

        // ==================== 世界设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.view-distance",
            ConfigFileName = file,
            DisplayName = "视距",
            Description = "服务器向玩家发送的区块渲染范围（单位：区块）。\n值越大看到越远，但服务器和客户端负担也越重。建议 8-12 🔭",
            Category = "世界设置",
            DefaultValue = "default",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.merge-radius.item",
            ConfigFileName = file,
            DisplayName = "物品合并半径",
            Description = "地上的掉落物品在多大范围内会自动合并为一堆。\n值越大，地面越干净，同时也减少实体数量 📦",
            Category = "世界设置",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.mob-spawn-range",
            ConfigFileName = file,
            DisplayName = "怪物生成范围",
            Description = "怪物在玩家周围生成的最大距离（单位：区块）。\n值越小生成的怪物越少，服务器越轻松 👾",
            Category = "世界设置",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.entity-per-chunk-save-limit",
            ConfigFileName = file,
            DisplayName = "每区块实体保存限制",
            Description = "每个区块保存的实体数量上限。\n超过此数量的实体会被删除，防止区块实体过多导致卡顿 📊",
            Category = "世界设置",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.growth",
            ConfigFileName = file,
            DisplayName = "作物生长调整",
            Description = "调整各种作物的生长速度倍率。\n值越大生长越快，1.0=默认速度 🌱",
            Category = "世界设置",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.tick-per",
            ConfigFileName = file,
            DisplayName = "Tick 间隔调整",
            Description = "调整各种系统的 tick 执行间隔。\n值越大执行频率越低 ⏱️",
            Category = "世界设置",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.random-light-updates",
            ConfigFileName = file,
            DisplayName = "随机光照更新",
            Description = "是否启用随机光照更新。\n关闭可减少光照计算开销 💡",
            Category = "世界设置",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.save-structure-info",
            ConfigFileName = file,
            DisplayName = "保存结构信息",
            Description = "是否保存结构信息（如村庄、神殿等）。\n关闭可节省少量磁盘空间 🏛️",
            Category = "世界设置",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.max-bulk-chunks",
            ConfigFileName = file,
            DisplayName = "最大批量区块数",
            Description = "批量处理的最大区块数量。\n影响区块发送和处理的效率 📦",
            Category = "世界设置",
            DefaultValue = "5",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.max-entity-collisions",
            ConfigFileName = file,
            DisplayName = "最大实体碰撞数",
            Description = "单个实体每 tick 最多处理的碰撞次数。\n降低可减少实体密集时的性能消耗 💥",
            Category = "世界设置",
            DefaultValue = "8",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.dragon-death-sound-radius",
            ConfigFileName = file,
            DisplayName = "末影龙死亡音效范围",
            Description = "末影龙死亡时播放音效的范围（方块）。\n0=只有附近玩家能听到 🐉",
            Category = "世界设置",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.seed-village",
            ConfigFileName = file,
            DisplayName = "村庄种子",
            Description = "村庄生成的种子。\n用于控制村庄的生成位置 🏘️",
            Category = "世界设置",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.seed-feature",
            ConfigFileName = file,
            DisplayName = "地物种子",
            Description = "地物（洞穴、矿脉等）生成的种子。\n用于控制地物的生成位置 ⛰️",
            Category = "世界设置",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.seed-monument",
            ConfigFileName = file,
            DisplayName = "海底神殿种子",
            Description = "海底神殿生成的种子。\n用于控制海底神殿的生成位置 🏛️",
            Category = "世界设置",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.seed-slime",
            ConfigFileName = file,
            DisplayName = "史莱姆区块种子",
            Description = "史莱姆区块生成的种子。\n用于控制史莱姆生成的区块位置 🟢",
            Category = "世界设置",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.hunger",
            ConfigFileName = file,
            DisplayName = "饥饿机制",
            Description = "饥饿相关的机制调整。\n影响玩家饥饿值消耗速度 🍖",
            Category = "世界设置",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.movement-speed-atk",
            ConfigFileName = file,
            DisplayName = "移动速度攻击修正",
            Description = "移动速度对攻击的修正系数。\n影响移动攻击的伤害计算 ⚔️",
            Category = "世界设置",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.item-dirty-ticks",
            ConfigFileName = file,
            DisplayName = "物品脏 Tick 数",
            Description = "掉落物实体多久标记为脏（需要保存）。\n值越大保存频率越低 📦",
            Category = "世界设置",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.arrow-despawn-rate",
            ConfigFileName = file,
            DisplayName = "箭矢消失速率",
            Description = "射出的箭矢多久后消失（tick）。\n值越小箭矢消失越快 🏹",
            Category = "世界设置",
            DefaultValue = "1200",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.trident-despawn-rate",
            ConfigFileName = file,
            DisplayName = "三叉戟消失速率",
            Description = "投掷的三叉戟多久后消失（tick）。\n值越小消失越快 🔱",
            Category = "世界设置",
            DefaultValue = "1200",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.nerf-spawner-mobs",
            ConfigFileName = file,
            DisplayName = "削弱刷怪笼怪物",
            Description = "是否削弱刷怪笼生成的怪物。\n削弱后的怪物 AI 会减弱，性能更好 🧟",
            Category = "世界设置",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.enable-zombie-pigmen-portal-spawns",
            ConfigFileName = file,
            DisplayName = "猪灵下界传送门生成",
            Description = "是否允许猪灵（僵尸猪人）从下界传送门生成。\n关闭可减少猪灵数量 🐷",
            Category = "世界设置",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.wither-spawn-sound-radius",
            ConfigFileName = file,
            DisplayName = "凋灵生成音效范围",
            Description = "凋灵生成时播放音效的范围（方块）。\n0=只有附近玩家能听到 💀",
            Category = "世界设置",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.hanging-tick-frequency",
            ConfigFileName = file,
            DisplayName = "悬挂实体 Tick 频率",
            Description = "画、物品展示框等悬挂实体的 tick 频率。\n值越大处理频率越低 🖼️",
            Category = "世界设置",
            DefaultValue = "100",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.zombie-aggressive-towards-villager",
            ConfigFileName = file,
            DisplayName = "僵尸攻击村民",
            Description = "僵尸是否主动攻击村民。\n关闭可保护村民不被僵尸攻击 🧟‍♂️",
            Category = "世界设置",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.log-villager-deaths",
            ConfigFileName = file,
            DisplayName = "记录村民死亡",
            Description = "是否在日志中记录村民死亡。\n方便排查村民死亡原因 📋",
            Category = "世界设置",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.log-named-deaths",
            ConfigFileName = file,
            DisplayName = "记录命名实体死亡",
            Description = "是否在日志中记录命名实体的死亡。\n命名实体包括被命名的宠物、村民等 📝",
            Category = "世界设置",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.log-deaths",
            ConfigFileName = file,
            DisplayName = "记录死亡信息",
            Description = "是否在日志中记录实体死亡信息。\n关闭可减少日志输出 📄",
            Category = "世界设置",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.save-usercache-on-player-join",
            ConfigFileName = file,
            DisplayName = "玩家加入时保存用户缓存",
            Description = "玩家加入时是否保存用户缓存（usercache.json）。\n开启可确保缓存及时更新 💾",
            Category = "世界设置",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.player-filter",
            ConfigFileName = file,
            DisplayName = "玩家过滤器",
            Description = "玩家过滤相关设置。\n用于过滤不符合条件的玩家 🚫",
            Category = "世界设置",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.filter-creative-items",
            ConfigFileName = file,
            DisplayName = "过滤创造模式物品",
            Description = "是否过滤创造模式物品栏中的某些物品。\n用于限制创造模式可用物品 🎒",
            Category = "世界设置",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "world-settings.default.world-border",
            ConfigFileName = file,
            DisplayName = "世界边界",
            Description = "世界边界相关设置。\n控制世界边界的大小和行为 🌍",
            Category = "世界设置",
            ValueType = "string",
        });

        // ==================== 玩家设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "players.ping-sample",
            ConfigFileName = file,
            DisplayName = "延迟采样数",
            Description = "服务器列表中显示的玩家延迟采样数量。\n影响服务器列表显示的延迟信息 📊",
            Category = "玩家设置",
            DefaultValue = "12",
            MinValue = 1,
            MaxValue = 1000,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "tab-replace",
            ConfigFileName = file,
            DisplayName = "Tab 列表替换",
            Description = "是否替换 Tab 玩家列表显示。\n用于自定义 Tab 列表显示 📋",
            Category = "玩家设置",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "commands.tab-complete",
            ConfigFileName = file,
            DisplayName = "Tab 命令补全",
            Description = "是否启用命令 Tab 补全。\n关闭可提高安全性 ⌨️",
            Category = "玩家设置",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        // ==================== 网络设置 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "settings.bungeecord",
            ConfigFileName = file,
            DisplayName = "BungeeCord 模式",
            Description = "是否启用 BungeeCord 模式。\n启用后服务器会信任 BungeeCord 转发的玩家信息 🔗",
            Category = "网络设置",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.timeout-time",
            ConfigFileName = file,
            DisplayName = "超时时间",
            Description = "玩家连接超时时间（秒）。\n超过此时间无响应则断开连接 ⏱️",
            Category = "网络设置",
            DefaultValue = "30",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.restart-on-crash",
            ConfigFileName = file,
            DisplayName = "崩溃自动重启",
            Description = "服务器崩溃后是否自动重启。\n需要配合重启脚本使用 🔄",
            Category = "网络设置",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.restart-script",
            ConfigFileName = file,
            DisplayName = "重启脚本",
            Description = "服务器崩溃时执行的重启脚本路径。\n需配合 restart-on-crash 使用 📜",
            Category = "网络设置",
            DefaultValue = "./start.sh",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.player-shuffle",
            ConfigFileName = file,
            DisplayName = "玩家混洗",
            Description = "是否打乱玩家连接处理顺序。\n可防止某些针对连接顺序的攻击 🎲",
            Category = "网络设置",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.advanced-ipc",
            ConfigFileName = file,
            DisplayName = "高级 IPC",
            Description = "是否启用高级进程间通信。\n用于某些插件的跨进程通信 🔌",
            Category = "网络设置",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.watchdog-thread",
            ConfigFileName = file,
            DisplayName = "看门狗线程",
            Description = "是否启用看门狗线程。\n用于检测服务器卡顿并生成报告 🐕",
            Category = "网络设置",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "settings.netty-threads",
            ConfigFileName = file,
            DisplayName = "Netty 线程数",
            Description = "Netty 网络 IO 线程数。\n-1=自动（CPU 核心数的一半）🧵",
            Category = "网络设置",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "late-bind",
            ConfigFileName = file,
            DisplayName = "延迟绑定",
            Description = "是否启用延迟绑定。\n启用后直到玩家完成握手才分配连接资源 🔗",
            Category = "网络设置",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });
    }

    /// <summary>
    /// 注册 config/paper-global.yml 配置文件的关键配置项
    /// </summary>
    /// <remarks>
    /// Paper/Folia 的全局配置文件，包含区域化多线程、方块更新控制、
    /// 区块系统、命令、控制台、物品验证等高级优化配置。
    /// 数据来源：PaperMC 官方文档 + Folia 26.1.2 默认配置
    /// </remarks>
    private void RegisterPaperGlobalYml()
    {
        const string file = "config/paper-global.yml";

        // ==================== Folia 专属：区域化多线程 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "threaded-regions.threads",
            ConfigFileName = file,
            DisplayName = "区域 tick 线程数",
            Description = "Folia 区域化多线程的 tick 线程数量。\n-1=根据 CPU 自动分配。分配完 Netty IO、Chunk IO、Chunk Worker、GC 并发线程后，剩余核心的 80% 以内分配给此项 🧵",
            Category = "区域化多线程",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "threaded-regions.gridExponent",
            ConfigFileName = file,
            DisplayName = "区域大小指数",
            Description = "每个区域 = 2^n × 2^n 区块。\n4=16×16区块(256×256格)；5=32×32(512×512)；6=64×64(1024×1024)。红石机器多时应调大到 6 📐",
            Category = "区域化多线程",
            DefaultValue = "4",
            MinValue = 2,
            MaxValue = 7,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "threaded-regions.scheduler",
            ConfigFileName = file,
            DisplayName = "区域调度算法",
            Description = "区域线程的调度算法。\nEDF=最早截止时间优先（最稳定）；WORK_STEALING=工作窃取（性能更好但已知有问题）⚙️",
            Category = "区域化多线程",
            DefaultValue = "EDF",
            AllowedValues = ["EDF", "WORK_STEALING"],
            ValueType = "enum",
            RequiresRestart = true,
        });

        // ==================== 方块更新控制 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "block-updates.disable-chorus-plant-updates",
            ConfigFileName = file,
            DisplayName = "禁用紫颂植物更新",
            Description = "是否禁用紫颂植物的方块更新。\n可以减少紫颂花/紫颂果生长导致的服务器卡顿 🌸",
            Category = "方块更新",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "block-updates.disable-mushroom-block-updates",
            ConfigFileName = file,
            DisplayName = "禁用蘑菇方块更新",
            Description = "是否禁用蘑菇方块的方块更新。\n减少蘑菇传播导致的更新开销 🍄",
            Category = "方块更新",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "block-updates.disable-noteblock-updates",
            ConfigFileName = file,
            DisplayName = "禁用音符盒更新",
            Description = "是否禁用音符盒的方块更新。\n大型红石音乐机器可能需要 🔔",
            Category = "方块更新",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "block-updates.disable-tripwire-updates",
            ConfigFileName = file,
            DisplayName = "禁用绊线更新",
            Description = "是否禁用绊线的方块更新。\n减少绊线更新开销 🪤",
            Category = "方块更新",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        // ==================== 区块系统 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-system.gen-parallelism",
            ConfigFileName = file,
            DisplayName = "区块生成并行度",
            Description = "区块生成的并行度。\ndefault=自动，true=启用，false=禁用 🏗️",
            Category = "区块系统",
            DefaultValue = "default",
            AllowedValues = ["default", "true", "false"],
            ValueType = "enum",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-system.io-threads",
            ConfigFileName = file,
            DisplayName = "区块 IO 线程数",
            Description = "区块 IO 操作的线程数。-1=自动 📖",
            Category = "区块系统",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-system.worker-threads",
            ConfigFileName = file,
            DisplayName = "区块工作线程数",
            Description = "区块处理工作线程数。-1=自动（物理核心数一半）🔧",
            Category = "区块系统",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 区块加载（高级）====================

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-loading-advanced.auto-config-send-distance",
            ConfigFileName = file,
            DisplayName = "自动配置发送距离",
            Description = "是否基于视距自动匹配发送距离。\n推荐开启，自动优化 📏",
            Category = "区块加载",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-loading-advanced.player-max-concurrent-chunk-generates",
            ConfigFileName = file,
            DisplayName = "每玩家最大并发区块生成",
            Description = "每个玩家最多同时生成多少个区块。0=无限 ⚡",
            Category = "区块加载",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-loading-advanced.player-max-concurrent-chunk-loads",
            ConfigFileName = file,
            DisplayName = "每玩家最大并发区块加载",
            Description = "每个玩家最多同时加载多少个区块。0=无限 📦",
            Category = "区块加载",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 区块加载（基础）====================

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-loading-basic.player-max-chunk-generate-rate",
            ConfigFileName = file,
            DisplayName = "每玩家每秒区块生成速率",
            Description = "每个玩家每秒最多生成多少个区块。-1.0=无限 📊",
            Category = "区块加载",
            DefaultValue = "-1.0",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-loading-basic.player-max-chunk-load-rate",
            ConfigFileName = file,
            DisplayName = "每玩家每秒区块加载速率",
            Description = "每个玩家每秒最多加载多少个区块。-1.0=无限 📈",
            Category = "区块加载",
            DefaultValue = "100.0",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-loading-basic.player-max-chunk-send-rate",
            ConfigFileName = file,
            DisplayName = "每玩家每秒区块发送速率",
            Description = "每个玩家每秒最多发送多少个区块数据包 📡",
            Category = "区块加载",
            DefaultValue = "75.0",
            ValueType = "string",
            RequiresRestart = true,
        });

        // ==================== 碰撞（全局）====================

        Register(new ServerConfigDescriptor
        {
            Key = "collisions.enable-player-collisions",
            ConfigFileName = file,
            DisplayName = "启用玩家碰撞",
            Description = "是否启用玩家之间的碰撞。\n关闭后玩家可以互相穿过 👥",
            Category = "碰撞",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "collisions.send-full-pos-for-hard-colliding-entities",
            ConfigFileName = file,
            DisplayName = "硬碰撞实体完整坐标",
            Description = "是否为硬碰撞的实体发送完整位置信息。\n用于减少位置不同步问题 🎯",
            Category = "碰撞",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        // ==================== 命令 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "commands.ride-command-allow-player-as-vehicle",
            ConfigFileName = file,
            DisplayName = "/ride 允许玩家作载具",
            Description = "是否允许 /ride 命令让玩家作为其他实体的载具。\n可能被滥用，谨慎开启 🐎",
            Category = "命令",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "commands.suggest-player-names-when-null-tab-completions",
            ConfigFileName = file,
            DisplayName = "Tab 补全建议玩家名",
            Description = "当 Tab 补全结果为空时，是否建议玩家名。\n方便输入玩家名 👤",
            Category = "命令",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "commands.time-command-affects-all-worlds",
            ConfigFileName = file,
            DisplayName = "/time 影响所有世界",
            Description = "/time 命令是否同时影响所有世界。\n默认只影响当前世界 ⏰",
            Category = "命令",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        // ==================== 控制台 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "console.enable-brigadier-completions",
            ConfigFileName = file,
            DisplayName = "Brigadier 补全",
            Description = "是否启用控制台命令的 Brigadier Tab 补全。\n让控制台命令输入更智能 💻",
            Category = "控制台",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "console.enable-brigadier-highlighting",
            ConfigFileName = file,
            DisplayName = "Brigadier 高亮",
            Description = "是否启用控制台命令的 Brigadier 语法高亮。\n让命令更易读 🎨",
            Category = "控制台",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "console.has-all-permissions",
            ConfigFileName = file,
            DisplayName = "控制台拥有所有权限",
            Description = "控制台是否默认拥有所有权限。\n关闭后控制台也需要权限插件管理 🔐",
            Category = "控制台",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        // ==================== 物品验证 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "item-validation.display-name",
            ConfigFileName = file,
            DisplayName = "显示名最大长度",
            Description = "物品显示名（DisplayName）的最大字符长度。\n防止过长名称导致客户端崩溃 📏",
            Category = "物品验证",
            DefaultValue = "8192",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "item-validation.lore-line",
            ConfigFileName = file,
            DisplayName = "Lore 每行最大长度",
            Description = "物品 Lore（描述）每行的最大字符长度 📜",
            Category = "物品验证",
            DefaultValue = "8192",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "item-validation.book.author",
            ConfigFileName = file,
            DisplayName = "书作者名最大长度",
            Description = "书本作者名的最大字符长度 ✍️",
            Category = "物品验证",
            DefaultValue = "8192",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "item-validation.book.title",
            ConfigFileName = file,
            DisplayName = "书标题最大长度",
            Description = "书本标题的最大字符长度 📖",
            Category = "物品验证",
            DefaultValue = "8192",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "item-validation.book.page",
            ConfigFileName = file,
            DisplayName = "书每页最大长度",
            Description = "书每页内容的最大字符长度 📄",
            Category = "物品验证",
            DefaultValue = "16384",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "item-validation.book-size.page-max",
            ConfigFileName = file,
            DisplayName = "书最大页数",
            Description = "一本书最多有多少页 📚",
            Category = "物品验证",
            DefaultValue = "2560",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "item-validation.book-size.total-multiplier",
            ConfigFileName = file,
            DisplayName = "书总大小乘数",
            Description = "书本总大小限制的乘数。\n0.0~1.0，值越小限制越严格 ⚖️",
            Category = "物品验证",
            DefaultValue = "0.98",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "item-validation.resolve-selectors-in-books",
            ConfigFileName = file,
            DisplayName = "书中解析选择器",
            Description = "是否在书本中解析目标选择器（如 @a）。\n可能导致性能问题，建议关闭 🎯",
            Category = "物品验证",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        // ==================== 杂项 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "misc.fix-entity-position-desync",
            ConfigFileName = file,
            DisplayName = "修复实体位置不同步",
            Description = "是否修复实体位置不同步的问题。\n推荐开启 ✅",
            Category = "杂项",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.load-permissions-yml-before-plugins",
            ConfigFileName = file,
            DisplayName = "插件前加载权限",
            Description = "是否在插件加载前加载 permissions.yml。\n确保权限配置及时生效 🔑",
            Category = "杂项",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.max-joins-per-tick",
            ConfigFileName = file,
            DisplayName = "每 tick 最大加入玩家数",
            Description = "单个游戏 tick 内最多允许多少玩家加入服务器。\n防止大量玩家同时加入导致卡顿 👥",
            Category = "杂项",
            DefaultValue = "5",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.prevent-negative-villager-demand",
            ConfigFileName = file,
            DisplayName = "防止村民负需求",
            Description = "是否防止村民交易需求变为负数。\n修复村民交易价格异常的问题 🏪",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.region-file-cache-size",
            ConfigFileName = file,
            DisplayName = "区域文件缓存大小",
            Description = "区域文件（.mca）的缓存大小。\n更大的缓存可以减少磁盘 IO，但占用更多内存 💾",
            Category = "杂项",
            DefaultValue = "256",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.send-full-pos-for-item-entities",
            ConfigFileName = file,
            DisplayName = "掉落物完整坐标",
            Description = "是否为掉落物实体发送完整位置信息。\n减少掉落物位置抖动，但增加网络开销 ✨",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.strict-advancement-dimension-check",
            ConfigFileName = file,
            DisplayName = "严格进度维度检查",
            Description = "是否严格检查进度的维度。\n防止玩家在错误的维度解锁进度 🏆",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.use-alternative-luck-formula",
            ConfigFileName = file,
            DisplayName = "替代幸运公式",
            Description = "是否使用替代的幸运值计算公式。\n可能影响附魔、钓鱼等的幸运效果 🍀",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.use-dimension-type-for-custom-spawners",
            ConfigFileName = file,
            DisplayName = "自定义刷怪笼用维度类型",
            Description = "自定义刷怪笼是否使用维度类型来决定刷怪。\n影响自定义刷怪笼的行为 🐣",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.xp-orb-groups-per-area",
            ConfigFileName = file,
            DisplayName = "每区域经验球分组数",
            Description = "每个区域的经验球分组数。default=自动。\n更多分组可以减少经验球卡顿 💫",
            Category = "杂项",
            DefaultValue = "default",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.client-interaction-leniency-distance",
            ConfigFileName = file,
            DisplayName = "客户端交互宽容距离",
            Description = "客户端交互的宽容距离。default=自动。\n值越大，玩家可以从越远的距离与方块/实体交互 🎯",
            Category = "杂项",
            DefaultValue = "default",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.compression-level",
            ConfigFileName = file,
            DisplayName = "网络压缩级别",
            Description = "网络数据包的压缩级别。default=自动，-1~9。\n值越高压缩率越高但 CPU 占用也越高 📦",
            Category = "杂项",
            DefaultValue = "default",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.chat-threads.chat-executor-core-size",
            ConfigFileName = file,
            DisplayName = "聊天执行器核心线程数",
            Description = "聊天处理线程池的核心线程数。-1=自动 💬",
            Category = "杂项",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "misc.chat-threads.chat-executor-max-size",
            ConfigFileName = file,
            DisplayName = "聊天执行器最大线程数",
            Description = "聊天处理线程池的最大线程数。-1=自动 🧵",
            Category = "杂项",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 数据包限制器 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "packet-limiter.all-packets.action",
            ConfigFileName = file,
            DisplayName = "超限操作",
            Description = "数据包超过限制时采取的操作。\nKICK=踢出玩家 DROP=丢弃数据包 🚫",
            Category = "数据包限制器",
            DefaultValue = "KICK",
            AllowedValues = ["KICK", "DROP"],
            ValueType = "enum",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "packet-limiter.all-packets.interval",
            ConfigFileName = file,
            DisplayName = "检测间隔",
            Description = "数据包速率检测的间隔（秒）⏱️",
            Category = "数据包限制器",
            DefaultValue = "7.0",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "packet-limiter.all-packets.max-packet-rate",
            ConfigFileName = file,
            DisplayName = "最大数据包速率",
            Description = "每秒最大数据包数量 📊",
            Category = "数据包限制器",
            DefaultValue = "500.0",
            ValueType = "string",
            RequiresRestart = true,
        });

        // ==================== 玩家自动保存 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "player-auto-save.max-per-tick",
            ConfigFileName = file,
            DisplayName = "每 tick 最大保存玩家数",
            Description = "单个游戏 tick 内最多保存多少个玩家数据。-1=无限 💾",
            Category = "玩家自动保存",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "player-auto-save.rate",
            ConfigFileName = file,
            DisplayName = "自动保存间隔",
            Description = "玩家数据自动保存的间隔（tick）。-1=禁用。\n默认 5 分钟保存一次 ⏰",
            Category = "玩家自动保存",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 垃圾信息限制 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "spam-limiter.incoming-packet-threshold",
            ConfigFileName = file,
            DisplayName = "入站包阈值",
            Description = "入站数据包的阈值，超过则视为垃圾信息 📨",
            Category = "垃圾信息限制",
            DefaultValue = "300",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spam-limiter.recipe-spam-increment",
            ConfigFileName = file,
            DisplayName = "合成配方递增量",
            Description = "每次合成配方操作增加的垃圾值 📈",
            Category = "垃圾信息限制",
            DefaultValue = "1",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spam-limiter.recipe-spam-limit",
            ConfigFileName = file,
            DisplayName = "合成配方限制",
            Description = "合成配方操作的垃圾信息限制值 🚫",
            Category = "垃圾信息限制",
            DefaultValue = "20",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spam-limiter.tab-spam-increment",
            ConfigFileName = file,
            DisplayName = "Tab 补全递增量",
            Description = "每次 Tab 补全操作增加的垃圾值 📊",
            Category = "垃圾信息限制",
            DefaultValue = "1",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spam-limiter.tab-spam-limit",
            ConfigFileName = file,
            DisplayName = "Tab 补全限制",
            Description = "Tab 补全操作的垃圾信息限制值 ⌨️",
            Category = "垃圾信息限制",
            DefaultValue = "500",
            MinValue = 1,
            ValueType = "int",
            RequiresRestart = true,
        });

        // ==================== 不支持设置（风险自担）====================

        Register(new ServerConfigDescriptor
        {
            Key = "unsupported-settings.allow-headless-pistons",
            ConfigFileName = file,
            DisplayName = "允许无头活塞",
            Description = "是否允许无头活塞（headless pistons）。\n可能导致漏洞，慎用！⚠️",
            Category = "不支持设置",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "unsupported-settings.allow-permanent-block-break-exploits",
            ConfigFileName = file,
            DisplayName = "允许永久破坏方块",
            Description = "是否允许永久破坏方块的漏洞。\n严重影响游戏平衡，非常不建议开启！💥",
            Category = "不支持设置",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });
    }

    /// <summary>
    /// 注册 config/paper-world-defaults.yml 配置文件的关键配置项
    /// </summary>
    /// <remarks>
    /// Paper/Folia 的世界默认配置文件，作为各世界个性化配置的模板。
    /// 包含实体、世界生成、杂项等世界级配置项。
    /// 数据来源：PaperMC 官方文档 + Folia 26.1.2 默认配置
    /// </remarks>
    private void RegisterPaperWorldDefaultsYml()
    {
        const string file = "config/paper-world-defaults.yml";

        // ==================== 方块与物理 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "anti-xray",
            ConfigFileName = file,
            DisplayName = "反矿透",
            Description = "反 X 射线（矿透）设置。\n通过隐藏或伪装矿石防止玩家使用透视作弊 🕵️",
            Category = "方块与物理",
            ValueType = "string",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "water-over-lava",
            ConfigFileName = file,
            DisplayName = "水浇岩浆",
            Description = "水流到岩浆上时的行为设置。\n控制水与岩浆交互生成石头/黑曜石的行为 💧",
            Category = "方块与物理",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-ice-and-snow",
            ConfigFileName = file,
            DisplayName = "禁用冰和雪",
            Description = "是否禁用冰和雪的形成与融化。\n开启后冰雪不会自然形成或融化 ❄️",
            Category = "方块与物理",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-thunder",
            ConfigFileName = file,
            DisplayName = "禁用雷暴",
            Description = "是否禁用雷暴天气。\n开启后不会打雷闪电 ⚡",
            Category = "方块与物理",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-raining",
            ConfigFileName = file,
            DisplayName = "禁用下雨",
            Description = "是否禁用下雨天气。\n开启后永远是晴天 ☀️",
            Category = "方块与物理",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "snow-accumulation-height",
            ConfigFileName = file,
            DisplayName = "积雪堆积高度",
            Description = "雪自然堆积的最大高度（层）。\n0=无限制 🌨️",
            Category = "方块与物理",
            DefaultValue = "8",
            MinValue = 0,
            MaxValue = 256,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "grass-spread",
            ConfigFileName = file,
            DisplayName = "草方块蔓延",
            Description = "草方块的蔓延速度调整。\n控制草方块向泥土蔓延的速率 🌿",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "mycelium-spread",
            ConfigFileName = file,
            DisplayName = "菌丝蔓延",
            Description = "菌丝方块的蔓延速度调整。\n控制菌丝向泥土蔓延的速率 🍄",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "vine-growth",
            ConfigFileName = file,
            DisplayName = "藤蔓生长",
            Description = "藤蔓的生长速度调整。\n控制藤蔓蔓延和生长的速率 🌱",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "cocoa-growth",
            ConfigFileName = file,
            DisplayName = "可可豆生长",
            Description = "可可豆的生长速度调整。\n控制可可豆成熟的速率 🌰",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "bamboo-growth",
            ConfigFileName = file,
            DisplayName = "竹子生长",
            Description = "竹子的生长速度调整。\n控制竹子长高的速率 🎋",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "kelp-growth",
            ConfigFileName = file,
            DisplayName = "海带生长",
            Description = "海带的生长速度调整。\n控制海带生长的速率 🌊",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "sugar-cane-growth",
            ConfigFileName = file,
            DisplayName = "甘蔗生长",
            Description = "甘蔗的生长速度调整。\n控制甘蔗长高的速率 🎋",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "cactus-growth",
            ConfigFileName = file,
            DisplayName = "仙人掌生长",
            Description = "仙人掌的生长速度调整。\n控制仙人掌长高的速率 🌵",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "pumpkin-and-melon-growth",
            ConfigFileName = file,
            DisplayName = "南瓜和西瓜生长",
            Description = "南瓜和西瓜的生长速度调整。\n控制南瓜和西瓜结果的速率 🎃",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "mushroom-growth",
            ConfigFileName = file,
            DisplayName = "蘑菇生长",
            Description = "蘑菇的生长速度调整。\n控制蘑菇蔓延和巨型蘑菇生成的速率 🍄",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "leaf-decay",
            ConfigFileName = file,
            DisplayName = "树叶腐烂",
            Description = "树叶的腐烂速度调整。\n控制树木被砍伐后树叶消失的速率 🍂",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "ice-and-snow",
            ConfigFileName = file,
            DisplayName = "冰和雪",
            Description = "冰和雪的形成/融化速度调整。\n控制冰雪的自然变化速率 ❄️",
            Category = "方块与物理",
            DefaultValue = "default",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "thunder",
            ConfigFileName = file,
            DisplayName = "雷暴",
            Description = "雷暴天气相关设置。\n控制打雷闪电的频率和行为 ⚡",
            Category = "方块与物理",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "rain",
            ConfigFileName = file,
            DisplayName = "降雨",
            Description = "降雨天气相关设置。\n控制下雨的频率和行为 🌧️",
            Category = "方块与物理",
            ValueType = "string",
        });

        // ==================== 实体 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "armor-stands",
            ConfigFileName = file,
            DisplayName = "盔甲架",
            Description = "盔甲架相关设置。\n控制盔甲架的行为和优化 🛡️",
            Category = "实体",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "painting",
            ConfigFileName = file,
            DisplayName = "画",
            Description = "画相关设置。\n控制画的放置和行为 🖼️",
            Category = "实体",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "sitting",
            ConfigFileName = file,
            DisplayName = "坐",
            Description = "玩家坐的相关设置。\n控制玩家坐下的行为 🪑",
            Category = "实体",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "zombie-pigmen-portal-spawn",
            ConfigFileName = file,
            DisplayName = "猪灵传送门生成",
            Description = "猪灵（僵尸猪人）从下界传送门生成的设置。\n控制猪灵生成的概率和数量 🐷",
            Category = "实体",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "wandering-trader",
            ConfigFileName = file,
            DisplayName = "流浪商人",
            Description = "流浪商人的生成设置。\n控制流浪商人出现的频率和条件 🐪",
            Category = "实体",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spawner-nerfed-mobs-should-jump",
            ConfigFileName = file,
            DisplayName = "刷怪笼削弱怪物跳跃",
            Description = "被削弱的刷怪笼怪物是否还能跳跃。\n开启后削弱的怪物仍可跳跃 🦘",
            Category = "实体",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "per-player-mob-spawns",
            ConfigFileName = file,
            DisplayName = "每玩家怪物生成",
            Description = "是否按玩家单独计算怪物生成上限。\n开启后多玩家不会共享怪物上限 👥",
            Category = "实体",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "fix-items-merging-through-walls",
            ConfigFileName = file,
            DisplayName = "修复穿墙物品合并",
            Description = "是否修复掉落物穿墙合并的问题。\n开启后物品不会穿过墙壁合并 🧱",
            Category = "实体",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-chest-cat-detection",
            ConfigFileName = file,
            DisplayName = "禁用箱子猫检测",
            Description = "是否禁用箱子上的猫检测。\n开启后猫不会阻止打开箱子，性能更好 🐱",
            Category = "实体",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-end-credits",
            ConfigFileName = file,
            DisplayName = "禁用终末之诗",
            Description = "是否禁用击败末影龙后的终末之诗和字幕。\n开启后击败末影龙直接重生 🎬",
            Category = "实体",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-relative-projectile-velocity",
            ConfigFileName = file,
            DisplayName = "禁用相对弹射物速度",
            Description = "是否禁用弹射物的相对速度计算。\n修复某些弹射物速度异常的问题 🏹",
            Category = "实体",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-sprint-interruption-on-attack",
            ConfigFileName = file,
            DisplayName = "禁用攻击打断冲刺",
            Description = "是否禁用攻击时打断玩家冲刺。\n开启后攻击不会打断冲刺 🏃",
            Category = "实体",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "shield-blocking-delay",
            ConfigFileName = file,
            DisplayName = "盾牌格挡延迟",
            Description = "举盾后到能格挡的延迟时间（tick）。\n值越大举盾后需要等越久才能格挡 🛡️",
            Category = "实体",
            DefaultValue = "5",
            MinValue = 0,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "only-players-collide",
            ConfigFileName = file,
            DisplayName = "仅玩家碰撞",
            Description = "是否只有玩家之间会发生碰撞。\n开启后玩家不会与其他实体碰撞 👥",
            Category = "实体",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "max-leash-distance",
            ConfigFileName = file,
            DisplayName = "最大牵引距离",
            Description = "拴绳的最大距离（方块）。\n超过此距离拴绳会断裂 🐕",
            Category = "实体",
            DefaultValue = "10.0",
            MinValue = 1,
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "projectile-burden",
            ConfigFileName = file,
            DisplayName = "弹射物负担",
            Description = "弹射物的性能负担设置。\n控制弹射物数量上限以优化性能 🎯",
            Category = "实体",
            ValueType = "string",
        });

        // ==================== 世界生成 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "disable-vanilla-api-ticking",
            ConfigFileName = file,
            DisplayName = "禁用原版 API Tick",
            Description = "是否禁用原版 API 的 tick 事件。\n可能提升性能但影响某些插件 ⚙️",
            Category = "世界生成",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "generate-random-seeds-for-all",
            ConfigFileName = file,
            DisplayName = "为所有结构生成随机种子",
            Description = "是否为所有结构生成随机种子。\n开启后每个世界的结构位置更随机 🌱",
            Category = "世界生成",
            DefaultValue = "false",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "seed-based-feature-search",
            ConfigFileName = file,
            DisplayName = "基于种子的地物搜索",
            Description = "是否启用基于种子的地物搜索优化。\n加速 locate 等命令的搜索速度 🔍",
            Category = "世界生成",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "seed-based-feature-search-loads-chunks",
            ConfigFileName = file,
            DisplayName = "地物搜索加载区块",
            Description = "基于种子的地物搜索是否加载区块。\n关闭可减少搜索时的区块加载 📦",
            Category = "世界生成",
            DefaultValue = "true",
            ValueType = "bool",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "optimize-explosions",
            ConfigFileName = file,
            DisplayName = "优化爆炸",
            Description = "是否优化爆炸的计算。\n开启后爆炸性能更好，行为略有不同 💥",
            Category = "世界生成",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "optimize-hoppers",
            ConfigFileName = file,
            DisplayName = "优化漏斗",
            Description = "是否优化漏斗的行为。\n开启后漏斗性能更好 🚰",
            Category = "世界生成",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "hopper-can-load-chunks",
            ConfigFileName = file,
            DisplayName = "漏斗可加载区块",
            Description = "漏斗是否能够加载区块。\n关闭可防止漏斗跨区块加载导致的性能问题 📦",
            Category = "世界生成",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "allow-non-player-entities-on-scoreboards",
            ConfigFileName = file,
            DisplayName = "允许非玩家实体在计分板",
            Description = "是否允许非玩家实体出现在计分板上。\n关闭可提升计分板性能 📊",
            Category = "世界生成",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "display-connection-messages-on-first-join",
            ConfigFileName = file,
            DisplayName = "首次加入显示连接消息",
            Description = "是否仅在玩家首次加入时显示连接消息。\n减少刷屏 💬",
            Category = "世界生成",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "do-not-tick-entities-in-unloaded-chunks",
            ConfigFileName = file,
            DisplayName = "不处理未加载区块实体",
            Description = "是否不对未加载区块中的实体进行 tick 处理。\n防止实体在未加载区块中异常 🚫",
            Category = "世界生成",
            DefaultValue = "false",
            ValueType = "bool",
        });

        // ==================== 杂项 ====================

        Register(new ServerConfigDescriptor
        {
            Key = "fire-tick-delay",
            ConfigFileName = file,
            DisplayName = "火焰 Tick 延迟",
            Description = "火焰传播的 tick 延迟。\n值越大火焰蔓延越慢 🔥",
            Category = "杂项",
            DefaultValue = "30",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "light-queue-size",
            ConfigFileName = file,
            DisplayName = "光照队列大小",
            Description = "光照更新队列的最大大小。\n过大可能导致卡顿，过小可能导致光照不同步 💡",
            Category = "杂项",
            DefaultValue = "10000",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "auto-save-interval",
            ConfigFileName = file,
            DisplayName = "自动保存间隔",
            Description = "世界自动保存的间隔（tick）。\n默认 5 分钟（6000 tick）⏰",
            Category = "杂项",
            DefaultValue = "6000",
            MinValue = 100,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "fix-curing-zombie-villager-discount-exploit",
            ConfigFileName = file,
            DisplayName = "修复村民交易折扣漏洞",
            Description = "是否修复多次治愈僵尸村民导致交易折扣叠加的漏洞。\n防止玩家刷低价交易 🏪",
            Category = "杂项",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "mobs-can-always-use-spawn-egg",
            ConfigFileName = file,
            DisplayName = "生物总能用刷怪蛋",
            Description = "怪物是否总能使用刷怪蛋生成。\n默认受刷怪限制影响 🥚",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "allow-leashing-villagers",
            ConfigFileName = file,
            DisplayName = "允许拴住村民",
            Description = "是否允许用拴绳拴住村民。\n方便搬运村民 👨‍🌾",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spawn-chunks-size",
            ConfigFileName = file,
            DisplayName = "出生点区块大小",
            Description = "出生点区块的大小（区块）。\n出生点区块会常驻加载 📍",
            Category = "杂项",
            DefaultValue = "3",
            MinValue = 0,
            ValueType = "int",
            RequiresRestart = true,
        });

        Register(new ServerConfigDescriptor
        {
            Key = "spawn-chunks-tick",
            ConfigFileName = file,
            DisplayName = "出生点区块 Tick",
            Description = "出生点区块是否进行 tick 处理。\n关闭可节省出生点区块的性能消耗 ⏱️",
            Category = "杂项",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "max-auto-save-chunks-per-tick",
            ConfigFileName = file,
            DisplayName = "每 tick 最大自动保存区块数",
            Description = "单个 tick 内最多自动保存多少个区块。\n限制保存速度防止卡顿 💾",
            Category = "杂项",
            DefaultValue = "20",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "falling-block-height-nerf",
            ConfigFileName = file,
            DisplayName = "下落方块高度削弱",
            Description = "超过此高度的下落方块会被直接删除。\n0=禁用，防止大量下落方块导致卡顿 📦",
            Category = "杂项",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "tnt-entity-height-nerf",
            ConfigFileName = file,
            DisplayName = "TNT 实体高度削弱",
            Description = "超过此高度的 TNT 实体会被直接删除。\n0=禁用，防止高空 TNT 导致卡顿 💣",
            Category = "杂项",
            DefaultValue = "0",
            MinValue = 0,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "water-over-lava-flow-speed",
            ConfigFileName = file,
            DisplayName = "水在岩浆上流速",
            Description = "水在岩浆上方流动的速度倍率。\n影响水浇岩浆生成石头的速度 💧",
            Category = "杂项",
            DefaultValue = "2",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "grass-spread-tick-rate",
            ConfigFileName = file,
            DisplayName = "草蔓延 Tick 速率",
            Description = "草方块蔓延的 tick 速率。\n值越大蔓延越慢 🌿",
            Category = "杂项",
            DefaultValue = "1",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "bed-search-radius",
            ConfigFileName = file,
            DisplayName = "床位搜索半径",
            Description = "玩家重生时搜索床位的半径（方块）。\n值越大找床范围越大 🛏️",
            Category = "杂项",
            DefaultValue = "1",
            MinValue = 1,
            MaxValue = 10,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-explosion-knockback",
            ConfigFileName = file,
            DisplayName = "禁用爆炸击退",
            Description = "是否禁用爆炸的击退效果。\n开启后爆炸不会击退实体 💥",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "water-lava-flow-speed",
            ConfigFileName = file,
            DisplayName = "水岩浆流速",
            Description = "水和岩浆的流动速度设置。\n控制液体流动的快慢 🌊",
            Category = "杂项",
            ValueType = "string",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "fixed-chunk-inhabited-time",
            ConfigFileName = file,
            DisplayName = "固定区块居住时间",
            Description = "是否使用固定的区块居住时间。\n影响区块的游戏机制难度 ⏰",
            Category = "杂项",
            DefaultValue = "-1",
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "use-vanilla-world-scoreboard-name-coloring",
            ConfigFileName = file,
            DisplayName = "使用原版计分板名称着色",
            Description = "是否使用原版计分板的名称着色方式。\n关闭可支持更多颜色格式 🎨",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "remove-corrupt-tile-entities",
            ConfigFileName = file,
            DisplayName = "移除损坏的方块实体",
            Description = "是否自动移除损坏的方块实体（如箱子、刷怪笼等）。\n防止损坏数据导致崩溃 🗑️",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "experience-merge-max-value",
            ConfigFileName = file,
            DisplayName = "经验合并最大值",
            Description = "经验球合并后的最大经验值。\n防止单个经验球经验过高导致不平衡 💫",
            Category = "杂项",
            DefaultValue = "-1",
            MinValue = -1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "prevent-moving-into-unloaded-chunks",
            ConfigFileName = file,
            DisplayName = "防止移入未加载区块",
            Description = "是否阻止玩家移动到未加载的区块中。\n防止玩家卡入未加载区域 🚫",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "ender-dragons-death-always-places-dragon-egg",
            ConfigFileName = file,
            DisplayName = "末影龙死亡总是生成龙蛋",
            Description = "每次击败末影龙是否都生成龙蛋。\n默认只有第一次会生成 🥚",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "use-faster-eigencraft-redstone",
            ConfigFileName = file,
            DisplayName = "使用快速红石算法",
            Description = "是否使用更快的 Eigencraft 红石算法。\n大幅提升红石性能，可能有细微行为差异 🔴",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "map-item-frame-cursor-limit",
            ConfigFileName = file,
            DisplayName = "地图物品展示框光标限制",
            Description = "每个地图上物品展示框光标的最大数量。\n过多光标可能导致性能问题 🗺️",
            Category = "杂项",
            DefaultValue = "128",
            MinValue = 0,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "allow-permanent-block-break-exploits",
            ConfigFileName = file,
            DisplayName = "允许永久破坏方块漏洞",
            Description = "是否允许永久破坏方块的漏洞。\n严重影响游戏平衡，非常不建议开启！💥",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "allow-headless-pistons",
            ConfigFileName = file,
            DisplayName = "允许无头活塞",
            Description = "是否允许无头活塞（headless pistons）。\n可能导致漏洞，慎用！⚠️",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "allow-piston-duplication",
            ConfigFileName = file,
            DisplayName = "允许活塞复制",
            Description = "是否允许活塞复制物品的漏洞。\n严重破坏游戏平衡，非常不建议开启！📦",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "perform-username-validation",
            ConfigFileName = file,
            DisplayName = "执行用户名验证",
            Description = "是否验证用户名的合法性。\n防止使用非法字符的用户名 👤",
            Category = "杂项",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "validate-function-tags-before-applying",
            ConfigFileName = file,
            DisplayName = "应用函数标签前验证",
            Description = "是否在应用函数标签前进行验证。\n防止无效函数标签导致错误 ✅",
            Category = "杂项",
            DefaultValue = "true",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "entities-target-with-follow-range",
            ConfigFileName = file,
            DisplayName = "实体使用跟随范围寻敌",
            Description = "实体是否使用跟随范围来寻找目标。\n可能减少实体寻敌的性能消耗 🎯",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "mob-spawner-tick-rate",
            ConfigFileName = file,
            DisplayName = "刷怪笼 Tick 速率",
            Description = "刷怪笼的 tick 处理速率。\n值越大刷怪笼工作越慢 🐣",
            Category = "杂项",
            DefaultValue = "1",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "chunk-tasks-per-tick",
            ConfigFileName = file,
            DisplayName = "每 tick 区块任务数",
            Description = "单个 tick 内最多执行多少个区块任务。\n限制区块处理速度防止卡顿 📦",
            Category = "杂项",
            DefaultValue = "1000",
            MinValue = 1,
            ValueType = "int",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-end-portal-creation",
            ConfigFileName = file,
            DisplayName = "禁用末地传送门创建",
            Description = "是否禁用末地传送门的创建。\n开启后无法激活末地传送门 🌀",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });

        Register(new ServerConfigDescriptor
        {
            Key = "disable-wither-spawning",
            ConfigFileName = file,
            DisplayName = "禁用凋灵生成",
            Description = "是否禁用凋灵的生成。\n开启后无法召唤凋灵 💀",
            Category = "杂项",
            DefaultValue = "false",
            ValueType = "bool",
        });
    }
}
