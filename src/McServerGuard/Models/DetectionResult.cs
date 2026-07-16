using CommunityToolkit.Mvvm.ComponentModel;

namespace McServerGuard.Models;

/// <summary>🔍 检测结果汇总 —— 一次扫描后所有情报的"大汇总"</summary>
public partial class DetectionResult : ObservableObject
{
    [ObservableProperty] private bool _isDetected;
    [ObservableProperty] private List<ServerInstance> _servers = [];
    [ObservableProperty] private List<StartupScriptInfo> _startupScripts = [];
    [ObservableProperty] private long _elapsedMs;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private List<string> _logMessages = [];
}
