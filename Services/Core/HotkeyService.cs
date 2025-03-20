using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.ComponentModel;
using WpfApp.ViewModels;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;
using WpfApp.Services.Models;

// 提供快捷键服务
namespace WpfApp.Services.Core;

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
    public event Action? StartHotkeyPressed; // 启动热键按下事件
    public event Action? StartHotkeyReleased; // 启动热键释放事件
    public event Action? SequenceModeStarted; // 序列模式开始事件
    public event Action? SequenceModeStopped; // 序列模式停止事件
    public event Action<LyKeysCode>? KeyTriggered; // 触发按键事件

    // 核心字段
    private readonly LyKeysService _lyKeysService;
    private readonly SerilogManager _logger = SerilogManager.Instance;
    private readonly MainViewModel _mainViewModel;
    private readonly Window _mainWindow;
    private List<LyKeysCode> _keyList = new();
    private List<KeyItemSettings> _keySettings = new();

    // 热键状态
    private int _hotkeyVirtualKey; // 热键虚拟键码 (简化为一个热键)
    private LyKeysCode? _pendingHotkey; // LyKeys热键键码 (简化为一个热键)
    private bool _isKeyHeld; // 防止全局热键的重复触发
    private bool _isSequenceRunning; // 序列模式是否正在运行
    private bool _isInputFocused; // 输入焦点是否在当前窗口
    private bool _isHotkeyControlEnabled = true; // 热键总开关状态
    private bool _isRegisteringHotkey = false; // 是否正在注册热键

    // 保持回调函数的引用
    private readonly HookProc _mouseProcDelegate; // 鼠标钩子回调函数
    private readonly HookProc _keyboardProcDelegate; // 键盘钩子回调函数
    private IntPtr _mouseHookHandle; // 鼠标钩子句柄
    private IntPtr _keyboardHookHandle; // 键盘钩子句柄
    private readonly object _hookLock = new(); // 钩子锁

    private IntPtr _targetWindowHandle;
    private bool _isTargetWindowActive;

    // 窗口状态枚举
    private enum WindowState
    {
        NoTargetWindow, // 未选择目标窗口
        ProcessNotRunning, // 进程未运行
        WindowInvalid, // 窗口无效
        WindowInactive, // 窗口未激活
        WindowActive // 窗口激活
    }

    // 构造函数
    public HotkeyService(Window mainWindow, LyKeysService lyKeysService)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
        _mainViewModel = mainWindow.DataContext as MainViewModel ??
                         throw new ArgumentException("Window.DataContext must be of type MainViewModel",
                             nameof(mainWindow));

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
            // 获取所有选中的按键和坐标
            var selectedItems = config.keys.Where(k => k.IsSelected).ToList();

            if (selectedItems.Count > 0)
            {
                // 创建统一操作列表
                var operations = new List<KeyItemSettings>();
                
                foreach (var item in selectedItems)
                {
                    if (item.Type == KeyItemType.Keyboard && item.Code.HasValue)
                    {
                        // 添加键盘操作
                        operations.Add(KeyItemSettings.CreateKeyboard(item.Code.Value, item.KeyInterval));
                    }
                    else if (item.Type == KeyItemType.Coordinates)
                    {
                        // 添加坐标操作
                        operations.Add(KeyItemSettings.CreateCoordinates(item.X, item.Y, item.KeyInterval));
                    }
                }
                
                // 设置统一操作列表
                SetKeySequence(operations);
                
                _logger.Debug($"初始化已加载操作列表 - 总数: {operations.Count}, 键盘按键: {operations.Count(o => o.Type == KeyItemType.Keyboard)}, 坐标操作: {operations.Count(o => o.Type == KeyItemType.Coordinates)}");
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

    // 注册热键 (简化为一个方法)
    public bool RegisterHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
    {
        try
        {
            _isRegisteringHotkey = true; // 进入热键注册模式
            _hotkeyVirtualKey = GetVirtualKeyFromLyKey(keyCode); // 转换为Windows虚拟键码
            _pendingHotkey = keyCode; // 保存LyKeys按键码
            SaveHotkeyConfig(keyCode, modifiers); // 保存到配置文件
            _isRegisteringHotkey = false; // 退出热键注册模式
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("注册热键失败", ex);
            _isRegisteringHotkey = false; // 确保异常情况下也能退出注册模式
            return false;
        }
    }

    // 保存热键配置 (简化为一个方法)
    private void SaveHotkeyConfig(LyKeysCode keyCode, ModifierKeys modifiers)
    {
        AppConfigService.UpdateConfig(config =>
        {
            config.startKey = keyCode;
            config.startMods = modifiers;
            config.stopKey = keyCode; // 设置相同停止键
            config.stopMods = modifiers; // 设置相同的修饰键
        });
    }

    // 启动序列控制
    public void StartSequence()
    {
        try
        {
            _logger.Debug("开始启动按键序列");

            // 如果在注册热键模式，不进行窗口状态检查
            if (!_isRegisteringHotkey && !CanTriggerHotkey()) return;

            // 检查是否有可执行项（键盘按键或坐标）
            bool hasKeyboardKeys = _keyList.Count > 0;
            bool hasCoordinates = _keySettings.Any(k => k.Type == KeyItemType.Coordinates);
            
            if (!hasKeyboardKeys && !hasCoordinates)
            {
                _logger.Warning("按键和坐标列表均为空，无法启动序列");
                _mainViewModel.UpdateStatusMessage("请至少添加一个按键或坐标", true);
                return;
            }

            _isSequenceRunning = true;
            _logger.Debug($"序列运行状态已设置为: {_isSequenceRunning}");
            
            _lyKeysService.IsEnabled = true;
            
            int coordinatesCount = _keySettings.Count(k => k.Type == KeyItemType.Coordinates);
            _logger.Debug($"序列已启动 - 模式: {(_lyKeysService.IsHoldMode ? "按压" : "顺序")}, " +
                          $"键盘按键数: {_keyList.Count}, 坐标点数: {coordinatesCount}, " +
                          $"使用独立按键间隔设置, 目标窗口句柄: {_targetWindowHandle}");

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
        if (!_isSequenceRunning) 
        {
            _logger.Debug("停止按键序列：序列已经处于停止状态，忽略此次调用");
            return;
        }

        _logger.Debug("停止按键序列，当前序列状态：" + _isSequenceRunning);

        // 确保按键服务先停止
        _lyKeysService.IsEnabled = false;
        _logger.Debug("已禁用按键服务");

        // 更新内部运行状态
        _isSequenceRunning = false;
        
        _logger.Debug("序列已停止，即将触发SequenceModeStopped事件");
        // 触发外部事件通知
        SequenceModeStopped?.Invoke();
        _logger.Debug("SequenceModeStopped事件已触发");
    }

    // 键盘钩子回调处理
    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_isInputFocused)
            try
            {
                var hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT))!;
                var isHotkey = hookStruct.vkCode == _hotkeyVirtualKey;

                // 如果正在注册热键，直接处理不进行窗口状态检查
                if (_isRegisteringHotkey && isHotkey)
                {
                    // 处理热键注册逻辑
                    switch ((int)wParam)
                    {
                        case WM_KEYDOWN:
                        case WM_SYSKEYDOWN:
                            if (!_isKeyHeld)
                            {
                                _isKeyHeld = true;
                                StartHotkeyPressed?.Invoke();
                            }
                            break;
                        case WM_KEYUP:
                        case WM_SYSKEYUP:
                            if (_isKeyHeld)
                            {
                                _isKeyHeld = false;
                                StartHotkeyReleased?.Invoke();
                            }
                            break;
                    }
                    return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
                }

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

                // 处理热键 - 修改这部分逻辑，避免重复调用CanTriggerHotkey
                if (isHotkey)
                {
                    if (_lyKeysService.IsHoldMode)
                    {
                        // 按压模式处理逻辑
                        switch ((int)wParam)
                        {
                            case WM_KEYDOWN:
                            case WM_SYSKEYDOWN:
                                if (!_isKeyHeld)
                                {
                                    _isKeyHeld = true;
                                    
                                    // 只在按键首次按下时检查一次触发条件
                                    if (CanTriggerHotkey())
                                    {
                                        StartHotkeyPressed?.Invoke();
                                        StartSequence();
                                    }
                                }
                                return new IntPtr(1);

                            case WM_KEYUP:
                            case WM_SYSKEYUP:
                                if (_isKeyHeld)
                                {
                                    _isKeyHeld = false;
                                    StartHotkeyReleased?.Invoke();
                                    StopSequence();
                                }
                                return new IntPtr(1);
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
                                    
                                    // 只在按键首次按下时检查一次触发条件
                                    if (CanTriggerHotkey())
                                    {
                                        if (_isSequenceRunning)
                                        {
                                            // 如果序列已在运行，则停止
                                            StartHotkeyPressed?.Invoke();
                                            StopSequence();
                                        }
                                        else
                                        {
                                            // 否则启动序列
                                            StartHotkeyPressed?.Invoke();
                                            StartSequence();
                                        }
                                    }
                                }
                                break;

                            case WM_KEYUP:
                            case WM_SYSKEYUP:
                                if (_isKeyHeld)
                                {
                                    _isKeyHeld = false;
                                    StartHotkeyReleased?.Invoke();
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

        return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
    }

    // 鼠标钩子回调处理
    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_isInputFocused)
            try
            {
                var hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
                var wParamInt = (int)wParam;
                
                // 如果正在注册热键模式，直接处理鼠标事件不进行窗口状态检查
                if (_isRegisteringHotkey)
                {
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
                    return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
                }
                
                // 获取当前活动窗口
                var activeWindow = GetForegroundWindow();
                // 修改判断逻辑：如果没有设置目标窗口，则允许在任何窗口触发
                var isTargetWindowActive = _targetWindowHandle == IntPtr.Zero || activeWindow == _targetWindowHandle;

                // 如果目标窗口未激活，停止当前执行
                if (!isTargetWindowActive && _isSequenceRunning)
                {
                    _lyKeysService.EmergencyStop(); // 使用紧急停止
                    StopSequence();
                    return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
                }

                // 如果是鼠标热键且窗口未激活，直接返回
                if ((wParamInt == WM_XBUTTONDOWN || wParamInt == WM_MBUTTONDOWN ||
                     wParamInt == WM_XBUTTONUP || wParamInt == WM_MBUTTONUP ||
                     wParamInt == WM_MOUSEWHEEL) && !isTargetWindowActive)
                    return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);

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

        return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    // 处理鼠标按键按下
    private void HandleMouseButtonDown(int wParam, MSLLHOOKSTRUCT hookStruct)
    {
        var buttonCode = GetMouseButtonCode(wParam, hookStruct);
        var isHotkey = buttonCode == _pendingHotkey;

        // 如果是在注册热键模式，只触发事件但不执行序列控制
        if (isHotkey && _isRegisteringHotkey)
        {
            if (!_isKeyHeld)
            {
                _isKeyHeld = true;
                StartHotkeyPressed?.Invoke();
            }
            return;
        }

        if (isHotkey)
        {
            if (_lyKeysService.IsHoldMode)
            {
                if (!_isKeyHeld)
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
                        // 如果序列已在运行，则停止
                        StartHotkeyPressed?.Invoke();
                        StopSequence();
                    }
                    else
                    {
                        // 否则启动序列
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
        var buttonCode = GetMouseButtonCode(wParam, hookStruct);
        var isHotkey = buttonCode == _pendingHotkey;

        // 如果是在注册热键模式，只触发事件不执行序列控制
        if (isHotkey && _isRegisteringHotkey)
        {
            if (_isKeyHeld)
            {
                _isKeyHeld = false;
                StartHotkeyReleased?.Invoke();
            }
            return;
        }

        if (isHotkey)
        {
            if (_lyKeysService.IsHoldMode)
            {
                if (_isKeyHeld)
                {
                    _isKeyHeld = false;
                    StartHotkeyReleased?.Invoke();
                    StopSequence();
                }
            }
            else
            {
                if (_isKeyHeld)
                {
                    _isKeyHeld = false;
                    StartHotkeyReleased?.Invoke();
                }
            }
        }
    }

    // 处理滚轮事件
    private void HandleMouseWheel(MSLLHOOKSTRUCT hookStruct)
    {
        // 获取滚轮方向（向上为正，向下为负）
        var wheelDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
        var buttonCode = wheelDelta > 0 ? LyKeysCode.VK_WHEELUP : LyKeysCode.VK_WHEELDOWN;

        var isHotkey = buttonCode == _pendingHotkey;

        // 如果是在注册热键模式，只触发事件不执行序列控制
        if (isHotkey && _isRegisteringHotkey)
        {
            if (!_isKeyHeld)
            {
                _isKeyHeld = true;
                StartHotkeyPressed?.Invoke();
                
                // 由于滚轮事件是瞬时的，立即触发释放事件
                _isKeyHeld = false;
                StartHotkeyReleased?.Invoke();
            }
            return;
        }

        if (isHotkey)
        {
            if (_lyKeysService.IsHoldMode)
            {
                if (_isSequenceRunning)
                {
                    _isKeyHeld = false;
                    StartHotkeyReleased?.Invoke();
                    StopSequence();
                }
                else if (!_isKeyHeld)
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
                        // 如果序列已在运行，则停止
                        StartHotkeyPressed?.Invoke();
                        StopSequence();
                    }
                    else
                    {
                        // 否则启动序列
                        StartHotkeyPressed?.Invoke();
                        StartSequence();
                    }
                }
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
            var wheelDelta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
            return wheelDelta > 0 ? LyKeysCode.VK_WHEELUP : LyKeysCode.VK_WHEELDOWN;
        }

        var xButton = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
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
        if (_pendingHotkey.HasValue)
        {
            RegisterHotkey(_pendingHotkey.Value, ModifierKeys.None);
        }
    }

    // 输入焦点控制
    public bool IsInputFocused
    {
        get => _isInputFocused;
        set => _isInputFocused = value;
    }

    // 热键总开关属性
    public bool IsHotkeyControlEnabled
    {
        get => _isHotkeyControlEnabled;
        set
        {
            if (_isHotkeyControlEnabled != value)
            {
                _isHotkeyControlEnabled = value;
                _logger.Debug($"热键服务总开关已{(value ? "启用" : "禁用")}");

                // 如果禁用热键总开关，同时停止当前执行的序列
                if (!value && _isSequenceRunning)
                {
                    _lyKeysService.EmergencyStop();
                    StopSequence();
                    _logger.Debug("因热键总开关关闭，已停止当前按键序列");
                }
            }
        }
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
        try
        {
            // 筛选出键盘类型且KeyCode有值的按键，然后提取KeyCode值组成新列表
            _keyList = keySettings
                .Where(k => k.Type == KeyItemType.Keyboard && k.KeyCode.HasValue)
                .Select(k => k.KeyCode.Value)
                .ToList();

            // 传递给LyKeysService统一的操作列表
            _lyKeysService.SetUnifiedOperationList(keySettings);

            // 保存完整的按键设置
            _keySettings = keySettings.ToList();

            _logger.Debug($"设置按键序列: 总操作数={keySettings.Count}, 键盘按键={_keyList.Count}, 坐标点={keySettings.Count(k => k.Type == KeyItemType.Coordinates)}, 使用独立按键间隔");
        }
        catch (Exception ex)
        {
            _logger.Error("设置按键序列失败", ex);
        }
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
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule!)
            {
                var hModule = GetModuleHandle(curModule.ModuleName);

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
                    throw new Win32Exception(Marshal.GetLastWin32Error());
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
                return WindowState.NoTargetWindow;

            // 2. 检查进程是否运行
            if (_targetWindowHandle == IntPtr.Zero &&
                !string.IsNullOrEmpty(_mainViewModel.KeyMappingViewModel.SelectedWindowProcessName))
                return WindowState.ProcessNotRunning;

            // 3. 检查窗口是否有效
            if (!IsWindow(_targetWindowHandle)) return WindowState.WindowInvalid;

            // 4. 检查窗口是否激活
            var activeWindow = GetForegroundWindow();
            return activeWindow == _targetWindowHandle ? WindowState.WindowActive : WindowState.WindowInactive;
        }
        catch (Exception ex)
        {
            _logger.Error("检查窗口状态时发生异常", ex);
            return WindowState.WindowInvalid;
        }
    }

    // 总开关：判断是否可以触发热键
    private bool CanTriggerHotkey()
    {
        _logger.Debug("检查是否可以触发热键");
        // 如果正在注册热键，跳过窗口状态检查
        if (_isRegisteringHotkey)
            return true;

        // 首先检查热键总开关是否启用
        if (!_isHotkeyControlEnabled)
        {
            _logger.Debug("热键总开关已关闭，忽略热键触发");
            return false;
        }

        // 继续检查窗口状态
        var state = GetWindowState();

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
                _logger.Debug("目标窗口未激活，忽略热键触发");
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