using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp.Services;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;

namespace WpfApp.Behaviors
{
    /// <summary>
    /// 焦点管理行为 - 提供简单的方式来管理控件的焦点
    /// </summary>
    public static class FocusManagerBehavior
    {
        #region EnableFocusManagement 附加属性

        public static readonly DependencyProperty EnableFocusManagementProperty =
            DependencyProperty.RegisterAttached(
                "EnableFocusManagement",
                typeof(bool),
                typeof(FocusManagerBehavior),
                new PropertyMetadata(false, OnEnableFocusManagementChanged));

        public static bool GetEnableFocusManagement(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableFocusManagementProperty);
        }

        public static void SetEnableFocusManagement(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableFocusManagementProperty, value);
        }

        private static void OnEnableFocusManagementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && (bool)e.NewValue)
            {
                if (element.IsLoaded)
                {
                    InitializeFocusManagement(element);
                }
                else
                {
                    element.Loaded += (s, args) => InitializeFocusManagement(element);
                }
            }
        }

        #endregion

        #region AutoClearFocusOnClick 附加属性

        public static readonly DependencyProperty AutoClearFocusOnClickProperty =
            DependencyProperty.RegisterAttached(
                "AutoClearFocusOnClick",
                typeof(bool),
                typeof(FocusManagerBehavior),
                new PropertyMetadata(false, OnAutoClearFocusOnClickChanged));

        public static bool GetAutoClearFocusOnClick(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoClearFocusOnClickProperty);
        }

        public static void SetAutoClearFocusOnClick(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoClearFocusOnClickProperty, value);
        }

        private static void OnAutoClearFocusOnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && (bool)e.NewValue)
            {
                element.PreviewMouseDown += (s, args) =>
                {
                    if (!IsInputControl(args.OriginalSource as DependencyObject))
                    {
                        FocusManagementService.Instance.ClearFocus();
                    }
                };
            }
        }

        #endregion

        #region 私有辅助方法

        private static void InitializeFocusManagement(FrameworkElement element)
        {
            if (element is TextBox || element is ComboBox)
            {
                FocusManagementService.Instance.RegisterFocusableElement(element);

                // 为TextBox添加特殊处理
                if (element is TextBox textBox)
                {
                    SetupTextBoxBehavior(textBox);
                }
                // 为ComboBox添加特殊处理
                else if (element is ComboBox comboBox)
                {
                    SetupComboBoxBehavior(comboBox);
                }
            }
        }

        private static void SetupTextBoxBehavior(TextBox textBox)
        {
            // 处理Enter和Escape键
            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Escape)
                {
                    e.Handled = true;
                    FocusManagementService.Instance.ClearFocus();
                }
            };
        }

        private static void SetupComboBoxBehavior(ComboBox comboBox)
        {
            // 处理选择变化
            comboBox.SelectionChanged += (s, e) =>
            {
                if (!comboBox.IsDropDownOpen)
                {
                    FocusManagementService.Instance.ClearFocus();
                }
            };

            // 处理下拉框关闭
            comboBox.DropDownClosed += (s, e) =>
            {
                FocusManagementService.Instance.ClearFocus();
            };
        }

        private static bool IsInputControl(DependencyObject element)
        {
            while (element != null)
            {
                if (element is TextBox || element is ComboBox)
                {
                    return true;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        #endregion
    }
} 