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
            textBox.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter || args.Key == Key.Escape)
                {
                    if (sender is TextBox tb)
                    {
                        args.Handled = true;
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ForceClearFocus(tb);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }
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
            comboBox.SelectionChanged += (sender, args) =>
            {
                if (sender is ComboBox cb && cb.IsDropDownOpen)
                {
                    cb.IsDropDownOpen = false;
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ForceClearFocus(cb);
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            };

            comboBox.DropDownClosed += (sender, args) =>
            {
                if (sender is ComboBox cb)
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ForceClearFocus(cb);
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            };
        }

        private static void HandleGlobalClick(DependencyObject clicked, Window window)
        {
            try
            {
                var focusedElement = FocusManager.GetFocusedElement(window) as UIElement;
                if (focusedElement == null) return;

                // 如果点击的不是输入控件或其子元素，清除焦点
                if (!IsInputControl(clicked) && !IsDescendantOf(clicked, focusedElement))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ForceClearFocus(focusedElement);
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("ControlStyles", "处理全局点击事件时发生异常", ex);
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

                // 强制清除焦点
                var focusScope = FocusManager.GetFocusScope(element);
                if (focusScope != null)
                {
                    FocusManager.SetFocusedElement(focusScope, null);
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