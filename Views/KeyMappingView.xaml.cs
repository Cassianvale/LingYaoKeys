using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Input;
using WpfApp.ViewModels;
using System.Windows.Media;
using WpfApp.Behaviors;
using WpfApp.Services.Core;
using WpfApp.Services.Utils;
using WpfApp.Services.Models;
using System;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;

// 提供按键映射视图
namespace WpfApp.Views;

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

    // 字典用于存储待确认删除的按钮和定时器
    private readonly Dictionary<System.Windows.Controls.Button, System.Windows.Threading.DispatcherTimer>
        _pendingDeleteButtons = new();

    // 定义删除按钮状态标记，用于识别按钮当前状态
    private static readonly DependencyProperty DeleteConfirmStateProperty =
        DependencyProperty.RegisterAttached(
            "DeleteConfirmState",
            typeof(bool),
            typeof(KeyMappingView),
            new PropertyMetadata(false));

    public KeyMappingView()
    {
        InitializeComponent();
        _viewModel = DataContext as KeyMappingViewModel;

        // 监听 DataContext 变化
        DataContextChanged += KeyMappingView_DataContextChanged;

        // 添加页面失去焦点事件，用于清除所有删除确认状态
        LostFocus += KeyMappingView_LostFocus;

        // 添加音量设置弹出窗口的事件处理
        if (soundSettingsPopup != null)
        {
            soundSettingsPopup.Opened += (s, e) => { _logger.Debug("音量设置弹出窗口已打开"); };

            soundSettingsPopup.Closed += (s, e) => { _logger.Debug("音量设置弹出窗口已关闭"); };
        }
    }

    private KeyMappingViewModel ViewModel => DataContext as KeyMappingViewModel;

    // 添加 DataContext 变化事件处理
    private void KeyMappingView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is KeyMappingViewModel viewModel)
        {
            _hotkeyService = viewModel.GetHotkeyService();
            _logger.Debug("已获取HotkeyService实例");
        }
    }

    // 添加页面焦点变化处理，用于清除所有确认状态
    private void KeyMappingView_LostFocus(object sender, RoutedEventArgs e)
    {
        ClearAllDeleteConfirmStates();
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
        if (key == Key.None) return;

        // 转换并检查按键
        if (TryConvertToLyKeysCode(key, out var lyKeysCode))
        {
            // 检查是否与热键冲突
            if (ViewModel.IsHotkeyConflict(lyKeysCode))
            {
                ShowError(HOTKEY_CONFLICT);
                return;
            }

            ViewModel?.SetCurrentKey(lyKeysCode);
            _logger.Debug($"按键已转换: {key} -> {lyKeysCode}");

            // 显示成功提示
            ShowMessage($"已选择按键: {ViewModel?.CurrentKeyText}");

            // 强制清除焦点
            var focusScope = FocusManager.GetFocusScope(textBox);
            FocusManager.SetFocusedElement(focusScope, null);
            Keyboard.ClearFocus();

            // 确保输入框失去焦点
            if (textBox.IsFocused)
            {
                var parent = textBox.Parent as UIElement;
                if (parent != null) parent.Focus();
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
            if (_hotkeyService != null)
                _hotkeyService.IsInputFocused = true;
    }

    // 处理按键输入框失去焦点
    private void KeyInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
            if (_hotkeyService != null)
                _hotkeyService.IsInputFocused = false;
    }

    // 处理超链接请求导航
    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
            { UseShellExecute = true });
        e.Handled = true;
    }

    // 显示错误信息到状态栏
    private void ShowError(string message)
    {
        ShowMessage(message, true);
    }

    // 显示提示信息到状态栏（通用方法）
    private void ShowMessage(string message, bool isError = false)
    {
        if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            mainViewModel.UpdateStatusMessage(message, isError);
    }

    // 处理热键输入框获得焦点
    private void HotkeyInputBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
            if (_hotkeyService != null)
            {
                _hotkeyService.IsInputFocused = true;
                _logger.Debug($"输入框获得焦点: {textBox.Name}");
            }
    }

    // 处理热键输入框失去焦点
    private void HotkeyInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
            if (_hotkeyService != null)
            {
                _hotkeyService.IsInputFocused = false;
                _logger.Debug($"输入框失去焦点: {textBox.Name}");
            }
    }

    // 处理鼠标按键
    private void HotkeyInputBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            var keyCode = e.ChangedButton switch
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

                // 显示成功提示
                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                    mainViewModel.UpdateStatusMessage($"已选择按键: {ViewModel?.CurrentKeyText}", false);
            }
        }
    }

    // 统一的热键处理方法
    private void HandleHotkeyInput(System.Windows.Controls.TextBox textBox, LyKeysCode keyCode, ModifierKeys modifiers,
        bool isStartHotkey)
    {
        if (textBox == null)
        {
            _logger.Warning("HandleHotkeyInput: textBox is null");
            return;
        }

        // 只过滤修饰键
        if (IsModifierKey(keyCode)) return;

        // 记录热键输入处理
        _logger.Debug($"处理热键输入 - keyCode: {keyCode}, 修饰键: {modifiers}");

        // 使用统一的热键设置方法
        ViewModel?.SetHotkey(keyCode, modifiers);

        // 显示成功提示
        ShowMessage("已设置热键");
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
        _logger.Debug("热键 Keyboard 按下 已触发");
        _logger.Debug($"Key: {e.Key}, SystemKey: {e.SystemKey}, KeyStates: {e.KeyStates}");
        StartHotkeyInput_PreviewKeyDown(sender, e);
    }

    private void StartHotkeyInput_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _logger.Debug("热键 Mouse 按下 已触发");
        _logger.Debug($"ChangedButton: {e.ChangedButton}");
        StartHotkeyInput_PreviewMouseDown(sender, e);
    }

    // 添加滚轮事件处理
    private void StartHotkeyInput_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            e.Handled = true;
            var keyCode = e.Delta > 0 ? LyKeysCode.VK_WHEELUP : LyKeysCode.VK_WHEELDOWN;
            _logger.Debug($"检测到滚轮事件: {keyCode}, Delta: {e.Delta}");
            HandleHotkeyInput(textBox, keyCode, Keyboard.Modifiers, true);
        }
    }

    // 处理热键的鼠标释放
    private void StartHotkeyInput_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        _logger.Debug("热键 Mouse 释放 已触发");
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

            if (key == Key.None) return;

            if (TryConvertToLyKeysCode(key, out var lyKeysCode))
            {
                HandleHotkeyInput(textBox, lyKeysCode, Keyboard.Modifiers, true);
                _logger.Debug($"热键已转换: {key} -> {lyKeysCode}");

                // 显示成功提示
                ShowMessage("已设置热键");
            }
            else
            {
                ShowError(KEY_ERROR);
                _logger.Warning($"无法转换热键: {key}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("StartHotkeyInput_PreviewKeyDown 处理异常", ex);
        }
    }

    // 处理热键的鼠标点击
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

                // 显示成功提示
                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                    mainViewModel.UpdateStatusMessage($"已选择按键: {ViewModel?.CurrentKeyText}", false);
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
                if (int.TryParse(textBox.Text, out var value))
                {
                    if (value <= 0)
                    {
                        // 处理无效值（小于等于0），自动设置为1
                        _logger.Debug($"按键间隔值 {value} 无效，自动设置为1ms");
                        value = 1; // 自动设置为1

                        // 区分是默认间隔输入框还是按键列表中的间隔输入框
                        if (textBox.Name == "txtKeyInterval") // 默认间隔输入框
                        {
                            if (ViewModel != null)
                            {
                                // 更新为1
                                ViewModel.KeyInterval = value;
                                textBox.Text = value.ToString();

                                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel
                                    mainViewModel) mainViewModel.UpdateStatusMessage("按键间隔必须大于0毫秒，已自动设置为1ms", true);
                            }
                        }
                        else // 按键列表中的间隔输入框
                        {
                            // 获取绑定的KeyItem对象
                            if (textBox.DataContext is KeyItem keyItem)
                            {
                                // 更新为1
                                keyItem.KeyInterval = value;
                                textBox.Text = value.ToString();

                                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel
                                    mainViewModel) mainViewModel.UpdateStatusMessage("按键间隔必须大于0毫秒，已自动设置为1ms", true);
                            }
                        }
                    }
                    else
                    {
                        // 值有效，直接更新到ViewModel，不显示自动调整提示
                        // 区分是默认间隔输入框还是按键列表中的间隔输入框
                        if (textBox.Name == "txtKeyInterval") // 默认间隔输入框
                            if (ViewModel != null)
                            {
                                ViewModel.KeyInterval = value;
                                _logger.Debug($"更新默认间隔值为: {value}ms");
                            }
                        // 按键列表中的间隔输入框由数据绑定自动更新，不需要额外处理
                    }
                }
                else
                {
                    // 输入的不是有效数字
                    _logger.Debug("输入的不是有效数字");

                    // 区分是默认间隔输入框还是按键列表中的间隔输入框
                    if (textBox.Name == "txtKeyInterval") // 默认间隔输入框
                    {
                        if (ViewModel != null)
                        {
                            textBox.Text = ViewModel.KeyInterval.ToString(); // 恢复为当前值
                            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel
                                mainViewModel) mainViewModel.UpdateStatusMessage("请输入有效的数字", true);
                        }
                    }
                    else // 按键列表中的间隔输入框
                    {
                        // 获取绑定的KeyItem对象
                        if (textBox.DataContext is KeyItem keyItem)
                        {
                            textBox.Text = keyItem.KeyInterval.ToString(); // 恢复为当前值
                            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel
                                mainViewModel) mainViewModel.UpdateStatusMessage("请输入有效的数字", true);
                        }
                    }
                }
            }
            else
            {
                // 输入框为空，恢复为当前值
                // 区分是默认间隔输入框还是按键列表中的间隔输入框
                if (textBox.Name == "txtKeyInterval") // 默认间隔输入框
                {
                    if (ViewModel != null) textBox.Text = ViewModel.KeyInterval.ToString();
                }
                else // 按键列表中的间隔输入框
                {
                    // 获取绑定的KeyItem对象
                    if (textBox.DataContext is KeyItem keyItem) textBox.Text = keyItem.KeyInterval.ToString();
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
            // 如果输入框有焦点，则不处理热键
            if (_hotkeyService != null && _hotkeyService.IsInputFocused)
            {
                _logger.Debug("输入框有焦点，忽略热键触发");
                return;
            }

            // 检查热键总开关是否开启
            if (!ViewModel.IsHotkeyControlEnabled)
            {
                _logger.Debug("热键总开关已关闭，忽略热键触发");
                return;
            }

            if (ViewModel.SelectedKeyMode == 0) // 顺序模式
            {
                _logger.Debug($"顺序模式 - 按键{(isKeyDown ? "按下" : "释放")}");
                if (isKeyDown) // 按下时启动
                    ViewModel.StartKeyMapping();
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

            // 如果输入框有焦点，则不处理热键
            if (_hotkeyService != null && _hotkeyService.IsInputFocused)
            {
                _logger.Debug("输入框有焦点，忽略热键触发");
                return;
            }

            // 检查热键总开关是否开启
            if (!ViewModel.IsHotkeyControlEnabled)
            {
                _logger.Debug("热键总开关已关闭，忽略热键触发");
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
            var scrollBar =
                FindParent<System.Windows.Controls.Primitives.ScrollBar>(e.OriginalSource as DependencyObject);
            if (scrollBar != null)
                // 如果点击在滚动条上，不处理事件
                return;

            // 检查点击是否在ListBox的空白区域
            var hitTest = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
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

        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && !(parent is T)) parent = VisualTreeHelper.GetParent(parent);

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
                if (pageListBox != null) ClearListBoxSelection(pageListBox);
            }
        }
    }

    // 添加一个通用的清除方法
    private void ClearListBoxSelection(System.Windows.Controls.ListBox listBox)
    {
        listBox.SelectedItem = null;

        // 清除所有项的拖拽高亮显示
        foreach (var item in listBox.Items)
            if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                DragDropProperties.SetIsDragTarget(listBoxItem, false);
    }

    // 添加FindChild辅助方法
    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
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
        // 使用FindName获取Popup控件引用
        var helpPopup = FindName("helpPopup") as Popup;

        // 切换帮助浮窗的显示状态
        if (helpPopup != null) helpPopup.IsOpen = !helpPopup.IsOpen;
    }

    private void ModeHelp_Click(object sender, RoutedEventArgs e)
    {
        // 使用FindName获取Popup控件引用
        var modeHelpPopup = FindName("modeHelpPopup") as Popup;

        // 切换帮助浮窗的显示状态
        if (modeHelpPopup != null) modeHelpPopup.IsOpen = !modeHelpPopup.IsOpen;
    }

    private void VolumeHelp_Click(object sender, RoutedEventArgs e)
    {
        // 使用FindName获取Popup控件引用
        var volumeHelpPopup = FindName("volumeHelpPopup") as Popup;

        // 切换音量帮助浮窗的显示状态
        if (volumeHelpPopup != null) volumeHelpPopup.IsOpen = !volumeHelpPopup.IsOpen;
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

            // 显示成功提示
            ShowMessage($"已选择按键: {ViewModel?.CurrentKeyText}");

            // 强制清除焦点
            var focusScope = FocusManager.GetFocusScope(textBox);
            FocusManager.SetFocusedElement(focusScope, null);
            Keyboard.ClearFocus();

            // 确保输入框失去焦点
            if (textBox.IsFocused)
            {
                var parent = textBox.Parent as UIElement;
                if (parent != null) parent.Focus();
            }
        }
    }

    private void GetWindowHandle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 创建一个新的窗口来显示所有可见窗口的句柄
            var windowHandleDialog = new WindowHandleDialog();
            windowHandleDialog.Owner = Window.GetWindow(this);
            windowHandleDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (windowHandleDialog.ShowDialog() == true)
            {
                // 获取选中的窗口信息
                var handle = windowHandleDialog.SelectedHandle;
                var title = windowHandleDialog.SelectedTitle;
                var className = windowHandleDialog.SelectedClassName;
                var processName = windowHandleDialog.SelectedProcessName;

                // 更新ViewModel中的窗口信息
                ViewModel.UpdateSelectedWindow(handle, title, className, processName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("获取窗口句柄时发生异常", ex);
            ShowError("获取窗口句柄失败，请查看日志");
        }
    }

    private void ClearWindowHandle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
            return;

        try
        {
            // 获取按钮是否已处于确认状态
            var isConfirmState = (bool)button.GetValue(DeleteConfirmStateProperty);

            if (isConfirmState)
            {
                // 已经是确认状态，执行清除窗口句柄操作
                try
                {
                    // 停止并移除定时器
                    if (_pendingDeleteButtons.TryGetValue(button, out var timer))
                    {
                        timer.Stop();
                        _pendingDeleteButtons.Remove(button);
                    }

                    // 重置按钮状态
                    button.SetValue(DeleteConfirmStateProperty, false);

                    // 清除窗口句柄
                    ViewModel.ClearSelectedWindow();
                    _logger.Debug("已清除窗口句柄");
                    
                    // 恢复按钮为原始状态
                    ResetDeleteButton(button);
                }
                catch (Exception ex)
                {
                    _logger.Error("清除窗口句柄时发生异常", ex);
                    ShowError("清除窗口句柄失败，请查看日志");

                    // 恢复按钮原始状态
                    ResetDeleteButton(button);
                }
            }
            else
            {
                // 清除其他所有按钮的确认状态
                ClearAllDeleteConfirmStates();

                // 将按钮设置为确认状态
                button.SetValue(DeleteConfirmStateProperty, true);
                ConvertToConfirmButton(button);

                // 创建3秒定时器
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };

                timer.Tick += (s, args) =>
                {
                    // 3秒后恢复按钮原始状态
                    timer.Stop();
                    if (_pendingDeleteButtons.ContainsKey(button)) _pendingDeleteButtons.Remove(button);
                    button.SetValue(DeleteConfirmStateProperty, false);
                    ResetDeleteButton(button);
                };

                // 添加到字典并启动定时器
                if (_pendingDeleteButtons.ContainsKey(button)) _pendingDeleteButtons[button].Stop();
                _pendingDeleteButtons[button] = timer;
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理清除窗口句柄按钮点击事件时发生异常", ex);
            // 确保按钮恢复原状
            if (sender is System.Windows.Controls.Button btn)
                ResetDeleteButton(btn);
        }
    }

    /// <summary>
    /// 将按钮转换为确认删除状态（红色X）
    /// </summary>
    private void ConvertToConfirmButton(System.Windows.Controls.Button button)
    {
        if (button == null) return;

        try
        {
            // 查找按钮内的Path元素
            if (button.Content is Path path)
            {
                // 保存原始图标数据
                button.SetValue(DeleteConfirmStateProperty, true);

                // 记录原始Path数据，用于恢复
                path.Tag = path.Data;

                // 设置为X图标
                path.Data = Geometry.Parse(
                    "M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41L15.59,7Z");

                // 设置为红色
                path.Fill = new SolidColorBrush(Colors.Red);

                // 修改按钮背景为淡红色，提供更明显的视觉提示
                button.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 255, 0, 0));
                button.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 0, 0));

                // 添加动画效果增强用户体验
                var animation = new System.Windows.Media.Animation.ColorAnimation
                {
                    To = System.Windows.Media.Color.FromArgb(40, 255, 0, 0),
                    Duration = TimeSpan.FromMilliseconds(200),
                    AutoReverse = true,
                    RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(3)
                };

                var brush = button.Background as SolidColorBrush;
                if (brush != null) brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);

                // 更改按钮提示为确认删除
                if (button.ToolTip is System.Windows.Controls.ToolTip toolTip && toolTip.Content is TextBlock textBlock)
                    textBlock.Text = "点击确认删除";
            }
        }
        catch (Exception ex)
        {
            _logger.Error("转换删除按钮到确认状态时发生异常", ex);
        }
    }

    /// <summary>
    /// 重置按钮到原始状态
    /// </summary>
    private void ResetDeleteButton(System.Windows.Controls.Button button)
    {
        if (button == null) return;

        try
        {
            // 清除确认状态标记
            button.SetValue(DeleteConfirmStateProperty, false);

            // 查找按钮内的Path元素
            if (button.Content is Path path)
            {
                // 如果有保存的原始数据，则恢复
                if (path.Tag is Geometry originalGeometry)
                {
                    // 恢复原始图标数据
                    path.Data = originalGeometry;
                    path.Tag = null;
                }

                // 恢复原始颜色
                path.Fill = new SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"));

                // 恢复原始背景
                button.Background = new SolidColorBrush(Colors.Transparent);
                button.BorderBrush = new SolidColorBrush(Colors.Transparent);

                // 恢复原始提示文本
                if (button.ToolTip is System.Windows.Controls.ToolTip toolTip && toolTip.Content is TextBlock textBlock)
                    textBlock.Text = "删除此按键";
            }

            // 确保移除定时器
            if (_pendingDeleteButtons.TryGetValue(button, out var timer))
            {
                timer.Stop();
                _pendingDeleteButtons.Remove(button);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("重置删除按钮到原始状态时发生异常", ex);
        }
    }

    /// <summary>
    /// 清除所有按钮的确认删除状态
    /// </summary>
    private void ClearAllDeleteConfirmStates()
    {
        try
        {
            // 创建一个临时列表存储所有按钮，避免在遍历过程中修改集合
            var buttonsToReset = new List<System.Windows.Controls.Button>(_pendingDeleteButtons.Keys);

            foreach (var button in buttonsToReset) ResetDeleteButton(button);

            // 清空字典
            _pendingDeleteButtons.Clear();
        }
        catch (Exception ex)
        {
            _logger.Error("清除所有删除确认状态时发生异常", ex);
        }
    }

    /// <summary>
    /// 音量设置按钮点击事件处理
    /// </summary>
    private void SoundSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (soundSettingsPopup == null)
            {
                _logger.Warning("音量设置弹出窗口未初始化");
                return;
            }

            // 切换弹出窗口的显示状态
            soundSettingsPopup.IsOpen = !soundSettingsPopup.IsOpen;
            _logger.Debug($"音量设置弹出窗口状态: {soundSettingsPopup.IsOpen}");

            // 如果弹出窗口打开，设置焦点到音量滑块
            if (soundSettingsPopup.IsOpen && volumeSlider != null) volumeSlider.Focus();
        }
        catch (Exception ex)
        {
            _logger.Error("处理音量设置按钮点击事件时发生异常", ex);
        }
    }

    /// <summary>
    /// 删除按键按钮点击事件处理
    /// </summary>
    private void DeleteKeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
            return;

        try
        {
            // 获取按钮是否已处于确认状态
            var isConfirmState = (bool)button.GetValue(DeleteConfirmStateProperty);
            var keyItem = button.Tag as KeyItem;

            // 检查KeyItem是否有效
            if (keyItem == null)
            {
                _logger.Error("按钮Tag不是KeyItem类型或为空");
                return;
            }

            if (isConfirmState)
            {
                // 已经是确认状态，执行删除操作
                try
                {
                    // 停止并移除定时器
                    if (_pendingDeleteButtons.TryGetValue(button, out var timer))
                    {
                        timer.Stop();
                        _pendingDeleteButtons.Remove(button);
                    }

                    // 重置按钮状态
                    button.SetValue(DeleteConfirmStateProperty, false);

                    // 删除按键
                    ViewModel.DeleteKey(keyItem);
                    _logger.Debug($"已删除按键: {keyItem.DisplayName}");
                }
                catch (Exception ex)
                {
                    _logger.Error("删除按键时发生异常", ex);
                    System.Windows.MessageBox.Show($"删除按键失败: {ex.Message}", "错误", MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    // 恢复按钮原始状态
                    ResetDeleteButton(button);
                }
            }
            else
            {
                // 清除其他所有按钮的确认状态
                ClearAllDeleteConfirmStates();

                // 将按钮设置为确认状态
                button.SetValue(DeleteConfirmStateProperty, true);
                ConvertToConfirmButton(button);

                // 创建3秒定时器
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };

                timer.Tick += (s, args) =>
                {
                    // 3秒后恢复按钮原始状态
                    timer.Stop();
                    if (_pendingDeleteButtons.ContainsKey(button)) _pendingDeleteButtons.Remove(button);
                    button.SetValue(DeleteConfirmStateProperty, false);
                    ResetDeleteButton(button);
                };

                // 添加到字典并启动定时器
                if (_pendingDeleteButtons.ContainsKey(button)) _pendingDeleteButtons[button].Stop();
                _pendingDeleteButtons[button] = timer;
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            _logger.Error("处理删除按钮点击事件时发生异常", ex);
            // 确保按钮恢复原状
            ResetDeleteButton(button);
        }
    }
}