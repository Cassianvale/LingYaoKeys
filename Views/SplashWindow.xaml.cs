using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using WPFProgressBar = System.Windows.Controls.ProgressBar;

namespace WpfApp.Views;

/// <summary>
/// SplashWindow.xaml 的交互逻辑
/// </summary>
public partial class SplashWindow : Window
{
    private TextBlock _statusText;
    private Border _progressBarContainer;
    private double _containerActualWidth;
    private BlurEffect _glowEffect;
    private Storyboard _pulseStoryboard;

    public SplashWindow()
    {
        InitializeComponent();
        Loaded += OnSplashWindowLoaded;
        _statusText = (TextBlock)FindName("StatusText");
        _progressBarContainer = (Border)FindName("ProgressBarContainer");
        _glowEffect = (BlurEffect)FindName("GlowEffect");
        
        // 初始化脉冲动画
        InitializePulseAnimation();
    }

    private void InitializePulseAnimation()
    {
        _pulseStoryboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = 0.3,
            To = 0.6,
            Duration = TimeSpan.FromSeconds(1.5),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        
        Storyboard.SetTarget(animation, _glowEffect);
        Storyboard.SetTargetProperty(animation, new PropertyPath(BlurEffect.RadiusProperty));
        
        _pulseStoryboard.Children.Add(animation);
    }

    private void OnSplashWindowLoaded(object sender, RoutedEventArgs e)
    {
        // 获取进度条容器的实际宽度
        _containerActualWidth = ((Grid)_progressBarContainer.Parent).ActualWidth;
        
        // 启动脉冲动画
        _pulseStoryboard.Begin();
    }

    public void UpdateProgress(string message, int percentage)
    {
        if (_statusText == null || _progressBarContainer == null) return;

        Dispatcher.Invoke(() =>
        {
            _statusText.Text = message;
            
            // 设置进度条宽度（0-100%）
            double width = (_containerActualWidth * percentage) / 100.0;
            
            // 使用动画使进度条平滑过渡
            var animation = new DoubleAnimation
            {
                To = width,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            _progressBarContainer.BeginAnimation(FrameworkElement.WidthProperty, animation);
        });
    }
}