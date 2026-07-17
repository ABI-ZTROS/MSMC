using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;
using McServerGuard.Services;
using McServerGuard.Views.Controls;
using McServerGuard.Views.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace McServerGuard.Views;

public partial class SettingsPage : UserControl
{
    private readonly IThemeService _themeService;
    private bool _animationPlayed;

    public SettingsPage()
    {
        InitializeComponent();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_animationPlayed)
        {
            Opacity = 1;
            return;
        }
        _animationPlayed = true;

        var duration = _themeService.EnableAnimations ? _themeService.AnimationDuration : 0;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            AnimationHelper.FadeAndSlideIn(this, duration);
        });
    }

    private void PrimaryColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.PrimaryColorHex = textBox.Text;
        }
    }

    private void AccentColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.AccentColorHex = textBox.Text;
        }
    }

    private void BackgroundColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.BackgroundColorHex = textBox.Text;
        }
    }

    private void CardColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.CardColorHex = textBox.Text;
        }
    }

    private void TextColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.TextColorHex = textBox.Text;
        }
    }

    private void BorderColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.BorderColorHex = textBox.Text;
        }
    }

    private void OpenColorPicker(UIElement target, string propertyPath)
    {
        ColorPickerPopup.PlacementTarget = target;
        BindingOperations.ClearBinding(ColorPicker, ColorPickerControl.SelectedColorProperty);
        var binding = new Binding(propertyPath)
        {
            Source = DataContext,
            Mode = BindingMode.TwoWay
        };
        ColorPicker.SetBinding(ColorPickerControl.SelectedColorProperty, binding);
        ColorPickerPopup.IsOpen = true;
    }

    private void PrimaryColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenColorPicker((UIElement)sender, nameof(ViewModels.SettingsViewModel.PrimaryColor));
    }

    private void AccentColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenColorPicker((UIElement)sender, nameof(ViewModels.SettingsViewModel.AccentColor));
    }

    private static void OpenGitHub(string username)
    {
        try
        {
            Process.Start(new ProcessStartInfo($"https://github.com/{username}")
            {
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void AuthorCard_ABI_ZTROS_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGitHub("ABI-ZTROS");
    }

    private void AuthorCard_Gglaoguan_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGitHub("Gglaoguan");
    }

    private void AuthorCard_MochaCello92377_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGitHub("MochaCello92377");
    }

    private void AuthorCard_CatStack_pixe_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGitHub("CatStack-pixe");
    }
}
