using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using McServerGuard.Models;
using McServerGuard.Services.AIService;
using Serilog;

namespace McServerGuard.ViewModels;

/// <summary>
/// 🤖 AI 守护 ViewModel —— "AI 说你的服务器快炸了，信不信由你"
/// 
/// 提供 AI 驱动的日志分析、健康报告生成、崩溃预测、配置优化建议等功能。
/// 虽然叫 AI，但很多功能是基于规则的 —— 没必要搞深度学习来判断 OOM 吧？🤷
/// 
/// 核心能力：
/// 1. 逐行分析日志，标记异常行并给出修复建议
/// 2. 综合分析生成服务器健康报告
/// 3. 预测崩溃风险（基于系统指标）
/// 4. 根据服务器配置和系统状态给出优化建议
/// </summary>
public partial class AIGuardViewModel : ObservableObject
{
    private readonly IAiGuardService _aiGuardService;

    public AIGuardViewModel(IAiGuardService aiGuardService)
    {
        Log.Information("🤖 AIGuardViewModel 初始化");
        _aiGuardService = aiGuardService;
    }

    // ─── 核心属性 ────────────────────────────────────────────────────

    /// <summary>
    /// 当前操作的服务器实例
    /// 没有服务器就无法生成健康报告和配置优化建议（日志分析倒是可以）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateHealthReportCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestConfigOptimizationCommand))]
    private ServerInstance? _server;

    /// <summary>
    /// 日志输入文本 —— 用户粘贴的日志内容
    /// 支持多行文本，每行会被逐行分析
    /// 
    /// 小提示：你可以在服务器控制台里复制日志，然后 Ctrl+V 粘贴到这里
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeLogCommand))]
    private string _logInput = string.Empty;

    /// <summary>
    /// 分析结果输出 —— AI 分析完日志后的结论
    /// 用 Markdown 格式输出，包含异常标记、严重程度、修复建议等
    /// </summary>
    [ObservableProperty]
    private string _analysisOutput = string.Empty;

    /// <summary>
    /// 是否正在处理中 —— 日志分析可能需要一点时间（尤其是日志很长的时候）
    /// 用来给 UI 一个加载动画
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeLogCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateHealthReportCommand))]
    [NotifyCanExecuteChangedFor(nameof(RequestConfigOptimizationCommand))]
    private bool _isProcessing;

    /// <summary>
    /// 分析历史记录 —— 每次分析的结果都存一份，方便回溯
    /// 最多保留 50 条记录，超出就 FIFO（先进先出）
    /// </summary>
    [ObservableProperty]
    private List<AnalysisRecord> _analysisHistory = [];

    /// <summary>
    /// 配置优化建议列表 —— AI 看了你的配置后给出的改进方案
    /// 比如说"你的 view-distance 太大了建议调到 8"
    /// </summary>
    [ObservableProperty]
    private List<ConfigOptimizationSuggestion> _configSuggestions = [];

    /// <summary>
    /// 有没有配置建议 —— 绑定 UI 的可见性用
    /// 没建议就别显示那个空荡荡的列表了，怪尴尬的 😅
    /// </summary>
    public bool HasSuggestions => ConfigSuggestions.Count > 0;

    /// <summary>
    /// 最新的崩溃预测结果 —— "你的服务器在接下来 30 分钟内崩溃的概率是 XX%"
    /// 这个数字仅供参考，别太当真...但也不能不当真 🫣
    /// </summary>
    [ObservableProperty]
    private CrashPrediction? _latestCrashPrediction;

    // ─── 命令 ──────────────────────────────────────────────────────────

    // ─── 属性变更响应 ────────────────────────────────────────────────

    /// <summary>
    /// 配置建议列表变了 —— 刷新 HasSuggestions
    /// 有建议了就显示列表，没建议了就藏起来 🎩
    /// </summary>
    partial void OnConfigSuggestionsChanged(List<ConfigOptimizationSuggestion> value)
    {
        OnPropertyChanged(nameof(HasSuggestions));
    }

    /// <summary>
    /// 分析日志 —— 逐行调用 IAiGuardService.AnalyzeLog
    /// 
    /// 把用户输入的日志文本按行拆分，每行都喂给 AI 分析。
    /// 最后汇总所有异常行，生成一份 Markdown 格式的分析报告。
    /// 
    /// 如果日志有 10000 行...建议你先喝杯茶 ☕
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAnalyzeLog))]
    private async Task AnalyzeLogAsync()
    {
        if (string.IsNullOrWhiteSpace(LogInput))
        {
            AnalysisOutput = "⚠️ 请先粘贴日志内容再进行分析哦";
            return;
        }

        IsProcessing = true;
        try
        {
            await PerformLogAnalysisAsync();
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// 日志分析核心逻辑（不含 IsProcessing 状态管理）
    /// 供 AnalyzeLogAsync 和 GenerateHealthReportAsync 共用
    /// </summary>
    private async Task PerformLogAnalysisAsync()
    {
        AnalysisOutput = "🔍 正在分析日志...";

        // 确保 AI 服务已初始化
        await _aiGuardService.InitializeAsync();

        // 📝 逐行分析
        var lines = LogInput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Log.Information("📝 开始分析日志，共 {Lines} 行", lines.Length);

        var anomalies = new List<(int LineNumber, string Content, LogAnalysisResult Result)>();
        var allResults = new List<LogAnalysisResult>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var result = _aiGuardService.AnalyzeLog(line);
            allResults.Add(result);

            if (result.IsAnomaly)
            {
                anomalies.Add((i + 1, line, result));
            }
        }

        // 📊 生成分析报告
        var report = BuildAnalysisReport(lines.Length, anomalies);
        AnalysisOutput = report;

        Log.Information("✅ 日志分析完成，发现 {Count} 条异常", anomalies.Count);

        // 📜 记录到历史
        var record = new AnalysisRecord
        {
            Timestamp = DateTime.Now,
            TotalLines = lines.Length,
            AnomalyCount = anomalies.Count,
            Summary = anomalies.Count == 0
                ? "✅ 日志正常，未发现异常"
                : $"⚠️ 发现 {anomalies.Count} 条异常日志",
            Details = report
        };

        var history = new List<AnalysisRecord>(AnalysisHistory) { record };
        // 最多保留 50 条
        while (history.Count > 50)
            history.RemoveAt(0);
        AnalysisHistory = history;
    }

    /// <summary>
    /// 能不能分析 —— 有日志内容 + 没在处理中
    /// </summary>
    private bool CanAnalyzeLog() => !string.IsNullOrWhiteSpace(LogInput) && !IsProcessing;

    /// <summary>
    /// 生成健康报告 —— 综合调用日志分析 + 崩溃预测 + 配置优化
    /// 
    /// 这是一个"全家桶"命令：一次性给你服务器做个全面体检 🩺
    /// 需要有 Server 和 SystemMetrics 才能给出完整报告
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGenerateHealthReport))]
    private async Task GenerateHealthReportAsync()
    {
        Log.Information("📋 生成健康报告...");
        IsProcessing = true;
        AnalysisOutput = "🏥 正在生成健康报告，请稍候...";

        try
        {
            // 确保 AI 服务已初始化
            await _aiGuardService.InitializeAsync();

            var report = new StringBuilder();

            // 📋 如果有日志内容，先分析日志
            if (!string.IsNullOrWhiteSpace(LogInput))
            {
                report.AppendLine("## 📋 日志分析报告");
                report.AppendLine();
                await PerformLogAnalysisAsync();
                report.AppendLine(AnalysisOutput);
                report.AppendLine();
            }

            // 💥 崩溃预测 —— 需要当前系统指标
            if (Server is not null)
            {
                report.AppendLine("## 💥 崩溃风险预测");
                report.AppendLine();

                // 🤔 这里需要一个 SystemMetrics...我们从哪来？
                // 理想情况下从 SystemMonitorViewModel 获取，但 ViewModel 之间不直接通信
                // 所以我们创建一个空快照或者让调用者传入
                // 暂时先基于 Server 信息给出定性分析
                report.AppendLine("*提示：完整崩溃预测需要系统监控数据，请先在监控页采集数据*");
                report.AppendLine();

                // 🎯 配置优化建议 —— 只要有 Server 就能出
                report.AppendLine("## ⚙️ 配置优化建议");
                report.AppendLine();

                var metrics = new SystemMetrics(); // 空快照，SuggestOptimizations 会尽量工作
                var suggestions = _aiGuardService.SuggestOptimizations(Server, metrics);
                ConfigSuggestions = suggestions;

                if (suggestions.Count == 0)
                {
                    report.AppendLine("✅ 当前配置看起来还不错，暂无明显优化建议");
                }
                else
                {
                    foreach (var suggestion in suggestions)
                    {
                        report.AppendLine($"### {suggestion.Category}");
                        report.AppendLine($"- **当前值**：{suggestion.CurrentValue}");
                        report.AppendLine($"- **建议值**：{suggestion.SuggestedValue}");
                        report.AppendLine($"- **原因**：{suggestion.Reason}");
                        report.AppendLine($"- **预期影响**：{suggestion.Impact}");
                        report.AppendLine();
                    }
                }
            }
            else
            {
                report.AppendLine("⚠️ 未选择服务器实例，无法生成完整健康报告");
                report.AppendLine("请先在检测页选择一个服务器");
            }

            AnalysisOutput = report.ToString();
            Log.Information("✅ 健康报告生成完成");
        }
        catch (Exception ex)
        {
            AnalysisOutput = $"❌ 生成健康报告失败：{ex.Message}";
            Log.Error(ex, "💥 fuck: 健康报告生成失败: {Message}", ex.Message);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// 能不能生成健康报告 —— 有 Server + 没在处理中
    /// </summary>
    private bool CanGenerateHealthReport() => Server is not null && !IsProcessing;

    /// <summary>
    /// 请求配置优化 —— 只调用 SuggestOptimizations
    /// 
    /// 比 GenerateHealthReport 轻量，只关注配置优化部分。
    /// 适合"我就想看看配置建议，不需要完整报告"的场景
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRequestConfigOptimization))]
    private async Task RequestConfigOptimizationAsync()
    {
        if (Server is null)
        {
            Log.Debug("🔄 RequestConfigOptimization 跳过: Server 为空");
            return;
        }

        Log.Information("💡 请求配置优化建议...");
        IsProcessing = true;

        try
        {
            await _aiGuardService.InitializeAsync();

            var metrics = new SystemMetrics();
            var suggestions = _aiGuardService.SuggestOptimizations(Server, metrics);
            ConfigSuggestions = suggestions;

            // 把建议格式化输出到 AnalysisOutput
            if (suggestions.Count == 0)
            {
                AnalysisOutput = "✅ 当前配置看起来还不错，暂无明显优化建议 🎉";
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine($"## ⚙️ 配置优化建议（共 {suggestions.Count} 条）");
                sb.AppendLine();

                foreach (var suggestion in suggestions)
                {
                    sb.AppendLine($"### {suggestion.Category}");
                    sb.AppendLine($"- **当前值**：{suggestion.CurrentValue}");
                    sb.AppendLine($"- **建议值**：{suggestion.SuggestedValue}");
                    sb.AppendLine($"- **原因**：{suggestion.Reason}");
                    sb.AppendLine($"- **预期影响**：{suggestion.Impact}");
                    sb.AppendLine();
                }

                AnalysisOutput = sb.ToString();
            }

            Log.Information("✅ 配置优化建议获取完成，共 {Count} 条建议", suggestions.Count);
        }
        catch (Exception ex)
        {
            AnalysisOutput = $"❌ 获取配置优化建议失败：{ex.Message}";
            Log.Error(ex, "💥 fuck: 配置优化建议获取失败: {Message}", ex.Message);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// 能不能请求配置优化 —— 有 Server + 没在处理中
    /// </summary>
    private bool CanRequestConfigOptimization() => Server is not null && !IsProcessing;

    // ─── 私有方法 ────────────────────────────────────────────────────

    /// <summary>
    /// 构建分析报告 —— 把异常日志汇总成人类可读的 Markdown
    /// </summary>
    private string BuildAnalysisReport(int totalLines, List<(int LineNumber, string Content, LogAnalysisResult Result)> anomalies)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"## 📊 日志分析结果");
        sb.AppendLine();
        sb.AppendLine($"- **总行数**：{totalLines}");
        sb.AppendLine($"- **异常行数**：{anomalies.Count}");
        sb.AppendLine($"- **异常率**：{(double)anomalies.Count / totalLines * 100:F1}%");
        sb.AppendLine();

        if (anomalies.Count == 0)
        {
            sb.AppendLine("✅ **一切正常！日志中未发现异常。**");
            sb.AppendLine();
            sb.AppendLine("你的服务器日志看起来很健康，继续保持 🌟");
        }
        else
        {
            // 按严重程度排序 —— 先报最危险的
            var sorted = anomalies
                .OrderByDescending(a => (int)a.Result.Severity)
                .ToList();

            sb.AppendLine("### ⚠️ 异常详情");
            sb.AppendLine();

            foreach (var anomaly in sorted)
            {
                var emoji = anomaly.Result.Severity switch
                {
                    LogSeverity.Critical => "💥",
                    LogSeverity.Error => "🧨",
                    LogSeverity.Warning => "⚠️",
                    _ => "❓"
                };

                sb.AppendLine($"#### {emoji} 第 {anomaly.LineNumber} 行 — [{anomaly.Result.Category}]");
                sb.AppendLine($"- **严重程度**：{anomaly.Result.Severity}");
                sb.AppendLine($"- **描述**：{anomaly.Result.Description}");
                sb.AppendLine();

                if (anomaly.Result.Suggestions.Count > 0)
                {
                    sb.AppendLine("**修复建议**：");
                    foreach (var suggestion in anomaly.Result.Suggestions)
                    {
                        sb.AppendLine($"  - {suggestion}");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine($"```");
                sb.AppendLine(anomaly.Content.Length > 200
                    ? anomaly.Content[..200] + "..."
                    : anomaly.Content);
                sb.AppendLine($"```");
                sb.AppendLine();
            }

            // 总结
            var criticalCount = sorted.Count(a => a.Result.Severity == LogSeverity.Critical);
            var errorCount = sorted.Count(a => a.Result.Severity == LogSeverity.Error);
            var warningCount = sorted.Count(a => a.Result.Severity == LogSeverity.Warning);

            sb.AppendLine("### 📋 总结");
            sb.AppendLine($"- 💥 Critical：{criticalCount} 条");
            sb.AppendLine($"- 🧨 Error：{errorCount} 条");
            sb.AppendLine($"- ⚠️ Warning：{warningCount} 条");

            if (criticalCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("🚨 **检测到严重异常！建议立即处理！**");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// 📜 分析历史记录 —— 每次分析的摘要
/// 用于在 UI 的历史列表里快速浏览之前的分析结果
/// </summary>
public partial class AnalysisRecord : ObservableObject
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public int TotalLines { get; init; }
    public int AnomalyCount { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;

    /// <summary>显示文本 —— 在列表里一行的摘要</summary>
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Summary} ({TotalLines} 行)";
}
