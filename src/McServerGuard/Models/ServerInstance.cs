using CommunityToolkit.Mvvm.ComponentModel;
using McServerGuard.Constants;

namespace McServerGuard.Models;

/// <summary>🎯 一个被检测到的 Minecraft 服务器实例 —— 我们的"猎物"档案</summary>
public partial class ServerInstance : ObservableObject
{
    public int ProcessId { get; init; }

    [ObservableProperty] private ServerType _serverType = ServerType.Unknown;
    [ObservableProperty] private string _workingDirectory = string.Empty;
    [ObservableProperty] private string _javaPath = string.Empty;
    [ObservableProperty] private string _serverJarPath = string.Empty;
    [ObservableProperty] private string _serverJarName = string.Empty;
    [ObservableProperty] private string? _startupScriptPath;
    [ObservableProperty] private string _fullCommandLine = string.Empty;
    public List<string> JvmArguments { get; init; } = [];

    [ObservableProperty] private long _initialHeapMemoryBytes;
    [ObservableProperty] private long _maxHeapMemoryBytes;
    [ObservableProperty] private List<string> _configFiles = [];
    [ObservableProperty] private bool _usesAikarFlags;
    [ObservableProperty] private string _gcType = string.Empty;
    [ObservableProperty] private int _serverPort = ServerConstants.DefaultServerPort;

    public DateTime DetectedAt { get; init; } = DateTime.Now;

    // 📛 显示名称 —— 在 UI 上让玩家一眼认出自己的服务器
    public string DisplayName => $"{ServerType} @ {System.IO.Path.GetFileName(WorkingDirectory)} (PID: {ProcessId})";

    // 📊 格式化内存 —— 把字节变成人类能看懂的东西
    public string FormattedMaxMemory => FormatBytes(MaxHeapMemoryBytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1L << 30)
            return $"{(bytes >> 30)} GB";
        if (bytes >= 1L << 20)
            return $"{(bytes >> 20)} MB";
        return $"{bytes} KB";
    }

    // 当 ServerType 或 WorkingDirectory 变化时，通知 DisplayName 刷新
    partial void OnServerTypeChanged(ServerType value)
        => OnPropertyChanged(nameof(DisplayName));

    partial void OnWorkingDirectoryChanged(string value)
        => OnPropertyChanged(nameof(DisplayName));

    partial void OnMaxHeapMemoryBytesChanged(long value)
        => OnPropertyChanged(nameof(FormattedMaxMemory));
}
