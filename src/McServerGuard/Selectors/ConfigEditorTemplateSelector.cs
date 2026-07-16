using System.Windows;
using System.Windows.Controls;
using McServerGuard.Models;

namespace McServerGuard.Selectors;

public class ConfigEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate BoolTemplate { get; set; } = null!;
    public DataTemplate EnumTemplate { get; set; } = null!;
    public DataTemplate NumericTemplate { get; set; } = null!;
    public DataTemplate StringTemplate { get; set; } = null!;

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