// -----------------------------------------------------------------------------
// 文件名: ServerStatusConverters.cs
// 命名空间: McServerGuard.Converters
// 功能描述: 服务器状态枚举值转换器集合，实现 IValueConverter 接口。
//           将 ServerStatus 枚举分别转换为颜色画笔、显示文本与图标名称，
//           供 XAML 绑定直接使用。
// 依赖组件: PresentationFramework, System.Windows.Data, System.Windows.Media
// 设计模式: 值转换器 (IValueConverter)
// -----------------------------------------------------------------------------
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using McServerGuard.Models;

namespace McServerGuard.Converters;

/// <summary>
/// 服务器状态转画笔值转换器。
/// 将 ServerStatus 枚举转换为对应语义色的 SolidColorBrush。
/// 运行中 → 绿色，启动中 → 黄色，停止中 → 橙色，已停止 → 灰色，异常 → 红色。
/// 单向绑定使用。
/// </summary>
public class ServerStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ServerStatus status)
            return Brushes.Gray;

        return status switch
        {
            ServerStatus.Running => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),  // 绿
            ServerStatus.Starting => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)), // 黄
            ServerStatus.Stopping => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)), // 橙
            ServerStatus.Stopped => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75)), // 灰
            ServerStatus.Error => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),   // 红
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 服务器状态转文本值转换器。
/// 将 ServerStatus 枚举转换为中文显示文本。
/// 运行中 / 启动中 / 停止中 / 已停止 / 异常 / 未知。
/// 单向绑定使用。
/// </summary>
public class ServerStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ServerStatus status)
            return "未知";

        return status switch
        {
            ServerStatus.Running => "运行中",
            ServerStatus.Starting => "启动中",
            ServerStatus.Stopping => "停止中",
            ServerStatus.Stopped => "已停止",
            ServerStatus.Error => "异常",
            _ => "未知"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 服务器状态转 FontAwesome6 图标值转换器。
/// 将 ServerStatus 枚举转换为 FontAwesome6 图标的 Kind 名称字符串。
/// 用于状态指示点旁的视觉锚点图标。
/// 单向绑定使用。
/// </summary>
public class ServerStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ServerStatus status)
            return "CircleQuestionSolid";

        return status switch
        {
            ServerStatus.Running => "CirclePlaySolid",
            ServerStatus.Starting => "SpinnerSolid",
            ServerStatus.Stopping => "CircleStopSolid",
            ServerStatus.Stopped => "CirclePauseSolid",
            ServerStatus.Error => "CircleExclamationSolid",
            _ => "CircleQuestionSolid"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 服务器状态转 MaterialDesign 图标值转换器（备用）。
/// 将 ServerStatus 枚举转换为 MaterialDesign PackIcon 的 Kind 名称字符串。
/// 单向绑定使用。
/// </summary>
public class ServerStatusToMaterialIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ServerStatus status)
            return "HelpCircle";

        return status switch
        {
            ServerStatus.Running => "PlayCircle",
            ServerStatus.Starting => "Loading",
            ServerStatus.Stopping => "StopCircleOutline",
            ServerStatus.Stopped => "PauseCircle",
            ServerStatus.Error => "AlertCircle",
            _ => "HelpCircle"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
