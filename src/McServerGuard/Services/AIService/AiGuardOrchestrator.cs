// 🎭 AI 保障编排器 —— 自动学习的智能守护
// 不需要手动导入模型！它会自动：
// 1. 扫描服务器日志目录，发现新日志就自动学习
// 2. 基于规则引擎实时分析 + 从历史日志中积累经验
// 3. 动态调整检测规则的权重和阈值
// 4. 发现新的异常模式时自动注册为规则
namespace McServerGuard.Services.AIService;

using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using McServerGuard.Models;
using Serilog;

/// <summary>
/// AI 保障编排器 —— IAiGuardService 的实现
/// 核心特性：自学习。不需要用户手动导入模型。
/// 从服务器运行日志中自动学习异常模式，动态调整规则权重。
/// </summary>
public class AiGuardOrchestrator : IAiGuardService, IAiSelfLearningService
{
    private readonly LogAnomalyDetector _logDetector;
    private readonly CrashPredictor _crashPredictor;
    private readonly ConfigOptimizer _configOptimizer;
    private readonly LogRootCauseAnalyzer _rootCauseAnalyzer;

    private bool _initialized;

    // 🧠 自学习相关
    private readonly ConcurrentDictionary<string, int> _patternHitCounts = new();
    private readonly ConcurrentDictionary<string, DateTime> _patternLastSeen = new();
    private readonly List<LearnedPattern> _learnedPatterns = [];
    private readonly object _learnedPatternsLock = new();
    private readonly string _learningDataPath;
    private DateTime _lastLogScanTime = DateTime.MinValue;
    private string? _lastScannedLogDir;

    private readonly ConcurrentDictionary<string, LogAnalysisResult?> _patternMatchCache = new();
    private const int MaxCacheSize = 2000;

    public AiGuardOrchestrator(
        LogAnomalyDetector logDetector,
        CrashPredictor crashPredictor,
        ConfigOptimizer configOptimizer,
        LogRootCauseAnalyzer rootCauseAnalyzer)
    {
        Log.Information("🎭 AiGuardOrchestrator 初始化（自学习模式 + 根因分析）...");
        _logDetector = logDetector;
        _crashPredictor = crashPredictor;
        _configOptimizer = configOptimizer;
        _rootCauseAnalyzer = rootCauseAnalyzer;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _learningDataPath = Path.Combine(baseDir, "ai_learning");

        Directory.CreateDirectory(_learningDataPath);
    }

    /// <summary>
    /// 初始化 AI 服务
    /// 加载历史学习数据
    /// </summary>
    public Task InitializeAsync()
    {
        Log.Information("🤖 初始化 AI 保障服务（自学习模式）...");

        if (_initialized)
            return Task.CompletedTask;

        // 加载历史学习数据
        LoadLearnedPatterns();

        _initialized = true;
        int learnedCount;
        lock (_learnedPatternsLock) learnedCount = _learnedPatterns.Count;
        Log.Information("✅ AI 服务初始化完成，已学习模式: {Count}", learnedCount);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 分析一行日志
    /// 先用内置规则匹配，再用学习到的模式匹配
    /// 同时将日志送入根因分析器构建上下文
    /// </summary>
    public LogAnalysisResult AnalyzeLog(string logLine)
    {
        var truncated = logLine.Length > 150 ? logLine[..150] + "..." : logLine;
        Log.Debug("🔍 分析日志行: {Log}", truncated);

        // 0. 将日志送入根因分析器构建上下文窗口
        _rootCauseAnalyzer.FeedLog(logLine);

        // 1. 先用内置规则
        var result = _logDetector.Classify(logLine);

        // 2. 如果内置规则没匹配到，尝试学习到的模式
        if (!result.IsAnomaly)
        {
            result = MatchLearnedPatterns(logLine);
        }

        // 3. 无论是否匹配，都记录这次日志用于学习
        LearnFromLogLine(logLine, result);

        return result;
    }

    /// <summary>
    /// 对日志进行根因分析（二次分析）
    /// 不只是告诉你"有错"，还要告诉你"为什么错"
    /// </summary>
    public RootCauseAnalysis AnalyzeRootCause(string logLine)
    {
        // 先做一次基础分析
        var primaryResult = AnalyzeLog(logLine);

        // 再进行根因分析
        return _rootCauseAnalyzer.AnalyzeRootCause(primaryResult, logLine);
    }

    /// <summary>
    /// 预测崩溃风险
    /// </summary>
    public Task<CrashPrediction> PredictCrashAsync(SystemMetrics metrics)
    {
        Log.Debug("🔮 预测崩溃风险...");
        _crashPredictor.Update(metrics);
        return Task.FromResult(_crashPredictor.GetCurrentPrediction());
    }

    /// <summary>
    /// 生成配置优化建议
    /// </summary>
    public List<ConfigOptimizationSuggestion> SuggestOptimizations(ServerInstance server, SystemMetrics metrics)
    {
        Log.Debug("💡 生成配置优化建议...");
        return _configOptimizer.Suggest(server, metrics);
    }

    // ─── 自学习核心方法 ────────────────────────────────────────────

    /// <summary>
    /// 从服务器日志目录自动学习
    /// 当用户导入/检测到服务器后调用，扫描服务器的日志目录
    /// </summary>
    public async Task AutoLearnFromServerAsync(ServerInstance server)
    {
        if (string.IsNullOrEmpty(server.WorkingDirectory) || !Directory.Exists(server.WorkingDirectory))
        {
            Log.Debug("📂 服务器工作目录不存在，跳过自学习");
            return;
        }

        // 避免重复扫描同一目录
        var logDir = server.WorkingDirectory;
        if (_lastScannedLogDir == logDir && DateTime.UtcNow - _lastLogScanTime < TimeSpan.FromMinutes(5))
        {
            Log.Debug("📂 刚扫过这个目录，跳过");
            return;
        }

        Log.Information("📚 开始从服务器日志自学习: {Dir}", logDir);

        try
        {
            await Task.Run(() =>
            {
                // 扫描 logs/ 目录
                var logsDir = Path.Combine(logDir, "logs");
                if (Directory.Exists(logsDir))
                {
                    ScanLogDirectory(logsDir);
                }

                // 也扫描根目录下的 .log 文件
                foreach (var logFile in Directory.GetFiles(logDir, "*.log"))
                {
                    LearnFromLogFile(logFile);
                }

                // 保存学习结果
                SaveLearnedPatterns();
            });

            _lastScannedLogDir = logDir;
            _lastLogScanTime = DateTime.UtcNow;

            int count;
            lock (_learnedPatternsLock) count = _learnedPatterns.Count;
            Log.Information("📚 自学习完成，已积累 {Count} 条模式", count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 自学习过程出错: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 扫描日志目录中的所有日志文件
    /// </summary>
    private void ScanLogDirectory(string logsDir)
    {
        try
        {
            // Minecraft 的日志文件名模式
            var logFiles = Directory.GetFiles(logsDir, "*.log")
                .Concat(Directory.GetFiles(logsDir, "*.log.gz"))
                .Concat(Directory.GetFiles(logsDir, "*.txt"))
                .ToArray();

            Log.Information("📂 发现 {Count} 个日志文件", logFiles.Length);

            // 只处理最新的几个日志文件（避免处理太多历史日志）
            var recentLogs = logFiles
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .Take(5)
                .ToArray();

            foreach (var logFile in recentLogs)
            {
                LearnFromLogFile(logFile);
            }
        }
        catch (IOException ex)
        {
            // 目录被占用或临时不可访问，降级为 Debug
            Log.Debug(ex, "📖 日志目录暂时不可访问，跳过: {Dir}", logsDir);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug(ex, "📖 无权访问日志目录，跳过: {Dir}", logsDir);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 扫描日志目录失败: {Dir}", logsDir);
        }
    }

    /// <summary>
    /// 从单个日志文件中学习
    /// 逐行读取日志，标注异常行，提取新的异常模式
    /// </summary>
    private void LearnFromLogFile(string logFilePath)
    {
        var fileName = Path.GetFileName(logFilePath);
        try
        {
            Log.Debug("📖 读取日志文件: {File}", fileName);

            var fileInfo = new FileInfo(logFilePath);
            if (fileInfo.Length > 10 * 1024 * 1024)
            {
                Log.Warning("⚠️ 日志文件过大 ({Size} MB)，跳过: {File}", 
                    Math.Round(fileInfo.Length / 1024.0 / 1024.0, 2), fileName);
                return;
            }

            var processedMarker = Path.Combine(_learningDataPath, $"processed_{fileName}.mark");
            var fileLastWrite = fileInfo.LastWriteTime;

            if (File.Exists(processedMarker))
            {
                var lastProcessed = File.GetLastWriteTime(processedMarker);
                if (lastProcessed >= fileLastWrite)
                {
                    Log.Debug("📖 文件未修改，跳过: {File}", fileName);
                    return;
                }
            }

            const int maxErrorContexts = 500;
            const int maxTotalLines = 50000;

            var anomalyCount = 0;
            var totalLines = 0;
            var errorContexts = new List<string>(maxErrorContexts);

            foreach (var line in File.ReadLines(logFilePath))
            {
                totalLines++;
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var result = _logDetector.Classify(trimmed);
                if (result.IsAnomaly)
                {
                    anomalyCount++;
                    LearnFromLogLine(trimmed, result);

                    if ((result.Severity == LogSeverity.Error || result.Severity == LogSeverity.Critical) &&
                        errorContexts.Count < maxErrorContexts)
                    {
                        errorContexts.Add(trimmed);
                    }
                }
                else
                {
                    RecordNormalPattern(trimmed);
                }

                if (totalLines >= maxTotalLines) break;
            }

            if (errorContexts.Count >= 2)
            {
                DiscoverNewPatterns(errorContexts);
            }

            File.WriteAllText(processedMarker, DateTime.UtcNow.ToString("O"));

            Log.Debug("📖 日志学习完成: {File} — {Total} 行, {Anomaly} 条异常", 
                fileName, totalLines, anomalyCount);
        }
        catch (IOException ex)
        {
            // 服务器正在写的日志文件（如 latest.log）会被独占，读取失败是预期情况，降级为 Debug
            Log.Debug(ex, "📖 日志文件被占用或不可读，跳过: {File}", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug(ex, "📖 无权读取日志文件，跳过: {File}", fileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 学习日志文件失败: {File}", logFilePath);
        }
    }

    /// <summary>
    /// 从单行日志中学习
    /// 记录模式出现频率，用于动态调整权重
    /// </summary>
    private void LearnFromLogLine(string logLine, LogAnalysisResult result)
    {
        if (string.IsNullOrEmpty(logLine) || logLine.Length < 5) return;

        // 提取关键模式（去除数字、路径等变化部分）
        var pattern = ExtractPattern(logLine);
        if (string.IsNullOrEmpty(pattern)) return;

        // 更新命中计数
        _patternHitCounts.AddOrUpdate(pattern, 1, (_, count) => count + 1);
        _patternLastSeen[pattern] = DateTime.UtcNow;

        // 如果是异常模式且命中次数足够多，考虑添加为学习模式
        if (result.IsAnomaly && _patternHitCounts.TryGetValue(pattern, out var hits) && hits >= 3)
        {
            AddLearnedPattern(pattern, result);
        }
    }

    /// <summary>
    /// 记录正常日志模式（用于频率统计）
    /// </summary>
    private void RecordNormalPattern(string logLine)
    {
        var pattern = ExtractPattern(logLine);
        if (string.IsNullOrEmpty(pattern)) return;

        _patternHitCounts.AddOrUpdate(pattern, 1, (_, count) => count + 1);
        _patternLastSeen[pattern] = DateTime.UtcNow;
    }

    /// <summary>
    /// 从 ERROR 上下文行中发现新的异常模式
    /// 通过哈希分组 + 聚类相似的 ERROR 行来识别未被规则覆盖的异常
    /// 优化：使用前缀哈希分组降低 O(n²) 复杂度，限制最大处理数量
    /// </summary>
    private void DiscoverNewPatterns(List<string> errorContexts)
    {
        if (errorContexts.Count < 2) return;

        const int maxLinesToProcess = 200;
        var linesToProcess = errorContexts.Count > maxLinesToProcess
            ? errorContexts.OrderByDescending(l => l.Length).Take(maxLinesToProcess).ToList()
            : errorContexts;

        var patternGroups = new Dictionary<string, List<(string Line, string Pattern)>>();

        foreach (var line in linesToProcess)
        {
            var pattern = ExtractPattern(line);
            if (string.IsNullOrEmpty(pattern)) continue;

            var key = pattern.Length >= 8 ? pattern[..8] : pattern;
            if (!patternGroups.TryGetValue(key, out var group))
            {
                group = [];
                patternGroups[key] = group;
            }
            group.Add((line, pattern));
        }

        var clusters = new List<List<string>>();

        foreach (var (_, group) in patternGroups)
        {
            foreach (var (line, pattern) in group)
            {
                var matched = false;

                foreach (var cluster in clusters)
                {
                    if (cluster.Count > 0 && IsSimilarPattern(pattern, ExtractPattern(cluster[0])))
                    {
                        cluster.Add(line);
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    clusters.Add([line]);
                }
            }
        }

        foreach (var cluster in clusters.Where(c => c.Count >= 3))
        {
            var representativePattern = ExtractPattern(cluster[0]);
            if (string.IsNullOrEmpty(representativePattern)) continue;

            var existingResult = _logDetector.Classify(cluster[0]);
            if (existingResult.IsAnomaly) continue;

            AddLearnedPattern(representativePattern, new LogAnalysisResult(
                IsAnomaly: true,
                Severity: LogSeverity.Warning,
                Category: "LearnedPattern",
                Description: $"自学习发现的异常模式（出现 {cluster.Count} 次）",
                Suggestions: ["关注此模式的日志输出", "检查相关插件或配置"]
            ));
        }
    }

    /// <summary>
    /// 提取日志行的"模式"—— 将变化的部分替换为通配符
    /// 例如："2024-01-01 12:00:00 [ERROR] Failed to load plugin WorldGuard"
    ///   → "[ERROR] Failed to load plugin *"
    /// </summary>
    private static string ExtractPattern(string logLine)
    {
        if (string.IsNullOrEmpty(logLine)) return string.Empty;

        var pattern = logLine;

        // 移除时间戳
        pattern = Regex.Replace(pattern, @"\d{4}-\d{2}-\d{2}[\sT]\d{2}:\d{2}:\d{2}(\.\d+)?", "<TIME>");
        pattern = Regex.Replace(pattern, @"\d{2}:\d{2}:\d{2}", "<TIME>");

        // 替换数字
        pattern = Regex.Replace(pattern, @"\b\d+\.?\d*\b", "*");

        // 替换文件路径
        pattern = Regex.Replace(pattern, @"[A-Za-z]:\\[^\s]+", "<PATH>");
        pattern = Regex.Replace(pattern, @"/[^\s]+\.(jar|yml|json|log|txt)", "<PATH>");

        // 替换 UUID
        pattern = Regex.Replace(pattern, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "<UUID>");

        // 替换 IP 地址
        pattern = Regex.Replace(pattern, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "<IP>");

        // 替换包名.类名
        pattern = Regex.Replace(pattern, @"[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+\.[A-Z]", "<CLASS>");

        // 截断
        if (pattern.Length > 200)
            pattern = pattern[..200];

        return pattern;
    }

    /// <summary>
    /// 判断两个模式是否相似
    /// </summary>
    private static bool IsSimilarPattern(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (a == b) return true;

        var maxLen = Math.Max(a.Length, b.Length);
        var minLen = Math.Min(a.Length, b.Length);

        if (maxLen == 0) return true;

        if (maxLen - minLen > maxLen * 0.3)
            return false;

        var dist = LevenshteinDistance(a, b);
        if (dist == int.MaxValue) return false;

        var similarity = 1.0 - (double)dist / maxLen;
        return similarity >= 0.7;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var m = a.Length;
        var n = b.Length;

        if (m < n)
        {
            (a, b) = (b, a);
            (m, n) = (n, m);
        }

        var prevRow = new int[n + 1];
        for (var j = 0; j <= n; j++) prevRow[j] = j;

        for (var i = 1; i <= m; i++)
        {
            var currRow = new int[n + 1];
            currRow[0] = i;

            var minVal = i;
            for (var j = 1; j <= n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                currRow[j] = Math.Min(
                    Math.Min(currRow[j - 1] + 1, prevRow[j] + 1),
                    prevRow[j - 1] + cost);
                if (currRow[j] < minVal) minVal = currRow[j];
            }

            if (minVal > Math.Max(m, n) * 0.3)
                return int.MaxValue;

            prevRow = currRow;
        }

        return prevRow[n];
    }

    /// <summary>
    /// 用学习到的模式匹配日志行
    /// </summary>
    private LogAnalysisResult MatchLearnedPatterns(string logLine)
    {
        if (_patternMatchCache.TryGetValue(logLine, out var cachedResult))
            return cachedResult ?? new LogAnalysisResult(
                IsAnomaly: false,
                Severity: LogSeverity.Normal,
                Category: "无匹配",
                Description: "未匹配到任何已知模式",
                Suggestions: []
            );

        List<LearnedPattern> patternsSnapshot;
        lock (_learnedPatternsLock) patternsSnapshot = [.. _learnedPatterns];

        if (patternsSnapshot.Count == 0)
        {
            _patternMatchCache.TryAdd(logLine, null);
            return new LogAnalysisResult(
                IsAnomaly: false,
                Severity: LogSeverity.Normal,
                Category: "无匹配",
                Description: "未匹配到任何已知模式",
                Suggestions: []
            );
        }

        var sortedPatterns = patternsSnapshot.OrderBy(p => p.Severity).ToList();

        foreach (var learned in sortedPatterns)
        {
            try
            {
                if (learned.Regex.IsMatch(logLine))
                {
                    Log.Debug("🧠 匹配到学习模式: {Pattern} (命中 {Hits} 次)",
                        learned.Category, learned.HitCount);

                    learned.HitCount++;

                    var result = new LogAnalysisResult(
                        IsAnomaly: true,
                        Severity: learned.Severity,
                        Category: learned.Category,
                        Description: learned.Description,
                        Suggestions: learned.Suggestions
                    );

                    if (_patternMatchCache.Count < MaxCacheSize)
                        _patternMatchCache.TryAdd(logLine, result);

                    return result;
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                Log.Debug("⏱️ 正则匹配超时，跳过: {Pattern} — {Message}", learned.Pattern, ex.Message);
            }
        }

        if (_patternMatchCache.Count < MaxCacheSize)
            _patternMatchCache.TryAdd(logLine, null);

        return new LogAnalysisResult(
            IsAnomaly: false,
            Severity: LogSeverity.Normal,
            Category: "无匹配",
            Description: "未匹配到任何已知模式",
            Suggestions: []
        );
    }

    /// <summary>
    /// 添加学习到的模式
    /// </summary>
    private void AddLearnedPattern(string pattern, LogAnalysisResult result)
    {
        lock (_learnedPatternsLock)
        {
            if (_learnedPatterns.Any(p => p.Pattern == pattern)) return;

            try
            {
                var learned = new LearnedPattern
                {
                    Pattern = pattern,
                    Severity = result.Severity,
                    Category = $"Learned_{result.Category}_{_learnedPatterns.Count}",
                    Description = result.Description,
                    Suggestions = result.Suggestions,
                    Regex = new Regex(pattern.Replace("*", ".*"), RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
                    HitCount = _patternHitCounts.GetValueOrDefault(pattern, 1),
                    LearnedAt = DateTime.UtcNow
                };

                _learnedPatterns.Add(learned);
                Log.Information("🧠 学习到新模式: {Category} — {Desc} (命中 {Hits} 次)",
                    learned.Category, learned.Description, learned.HitCount);
            }
            catch (ArgumentException ex)
            {
                Log.Debug("⚠️ 模式正则无效，跳过: {Pattern}: {Msg}", pattern, ex.Message);
            }
        }

        _patternMatchCache.Clear();
    }

    // ─── 模型持久化 ──────────────────────────────────────────────────

    /// <summary>
    /// 加载之前的学习结果
    /// </summary>
    private void LoadLearnedPatterns()
    {
        var dataFile = Path.Combine(_learningDataPath, "learned_patterns.json");
        if (!File.Exists(dataFile))
        {
            Log.Information("📂 未找到历史学习数据，从零开始学习");
            return;
        }

        try
        {
            var json = File.ReadAllText(dataFile);
            var patterns = JsonSerializer.Deserialize<List<LearnedPatternData>>(json);
            if (patterns == null) return;

            lock (_learnedPatternsLock)
            {
                foreach (var p in patterns)
                {
                    try
                    {
                        var regexPattern = p.Pattern.Replace("*", ".*");
                        _learnedPatterns.Add(new LearnedPattern
                        {
                            Pattern = p.Pattern,
                            Severity = p.Severity,
                            Category = p.Category,
                            Description = p.Description,
                            Suggestions = p.Suggestions,
                            Regex = new Regex(regexPattern, RegexOptions.Compiled, TimeSpan.FromSeconds(1)),
                            HitCount = p.HitCount,
                            LearnedAt = p.LearnedAt
                        });
                    }
                    catch (ArgumentException ex)
                    {
                        Log.Debug("⚠️ 无效正则模式，跳过加载: {Pattern} — {Message}", p.Pattern, ex.Message);
                    }
                }

                Log.Information("📚 加载了 {Count} 条历史学习模式", _learnedPatterns.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 加载学习数据失败: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// 保存学习结果
    /// </summary>
    private void SaveLearnedPatterns()
    {
        var dataFile = Path.Combine(_learningDataPath, "learned_patterns.json");

        try
        {
            List<LearnedPatternData> data;
            lock (_learnedPatternsLock)
            {
                data = _learnedPatterns.Select(p => new LearnedPatternData
                {
                    Pattern = p.Pattern,
                    Severity = p.Severity,
                    Category = p.Category,
                    Description = p.Description,
                    Suggestions = p.Suggestions,
                    HitCount = p.HitCount,
                    LearnedAt = p.LearnedAt
                }).ToList();
            }

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dataFile, json);

            Log.Debug("💾 学习数据已保存: {Count} 条模式", data.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 保存学习数据失败: {Message}", ex.Message);
        }
    }

    // ─── 内部类型 ──────────────────────────────────────────────────────

    private sealed class LearnedPattern
    {
        public string Pattern { get; init; } = string.Empty;
        public LogSeverity Severity { get; init; }
        public string Category { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public List<string> Suggestions { get; init; } = [];
        public Regex Regex { get; init; } = new(".*", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        public int HitCount { get; set; }
        public DateTime LearnedAt { get; init; }
    }

    private class LearnedPatternData
    {
        public string Pattern { get; set; } = string.Empty;
        public LogSeverity Severity { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = [];
        public int HitCount { get; set; }
        public DateTime LearnedAt { get; set; }
    }
}
