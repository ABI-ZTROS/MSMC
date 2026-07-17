// -----------------------------------------------------------------------------
// 文件名: SettingsPage.xaml.cs
// 命名空间: McServerGuard.Views
// 功能描述: 设置页面代码隐藏类，负责页面入场动画控制、主题颜色编辑交互、
//           取色器弹窗绑定以及作者卡片外部链接跳转。
// 依赖组件: PresentationFramework, System.Windows.Media
// 设计模式: 代码隐藏模式
// -----------------------------------------------------------------------------
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

/// <summary>
/// 设置页面代码隐藏类。
/// 负责页面级入场动画控制、主题颜色十六进制输入同步、取色器弹窗数据绑定
/// 以及作者卡片点击跳转 GitHub 主页等交互逻辑。
/// </summary>
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

    // 页面 Loaded 事件处理：首次加载触发入场动画，重复加载直接显示
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

    // 主色输入框失去焦点事件处理：同步文本值至 ViewModel
    private void PrimaryColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.PrimaryColorHex = textBox.Text;
        }
    }

    // 强调色输入框失去焦点事件处理：同步文本值至 ViewModel
    private void AccentColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.AccentColorHex = textBox.Text;
        }
    }

    // 背景色输入框失去焦点事件处理：同步文本值至 ViewModel
    private void BackgroundColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.BackgroundColorHex = textBox.Text;
        }
    }

    // 卡片色输入框失去焦点事件处理：同步文本值至 ViewModel
    private void CardColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.CardColorHex = textBox.Text;
        }
    }

    // 文本色输入框失去焦点事件处理：同步文本值至 ViewModel
    private void TextColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.TextColorHex = textBox.Text;
        }
    }

    // 边框色输入框失去焦点事件处理：同步文本值至 ViewModel
    private void BorderColorHex_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && DataContext is ViewModels.SettingsViewModel viewModel)
        {
            viewModel.BorderColorHex = textBox.Text;
        }
    }

    /// <summary>
    /// 打开取色器弹窗并建立双向数据绑定。
    /// 先清除旧绑定再重新设置，确保绑定源与属性路径正确对应。
    /// </summary>
    /// <param name="target">弹窗定位目标元素</param>
    /// <param name="propertyPath">ViewModel 中颜色属性的路径</param>
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

    // 主色预览区鼠标左键按下事件处理：打开主色取色器
    private void PrimaryColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenColorPicker((UIElement)sender, nameof(ViewModels.SettingsViewModel.PrimaryColor));
    }

    // 强调色预览区鼠标左键按下事件处理：打开强调色取色器
    private void AccentColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenColorPicker((UIElement)sender, nameof(ViewModels.SettingsViewModel.AccentColor));
    }

    /// <summary>
    /// 使用系统默认浏览器打开指定 GitHub 用户主页。
    /// 异常静默处理，确保不因外部调用失败影响应用运行。
    /// </summary>
    /// <param name="username">GitHub 用户名</param>
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

    // 作者卡片 ABI-ZTROS 鼠标左键按下事件处理
    private void AuthorCard_ABI_ZTROS_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGitHub("ABI-ZTROS");
    }

    // 作者卡片 Gglaoguan 鼠标左键按下事件处理
    private void AuthorCard_Gglaoguan_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGitHub("Gglaoguan");
    }

    // 作者卡片 MochaCello92377 鼠标左键按下事件处理
    private void AuthorCard_MochaCello92377_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGitHub("MochaCello92377");
    }

    // 作者卡片 CatStack-pixe 鼠标左键按下事件处理
    private void AuthorCard_CatStack_pixe_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenGitHub("CatStack-pixe");
    }
}
