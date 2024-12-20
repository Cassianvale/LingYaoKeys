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
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // 定义委托
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        // 添加常量
        private const int WH_MOUSE_LL = 14;
        private const int WM_HOTKEY = 0x0312;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_MBUTTONDOWN = 0x0207;

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

        // 添加新的字段
        private bool _isInputFocused;
        
        // 添加新的属性
        public bool IsInputFocused
        {
            get => _isInputFocused;
            set
            {
                if (_isInputFocused != value)
                {
                    _isInputFocused = value;
                    _logger.LogDebug("HotkeyService", $"输入框焦点状态改变: {value}");
                    
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
        private const int MIN_TOGGLE_INTERVAL = 300; // 启动/停止切换的最小间隔(毫秒)
        private const int KEY_RELEASE_TIMEOUT = 50;  // 按键释放检测超时(毫秒)
        private DateTime _lastToggleTime = DateTime.MinValue;
        private DateTime _lastKeyDownTime = DateTime.MinValue;
        private bool _isKeyHeld = false;

        // 构造函数
        public HotkeyService(Window mainWindow, DDDriverService ddDriverService)
        {
            _mainWindow = mainWindow;
            _ddDriverService = ddDriverService;
            
            // 确保在窗口初始化后自动注册热键
            _mainWindow.SourceInitialized += (s, e) =>
            {
                _windowHandle = new WindowInteropHelper(_mainWindow).Handle;
                _source = HwndSource.FromHwnd(_windowHandle);
                if (_source != null)
                {
                    _source.AddHook(WndProc);
                    _isWindowInitialized = true;
                    _logger.LogInitialization("HotkeyService",$"窗口初始化完成，获取句柄: {_windowHandle:X}");
                    
                    // 注册待处理的热键
                    RegisterPendingHotkeys();
                }
            };

            // 添加全局鼠标钩子
            _mouseProc = MouseHookCallback;
            _mouseHookHandle = SetMouseHook(_mouseProc);
            
            // 窗口关闭时清理资源
            _mainWindow.Closed += (s, e) =>
            {
                if (_mouseHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookHandle);
                    _mouseHookHandle = IntPtr.Zero;
                }
                Dispose();
            };

            if (!IsRunAsAdministrator())
            {
                MessageBox.Show("请以管理员身份运行程序以使用热键功能", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 检查程序是否以管理员身份运行
        private bool IsRunAsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        // 注册快捷键
        public bool RegisterHotKey(Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            if (_isRegistered || _windowHandle == IntPtr.Zero) return false;

            try
            {
                _isRegistered = RegisterHotKey(
                    _windowHandle,
                    START_HOTKEY_ID,
                    (uint)modifiers,
                    (uint)KeyInterop.VirtualKeyFromKey(key)
                );

                if (!_isRegistered)
                {
                    MessageBox.Show("热键注册失败，可能被其他程序占用", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return _isRegistered;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"热键注册异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // 取消注册快捷键
        public void UnregisterHotKey()
        {
            if (!_isRegistered || _windowHandle == IntPtr.Zero) return;

            try
            {
                UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
                _isRegistered = false;
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

                switch (msg)
                {
                    case WM_HOTKEY:
                        HandleHotkeyMessage(wParam.ToInt32());
                        handled = true;
                        break;

                    case WM_XBUTTONDOWN:
                        int xButton = (int)((wParam.ToInt32() >> 16) & 0xFFFF);
                        DDKeyCode xButtonCode = xButton == 1 ? DDKeyCode.XBUTTON1 : DDKeyCode.XBUTTON2;
                        HandleMouseButtonMessage(xButtonCode);
                        handled = true;
                        break;

                    case WM_MBUTTONDOWN:
                        HandleMouseButtonMessage(DDKeyCode.MBUTTON);
                        handled = true;
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
            if (_mouseHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }
            
            StopSequence();
            UnregisterHotKey();
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }

        // 修改注册开始热键的方法
        public bool RegisterStartHotkey(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            if (!_isWindowInitialized)
            {
                _pendingStartKey = ddKeyCode;
                _pendingStartMods = modifiers;
                _logger.LogDebug("HotkeyService", $"窗口未初始化，保存待处理的开始热键: {ddKeyCode}, 修饰键: {modifiers}");
                return true;
            }

            try
            {
                _logger.LogDebug("HotkeyService", $"开始注册热键 - 键码: {ddKeyCode}, 修饰键: {modifiers}");
                
                // 清理现有热键
                CleanupExistingHotkeys();
                
                _startVirtualKey = GetVirtualKeyFromDDKey(ddKeyCode);
                uint modifierFlags = ConvertToModifierFlags(modifiers);
                _lastStartModifiers = modifierFlags;
                _pendingStartKey = ddKeyCode;

                // 如果是鼠标按键，不需要实际注册热键
                if (IsMouseButton(ddKeyCode))
                {
                    _startHotkeyRegistered = true;
                    _logger.LogDebug("HotkeyService", "注册鼠标按键作为开始热键");
                    return true;
                }

                // 检查是否与停止键相同
                if (_stopHotkeyRegistered && _stopVirtualKey == _startVirtualKey && _lastStopModifiers == modifierFlags)
                {
                    _currentMode = HotkeyMode.Same;
                    _startHotkeyRegistered = true;
                    _logger.LogDebug("HotkeyService", "切换到相同热键模式");
                    return true;
                }

                // 不同热键模式
                _currentMode = HotkeyMode.Different;
                bool result = RegisterHotKey(_windowHandle, START_HOTKEY_ID, modifierFlags, (uint)_startVirtualKey);
                _startHotkeyRegistered = result;
                
                _logger.LogDebug("HotkeyService", 
                    $"注册开始热键结果: {result}, 模式: {_currentMode}, VK=0x{_startVirtualKey:X2}, Mods=0x{modifierFlags:X}");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "注册开始热键异常", ex);
                return false;
            }
        }

        // 修改注册停止热键的方法
        public bool RegisterStopHotkey(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            if (!_isWindowInitialized)
            {
                _pendingStopKey = ddKeyCode;
                _pendingStopMods = modifiers;
                _logger.LogDebug("HotkeyService", $"窗口未初始化，保存待处理的停止热键: {ddKeyCode}, 修饰键: {modifiers}");
                return true;
            }

            try
            {
                _logger.LogDebug("HotkeyService", $"开始注册停止热键 - 键码: {ddKeyCode}, 修饰键: {modifiers}");
                
                // 先清理现有的停止热键
                if (_stopHotkeyRegistered)
                {
                    UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
                    _stopHotkeyRegistered = false;
                }

                _stopVirtualKey = GetVirtualKeyFromDDKey(ddKeyCode);
                uint modifierFlags = ConvertToModifierFlags(modifiers);
                _lastStopModifiers = modifierFlags;

                // 检查是否与开始热键相同
                if (_startHotkeyRegistered && _startVirtualKey == _stopVirtualKey && _lastStartModifiers == modifierFlags)
                {
                    _currentMode = HotkeyMode.Same;
                    _stopHotkeyRegistered = true;
                    _logger.LogDebug("HotkeyService", "切换到相同热键模式");
                    return true;
                }

                // 不同热键模式
                _currentMode = HotkeyMode.Different;
                bool result = RegisterHotKey(_windowHandle, STOP_HOTKEY_ID, modifierFlags, (uint)_stopVirtualKey);
                _stopHotkeyRegistered = result;
                
                _logger.LogDebug("HotkeyService", 
                    $"注册停止热键结果: {result}, 模式: {_currentMode}, VK=0x{_stopVirtualKey:X2}, Mods=0x{modifierFlags:X}");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "注册停止热键异常", ex);
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
            try
            {
                if (!_isSequenceRunning && !_isStarted) 
                {
                    _logger.LogDebug("HotkeyService", "序列未运行，无需停止");
                    return;
                }

                _logger.LogDebug("HotkeyService", "正在停止序列...");
                _isSequenceRunning = false;
                _isStarted = false;
                
                if (_sequenceCts != null)
                {
                    _sequenceCts.Cancel();
                    _sequenceCts.Dispose();
                    _sequenceCts = null;
                    _logger.LogDebug("HotkeyService", "已取消序列任务");
                }

                SequenceModeStopped?.Invoke();
                _logger.LogDebug("HotkeyService", "序列已完全停止");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "停止序列时发生异常", ex);
                // 确保状态被重置
                _isSequenceRunning = false;
                _isStarted = false;
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

        // 注册待处理的热键
        private void RegisterPendingHotkeys()
        {
            try
            {
                if (_pendingStartKey.HasValue)
                {
                    _logger.LogDebug("HotkeyService", $"注册待处理的开始热键: {_pendingStartKey.Value}");
                    RegisterStartHotkey(_pendingStartKey.Value, _pendingStartMods);
                }

                if (_pendingStopKey.HasValue)
                {
                    _logger.LogDebug("HotkeyService", $"注册待处理的停止热键: {_pendingStopKey.Value}");
                    RegisterStopHotkey(_pendingStopKey.Value, _pendingStopMods);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "注册待处理热键时发生错误", ex);
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

        // 修改 HandleMouseButtonMessage 方法
        private void HandleMouseButtonMessage(DDKeyCode buttonCode)
        {
            try
            {
                var now = DateTime.Now;

                // 检查是否是按键按下状态
                if (!_isKeyHeld)
                {
                    _lastKeyDownTime = now;
                    _isKeyHeld = true;
                    
                    // 检查是否满足切换间隔要求
                    var timeSinceLastToggle = (now - _lastToggleTime).TotalMilliseconds;
                    if (timeSinceLastToggle < MIN_TOGGLE_INTERVAL)
                    {
                        _logger.LogDebug("HotkeyService", $"忽略过快的切换，间隔: {timeSinceLastToggle}ms");
                        return;
                    }
                }
                else
                {
                    // 检查按键是否持续按下
                    var keyHoldTime = (now - _lastKeyDownTime).TotalMilliseconds;
                    if (keyHoldTime < KEY_RELEASE_TIMEOUT)
                    {
                        _logger.LogDebug("HotkeyService", "按键持续按下中，忽略重复触发");
                        return;
                    }
                    _isKeyHeld = false;
                }

                _logger.LogDebug("HotkeyService", 
                    $"收到鼠标按键消息: {buttonCode}, " +
                    $"开始热键: {_pendingStartKey}, " +
                    $"停止热键: {_pendingStopKey}, " +
                    $"当前状态: {(_isStarted ? "已启动" : "未启动")}, " +
                    $"序列运行: {_isSequenceRunning}, " +
                    $"按键状态: {(_isKeyHeld ? "按下" : "释放")}");

                // 检查是否匹配开始热键
                if (_startHotkeyRegistered && _pendingStartKey == buttonCode)
                {
                    if (!_isStarted && !_isSequenceRunning)
                    {
                        _logger.LogDebug("HotkeyService", "触发鼠标按键开始事件");
                        StartHotkeyPressed?.Invoke();
                        _isStarted = true;
                        _lastToggleTime = now;
                    }
                    else if (_currentMode == HotkeyMode.Same)
                    {
                        _logger.LogDebug("HotkeyService", "相同模式下触发鼠标按键停止事件");
                        StopKeyMapping();
                        _lastToggleTime = now;
                    }
                }
                // 检查是否匹配停止热键
                else if (_stopHotkeyRegistered && _pendingStopKey == buttonCode)
                {
                    _logger.LogDebug("HotkeyService", "检测到停止热键");
                    if (_isStarted || _isSequenceRunning)
                    {
                        _logger.LogDebug("HotkeyService", "触发鼠标按键停止事件");
                        StopKeyMapping();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "处理鼠标按键消息异常", ex);
            }
        }

        // 添加新的停止方法
        private void StopKeyMapping()
        {
            try
            {
                _logger.LogDebug("HotkeyService", "开始停止按键映射");
                
                // 先停止序列
                StopSequence();
                
                // 触发停止事件
                StopHotkeyPressed?.Invoke();
                
                // 重置状态
                _isStarted = false;
                _isSequenceRunning = false;
                
                _logger.LogDebug("HotkeyService", "按键映射已停止");
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "停止按键映射异常", ex);
            }
        }

        // 添加辅助方法
        public bool IsMouseButton(DDKeyCode keyCode)
        {
            return keyCode == DDKeyCode.MBUTTON || 
                   keyCode == DDKeyCode.XBUTTON1 || 
                   keyCode == DDKeyCode.XBUTTON2;
        }

        // 处理热键消息
        private void HandleHotkeyMessage(int id)
        {
            try 
            {
                var now = DateTime.Now;
                
                // 检查是否是按键按下状态
                if (!_isKeyHeld)
                {
                    _lastKeyDownTime = now;
                    _isKeyHeld = true;
                    
                    // 检查是否满足切换间隔要求
                    var timeSinceLastToggle = (now - _lastToggleTime).TotalMilliseconds;
                    if (timeSinceLastToggle < MIN_TOGGLE_INTERVAL)
                    {
                        _logger.LogDebug("HotkeyService", $"忽略过快的切换，间隔: {timeSinceLastToggle}ms");
                        return;
                    }
                }
                else
                {
                    // 检查按键是否持续按下
                    var keyHoldTime = (now - _lastKeyDownTime).TotalMilliseconds;
                    if (keyHoldTime < KEY_RELEASE_TIMEOUT)
                    {
                        _logger.LogDebug("HotkeyService", "按键持续按下中，忽略重复触发");
                        return;
                    }
                    _isKeyHeld = false;
                }

                _logger.LogDebug("HotkeyService", 
                    $"收到热键消息: ID={id}, 当前模式: {_currentMode}, " +
                    $"已启动: {_isStarted}, 按键状态: {(_isKeyHeld ? "按下" : "释放")}");

                // 相同热键模式处理
                if (_currentMode == HotkeyMode.Same && id == START_HOTKEY_ID)
                {
                    if (!_isStarted && !_isSequenceRunning)
                    {
                        _logger.LogDebug("HotkeyService", "相同热键模式 - 触发启动");
                        StartHotkeyPressed?.Invoke();
                        _isStarted = true;
                        _lastToggleTime = now;
                    }
                    else if (_isStarted || _isSequenceRunning)
                    {
                        _logger.LogDebug("HotkeyService", "相同热键模式 - 触发停止");
                        StopSequence();
                        StopHotkeyPressed?.Invoke();
                        _isStarted = false;
                        _lastToggleTime = now;
                    }
                    return;
                }

                // 不同热键模式处理
                switch (id)
                {
                    case START_HOTKEY_ID:
                        if (!_isStarted && !_isSequenceRunning)
                        {
                            _logger.LogDebug("HotkeyService", "不同热键模式 - 触发启动");
                            StartHotkeyPressed?.Invoke();
                            _isStarted = true;
                        }
                        break;

                    case STOP_HOTKEY_ID:
                        if (_isStarted || _isSequenceRunning)
                        {
                            _logger.LogDebug("HotkeyService", "不同热键模式 - 触发停止");
                            StopSequence();
                            StopHotkeyPressed?.Invoke();
                            _isStarted = false;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("HotkeyService", "处理热键消息异常", ex);
            }
        }

        // 添加设置鼠标钩子的方法
        private IntPtr SetMouseHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule == null) return IntPtr.Zero;
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        // 添加鼠标钩子回调
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
                            
                            _logger.LogDebug("HotkeyService", $"全局鼠标钩子捕获到XButton: {xButtonCode}");
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
    }
}