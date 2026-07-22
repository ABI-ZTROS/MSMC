namespace McServerGuard.Views.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using McServerGuard.Services;

public partial class IndependentLoadingIcon : UserControl
{
    private readonly RotateTransform _rotateTransform;
    private Storyboard? _storyboard;

    public IndependentLoadingIcon()
    {
        InitializeComponent();
        _rotateTransform = (RotateTransform)SpinnerRotate;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(IndependentLoadingIcon),
            new PropertyMetadata(48.0));

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!AnimationSettings.AnimationsEnabled)
            return;

        _storyboard = new Storyboard();
        var anim = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(1),
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(anim, _rotateTransform);
        Storyboard.SetTargetProperty(anim, new PropertyPath(RotateTransform.AngleProperty));
        _storyboard.Children.Add(anim);
        _storyboard.Begin(this, true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _storyboard?.Stop();
        _storyboard = null;
    }
}