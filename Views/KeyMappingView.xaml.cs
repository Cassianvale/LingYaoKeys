using System;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Text;
using System.Windows;
using WpfApp.ViewModels;
using WpfApp.Services;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using WpfApp.Styles;
using WpfApp.Behaviors;

// 提供按键映射视图
namespace WpfApp.Views
{
    public partial class KeyMappingView : Page
    {   
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private const string KEY_ERROR = "无法识别按键，请检查输入法是否关闭";
        private const string HOTKEY_CONFLICT = "无法设置与热键相同的按键";
        private HotkeyService? _hotkeyService;

        private KeyMappingViewModel ViewModel => (KeyMappingViewModel)DataContext;

        public KeyMappingView()
        {
            InitializeComponent();
            
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

        private void KeyInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            
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
            if (TryConvertToDDKeyCode(key, out DDKeyCode ddKeyCode))
            {
                // 检查是否与热键冲突
                if (ViewModel.IsHotkeyConflict(ddKeyCode))
                {
                    ShowError(HOTKEY_CONFLICT);
                    return;
                }

                ViewModel?.SetCurrentKey(ddKeyCode);
                _logger.Debug("KeyInput", $"按键已转换: {key} -> {ddKeyCode}");
            }
            else
            {
                ShowError(KEY_ERROR);
                _logger.Warning("KeyInput", $"无法转换按键: {key}");
            }
        }

        // 将WPF的Key映射到DDKeyCode
        private bool TryConvertToDDKeyCode(Key key, out DDKeyCode ddKeyCode)
        {
            // 将WPF的Key映射到DDKeyCode
            ddKeyCode = key switch
            {
                // 字母键
                Key.A => DDKeyCode.A,
                Key.B => DDKeyCode.B,
                Key.C => DDKeyCode.C,
                Key.D => DDKeyCode.D,
                Key.E => DDKeyCode.E,
                Key.F => DDKeyCode.F,
                Key.G => DDKeyCode.G,
                Key.H => DDKeyCode.H,
                Key.I => DDKeyCode.I,
                Key.J => DDKeyCode.J,
                Key.K => DDKeyCode.K,
                Key.L => DDKeyCode.L,
                Key.M => DDKeyCode.M,
                Key.N => DDKeyCode.N,
                Key.O => DDKeyCode.O,
                Key.P => DDKeyCode.P,
                Key.Q => DDKeyCode.Q,
                Key.R => DDKeyCode.R,
                Key.S => DDKeyCode.S,
                Key.T => DDKeyCode.T,
                Key.U => DDKeyCode.U,
                Key.V => DDKeyCode.V,
                Key.W => DDKeyCode.W,
                Key.X => DDKeyCode.X,
                Key.Y => DDKeyCode.Y,
                Key.Z => DDKeyCode.Z,

                // 数字键
                Key.D0 => DDKeyCode.NUM_0,
                Key.D1 => DDKeyCode.NUM_1,
                Key.D2 => DDKeyCode.NUM_2,
                Key.D3 => DDKeyCode.NUM_3,
                Key.D4 => DDKeyCode.NUM_4,
                Key.D5 => DDKeyCode.NUM_5,
                Key.D6 => DDKeyCode.NUM_6,
                Key.D7 => DDKeyCode.NUM_7,
                Key.D8 => DDKeyCode.NUM_8,
                Key.D9 => DDKeyCode.NUM_9,

                // 功能键
                Key.F1 => DDKeyCode.F1,
                Key.F2 => DDKeyCode.F2,
                Key.F3 => DDKeyCode.F3,
                Key.F4 => DDKeyCode.F4,
                Key.F5 => DDKeyCode.F5,
                Key.F6 => DDKeyCode.F6,
                Key.F7 => DDKeyCode.F7,
                Key.F8 => DDKeyCode.F8,
                Key.F9 => DDKeyCode.F9,
                Key.F10 => DDKeyCode.F10,
                Key.F11 => DDKeyCode.F11,
                Key.F12 => DDKeyCode.F12,

                // 特殊键
                Key.Escape => DDKeyCode.ESC,
                Key.Tab => DDKeyCode.TAB,
                Key.CapsLock => DDKeyCode.CAPS_LOCK,
                Key.LeftShift => DDKeyCode.LEFT_SHIFT,
                Key.RightShift => DDKeyCode.RIGHT_SHIFT,
                Key.LeftCtrl => DDKeyCode.LEFT_CTRL,
                Key.RightCtrl => DDKeyCode.RIGHT_CTRL,
                Key.LeftAlt => DDKeyCode.LEFT_ALT,
                Key.RightAlt => DDKeyCode.RIGHT_ALT,
                Key.Space => DDKeyCode.SPACE,
                Key.Enter => DDKeyCode.ENTER,
                Key.Back => DDKeyCode.BACKSPACE,

                // 符号键
                Key.OemTilde => DDKeyCode.TILDE,
                Key.OemMinus => DDKeyCode.MINUS,
                Key.OemPlus => DDKeyCode.EQUALS,
                Key.OemOpenBrackets => DDKeyCode.LEFT_BRACKET,
                Key.OemCloseBrackets => DDKeyCode.RIGHT_BRACKET,
                Key.OemSemicolon => DDKeyCode.SEMICOLON,
                Key.OemQuotes => DDKeyCode.QUOTE,
                Key.OemComma => DDKeyCode.COMMA,
                Key.OemPeriod => DDKeyCode.PERIOD,
                Key.OemQuestion => DDKeyCode.SLASH,
                Key.OemBackslash => DDKeyCode.BACKSLASH,

                // 添加方向键映射
                Key.Up => DDKeyCode.ARROW_UP,
                Key.Down => DDKeyCode.ARROW_DOWN,
                Key.Left => DDKeyCode.ARROW_LEFT,
                Key.Right => DDKeyCode.ARROW_RIGHT,

                _ => DDKeyCode.ESC
            };

            // 修改返回逻辑，添加方向键判断
            return key == Key.Escape || 
                   (key >= Key.Left && key <= Key.Down) || // 方向键
                   ddKeyCode != DDKeyCode.ESC;
        }

        // 处理按键输入框获得焦点
        private void KeyInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
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
            if (sender is TextBox textBox)
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
            if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.UpdateStatusMessage(message, true);
            }
        }

        // 处理热键输入框获得焦点
        private void HotkeyInputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
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
            if (sender is TextBox textBox)
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
            if (sender is TextBox textBox)
            {
                DDKeyCode keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => DDKeyCode.MBUTTON,
                    MouseButton.XButton1 => DDKeyCode.XBUTTON1,
                    MouseButton.XButton2 => DDKeyCode.XBUTTON2,
                    _ => DDKeyCode.ESC
                };

                if (keyCode != DDKeyCode.ESC)
                {
                    ViewModel.SetCurrentKey(keyCode);
                    e.Handled = true;
                }
            }
        }

        // 统一的热键处理方法
        private void HandleHotkeyInput(TextBox textBox, DDKeyCode keyCode, ModifierKeys modifiers, bool isStartHotkey)
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
        private bool IsModifierKey(DDKeyCode keyCode)
        {
            return keyCode == DDKeyCode.LEFT_CTRL 
                || keyCode == DDKeyCode.RIGHT_CTRL
                || keyCode == DDKeyCode.LEFT_ALT 
                || keyCode == DDKeyCode.RIGHT_ALT
                || keyCode == DDKeyCode.LEFT_SHIFT 
                || keyCode == DDKeyCode.RIGHT_SHIFT;
        }

        // 处理开始热键
        private void StartHotkeyInput_KeyDown(object sender, KeyEventArgs e)
        {
            _logger.Debug("StartHotkeyInput_KeyDown 已触发");
            _logger.Debug($"Key: {e.Key}, SystemKey: {e.SystemKey}, KeyStates: {e.KeyStates}");
            StartHotkeyInput_PreviewKeyDown(sender, e);
        }

        private void StartHotkeyInput_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _logger.Debug("StartHotkeyInput_MouseDown 已触发");
            _logger.Debug($"ChangedButton: {e.ChangedButton}");
            StartHotkeyInput_PreviewMouseDown(sender, e);
        }

        // 处理开始热键的鼠标释放
        private void StartHotkeyInput_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _logger.Debug("StartHotkeyInput_PreviewMouseUp 已触发");
            _logger.Debug($"ChangedButton: {e.ChangedButton}");
        }

        private void StartHotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;

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

                if (TryConvertToDDKeyCode(key, out DDKeyCode ddKeyCode))
                {
                    HandleHotkeyInput(textBox, ddKeyCode, Keyboard.Modifiers, true);
                    _logger.Debug($"开始热键已转换: {key} -> {ddKeyCode}");
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
        private void StopHotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox) return;
            
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
                
                if (TryConvertToDDKeyCode(key, out DDKeyCode ddKeyCode))
                {
                    if (IsModifierKey(ddKeyCode))
                    {
                        return;
                    }
                    HandleHotkeyInput(textBox, ddKeyCode, modifiers, false);
                    _logger.Debug($"停止热键已转换: {key} -> {ddKeyCode}");
                }
                else
                {
                    ShowError(KEY_ERROR);
                    _logger.Warning($"无法转换停止热键: {key}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("StopHotkeyInput_PreviewKeyDown 处理异常", ex);
            }
        }

        // 处理开始热键的鼠标点击
        private void StartHotkeyInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                DDKeyCode? keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => DDKeyCode.MBUTTON,
                    MouseButton.XButton1 => DDKeyCode.XBUTTON1,
                    MouseButton.XButton2 => DDKeyCode.XBUTTON2,
                    _ => null // 对于左键和右键不处理，让输入框正常获取焦点以接收键盘输入
                };

                if (keyCode.HasValue)
                {
                    _logger.Debug($"检测到鼠标按键: {keyCode.Value}");
                    HandleHotkeyInput(textBox, keyCode.Value, Keyboard.Modifiers, true);
                    e.Handled = true; // 阻止事件继续传播
                }
            }
        }

        // 处理停止热键的鼠标点击
        private void StopHotkeyInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                DDKeyCode? keyCode = e.ChangedButton switch
                {
                    MouseButton.Middle => DDKeyCode.MBUTTON,
                    MouseButton.XButton1 => DDKeyCode.XBUTTON1,
                    MouseButton.XButton2 => DDKeyCode.XBUTTON2,
                    _ => null // 对于左键和右键，不处理，让输入框正常获取焦点以接收键盘输入
                };

                if (keyCode.HasValue)
                {
                    _logger.Debug($"检测到鼠标按键: {keyCode.Value}");
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
            if (sender is TextBox textBox)
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
                                
                                if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
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
                                    if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
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
                            if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
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
            if (sender is ListBox listBox)
            {
                // 检查点击是否在ListBox的空白区域
                HitTestResult hitTest = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
                if (hitTest == null || 
                    (hitTest.VisualHit != null && 
                     FindParent<ListBoxItem>(hitTest.VisualHit as DependencyObject) == null))
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
                var listBox = FindParent<ListBox>(depObj);
                if (listBox == null) // 点击在ListBox外部
                {
                    // 查找页面中的ListBox
                    var pageListBox = FindChild<ListBox>((sender as Page)!);
                    if (pageListBox != null)
                    {
                        ClearListBoxSelection(pageListBox);
                    }
                }
            }
        }

        // 添加一个通用的清除方法
        private void ClearListBoxSelection(ListBox listBox)
        {
            listBox.SelectedItem = null;

            // 清除所有项的拖拽高亮显示
            foreach (var item in listBox.Items)
            {
                if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
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

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
} 
