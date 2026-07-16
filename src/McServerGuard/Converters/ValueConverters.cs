// 🔄 值转换器三人组 —— XAML 绑定时的"翻译官"
// WPF 绑定说："这个 bool 我不能直接用"，转换器说："交给我" ✨
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace McServerGuard.Converters;

/// <summary>
/// InvertBoolConverter —— bool 取反器
/// true → false, false → true
/// 就像 Minecraft 里的红石火把，有信号就没信号，没信号就有信号 🔴
/// </summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>
/// NullToVisibilityConverter —— "你有东西就显示，没东西就消失"
/// null → Collapsed（藏起来）
/// 非 null → Visible（亮出来）
/// 就像苦力怕看你的时候突然消失一样 🟩
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        // 这货是单向绑定用的，ConvertBack 理论上不会被调用，但以防万一返回个 Binding.DoNothing
        => Binding.DoNothing;
}

/// <summary>
/// BoolToVisibilityConverter —— "true 就让你看，false 就让你猜"
/// true → Visible
/// false → Collapsed
/// 最经典的 WPF 转换器，出场率比僵尸还高 🧟
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v == Visibility.Visible;
        return Binding.DoNothing;
    }
}

/// <summary>
/// IndexToBoolConverter —— 通用索引→bool 转换器
/// ConverterParameter 传入字符串形式的索引（如 "0", "1", "2", "3"）
/// 当 value 等于该索引时返回 true，否则返回 false
/// ConvertBack: true 时返回 parameter 的值
/// 就像抽奖号码对对碰，对上了就中奖 🎰
/// </summary>
public class IndexToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        var indexStr = value.ToString();
        var targetStr = parameter.ToString();
        return string.Equals(indexStr, targetStr, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // true 时返回 parameter 的值，false 时返回 Binding.DoNothing
        if (value is true && parameter != null)
            return parameter.ToString() ?? string.Empty;

        return Binding.DoNothing;
    }
}

/// <summary>
/// BoolStringConverter —— "true"/"false" 字符串 ↔ bool? 互转
/// Convert: string → bool?（"true" → true, 其他 → false, 非string → Binding.DoNothing）
/// ConvertBack: bool? → string（true → "true", false → "false", null → Binding.DoNothing）
/// 容错设计：值不合法时返回 Binding.DoNothing，避免产生绑定错误
/// </summary>
public class BoolStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
            return (bool?)string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
        if (value is bool b)
            return (bool?)b;
        return Binding.DoNothing;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? "true" : "false";
        return Binding.DoNothing;
    }
}

/// <summary>
/// ValueTypeToVisibilityConverter —— 根据 ValueType 字符串控制 Visibility
/// ConverterParameter 传入期望的类型（如 "bool", "int", "enum", "string"）
/// 当 value（字符串类型）等于 parameter 时返回 Visible，否则 Collapsed
/// 就像门禁卡，名字对上了才让进 🚪
/// </summary>
public class ValueTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var valueStr = value.ToString();
        var targetTypeStr = parameter.ToString();
        return string.Equals(valueStr, targetTypeStr, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        // 同样是单向绑定，ConvertBack 不会被调用，返回 Binding.DoNothing 安全又优雅
        => Binding.DoNothing;
}

/// <summary>
/// BoolToPackIconConverter —— bool 转 MaterialDesign PackIcon Kind 名称
/// true → "CheckCircle"（绿色，一切正常 ✅）
/// false → "AlertCircle"（黄色/警告，要注意啦 ⚠️）
/// 返回 string 类型，PackIcon 的 Kind 属性接受 string
/// 状态好不好，图标告诉你 👀
/// </summary>
public class BoolToPackIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Check" : "Alert";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        // 单向绑定不需要 ConvertBack，但还是给个合理的实现吧：CheckCircle=true，其余=false
        => value is string s && s == "Check";
}

/// <summary>
/// BoolToTextConverter —— bool 转 "是"/"否"
/// true → "是"
/// false → "否"
/// 简简单单，清清爽爽，不整那些花里胡哨的 🌸
/// </summary>
public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "是" : "否";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
            return string.Equals(s, "是", StringComparison.OrdinalIgnoreCase);

        return false;
    }
}

/// <summary>
/// InvertBoolToVisibilityConverter —— bool 取反后转 Visibility
/// true → Collapsed
/// false → Visible
/// 专门用于显示"未检测"提示——当 IsDetected 为 false 时亮出提示
/// 和 BoolToVisibilityConverter 刚好反过来，就像互为镜像的末影人 🪞
/// </summary>
public class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v != Visibility.Visible;
        return Binding.DoNothing;
    }
}

/// <summary>
/// BooleanToBrushConverter —— bool 转 Brush
/// true → PrimaryHueLightBrush（按下状态）
/// false → 默认透明
/// 用于按钮按下效果
/// </summary>
public class BooleanToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return System.Windows.Media.Brushes.LightBlue;
        }
        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// StringToVisibilityConverter —— 字符串转 Visibility
/// 非空且非空白 → Visible
/// 空或空白 → Collapsed
/// 用于显示警告文本
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// NullToBoolConverter —— null → false, 非 null → true
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
