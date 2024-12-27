using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using System.Collections.Generic;
using System.Threading;
using WpfApp.ViewModels;

// 提供快捷键服务
namespace WpfApp.Services
{
    public class HotkeyService : IDisposable
    {
        // Win32 API 函数
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // 定义低级鼠标钩子回调函数
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        // 添加常量
        private const int WH_MOUSE_LL = 14;  // 低级鼠标钩子
        private const int WM_HOTKEY = 0x0312;  // 热键消息
        private const int WM_XBUTTONDOWN = 0x020B; // 鼠标左键按下消息
        private const int WM_MBUTTONDOWN = 0x0207; // 鼠标中键按下消息

        // 添加Windows消息常量
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_MBUTTONUP = 0x0208;

        private IntPtr _windowHandle;
        private Window _mainWindow;
        
        private HwndSource? _source;
        private bool _isRegistered;

        public event Action? StartHotkeyPressed;
        public event Action? StartHotkeyReleased;
        public event Action? StopHotkeyPressed;
        public event Action? SequenceModeStarted;
        public event Action? SequenceModeStopped;
        public event Action<DDKeyCode>? KeyTriggered;

        private const int START_HOTKEY_ID = 1;
        private const int STOP_HOTKEY_ID = 2;

        private bool _isSequenceRunning;
        private DDDriverService _ddDriverService;

        private List<DDKeyCode> _keyList = new List<DDKeyCode>();
        private CancellationTokenSource? _sequenceCts;

        // 添加字段保存虚拟键码
        private int _startVirtualKey;
        private int _stopVirtualKey;
        private DDKeyCode? _pendingStartKey;
        private DDKeyCode? _pendingStopKey;
        private ModifierKeys _pendingStartMods;
        private ModifierKeys _pendingStopMods;
        private bool _isWindowInitialized;
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly MainViewModel _mainViewModel;
        private bool _isInputFocused;
        
        // 输入框获得焦点的处
        public bool IsInputFocused
        {
            get => _isInputFocused;
            set
            {
                if (_isInputFocused != value)
                {
                    _isInputFocused = value;

                    // 如果获得焦点，临时取消注册热键
                    if (value)
                    {
                        TemporarilyUnregisterHotkeys();
                    }
                    else
                    {
                        RestoreHotkeys();
                    }
                }
            }
        }

        // 添加热键状态枚举
        private enum HotkeyMode
        {
            Different,  // 不同热键模式
            Same       // 相同热键模式
        }

        // 修改状态追踪字段
        private HotkeyMode _currentMode = HotkeyMode.Different;
        private bool _isStarted = false;
        private bool _startHotkeyRegistered = false;
        private bool _stopHotkeyRegistered = false;
        private uint _lastStartModifiers = 0;
        private uint _lastStopModifiers = 0;


        private IntPtr _mouseHookHandle;
        private LowLevelMouseProc? _mouseProc;

        // 修改防抖动相关字段
        private const int MIN_TOGGLE_INTERVAL = 300; // 启动/停止切换的最小间隔(秒)
        private const int KEY_RELEASE_TIMEOUT = 50;  // 按键释放检测超时(毫秒)
        private DateTime _lastToggleTime = DateTime.MinValue;
        private DateTime _lastKeyDownTime = DateTime.MinValue;
        private bool _isKeyHeld = false;

        private DDKeyCode? _sequenceModeStartKey;
        private DDKeyCode? _sequenceModeStopKey;
        private ModifierKeys _sequenceModeStartMods;
        private ModifierKeys _sequenceModeStopMods;
        private DDKeyCode? _holdModeKey;
        private ModifierKeys _holdModeMods;

        private volatile bool _isDisposed;
        private readonly object _disposeLock = new object();

        // 添加按键状态检查相关字段
        private CancellationTokenSource? _keyCheckCts;
        private const int KEY_CHECK_INTERVAL = 50; // 按键状态检查间隔(毫秒)

        // 添加键盘钩子相关常量和委托
        private const int WH_KEYBOARD_LL = 13;
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private IntPtr _keyboardHookHandle;
        private LowLevelKeyboardProc? _keyboardProc;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private readonly object _holdModeLock = new object();
        private volatile bool _isHoldModeRunning = false;

        // 构造函数
        public HotkeyService(Window mainWindow, DDDriverService ddDriverService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _ddDriverService = ddDriverService ?? throw new ArgumentNullException(nameof(ddDriverService));
            _mainViewModel = mainWindow.DataContext as MainViewModel ?? 
                throw new ArgumentException("Window.DataContext must be of type MainViewModel", nameof(mainWindow));
            
            // 订阅模式切换事件
            _ddDriverService.ModeSwitched += OnModeSwitched;
            
            // 1. 从配置中加载按键模式
            var config = AppConfigService.Config;
            bool isSequenceMode = config.keyMode == 0;

            // 2. 设置驱动服务的初始模式
            _ddDriverService.IsSequenceMode = isSequenceMode;

            // 3. 根据模式载不同的热键配置
            if (isSequenceMode)
            {
                // 加载顺序模式的热键配置，注册启动键和停止键
                _sequenceModeStartKey = config.startKey;
                _sequenceModeStopKey = config.stopKey;
                _sequenceModeStartMods = config.startMods;
                _sequenceModeStopMods = config.stopMods;
                _logger.InitLog("初始化为顺序模式", $"启动键: {config.startKey}, 停止键: {config.stopKey}");
            }
            else
            {
                // 加载按压模式的热键配置，只注册启动键
                _holdModeKey = config.startKey;
                _holdModeMods = config.startMods;
                _logger.InitLog("初始化为按压模式", $"启动键: {config.startKey}");
            }
            
            // 3. 确保在窗口初始化后自动注册热键
            _mainWindow.SourceInitialized += (s, e) =>
            {
                try
                {
                    // 1. 获取主程序窗口句柄
                    _windowHandle = new WindowInteropHelper(_mainWindow).Handle;
                    // 2. 获取窗口句柄的HwndSource对象
                    _source = HwndSource.FromHwnd(_windowHandle);
                    if (_source != null)
                    {
                        _source.AddHook(WndProc);
                        _isWindowInitialized = true;
                        _logger.InitLog("窗口初始化完成", $"获取句柄: {_windowHandle:X}");
                        
                        // 注册待处理的热键
                        if (_pendingStartKey.HasValue)
                        {
                            RegisterStartHotkeyInternal(_pendingStartKey.Value, _pendingStartMods);
                            
                            if (_currentMode == HotkeyMode.Different && _pendingStopKey.HasValue)
                            {
                                RegisterStopHotkey(_pendingStopKey.Value, _pendingStopMods);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("窗口初始化处理异常", ex);
                }
            };

            // 4. 添加全局鼠标钩子
            _mouseProc = MouseHookCallback;
            _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName), 0);

            // 5. 添加全局键盘钩子
            _keyboardProc = KeyboardHookCallback;
            _keyboardHookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName), 0);
            
            // 6. 窗口关闭时清理资源
            _mainWindow.Closed += (s, e) =>
            {
                // 移除鼠标钩子
                if (_mouseHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookHandle);
                    _mouseHookHandle = IntPtr.Zero;
                }

                // 移除键盘钩子
                if (_keyboardHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_keyboardHookHandle);
                    _keyboardHookHandle = IntPtr.Zero;
                }

                // 移除模式切换事件
                _ddDriverService.ModeSwitched -= OnModeSwitched;
                // 释放资源
                Dispose();
            };

            // 7. 检查是否以管理员身份运行
            if (!IsRunAsAdministrator())
            {
                System.Windows.MessageBox.Show("请以管理员身份运行程序以使用热键功能", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 打开程序时检查程序是否以管理员身份打开
        private bool IsRunAsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        // 注册快捷键
        public bool RegisterHotKey(Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            // 条件1：检查是否已注册
            if (_isRegistered)
            {
                _logger.Debug("热键已经注册，请勿重复注册");
                return false;
            }
            
            // 条件2：检查窗口句柄是否有效
            if (_windowHandle == IntPtr.Zero)
            {
                _logger.Error("无效的窗口句柄，无法注册热键");
                return false;
            }

            // 条3：检查是否已注册
            try
            {
                // 注册热键时使用主窗口句柄，这样热键触发时消息会发送到主窗口
                _isRegistered = RegisterHotKey(
                    _windowHandle,  // 使用主窗口句柄
                    START_HOTKEY_ID,    // 热键的ID
                    (uint)modifiers,    // 修饰键
                    (uint)KeyInterop.VirtualKeyFromKey(key)    // 虚拟键码
                );

                // 断调用Win32API的RegisterHotKey函数返回，如果注册失败显示错误信息
                if (!_isRegistered)
                {
                    _mainViewModel.UpdateStatusMessage("热键注册失败，可能被其他程序占用", true);
                }
                _logger.Debug($"热键注册成功，热键ID: {START_HOTKEY_ID}");
                return _isRegistered;
            }
            catch (Exception ex)
            {
                _logger.Error("热键注册异常", ex);
                _mainViewModel.UpdateStatusMessage($"热键注册异常: {ex.Message}", true);
                return false;
            }
        }

        // 取消注册快捷键
        public void UnregisterHotKey()
        {
            if (_isDisposed) return;
            
            // 1. 检查是否已注册
            if (!_isRegistered)
            {
                return;
            }
            
            // 2. 检查窗口句柄是否有效
            if (_windowHandle == IntPtr.Zero)
            {
                _logger.Error("窗口句柄无效，无法取消注册热键");
                return;
            }

            // 3. 取消热键注册
            try
            {   
                // 调用Win32APIUnregisterHotKey函数取消注册热键
                UnregisterHotKey(
                    _windowHandle, // 使用主窗口句柄
                    START_HOTKEY_ID // 热键的ID
                    );
                _isRegistered = false;  // 将热键注册状态设置为false
                _logger.Debug("热键注销成功");
            }
            catch (Exception ex)
            {
                _logger.Error($"热键注销异常: {ex.Message}");
            }
        }

        // 处理热键事件
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                if (_isInputFocused)
                {
                    return IntPtr.Zero;
                }

                // 处理热键消息
                switch (msg)
                {
                    case WM_HOTKEY:
                        HandleHotkeyMessage(wParam.ToInt32());
                        handled = true;
                        break;

                    case WM_KEYUP:
                    case WM_SYSKEYUP:
                        if (!_ddDriverService.IsSequenceMode)
                        {
                            int vkCode = wParam.ToInt32();
                            _logger.Debug($"收到按键释放消息 - VK: 0x{vkCode:X}, 当前热键VK: 0x{_startVirtualKey:X}");
                            
                            if (vkCode == _startVirtualKey)
                            {
                                _logger.Debug("检测到启动键释放");
                                HandleHoldModeKeyRelease();
                                handled = true;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("WndProc处理异常", ex);
            }
            
            return IntPtr.Zero;
        }

        // 释放资源
        public async Task DisposeAsync()
        {
            if (_isDisposed) return;

            lock (_disposeLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;
            }

            try
            {
                _logger.Debug("开始释放热键服务资源");

                // 1. 停止所有运行中的序列
                StopSequence();

                // 2. 取消注册所有热键
                if (_startHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
                    _startHotkeyRegistered = false;
                }
                if (_stopHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
                    _stopHotkeyRegistered = false;
                }

                // 3. 移除钩子
                if (_mouseHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookHandle);
                    _mouseHookHandle = IntPtr.Zero;
                }
                if (_keyboardHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_keyboardHookHandle);
                    _keyboardHookHandle = IntPtr.Zero;
                }

                // 4. 移除窗口钩子
                if (_source != null)
                {
                    _source.RemoveHook(WndProc);
                    _source = null;
                }

                // 5. 清理事件订阅
                StartHotkeyPressed = null;
                StartHotkeyReleased = null;
                StopHotkeyPressed = null;
                SequenceModeStarted = null;
                SequenceModeStopped = null;
                KeyTriggered = null;

                // 6. 重置状态
                _isStarted = false;
                _isSequenceRunning = false;
                _isHoldModeRunning = false;
                _windowHandle = IntPtr.Zero;
            }
            catch (Exception ex)
            {
                _logger.Error("释放热键服务资源时发生异常", ex);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                DisposeAsync().Wait();
            }
            catch (Exception ex)
            {
                _logger.Error("Dispose过程中发生异常", ex);
            }
            GC.SuppressFinalize(this);
        }

        // 修改注册开始热键的方法
        private bool RegisterStartHotkeyInternal(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            try
            {
                _logger.Debug($"开始注册开始热键 - " +
                    $"键码: {ddKeyCode}, " +
                    $"修饰键: {modifiers}, " +
                    $"停止键: {_pendingStopKey}, " +
                    $"当前状态: 已启动({_isStarted}), 序列运行({_isSequenceRunning})");

                // 1. 检查窗口是否初始化
                if (!_isWindowInitialized)
                {
                    _logger.Debug("窗口未初始化，保存待处理的热键");
                    return false;
                }

                // 2. 获取主热键虚拟键码和修饰键标志
                _startVirtualKey = GetVirtualKeyFromDDKey(ddKeyCode);
                if (_startVirtualKey == 0)
                {
                    _logger.Error($"无效的虚拟键码: {ddKeyCode}");
                    return false;
                }

                uint modifierFlags = ConvertToModifierFlags(modifiers);
                _lastStartModifiers = modifierFlags;
                _pendingStartKey = ddKeyCode;
                _pendingStartMods = modifiers;

                // 3. 提前确定模式并更新配置
                bool isSameKeyMode = _pendingStopKey.HasValue && _pendingStopKey.Value == ddKeyCode;
                _currentMode = isSameKeyMode ? HotkeyMode.Same : HotkeyMode.Different;

                // 4. 更新配置文件
                AppConfigService.UpdateConfig(config =>
                {
                    // 保存当前按键配置
                    config.startKey = ddKeyCode;
                    config.startMods = modifiers;

                    // 根据当前模式保存相应的配置
                    if (_ddDriverService.IsSequenceMode)
                    {
                        _sequenceModeStartKey = ddKeyCode;
                        _sequenceModeStartMods = modifiers;

                        // 如果是Same模式，停止键也使用相同的按键
                        if (_currentMode == HotkeyMode.Same)
                        {
                            config.stopKey = ddKeyCode;
                            config.stopMods = modifiers;
                            _sequenceModeStopKey = ddKeyCode;
                            _sequenceModeStopMods = modifiers;
                        }
                    }
                    else
                    {
                        _holdModeKey = ddKeyCode;
                        _holdModeMods = modifiers;
                    }
                });

                _logger.Debug($"模式已确定: {_currentMode}，配置已更新");

                // 5. 如果是鼠标按键，不需要实际注册热键
                if (IsMouseButton(ddKeyCode))
                {
                    _startHotkeyRegistered = true;
                    _logger.Debug("鼠标按键无需注册系统热键");
                    return true;
                }

                // 6. 注册系统热键
                if (_startHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
                    _startHotkeyRegistered = false;
                }

                bool success = RegisterHotKey(
                    _windowHandle,
                    START_HOTKEY_ID,
                    _lastStartModifiers,
                    (uint)_startVirtualKey
                );

                if (success)
                {
                    _startHotkeyRegistered = true;
                    _logger.Debug($"热键注册成功 - " +
                        $"ID: {START_HOTKEY_ID}, " +
                        $"VK: 0x{_startVirtualKey:X}, " +
                        $"Mods: 0x{_lastStartModifiers:X}");
                }
                else
                {
                    _logger.Error($"热键注册失败 - " +
                        $"LastError: {Marshal.GetLastWin32Error()}");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error("注册开始热键异常", ex);
                return false;
            }
        }

        // 修改注册停止热键的方法
        public bool RegisterStopHotkey(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            try
            {
                _logger.Debug($"开始注册停止热键 - " +
                    $"键码: {ddKeyCode}, " +
                    $"修饰键: {modifiers}, " +
                    $"开始键: {_pendingStartKey}, " +
                    $"当前状态: 已启动({_isStarted}), 序列运行({_isSequenceRunning})");

                // 1. 检查窗口是否已初始化
                if (!_isWindowInitialized)
                {
                    _pendingStopKey = ddKeyCode;
                    _pendingStopMods = modifiers;
                    _logger.Debug("窗口未初始化，保存待处理的热键");
                    return true;
                }

                // 2. 获取虚拟键码和修饰键志
                _stopVirtualKey = GetVirtualKeyFromDDKey(ddKeyCode);
                if (_stopVirtualKey == 0)
                {
                    _logger.Error($"无效的虚拟键码: {ddKeyCode}");
                    return false;
                }

                uint modifierFlags = ConvertToModifierFlags(modifiers);
                _lastStopModifiers = modifierFlags;
                _pendingStopKey = ddKeyCode;

                // 3. 提前确定模式并更新配置
                bool isSameKeyMode = _pendingStartKey.HasValue && _pendingStartKey.Value == ddKeyCode;
                _currentMode = isSameKeyMode ? HotkeyMode.Same : HotkeyMode.Different;

                // 4. 更新配置文件
                AppConfigService.UpdateConfig(config =>
                {
                    config.stopKey = ddKeyCode;
                    config.stopMods = modifiers;
                    if (_ddDriverService.IsSequenceMode)
                    {
                        _sequenceModeStopKey = ddKeyCode;
                        _sequenceModeStopMods = modifiers;
                    }
                });

                _logger.Debug($"模式已确定: {_currentMode}, " +
                    $"配置已更新");

                // 5. 如果是鼠标按键，不需要实际注册热键
                if (IsMouseButton(ddKeyCode))
                {
                    _stopHotkeyRegistered = true;
                    _logger.Debug("鼠标按键无需注册系统热键");
                    return true;
                }

                // 6. 在Different模式下注册系统热键
                if (_currentMode == HotkeyMode.Different)
                {
                    if (_stopHotkeyRegistered)
                    {
                        UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
                        _stopHotkeyRegistered = false;
                    }

                    bool success = RegisterHotKey(
                        _windowHandle,
                        STOP_HOTKEY_ID,
                        _lastStopModifiers,
                        (uint)_stopVirtualKey
                    );

                    if (success)
                    {
                        _stopHotkeyRegistered = true;
                        _logger.Debug($"热键注册成功 - " +
                            $"ID: {STOP_HOTKEY_ID}, " +
                            $"VK: 0x{_stopVirtualKey:X}, " +
                            $"Mods: 0x{_lastStopModifiers:X}");
                    }
                    else
                    {
                        _logger.Error($"热键注册失败 - " +
                            $"LastError: {Marshal.GetLastWin32Error()}");
                    }

                    return success;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("注册停止热键异常", ex);
                return false;
            }
        }

        // 检查序列模式状态
        public bool IsSequenceRunning => _isSequenceRunning;

        // 手动触发按键
        public async Task TriggerKeyAsync(DDKeyCode keyCode)
        {
            if (!_isSequenceRunning)
            {
                _logger.Debug($"序列未运行，忽略按键: {keyCode}");
                return;
            }

            try
            {
                _logger.Debug($"开始触发按键: {keyCode}");
                KeyTriggered?.Invoke(keyCode);
                bool result = await Task.Run(() => _ddDriverService.SimulateKeyPress(keyCode));
                _logger.Debug($"按键触发{(result ? "成功" : "失败")}: {keyCode}");
            }
            catch (Exception ex)
            {
                _logger.Error($"触发按键异常: {keyCode}", ex);
            }
        }

        // 修改停止序列的方法
        public void StopSequence()
        {
            if (_isDisposed) return;

            try
            {
                _logger.Debug($"开始停止序列 - " +
                    $"当前状态: 已启动({_isStarted}), 序列运行({_isSequenceRunning}), " +
                    $"驱动模式: {(_ddDriverService.IsSequenceMode ? "顺序模式" : "按压模式")}");

                if (!_isSequenceRunning && !_isStarted) 
                {
                    _logger.Debug("序列未运行，无需停止");
                    return;
                }

                // 先重置状态
                _isSequenceRunning = false;
                _isStarted = false;

                // 停止驱动服务
                try
                {
                    _ddDriverService.IsEnabled = false;
                    if (!_ddDriverService.IsSequenceMode)
                    {
                        _ddDriverService.SetHoldMode(false);
                    }
                    _logger.Debug("驱动服务已停止");
                }
                catch (Exception driverEx)
                {
                    _logger.Error("停止动服务时发生异常", driverEx);
                }

                // 取消序列任务
                var cts = Interlocked.Exchange(ref _sequenceCts, null);
                if (cts != null)
                {
                    try
                    {
                        cts.Cancel();
                        _logger.Debug("序列任务已取消");
                    }
                    catch (Exception ctsEx)
                    {
                        _logger.Error("取消序列任务时发生异常", ctsEx);
                    }
                    finally
                    {
                        cts.Dispose();
                    }
                }

                // 触发停止事件
                try
                {
                    SequenceModeStopped?.Invoke();
                    _logger.Debug("🍒 ==》 序列已全停止 《== 🍒 ");
                    _logger.Debug("=================================================");
                }
                catch (Exception eventEx)
                {
                    _logger.Error("触发停止事件时发生异常", eventEx);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Debug停止序列时发生异常", ex);
                // 确保状态被重置
                _isSequenceRunning = false;
                _isStarted = false;
                
                // 确保驱动服务停止
                try
                {
                    _ddDriverService.IsEnabled = false;
                    _ddDriverService.SetHoldMode(false);
                }
                catch { /* 忽略清理时的异常 */ }
            }
        }

        // 设置按键列表和间隔
        public void SetKeySequence(List<DDKeyCode> keyList, int interval)
        {
            try
            {
                // _logger.Debug($"设置按键序列 - 按键数量: {keyList?.Count ?? 0}, 间隔: {interval}ms");
                
                if (keyList == null || keyList.Count == 0)
                {
                    _logger.Warning("收到空的按键序列，停止当前运行的序列");
                    // 如果当前正在运行，则停止
                    if (_isSequenceRunning || _isStarted)
                    {
                        StopSequence();
                    }
                    _keyList.Clear();
                    return;
                }
                
                _keyList = new List<DDKeyCode>(keyList);
                _ddDriverService.SetKeyInterval(interval);
                _logger.Debug($"按键序列已更新 - 按键数量: {_keyList.Count}, 间隔: {_ddDriverService.KeyInterval}ms");
            }
            catch (Exception ex)
            {
                _logger.Error("设置按键序列异常", ex);
                // 发生异常时清空按键列表并停止序列
                _keyList.Clear();
                StopSequence();
            }
        }

        // 使用Windows API检查按键是否按下
        private bool IsKeyPressedBySystem(DDKeyCode ddKeyCode)
        {
            if (ddKeyCode == DDKeyCode.None) return false;
            
            int vk = GetVirtualKeyFromDDKey(ddKeyCode);
            if (vk == 0) return false;
            
            // 使用GetAsyncKeyState检查按键状态
            short keyState = GetAsyncKeyState(vk);
            return (keyState & 0x8000) != 0;
        }

        // 添加反向查找方法
        // 将DD键码转换为虚拟键码
        private int GetVirtualKeyFromDDKey(DDKeyCode ddKeyCode)
        {
            try 
            {
                _logger.Debug($"尝试转换DD键码: {ddKeyCode} ({(int)ddKeyCode})");
                
                // 添加鼠标按键的特殊处理
                switch (ddKeyCode)
                {
                    case DDKeyCode.MBUTTON:
                        return 0x04; // VK_MBUTTON
                    case DDKeyCode.XBUTTON1:
                        return 0x05; // VK_XBUTTON1
                    case DDKeyCode.XBUTTON2:
                        return 0x06; // VK_XBUTTON2
                }

                // 检查映射表中的所有项
                foreach (var pair in KeyCodeMapping.VirtualToDDKeyMap)
                {
                    if (pair.Value == ddKeyCode)
                    {
                        _logger.Debug($"找到匹配的虚拟键码: 0x{pair.Key:X2}");
                        return pair.Key;
                    }
                }
                
                _logger.Debug($"未找到匹配的虚拟键码: {ddKeyCode}");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.Error("转换DD键码异常", ex);
                return 0;
            }
        }

        // 修改处理热键消息的方法
        private void HandleHotkeyMessage(int id)
        {
            try 
            {
                if (_isInputFocused)
                {
                    return;
                }

                var now = DateTime.Now;
                
                // 根据当前模式分发处理
                if (!_ddDriverService.IsSequenceMode)
                {
                    HandleHoldModeHotkey(id);
                }
                else
                {
                    HandleSequenceModeHotkey(id, now);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("[HandleHotkeyMessage] 处理热键消息异常", ex);
                try
                {
                    StopSequence();
                }
                catch (Exception stopEx)
                {
                    _logger.Error("[HandleHotkeyMessage] 异常处理时停止序列失败", stopEx);
                }
            }
        }

        // 处理按模式的热键消息
        private void HandleHoldModeHotkey(int id)
        {
            switch (id)
            {
                case START_HOTKEY_ID:
                    if (!_isKeyHeld)
                    {
                        HandleHoldModeKeyPress();
                    }
                    else
                    {
                        HandleHoldModeKeyRelease();
                    }
                    break;
            }
        }

        // 处理顺序模式的热键消息
        private void HandleSequenceModeHotkey(int id, DateTime now)
        {
            try
            {
                // 检查是否是按键按下状态
                if (!_isKeyHeld)
                {
                    _lastKeyDownTime = now;
                    _isKeyHeld = true;
                    
                    // 只在已经启动的情况下进行防抖处理
                    if (_isStarted || _isSequenceRunning)
                    {
                        var timeSinceLastToggle = (now - _lastToggleTime).TotalMilliseconds;
                        if (timeSinceLastToggle < MIN_TOGGLE_INTERVAL)
                        {
                            _logger.Debug($"忽略过快的切换 - " +
                                $"间隔: {timeSinceLastToggle}ms, " +
                                $"最小间隔: {MIN_TOGGLE_INTERVAL}ms");
                            return;
                        }
                    }
                }
                else
                {
                    // 防抖处理
                    var keyHoldTime = (now - _lastKeyDownTime).TotalMilliseconds;
                    if (keyHoldTime < KEY_RELEASE_TIMEOUT)
                    {
                        _logger.Debug($"按键持续按下 - " +
                            $"持续时间: {keyHoldTime}ms, " +
                            $"超时阈值: {KEY_RELEASE_TIMEOUT}ms");
                        return;
                    }
                    _isKeyHeld = false;
                }

                // 相同热键模式处理
                if (_currentMode == HotkeyMode.Same && id == START_HOTKEY_ID)
                {
                    if (!_isStarted && !_isSequenceRunning)
                    {
                        StartHotkeyPressed?.Invoke();
                        StartSequence();
                    }
                    else if (_isStarted || _isSequenceRunning)
                    {
                        StopHotkeyPressed?.Invoke();
                        StopSequence();
                    }
                    _lastToggleTime = now;
                    return;
                }

                // 不同热键模式处理
                switch (id)
                {
                    case START_HOTKEY_ID:
                        if (!_isStarted && !_isSequenceRunning)
                        {
                            StartHotkeyPressed?.Invoke();
                            StartSequence();
                            _lastToggleTime = now;
                        }
                        break;

                    case STOP_HOTKEY_ID:
                        if (_isStarted || _isSequenceRunning)
                        {
                            StopHotkeyPressed?.Invoke();
                            StopSequence();
                            _lastToggleTime = now;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("处理顺序模式热键异常", ex);
                StopSequence();
            }
        }

        // 修改鼠标按键消息处理方法
        private void HandleMouseButtonMessage(DDKeyCode buttonCode)
        {
            try
            {
                if (_isInputFocused)
                {
                    return;
                }

                var now = DateTime.Now;
                
                // 根据当前模式分发处理
                if (!_ddDriverService.IsSequenceMode)
                {
                    HandleHoldModeMouseButton(buttonCode);
                }
                else
                {
                    HandleSequenceModeMouseButton(buttonCode, now);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"处理鼠标按键消息异常: {ex.Message}", ex);
            }
        }

        // 处理按压模式的鼠标按键
        private void HandleHoldModeMouseButton(DDKeyCode buttonCode)
        {
            if (buttonCode != _pendingStartKey)
            {
                return;
            }

            if (!_isKeyHeld)
            {
                HandleHoldModeKeyPress();
            }
            else
            {
                HandleHoldModeKeyRelease();
            }
        }

        // 处理顺序模式的鼠标按键
        private void HandleSequenceModeMouseButton(DDKeyCode buttonCode, DateTime now)
        {
            // 检查是否是按键按下状态
            if (!_isKeyHeld)
            {
                _lastKeyDownTime = now;
                _isKeyHeld = true;
                
                // 防抖处理
                var timeSinceLastToggle = (now - _lastToggleTime).TotalMilliseconds;
                if (timeSinceLastToggle < MIN_TOGGLE_INTERVAL)
                {
                    _logger.Debug($"忽略过快的切换 - " +
                        $"间隔: {timeSinceLastToggle}ms, " +
                        $"最小间隔: {MIN_TOGGLE_INTERVAL}ms");
                    return;
                }
            }
            else
            {
                // 防抖处理
                var keyHoldTime = (now - _lastKeyDownTime).TotalMilliseconds;
                if (keyHoldTime < KEY_RELEASE_TIMEOUT)
                {
                    _logger.Debug($"按键持续按下 - " +
                        $"持续时间: {keyHoldTime}ms, " +
                        $"超时阈值: {KEY_RELEASE_TIMEOUT}ms");
                    return;
                }
                _isKeyHeld = false;
            }

            // 优先处理停止键
            if (buttonCode == _pendingStopKey && (_isStarted || _isSequenceRunning))
            {
                StopKeyMapping();
                _lastToggleTime = now;
                return;
            }

            // 相同热键模式处理
            if (_currentMode == HotkeyMode.Same && buttonCode == _pendingStartKey)
            {
                if (!_isStarted && !_isSequenceRunning)
                {
                    StartHotkeyPressed?.Invoke();
                    StartSequence();
                }
                else
                {
                    StopKeyMapping();
                }
                _lastToggleTime = now;
                return;
            }

            // 不同热键模式处理
            if (buttonCode == _pendingStartKey && !_isStarted && !_isSequenceRunning)
            {
                StartHotkeyPressed?.Invoke();
                StartSequence();
                _lastToggleTime = now;
            }
        }

        // 添加临时取消注册热键的方法
        private void TemporarilyUnregisterHotkeys()
        {
            try
            {
                _logger.Debug("临时取消注册热键");
                if (_startHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
                    _logger.Debug("已取消注册开始热键");
                }
                if (_stopHotkeyRegistered && _currentMode == HotkeyMode.Different)
                {
                    UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
                    _logger.Debug("已取消注册停止热键");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("临时取消注册热键时发生错误", ex);
            }
        }

        // 添加恢复热键注册的方法
        private void RestoreHotkeys()
        {
            try
            {
                _logger.Debug("尝试恢复热键注册");
                if (_startHotkeyRegistered)
                {
                    bool result = RegisterHotKey(_windowHandle, START_HOTKEY_ID, _lastStartModifiers, (uint)_startVirtualKey);
                    _logger.Debug($"恢复开始热键注册: {result}");
                }
                if (_stopHotkeyRegistered && _currentMode == HotkeyMode.Different)
                {
                    bool result = RegisterHotKey(_windowHandle, STOP_HOTKEY_ID, _lastStopModifiers, (uint)_stopVirtualKey);
                    _logger.Debug($"恢复停止热键注册: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("恢复热键注册时发生错误", ex);
            }
        }

        // 添加辅助方法
        private void CleanupExistingHotkeys()
        {
            try
            {   
                // 如果开始热键已注册，则取消注册
                if (_startHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
                    _startHotkeyRegistered = false;
                }

                _currentMode = HotkeyMode.Different;
                _isStarted = false;
                
                _logger.Debug("清理现有热键注册");
            }
            catch (Exception ex)
            {
                _logger.Error("清理热键注册时发生错误", ex);
            }
        }

        // 将修饰键转换为Win32 API的修饰键标志
        private uint ConvertToModifierFlags(ModifierKeys modifiers)
        {
            uint flags = 0;
            if (modifiers.HasFlag(ModifierKeys.Alt)) flags |= 0x0001;
            if (modifiers.HasFlag(ModifierKeys.Control)) flags |= 0x0002;
            if (modifiers.HasFlag(ModifierKeys.Shift)) flags |= 0x0004;
            return flags;
        }

        // 添加鼠标按键状态检查
        private bool IsMouseButtonPressed(DDKeyCode ddKeyCode)
        {
            try
            {
                int vk = ddKeyCode switch
                {
                    DDKeyCode.MBUTTON => 0x04,
                    DDKeyCode.XBUTTON1 => 0x05,
                    DDKeyCode.XBUTTON2 => 0x06,
                    _ => 0
                };

                if (vk == 0) return false;
                
                short keyState = GetAsyncKeyState(vk);
                return (keyState & 0x8000) != 0;
            }
            catch (Exception ex)
            {
                _logger.Error("检查鼠标按键状态异常", ex);
                return false;
            }
        }

        // 修改 StopKeyMapping 方法
        private void StopKeyMapping()
        {
            try
            {
                _logger.Debug($"开始停止按键映射 - " +
                    $"当前状态: {(_isStarted ? "已启动" : "未启动")}, " +
                    $"序列运行: {_isSequenceRunning}, " +
                    $"当前模式: {_currentMode}");
                
                // 先停止序列
                StopSequence();
                
                // 触发停止事件
                StopHotkeyPressed?.Invoke();
                
                // 重置状态
                _isStarted = false;
                _isSequenceRunning = false;
                
                _logger.Debug("按键映射已停止，所有状态已重置");
            }
            catch (Exception ex)
            {
                _logger.Error("停止按键映射异常", ex);
            }
        }

        // 添加辅助方法
        public bool IsMouseButton(DDKeyCode keyCode)
        {
            return keyCode == DDKeyCode.MBUTTON || 
                   keyCode == DDKeyCode.XBUTTON1 || 
                   keyCode == DDKeyCode.XBUTTON2;
        }

        // 添加全局鼠标钩子回调
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    if (_isInputFocused)
                    {
                        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
                    }

                    int wParamInt = (int)wParam;
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
                    
                    switch (wParamInt)
                    {
                        // 处理鼠标侧键
                        case WM_XBUTTONDOWN:
                            int xButton = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
                            DDKeyCode xButtonCode = xButton == 1 ? DDKeyCode.XBUTTON1 : DDKeyCode.XBUTTON2;
                            
                            _logger.Debug($"全局鼠标钩子捕获到侧键按下: {xButtonCode}, 当前模式: {(_ddDriverService.IsSequenceMode ? "顺序模式" : "按压模式")}");
                            
                            if (_ddDriverService.IsSequenceMode)
                            {
                                // 顺序模式下，处理开始键和停止键
                                if (xButtonCode == _pendingStartKey || xButtonCode == _pendingStopKey)
                                {
                                    HandleSequenceModeMouseButton(xButtonCode, DateTime.Now);
                                }
                            }
                            else
                            {
                                // 按压模式下，只处理开始键
                                if (xButtonCode == _pendingStartKey)
                                {
                                    HandleMouseButtonMessage(xButtonCode);
                                }
                            }
                            break;

                        case WM_XBUTTONUP:
                            int xButtonUp = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
                            DDKeyCode xButtonUpCode = xButtonUp == 1 ? DDKeyCode.XBUTTON1 : DDKeyCode.XBUTTON2;
                            if (!_ddDriverService.IsSequenceMode && _pendingStartKey == xButtonUpCode)
                            {
                                _logger.Debug($"全局鼠标钩子捕获到侧键释放");
                                HandleHoldModeKeyRelease();
                            }
                            break;

                        // 处理鼠标中键
                        case WM_MBUTTONDOWN:
                            _logger.Debug($"全局鼠标钩子捕获到中键按下, 当前模式: {(_ddDriverService.IsSequenceMode ? "顺序模式" : "按压模式")}");
                            
                            if (_ddDriverService.IsSequenceMode)
                            {
                                // 顺序模式下，处理开始键和停止键
                                if (_pendingStartKey == DDKeyCode.MBUTTON || _pendingStopKey == DDKeyCode.MBUTTON)
                                {
                                    HandleSequenceModeMouseButton(DDKeyCode.MBUTTON, DateTime.Now);
                                }
                            }
                            else
                            {
                                // 按压模式下，只处理开始键
                                if (_pendingStartKey == DDKeyCode.MBUTTON)
                                {
                                    HandleMouseButtonMessage(DDKeyCode.MBUTTON);
                                }
                            }
                            break;

                        case WM_MBUTTONUP:
                            if (!_ddDriverService.IsSequenceMode && _pendingStartKey == DDKeyCode.MBUTTON)
                            {
                                _logger.Debug("全局鼠标钩子捕获到中键释放");
                                HandleHoldModeKeyRelease();
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("鼠标钩子回调异常", ex);
                }
            }
            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        // 添加鼠标钩子结构
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        // 处理模式切换
        private void OnModeSwitched(object? sender, bool isSequenceMode)
        {
            try
            {
                _logger.Debug($"开始处理模式切换 - " +
                    $"目标模式: {(isSequenceMode ? "顺序模式" : "按压模式")}, " +
                    $"当前状态: 已启动({_isStarted}), 序列运行({_isSequenceRunning})");

                // 1. 停止当前运行的序列
                StopSequence();
                
                // 2. 取消注册所有热键
                UnregisterAllHotkeys();

                // 3. 根据目标模式处理热键配置
                if (isSequenceMode)
                {
                    // 从按压模式切换到顺序模式
                    if (_holdModeKey.HasValue)
                    {
                        // 3.1 保存当前按压模式的按键配置作为顺序模式的启动键
                        _sequenceModeStartKey = _holdModeKey;
                        _sequenceModeStartMods = _holdModeMods;
                        
                        // 3.2 检查历史顺序模式状态
                        var config = AppConfigService.Config;
                        if (config.stopKey != DDKeyCode.None && config.stopKey != _holdModeKey)
                        {
                            // 如果历史配置中有不同的停止键，恢复Different模式
                            _currentMode = HotkeyMode.Different;
                            _sequenceModeStopKey = config.stopKey;
                            _sequenceModeStopMods = config.stopMods;
                        }
                        else
                        {
                            // 否则使用Same模式
                            _currentMode = HotkeyMode.Same;
                            _sequenceModeStopKey = _holdModeKey;
                            _sequenceModeStopMods = _holdModeMods;
                        }
                    }
                    else
                    {
                        // 3.3 如果没有按压模式配置，从配置文件读取
                        var config = AppConfigService.Config;
                        if (config.startKey != DDKeyCode.None)
                        {
                            _sequenceModeStartKey = config.startKey;
                            _sequenceModeStartMods = config.startMods;
                            
                            if (config.stopKey != DDKeyCode.None && config.stopKey != config.startKey)
                            {
                                _currentMode = HotkeyMode.Different;
                                _sequenceModeStopKey = config.stopKey;
                                _sequenceModeStopMods = config.stopMods;
                            }
                            else
                            {
                                _currentMode = HotkeyMode.Same;
                                _sequenceModeStopKey = config.startKey;
                                _sequenceModeStopMods = config.startMods;
                            }
                        }
                    }
                }
                else
                {
                    // 从顺序模式切换到按压模式
                    if (_sequenceModeStartKey.HasValue)
                    {
                        // 3.4 保存当前顺序模式的启动键配置
                        _holdModeKey = _sequenceModeStartKey;
                        _holdModeMods = _sequenceModeStartMods;
                    }
                    else
                    {
                        // 3.5 如果没有顺序模式配置，从配置文件读取
                        var config = AppConfigService.Config;
                        if (config.startKey != DDKeyCode.None)
                        {
                            _holdModeKey = config.startKey;
                            _holdModeMods = config.startMods;
                        }
                    }
                }

                // 4. 更新配置文件
                AppConfigService.UpdateConfig(config =>
                {
                    // 4.1 保存模式
                    config.keyMode = isSequenceMode ? 0 : 1;

                    if (isSequenceMode)
                    {
                        // 4.2 保存顺序模式配置
                        if (_sequenceModeStartKey.HasValue)
                        {
                            config.startKey = _sequenceModeStartKey.Value;
                            config.startMods = _sequenceModeStartMods;

                            if (_currentMode == HotkeyMode.Different && _sequenceModeStopKey.HasValue)
                            {
                                config.stopKey = _sequenceModeStopKey.Value;
                                config.stopMods = _sequenceModeStopMods;
                            }
                            else
                            {
                                // Same模式下，停止键与开始键相同
                                config.stopKey = _sequenceModeStartKey.Value;
                                config.stopMods = _sequenceModeStartMods;
                            }
                        }
                    }
                    else
                    {
                        // 4.3 保存按压模式配置
                        if (_holdModeKey.HasValue)
                        {
                            config.startKey = _holdModeKey.Value;
                            config.startMods = _holdModeMods;
                            // 保持原有的停止键配置不变
                        }
                    }
                });

                // 5. 注册热键
                if (_isWindowInitialized)
                {
                    if (isSequenceMode && _sequenceModeStartKey.HasValue)
                    {
                        // 5.1 注册顺序模式热键
                        RegisterStartHotkeyInternal(_sequenceModeStartKey.Value, _sequenceModeStartMods);
                        if (_currentMode == HotkeyMode.Different && _sequenceModeStopKey.HasValue)
                        {
                            RegisterStopHotkey(_sequenceModeStopKey.Value, _sequenceModeStopMods);
                        }
                    }
                    else if (!isSequenceMode && _holdModeKey.HasValue)
                    {
                        // 5.2 注册按压模式热键
                        RegisterStartHotkeyInternal(_holdModeKey.Value, _holdModeMods);
                    }
                }

                _logger.Debug($"模式切换完成 - " +
                    $"模式: {(isSequenceMode ? "顺序模式" : "按压模式")}, " +
                    $"热键模式: {_currentMode}");
            }
            catch (Exception ex)
            {
                _logger.Error("处理模式切换时发生异常", ex);
                try
                {
                    RestoreHotkeys();
                }
                catch (Exception restoreEx)
                {
                    _logger.Error("恢复热键失败", restoreEx);
                }
            }
        }

        // 不触发模式切换的热键注册
        private void RegisterPendingHotkeysWithoutModeSwitch()
        {
            try
            {
                _logger.Debug("开始注册待理的热键");

                bool startSuccess = true;
                bool stopSuccess = true;

                if (_pendingStartKey.HasValue)
                {
                    startSuccess = RegisterStartHotkey(_pendingStartKey.Value, _pendingStartMods);
                    _logger.Debug($"注册开始热键 - " +
                        $"键码: {_pendingStartKey.Value}, " +
                        $"结果: {(startSuccess ? "成功" : "失败")}");
                }

                if (_pendingStopKey.HasValue && _currentMode == HotkeyMode.Different)
                {
                    stopSuccess = RegisterStopHotkey(_pendingStopKey.Value, _pendingStopMods);
                    _logger.Debug($"注册停止热键 - " +
                        $"键码: {_pendingStopKey.Value}, " +
                        $"结果: {(stopSuccess ? "成功" : "失败")}");
                }

                // 只在所有热键注册完成后一次性更新配置，但不包含模式信息
                if (startSuccess && stopSuccess)
                {
                    AppConfigService.UpdateConfig(config =>
                    {
                        if (_pendingStartKey.HasValue)
                        {
                            config.startKey = _pendingStartKey.Value;
                            config.startMods = _pendingStartMods;
                        }
                        
                        if (_pendingStopKey.HasValue)
                        {
                            config.stopKey = _pendingStopKey.Value;
                            config.stopMods = _pendingStopMods;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error("注册待处理热键时发生错误", ex);
            }
        }

        // 取消注册所有热键
        private void UnregisterAllHotkeys()
        {
            try
            {
                _logger.Debug("开始取消注册所有热键");
                
                if (_startHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
                    _startHotkeyRegistered = false;
                }
                
                if (_stopHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
                    _stopHotkeyRegistered = false;
                }
                
                _currentMode = HotkeyMode.Different;
                _logger.Debug("所有热键已取消注册");
            }
            catch (Exception ex)
            {
                _logger.Error("取消注册热键时发生异常", ex);
            }
        }

        // 添加启动序列的方法
        private void StartSequence()
        {
            CancellationTokenSource? cts = null;
            try
            {
                _logger.Debug("开始启动序列...");
                
                // 确保序列已停止
                StopSequence();
                
                // 检查按键列表是否为空
                if (_keyList == null || _keyList.Count == 0)
                {
                    _logger.Warning("按键列表为空，无法启动序列");
                    return;
                }

                // 创建新的取消令牌
                cts = new CancellationTokenSource();
                var token = cts.Token;
                _sequenceCts = cts;
                
                // 设置状态
                _isStarted = true;
                _isSequenceRunning = true;
                
                // 确保驱动服务于正确状态
                if (_ddDriverService.IsSequenceMode)
                {
                    _ddDriverService.IsEnabled = true;
                }
                else
                {
                    _ddDriverService.SetHoldMode(true);
                }
                
                // 触发启动事件
                SequenceModeStarted?.Invoke();
                
                _logger.Debug("序列已启动");
            }
            catch (Exception ex)
            {
                _logger.Error("启动序列时发生异常", ex);
                // 出错时重置状态
                _isSequenceRunning = false;
                _isStarted = false;
                
                if (cts != null)
                {
                    try
                    {
                        if (_sequenceCts == cts)
                        {
                            _sequenceCts = null;
                        }
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch { /* 忽略清理时的异常 */ }
                }
                
                _ddDriverService.IsEnabled = false;
                _ddDriverService.SetHoldMode(false);
            }
        }

        // 修改按压模式的按键处理方法
        private void HandleHoldModeKeyPress()
        {
            if (!Monitor.TryEnter(_holdModeLock))
            {
                _logger.Debug("已有按压模式在运行，忽略此次按键");
                return;
            }

            try
            {
                if (_isHoldModeRunning)
                {
                    _logger.Debug("按压模式已在运行中");
                    return;
                }

                if (_keyList == null || _keyList.Count == 0)
                {
                    _logger.Warning("按键列表为空，无法启动序列");
                    _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                    return;
                }

                var selectedKeys = _keyList.Where(k => k != DDKeyCode.None).ToList();
                if (selectedKeys.Count == 0)
                {
                    _logger.Warning("没有选中任何按键，无法启动序列");
                    _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                    return;
                }

                _isHoldModeRunning = true;
                _isSequenceRunning = true;
                _isStarted = true;
                
                _ddDriverService.SetHoldMode(true);
                
                _logger.Debug("按压模式已启动");
                
                StartHotkeyPressed?.Invoke();
                SequenceModeStarted?.Invoke();
            }
            finally
            {
                Monitor.Exit(_holdModeLock);
            }
        }

        private void HandleHoldModeKeyRelease()
        {
            CancellationTokenSource? cts = null;
            bool needsCleanup = false;

            try
            {
                _logger.Debug("处理按压模式按键释放");

                lock (_holdModeLock)
                {
                    if (!_isHoldModeRunning && !_isSequenceRunning && !_isStarted)
                    {
                        _logger.Debug("序列未运行，忽略按键释放");
                        return;
                    }

                    cts = Interlocked.Exchange(ref _sequenceCts, null);
                    needsCleanup = true;

                    _isHoldModeRunning = false;
                    _isSequenceRunning = false;
                    _isStarted = false;
                }

                if (cts != null)
                {
                    try
                    {
                        cts.Cancel();
                        _logger.Debug("序列任务已取消");

                        Task.WaitAll(new[] { Task.Delay(50) }, 100);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("取消序列任务时发生异常", ex);
                    }
                    finally
                    {
                        try
                        {
                            cts.Dispose();
                        }
                        catch { /* 忽略释放时的异常 */ }
                    }
                }

                if (needsCleanup)
                {
                    try
                    {
                        _ddDriverService.SetHoldMode(false);
                        _ddDriverService.IsEnabled = false;
                        needsCleanup = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("重置驱动服务状态时发生异常", ex);
                    }

                    try
                    {
                        StopHotkeyPressed?.Invoke();
                        SequenceModeStopped?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("触发停止事件时发生异常", ex);
                    }
                }

                _logger.Debug("按压模式已停止");
            }
            catch (Exception ex)
            {
                _logger.Error("处理按压模式按键释放异常", ex);
                
                // 发生异常时的最终清理，只在之前没有成功清理时执行
                if (needsCleanup)
                {
                    try
                    {
                        // 再次尝试重置所有状态
                        lock (_holdModeLock)
                        {
                            _isHoldModeRunning = false;
                            _isSequenceRunning = false;
                            _isStarted = false;
                        }

                        _ddDriverService.SetHoldMode(false);
                        _ddDriverService.IsEnabled = false;

                        // 如果之前没有成功取消任务，再次尝试
                        if (cts == null)
                        {
                            cts = Interlocked.Exchange(ref _sequenceCts, null);
                        }
                        
                        if (cts != null)
                        {
                            try
                            {
                                cts.Cancel();
                                cts.Dispose();
                            }
                            catch { /* 忽略清理时的异常 */ }
                        }
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.Error("最终清理时发生异常", cleanupEx);
                    }
                }
            }
        }

        // 添加键盘钩子回调
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int wParamInt = wParam.ToInt32();
                    KBDLLHOOKSTRUCT hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT))!;

                    // 检查是否是我们关注的按键
                    if (hookStruct.vkCode == _startVirtualKey)
                    {
                        if (!_ddDriverService.IsSequenceMode)
                        {
                            switch (wParamInt)
                            {
                                case WM_KEYUP:
                                case WM_SYSKEYUP:
                                    _logger.Debug($"检测到真实物理 Keyboard 被释放 - VK: {hookStruct.vkCode}");
                                    HandleHoldModeKeyRelease();
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("键盘钩子回调异常", ex);
                }
            }
            return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        // 添加公共的RegisterStartHotkey方法
        public bool RegisterStartHotkey(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            try
            {
                _logger.Debug($"开始注册开始热键 - " +
                    $"键码: {ddKeyCode}, " +
                    $"修饰键: {modifiers}, " +
                    $"停止键: {_pendingStopKey}, " +
                    $"当前状态: 已启动({_isStarted}), 序列运行({_isSequenceRunning})");

                // 保存待处理的热键
                _pendingStartKey = ddKeyCode;
                _pendingStartMods = modifiers;

                // 如果窗口未初始化，将热键注册任务加入到窗口初始化事件中
                if (!_isWindowInitialized)
                {
                    _logger.Debug("窗口未初始化，将在窗口初始化后注册热键");
                    _mainWindow.SourceInitialized += (s, e) =>
                    {
                        RegisterStartHotkeyInternal(ddKeyCode, modifiers);
                    };
                    return true;
                }

                return RegisterStartHotkeyInternal(ddKeyCode, modifiers);
            }
            catch (Exception ex)
            {
                _logger.Error("注册开始热键异常", ex);
                return false;
            }
        }
    }
}