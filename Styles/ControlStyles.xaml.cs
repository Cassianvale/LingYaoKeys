using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using WpfApp.Services;

namespace WpfApp.Styles
{
    public partial class ControlStyles
    {
        private static readonly LogManager _logger = LogManager.Instance;

        static ControlStyles()
        {
            _logger.LogDebug("ControlStyles", "ControlStyles 类初始化");
        }

        public static readonly DependencyProperty AutoFocusManagementProperty =
            DependencyProperty.RegisterAttached(
                "AutoFocusManagement",
                typeof(bool),
                typeof(ControlStyles),
                new PropertyMetadata(false, OnAutoFocusManagementChanged));

        public static bool GetAutoFocusManagement(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoFocusManagementProperty);
        }

        public static void SetAutoFocusManagement(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoFocusManagementProperty, value);
        }

        private static void OnAutoFocusManagementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && (bool)e.NewValue)
            {
                _logger.LogDebug("ControlStyles", $"OnAutoFocusManagementChanged 触发: {element.GetType().Name}");

                // 等待控件加载完成
                if (element.IsLoaded)
                {
                    InitializeFocusManagement(element);
                }
                else
                {
                    element.Loaded += (s, args) =>
                    {
                        InitializeFocusManagement(element);
                    };
                }
            }
        }

        // 定义一个静态的附加属性用于标记窗口是否已初始化
        private static readonly DependencyProperty FocusManagementInitializedProperty =
            DependencyProperty.RegisterAttached(
                "_focusManagementInitialized",
                typeof(bool),
                typeof(ControlStyles),
                new PropertyMetadata(false));

        private static bool GetFocusManagementInitialized(DependencyObject obj)
        {
            return (bool)obj.GetValue(FocusManagementInitializedProperty);
        }

        private static void SetFocusManagementInitialized(DependencyObject obj, bool value)
        {
            obj.SetValue(FocusManagementInitializedProperty, value);
        }

        private static void InitializeFocusManagement(FrameworkElement element)
        {
            try
            {
                Window? parentWindow = Window.GetWindow(element);
                if (parentWindow == null)
                {
                    _logger.LogWarning("ControlStyles", $"无法获取控件所属窗口: {element.GetType().Name}");
                    return;
                }

                _logger.LogDebug("ControlStyles", $"开始设置焦点管理: {element.GetType().Name}, Window: {parentWindow.GetType().Name}");

                // 使用预定义的附加属性检查窗口是否已初始化
                if (!GetFocusManagementInitialized(parentWindow))
                {
                    SetFocusManagementInitialized(parentWindow, true);
                    _logger.LogDebug("ControlStyles", "初始化窗口级别焦点管理");

                    // 窗口失去激活状态时
                    parentWindow.Deactivated += (s, args) =>
                    {
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ForceClearAllFocus(parentWindow);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    };

                    // 窗口获得焦点时检查并清理
                    parentWindow.Activated += (s, args) =>
                    {
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            CheckAndClearFocus(parentWindow);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    };

                    // 全局鼠标点击事件
                    parentWindow.PreviewMouseDown += (s, args) =>
                    {
                        if (args.OriginalSource is DependencyObject clicked)
                        {
                            HandleGlobalClick(clicked, parentWindow);
                        }
                    };
                }

                // 控件特定的处理
                if (element is TextBox textBox)
                {
                    SetupTextBoxBehavior(textBox);
                }
                else if (element is ComboBox comboBox)
                {
                    SetupComboBoxBehavior(comboBox);
                }

                _logger.LogDebug("ControlStyles", $"焦点管理设置完成: {element.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError("ControlStyles", $"设置焦点管理时发生异常: {element.GetType().Name}", ex);
            }
        }

        private static void SetupTextBoxBehavior(TextBox textBox)
        {
            // 添加鼠标点击事件处理
            textBox.PreviewMouseDown += (sender, args) =>
            {
                if (sender is TextBox tb && !tb.IsKeyboardFocused)
                {
                    args.Handled = true;
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        tb.Focus();
                        tb.SelectAll();
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            };

            textBox.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter || args.Key == Key.Escape)
                {
                    if (sender is TextBox tb)
                    {
                        args.Handled = true;
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                            ForceClearFocus(tb);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
            };

            // 添加获得焦点时的处理
            textBox.GotFocus += (sender, args) =>
            {
                if (sender is TextBox tb)
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        tb.SelectAll();
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            };

            // 添加失去焦点时的处理
            textBox.LostFocus += (sender, args) =>
            {
                if (sender is TextBox tb)
                {
                    tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }
            };
        }

        private static void SetupComboBoxBehavior(ComboBox comboBox)
        {
            // 添加状态追踪
            bool isSelectionChanging = false;
            bool isAnimating = false;

            // 监听 IsDropDownOpen 属性变化
            var dpd = DependencyPropertyDescriptor.FromProperty(
                ComboBox.IsDropDownOpenProperty, typeof(ComboBox));
            
            dpd.AddValueChanged(comboBox, (sender, args) =>
            {
                if (sender is ComboBox cb)
                {
                    isAnimating = true;
                    
                    // 等待动画完成，增加延迟时间确保动画完整播放
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        isAnimating = false;
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            });

            // 添加鼠标点击事件处理
            comboBox.PreviewMouseDown += (sender, args) =>
            {
                if (sender is ComboBox cb && !isAnimating)
                {
                    // 检查点击源是否为ComboBox本身或其ToggleButton
                    var source = args.OriginalSource as DependencyObject;
                    bool isComboBoxClick = IsComboBoxMainPartClick(source, cb);

                    if (isComboBoxClick)
                    {
                        if (cb.IsDropDownOpen)
                        {
                            isAnimating = true;
                            args.Handled = true;

                            // 使用动画关闭下拉框
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    // 触发关闭动画
                                    cb.IsDropDownOpen = false;
                                }
                                finally
                                {
                                    // 动画完成后重置状态
                                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        isAnimating = false;
                                    }), System.Windows.Threading.DispatcherPriority.Background);
                                }
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                    }
                }
            };

            comboBox.SelectionChanged += (sender, args) =>
            {
                if (sender is ComboBox cb && !isAnimating)
                {
                    isSelectionChanging = true;
                    isAnimating = true;

                    // 确保选择更新完成后再关闭下拉框
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            // 更新绑定
                            cb.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                            cb.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                            cb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                            
                            // 触发关闭动画
                            cb.IsDropDownOpen = false;
                        }
                        finally
                        {
                            // 动画完成后重置状态
                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                isSelectionChanging = false;
                                isAnimating = false;
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            };

            comboBox.DropDownClosed += (sender, args) =>
            {
                if (sender is ComboBox cb)
                {
                    // 只在非选择改变的情况下更新绑定
                    if (!isSelectionChanging)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 更新绑定
                            cb.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                            cb.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                            cb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
            };

            // 添加失去焦点事件处理
            comboBox.LostFocus += (sender, args) =>
            {
                if (sender is ComboBox cb)
                {
                    // 确保更新绑定
                    cb.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                    cb.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                    cb.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                }
            };

            // 在控件卸载时移除属性变化监听
            comboBox.Unloaded += (sender, args) =>
            {
                dpd.RemoveValueChanged(comboBox, (s, e) => { });
            };
        }

        // 添加新的辅助方法
        private static bool IsComboBoxMainPartClick(DependencyObject? source, ComboBox comboBox)
        {
            if (source == null) return false;

            // 检查是否点击在ComboBox的主要部分（非下拉列表部分）
            while (source != null)
            {
                // 如果遇到ComboBoxItem，说明点击在下拉列表上
                if (source is ComboBoxItem)
                    return false;

                // 如果到达ComboBox本身，说明是点击在主要部分
                if (source == comboBox)
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private static void HandleGlobalClick(DependencyObject clicked, Window window)
        {
            try
            {
                var focusedElement = FocusManager.GetFocusedElement(window) as UIElement;
                if (focusedElement == null) return;

                // 获取点击的实际控件
                var clickedControl = clicked as UIElement;
                while (clickedControl == null && clicked != null)
                {
                    clicked = VisualTreeHelper.GetParent(clicked);
                    clickedControl = clicked as UIElement;
                }

                // 特殊处理 ComboBox 相关的点击
                if (IsComboBoxOrItsChildren(clicked))
                {
                    return; // 让 ComboBox 自己处理其内部的点击事件
                }

                // 如果点击了新的输入控件，直接转移焦点
                if (clickedControl != null && IsInputControl(clicked))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // 先更新当前焦点元素的绑定
                        UpdateFocusedElementBinding(focusedElement);
                        // 设置新的焦点
                        clickedControl.Focus();
                    }, System.Windows.Threading.DispatcherPriority.Input);
                    return;
                }

                // 如果点击的是其他控件，且不是当前焦点元素的子元素
                if (!IsDescendantOf(clicked, focusedElement))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        UpdateFocusedElementBinding(focusedElement);
                        ForceClearFocus(focusedElement);
                        
                        // 如果点击的是可获得焦点的控件，则设置焦点
                        if (clickedControl?.Focusable == true)
                        {
                            clickedControl.Focus();
                        }
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("ControlStyles", "处理全局点击事件时发生异常", ex);
            }
        }

        // 添加新的辅助方法
        private static bool IsComboBoxOrItsChildren(DependencyObject? element)
        {
            if (element == null) return false;

            // 检查元素链
            var current = element;
            while (current != null)
            {
                if (current is ComboBox || current is ComboBoxItem)
                    return true;

                // 获取可视化树的父元素
                var visualParent = VisualTreeHelper.GetParent(current);
                if (visualParent != null)
                {
                    current = visualParent;
                }
                else
                {
                    // 如果没有可视化树父元素，尝试获取逻辑树父元素
                    current = LogicalTreeHelper.GetParent(current);
                }
            }
            return false;
        }

        private static void UpdateFocusedElementBinding(UIElement focusedElement)
        {
            if (focusedElement is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
            else if (focusedElement is ComboBox comboBox)
            {
                comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
            }
        }

        private static bool IsInputControl(DependencyObject element)
        {
            return element is TextBox || element is ComboBox ||
                   IsDescendantOf(element, element is TextBox ? element : null) ||
                   IsDescendantOf(element, element is ComboBox ? element : null);
        }

        private static void ForceClearAllFocus(Window window)
        {
            try
            {
                var focusedElement = FocusManager.GetFocusedElement(window) as UIElement;
                if (focusedElement != null)
                {
                    ForceClearFocus(focusedElement);
                }
                
                // 强制清除键盘焦点
                Keyboard.ClearFocus();
                
                // 将焦点设置到窗口
                window.Focus();
                
                _logger.LogDebug("ControlStyles", "强制清除所有焦点");
            }
            catch (Exception ex)
            {
                _logger.LogError("ControlStyles", "强制清除所有焦点时发生异常", ex);
            }
        }

        private static void ForceClearFocus(UIElement element)
        {
            try
            {
                // 更新绑定
                if (element is TextBox textBox)
                {
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }
                else if (element is ComboBox comboBox)
                {
                    comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                }

                // 清除焦点
                var focusScope = FocusManager.GetFocusScope(element);
                if (focusScope != null)
                {
                    FocusManager.SetFocusedElement(focusScope, null);
                }

                // 确保键盘焦点被清除
                if (Keyboard.FocusedElement == element)
                {
                    Keyboard.ClearFocus();
                }

                _logger.LogDebug("ControlStyles", $"强制清除焦点: {element.GetType().Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError("ControlStyles", "强制清除焦点时发生异常", ex);
            }
        }

        private static void CheckAndClearFocus(Window window)
        {
            try
            {
                var focusedElement = FocusManager.GetFocusedElement(window) as UIElement;
                if (focusedElement != null && (focusedElement is TextBox || focusedElement is ComboBox))
                {
                    ForceClearFocus(focusedElement);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("ControlStyles", "检查并清除焦点时发生异常", ex);
            }
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject? parent)
        {
            try
            {
                var current = child;
                while (current != null)
                {
                    if (current == parent)
                    {
                        return true;
                    }
                    current = VisualTreeHelper.GetParent(current) ?? 
                             LogicalTreeHelper.GetParent(current);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError("ControlStyles", "检查控件层级关系时发生异常", ex);
                return false;
            }
        }
    }

    public static class DependencyObjectExtensions
    {
        public static bool HasProperty(this DependencyObject obj, string propertyName)
        {
            return DependencyPropertyDescriptor
                .FromName(propertyName, obj.GetType(), obj.GetType()) != null;
        }
    }
}