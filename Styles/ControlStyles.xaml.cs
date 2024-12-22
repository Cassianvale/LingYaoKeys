using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using WpfApp.Services;

// 焦点管理器

/// 由于 HotkeyService 中的热键处理机制，会在失去焦点时取消注册热键
/// 取消注册热键的过程会导致窗口重新获得短暂的焦点
/// 这会导致焦点管理器错误地认为窗口已经获得焦点，从而触发不必要的焦点清理

namespace WpfApp.Styles
{
    public partial class ControlStyles
    {
        private static readonly LogManager _logger = LogManager.Instance;
        private static readonly string LOG_TAG = "ControlStyles";

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
                    _logger.LogWarning(LOG_TAG, $"[Focus] 无法获取控件所属窗口: {element.Name ?? element.GetType().Name}");
                    return;
                }

                // 使用预定义的附加属性检查窗口是否已初始化
                if (!GetFocusManagementInitialized(parentWindow))
                {
                    SetFocusManagementInitialized(parentWindow, true);
                    _logger.LogDebug(LOG_TAG, $"[Focus] 初始化窗口级别焦点管理: {parentWindow.Title}");

                    // 窗口失去激活状态时
                    parentWindow.Deactivated += (s, args) =>
                    {
                        // 立即记录日志
                        _logger.LogDebug(LOG_TAG, $"[Focus] 窗口失去焦点: {parentWindow.Title}");
                        
                        // 然后异步处理焦点清理
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ForceClearAllFocus(parentWindow);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    };

                    // 窗口获得焦点时检查并清理
                    parentWindow.Activated += (s, args) =>
                    {
                        // 立即记录日志
                        _logger.LogDebug(LOG_TAG, $"[Focus] 窗口获得焦点: {parentWindow.Title}");
                        
                        // 然后异步处理焦点检查
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var focusedElement = FocusManager.GetFocusedElement(parentWindow) as FrameworkElement;
                            if (focusedElement != null)
                            {
                                _logger.LogDebug(LOG_TAG, $"[Focus] 当前焦点元素: {GetControlIdentifier(focusedElement)}");
                            }
                            
                            CheckAndClearFocus(parentWindow);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    };

                    // 全局鼠标点击事件
                    parentWindow.PreviewMouseDown += (s, args) =>
                    {
                        if (args.OriginalSource is DependencyObject clicked)
                        {
                            var clickedElement = clicked as FrameworkElement;
                            if (clickedElement != null)
                            {
                                _logger.LogDebug(LOG_TAG, $"[Focus] 鼠标点击元素: {GetControlIdentifier(clickedElement)}");
                            }
                            
                            HandleGlobalClick(clicked, parentWindow);
                        }
                    };
                }

                // 控件特定的处理
                if (element is TextBox textBox)
                {
                    _logger.LogDebug(LOG_TAG, $"[Focus] 设置TextBox焦点行为: {GetControlIdentifier(textBox)}");
                    SetupTextBoxBehavior(textBox);
                }
                else if (element is ComboBox comboBox)
                {
                    _logger.LogDebug(LOG_TAG, $"[Focus] 设置ComboBox焦点行为: {GetControlIdentifier(comboBox)}");
                    SetupComboBoxBehavior(comboBox);
                }

                _logger.LogDebug(LOG_TAG, $"[Focus] 焦点管理设置完成: {GetControlIdentifier(element)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(LOG_TAG, $"[Focus] 设置焦点管理时发生异常: {element.GetType().Name}", ex);
            }
        }

        private static void SetupTextBoxBehavior(TextBox textBox)
        {
            // 1. 鼠标点击事件
            textBox.PreviewMouseDown += (sender, args) =>
            {
                if (sender is TextBox tb && !tb.IsKeyboardFocused)
                {
                    args.Handled = true;
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        tb.Focus();
                        tb.SelectAll();   // 获得焦点时全选文本
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            };
            
            // 2. 按键事件（Enter/Escape）
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
                    _logger.LogDebug(LOG_TAG, $"[Focus] TextBox获得焦点: {GetControlIdentifier(tb)}");
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
                    _logger.LogDebug(LOG_TAG, $"[Focus] TextBox失去焦点: {GetControlIdentifier(tb)}");
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

            // 添加下拉框打开关闭日志
            comboBox.DropDownOpened += (sender, args) =>
            {
                if (sender is ComboBox cb)
                {
                    _logger.LogDebug(LOG_TAG, $"[Focus] ComboBox下拉框打开: {GetControlIdentifier(cb)}");
                }
            };

            comboBox.DropDownClosed += (sender, args) =>
            {
                if (sender is ComboBox cb)
                {
                    _logger.LogDebug(LOG_TAG, $"[Focus] ComboBox下拉框关闭: {GetControlIdentifier(cb)}");
                }
            };

            // 添加焦点变化日志
            comboBox.GotFocus += (sender, args) =>
            {
                if (sender is ComboBox cb)
                {
                    _logger.LogDebug(LOG_TAG, $"[Focus] ComboBox获得焦点: {GetControlIdentifier(cb)}");
                }
            };

            comboBox.LostFocus += (sender, args) =>
            {
                if (sender is ComboBox cb)
                {
                    _logger.LogDebug(LOG_TAG, $"[Focus] ComboBox失去焦点: {GetControlIdentifier(cb)}");
                }
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
                var focusedElement = FocusManager.GetFocusedElement(window) as FrameworkElement;
                if (focusedElement != null)
                {
                    _logger.LogDebug(LOG_TAG, $"[Focus] 清除窗口所有焦点, 当前焦点元素: {GetControlIdentifier(focusedElement)}");
                    ForceClearFocus(focusedElement);
                }
                
                Keyboard.ClearFocus();
                window.Focus();
            }
            catch (Exception ex)
            {
                _logger.LogError(LOG_TAG, "[Focus] 强制清除所有焦点时发生异常", ex);
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
                var focusedElement = FocusManager.GetFocusedElement(window) as FrameworkElement;
                if (focusedElement != null)
                {
                    _logger.LogDebug(LOG_TAG, $"[Focus] 检查焦点元素: {GetControlIdentifier(focusedElement)}");
                    
                    if (focusedElement is TextBox || focusedElement is ComboBox)
                    {
                        _logger.LogDebug(LOG_TAG, $"[Focus] 清除输入控件焦点: {GetControlIdentifier(focusedElement)}");
                        ForceClearFocus(focusedElement);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(LOG_TAG, "[Focus] 检查并清除焦点时发生异常", ex);
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

        // 添加新的辅助方法用于获取控件标识
        private static string GetControlIdentifier(FrameworkElement element)
        {
            var name = !string.IsNullOrEmpty(element.Name) ? element.Name : "Unnamed";
            var type = element.GetType().Name;
            return $"{type}[{name}]";
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