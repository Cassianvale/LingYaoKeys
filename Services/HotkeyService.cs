using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Security.Principal;
using System.Threading.Tasks;

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

        private const int HOTKEY_ID = 80;
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

        private DDKeyCode? _startHotkey;
        private DDKeyCode? _stopHotkey;

        private bool _isSequenceRunning;
        private DDDriverService _ddDriverService;

        private List<DDKeyCode> _keyList = new List<DDKeyCode>();
        private CancellationTokenSource? _sequenceCts;
        private int _keyInterval = 50;

        // 添加字段保存虚拟键码
        private int _startVirtualKey;
        private int _stopVirtualKey;
        private DDKeyCode? _startDDKey;
        private DDKeyCode? _stopDDKey;

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
                    System.Diagnostics.Debug.WriteLine($"窗口初始化完成，句柄: {_windowHandle:X}");
                    
                    // 从配置加载并注册热键
                    LoadAndRegisterHotkeys();
                }
            };

            // 窗口关闭时清理资源
            _mainWindow.Closed += (s, e) =>
            {
                Dispose();
            };

            if (!IsRunAsAdministrator())
            {
                MessageBox.Show("请以管理员身份运行程序以使用热键功能", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

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
                    HOTKEY_ID,
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
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
                _isRegistered = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"热键注销异常: {ex.Message}");
            }
        }

        // 处理热键事件
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                int keyState = (lParam.ToInt32() >> 16) & 0xFFFF;
                int modState = lParam.ToInt32() & 0xFFFF;

                System.Diagnostics.Debug.WriteLine($"收到热键消息: ID={id}, KeyState=0x{keyState:X}, ModState=0x{modState:X}");

                switch (id)
                {
                    case START_HOTKEY_ID:
                        System.Diagnostics.Debug.WriteLine("触发开始热键");
                        if (!_isSequenceRunning)
                        {
                            StartSequence();
                        }
                        StartHotkeyPressed?.Invoke();
                        handled = true;
                        break;

                    case STOP_HOTKEY_ID:
                        System.Diagnostics.Debug.WriteLine("触发停止热键");
                        StopSequence();
                        StopHotkeyPressed?.Invoke();
                        handled = true;
                        break;
                }
            }
            
            return IntPtr.Zero;
        }

        // 释放资源
        public void Dispose()
        {
            StopSequence();
            UnregisterHotKey();
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }

        public bool RegisterStartHotkey(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            if (_windowHandle == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("错误：��口句柄无效");
                return false;
            }

            try
            {
                // 先注销已有的热键
                UnregisterHotKey(_windowHandle, START_HOTKEY_ID);

                _startVirtualKey = GetVirtualKeyFromDDKey(ddKeyCode);
                if (_startVirtualKey == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"错误：无法转换DD键码 {ddKeyCode}");
                    return false;
                }

                _startDDKey = ddKeyCode;
                
                uint modifierFlags = 0;
                if (modifiers.HasFlag(ModifierKeys.Alt)) modifierFlags |= 0x0001;
                if (modifiers.HasFlag(ModifierKeys.Control)) modifierFlags |= 0x0002;
                if (modifiers.HasFlag(ModifierKeys.Shift)) modifierFlags |= 0x0004;

                bool result = RegisterHotKey(_windowHandle, START_HOTKEY_ID, modifierFlags, (uint)_startVirtualKey);
                System.Diagnostics.Debug.WriteLine($"注册开始热键: VK=0x{_startVirtualKey:X2}, Mods=0x{modifierFlags:X}, Result={result}");
                
                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();
                    System.Diagnostics.Debug.WriteLine($"注册失败，错误码: {error}");
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册热键异常: {ex.Message}");
                return false;
            }
        }

        public bool RegisterStopHotkey(DDKeyCode ddKeyCode, ModifierKeys modifiers)
        {
            try
            {
                _stopVirtualKey = GetVirtualKeyFromDDKey(ddKeyCode);
                if (_stopVirtualKey == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"无法找到对应的虚拟键码: {ddKeyCode}");
                    return false;
                }

                _stopDDKey = ddKeyCode;
                UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
                return RegisterHotKey(_windowHandle, STOP_HOTKEY_ID, (uint)modifiers, (uint)_stopVirtualKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册停止热键异常: {ex.Message}");
                return false;
            }
        }

        // 新增方法：检查序列模式状态
        public bool IsSequenceRunning => _isSequenceRunning;

        // 新增方法：手动触发按键
        public async Task TriggerKeyAsync(DDKeyCode keyCode)
        {
            if (!_isSequenceRunning)
            {
                System.Diagnostics.Debug.WriteLine($"序列未运行，忽略按键: {keyCode}");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"开始触发按键: {keyCode}");
                KeyTriggered?.Invoke(keyCode);
                bool result = await Task.Run(() => _ddDriverService.SimulateKeyPress(keyCode));
                System.Diagnostics.Debug.WriteLine($"按键触发{(result ? "成功" : "失败")}: {keyCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"触发按键异常: {keyCode}\n{ex.Message}\n{ex.StackTrace}");
            }
        }

        // 新增方法：停止序列模式
        public void StopSequence()
        {
            if (!_isSequenceRunning) return;

            _isSequenceRunning = false;
            SequenceModeStopped?.Invoke();
            System.Diagnostics.Debug.WriteLine("序列已停止");
            if (_sequenceCts != null)
            {
                _sequenceCts.Cancel();
                _sequenceCts.Dispose();
                _sequenceCts = null;
            }
        }

        // 设置按键列表和间隔
        public void SetKeySequence(List<DDKeyCode> keyList, int interval)
        {
            if (keyList == null || keyList.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("警告：试图设置空的按键序列");
                return;
            }
            
            _keyList = new List<DDKeyCode>(keyList); // 创建新的列表副本
            _keyInterval = Math.Max(1, interval);
            System.Diagnostics.Debug.WriteLine($"更新按键序列 - 按键数量: {_keyList.Count}, 间隔: {_keyInterval}ms");
        }

        // 新增：启动序列
        private async void StartSequence()
        {
            if (_isSequenceRunning)
            {
                System.Diagnostics.Debug.WriteLine("序列已在运行中，忽略启动请求");
                return;
            }

            if (_keyList.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("按键列表为空，无法启动序列");
                return;
            }

            try
            {
                _isSequenceRunning = true;
                SequenceModeStarted?.Invoke();
                System.Diagnostics.Debug.WriteLine($"开始序列，按键数量: {_keyList.Count}, 间隔: {_keyInterval}ms");
                _sequenceCts = new CancellationTokenSource();

                while (!_sequenceCts.Token.IsCancellationRequested)
                {
                    foreach (var ddKeyCode in _keyList)
                    {
                        if (_sequenceCts.Token.IsCancellationRequested)
                        {
                            break;
                        }
                        
                        bool success = await Task.Run(() => _ddDriverService.SimulateKeyPress(ddKeyCode));
                        if (!success)
                        {
                            System.Diagnostics.Debug.WriteLine($"按键执行失败: {ddKeyCode}，停止序列");
                            StopSequence();
                            return;
                        }
                        
                        await Task.Delay(_keyInterval, _sequenceCts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("序列被取消");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"序列执行异常: {ex.Message}");
            }
            finally
            {
                _isSequenceRunning = false;
                SequenceModeStopped?.Invoke();
                System.Diagnostics.Debug.WriteLine("序列已停止");
            }
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
                System.Diagnostics.Debug.WriteLine($"尝试转换DD键码: {ddKeyCode} ({(int)ddKeyCode})");
                
                // 特殊处理数字键盘键
                if (ddKeyCode >= DDKeyCode.NUM_0 && ddKeyCode <= DDKeyCode.NUM_9)
                {
                    int numValue = (int)ddKeyCode - 200; // NUM_0 的枚举值是200
                    return 0x60 + numValue; // VK_NUMPAD0 是 0x60
                }

                // 检查映射表中的所有项
                foreach (var pair in KeyCodeMapping.VirtualToDDKeyMap)
                {
                    if (pair.Value == ddKeyCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"找到匹配的虚拟键码: 0x{pair.Key:X2}");
                        return pair.Key;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"未找到匹配的虚拟键码: {ddKeyCode}");
                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"转换DD键码时发生异常: {ex.Message}");
                return 0;
            }
        }

        // 添加新方法用于检查热键状态
        private bool IsHotkeyPressed(int hotkeyId)
        {
            if (hotkeyId == START_HOTKEY_ID && _startDDKey.HasValue)
            {
                return IsKeyPressed(_startDDKey.Value);
            }
            else if (hotkeyId == STOP_HOTKEY_ID && _stopDDKey.HasValue)
            {
                return IsKeyPressed(_stopDDKey.Value);
            }
            return false;
        }

        // 添加加载和注册热键的方法
        private void LoadAndRegisterHotkeys()
        {
            try
            {
                // 这里应该从配置中加载已保存的热键
                if (_startDDKey.HasValue)
                {
                    RegisterStartHotkey(_startDDKey.Value, ModifierKeys.None);
                }
                if (_stopDDKey.HasValue)
                {
                    RegisterStopHotkey(_stopDDKey.Value, ModifierKeys.None);
                }
                System.Diagnostics.Debug.WriteLine("热键注册完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册热键时发生错误: {ex.Message}");
            }
        }
    }
}