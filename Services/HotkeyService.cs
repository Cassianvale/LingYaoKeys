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

        // 事件
        public event Action? StartHotkeyPressed;  // 启动热键按下事件
        public event Action? StartHotkeyReleased;  // 启动热键释放事件
        public event Action? StopHotkeyPressed;  // 停止热键按下事件
        public event Action? SequenceModeStarted;  // 序列模式开始事件
        public event Action? SequenceModeStopped;  // 序列模式停止事件
        public event Action<DDKeyCode>? KeyTriggered;  // 触发按键事件

        // 核心字段
        private readonly DDDriverService _ddDriverService;
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly MainViewModel _mainViewModel;
        private readonly Window _mainWindow;
        private List<DDKeyCode> _keyList = new List<DDKeyCode>();

        // 热键状态
        private int _startVirtualKey;  // 启动热键虚拟键码
        private int _stopVirtualKey;  // 停止热键虚拟键码
        private DDKeyCode? _pendingStartKey;  // dd启动键键码
        private DDKeyCode? _pendingStopKey;  // dd停止键键码
        private bool _isKeyHeld;    // 防止全局热键的重复触发
        private bool _isSequenceRunning;  // 序列模式是否正在运行
        private bool _isInputFocused;  // 输入焦点是否在当前窗口

        // 保持回调函数的引用
        private readonly HookProc _mouseProcDelegate;  // 鼠标钩子回调函数
        private readonly HookProc _keyboardProcDelegate;  // 键盘钩子回调函数
        private IntPtr _mouseHookHandle;  // 鼠标钩子句柄
        private IntPtr _keyboardHookHandle;  // 键盘钩子句柄
        private readonly object _hookLock = new object();  // 钩子锁

        // 构造函数
        public HotkeyService(Window mainWindow, DDDriverService ddDriverService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _ddDriverService = ddDriverService ?? throw new ArgumentNullException(nameof(ddDriverService));
            _mainViewModel = mainWindow.DataContext as MainViewModel ??
                throw new ArgumentException("Window.DataContext must be of type MainViewModel", nameof(mainWindow));

            // 初始化回调委托
            _mouseProcDelegate = MouseHookCallback;
            _keyboardProcDelegate = KeyboardHookCallback;

            // 订阅模式切换事件（仅在真正切换模式时触发）
            _ddDriverService.ModeSwitched += OnModeSwitched;

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
            if (config.keyList?.Count > 0)
            {
                var selectedKeys = config.keyList
                    .Where((key, index) => index < config.keySelections.Count && config.keySelections[index])
                    .ToList();

                if (selectedKeys.Count > 0)
                {
                    _keyList = selectedKeys;
                    _ddDriverService.SetKeyList(selectedKeys);
                    _ddDriverService.SetKeyInterval(config.interval);
                }
            }

            // 直接设置模式，不触发事件
            _ddDriverService.SetModeWithoutEvent(config.keyMode == 0);

        }

        public void Dispose()
        {
            lock (_hookLock)
            {
                // 停止序列
                StopSequence();

                // 移除事件订阅
                _ddDriverService.ModeSwitched -= OnModeSwitched;

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
        public bool RegisterStartHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            try
            {
                _startVirtualKey = GetVirtualKeyFromDDKey(keyCode);  // 转换为Windows虚拟键码
                _pendingStartKey = keyCode;                          // 保存DD按键码
                SaveHotkeyConfig(true, keyCode, modifiers);         // 保存到配置文件
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("注册开始热键失败", ex);
                return false;
            }
        }

        public bool RegisterStopHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            try
            {
                _stopVirtualKey = GetVirtualKeyFromDDKey(keyCode);  // 转换为Windows虚拟键码
                _pendingStopKey = keyCode;                          // 保存DD按键码
                SaveHotkeyConfig(false, keyCode, modifiers);        // 保存到配置文件
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("注册停止热键失败", ex);
                return false;
            }
        }

        private void SaveHotkeyConfig(bool isStart, DDKeyCode keyCode, ModifierKeys modifiers)
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
            if (_keyList.Count == 0)
            {
                _mainViewModel.UpdateStatusMessage("请至少选择一个按键", true);
                return;
            }

            _isSequenceRunning = true;

            if (_ddDriverService.IsSequenceMode)
            {
                _ddDriverService.IsEnabled = true;
            }
            else
            {
                _ddDriverService.SetHoldMode(true);
                _ddDriverService.IsEnabled = true;  // 确保在按压模式下也设置启用状态
            }

            SequenceModeStarted?.Invoke();  // 触发序列开始事件
        }

        public void StopSequence()
        {
            if (!_isSequenceRunning) return;

            _isSequenceRunning = false;
            _ddDriverService.IsEnabled = false;

            if (!_ddDriverService.IsSequenceMode)
            {
                _ddDriverService.SetHoldMode(false);
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
                        if (!_ddDriverService.IsSequenceMode)
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
            DDKeyCode buttonCode = GetMouseButtonCode(wParam, hookStruct);
            bool isStartKey = buttonCode == _pendingStartKey;
            bool isStopKey = buttonCode == _pendingStopKey;

            if (isStartKey || isStopKey)
            {
                if (!_ddDriverService.IsSequenceMode)
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
            DDKeyCode buttonCode = GetMouseButtonCode(wParam, hookStruct);

            if (!_ddDriverService.IsSequenceMode &&
                buttonCode == _pendingStartKey &&
                _isKeyHeld)
            {
                _isKeyHeld = false;
                StopSequence();
            }
        }

        private DDKeyCode GetMouseButtonCode(int wParam, MSLLHOOKSTRUCT hookStruct)
        {
            if (wParam == WM_MBUTTONDOWN || wParam == WM_MBUTTONUP)
            {
                return DDKeyCode.MBUTTON;
            }

            int xButton = (int)((hookStruct.mouseData >> 16) & 0xFFFF);
            return xButton == 1 ? DDKeyCode.XBUTTON1 : DDKeyCode.XBUTTON2;
        }

        // 工具方法
        private int GetVirtualKeyFromDDKey(DDKeyCode ddKeyCode)
        {
            switch (ddKeyCode)
            {
                case DDKeyCode.MBUTTON:
                    return 0x04;
                case DDKeyCode.XBUTTON1:
                    return 0x05;
                case DDKeyCode.XBUTTON2:
                    return 0x06;
                default:
                    foreach (var pair in KeyCodeMapping.VirtualToDDKeyMap)
                    {
                        if (pair.Value == ddKeyCode)
                        {
                            return pair.Key;
                        }
                    }
                    return 0;
            }
        }

        private void OnModeSwitched(object? sender, bool isSequenceMode)
        {
            StopSequence();

            // 重新注册热键
            if (_pendingStartKey.HasValue)
            {
                RegisterStartHotkey(_pendingStartKey.Value, ModifierKeys.None);
                if (isSequenceMode && _pendingStopKey.HasValue)
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
        public void SetKeySequence(List<DDKeyCode> keys, int interval)
        {
            _keyList = keys;
            _ddDriverService.SetKeyInterval(interval);
            _logger.Debug($"设置按键序列: 按键数={keys.Count}, 间隔={interval}ms");
        }

        // 判断是否为鼠标按键
        public bool IsMouseButton(DDKeyCode keyCode)
        {
            return keyCode == DDKeyCode.LBUTTON ||
                   keyCode == DDKeyCode.RBUTTON ||
                   keyCode == DDKeyCode.MBUTTON ||
                   keyCode == DDKeyCode.XBUTTON1 ||
                   keyCode == DDKeyCode.XBUTTON2;
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
    }
}