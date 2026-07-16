// 🕵️ 日志异常检测器 —— 关键词 + 正则双层匹配
// 覆盖 Minecraft 服务器常见的 20+ 种日志场景
// 关键词匹配简单粗暴但够用，正则用于更精准的模式识别
namespace McServerGuard.Services.AIService;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using Serilog;

/// <summary>
/// 日志异常检测器 —— 关键词优先 + 正则兜底的日志分类器
/// 
/// 匹配策略（从快到慢，从精准到模糊）：
/// 1. 关键词快速匹配 —— 简单的 Contains，性能最好
/// 2. 正则精准匹配 —— 处理需要模式匹配的场景
/// 3. 日志级别回退 —— 都没命中时用 WARN/ERROR 关键词兜底
/// </summary>
public class LogAnomalyDetector
{
    // ─── 关键词匹配表（最快，优先匹配） ──────────────────────

    /// <summary>
    /// 关键词规则表 —— 用简单的 Contains 就能匹配的场景
    /// Key = 关键词（小写），Value = 对应的规则
    /// 
    /// 为什么用关键词而不是全正则？
    /// - 性能好：O(n) 字符串包含匹配比正则快得多
    /// - 可维护：服主也能看懂，不像正则像天书
    /// - 够用：Minecraft 日志格式相对固定，关键词足够覆盖 80% 场景
    /// </summary>
    private static readonly Dictionary<string, LogKeywordRule> _keywordRules = new()
    {
        // ─── 🔴 致命错误 ─────────────────────────────────────
        ["outofmemoryerror"] = new(
            Keyword: "OutOfMemoryError",
            Severity: LogSeverity.Critical,
            Category: "OutOfMemory",
            Description: "内存溢出！JVM 堆内存不足，服务器可能随时崩溃",
            Suggestions: ["增加 -Xmx 值（建议至少 4G）", "检查是否有内存泄漏的插件", "减少 view-distance", "考虑使用 Aikar 优化参数"]
        ),
        ["stackoverflow"] = new(
            Keyword: "StackOverflowError",
            Severity: LogSeverity.Critical,
            Category: "StackOverflow",
            Description: "栈溢出！递归过深或方法调用层级爆炸",
            Suggestions: ["检查最近安装的 mod/插件", "增大 -Xss 栈大小", "更新相关依赖到最新版"]
        ),
        ["fatal error"] = new(
            Keyword: "fatal error",
            Severity: LogSeverity.Critical,
            Category: "FatalError",
            Description: "JVM 致命错误！虚拟机可能已经崩溃",
            Suggestions: ["检查 JVM 版本是否与服务器兼容", "查看 hs_err_pid*.log 文件", "尝试更换 JVM 实现（如 Temurin/Zulu）"]
        ),
        ["a fatal error has been detected"] = new(
            Keyword: "A fatal error has been detected",
            Severity: LogSeverity.Critical,
            Category: "FatalError",
            Description: "JVM 检测到致命错误，即将崩溃",
            Suggestions: ["检查 JVM 版本", "查看错误日志文件", "可能是硬件问题（内存/CPU）"]
        ),

        // ─── 🟠 严重错误 ─────────────────────────────────────
        ["cannot load plugin"] = new(
            Keyword: "Cannot load plugin",
            Severity: LogSeverity.Error,
            Category: "PluginLoadFail",
            Description: "插件加载失败！某个插件无法正常启动",
            Suggestions: ["检查插件版本是否与服务端兼容", "查看完整错误堆栈", "更新或替换该插件", "检查插件依赖是否齐全"]
        ),
        ["plugin already initialized"] = new(
            Keyword: "Plugin already initialized",
            Severity: LogSeverity.Error,
            Category: "PluginDuplicate",
            Description: "插件重复加载！可能有重复的 jar 文件",
            Suggestions: ["检查 plugins 目录是否有重复的插件", "删除旧版本的插件 jar"]
        ),
        ["invalid plugin.yml"] = new(
            Keyword: "Invalid plugin.yml",
            Severity: LogSeverity.Error,
            Category: "PluginConfigError",
            Description: "插件配置文件 plugin.yml 无效或损坏",
            Suggestions: ["重新下载插件", "检查 plugin.yml 格式是否正确"]
        ),
        ["unable to access jarfile"] = new(
            Keyword: "Unable to access jarfile",
            Severity: LogSeverity.Error,
            Category: "JarNotFound",
            Description: "找不到服务器核心 JAR 文件！路径错误或文件被移动",
            Suggestions: ["检查启动命令中的 jar 路径", "确认 jar 文件存在且未被占用"]
        ),
        ["address already in use"] = new(
            Keyword: "Address already in use",
            Severity: LogSeverity.Error,
            Category: "PortInUse",
            Description: "端口被占用！服务器无法绑定到指定端口",
            Suggestions: ["检查是否已有服务器在运行", "修改 server.properties 中的 server-port", "用 netstat 查看端口占用情况"]
        ),
        ["bind failed"] = new(
            Keyword: "BindException",
            Severity: LogSeverity.Error,
            Category: "PortInUse",
            Description: "端口绑定失败",
            Suggestions: ["检查端口是否被占用", "确认防火墙设置"]
        ),
        ["world failed to load"] = new(
            Keyword: "Failed to load world",
            Severity: LogSeverity.Error,
            Category: "WorldLoadFail",
            Description: "世界加载失败！存档可能损坏",
            Suggestions: ["备份世界存档", "尝试用 MCA Selector 修复", "检查是否有区块损坏"]
        ),
        ["chunk loading error"] = new(
            Keyword: "Chunk loading error",
            Severity: LogSeverity.Error,
            Category: "ChunkError",
            Description: "区块加载错误，可能导致世界异常",
            Suggestions: ["备份存档", "用区块修复工具检查", "减少区块加载范围"]
        ),
        ["sql"] = new(
            Keyword: "SQLException",
            Severity: LogSeverity.Error,
            Category: "DatabaseError",
            Description: "数据库错误！可能是权限插件或数据存储插件出问题",
            Suggestions: ["检查数据库连接配置", "确认数据库服务是否运行", "检查用户名密码是否正确"]
        ),
        ["mysql"] = new(
            Keyword: "MySQLNonTransientConnectionException",
            Severity: LogSeverity.Error,
            Category: "DatabaseError",
            Description: "MySQL 数据库连接失败",
            Suggestions: ["检查 MySQL 服务状态", "检查连接配置", "确认网络连通性"]
        ),

        // ─── 🟡 警告 ─────────────────────────────────────────
        ["can't keep up!"] = new(
            Keyword: "Can't keep up!",
            Severity: LogSeverity.Warning,
            Category: "TpsDrop",
            Description: "服务器主线程跟不上！TPS 已下降，玩家会感觉到卡顿",
            Suggestions: ["减少 view-distance", "启用 Paper 的 anti-xray 优化", "检查高延迟插件", "升级 CPU 或优化实体数量"]
        ),
        ["tick took"] = new(
            Keyword: "Tick took",
            Severity: LogSeverity.Warning,
            Category: "LongTick",
            Description: "单 tick 耗时过长，可能导致 TPS 下降",
            Suggestions: ["优化世界生成速度", "减少红石电路复杂度", "清理过多实体", "检查区块加载策略"]
        ),
        ["too many entities"] = new(
            Keyword: "too many entities",
            Severity: LogSeverity.Warning,
            Category: "EntityOverload",
            Description: "实体数量过多，可能导致性能严重下降",
            Suggestions: ["安装 ClearLagg 等清理插件", "减少刷怪塔范围", "启用 entity-cramming", "降低 spawn-limits"]
        ),
        ["entity.*exceeded"] = new(
            Keyword: "exceeded entity limit",
            Severity: LogSeverity.Warning,
            Category: "EntityOverload",
            Description: "实体数量超出限制",
            Suggestions: ["清理多余实体", "调整刷怪配置"]
        ),
        ["connection reset"] = new(
            Keyword: "connection reset",
            Severity: LogSeverity.Warning,
            Category: "ConnectionIssue",
            Description: "连接被重置，玩家可能掉线",
            Suggestions: ["检查服务器网络", "优化代理配置", "检查防火墙"]
        ),
        ["timed out"] = new(
            Keyword: "timed out",
            Severity: LogSeverity.Warning,
            Category: "ConnectionTimeout",
            Description: "连接超时，玩家掉线",
            Suggestions: ["检查带宽是否充足", "优化 BungeeCord/Velocity", "检查防火墙设置"]
        ),
        ["readtimedout"] = new(
            Keyword: "ReadTimedOut",
            Severity: LogSeverity.Warning,
            Category: "ConnectionTimeout",
            Description: "读取超时，玩家连接断开",
            Suggestions: ["检查网络稳定性", "考虑增加超时时间"]
        ),
        ["deprecated"] = new(
            Keyword: "deprecated",
            Severity: LogSeverity.Info,
            Category: "Deprecated",
            Description: "使用了已弃用的 API，未来版本可能失效",
            Suggestions: ["更新相关插件", "关注插件更新日志"]
        ),
        ["low memory"] = new(
            Keyword: "low memory",
            Severity: LogSeverity.Warning,
            Category: "LowMemory",
            Description: "内存不足警告，可能影响性能",
            Suggestions: ["增加 -Xmx", "优化插件数量", "使用 Aikar 参数"]
        ),

        // ─── ℹ️ 信息类（正常但值得关注） ─────────────────────
        ["server started"] = new(
            Keyword: "Done (",
            Severity: LogSeverity.Info,
            Category: "ServerStarted",
            Description: "服务器启动完成！",
            Suggestions: []
        ),
        ["starting minecraft server"] = new(
            Keyword: "Starting minecraft server",
            Severity: LogSeverity.Info,
            Category: "ServerStarting",
            Description: "服务器正在启动中...",
            Suggestions: []
        ),
        ["preparing spawn area"] = new(
            Keyword: "Preparing spawn area",
            Severity: LogSeverity.Info,
            Category: "WorldGenerating",
            Description: "正在生成出生点区域",
            Suggestions: []
        ),
        ["joined the game"] = new(
            Keyword: "joined the game",
            Severity: LogSeverity.Info,
            Category: "PlayerJoin",
            Description: "玩家加入游戏",
            Suggestions: []
        ),
        ["left the game"] = new(
            Keyword: "left the game",
            Severity: LogSeverity.Info,
            Category: "PlayerQuit",
            Description: "玩家离开游戏",
            Suggestions: []
        ),
        ["stopping server"] = new(
            Keyword: "Stopping server",
            Severity: LogSeverity.Info,
            Category: "ServerStopping",
            Description: "服务器正在关闭",
            Suggestions: []
        ),
        ["saving chunks"] = new(
            Keyword: "Saving chunks",
            Severity: LogSeverity.Info,
            Category: "WorldSaving",
            Description: "正在保存世界",
            Suggestions: []
        ),
    };

    // ─── 正则匹配表（需要模式匹配的复杂场景） ───────────────

    /// <summary>
    /// 正则规则表 —— 处理需要模式匹配的场景
    /// </summary>
    private static readonly List<LogPatternRule> _regexRules =
    [
        new(
            Pattern: @"Exception",
            Severity: LogSeverity.Error,
            Category: "Exception",
            Description: "检测到未处理的异常",
            Suggestions: ["查看完整堆栈信息定位问题", "检查相关插件/mod 版本兼容性"]
        ),
    ];

    // ─── 统计信息 ──────────────────────────────────────────

    private readonly ConcurrentDictionary<string, long> _categoryCounts = new();
    private readonly ConcurrentDictionary<LogSeverity, long> _severityCounts = new();
    private long _totalLines;

    /// <summary>
    /// 分析一行日志，判断是否为异常
    /// 匹配顺序：关键词 → 正则 → 日志级别兜底
    /// </summary>
    public LogAnalysisResult Classify(string logLine)
    {
        Interlocked.Increment(ref _totalLines);

        if (string.IsNullOrWhiteSpace(logLine))
        {
            return new LogAnalysisResult(false, LogSeverity.Normal, "Empty", "空日志行", []);
        }

        // 第一步：关键词快速匹配（性能最好）
        var lowerLine = logLine.ToLowerInvariant();
        foreach (var (keyword, rule) in _keywordRules)
        {
            if (lowerLine.Contains(keyword))
            {
                RecordHit(rule.Category, rule.Severity);
                Log.Debug("🎯 关键词匹配 [{Category}]: {Keyword}", rule.Category, rule.Keyword);

                return new LogAnalysisResult(
                    IsAnomaly: rule.Severity >= LogSeverity.Warning,
                    Severity: rule.Severity,
                    Category: rule.Category,
                    Description: rule.Description,
                    Suggestions: rule.Suggestions
                );
            }
        }

        // 第二步：正则精准匹配
        foreach (var rule in _regexRules)
        {
            if (rule.Regex.IsMatch(logLine))
            {
                RecordHit(rule.Category, rule.Severity);
                Log.Debug("🔍 正则匹配 [{Category}]", rule.Category);

                return new LogAnalysisResult(
                    IsAnomaly: rule.Severity >= LogSeverity.Warning,
                    Severity: rule.Severity,
                    Category: rule.Category,
                    Description: rule.Description,
                    Suggestions: rule.Suggestions
                );
            }
        }

        // 第三步：日志级别回退分类
        return ClassifyByLogLevel(lowerLine);
    }

    /// <summary>
    /// 通过日志级别关键字进行回退分类
    /// </summary>
    private LogAnalysisResult ClassifyByLogLevel(string lowerLine)
    {
        // 要求日志级别出现在标准前缀位置（如 [ERROR]、[WARN]、[INFO] 或行首）
        // 避免误报正常文本中包含 "error" / "warn" 子串的情况
        if (HasLogLevelPrefix(lowerLine, "error"))
        {
            RecordHit("LogLevelError", LogSeverity.Error);
            return new LogAnalysisResult(
                IsAnomaly: true,
                Severity: LogSeverity.Error,
                Category: "LogLevelError",
                Description: "日志中包含 ERROR 级别信息",
                Suggestions: ["查看具体错误内容", "检查相关组件"]
            );
        }

        if (HasLogLevelPrefix(lowerLine, "warn"))
        {
            RecordHit("LogLevelWarning", LogSeverity.Warning);
            return new LogAnalysisResult(
                IsAnomaly: true,
                Severity: LogSeverity.Warning,
                Category: "LogLevelWarning",
                Description: "日志中包含 WARN 级别信息",
                Suggestions: ["关注警告内容，可能演变为错误"]
            );
        }

        if (HasLogLevelPrefix(lowerLine, "info"))
        {
            RecordHit("LogLevelInfo", LogSeverity.Info);
            return new LogAnalysisResult(
                IsAnomaly: false,
                Severity: LogSeverity.Info,
                Category: "LogLevelInfo",
                Description: "日志中包含 INFO 级别信息",
                Suggestions: []
            );
        }

        return new LogAnalysisResult(
            IsAnomaly: false,
            Severity: LogSeverity.Normal,
            Category: "Normal",
            Description: "未检测到异常",
            Suggestions: []
        );
    }

    /// <summary>
    /// 检查日志行是否包含标准日志级别前缀，避免误报
    /// 支持格式：[ERROR]、[WARN]、[INFO]、ERROR:、WARN:、行首 ERROR 等
    /// </summary>
    private static bool HasLogLevelPrefix(string line, string level)
    {
        // [ERROR]、[WARN]、[INFO] 等方括号格式
        if (line.Contains($"[{level}]")) return true;
        // 冒号格式：ERROR:、WARN:
        if (line.Contains($"{level}:")) return true;
        // 行首格式（前面是空格或时间戳）
        var idx = line.IndexOf(level, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            // 确保前面是边界字符（空格、[、(、: 或行首）
            if (idx == 0) return true;
            var prev = line[idx - 1];
            if (prev is ' ' or '[' or '(' or ':' or ']' or '>')
                return true;
        }
        return false;
    }

    /// <summary>
    /// 记录一次匹配命中（用于统计）
    /// </summary>
    private void RecordHit(string category, LogSeverity severity)
    {
        _categoryCounts.AddOrUpdate(category, 1, (_, count) => count + 1);
        _severityCounts.AddOrUpdate(severity, 1, (_, count) => count + 1);
    }

    /// <summary>
    /// 获取日志统计信息
    /// </summary>
    public LogStatistics GetStatistics()
    {
        return new LogStatistics(
            TotalLines: _totalLines,
            CriticalCount: _severityCounts.GetValueOrDefault(LogSeverity.Critical),
            ErrorCount: _severityCounts.GetValueOrDefault(LogSeverity.Error),
            WarningCount: _severityCounts.GetValueOrDefault(LogSeverity.Warning),
            InfoCount: _severityCounts.GetValueOrDefault(LogSeverity.Info),
            CategoryBreakdown: new Dictionary<string, long>(_categoryCounts)
        );
    }

    /// <summary>
    /// 重置统计
    /// </summary>
    public void ResetStatistics()
    {
        _categoryCounts.Clear();
        _severityCounts.Clear();
        _totalLines = 0;
    }

    #region 内部类型定义

    /// <summary>
    /// 关键词规则 —— 简单的 Contains 匹配
    /// </summary>
    private sealed record LogKeywordRule(
        string Keyword,
        LogSeverity Severity,
        string Category,
        string Description,
        List<string> Suggestions
    );

    /// <summary>
    /// 正则规则 —— 需要模式匹配的场景
    /// </summary>
    private sealed record LogPatternRule(
        string Pattern,
        LogSeverity Severity,
        string Category,
        string Description,
        List<string> Suggestions
    )
    {
        public Regex Regex { get; } = new(Pattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    }

    #endregion
}

/// <summary>
/// 日志统计信息
/// </summary>
public record LogStatistics(
    long TotalLines,
    long CriticalCount,
    long ErrorCount,
    long WarningCount,
    long InfoCount,
    Dictionary<string, long> CategoryBreakdown
);
