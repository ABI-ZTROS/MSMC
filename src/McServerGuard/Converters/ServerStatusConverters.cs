// 🎨 服务器状态转换器 —— 把 ServerStatus 枚举翻译成颜色/文字/图标
// 让 UI 一眼看出服务器是"跑得欢"还是"趴窝了"
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using McServerGuard.Models;

namespace McServerGuard.Converters;

/// <summary>
/// 🟢🟡⚫🔴 ServerStatus → Brush（颜色）
/// 就像红绿灯，告诉你服务器处于哪个状态 🚦
/// </summary>
public class ServerStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ServerStatus status)
            return Brushes.Gray;

        return status switch
        {
            ServerStatus.Running => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),  // 🟢 绿
            ServerStatus.Starting => new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)), // 🟡 黄
            ServerStatus.Stopping => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)), // 🟠 橙
            ServerStatus.Stopped => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75)), // ⚫ 灰
            ServerStatus.Error => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),   // 🔴 红
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 📝 ServerStatus → 显示文字
/// "🟢 运行中" / "🟡 启动中" / "⚫ 已停止" / "🔴 异常" / "❓ 未知"
/// </summary>
public class ServerStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ServerStatus status)
            return "❓ 未知";

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
/// 🎯 ServerStatus → FontAwesome6 图标 Kind
/// 用在状态点旁边，给个视觉锚点
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
/// 🔵 ServerStatus → MaterialDesign PackIcon Kind（备用）
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
