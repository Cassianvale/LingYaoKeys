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

// 提供快捷键服务
namespace WpfApp.Services
{
    public class HotkeyService
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
        private readonly LogManager _logger = LogManager.Instance;
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

        // 构造函数
        public HotkeyService(Window mainWindow, DDDriverService ddDriverService)
        {
            _mainWindow = mainWindow;
            _ddDriverService = ddDriverService;
            
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
                _logger.LogDebug("HotkeyService", "[Constructor] 初始化为顺序模式");
            }
            else
            {
                // 加载按压模式的热键配置，只注册启动键
                _holdModeKey = config.startKey;
                _holdModeMods = config.startMods;
                _logger.LogDebug("HotkeyService", "[Constructor] 初始化为按压模式");
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
                        _logger.LogInitialization("HotkeyService", $"窗口初始化完成，获取句柄: {_windowHandle:X}");
                        
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
                    _logger.LogError("HotkeyService", "窗口初始化处理异常", ex);
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
                MessageBox.Show("请以管理员身份运行程序以使用热键功能", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                _logger.LogDebug("HotkeyService", "热键已经注册，请勿重复注册");
                return false;
            }
            
            // 条件2：检查窗口句柄是否有效
            if (_windowHandle == IntPtr.Zero)
            {
                _logger.LogError("HotkeyService", "无效的窗口句柄，无法注册热键");
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
                    MessageBox.Show("热键注册失败，可能被其他程序占用", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                _logger.LogDebug("HotkeyService", $"热键注册成功，热键ID: {START_HOTKEY_ID}");
                return _isRegistered;
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "热键注册异常", ex);
                MessageBox.Show($"热键注册异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                _logger.LogError("HotkeyService", "窗口句柄无效，无法取消注册热键");
                return;
            }

            // 3. 取消热键注册
            try
            {   
                // 调用Win32API��UnregisterHotKey函数取消注册热键
                UnregisterHotKey(
                    _windowHandle, // 使用主窗口句柄
                    START_HOTKEY_ID // 热键的ID
                    );
                _isRegistered = false;  // 将热键注册状态设置为false
                _logger.LogDebug("HotkeyService", "热键注销成功");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", $"热键注销异常: {ex.Message}");
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
                            _logger.LogDebug("HotkeyService", $"[WndProc] 收到按键释放消息 - VK: 0x{vkCode:X}, 当前热键VK: 0x{_startVirtualKey:X}");
                            
                            if (vkCode == _startVirtualKey)
                            {
                                _logger.LogDebug("HotkeyService", "[WndProc] 检测到启动键释放");
                                HandleHoldModeKeyRelease();
                                handled = true;
                            }
                        }
                        break;

                    case WM_XBUTTONDOWN:
                        int xButton = (int)((wParam.ToInt32() >> 16) & 0xFFFF);
                        DDKeyCode xButtonCode = xButton == 1 ? DDKeyCode.XBUTTON1 : DDKeyCode.XBUTTON2;
                        HandleMouseButtonMessage(xButtonCode);
                        handled = true;
                        break;

                    case WM_XBUTTONUP:
                        if (!_ddDriverService.IsSequenceMode)
                        {
                            int xButtonUp = (int)((wParam.ToInt32() >> 16) & 0xFFFF);
                            DDKeyCode xButtonUpCode = xButtonUp == 1 ? DDKeyCode.XBUTTON1 : DDKeyCode.XBUTTON2;
                            _logger.LogDebug("HotkeyService", $"[WndProc] 收到鼠标侧键释放消息 - 按键: {xButtonUpCode}, 当前热键: {_pendingStartKey}");
                            
                            if (xButtonUpCode == _pendingStartKey)
                            {
                                _logger.LogDebug("HotkeyService", "[WndProc] 检测到鼠标侧键释放");
                                HandleHoldModeKeyRelease();
                                handled = true;
                            }
                        }
                        break;

                    case WM_MBUTTONDOWN:
                        HandleMouseButtonMessage(DDKeyCode.MBUTTON);
                        handled = true;
                        break;

                    case WM_MBUTTONUP:
                        if (!_ddDriverService.IsSequenceMode && _pendingStartKey == DDKeyCode.MBUTTON)
                        {
                            _logger.LogDebug("HotkeyService", "[WndProc] 检测到鼠标中键释放");
                            HandleHoldModeKeyRelease();
                            handled = true;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "WndProc处理异常", ex);
            }
            
            return IntPtr.Zero;
        }

        // 释放资源
        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_disposeLock)
            {
                if (_isDisposed) return;

                try
                {
                    _logger.LogDebug("HotkeyService", "开始清理资源...");
                    
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
                    
                    StopSequence();
                    UnregisterHotKey();
                    
                    if (_source != null)
                    {
                        _source.RemoveHook(WndProc);
                        _source = null;
                    }

                    _isDisposed = true;
                    _logger.LogDebug("HotkeyService", "资源清理完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError("HotkeyService", "清理资源时发生异常", ex);
                }
            }
        }

        // 修改注册开始热键的方法
        public bool RegisterStartHotkey(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            try
            {
                _logger.LogDebug("HotkeyService", 
                    $"[RegisterStartHotkey] 开始注册开始热键 - " +
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
                    _logger.LogDebug("HotkeyService", "[RegisterStartHotkey] 窗口未初始化，将在窗口初始化后注册热键");
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
                _logger.LogError("HotkeyService", "[RegisterStartHotkey] 注册开始热键异常", ex);
                return false;
            }
        }

        // 修改注册停止热键的方法
        public bool RegisterStopHotkey(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            try
            {
                _logger.LogDebug("HotkeyService", 
                    $"[RegisterStopHotkey] 开始注册停止热键 - " +
                    $"键码: {ddKeyCode}, " +
                    $"修饰键: {modifiers}, " +
                    $"开始键: {_pendingStartKey}, " +
                    $"当前状态: 已启动({_isStarted}), 序列运行({_isSequenceRunning})");

                // 1. 检查窗口是否已初始化
                if (!_isWindowInitialized)
                {
                    _pendingStopKey = ddKeyCode;
                    _pendingStopMods = modifiers;
                    _logger.LogDebug("HotkeyService", "[RegisterStopHotkey] 窗口未初始化，保存待处理的热键");
                    return true;
                }

                // 2. 获取虚拟键码和修饰键标志
                _stopVirtualKey = GetVirtualKeyFromDDKey(ddKeyCode);
                if (_stopVirtualKey == 0)
                {
                    _logger.LogError("HotkeyService", $"[RegisterStopHotkey] 无效的虚拟键码: {ddKeyCode}");
                    return false;
                }

                uint modifierFlags = ConvertToModifierFlags(modifiers);
                _lastStopModifiers = modifierFlags;
                _pendingStopKey = ddKeyCode;

                // 3. 提前确定模式并更新配置
                bool isSameKeyMode = _pendingStartKey.HasValue && _pendingStartKey.Value == ddKeyCode;
                _currentMode = isSameKeyMode ? HotkeyMode.Same : HotkeyMode.Different;

                _logger.LogDebug("HotkeyService", 
                    $"[RegisterStopHotkey] 模式已确定: {_currentMode}, " +
                    $"配置已更新");

                // 4. 如果是鼠标按键，不需要实际注册热键
                if (IsMouseButton(ddKeyCode))
                {
                    _stopHotkeyRegistered = true;
                    _logger.LogDebug("HotkeyService", "[RegisterStopHotkey] 鼠标按键无需注册系统热键");
                    return true;
                }

                // 5. 在Different模式下注册系统热键
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
                        _logger.LogDebug("HotkeyService", 
                            $"[RegisterStopHotkey] 热键注册成功 - " +
                            $"ID: {STOP_HOTKEY_ID}, " +
                            $"VK: 0x{_stopVirtualKey:X}, " +
                            $"Mods: 0x{_lastStopModifiers:X}");
                    }
                    else
                    {
                        _logger.LogError("HotkeyService", 
                            $"[RegisterStopHotkey] 热键注册失败 - " +
                            $"LastError: {Marshal.GetLastWin32Error()}");
                    }

                    return success;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "[RegisterStopHotkey] 注册停止热键异常", ex);
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
                _logger.LogDebug("HotkeyService", $"序列未运行，忽略按键: {keyCode}");
                return;
            }

            try
            {
                _logger.LogDebug("HotkeyService", $"开始触发按键: {keyCode}");
                KeyTriggered?.Invoke(keyCode);
                bool result = await Task.Run(() => _ddDriverService.SimulateKeyPress(keyCode));
                _logger.LogDebug("HotkeyService", $"按键触发{(result ? "成功" : "失败")}: {keyCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", $"触发按键异常: {keyCode}", ex);
            }
        }

        // 停止序列模式
        public void StopSequence()
        {
            if (_isDisposed) return;

            try
            {
                _logger.LogDebug("HotkeyService", 
                    $"[StopSequence] 开始停止序列 - " +
                    $"当前状态: 已启动({_isStarted}), 序列运行({_isSequenceRunning}), " +
                    $"驱动模式: {(_ddDriverService.IsSequenceMode ? "顺序模式" : "按压模式")}");

                if (!_isSequenceRunning && !_isStarted) 
                {
                    _logger.LogDebug("HotkeyService", "[StopSequence] 序列未运行，无需停止");
                    return;
                }

                // 先停止驱动服务
                try
                {
                    _ddDriverService.IsEnabled = false;
                    if (!_ddDriverService.IsSequenceMode)
                    {
                        _ddDriverService.SetHoldMode(false);
                    }
                    _logger.LogDebug("HotkeyService", "[StopSequence] 驱动服务已停止");
                }
                catch (Exception driverEx)
                {
                    _logger.LogError("HotkeyService", "[StopSequence] 停止驱动服务时发生异常", driverEx);
                }

                // 取消序列任务
                if (_sequenceCts != null)
                {
                    try
                    {
                        _sequenceCts.Cancel();
                        _sequenceCts.Dispose();
                        _sequenceCts = null;
                        _logger.LogDebug("HotkeyService", "[StopSequence] 序列任务已取消");
                    }
                    catch (Exception ctsEx)
                    {
                        _logger.LogError("HotkeyService", "[StopSequence] 取消序列任务时发生异常", ctsEx);
                    }
                }

                // 重置状态
                _isSequenceRunning = false;
                _isStarted = false;

                // 触发停止事件
                try
                {
                    SequenceModeStopped?.Invoke();
                    _logger.LogDebug("HotkeyService", "[StopSequence] 序列已完全停止");
                }
                catch (Exception eventEx)
                {
                    _logger.LogError("HotkeyService", "[StopSequence] 触发停止事件时发生异常", eventEx);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "[StopSequence] 停止序列时发生异常", ex);
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
            if (keyList == null || keyList.Count == 0)
            {
                _logger.LogWarning("HotkeyService", "试图设置空的按键序列");
                return;
            }
            
            _keyList = new List<DDKeyCode>(keyList);
            _ddDriverService.SetKeyInterval(interval);
            _logger.LogDebug("HotkeyService", 
                $"更新按键序列 - 按键数量: {_keyList.Count}, 间隔: {_ddDriverService.KeyInterval}ms");
        }

        private bool IsKeyPressed(DDKeyCode ddKeyCode)
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
                _logger.LogDebug("HotkeyService", $"尝试转换DD键码: {ddKeyCode} ({(int)ddKeyCode})");
                
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
                        _logger.LogDebug("HotkeyService", $"找到匹配的虚拟键码: 0x{pair.Key:X2}");
                        return pair.Key;
                    }
                }
                
                _logger.LogDebug("HotkeyService", $"未找到匹配的虚拟键码: {ddKeyCode}");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "转换DD键码异常", ex);
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
                _logger.LogError("HotkeyService", "[HandleHotkeyMessage] 处理热键消息异常", ex);
                try
                {
                    StopSequence();
                }
                catch (Exception stopEx)
                {
                    _logger.LogError("HotkeyService", "[HandleHotkeyMessage] 异常处理时停止序列失败", stopEx);
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
            // 检查是否是按键按下状态
            if (!_isKeyHeld)
            {
                _lastKeyDownTime = now;
                _isKeyHeld = true;
                
                // 防抖处理
                var timeSinceLastToggle = (now - _lastToggleTime).TotalMilliseconds;
                if (timeSinceLastToggle < MIN_TOGGLE_INTERVAL)
                {
                    _logger.LogDebug("HotkeyService", 
                        $"[HandleSequenceModeHotkey] 忽略过快的切换 - " +
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
                    _logger.LogDebug("HotkeyService", 
                        $"[HandleSequenceModeHotkey] 按键持续按下 - " +
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
                _logger.LogError("HotkeyService", $"[MouseHandler] 处理鼠标按键消息异常: {ex.Message}", ex);
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
                    _logger.LogDebug("HotkeyService", 
                        $"[HandleSequenceModeMouseButton] 忽略过快的切换 - " +
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
                    _logger.LogDebug("HotkeyService", 
                        $"[HandleSequenceModeMouseButton] 按键持续按下 - " +
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
                _logger.LogDebug("HotkeyService", "临时取消注册热键");
                if (_startHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
                    _logger.LogDebug("HotkeyService", "已取消注册开始热键");
                }
                if (_stopHotkeyRegistered && _currentMode == HotkeyMode.Different)
                {
                    UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
                    _logger.LogDebug("HotkeyService", "已取消注册停止热键");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "临时取消注册热键时发生错误", ex);
            }
        }

        // 添加恢复热键注册的方法
        private void RestoreHotkeys()
        {
            try
            {
                _logger.LogDebug("HotkeyService", "尝试恢复热键注册");
                if (_startHotkeyRegistered)
                {
                    bool result = RegisterHotKey(_windowHandle, START_HOTKEY_ID, _lastStartModifiers, (uint)_startVirtualKey);
                    _logger.LogDebug("HotkeyService", $"恢复开始热键注册: {result}");
                }
                if (_stopHotkeyRegistered && _currentMode == HotkeyMode.Different)
                {
                    bool result = RegisterHotKey(_windowHandle, STOP_HOTKEY_ID, _lastStopModifiers, (uint)_stopVirtualKey);
                    _logger.LogDebug("HotkeyService", $"恢复停止热键注册: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "恢复热键注册时发生错误", ex);
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
                
                _logger.LogDebug("HotkeyService", "清理现有热键注册");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "清理热键注册时发生错误", ex);
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
                _logger.LogError("HotkeyService", "检查鼠标按键状态异常", ex);
                return false;
            }
        }

        // 修改 StopKeyMapping 方法
        private void StopKeyMapping()
        {
            try
            {
                _logger.LogDebug("HotkeyService", 
                    $"[StopKeyMapping] 开始停止按键映射 - " +
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
                
                _logger.LogDebug("HotkeyService", "[StopKeyMapping] 按键映射已停止，所有状态已重置");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "[StopKeyMapping] 停止按键映射异常", ex);
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

                    switch ((int)wParam)
                    {
                        case WM_XBUTTONDOWN:
                            MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
                            int xButton = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
                            DDKeyCode xButtonCode = xButton == 1 ? DDKeyCode.XBUTTON1 : DDKeyCode.XBUTTON2;
                            
                            _logger.LogDebug("HotkeyService", $"全局鼠标钩子捕获到侧键: {xButtonCode}");
                            HandleMouseButtonMessage(xButtonCode);
                            break;

                        case WM_MBUTTONDOWN:
                            _logger.LogDebug("HotkeyService", "全局鼠标钩子捕获到中键");
                            HandleMouseButtonMessage(DDKeyCode.MBUTTON);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("HotkeyService", "鼠标钩子回调异常", ex);
                }
            }
            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        // 添加鼠标钩子结构体
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
                _logger.LogDebug("HotkeyService", 
                    $"[OnModeSwitched] 开始处理模式切换 - " +
                    $"目标模式: {(isSequenceMode ? "顺序模式" : "按压模式")}, " +
                    $"当前态: 已启动({_isStarted}), 序列运行({_isSequenceRunning})");

                // 停止当前运行的序列
                StopSequence();
                
                // 保存当前模式的热键配置
                if (!isSequenceMode) // 从顺序模式切换到按压模式
                {
                    // 保存顺序模式配置和模式类型
                    var previousMode = _currentMode;  // 保存切换前的模式类型
                    
                    if (_currentMode == HotkeyMode.Same)
                    {
                        // Same模式下，开始键和停止键相同
                        _sequenceModeStartKey = _pendingStartKey;
                        _sequenceModeStartMods = _pendingStartMods;
                        _sequenceModeStopKey = _pendingStartKey;  // 使用相同的键
                        _sequenceModeStopMods = _pendingStartMods;
                    }
                    else
                    {
                        // Different模式下，保持原有的停止键
                        _sequenceModeStartKey = _pendingStartKey;
                        _sequenceModeStartMods = _pendingStartMods;
                        if (_pendingStopKey.HasValue)
                        {
                            _sequenceModeStopKey = _pendingStopKey;
                            _sequenceModeStopMods = _pendingStopMods;
                        }
                    }
                    
                    _logger.LogDebug("HotkeyService", 
                        $"[OnModeSwitched] 保存顺序模式配置 - " +
                        $"开始键: {_sequenceModeStartKey}, " +
                        $"停止键: {_sequenceModeStopKey}, " +
                        $"模式: {previousMode}");  // 使用保存的模式类型
                }
                else // 从按压模式切换到顺序模式
                {
                    // 保存按压模式的配置
                    _holdModeKey = _pendingStartKey;
                    _holdModeMods = _pendingStartMods;
                    
                    _logger.LogDebug("HotkeyService", 
                        $"[OnModeSwitched] 保存按压模式配置 - " +
                        $"按压键: {_holdModeKey}");
                    
                    // 如果在按压模式下修改过启动键，需要更新顺序模式的配置
                    if (_holdModeKey.HasValue && _holdModeKey != _sequenceModeStartKey)
                    {
                        _sequenceModeStartKey = _holdModeKey;
                        _sequenceModeStartMods = _holdModeMods;
                        
                        // 根据当前的启动键和停止键关系确定模式
                        if (_sequenceModeStopKey.HasValue)
                        {
                            bool isSameKey = _sequenceModeStartKey.Value == _sequenceModeStopKey.Value;
                            if (isSameKey)
                            {
                                // 如果启动键和停止键相同，切换到Same模式
                                _currentMode = HotkeyMode.Same;
                                _sequenceModeStopKey = _sequenceModeStartKey;
                                _sequenceModeStopMods = _sequenceModeStartMods;
                            }
                            else
                            {
                                // 如启动键和停止键不同，保持Different模式
                                _currentMode = HotkeyMode.Different;
                            }
                        }
                        else
                        {
                            // 如果没有停止键配置，默认为Same模式
                            _currentMode = HotkeyMode.Same;
                            _sequenceModeStopKey = _sequenceModeStartKey;
                            _sequenceModeStopMods = _sequenceModeStartMods;
                        }
                        
                        _logger.LogDebug("HotkeyService", 
                            $"[OnModeSwitched] 按压模式启动键变更，更新顺序模式配置 - " +
                            $"新模式: {_currentMode}, " +
                            $"开始键: {_sequenceModeStartKey.Value}, " +
                            $"停止键: {_sequenceModeStopKey}");
                    }
                }

                // 取消注册所有热键
                UnregisterAllHotkeys();

                if (isSequenceMode)
                {
                    // 切换顺序模式，先从配置中读取热键
                    var config = AppConfigService.Config;
                    
                    // 如果内存中没有保存的热键配置，从配置文件中读取
                    if (!_sequenceModeStartKey.HasValue && config.startKey != DDKeyCode.None)
                    {
                        _sequenceModeStartKey = config.startKey;
                        _sequenceModeStartMods = config.startMods;
                        _logger.LogDebug("HotkeyService", 
                            $"[OnModeSwitched] 从配置读取开始键 - " +
                            $"键码: {_sequenceModeStartKey.Value}");
                    }
                    
                    if (!_sequenceModeStopKey.HasValue && config.stopKey != DDKeyCode.None)
                    {
                        _sequenceModeStopKey = config.stopKey;
                        _sequenceModeStopMods = config.stopMods;
                        _logger.LogDebug("HotkeyService", 
                            $"[OnModeSwitched] 从配置读取停止键 - " +
                            $"键码: {_sequenceModeStopKey.Value}");
                    }

                    // 确定是Same还是Different模式
                    if (_sequenceModeStartKey.HasValue)
                    {
                        if (_sequenceModeStopKey.HasValue)
                        {
                            bool isSameKey = _sequenceModeStartKey.Value == _sequenceModeStopKey.Value;
                            _currentMode = isSameKey ? HotkeyMode.Same : HotkeyMode.Different;
                        }
                        else
                        {
                            // 如果没有停止键配置，默认为Same模式
                            _currentMode = HotkeyMode.Same;
                            _sequenceModeStopKey = _sequenceModeStartKey;
                            _sequenceModeStopMods = _sequenceModeStartMods;
                        }
                        
                        _logger.LogDebug("HotkeyService", 
                            $"[OnModeSwitched] 确定顺序模式类型 - " +
                            $"模式: {_currentMode}, " +
                            $"开始键: {_sequenceModeStartKey.Value}, " +
                            $"停止键: {_sequenceModeStopKey}");
                    }

                    // 修改热键注册逻辑
                    if (_sequenceModeStartKey.HasValue && _isWindowInitialized)
                    {
                        RegisterStartHotkeyInternal(_sequenceModeStartKey.Value, _sequenceModeStartMods);
                        
                        if (_currentMode == HotkeyMode.Different && _sequenceModeStopKey.HasValue)
                        {
                            RegisterStopHotkey(_sequenceModeStopKey.Value, _sequenceModeStopMods);
                        }
                    }
                    else if (_sequenceModeStartKey.HasValue)
                    {
                        // 如果窗口未初始化，添加到初始化事件中
                        _mainWindow.SourceInitialized += (s, e) =>
                        {
                            RegisterStartHotkeyInternal(_sequenceModeStartKey.Value, _sequenceModeStartMods);
                            
                            if (_currentMode == HotkeyMode.Different && _sequenceModeStopKey.HasValue)
                            {
                                RegisterStopHotkey(_sequenceModeStopKey.Value, _sequenceModeStopMods);
                            }
                        };
                    }
                }
                else
                {
                    // 切换到按压模式，先从配置中读取热键
                    var config = AppConfigService.Config;
                    
                    // 如果内存中没有保存的热键配置，从配置文件中读取
                    if (!_holdModeKey.HasValue && config.startKey != DDKeyCode.None)
                    {
                        _holdModeKey = config.startKey;
                        _holdModeMods = config.startMods;
                        _logger.LogDebug("HotkeyService", 
                            $"[OnModeSwitched] 从配置读取按压键 - " +
                            $"键码: {_holdModeKey.Value}");
                    }

                    // 修改热键注册逻辑
                    if (_holdModeKey.HasValue && _isWindowInitialized)
                    {
                        RegisterStartHotkeyInternal(_holdModeKey.Value, _holdModeMods);
                    }
                    else if (_holdModeKey.HasValue)
                    {
                        // 如果窗口未初始化，添加到初始化事件中
                        _mainWindow.SourceInitialized += (s, e) =>
                        {
                            RegisterStartHotkeyInternal(_holdModeKey.Value, _holdModeMods);
                        };
                    }
                }

                // 更新置
                AppConfigService.UpdateConfig(config =>
                {
                    config.keyMode = isSequenceMode ? 0 : 1;
                    
                    // 保存当前模式的热键配置
                    if (isSequenceMode)
                    {
                        if (_sequenceModeStartKey.HasValue)
                        {
                            config.startKey = _sequenceModeStartKey.Value;
                            config.startMods = _sequenceModeStartMods;
                        }
                        if (_sequenceModeStopKey.HasValue)
                        {
                            config.stopKey = _sequenceModeStopKey.Value;
                            config.stopMods = _sequenceModeStopMods;
                        }
                    }
                    else
                    {
                        if (_holdModeKey.HasValue)
                        {
                            config.startKey = _holdModeKey.Value;
                            config.startMods = _holdModeMods;
                        }
                    }
                });

                _logger.LogDebug("HotkeyService", 
                    $"[OnModeSwitched] 模式切换完成 - " +
                    $"模式: {(isSequenceMode ? "顺序模式" : "按压模式")}, " +
                    $"热键模式: {(isSequenceMode ? _currentMode : _sequenceModeStartKey == _sequenceModeStopKey ? HotkeyMode.Same : HotkeyMode.Different)}");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "[OnModeSwitched] 处理模式切换时发生异常", ex);
                try
                {
                    RestoreHotkeys();
                }
                catch (Exception restoreEx)
                {
                    _logger.LogError("HotkeyService", "[OnModeSwitched] 恢复热键失败", restoreEx);
                }
            }
        }

        // 不触发模式切换的热键注册
        private void RegisterPendingHotkeysWithoutModeSwitch()
        {
            try
            {
                _logger.LogDebug("HotkeyService", "[RegisterPendingHotkeys] 开始注册待理的热键");

                bool startSuccess = true;
                bool stopSuccess = true;

                if (_pendingStartKey.HasValue)
                {
                    startSuccess = RegisterStartHotkey(_pendingStartKey.Value, _pendingStartMods);
                    _logger.LogDebug("HotkeyService", 
                        $"[RegisterPendingHotkeys] 注册开始热键 - " +
                        $"键码: {_pendingStartKey.Value}, " +
                        $"结果: {(startSuccess ? "成功" : "失败")}");
                }

                if (_pendingStopKey.HasValue && _currentMode == HotkeyMode.Different)
                {
                    stopSuccess = RegisterStopHotkey(_pendingStopKey.Value, _pendingStopMods);
                    _logger.LogDebug("HotkeyService", 
                        $"[RegisterPendingHotkeys] 注册停止热键 - " +
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
                _logger.LogError("HotkeyService", "[RegisterPendingHotkeys] 注册待处理热键时发生错误", ex);
            }
        }

        // 取消注册所有热键
        private void UnregisterAllHotkeys()
        {
            try
            {
                _logger.LogDebug("HotkeyService", "开始取消注册所有热键");
                
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
                _logger.LogDebug("HotkeyService", "所有热键已取消注册");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "取消注册热键时发生异常", ex);
            }
        }

        // 添加启动序列的方法
        private void StartSequence()
        {
            try
            {
                _logger.LogDebug("HotkeyService", "开始启动序列...");
                
                // 确保序列已停止
                StopSequence();
                
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
                
                _logger.LogDebug("HotkeyService", "序列已启动");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "启动序列时发生异常", ex);
                // 出错时重置状态
                _isSequenceRunning = false;
                _isStarted = false;
            }
        }

        // 修改 RegisterStartHotkey 方法
        private bool RegisterStartHotkeyInternal(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            try
            {
                _logger.LogDebug("HotkeyService", 
                    $"[RegisterStartHotkey] 开始注册开始热键 - " +
                    $"键码: {ddKeyCode}, " +
                    $"修饰键: {modifiers}, " +
                    $"停止键: {_pendingStopKey}, " +
                    $"当前状态: 已启动({_isStarted}), 序列运行({_isSequenceRunning})");

                // 1. 检查窗口是否已初始化
                if (!_isWindowInitialized)
                {
                    _logger.LogDebug("HotkeyService", "[RegisterStartHotkey] 窗口未初始化，保存待处理的热键");
                    return false;
                }

                // 2. 获取主热键虚拟键码和修饰键标志
                _startVirtualKey = GetVirtualKeyFromDDKey(ddKeyCode);
                if (_startVirtualKey == 0)
                {
                    _logger.LogError("HotkeyService", $"[RegisterStartHotkey] 无效的虚拟键码: {ddKeyCode}");
                    return false;
                }

                uint modifierFlags = ConvertToModifierFlags(modifiers);
                _lastStartModifiers = modifierFlags;

                // 3. 提前确定模式并更新配置
                bool isSameKeyMode = _pendingStopKey.HasValue && _pendingStopKey.Value == ddKeyCode;
                _currentMode = isSameKeyMode ? HotkeyMode.Same : HotkeyMode.Different;

                _logger.LogDebug("HotkeyService", 
                    $"[RegisterStartHotkey] 模式已确定: {_currentMode}, " +
                    $"配置已更新");

                // 4. 如果是鼠标按键，不需要实际注册热键
                if (IsMouseButton(ddKeyCode))
                {
                    _startHotkeyRegistered = true;
                    _logger.LogDebug("HotkeyService", "[RegisterStartHotkey] 鼠标按键无需注册系统热键");
                    return true;
                }

                // 5. 注册系统热键
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
                    _logger.LogDebug("HotkeyService", 
                        $"[RegisterStartHotkey] 热键注册成功 - " +
                        $"ID: {START_HOTKEY_ID}, " +
                        $"VK: 0x{_startVirtualKey:X}, " +
                        $"Mods: 0x{_lastStartModifiers:X}");
                }
                else
                {
                    _logger.LogError("HotkeyService", 
                        $"[RegisterStartHotkey] 热键注册失败 - " +
                        $"LastError: {Marshal.GetLastWin32Error()}");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "[RegisterStartHotkey] 注册开始热键异常", ex);
                return false;
            }
        }

        // 修改按压模式的按键处理方法
        private void HandleHoldModeKeyPress()
        {
            try
            {
                _logger.LogDebug("HotkeyService", "[HandleHoldModeKeyPress] 处理按压模式按键按下");

                if (_isSequenceRunning)
                {
                    _logger.LogDebug("HotkeyService", "[HandleHoldModeKeyPress] 序列已在运行中，忽略按键按下");
                    return;
                }

                // 检查按键列表是否为空
                if (_keyList == null || _keyList.Count == 0)
                {
                    _logger.LogWarning("HotkeyService", "[HandleHoldModeKeyPress] 按键列表为空，无法启动序列");
                    return;
                }

                // 触发按键按下事件
                StartHotkeyPressed?.Invoke();

                // 创建新的取消令牌
                _sequenceCts?.Cancel();
                _sequenceCts?.Dispose();
                _sequenceCts = new CancellationTokenSource();

                // 启动序列
                _isSequenceRunning = true;
                _ddDriverService.SetHoldMode(true);

                // 在后台开始循环执行按键序列
                Task.Run(async () =>
                {
                    try
                    {
                        while (!_sequenceCts.Token.IsCancellationRequested)
                        {
                            foreach (var key in _keyList)
                            {
                                if (_sequenceCts.Token.IsCancellationRequested)
                                    break;

                                try
                                {
                                    // 触发按键事件
                                    KeyTriggered?.Invoke(key);
                                    // 模拟按键
                                    await Task.Run(() => _ddDriverService.SimulateKeyPress(key));
                                    // 等待指定间隔
                                    await Task.Delay(_ddDriverService.KeyInterval, _sequenceCts.Token);
                                }
                                catch (OperationCanceledException)
                                {
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError("HotkeyService", $"[HandleHoldModeKeyPress] 模拟按键异常: {key}", ex);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常取消，不需要处理
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("HotkeyService", "[HandleHoldModeKeyPress] 按键序列循环异常", ex);
                    }
                }, _sequenceCts.Token);

                _logger.LogDebug("HotkeyService", "[HandleHoldModeKeyPress] 按压模式已启动");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "[HandleHoldModeKeyPress] 处理按压模式按键按下异常", ex);
                // 发生异常时重置状态
                _isSequenceRunning = false;
                _ddDriverService.SetHoldMode(false);
                _sequenceCts?.Cancel();
            }
        }

        private void HandleHoldModeKeyRelease()
        {
            try
            {
                _logger.LogDebug("HotkeyService", "[HandleHoldModeKeyRelease] 处理按压模式按键释放");

                if (!_isSequenceRunning)
                {
                    _logger.LogDebug("HotkeyService", "[HandleHoldModeKeyRelease] 序列未运行，忽略按键释放");
                    return;
                }

                // 取消序列任务
                _sequenceCts?.Cancel();

                // 停止驱动服务
                _ddDriverService.SetHoldMode(false);
                _isSequenceRunning = false;

                // 触发按键释放事件
                StopHotkeyPressed?.Invoke();

                _logger.LogDebug("HotkeyService", "[HandleHoldModeKeyRelease] 按压模式已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "[HandleHoldModeKeyRelease] 处理按压模式按键释放异常", ex);
                // 确保状态被重置
                _isSequenceRunning = false;
                _ddDriverService.SetHoldMode(false);
                _sequenceCts?.Cancel();
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
                                    _logger.LogDebug("HotkeyService", $"[KeyboardHook] 检测到启动键释放 - VK: 0x{hookStruct.vkCode:X}");
                                    HandleHoldModeKeyRelease();
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("HotkeyService", "键盘钩子回调异常", ex);
                }
            }
            return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }
    }
}