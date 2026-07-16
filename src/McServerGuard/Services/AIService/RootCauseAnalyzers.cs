// 🧩 根因分析器集合 —— 各个领域的专业分析器
// 每个分析器专注一个领域，像专科医生一样会诊
namespace McServerGuard.Services.AIService;

using System.Linq;
using System.Text.RegularExpressions;

// ─── 🔌 插件错误分析器 ───────────────────────────────────────────

/// <summary>
/// 插件错误分析器
/// 分析插件加载失败、依赖缺失、版本不兼容、配置错误等
/// </summary>
public class PluginErrorAnalyzer : IRootCauseAnalyzer
{
    public string Name => "PluginErrorAnalyzer";

    public RootCauseCandidate? Analyze(AnalysisContext context)
    {
        var line = context.TargetLogLine;
        var lower = line.ToLowerInvariant();
        var category = context.PrimaryResult.Category;

        // 如果不是插件相关的错误，快速返回
        if (!category.Contains("Plugin") && !lower.Contains("plugin") && !lower.Contains("plugin.yml"))
            return null;

        int confidence = 0;
        string rootCause = "PluginIssue";
        string description = "插件相关问题";
        var suggestions = new List<string>();

        // 1. 插件加载失败
        if (lower.Contains("cannot load plugin") || lower.Contains("failed to load plugin"))
        {
            confidence = 60;
            rootCause = "PluginLoadFailed";
            description = "插件无法加载";

            // 进一步细分原因
            if (lower.Contains("invalid") && lower.Contains("plugin.yml"))
            {
                confidence = 85;
                rootCause = "PluginConfigInvalid";
                description = "插件配置文件 plugin.yml 无效或损坏";
                suggestions.AddRange([
                    "重新下载该插件，确保文件完整",
                    "用 YAML 编辑器检查 plugin.yml 格式",
                    "确认插件版本与服务端兼容"
                ]);
            }
            else if (lower.Contains("depend") || lower.Contains("dependency"))
            {
                confidence = 80;
                rootCause = "PluginDependencyMissing";
                description = "插件缺少依赖的其他插件";
                suggestions.AddRange([
                    "查看完整错误，找出缺失的依赖插件",
                    "安装所需的依赖插件（如 Vault、ProtocolLib 等）",
                    "检查依赖插件的版本是否兼容"
                ]);
            }
            else if (lower.Contains("version") || lower.Contains("api-version"))
            {
                confidence = 75;
                rootCause = "PluginVersionMismatch";
                description = "插件版本与服务端版本不兼容";
                suggestions.AddRange([
                    "检查插件是否支持当前服务端版本",
                    "升级或降级插件到兼容版本",
                    "考虑使用替代插件"
                ]);
            }
            else if (lower.Contains("classnotfound") || lower.Contains("noclassdeffound"))
            {
                confidence = 70;
                rootCause = "PluginClassNotFound";
                description = "插件缺少类文件，可能是不完整的 jar";
                suggestions.AddRange([
                    "重新下载插件",
                    "检查插件 jar 是否损坏",
                    "确认下载来源可靠"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "查看完整的错误堆栈信息",
                    "检查插件版本是否与服务端兼容",
                    "尝试禁用最近安装的插件"
                ]);
            }
        }

        // 2. 插件重复
        if (lower.Contains("plugin already initialized") || lower.Contains("duplicate"))
        {
            confidence = 95;
            rootCause = "PluginDuplicate";
            description = "插件重复加载，plugins 目录中有重复的 jar 文件";
            suggestions.AddRange([
                "检查 plugins 目录，删除重复或旧版本的插件",
                "保留最新版本的插件 jar"
            ]);
        }

        // 3. 插件运行时错误
        if (lower.Contains("plugin") && lower.Contains("exception") && !lower.Contains("load"))
        {
            confidence = 55;
            rootCause = "PluginRuntimeError";
            description = "插件运行时异常";
            
            if (lower.Contains("nullpointer"))
            {
                confidence = 70;
                description += "（空指针异常，通常是插件 bug）";
                suggestions.AddRange([
                    "这是插件本身的 bug，尝试更新到最新版",
                    "向插件作者报告此问题",
                    "临时方案：禁用该插件"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "检查插件配置是否正确",
                    "查看完整堆栈定位问题",
                    "更新插件到最新版本"
                ]);
            }
        }

        if (confidence == 0) return null;

        return new RootCauseCandidate
        {
            RootCause = rootCause,
            RootCauseDescription = description,
            Confidence = confidence,
            Category = "Plugin",
            DetailedSuggestions = suggestions
        };
    }
}

// ─── 🌍 世界/存档错误分析器 ─────────────────────────────────────

/// <summary>
/// 世界/存档错误分析器
/// 分析区块损坏、世界加载失败、版本不兼容等
/// </summary>
public class WorldErrorAnalyzer : IRootCauseAnalyzer
{
    public string Name => "WorldErrorAnalyzer";

    public RootCauseCandidate? Analyze(AnalysisContext context)
    {
        var line = context.TargetLogLine;
        var lower = line.ToLowerInvariant();
        var category = context.PrimaryResult.Category;

        bool isWorldRelated = category is "WorldLoadFail" or "ChunkError"
            || lower.Contains("world") || lower.Contains("chunk")
            || lower.Contains("region") || lower.Contains("save");

        if (!isWorldRelated)
            return null;

        int confidence = 0;
        string rootCause = "WorldIssue";
        string description = "世界/存档相关问题";
        var suggestions = new List<string>();

        // 1. 世界加载失败
        if (lower.Contains("failed to load world") || lower.Contains("could not load world"))
        {
            confidence = 70;
            rootCause = "WorldLoadFailed";
            description = "世界无法加载";

            if (lower.Contains("version") || lower.Contains("old"))
            {
                confidence = 85;
                rootCause = "WorldVersionMismatch";
                description = "世界版本与服务端版本不兼容";
                suggestions.AddRange([
                    "确认世界是用哪个版本创建的",
                    "使用 WorldEdit/MCA Selector 等工具转换世界",
                    "先用相同版本的服务端打开世界并备份"
                ]);
            }
            else if (lower.Contains("corrupt") || lower.Contains("damaged"))
            {
                confidence = 90;
                rootCause = "WorldCorrupted";
                description = "世界存档已损坏";
                suggestions.AddRange([
                    "立即备份当前世界！",
                    "使用 MCA Selector 检查并修复损坏的区块",
                    "从最近的备份恢复",
                    "考虑使用区域备份工具（如 Chunky）"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "检查 server.properties 中的 level-name 是否正确",
                    "确认世界文件夹存在且未被重命名",
                    "检查 world 文件夹的读写权限"
                ]);
            }
        }

        // 2. 区块加载错误
        if (lower.Contains("chunk") && (lower.Contains("error") || lower.Contains("exception")))
        {
            confidence = 65;
            rootCause = "ChunkLoadError";
            description = "区块加载错误";

            if (lower.Contains("out of memory") || lower.Contains("oom"))
            {
                confidence = 80;
                rootCause = "ChunkLoadMemory";
                description = "区块加载导致内存不足";
                suggestions.AddRange([
                    "增加服务器内存（-Xmx）",
                    "减少 view-distance 和 simulation-distance",
                    "使用 Paper 的区块加载优化"
                ]);
            }
            else if (lower.Contains("corrupt"))
            {
                confidence = 85;
                description = "区块数据损坏";
                suggestions.AddRange([
                    "用 MCA Selector 定位并修复/删除损坏区块",
                    "从备份恢复该区域的区域文件"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "检查是否有过多的实体或方块实体",
                    "减少区块加载范围",
                    "考虑使用区块预生成插件"
                ]);
            }
        }

        // 3. 保存错误
        if (lower.Contains("save") && lower.Contains("fail"))
        {
            confidence = 70;
            rootCause = "WorldSaveFailed";
            description = "世界保存失败";
            suggestions.AddRange([
                "检查磁盘空间是否充足",
                "确认服务器对世界目录有写入权限",
                "检查磁盘是否有坏道"
            ]);
        }

        if (confidence == 0) return null;

        return new RootCauseCandidate
        {
            RootCause = rootCause,
            RootCauseDescription = description,
            Confidence = confidence,
            Category = "World",
            DetailedSuggestions = suggestions
        };
    }
}

// ─── ⚡ 性能错误分析器 ───────────────────────────────────────────

/// <summary>
/// 性能错误分析器
/// 分析 TPS 下降、内存不足、实体爆炸、红石卡顿等
/// </summary>
public class PerformanceErrorAnalyzer : IRootCauseAnalyzer
{
    public string Name => "PerformanceErrorAnalyzer";

    public RootCauseCandidate? Analyze(AnalysisContext context)
    {
        var line = context.TargetLogLine;
        var lower = line.ToLowerInvariant();
        var category = context.PrimaryResult.Category;

        bool isPerfRelated = category is "TpsDrop" or "LongTick" or "EntityOverload" or "OutOfMemory" or "LowMemory"
            || lower.Contains("tps") || lower.Contains("tick") || lower.Contains("lag")
            || lower.Contains("out of memory") || lower.Contains("heap");

        if (!isPerfRelated)
            return null;

        int confidence = 0;
        string rootCause = "PerformanceIssue";
        string description = "性能相关问题";
        var suggestions = new List<string>();

        // 1. TPS 下降
        if (category == "TpsDrop" || lower.Contains("can't keep up"))
        {
            confidence = 60;
            rootCause = "TpsDrop";
            description = "服务器 TPS 下降";

            // 从上下文找线索
            var recentText = string.Join(" ", context.RecentLogs.Select(l => l.Line.ToLowerInvariant()));

            if (recentText.Contains("entity") || lower.Contains("entity"))
            {
                confidence = 75;
                rootCause = "TpsDrop_EntityOverload";
                description = "TPS 下降的主要原因是实体数量过多";
                suggestions.AddRange([
                    "安装 ClearLagg 或 EntityTrackerFixer 插件",
                    "降低 spigot.yml 中的 entity-tracking-range",
                    "减少刷怪塔和动物农场的规模",
                    "启用 mob-spawn-range 优化"
                ]);
            }
            else if (recentText.Contains("redstone") || lower.Contains("redstone"))
            {
                confidence = 70;
                rootCause = "TpsDrop_Redstone";
                description = "TPS 下降可能与红石电路有关";
                suggestions.AddRange([
                    "检查是否有高频红石电路",
                    "使用 Carpet 或 RedstoneTools 查找卡顿源",
                    "考虑限制高频红石"
                ]);
            }
            else if (recentText.Contains("chunk") || lower.Contains("chunk"))
            {
                confidence = 70;
                rootCause = "TpsDrop_ChunkGen";
                description = "TPS 下降可能与区块生成有关";
                suggestions.AddRange([
                    "使用 Chunky 预生成区块",
                    "减少 view-distance",
                    "优化世界生成设置"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "使用 Spark 性能分析工具精确定位卡顿源",
                    "减少 view-distance 和 simulation-distance",
                    "检查是否有高延迟的插件事件",
                    "考虑升级服务器 CPU"
                ]);
            }
        }

        // 2. 内存溢出
        if (category == "OutOfMemory" || lower.Contains("outofmemory") || lower.Contains("out of memory"))
        {
            confidence = 90;
            rootCause = "OutOfMemory";
            description = "服务器内存溢出（OOM）";

            if (lower.Contains("metaspace"))
            {
                confidence = 95;
                description = "Metaspace 元空间不足（加载了太多类）";
                suggestions.AddRange([
                    "增加 -XX:MaxMetaspaceSize 参数",
                    "检查是否安装了过多的插件",
                    "移除不必要的 mod/插件"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "立即增加 -Xmx 值（建议至少 4G）",
                    "检查是否有内存泄漏的插件（用 Spark 分析）",
                    "减少 view-distance 和实体数量",
                    "使用 Aikar 优化参数改善内存管理"
                ]);
            }
        }

        // 3. 实体爆炸
        if (category == "EntityOverload" || lower.Contains("too many entities"))
        {
            confidence = 85;
            rootCause = "EntityOverload";
            description = "实体数量过多导致性能下降";
            suggestions.AddRange([
                "安装 ClearLagg 定期清理掉落物和怪物",
                "降低 spawn-limits 配置",
                "使用 entity-per-chunk-limit 限制单区块实体数",
                "检查是否有刷怪塔失控"
            ]);
        }

        if (confidence == 0) return null;

        return new RootCauseCandidate
        {
            RootCause = rootCause,
            RootCauseDescription = description,
            Confidence = confidence,
            Category = "Performance",
            DetailedSuggestions = suggestions
        };
    }
}

// ─── 🌐 网络错误分析器 ───────────────────────────────────────────

/// <summary>
/// 网络错误分析器
/// 分析端口占用、连接超时、防火墙等问题
/// </summary>
public class NetworkErrorAnalyzer : IRootCauseAnalyzer
{
    public string Name => "NetworkErrorAnalyzer";

    public RootCauseCandidate? Analyze(AnalysisContext context)
    {
        var line = context.TargetLogLine;
        var lower = line.ToLowerInvariant();
        var category = context.PrimaryResult.Category;

        bool isNetRelated = category is "PortInUse" or "ConnectionTimeout" or "ConnectionIssue"
            || lower.Contains("connection") || lower.Contains("bind") || lower.Contains("port")
            || lower.Contains("network") || lower.Contains("timeout");

        if (!isNetRelated)
            return null;

        int confidence = 0;
        string rootCause = "NetworkIssue";
        string description = "网络相关问题";
        var suggestions = new List<string>();

        // 1. 端口占用
        if (category == "PortInUse" || lower.Contains("address already in use") || lower.Contains("bindexception"))
        {
            confidence = 95;
            rootCause = "PortAlreadyInUse";
            description = "端口被占用，服务器无法绑定";
            suggestions.AddRange([
                "检查是否已有服务器实例在运行",
                "用 netstat -ano | findstr :25565 查看占用进程",
                "修改 server.properties 中的 server-port",
                "如果是开了多个服务器，确保端口不冲突"
            ]);

            // 进一步判断可能是谁占用
            if (lower.Contains("25565"))
            {
                description += "（默认端口 25565）";
            }
        }

        // 2. 连接超时/断开
        if (category is "ConnectionTimeout" or "ConnectionIssue" 
            || lower.Contains("timed out") || lower.Contains("connection reset") || lower.Contains("readtimedout"))
        {
            confidence = 55;
            rootCause = "ConnectionTimeout";
            description = "玩家连接超时或断开";

            // 从日志中提取更多信息
            if (lower.Contains("bungee") || lower.Contains("velocity"))
            {
                confidence = 70;
                description += "（可能与代理服务器有关）";
                suggestions.AddRange([
                    "检查 BungeeCord/Velocity 配置",
                    "确认服务器已设置为离线模式（online-mode=false）",
                    "检查代理与服务器之间的网络连通性"
                ]);
            }
            else if (lower.Contains("login") || lower.Contains("handshake"))
            {
                confidence = 65;
                description += "（登录阶段断开）";
                suggestions.AddRange([
                    "检查网络带宽是否充足",
                    "考虑增加 proxy-protocol 支持",
                    "检查防火墙/安全组设置"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "检查服务器网络带宽和延迟",
                    "确认防火墙没有阻止连接",
                    "如果是家用服务器，检查端口映射是否正确",
                    "考虑使用 TCPShield 等反代服务缓解 DDoS"
                ]);
            }
        }

        // 3. 服务器启动但无法连接的提示
        if (lower.Contains("failed to bind"))
        {
            confidence = 90;
            rootCause = "BindFailed";
            description = "服务器无法绑定到指定地址/端口";
            suggestions.AddRange([
                "检查 server-ip 设置是否正确（通常留空即可）",
                "确认端口没有被其他程序占用",
                "如果指定了 server-ip，确认该 IP 属于本机"
            ]);
        }

        if (confidence == 0) return null;

        return new RootCauseCandidate
        {
            RootCause = rootCause,
            RootCauseDescription = description,
            Confidence = confidence,
            Category = "Network",
            DetailedSuggestions = suggestions
        };
    }
}

// ─── ☕ JVM/Java 错误分析器 ──────────────────────────────────────

/// <summary>
/// JVM/Java 错误分析器
/// 分析 Java 版本不兼容、参数错误、JVM 崩溃等
/// </summary>
public class JvmErrorAnalyzer : IRootCauseAnalyzer
{
    public string Name => "JvmErrorAnalyzer";

    public RootCauseCandidate? Analyze(AnalysisContext context)
    {
        var line = context.TargetLogLine;
        var lower = line.ToLowerInvariant();
        var category = context.PrimaryResult.Category;

        bool isJvmRelated = category is "FatalError" or "StackOverflow" or "OutOfMemory"
            || lower.Contains("jvm") || lower.Contains("java") || lower.Contains("unsupportedclassversion")
            || lower.Contains("fatal error") || lower.Contains("stackoverflow");

        if (!isJvmRelated)
            return null;

        int confidence = 0;
        string rootCause = "JvmIssue";
        string description = "JVM/Java 相关问题";
        var suggestions = new List<string>();

        // 1. Java 版本不兼容
        if (lower.Contains("unsupportedclassversion") || lower.Contains("class version") || lower.Contains("major.minor"))
        {
            confidence = 95;
            rootCause = "JavaVersionMismatch";
            description = "Java 版本不兼容！插件/核心需要更高版本的 Java";
            
            // 尝试判断需要什么版本
            if (lower.Contains("61.0") || lower.Contains("17"))
            {
                description += "（需要 Java 17+）";
            }
            else if (lower.Contains("65.0") || lower.Contains("21"))
            {
                description += "（需要 Java 21+）";
            }

            suggestions.AddRange([
                "升级 Java 到兼容版本（Paper/Folia 推荐 Java 21）",
                "如果是插件报错，更新插件或降级服务端",
                "确认 JAVA_HOME 指向正确的版本"
            ]);
        }

        // 2. JVM 致命错误
        if (category == "FatalError" || lower.Contains("fatal error has been detected"))
        {
            confidence = 80;
            rootCause = "JvmCrash";
            description = "JVM 虚拟机崩溃";

            if (lower.Contains("out of memory") || lower.Contains("oom"))
            {
                confidence = 90;
                description += "（内存不足导致崩溃）";
                suggestions.AddRange([
                    "增加 -Xmx 内存参数",
                    "检查是否有内存泄漏",
                    "使用 Aikar 参数优化内存管理"
                ]);
            }
            else if (lower.Contains("siginfo"))
            {
                confidence = 85;
                description += "（可能是本地库/JNI 问题）";
                suggestions.AddRange([
                    "检查是否使用了本地库（如 OpenJ9 的类库）",
                    "尝试更换 JVM 实现（Temurin/Zulu/Amazon Corretto）",
                    "查看 hs_err_pid*.log 文件获取详细信息"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "查看 hs_err_pid*.log 错误日志文件",
                    "尝试更换 JVM 版本或发行版",
                    "检查是否有硬件问题（内存故障）"
                ]);
            }
        }

        // 3. 栈溢出
        if (category == "StackOverflow" || lower.Contains("stackoverflowerror"))
        {
            confidence = 80;
            rootCause = "StackOverflow";
            description = "栈溢出（递归过深或方法调用链过长）";
            suggestions.AddRange([
                "通常是插件或 mod 的 bug，更新相关插件",
                "可以尝试增大 -Xss 栈大小（但不推荐作为长期方案）",
                "用 Spark 或堆栈跟踪定位问题代码"
            ]);
        }

        // 4. 找不到 jar
        if (category == "JarNotFound" || lower.Contains("unable to access jarfile"))
        {
            confidence = 95;
            rootCause = "JarNotFound";
            description = "找不到服务器核心 JAR 文件";
            suggestions.AddRange([
                "检查启动命令中的 jar 文件路径是否正确",
                "确认 jar 文件存在且文件名拼写正确",
                "检查当前工作目录是否正确"
            ]);
        }

        if (confidence == 0) return null;

        return new RootCauseCandidate
        {
            RootCause = rootCause,
            RootCauseDescription = description,
            Confidence = confidence,
            Category = "JVM",
            DetailedSuggestions = suggestions
        };
    }
}

// ─── 🎮 玩家/游戏内错误分析器 ────────────────────────────────────

/// <summary>
/// 玩家/游戏内错误分析器
/// 分析玩家数据异常、作弊、权限等问题
/// </summary>
public class PlayerErrorAnalyzer : IRootCauseAnalyzer
{
    public string Name => "PlayerErrorAnalyzer";

    public RootCauseCandidate? Analyze(AnalysisContext context)
    {
        var line = context.TargetLogLine;
        var lower = line.ToLowerInvariant();

        bool isPlayerRelated = lower.Contains("player") || lower.Contains("playerdata")
            || (lower.Contains("join") && lower.Contains("game"))
            || lower.Contains("permission");

        if (!isPlayerRelated)
            return null;

        int confidence = 0;
        string rootCause = "PlayerIssue";
        string description = "玩家/游戏内相关问题";
        var suggestions = new List<string>();

        // 1. 玩家数据加载失败
        if (lower.Contains("player data") && (lower.Contains("fail") || lower.Contains("error")))
        {
            confidence = 75;
            rootCause = "PlayerDataError";
            description = "玩家数据文件加载失败，可能损坏";
            suggestions.AddRange([
                "检查 playerdata 目录中对应玩家的 .dat 文件",
                "用 NBTExplorer 检查玩家数据文件是否损坏",
                "从备份恢复玩家数据"
            ]);
        }

        // 2. 权限相关
        if (lower.Contains("permission") && (lower.Contains("error") || lower.Contains("denied")))
        {
            confidence = 60;
            rootCause = "PermissionIssue";
            description = "权限插件相关问题";
            suggestions.AddRange([
                "检查权限插件配置（LuckPerms/PermissionsEx 等）",
                "确认玩家所在组的权限设置",
                "检查权限插件是否正常加载"
            ]);
        }

        // 3. 玩家踢出/封禁
        if (lower.Contains("kicked") || lower.Contains("ban"))
        {
            confidence = 70;
            rootCause = "PlayerKicked";
            description = "玩家被踢出或封禁";

            if (lower.Contains("flying") || lower.Contains("fly"))
            {
                confidence = 85;
                description += "（飞行检测，可能是反作弊误判）";
                suggestions.AddRange([
                    "检查 Paper 的 disable-player-interaction-prediction 设置",
                    "如果有允许飞行的插件，确保正确配置",
                    "考虑调整 NoCheatPlus/Matrix 等反作弊的检测灵敏度"
                ]);
            }
            else if (lower.Contains("spam"))
            {
                confidence = 80;
                description += "（聊天刷屏检测）";
                suggestions.AddRange([
                    "检查聊天过滤插件配置",
                    "调整刷屏检测的阈值"
                ]);
            }
            else
            {
                suggestions.AddRange([
                    "查看踢出原因",
                    "检查是否有反作弊误报",
                    "确认玩家是否违规"
                ]);
            }
        }

        if (confidence == 0) return null;

        return new RootCauseCandidate
        {
            RootCause = rootCause,
            RootCauseDescription = description,
            Confidence = confidence,
            Category = "Player",
            DetailedSuggestions = suggestions
        };
    }
}

// ─── 🛡️ 自身故障排除分析器 ──────────────────────────────────────

/// <summary>
/// 自身故障排除分析器
/// 检查错误是不是 McServerGuard 自己造成的
/// </summary>
public class SelfFaultAnalyzer : IRootCauseAnalyzer
{
    public string Name => "SelfFaultAnalyzer";

    private static readonly HashSet<string> _mcsgKeywords = new()
    {
        "mcserverguard",
        "mcguard",
        "mc_guard",
        "mcserver",
        "serverguard",
        "mcsg"
    };

    public RootCauseCandidate? Analyze(AnalysisContext context)
    {
        var line = context.TargetLogLine;
        var lower = line.ToLowerInvariant();

        // 检查日志中是否包含 McServerGuard 相关的关键词
        foreach (var keyword in _mcsgKeywords)
        {
            if (lower.Contains(keyword))
            {
                return new RootCauseCandidate
                {
                    RootCause = "McsgSelfFault",
                    RootCauseDescription = "错误可能与 McServerGuard 本身有关（但概率较低）",
                    Confidence = 15,
                    Category = "Self",
                    DetailedSuggestions =
                    [
                        "检查 McServerGuard 是否是最新版本",
                        "尝试关闭 McServerGuard 看错误是否消失",
                        "如果确认是 McServerGuard 的问题，请向开发者反馈"
                    ]
                };
            }
        }

        // 检查是否是监控本身导致的性能问题
        if (lower.Contains("can't keep up") && context.RecentLogs.Any(l => l.Line.ToLowerInvariant().Contains("mcserverguard")))
        {
            return new RootCauseCandidate
            {
                RootCause = "McsgPerformanceImpact",
                RootCauseDescription = "TPS 下降可能与 McServerGuard 的监控有关（低概率）",
                Confidence = 5,
                Category = "Self",
                DetailedSuggestions =
                [
                    "McServerGuard 的监控开销很小，通常不是主因",
                    "可以尝试减少数据采集频率",
                    "先排查其他更可能的原因（插件、实体等）"
                ]
            };
        }

        return null;
    }
}
