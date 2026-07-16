using CommunityToolkit.Mvvm.ComponentModel;

namespace McServerGuard.Models;

/// <summary>📄 启动脚本的"体检报告" —— 我们用内容架构而非文件名来判断它是不是启动脚本</summary>
public partial class StartupScriptInfo : ObservableObject
{
    [ObservableProperty] private string _scriptPath = string.Empty;
    [ObservableProperty] private string _scriptName = string.Empty;
    [ObservableProperty] private bool _isServerStartupScript;
    [ObservableProperty] private string? _javaPath;
    [ObservableProperty] private long _maxHeapMemoryBytes;
    [ObservableProperty] private string? _serverJarName;
    [ObservableProperty] private bool _hasAutoRestart;
    [ObservableProperty] private bool _usesAikarFlags;
    [ObservableProperty] private string _rawContent = string.Empty;
    public List<string> MatchedRules { get; set; } = [];
}
