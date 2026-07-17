// -----------------------------------------------------------------------------
// 文件名: ConfigEditorTemplateSelector.cs
// 命名空间: McServerGuard.Selectors
// 功能描述: 配置编辑器数据模板选择器，根据配置项类型动态选择 UI 编辑模板
// 依赖组件: System.Windows, ServerConfigEntry
// 设计模式: 策略模式 + 数据模板选择器
// -----------------------------------------------------------------------------
using System.Windows;
using System.Windows.Controls;
using McServerGuard.Models;

namespace McServerGuard.Selectors;

/// <summary>
/// 配置编辑器数据模板选择器，根据配置项的值类型动态选择对应的 UI 编辑模板。
/// 作为 WPF 数据绑定与模板系统的策略分发器，实现布尔、枚举、数值、字符串四类编辑控件的自动切换。
/// </summary>
public class ConfigEditorTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// 布尔类型配置项的编辑模板。
    /// 对应 ToggleButton 或 CheckBox 控件。
    /// </summary>
    public DataTemplate BoolTemplate { get; set; } = null!;

    /// <summary>
    /// 枚举类型配置项的编辑模板。
    /// 对应 ComboBox 下拉选择控件。
    /// </summary>
    public DataTemplate EnumTemplate { get; set; } = null!;

    /// <summary>
    /// 数值类型配置项的编辑模板。
    /// 对应数值输入框控件。
    /// </summary>
    public DataTemplate NumericTemplate { get; set; } = null!;

    /// <summary>
    /// 字符串类型配置项的编辑模板。
    /// 对应普通 TextBox 文本输入控件，亦为默认模板。
    /// </summary>
    public DataTemplate StringTemplate { get; set; } = null!;

    /// <summary>
    /// 根据绑定项的数据类型选择对应的数据模板。
    /// 优先级：布尔 > 枚举 > 数值 > 字符串（默认）。
    /// </summary>
    /// <param name="item">绑定的数据对象，预期为 ServerConfigEntry</param>
    /// <param name="container">绑定目标的依赖对象</param>
    /// <returns>选中的 DataTemplate 实例</returns>
    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is not ServerConfigEntry entry)
            return StringTemplate;

        if (entry.IsBoolType)
            return BoolTemplate;

        if (entry.IsEnumType)
            return EnumTemplate;

        if (entry.IsNumericType)
            return NumericTemplate;

        return StringTemplate;
    }
}
