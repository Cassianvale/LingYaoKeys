using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.InteropServices;
using WpfApp.Services;

// 焦点管理器

/// 由于 HotkeyService 中的热键处理机制，会在失去焦点时取消注册热键
/// 取消注册热键的过程会导致窗口重新获得短暂的焦点
/// 这会导致焦点管理器错误地认为窗口已经获得焦点，从而触发不必要的焦点清理

namespace WpfApp.Styles
{
    public partial class ControlStyles
    {
        private static readonly SerilogManager _logger = SerilogManager.Instance;
        private static IntPtr _mouseHookHandle;
        private static Win32.HookProc? _mouseProc;

        // Win32 API 定义
        private static class Win32
        {
            public const int WH_MOUSE_LL = 14;
            public const int WM_LBUTTONDOWN = 0x0201;
            public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [StructLayout(LayoutKind.Sequential)]
            public struct POINT
            {
                public int x;
                public int y;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MSLLHOOKSTRUCT
            {
                public POINT pt;
                public uint mouseData;
                public uint flags;
                public uint time;
                public IntPtr dwExtraInfo;
            }
        }

        private static void SetupGlobalMouseHook()
        {
            if (_mouseHookHandle == IntPtr.Zero)
            {
                _mouseProc = MouseHookCallback;
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _mouseHookHandle = Win32.SetWindowsHookEx(
                        Win32.WH_MOUSE_LL,
                        _mouseProc,
                        Win32.GetModuleHandle(curModule?.ModuleName ?? string.Empty),
                        0);
                }

                if (_mouseHookHandle == IntPtr.Zero)
                {
                    _logger.Error("设置全局鼠标钩子失败");
                }
                else
                {
                    _logger.Debug("全局鼠标钩子设置成功");
                }

                // 确保应用程序退出时移除钩子
                System.Windows.Application.Current.Exit += (s, e) => RemoveGlobalMouseHook();
            }
        }

        private static void RemoveGlobalMouseHook()
        {
            if (_mouseHookHandle != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
                _mouseProc = null;
                _logger.Debug("全局鼠标钩子已移除");
            }
        }

        private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)Win32.WM_LBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                var screenPoint = new System.Windows.Point(hookStruct.pt.x, hookStruct.pt.y);

                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (System.Windows.Window window in System.Windows.Application.Current.Windows)
                    {
                        if (window.IsActive)
                        {
                            // 检查是否有打开的ComboBox
                            bool hasOpenComboBox = false;
                            var comboBoxes = FindVisualChildren<System.Windows.Controls.ComboBox>(window);
                            foreach (var comboBox in comboBoxes)
                            {
                                if (comboBox.IsDropDownOpen)
                                {
                                    hasOpenComboBox = true;
                                    break;
                                }
                            }

                            // 如果有打开的ComboBox，不处理窗口外点击
                            if (hasOpenComboBox)
                            {
                                return;
                            }

                            // 检查点击是否在窗口外
                            var windowRect = new Rect(
                                window.Left, window.Top,
                                window.Width, window.Height);

                            if (!windowRect.Contains(screenPoint))
                            {
                                // 将屏幕坐标转换为窗口坐标
                                var windowPoint = window.PointFromScreen(screenPoint);
                                var hitTestResult = VisualTreeHelper.HitTest(window, windowPoint);
                                
                                // 如果点击到了特殊UI元素，不处理窗口外点击
                                if (hitTestResult != null && 
                                    hitTestResult.VisualHit is DependencyObject element && 
                                    IsComboBoxOrItsChildren(element))
                                {
                                    _logger.Debug("点击在特殊UI元素上，不清除焦点");
                                    return;
                                }

                                _logger.Debug("检测到窗口外点击，清除焦点");
                                ForceClearAllFocus(window);
                            }
                        }
                    }
                }));
            }
            return Win32.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        // 辅助方法：查找视觉树中的所有指定类型控件
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                
                if (child is T t)
                    yield return t;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
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
                _logger.Debug($"OnAutoFocusManagementChanged 触发: {element.GetType().Name}");

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
                // 设置全局鼠标钩子
                SetupGlobalMouseHook();

                Window? parentWindow = Window.GetWindow(element);
                if (parentWindow == null)
                {
                    _logger.Warning($"无法获取控件所属窗口: {element.Name ?? element.GetType().Name}");
                    return;
                }

                // 使用预定义的附加属性检查窗口是否已初始化
                if (!GetFocusManagementInitialized(parentWindow))
                {
                    SetFocusManagementInitialized(parentWindow, true);
                    _logger.Debug($"初始化窗口级别焦点管理: {parentWindow.Title}");

                    // 窗口失去激活状态时
                    parentWindow.Deactivated += (s, args) =>
                    {
                        // 立即记录日志
                        _logger.Debug($"窗口失去焦点: {parentWindow.Title}");
                        
                        // 然后异步处理焦点清理
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ForceClearAllFocus(parentWindow);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    };

                    // 窗口获得焦点时检查并清理
                    parentWindow.Activated += (s, args) =>
                    {
                        // 立即记录日志
                        _logger.Debug($"窗口获得焦点: {parentWindow.Title}");
                        
                        // 然后异步处理焦点检查
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var focusedElement = FocusManager.GetFocusedElement(parentWindow) as FrameworkElement;
                            if (focusedElement != null)
                            {
                                _logger.Debug($"当前焦点元素: {GetControlIdentifier(focusedElement)}");
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
                                _logger.Debug($"鼠标点击元素: {GetControlIdentifier(clickedElement)}");
                            }
                            
                            HandleGlobalClick(clicked, parentWindow);
                        }
                    };
                }

                // 控件特定的处理
                if (element is System.Windows.Controls.TextBox textBox)
                {
                    _logger.Debug($"设置TextBox焦点行为: {GetControlIdentifier(textBox)}");
                    SetupTextBoxBehavior(textBox);
                }
                else if (element is System.Windows.Controls.ComboBox comboBox)
                {
                    _logger.Debug($"设置ComboBox焦点行为: {GetControlIdentifier(comboBox)}");
                    SetupComboBoxBehavior(comboBox);
                }

                _logger.Debug($"焦点管理设置完成: {GetControlIdentifier(element)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"设置焦点管理时发生异常: {element.GetType().Name}", ex);
            }
        }

        private static void SetupTextBoxBehavior(System.Windows.Controls.TextBox textBox)
        {
            // 1. 鼠标点击事件
            textBox.PreviewMouseDown += (sender, args) =>
            {
                if (sender is System.Windows.Controls.TextBox tb)
                {
                    // 不立即阻止事件传播
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!tb.IsKeyboardFocused)
                        {
                            tb.Focus();
                            tb.CaretIndex = tb.Text.Length;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            };
            
            // 2. 按键事件（Enter/Escape）
            textBox.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter || args.Key == Key.Escape)
                {
                    if (sender is System.Windows.Controls.TextBox tb)
                    {
                        args.Handled = true;
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            tb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                            ForceClearFocus(tb);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
            };

            // 修改获得焦点时的处理，移除自动全选
            textBox.GotFocus += (sender, args) =>
            {
                if (sender is System.Windows.Controls.TextBox tb)
                {
                    _logger.Debug($"TextBox获得焦点: {GetControlIdentifier(tb)}");
                }
            };

            // 保持失去焦点时的处理不变
            textBox.LostFocus += (sender, args) =>
            {
                if (sender is System.Windows.Controls.TextBox tb)
                {
                    _logger.Debug($"TextBox失去焦点: {GetControlIdentifier(tb)}");
                    tb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                }
            };
        }

        private static void SetupComboBoxBehavior(System.Windows.Controls.ComboBox comboBox)
        {
            // 添加状态追踪
            bool isSelectionChanging = false;
            bool isAnimating = false;

            // 监听 IsDropDownOpen 属性变化
            var dpd = DependencyPropertyDescriptor.FromProperty(
                System.Windows.Controls.ComboBox.IsDropDownOpenProperty, typeof(System.Windows.Controls.ComboBox));
            
            dpd.AddValueChanged(comboBox, (sender, args) =>
            {
                if (sender is System.Windows.Controls.ComboBox cb)
                {
                    isAnimating = true;
                    _logger.Debug($"ComboBox下拉状态改变: {GetControlIdentifier(cb)}, IsDropDownOpen: {cb.IsDropDownOpen}");
                    
                    // 等待动画完成
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        isAnimating = false;
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            });

            // 添加鼠标点击事件处理
            comboBox.PreviewMouseDown += (sender, args) =>
            {
                if (sender is System.Windows.Controls.ComboBox cb && !isAnimating)
                {
                    // 检查点击源是否为ComboBox本身或其ToggleButton
                    var source = args.OriginalSource as DependencyObject;
                    bool isComboBoxClick = IsComboBoxMainPartClick(source, cb);

                    if (isComboBoxClick)
                    {
                        if (cb.IsDropDownOpen)
                        {
                            args.Handled = true;
                            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                cb.IsDropDownOpen = false;
                                // 不立即清除焦点，等待可能的选择操作完成
                                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (!cb.IsDropDownOpen && !isSelectionChanging)
                                    {
                                        ForceClearFocus(cb);
                                    }
                                }), System.Windows.Threading.DispatcherPriority.Background);
                            }, System.Windows.Threading.DispatcherPriority.Input);
                        }
                        else if (!cb.IsKeyboardFocused)
                        {
                            args.Handled = true;
                            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                cb.Focus();
                                cb.IsDropDownOpen = true;
                            }, System.Windows.Threading.DispatcherPriority.Input);
                        }
                    }
                }
            };

            // 添加选择变化事件处理
            comboBox.SelectionChanged += (sender, args) =>
            {
                if (sender is System.Windows.Controls.ComboBox cb && !isSelectionChanging)
                {
                    isSelectionChanging = true;
                    _logger.Debug($"ComboBox选择改变: {GetControlIdentifier(cb)}, SelectedItem: {cb.SelectedItem}");

                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (cb.IsDropDownOpen)
                            {
                                cb.IsDropDownOpen = false;
                            }

                            // 延迟清除焦点，确保选择操作完全完成
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (!cb.IsDropDownOpen)
                                {
                                    ForceClearFocus(cb);
                                }
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                        finally
                        {
                            isSelectionChanging = false;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            };

            // 添加焦点事件处理
            comboBox.GotFocus += (sender, args) =>
            {
                if (sender is System.Windows.Controls.ComboBox cb)
                {
                    _logger.Debug($"ComboBox获得焦点: {GetControlIdentifier(cb)}");
                }
            };

            comboBox.LostFocus += (sender, args) =>
            {
                if (sender is System.Windows.Controls.ComboBox cb)
                {
                    _logger.Debug($"ComboBox失去焦点: {GetControlIdentifier(cb)}");
                    if (cb.IsDropDownOpen && !isSelectionChanging)
                    {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            cb.IsDropDownOpen = false;
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
            };

            // 在控件卸载时移除属性变化监听
            comboBox.Unloaded += (sender, args) =>
            {
                dpd.RemoveValueChanged(comboBox, (s, e) => { });
            };
        }

        private static bool IsComboBoxMainPartClick(DependencyObject? source, System.Windows.Controls.ComboBox comboBox)
        {
            if (source == null) return false;

            // 检查是否点击在ComboBox的主要部分（非下拉列表部分）
            while (source != null)
            {
                // 如果遇到ComboBoxItem，说明点击在下拉列表上
                if (source is System.Windows.Controls.ComboBoxItem)
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

                // 特殊处理 ComboBox 和其他特殊UI元素
                if (IsComboBoxOrItsChildren(clicked))
                {
                    _logger.Debug($"点击在特殊UI元素上: {clicked.GetType().Name}");
                    
                    // 如果当前有焦点元素，且不是特殊UI元素，则更新其绑定
                    if (focusedElement != null && !IsComboBoxOrItsChildren(focusedElement as DependencyObject))
                    {
                        UpdateFocusedElementBinding(focusedElement);
                    }
                    return;
                }

                // 如果点击了新的输入控件
                if (clickedControl != null && IsInputControl(clicked))
                {
                    if (clickedControl != focusedElement)
                    {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            UpdateFocusedElementBinding(focusedElement);
                            clickedControl.Focus();
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }
                    return;
                }

                // 如果点击的是其他控件
                if (!IsDescendantOf(clicked, focusedElement))
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        UpdateFocusedElementBinding(focusedElement);
                        ForceClearFocus(focusedElement);
                        
                        if (clickedControl?.Focusable == true)
                        {
                            clickedControl.Focus();
                        }
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("处理全局点击事件时发生异常", ex);
            }
        }

        private static bool IsComboBoxOrItsChildren(DependencyObject? element)
        {
            if (element == null) return false;

            try
            {
                // 检查元素链
                var current = element;
                while (current != null)
                {
                    // 检查是否是特殊UI元素
                    if (current is System.Windows.Controls.ComboBox || 
                        current is System.Windows.Controls.ComboBoxItem ||
                        current.GetType().Name.Contains("ComboBox") || // 处理ComboBox的模板元素
                        current is System.Windows.Controls.CheckBox || // 处理Switch控件
                        current.GetType().Name.Contains("Switch") || // 处理Switch的模板元素
                        current is System.Windows.Controls.Button || // 处理所有按钮
                        current is System.Windows.Controls.Primitives.Popup || // 处理浮窗
                        current is System.Windows.Controls.ToolTip) // 处理工具提示
                    {
                        _logger.Debug($"检测到特殊UI元素: {current.GetType().Name}");
                        return true;
                    }

                    // 检查父级是否包含特殊UI元素
                    var parent = VisualTreeHelper.GetParent(current);
                    if (parent != null)
                    {
                        // 检查父级是否是特殊UI元素
                        if (parent is System.Windows.Controls.Button ||
                            parent is System.Windows.Controls.CheckBox ||
                            parent is System.Windows.Controls.ComboBox ||
                            parent.GetType().Name.Contains("Switch") ||
                            parent.GetType().Name.Contains("Help"))
                        {
                            _logger.Debug($"检测到特殊UI元素的子元素: {current.GetType().Name} -> {parent.GetType().Name}");
                            return true;
                        }
                        current = parent;
                    }
                    else
                    {
                        // 如果没有可视化树父元素，尝试获取逻辑树父元素
                        current = LogicalTreeHelper.GetParent(current);
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("检查UI元素关系时发生异常", ex);
                return false;
            }
        }

        private static void UpdateFocusedElementBinding(UIElement focusedElement)
        {
            try
            {
                if (focusedElement is System.Windows.Controls.TextBox textBox)
                {
                    textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                }
                else if (focusedElement is System.Windows.Controls.ComboBox comboBox)
                {
                    comboBox.GetBindingExpression(System.Windows.Controls.ComboBox.SelectedItemProperty)?.UpdateSource();
                    comboBox.GetBindingExpression(System.Windows.Controls.ComboBox.SelectedValueProperty)?.UpdateSource();
                    comboBox.GetBindingExpression(System.Windows.Controls.ComboBox.TextProperty)?.UpdateSource();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("更新焦点元素绑定时发生异常", ex);
            }
        }

        private static bool IsInputControl(DependencyObject element)
        {
            return element is System.Windows.Controls.TextBox || 
                   element is System.Windows.Controls.ComboBox ||
                   IsDescendantOf(element, element is System.Windows.Controls.TextBox ? element : null) ||
                   IsDescendantOf(element, element is System.Windows.Controls.ComboBox ? element : null);
        }

        private static void ForceClearAllFocus(Window window)
        {
            try
            {
                var focusedElement = FocusManager.GetFocusedElement(window) as UIElement;
                if (focusedElement != null)
                {
                    UpdateFocusedElementBinding(focusedElement);
                    ForceClearFocus(focusedElement);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("清除所有焦点时发生异常", ex);
            }
        }

        private static void ForceClearFocus(UIElement element)
        {
            try
            {
                if (element is System.Windows.Controls.TextBox textBox)
                {
                    textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                }
                else if (element is System.Windows.Controls.ComboBox comboBox)
                {
                    if (comboBox.IsDropDownOpen)
                    {
                        comboBox.IsDropDownOpen = false;
                    }
                }

                // 移除键盘焦点
                if (element.IsKeyboardFocused)
                {
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Keyboard.ClearFocus();
                        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(element), null);
                    }, System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("强制清除焦点时发生异常", ex);
            }
        }

        private static void CheckAndClearFocus(Window window)
        {
            try
            {
                var focusedElement = FocusManager.GetFocusedElement(window) as UIElement;
                if (focusedElement != null)
                {
                    if (focusedElement is System.Windows.Controls.TextBox || 
                        focusedElement is System.Windows.Controls.ComboBox)
                    {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            UpdateFocusedElementBinding(focusedElement);
                            ForceClearFocus(focusedElement);
                        }, System.Windows.Threading.DispatcherPriority.Input);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("检查并清除焦点时发生异常", ex);
            }
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject? parent)
        {
            try
            {
                while (child != null)
                {
                    if (child == parent)
                        return true;
                    
                    child = VisualTreeHelper.GetParent(child);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("检查元素父子关系时发生异常", ex);
                return false;
            }
        }

        private static string GetControlIdentifier(FrameworkElement element)
        {
            return $"{element.GetType().Name}{(string.IsNullOrEmpty(element.Name) ? "" : $"[{element.Name}]")}";
        }
    }

    public static class DependencyObjectExtensions
    {
        public static bool HasProperty(this DependencyObject obj, string propertyName)
        {
            return DependencyPropertyDescriptor.FromName(
                propertyName, obj.GetType(), obj.GetType()) != null;
        }
    }
}