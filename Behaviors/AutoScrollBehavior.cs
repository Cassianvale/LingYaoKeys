using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.ComponentModel;
using System.Windows.Data;

namespace WpfApp.Behaviors
{
    /// <summary>
    /// 为TextBox提供自动横向滚动行为的附加属性
    /// </summary>
    public static class AutoScrollBehavior
    {
        #region IsEnabled附加属性

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", 
                typeof(bool), 
                typeof(AutoScrollBehavior), 
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        #endregion

        #region ScrollDelay附加属性

        public static readonly DependencyProperty ScrollDelayProperty =
            DependencyProperty.RegisterAttached(
                "ScrollDelay", 
                typeof(double), 
                typeof(AutoScrollBehavior), 
                new PropertyMetadata(3000.0)); // 默认延迟3秒开始滚动

        public static double GetScrollDelay(DependencyObject obj)
        {
            return (double)obj.GetValue(ScrollDelayProperty);
        }

        public static void SetScrollDelay(DependencyObject obj, double value)
        {
            obj.SetValue(ScrollDelayProperty, value);
        }

        #endregion

        #region ScrollSpeed附加属性

        public static readonly DependencyProperty ScrollSpeedProperty =
            DependencyProperty.RegisterAttached(
                "ScrollSpeed", 
                typeof(double), 
                typeof(AutoScrollBehavior), 
                new PropertyMetadata(10.0)); // 默认滚动速度

        public static double GetScrollSpeed(DependencyObject obj)
        {
            return (double)obj.GetValue(ScrollSpeedProperty);
        }

        public static void SetScrollSpeed(DependencyObject obj, double value)
        {
            obj.SetValue(ScrollSpeedProperty, value);
        }

        #endregion

        #region AutoScrollAnimation附加属性(内部使用)

        private static readonly DependencyProperty AutoScrollAnimationProperty =
            DependencyProperty.RegisterAttached(
                "AutoScrollAnimation", 
                typeof(AnimationTimeline), 
                typeof(AutoScrollBehavior));

        private static AnimationTimeline GetAutoScrollAnimation(DependencyObject obj)
        {
            return (AnimationTimeline)obj.GetValue(AutoScrollAnimationProperty);
        }

        private static void SetAutoScrollAnimation(DependencyObject obj, AnimationTimeline value)
        {
            obj.SetValue(AutoScrollAnimationProperty, value);
        }

        #endregion

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is System.Windows.Controls.TextBox textBox))
                return;

            if ((bool)e.NewValue)
            {
                textBox.TextChanged += TextBox_TextChanged;
                textBox.Loaded += TextBox_Loaded;

                if (textBox.IsLoaded)
                {
                    SetupAutoScroll(textBox);
                }

                // 确保滚动条是隐藏的
                textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
            else
            {
                textBox.TextChanged -= TextBox_TextChanged;
                textBox.Loaded -= TextBox_Loaded;
                textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                StopAnimation(textBox);
            }
        }

        private static void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                SetupAutoScroll(textBox);
            }
        }

        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                SetupAutoScroll(textBox);
            }
        }

        private static void SetupAutoScroll(System.Windows.Controls.TextBox textBox)
        {
            // 停止当前动画
            StopAnimation(textBox);

            // 确保我们有文本，且文本宽度超过TextBox宽度
            if (string.IsNullOrEmpty(textBox.Text))
                return;

            // 计算文本宽度
            var formattedText = new FormattedText(
                textBox.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                textBox.FontSize,
                System.Windows.Media.Brushes.Black,
                new NumberSubstitution(),
                1.0); // 最后一个参数PixelsPerDip，一般使用1.0

            double textWidth = formattedText.Width;
            double textBoxWidth = textBox.ActualWidth - textBox.Padding.Left - textBox.Padding.Right - SystemParameters.VerticalScrollBarWidth;

            if (textWidth <= textBoxWidth)
                return; // 文本不需要滚动

            // 创建从左到右的来回滚动动画
            var animation = new DoubleAnimation
            {
                From = 0,
                To = textWidth - textBoxWidth + 20, // 多滚动一点以便看清末尾
                Duration = new Duration(TimeSpan.FromSeconds((textWidth / GetScrollSpeed(textBox)))),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true, // 来回滚动
                BeginTime = TimeSpan.FromMilliseconds(GetScrollDelay(textBox)) // 延迟开始滚动
            };

            // 设置缓动函数使滚动看起来更自然
            animation.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

            // 保存动画以便后续操作
            SetAutoScrollAnimation(textBox, animation);

            // 开始动画
            textBox.BeginAnimation(ScrollViewerHelper.HorizontalOffsetProperty, animation);
        }

        private static void StopAnimation(System.Windows.Controls.TextBox textBox)
        {
            var animation = GetAutoScrollAnimation(textBox);
            if (animation != null)
            {
                textBox.BeginAnimation(ScrollViewerHelper.HorizontalOffsetProperty, null);
                SetAutoScrollAnimation(textBox, null);
            }
        }
    }

    /// <summary>
    /// 辅助类，用于访问TextBox内部ScrollViewer的HorizontalOffset属性
    /// </summary>
    public static class ScrollViewerHelper
    {
        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.RegisterAttached(
                "HorizontalOffset",
                typeof(double),
                typeof(ScrollViewerHelper),
                new PropertyMetadata(0.0, OnHorizontalOffsetChanged));

        public static double GetHorizontalOffset(DependencyObject obj)
        {
            return (double)obj.GetValue(HorizontalOffsetProperty);
        }

        public static void SetHorizontalOffset(DependencyObject obj, double value)
        {
            obj.SetValue(HorizontalOffsetProperty, value);
        }

        private static void OnHorizontalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is System.Windows.Controls.TextBox textBox)
            {
                ScrollViewer scrollViewer = FindScrollViewer(textBox);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToHorizontalOffset((double)e.NewValue);
                }
            }
        }

        private static ScrollViewer FindScrollViewer(Visual visual)
        {
            if (visual == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
            {
                Visual child = (Visual)VisualTreeHelper.GetChild(visual, i);
                
                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                ScrollViewer descendent = FindScrollViewer(child);
                if (descendent != null)
                    return descendent;
            }

            return null;
        }
    }
} 