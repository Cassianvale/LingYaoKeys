using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.ComponentModel;
using WpfApp.ViewModels;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;

// 提供快捷键服务
namespace WpfApp.Services.Core
{
    public class HotkeyService
    {
        // Win32 API 函数
        // 统一的钩子回调委托
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        // 统一的钩子安装函数
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        // 释放钩子
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);  

        // 调用下一个钩子
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // 获取模块句柄
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // 获取前台窗口句柄
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        // 检查窗口是否有效
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        // Windows消息常量
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_MOUSEWHEEL = 0x020A;

        // 事件
        public event Action? StartHotkeyPressed;  // 启动热键按下事件
        public event Action? StartHotkeyReleased;  // 启动热键释放事件
        public event Action? StopHotkeyPressed;  // 停止热键按下事件
        public event Action? SequenceModeStarted;  // 序列模式开始事件
        public event Action? SequenceModeStopped;  // 序列模式停止事件
        public event Action<LyKeysCode>? KeyTriggered;  // 触发按键事件

        // 核心字段
        private readonly LyKeysService _lyKeysService;
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly MainViewModel _mainViewModel;
        private readonly Window _mainWindow;
        private List<LyKeysCode> _keyList = new List<LyKeysCode>();
        private List<KeyItemSettings> _keySettings = new List<KeyItemSettings>();

        // 热键状态
        private int _startVirtualKey;  // 启动热键虚拟键码
        private int _stopVirtualKey;  // 停止热键虚拟键码
        private LyKeysCode? _pendingStartKey;  // LyKeys启动键键码
        private LyKeysCode? _pendingStopKey;  // LyKeys停止键键码
        private bool _isKeyHeld;    // 防止全局热键的重复触发
        private bool _isSequenceRunning;  // 序列模式是否正在运行
        private bool _isInputFocused;  // 输入焦点是否在当前窗口

        // 保持回调函数的引用
        private readonly HookProc _mouseProcDelegate;  // 鼠标钩子回调函数
        private readonly HookProc _keyboardProcDelegate;  // 键盘钩子回调函数
        private IntPtr _mouseHookHandle;  // 鼠标钩子句柄
        private IntPtr _keyboardHookHandle;  // 键盘钩子句柄
        private readonly object _hookLock = new object();  // 钩子锁

        private IntPtr _targetWindowHandle;
        private bool _isTargetWindowActive;

        // 窗口状态枚举
        private enum WindowState
        {
            NoTargetWindow,      // 未选择目标窗口
            ProcessNotRunning,   // 进程未运行
            WindowInvalid,       // 窗口无效
            WindowInactive,      // 窗口未激活
            WindowActive         // 窗口激活
        }

        // 构造函数
        public HotkeyService(Window mainWindow, LyKeysService lyKeysService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
            _mainViewModel = mainWindow.DataContext as MainViewModel ??
                throw new ArgumentException("Window.DataContext must be of type MainViewModel", nameof(mainWindow));

            // 初始化回调委托
            _mouseProcDelegate = MouseHookCallback;
            _keyboardProcDelegate = KeyboardHookCallback;

            // 订阅模式切换事件（仅在真正切换模式时触发）
            _lyKeysService.ModeSwitched += OnModeSwitched;

            // 从配置加载初始状态
            LoadInitialState();

            // 安装钩子
            InstallHooks();

            // 窗口关闭时清理资源
            _mainWindow.Closed += (s, e) => Dispose();
        }

        // 加载初始状态
        private void LoadInitialState()
        {
            var config = AppConfigService.Config;

            // 加载按键列表
            if (config.keys?.Count > 0)
            {
                // 只获取选中的按键
                var selectedKeys = config.keys
                    .Where(k => k.IsSelected)
                    .Select(k => k.Code)
                    .ToList();

                if (selectedKeys.Count > 0)
                {
                    _keyList = selectedKeys;
                    _lyKeysService.SetKeyList(selectedKeys);
                }
            }

            // 直接设置模式，不触发事件
            _lyKeysService.IsHoldMode = config.keyMode != 0;
        }

        // 释放资源
        public void Dispose()
        {
            lock (_hookLock)
            {
                // 停止序列
                StopSequence();

                // 移除事件订阅
                _lyKeysService.ModeSwitched -= OnModeSwitched;

                // 卸载钩子
                if (_keyboardHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_keyboardHookHandle);
                    _keyboardHookHandle = IntPtr.Zero;
                }

                if (_mouseHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookHandle);
                    _mouseHookHandle = IntPtr.Zero;
                }
            }
        }

        // 注册开始热键
        public bool RegisterStartHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
        {
            try
            {
                _startVirtualKey = GetVirtualKeyFromLyKey(keyCode);  // 转换为Windows虚拟键码
                _pendingStartKey = keyCode;                          // 保存LyKeys按键码
                SaveHotkeyConfig(true, keyCode, modifiers);         // 保存到配置文件
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("注册开始热键失败", ex);
                return false;
            }
        }

        // 注册停止热键
        public bool RegisterStopHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
        {
            try
            {
                _stopVirtualKey = GetVirtualKeyFromLyKey(keyCode);  // 转换为Windows虚拟键码
                _pendingStopKey = keyCode;                          // 保存LyKeys按键码
                SaveHotkeyConfig(false, keyCode, modifiers);        // 保存到配置文件
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("注册停止热键失败", ex);
                return false;
            }
        }

        // 保存热键配置
        private void SaveHotkeyConfig(bool isStart, LyKeysCode keyCode, ModifierKeys modifiers)
        {
            AppConfigService.UpdateConfig(config =>
            {
                if (isStart)
                {
                    config.startKey = keyCode;
                    config.startMods = modifiers;
                }
                else
                {
                    config.stopKey = keyCode;
                    config.stopMods = modifiers;
                }
            });
        }

        // 启动序列控制
        public void StartSequence()
        {
            try
            {
                _logger.Debug("开始启动按键序列");
                
                // 使用新的窗口状态检查
                if (!CanTriggerHotkey())
                {
                    return;
                }

                if (_keyList.Count == 0)
                {
                    _logger.Warning("按键列表为空，无法启动序列");
                    _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                    return;
                }

                _isSequenceRunning = true;
                _logger.Debug($"序列运行状态已设置为: {_isSequenceRunning}");

                if (!_lyKeysService.IsHoldMode)
                {
                    _logger.Debug("当前为顺序模式，启动LyKeysService");
                    _lyKeysService.IsEnabled = true;
                }
                else
                {
                    _logger.Debug("当前为按压模式，设置并启动LyKeysService");
                    _lyKeysService.IsHoldMode = true;
                    _lyKeysService.IsEnabled = true;
                }

                _logger.Debug($"序列已启动 - 模式: {(_lyKeysService.IsHoldMode ? "按压" : "顺序")}, " +
                             $"按键数: {_keyList.Count}, 使用独立按键间隔设置, " +
                             $"目标窗口句柄: {_targetWindowHandle}");
                
                SequenceModeStarted?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.Error("启动序列时发生异常", ex);
                _isSequenceRunning = false;
                _lyKeysService.IsEnabled = false;
                _mainViewModel.UpdateStatusMessage("启动序列失败，请检查日志", true);
            }
        }

        // 停止序列控制
        public void StopSequence()
        {
            if (!_isSequenceRunning) return;

            _isSequenceRunning = false;
            _lyKeysService.IsEnabled = false;

            if (_lyKeysService.IsHoldMode)
            {
                _lyKeysService.IsHoldMode = false;
            }

            SequenceModeStopped?.Invoke();
        }

        // 键盘钩子回调处理
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_isInputFocused)
            {
                try
                {
                    var hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT))!;
                    bool isStartKey = hookStruct.vkCode == _startVirtualKey;
                    bool isStopKey = hookStruct.vkCode == _stopVirtualKey;

                    // 获取当前窗口状态
                    var windowState = GetWindowState();
                    
                    // 如果序列正在运行，但窗口状态异常，则停止序列
                    if (_isSequenceRunning && windowState != WindowState.WindowActive && 
                        windowState != WindowState.NoTargetWindow)
                    {
                        _lyKeysService.EmergencyStop();
                        StopSequence();
                        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
                    }

                    // 如果是热键，检查是否可以触发
                    if ((isStartKey || isStopKey) && !CanTriggerHotkey())
                    {
                        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
                    }

                    // 处理热键
                    if (isStartKey || isStopKey)
                    {
                        if (_lyKeysService.IsHoldMode)
                        {
                            // 按压模式处理逻辑
                            if (isStartKey)
                            {
                                switch ((int)wParam)
                                {
                                    case WM_KEYDOWN:
                                    case WM_SYSKEYDOWN:
                                        if (!_isKeyHeld)
                                        {
                                            _isKeyHeld = true;
                                            StartHotkeyPressed?.Invoke();
                                            StartSequence();
                                        }
                                        return new IntPtr(1);

                                    case WM_KEYUP:
                                    case WM_SYSKEYUP:
                                        if (_isKeyHeld)
                                        {
                                            _isKeyHeld = false;
                                            StopHotkeyPressed?.Invoke();
                                            StopSequence();
                                        }
                                        return new IntPtr(1);
                                }
                            }
                        }
                        else
                        {
                            // 顺序模式处理逻辑
                            switch ((int)wParam)
                            {
                                case WM_KEYDOWN:
                                case WM_SYSKEYDOWN:
                                    if (!_isKeyHeld)
                                    {
                                        _isKeyHeld = true;
                                        if (_isSequenceRunning)
                                        {
                                            if (isStopKey || (_startVirtualKey == _stopVirtualKey && isStartKey))
                                            {
                                                StopHotkeyPressed?.Invoke();
                                                StopSequence();
                                            }
                                        }
                                        else if (isStartKey)
                                        {
                                            StartHotkeyPressed?.Invoke();
                                            StartSequence();
                                        }
                                    }
                                    break;

                                case WM_KEYUP:
                                case WM_SYSKEYUP:
                                    if (_isKeyHeld)
                                    {
                                        _isKeyHeld = false;
                                        if (isStartKey)
                                        {
                                            StartHotkeyReleased?.Invoke();
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("键盘钩子回调异常", ex);
                    _isKeyHeld = false;
                    StopSequence();
                }
            }
            return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

        // 鼠标钩子回调处理
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_isInputFocused)
            {
                try
                {
                    // 获取当前活动窗口
                    IntPtr activeWindow = GetForegroundWindow();
                    // 修改判断逻辑：如果没有设置目标窗口，则允许在任何窗口触发
                    bool isTargetWindowActive = _targetWindowHandle == IntPtr.Zero || activeWindow == _targetWindowHandle;

                    // 如果目标窗口未激活，停止当前执行
                    if (!isTargetWindowActive && _isSequenceRunning)
                    {
                        _lyKeysService.EmergencyStop(); // 使用紧急停止
                        StopSequence();
                        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
                    }

                    var hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
                    int wParamInt = (int)wParam;

                    // 如果是鼠标热键且窗口未激活，直接返回
                    if ((wParamInt == WM_XBUTTONDOWN || wParamInt == WM_MBUTTONDOWN || 
                         wParamInt == WM_XBUTTONUP || wParamInt == WM_MBUTTONUP || 
                         wParamInt == WM_MOUSEWHEEL) && !isTargetWindowActive)
                    {
                        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
                    }

                    switch (wParamInt)
                    {
                        case WM_XBUTTONDOWN:
                        case WM_MBUTTONDOWN:
                            HandleMouseButtonDown(wParamInt, hookStruct);
                            break;

                        case WM_XBUTTONUP:
                        case WM_MBUTTONUP:
                            HandleMouseButtonUp(wParamInt, hookStruct);
                            break;

                        case WM_MOUSEWHEEL:
                            HandleMouseWheel(hookStruct);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("鼠标钩子回调异常", ex);
                    _isKeyHeld = false;
                    StopSequence();
                }
            }
            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        // 处理鼠标按键按下
        private void HandleMouseButtonDown(int wParam, MSLLHOOKSTRUCT hookStruct)
        {
            LyKeysCode buttonCode = GetMouseButtonCode(wParam, hookStruct);
            bool isStartKey = buttonCode == _pendingStartKey;
            bool isStopKey = buttonCode == _pendingStopKey;

            if (isStartKey || isStopKey)
            {
                if (_lyKeysService.IsHoldMode)
                {
                    if (isStartKey && !_isKeyHeld)
                    {
                        _isKeyHeld = true;
                        StartHotkeyPressed?.Invoke();
                        StartSequence();
                    }
                }
                else
                {
                    if (!_isKeyHeld)
                    {
                        _isKeyHeld = true;
                        if (_isSequenceRunning)
                        {
                            if (isStopKey || (_pendingStartKey == _pendingStopKey && isStartKey))
                            {
                                StopHotkeyPressed?.Invoke();
                                StopSequence();
                            }
                        }
                        else if (isStartKey)
                        {
                            StartHotkeyPressed?.Invoke();
                            StartSequence();
                        }
                    }
                }
            }
        }

        // 处理鼠标按键释放
        private void HandleMouseButtonUp(int wParam, MSLLHOOKSTRUCT hookStruct)
        {
            LyKeysCode buttonCode = GetMouseButtonCode(wParam, hookStruct);
            bool isStartKey = buttonCode == _pendingStartKey;

            if (_lyKeysService.IsHoldMode)
            {
                if (isStartKey && _isKeyHeld)
                {
                    _isKeyHeld = false;
                    StopHotkeyPressed?.Invoke();
                    StopSequence();
                }
            }
            else
            {
                if (_isKeyHeld)
                {
                    _isKeyHeld = false;
                    if (isStartKey)
                    {
                        StartHotkeyReleased?.Invoke();
                    }
                }
            }
        }

        // 处理滚轮事件
        private void HandleMouseWheel(MSLLHOOKSTRUCT hookStruct)
        {
            // 获取滚轮方向（向上为正，向下为负）
            short wheelDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
            LyKeysCode buttonCode = wheelDelta > 0 ? LyKeysCode.VK_WHEELUP : LyKeysCode.VK_WHEELDOWN;

            bool isStartKey = buttonCode == _pendingStartKey;
            bool isStopKey = buttonCode == _pendingStopKey;

            if (isStartKey || isStopKey)
            {
                if (_lyKeysService.IsHoldMode)
                {
                    if (isStartKey)
                    {
                        if (_isSequenceRunning)
                        {
                            _isKeyHeld = false;
                            StopHotkeyPressed?.Invoke();
                            StopSequence();
                        }
                        else if (!_isKeyHeld)
                        {
                            _isKeyHeld = true;
                            StartHotkeyPressed?.Invoke();
                            StartSequence();
                        }
                    }
                }
                else
                {
                    if (!_isKeyHeld)
                    {
                        _isKeyHeld = true;
                        if (_isSequenceRunning)
                        {
                            if (isStopKey || (_pendingStartKey == _pendingStopKey && isStartKey))
                            {
                                StopHotkeyPressed?.Invoke();
                                StopSequence();
                            }
                        }
                        else if (isStartKey)
                        {
                            StartHotkeyPressed?.Invoke();
                            StartSequence();
                        }
                    }
                }
            }

            // 重置按键状态（因为滚轮事件是即时的）
            if (_isKeyHeld)
            {
                _isKeyHeld = false;
                if (isStartKey)
                {
                    StartHotkeyReleased?.Invoke();
                }
            }
        }

        // 获取鼠标按键代码
        private LyKeysCode GetMouseButtonCode(int wParam, MSLLHOOKSTRUCT hookStruct)
        {
            if (wParam == WM_MBUTTONDOWN || wParam == WM_MBUTTONUP)
            {
                return LyKeysCode.VK_MBUTTON;
            }
            else if (wParam == WM_MOUSEWHEEL)
            {
                short wheelDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                return wheelDelta > 0 ? LyKeysCode.VK_WHEELUP : LyKeysCode.VK_WHEELDOWN;
            }

            int xButton = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
            return xButton == 1 ? LyKeysCode.VK_XBUTTON1 : LyKeysCode.VK_XBUTTON2;
        }

        // 工具方法
        private int GetVirtualKeyFromLyKey(LyKeysCode lyKeyCode)
        {
            // 直接使用LyKeysCode的值作为虚拟键码
            return (int)lyKeyCode;
        }

        // 模式切换事件处理
        private void OnModeSwitched(object? sender, bool isHoldMode)
        {
            StopSequence();

            // 重新注册热键
            if (_pendingStartKey.HasValue)
            {
                RegisterStartHotkey(_pendingStartKey.Value, ModifierKeys.None);
                if (!isHoldMode && _pendingStopKey.HasValue)
                {
                    RegisterStopHotkey(_pendingStopKey.Value, ModifierKeys.None);
                }
            }
        }

        // 输入焦点控制
        public bool IsInputFocused
        {
            get => _isInputFocused;
            set => _isInputFocused = value;
        }

        // 结构体定义
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

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

        // 设置按键序列
        public void SetKeySequence(List<KeyItemSettings> keySettings)
        {
            // 仅保存按键码列表
            _keyList = keySettings.Select(k => k.KeyCode).ToList();
            
            // 保存按键设置，包括每个按键的间隔
            _keySettings = keySettings.ToList();
            
            _logger.Debug($"设置按键序列: 按键数={keySettings.Count}, 使用独立按键间隔");
        }

        // 旧方法保留用于兼容现有代码，但标记为弃用
        [Obsolete("请使用接受KeyItemSettings列表的重载")]
        public void SetKeySequence(List<LyKeysCode> keys, int interval)
        {
            // 转换为新的格式调用新方法
            var settings = keys.Select(k => new KeyItemSettings 
            { 
                KeyCode = k, 
                Interval = interval 
            }).ToList();
            
            SetKeySequence(settings);
        }

        // 判断是否为鼠标按键
        public bool IsMouseButton(LyKeysCode keyCode)
        {
            return keyCode == LyKeysCode.VK_LBUTTON ||
                   keyCode == LyKeysCode.VK_RBUTTON ||
                   keyCode == LyKeysCode.VK_MBUTTON ||
                   keyCode == LyKeysCode.VK_XBUTTON1 ||
                   keyCode == LyKeysCode.VK_XBUTTON2;
        }

        // 添加钩子安装方法
        private void InstallHooks()
        {
            try
            {
                using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule!)
                {
                    IntPtr hModule = GetModuleHandle(curModule.ModuleName);
                    
                    // 安装键盘钩子
                    _keyboardHookHandle = SetWindowsHookEx(
                        WH_KEYBOARD_LL,
                        _keyboardProcDelegate,
                        hModule,
                        0);

                    // 安装鼠标钩子
                    _mouseHookHandle = SetWindowsHookEx(
                        WH_MOUSE_LL,
                        _mouseProcDelegate,
                        hModule,
                        0);

                    if (_keyboardHookHandle == IntPtr.Zero || _mouseHookHandle == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                _logger.Debug("成功安装键盘和鼠标钩子");
            }
            catch (Exception ex)
            {
                _logger.Error("安装钩子失败", ex);
                throw;
            }
        }

        // 目标窗口句柄
        public IntPtr TargetWindowHandle
        {
            get => _targetWindowHandle;
            set
            {
                if (_targetWindowHandle != value)
                {
                    _targetWindowHandle = value;
                    _logger.Debug($"热键服务窗口句柄已更新: {value}");
                    
                    // 如果句柄变为0，停止当前执行
                    if (value == IntPtr.Zero && _isSequenceRunning)
                    {
                        _lyKeysService.EmergencyStop();
                        StopSequence();
                        _logger.Debug("目标窗口已关闭，停止当前执行");
                    }
                }
            }
        }

        // 获取目标窗口是否激活
        public bool IsTargetWindowActive
        {
            get => _isTargetWindowActive;
            set => _isTargetWindowActive = value;
        }

        // 获取窗口状态
        private WindowState GetWindowState()
        {
            try
            {
                // 1. 检查是否选择了窗口
                if (_targetWindowHandle == IntPtr.Zero && 
                    string.IsNullOrEmpty(_mainViewModel.KeyMappingViewModel.SelectedWindowProcessName))
                {
                    return WindowState.NoTargetWindow;
                }

                // 2. 检查进程是否运行
                if (_targetWindowHandle == IntPtr.Zero && 
                    !string.IsNullOrEmpty(_mainViewModel.KeyMappingViewModel.SelectedWindowProcessName))
                {
                    return WindowState.ProcessNotRunning;
                }

                // 3. 检查窗口是否有效
                if (!IsWindow(_targetWindowHandle))
                {
                    return WindowState.WindowInvalid;
                }

                // 4. 检查窗口是否激活
                IntPtr activeWindow = GetForegroundWindow();
                return activeWindow == _targetWindowHandle ? 
                    WindowState.WindowActive : WindowState.WindowInactive;
            }
            catch (Exception ex)
            {
                _logger.Error("检查窗口状态时发生异常", ex);
                return WindowState.WindowInvalid;
            }
        }

        // 判断是否可以触发热键
        private bool CanTriggerHotkey()
        {
            var state = GetWindowState();
            
            // 记录状态变化
            _logger.Debug($"当前窗口状态: {state}");
            
            switch (state)
            {
                case WindowState.NoTargetWindow:
                    return true; // 未选择窗口时允许全局触发
                    
                case WindowState.ProcessNotRunning:
                    _mainViewModel.UpdateStatusMessage("目标进程未运行，请等待程序启动", true);
                    return false;
                    
                case WindowState.WindowInvalid:
                    _mainViewModel.UpdateStatusMessage("目标窗口无效，请重新选择窗口", true);
                    return false;
                    
                case WindowState.WindowInactive:
                    _mainViewModel.UpdateStatusMessage("请先激活目标窗口", true);
                    return false;
                    
                case WindowState.WindowActive:
                    return true;
                    
                default:
                    _logger.Error($"未处理的窗口状态: {state}");
                    return false;
            }
        }
    }
}