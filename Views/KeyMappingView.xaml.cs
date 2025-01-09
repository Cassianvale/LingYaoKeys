using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Text;
using System.Collections.ObjectModel;
using WpfApp.ViewModels;
using WpfApp.Services;
using WpfApp.Services.Models;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using WpfApp.Behaviors;
using System.Windows.Media.Animation;

// 提供按键映射视图
namespace WpfApp.Views
{
    /// <summary>
    /// KeyMappingView.xaml 的交互逻辑
    /// </summary>
    public partial class KeyMappingView : Page
    {   
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private const string KEY_ERROR = "无法识别按键，请检查输入法是否关闭";
        private const string HOTKEY_CONFLICT = "无法设置与热键相同的按键";
        private HotkeyService? _hotkeyService;
        private readonly KeyMappingViewModel _viewModel;

        private KeyMappingViewModel ViewModel => (KeyMappingViewModel)DataContext;

        public KeyMappingView()
        {
            InitializeComponent();
            _viewModel = (KeyMappingViewModel)DataContext;
            
            // 监听 DataContext 变化
            this.DataContextChanged += KeyMappingView_DataContextChanged;
        }

        // 添加 DataContext 变化事件处理
        private void KeyMappingView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is KeyMappingViewModel viewModel)
            {
                _hotkeyService = viewModel.GetHotkeyService();
                _logger.Debug("已获取HotkeyService实例");
            }
        }

        private void KeyInputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox) return;
            
            e.Handled = true;
            
            // 处理IME输入
            if (e.Key == Key.ImeProcessed && e.SystemKey == Key.None)
            {
                ShowError(KEY_ERROR);
                return;
            }

            // 获取实际按键，优先使用SystemKey
            var key = e.SystemKey != Key.None ? e.SystemKey : 
                     e.Key == Key.ImeProcessed ? e.SystemKey : e.Key;
            
            // 过滤无效按键，但允许系统功能键
            if (key == Key.None)
            {
                return;
            }
            
            // 转换并检查按键
            if (TryConvertToLyKeysCode(key, out LyKeysCode lyKeysCode))
            {
                // 检查是否与热键冲突
                if (ViewModel.IsHotkeyConflict(lyKeysCode))
                {
                    ShowError(HOTKEY_CONFLICT);
                    return;
                }

                ViewModel?.SetCurrentKey(lyKeysCode);
                _logger.Debug($"按键已转换: {key} -> {lyKeysCode}");

                // 强制清除焦点
                var focusScope = FocusManager.GetFocusScope(textBox);
                FocusManager.SetFocusedElement(focusScope, null);
                Keyboard.ClearFocus();
                
                // 确保输入框失去焦点
                if (textBox.IsFocused)
                {
                    var parent = textBox.Parent as UIElement;
                    if (parent != null)
                    {
                        parent.Focus();
                    }
                }
            }
            else
            {
                ShowError(KEY_ERROR);
                _logger.Warning($"无法转换按键: {key}");
            }
        }

        // 将WPF的Key映射到LyKeysCode
        private bool TryConvertToLyKeysCode(Key key, out LyKeysCode lyKeysCode)
        {
            // 将WPF的Key映射到LyKeysCode
            lyKeysCode = key switch
            {
                // 字母键
                Key.A => LyKeysCode.VK_A,
                Key.B => LyKeysCode.VK_B,
                Key.C => LyKeysCode.VK_C,
                Key.D => LyKeysCode.VK_D,
                Key.E => LyKeysCode.VK_E,
                Key.F => LyKeysCode.VK_F,
                Key.G => LyKeysCode.VK_G,
                Key.H => LyKeysCode.VK_H,
                Key.I => LyKeysCode.VK_I,
                Key.J => LyKeysCode.VK_J,
                Key.K => LyKeysCode.VK_K,
                Key.L => LyKeysCode.VK_L,
                Key.M => LyKeysCode.VK_M,
                Key.N => LyKeysCode.VK_N,
                Key.O => LyKeysCode.VK_O,
                Key.P => LyKeysCode.VK_P,
                Key.Q => LyKeysCode.VK_Q,
                Key.R => LyKeysCode.VK_R,
                Key.S => LyKeysCode.VK_S,
                Key.T => LyKeysCode.VK_T,
                Key.U => LyKeysCode.VK_U,
                Key.V => LyKeysCode.VK_V,
                Key.W => LyKeysCode.VK_W,
                Key.X => LyKeysCode.VK_X,
                Key.Y => LyKeysCode.VK_Y,
                Key.Z => LyKeysCode.VK_Z,

                // 数字键
                Key.D0 => LyKeysCode.VK_0,
                Key.D1 => LyKeysCode.VK_1,
                Key.D2 => LyKeysCode.VK_2,
                Key.D3 => LyKeysCode.VK_3,
                Key.D4 => LyKeysCode.VK_4,
                Key.D5 => LyKeysCode.VK_5,
                Key.D6 => LyKeysCode.VK_6,
                Key.D7 => LyKeysCode.VK_7,
                Key.D8 => LyKeysCode.VK_8,
                Key.D9 => LyKeysCode.VK_9,

                // 功能键
                Key.F1 => LyKeysCode.VK_F1,
                Key.F2 => LyKeysCode.VK_F2,
                Key.F3 => LyKeysCode.VK_F3,
                Key.F4 => LyKeysCode.VK_F4,
                Key.F5 => LyKeysCode.VK_F5,
                Key.F6 => LyKeysCode.VK_F6,
                Key.F7 => LyKeysCode.VK_F7,
                Key.F8 => LyKeysCode.VK_F8,
                Key.F9 => LyKeysCode.VK_F9,
                Key.F10 => LyKeysCode.VK_F10,
                Key.F11 => LyKeysCode.VK_F11,
                Key.F12 => LyKeysCode.VK_F12,

                // 特殊键
                Key.Escape => LyKeysCode.VK_ESCAPE,
                Key.Tab => LyKeysCode.VK_TAB,
                Key.CapsLock => LyKeysCode.VK_CAPITAL,
                Key.LeftShift => LyKeysCode.VK_LSHIFT,
                Key.RightShift => LyKeysCode.VK_RSHIFT,
                Key.LeftCtrl => LyKeysCode.VK_LCONTROL,
                Key.RightCtrl => LyKeysCode.VK_RCONTROL,
                Key.LeftAlt => LyKeysCode.VK_LMENU,
                Key.RightAlt => LyKeysCode.VK_RMENU,
                Key.Space => LyKeysCode.VK_SPACE,
                Key.Enter => LyKeysCode.VK_RETURN,
                Key.Back => LyKeysCode.VK_BACK,

                // 符号键
                Key.OemTilde => LyKeysCode.VK_OEM_3,
                Key.OemMinus => LyKeysCode.VK_OEM_MINUS,
                Key.OemPlus => LyKeysCode.VK_OEM_PLUS,
                Key.OemOpenBrackets => LyKeysCode.VK_OEM_4,
                Key.OemCloseBrackets => LyKeysCode.VK_OEM_6,
                Key.OemSemicolon => LyKeysCode.VK_OEM_1,
                Key.OemQuotes => LyKeysCode.VK_OEM_7,
                Key.OemComma => LyKeysCode.VK_OEM_COMMA,
                Key.OemPeriod => LyKeysCode.VK_OEM_PERIOD,
                Key.OemQuestion => LyKeysCode.VK_OEM_2,
                Key.OemBackslash => LyKeysCode.VK_OEM_5,

                // 添加方向键映射
                Key.Up => LyKeysCode.VK_UP,
                Key.Down => LyKeysCode.VK_DOWN,
                Key.Left => LyKeysCode.VK_LEFT,
                Key.Right => LyKeysCode.VK_RIGHT,

                _ => LyKeysCode.VK_ESCAPE
            };

            // 修改返回逻辑，添加方向键判断
            return key == Key.Escape || 
                   (key >= Key.Left && key <= Key.Down) || // 方向键
                   lyKeysCode != LyKeysCode.VK_ESCAPE;
        }

        // 处理按键输入框获得焦点
        private void KeyInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (_hotkeyService != null)
                {
                    _hotkeyService.IsInputFocused = true;
                }
            }
        }

        // 处理按键输入框失去焦点
        private void KeyInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (_hotkeyService != null)
                {
                    _hotkeyService.IsInputFocused = false;
                }
            }
        }

        // 处理超链接请求导航
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        // 显示错误信息到状态栏
        private void ShowError(string message)
        {
            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.UpdateStatusMessage(message, true);
            }
        }

        // 处理热键输入框获得焦点
        private void HotkeyInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (_hotkeyService != null)
                {
                    _hotkeyService.IsInputFocused = true;
                    _logger.Debug($"输入框获得焦点: {textBox.Name}");
                }
            }
        }

        // 处理热键输入框失去焦点
        private void HotkeyInputBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (_hotkeyService != null)
                {
                    _hotkeyService.IsInputFocused = false;
                    _logger.Debug($"输入框失去焦点: {textBox.Name}");
                }
            }
        }

        // 处理鼠标按键
        private void HotkeyInputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                LyKeysCode keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => LyKeysCode.VK_MBUTTON,
                    MouseButton.XButton1 => LyKeysCode.VK_XBUTTON1,
                    MouseButton.XButton2 => LyKeysCode.VK_XBUTTON2,
                    _ => LyKeysCode.VK_ESCAPE
                };

                if (keyCode != LyKeysCode.VK_ESCAPE)
                {
                    ViewModel.SetCurrentKey(keyCode);
                    e.Handled = true;
                }
            }
        }

        // 统一的热键处理方法
        private void HandleHotkeyInput(System.Windows.Controls.TextBox textBox, LyKeysCode keyCode, ModifierKeys modifiers, bool isStartHotkey)
        {
            if (textBox == null)
            {
                _logger.Warning("HandleHotkeyInput: textBox is null");
                return;
            }

            // 只过滤修饰键
            if (IsModifierKey(keyCode))
            {
                return;
            }

            // 记录热键输入处理
            _logger.Debug($"处理热键输入 - keyCode: {keyCode}, 修饰键: {modifiers}, isStartHotkey: {isStartHotkey}");

            // 区分当前处理的是开始热键还是停止热键
            if (isStartHotkey)
            {
                ViewModel?.SetStartHotkey(keyCode, modifiers);
            }
            else
            {
                ViewModel?.SetStopHotkey(keyCode, modifiers);
            }
        }

        // 判断是否为修饰键
        private bool IsModifierKey(LyKeysCode keyCode)
        {
            return keyCode == LyKeysCode.VK_LCONTROL 
                || keyCode == LyKeysCode.VK_RCONTROL
                || keyCode == LyKeysCode.VK_LMENU 
                || keyCode == LyKeysCode.VK_RMENU
                || keyCode == LyKeysCode.VK_LSHIFT 
                || keyCode == LyKeysCode.VK_RSHIFT;
        }

        // 处理开始热键
        private void StartHotkeyInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _logger.Debug("开始热键 Keyboard 按下 已触发");
            _logger.Debug($"Key: {e.Key}, SystemKey: {e.SystemKey}, KeyStates: {e.KeyStates}");
            StartHotkeyInput_PreviewKeyDown(sender, e);
        }

        private void StartHotkeyInput_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _logger.Debug("开始热键 Mouse 按下 已触发");
            _logger.Debug($"ChangedButton: {e.ChangedButton}");
            StartHotkeyInput_PreviewMouseDown(sender, e);
        }

        // 处理开始热键的鼠标释放
        private void StartHotkeyInput_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _logger.Debug("开始热键 Mouse 释放 已触发");
            _logger.Debug($"ChangedButton: {e.ChangedButton}");
        }

        private void StartHotkeyInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox) return;

            try 
            {
                e.Handled = true;
                
                if (e.Key == Key.ImeProcessed && e.SystemKey == Key.None)
                {
                    ShowError(KEY_ERROR);
                    return;
                }

                // 获取实际按键，优先使用SystemKey
                var key = e.SystemKey != Key.None ? e.SystemKey : 
                         e.Key == Key.ImeProcessed ? e.SystemKey : e.Key;
                
                if (key == Key.None)
                {
                    return;
                }

                if (TryConvertToLyKeysCode(key, out LyKeysCode lyKeysCode))
                {
                    HandleHotkeyInput(textBox, lyKeysCode, Keyboard.Modifiers, true);
                    _logger.Debug($"开始热键已转换: {key} -> {lyKeysCode}");
                }
                else
                {
                    ShowError(KEY_ERROR);
                    _logger.Warning($"无法转换开始热键: {key}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("StartHotkeyInput_PreviewKeyDown 处理异常", ex);
            }
        }

        // 处理停止热键
        private void StopHotkeyInput_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox) return;
            
            try
            {
                e.Handled = true;
                
                if (e.Key == Key.ImeProcessed && e.SystemKey == Key.None)
                {
                    ShowError(KEY_ERROR);
                    return;
                }

                // 获取实际按键，优先使用SystemKey
                var key = e.SystemKey != Key.None ? e.SystemKey : 
                         e.Key == Key.ImeProcessed ? e.SystemKey : e.Key;
                
                if (key == Key.None)
                {
                    return;
                }

                // 获取当前修饰键状态
                var modifiers = Keyboard.Modifiers;
                
                if (TryConvertToLyKeysCode(key, out LyKeysCode lyKeysCode))
                {
                    if (IsModifierKey(lyKeysCode))
                    {
                        return;
                    }
                    HandleHotkeyInput(textBox, lyKeysCode, modifiers, false);
                    _logger.Debug($"停止热键已转换: {key} -> {lyKeysCode}");
                }
                else
                {
                    ShowError(KEY_ERROR);
                    _logger.Warning($"无法转换停止热键: {key}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("停止热键 Keyboard 按下 处理异常", ex);
            }
        }

        // 处理开始热键的鼠标点击
        private void StartHotkeyInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                LyKeysCode? keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => LyKeysCode.VK_MBUTTON,
                    MouseButton.XButton1 => LyKeysCode.VK_XBUTTON1,
                    MouseButton.XButton2 => LyKeysCode.VK_XBUTTON2,
                    _ => null // 对于左键和右键不处理，让输入框正常获取焦点以接收键盘输入
                };

                if (keyCode.HasValue)
                {
                    _logger.Debug($"检测到 Mouse 按键点击: {keyCode.Value}");
                    HandleHotkeyInput(textBox, keyCode.Value, Keyboard.Modifiers, true);
                    e.Handled = true; // 阻止事件继续传播
                }
            }
        }

        // 处理停止热键的鼠标点击
        private void StopHotkeyInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                LyKeysCode? keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => LyKeysCode.VK_MBUTTON,
                    MouseButton.XButton1 => LyKeysCode.VK_XBUTTON1,
                    MouseButton.XButton2 => LyKeysCode.VK_XBUTTON2,
                    _ => null // 对于左键和右键，不处理，让输入框正常获取焦点以接收键盘输入
                };

                if (keyCode.HasValue)
                {
                    _logger.Debug($"检测到 Mouse 按键点击: {keyCode.Value}");
                    HandleHotkeyInput(textBox, keyCode.Value, Keyboard.Modifiers, false);
                    e.Handled = true; // 阻止事件继续传播
                }
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // 只验证输入是否为数字，允许输入任何整数
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void NumberInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // 处理焦点状态
                if (_hotkeyService != null)
                {
                    _hotkeyService.IsInputFocused = false;
                    _logger.Debug("数字输入框失去焦点");
                }

                // 验证并纠正值
                if (!string.IsNullOrEmpty(textBox.Text))
                {
                    if (int.TryParse(textBox.Text, out int value))
                    {
                        if (value <= 0)
                        {
                            // 处理无效值（小于等于0）
                            _logger.Debug($"按键间隔值 {value} 无效，必须大于0");
                            if (DataContext is KeyMappingViewModel viewModel)
                            {
                                viewModel.KeyInterval = 1; // 设置为最小值
                                textBox.Text = viewModel.KeyInterval.ToString(); // 显示实际设置的值
                                
                                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                                {
                                    mainViewModel.UpdateStatusMessage("按键间隔必须大于0毫秒", true);
                                }
                            }
                        }
                        else
                        {
                            // 值有效，更新到ViewModel
                            if (DataContext is KeyMappingViewModel viewModel)
                            {
                                int oldValue = viewModel.KeyInterval;
                                viewModel.KeyInterval = value;
                                
                                // 如果值被调整，显示提示信息
                                if (oldValue != viewModel.KeyInterval)
                                {
                                    textBox.Text = viewModel.KeyInterval.ToString();
                                    if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                                    {
                                        mainViewModel.UpdateStatusMessage($"键间隔已自动调整为: {viewModel.KeyInterval}ms", true);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // 输入的不是有效数字
                        _logger.Debug("输入的不是有效数字");
                        if (DataContext is KeyMappingViewModel viewModel)
                        {
                            textBox.Text = viewModel.KeyInterval.ToString(); // 恢复为当前值
                            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                            {
                                mainViewModel.UpdateStatusMessage("请输入有效的数字", true);
                            }
                        }
                    }
                }
                else
                {
                    // 输入框为空，恢复为当前值
                    if (DataContext is KeyMappingViewModel viewModel)
                    {
                        textBox.Text = viewModel.KeyInterval.ToString();
                    }
                }
            }
        }

        private void HandleStartHotkey(bool isKeyDown)
        {
            if (ViewModel == null)
            {
                _logger.Warning("ViewModel is null");
                return;
            }
            
            try
            {
                if (ViewModel.SelectedKeyMode == 0) // 顺序模式
                {
                    _logger.Debug($"顺序模式 - 按键{(isKeyDown ? "按下" : "释放")}");
                    if (isKeyDown) // 按下时启动
                    {
                        ViewModel.StartKeyMapping();
                    }
                }
                else // 按压模式
                {
                    _logger.Debug($"按压模式 - 按键{(isKeyDown ? "按下" : "释放")}");
                    if (isKeyDown)
                    {
                        ViewModel.StartKeyMapping();
                        ViewModel.SetHoldMode(true);
                    }
                    else
                    {
                        ViewModel.SetHoldMode(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("处理开始热键异常", ex);
            }
        }

        private void HandleStopHotkey()
        {
            try
            {
                if (ViewModel == null)
                {
                    _logger.Warning("ViewModel is null");
                    return;
                }
                
                _logger.Debug("处理停止热键");
                ViewModel.StopKeyMapping();
            }
            catch (Exception ex)
            {
                _logger.Error("处理理停止热键异常", ex);
            }
        }

        // 添加数字输入框焦点事件处理
        private void NumberInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_hotkeyService != null)
            {
                _hotkeyService.IsInputFocused = true;
                _logger.Debug("数字输入框获得焦点");
            }
        }

        private void KeysList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox)
            {
                // 检查点击是否在ScrollBar上
                var scrollBar = FindParent<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject);
                if (scrollBar != null)
                {
                    // 如果点击在滚动条上，不处理事件
                    return;
                }

                // 检查点击是否在ListBox的空白区域
                HitTestResult hitTest = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
                if (hitTest == null || 
                    (hitTest.VisualHit != null && 
                     FindParent<System.Windows.Controls.ListBoxItem>(hitTest.VisualHit as DependencyObject) == null))
                {
                    // 点击在ListBox的空白区域，清除选中项和高亮
                    ClearListBoxSelection(listBox);
                    e.Handled = true;
                }
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;

            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            return parent as T;
        }

        private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 检查点击是否在ListBox区域内
            if (e.OriginalSource is DependencyObject depObj)
            {
                var listBox = FindParent<System.Windows.Controls.ListBox>(depObj);
                if (listBox == null) // 点击在ListBox外部
                {
                    // 查找页面中的ListBox
                    var pageListBox = FindChild<System.Windows.Controls.ListBox>((sender as Page)!);
                    if (pageListBox != null)
                    {
                        ClearListBoxSelection(pageListBox);
                    }
                }
            }
        }

        // 添加一个通用的清除方法
        private void ClearListBoxSelection(System.Windows.Controls.ListBox listBox)
        {
            listBox.SelectedItem = null;

            // 清除所有项的拖拽高亮显示
            foreach (var item in listBox.Items)
            {
                if (listBox.ItemContainerGenerator.ContainerFromItem(item) is System.Windows.Controls.ListBoxItem listBoxItem)
                {
                    DragDropProperties.SetIsDragTarget(listBoxItem, false);
                }
            }
        }

        // 添加FindChild辅助方法
        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;
                
                var result = FindChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void IntervalHelp_Click(object sender, RoutedEventArgs e)
        {
            // 切换帮助浮窗的显示状态
            if (helpPopup != null)
            {
                helpPopup.IsOpen = !helpPopup.IsOpen;
            }
        }

        private void ModeHelp_Click(object sender, RoutedEventArgs e)
        {
            // 切换帮助浮窗的显示状态
            if (modeHelpPopup != null)
            {
                modeHelpPopup.IsOpen = !modeHelpPopup.IsOpen;
            }
        }

        private void KeyInputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox) return;

            // 如果输入框没有焦点，优先获取焦点
            if (!textBox.IsFocused)
            {
                textBox.Focus();
                e.Handled = true;
                return;
            }

            // 已有焦点时，处理所有鼠标按键
            LyKeysCode? keyCode = e.ChangedButton switch
            {
                MouseButton.Left => LyKeysCode.VK_LBUTTON,
                MouseButton.Right => LyKeysCode.VK_RBUTTON,
                MouseButton.Middle => LyKeysCode.VK_MBUTTON,
                MouseButton.XButton1 => LyKeysCode.VK_XBUTTON1,
                MouseButton.XButton2 => LyKeysCode.VK_XBUTTON2,
                _ => null
            };

            if (keyCode.HasValue)
            {
                e.Handled = true;

                // 检查是否与热键冲突
                if (ViewModel.IsHotkeyConflict(keyCode.Value))
                {
                    ShowError(HOTKEY_CONFLICT);
                    return;
                }

                ViewModel?.SetCurrentKey(keyCode.Value);
                _logger.Debug($"鼠标按键已转换: {e.ChangedButton} -> {keyCode.Value}");
                
                // 强制清除焦点
                var focusScope = FocusManager.GetFocusScope(textBox);
                FocusManager.SetFocusedElement(focusScope, null);
                Keyboard.ClearFocus();
                
                // 确保输入框失去焦点
                if (textBox.IsFocused)
                {
                    var parent = textBox.Parent as UIElement;
                    if (parent != null)
                    {
                        parent.Focus();
                    }
                }
            }
        }

        private void ShowKeyboardLayout_Click(object sender, RoutedEventArgs e)
        {
            KeyboardLayoutDrawer.Visibility = Visibility.Visible;
            var animation = new DoubleAnimation
            {
                To = 400,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            DrawerContent.BeginAnimation(Border.WidthProperty, animation);
        }

        private void CloseKeyboardLayout_Click(object sender, RoutedEventArgs e)
        {
            var animation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            animation.Completed += (s, _) => KeyboardLayoutDrawer.Visibility = Visibility.Collapsed;
            DrawerContent.BeginAnimation(Border.WidthProperty, animation);
        }

    }
} 