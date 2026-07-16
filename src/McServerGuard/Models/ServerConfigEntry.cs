using CommunityToolkit.Mvvm.ComponentModel;
using SvcDescriptor = McServerGuard.Services.ConfigManagement.ServerConfigDescriptor;

namespace McServerGuard.Models;

/// <summary>🔧 配置文件条目 —— 一行配置，一个故事</summary>
public partial class ServerConfigEntry : ObservableObject
{
    [ObservableProperty] private string _key = string.Empty;
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private string _sourceFile = string.Empty;
    [ObservableProperty] private bool _isModified;
    [ObservableProperty] private string _originalValue = string.Empty;
    [ObservableProperty] private bool _isValid = true;
    [ObservableProperty] private string? _errorMessage;
    public SvcDescriptor? Descriptor { get; set; }

    /// <summary>显示名称 —— 有描述符用中文名，没有就用 Key</summary>
    public string DisplayName => Descriptor?.DisplayName ?? Key;

    /// <summary>
    /// 友好显示名称 —— 有描述符用中文名，没有则将 kebab-case key 转为 Title Case
    /// 如 "network-compression-threshold" → "Network Compression Threshold"
    /// </summary>
    public string FriendlyDisplayName
    {
        get
        {
            if (Descriptor is not null)
                return Descriptor.DisplayName;

            // 无描述符时：kebab-case → Title Case
            if (string.IsNullOrEmpty(Key))
                return "(空)";

            var words = Key.Split('-', '.', '_');
            return string.Join(' ', words.Select(w =>
                string.IsNullOrEmpty(w) ? w :
                char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
        }
    }

    /// <summary>描述 —— 有描述符用中文描述，没有就空</summary>
    public string Description => Descriptor?.Description ?? string.Empty;

    /// <summary>是否需要重启 —— 安全访问</summary>
    public bool RequiresRestart => Descriptor?.RequiresRestart == true;

    /// <summary>是否为布尔类型配置 —— 用于 UI 控制 ToggleButton 显隐</summary>
    public bool IsBoolType => Descriptor?.ValueType.Equals("bool", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>是否为枚举类型配置 —— 用于 UI 控制 ComboBox 显隐</summary>
    public bool IsEnumType => Descriptor?.AllowedValues?.Length > 0;

    /// <summary>是否为数值类型配置 —— 用于 UI 控制数值输入框显隐</summary>
    public bool IsNumericType => Descriptor != null && 
        (Descriptor.ValueType.Equals("int", StringComparison.OrdinalIgnoreCase) ||
         Descriptor.ValueType.Equals("float", StringComparison.OrdinalIgnoreCase) ||
         Descriptor.ValueType.Equals("double", StringComparison.OrdinalIgnoreCase) ||
         Descriptor.ValueType.Equals("long", StringComparison.OrdinalIgnoreCase));

    /// <summary>是否为普通字符串类型 —— 不是布尔、不是枚举、不是数值，用普通 TextBox 编辑</summary>
    public bool IsStringType => !IsBoolType && !IsEnumType && !IsNumericType;
}
