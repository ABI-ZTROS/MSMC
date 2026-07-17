// -----------------------------------------------------------------------------
// 文件名: ValueConverters.cs
// 命名空间: McServerGuard.Converters
// 功能描述: 通用值转换器集合，实现 IValueConverter 接口，
//           用于 XAML 数据绑定时的类型转换与条件映射。
//           包含布尔取反、空值可见性、索引匹配、字符串转换等常用转换器。
// 依赖组件: PresentationFramework, System.Windows.Data
// 设计模式: 值转换器 (IValueConverter)
// -----------------------------------------------------------------------------
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace McServerGuard.Converters;

/// <summary>
/// 布尔取反值转换器。
/// 将 bool 值取反后返回，true 变 false，false 变 true。
/// 支持双向转换，Convert 与 ConvertBack 逻辑一致。
/// </summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

/// <summary>
/// 空值可见性值转换器。
/// 当绑定值为 null 时返回 Visibility.Collapsed，非 null 时返回 Visibility.Visible。
/// 单向绑定使用，ConvertBack 返回 Binding.DoNothing。
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        // 单向绑定用途，ConvertBack 理论上不会被调用，返回 Binding.DoNothing 确保安全
        => Binding.DoNothing;
}

/// <summary>
/// 布尔可见性值转换器。
/// true 时返回 Visibility.Visible，false 时返回 Visibility.Collapsed。
/// 支持双向转换。
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
/// 索引布尔值转换器。
/// ConverterParameter 传入字符串形式的目标索引（如 "0"、"1"、"2"、"3"），
/// 当绑定值与该索引相等时返回 true，否则返回 false。
/// ConvertBack：值为 true 时返回 parameter 对应的字符串。
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
        // 值为 true 时返回 parameter 的值，值为 false 时返回 Binding.DoNothing
        if (value is true && parameter != null)
            return parameter.ToString() ?? string.Empty;

        return Binding.DoNothing;
    }
}

/// <summary>
/// 布尔字符串互转值转换器。
/// Convert：string → bool?（"true" → true，其他 → false，非字符串 → Binding.DoNothing）
/// ConvertBack：bool? → string（true → "true"，false → "false"，null → Binding.DoNothing）
/// 容错设计：值不合法时返回 Binding.DoNothing，避免产生绑定错误。
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
/// 值类型可见性值转换器。
/// ConverterParameter 传入期望的类型字符串（如 "bool"、"int"、"enum"、"string"），
/// 当绑定值（字符串）与 parameter 相等时返回 Visible，否则返回 Collapsed。
/// 大小写不敏感比较。
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
        // 单向绑定用途，ConvertBack 不会被调用，返回 Binding.DoNothing
        => Binding.DoNothing;
}

/// <summary>
/// 布尔转图标名称值转换器。
/// 将 bool 值转换为 MaterialDesign PackIcon 的 Kind 名称字符串。
/// true → "Check"，false → "Alert"。
/// 支持双向转换。
/// </summary>
public class BoolToPackIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "Check" : "Alert";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        // 单向绑定不需要 ConvertBack，仍提供合理实现：Check → true，其余 → false
        => value is string s && s == "Check";
}

/// <summary>
/// 布尔转文本值转换器。
/// true → "是"，false → "否"。
/// 支持双向转换。
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
/// 布尔取反可见性值转换器。
/// true → Collapsed，false → Visible。
/// 与 BoolToVisibilityConverter 逻辑相反，适用于"未检测"等否定性提示的显示控制。
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
/// 布尔转画笔值转换器。
/// true → LightBlue（按下状态），false → Transparent。
/// 用于按钮按下等视觉反馈效果。
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
/// 字符串可见性值转换器。
/// 非空且非空白 → Visible，空或空白 → Collapsed。
/// 用于警告文本等条件显示场景。
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 空值布尔值转换器。
/// null → false，非 null → true。
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
