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
    private Storyboard _fadeInStoryboard;

    public SplashWindow()
    {
        InitializeComponent();
        Loaded += OnSplashWindowLoaded;
        _statusText = (TextBlock)FindName("StatusText");
        _progressBarContainer = (Border)FindName("ProgressBarContainer");
        
        // 获取淡入动画
        _fadeInStoryboard = (Storyboard)FindResource("FadeInStoryboard");
        
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

    }

    /// <summary>
    /// 窗口加载事件处理
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 开始淡入动画
            if (_fadeInStoryboard != null)
            {
                _fadeInStoryboard.Begin(this);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"启动动画异常: {ex.Message}");
        }
    }

    private void OnSplashWindowLoaded(object sender, RoutedEventArgs e)
    {
        // 获取进度条容器的实际宽度
        _containerActualWidth = ((Grid)_progressBarContainer.Parent).ActualWidth;

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