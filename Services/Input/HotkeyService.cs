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
using System.ComponentModel;
using WpfApp.ViewModels;
using WpfApp.Services.Models;

// 提供快捷键服务
namespace WpfApp.Services
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

        private bool _isRapidFireEnabled;
        private List<KeyBurstConfig> _rapidFireKeys = new();

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
                    _lyKeysService.KeyInterval = config.interval;
                }
            }

            // 直接设置模式，不触发事件
            _lyKeysService.IsHoldMode = config.keyMode != 0;
        }

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

        // 热键注册方法
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

        // 序列控制
        public void StartSequence()
        {
            try
            {
                _logger.Debug("开始启动按键序列");
                
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
                             $"按键数: {_keyList.Count}, 间隔: {_lyKeysService.KeyInterval}ms");
                
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

        // 钩子回调处理
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_isInputFocused)
            {
                try
                {
                    var hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT))!;
                    bool isStartKey = hookStruct.vkCode == _startVirtualKey;
                    bool isStopKey = hookStruct.vkCode == _stopVirtualKey;

                    if (isStartKey || isStopKey)
                    {
                        if (_lyKeysService.IsHoldMode)
                        {
                            // 按压模式：阻止原始按键信号
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
                            // 顺序模式：允许原始按键信号
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

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_isInputFocused)
            {
                try
                {
                    var hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT))!;
                    int wParamInt = (int)wParam;

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
        public void SetKeySequence(List<LyKeysCode> keys, int interval)
        {
            _keyList = keys;
            _lyKeysService.KeyInterval = interval;
            _logger.Debug($"设置按键序列: 按键数={keys.Count}, 间隔={interval}ms");
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

        // 设置连发功能启用状态
        public void SetRapidFireEnabled(bool enabled)
        {
            _isRapidFireEnabled = enabled;
            // 同步状态到LyKeysService
            _lyKeysService.IsRapidFireEnabled = enabled;
            if (!enabled)
            {
                // 当禁用连发时，清空连发按键列表
                _rapidFireKeys.Clear();
            }
        }

        // 更新连发按键列表
        public void UpdateRapidFireKeys(IEnumerable<KeyBurstConfig> keys)
        {
            _rapidFireKeys = keys.ToList();
            if (_isRapidFireEnabled)
            {
                // 当连发功能启用时，应用连发设置
                foreach (var key in _rapidFireKeys)
                {
                    // TODO: 实现具体的连发逻辑
                    _logger.Debug($"更新连发按键: {key.Code}, 延迟: {key.RapidFireDelay}ms, 按压时间: {key.PressTime}ms");
                }
            }
        }

        // 获取按键的连发状态
        public bool IsKeyInRapidFire(LyKeysCode keyCode)
        {
            return _rapidFireKeys.Any(k => k.Code == keyCode);
        }

        // 获取连发功能启用状态
        public bool IsRapidFireEnabled => _isRapidFireEnabled;
    }
}