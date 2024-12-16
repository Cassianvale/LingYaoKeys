using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Security.Principal;

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

        private const int HOTKEY_ID = 80;
        private IntPtr _windowHandle;
        private Window _mainWindow;
        private HwndSource? _source;
        private bool _isRegistered;

        public event Action? StartHotkeyPressed;
        public event Action? StartHotkeyReleased;
        public event Action? StopHotkeyPressed;

        private const int START_HOTKEY_ID = 1;
        private const int STOP_HOTKEY_ID = 2;

        private DDKeyCode? _startHotkey;
        private DDKeyCode? _stopHotkey;

        // 构造函数
        public HotkeyService(Window mainWindow)
        {
            _mainWindow = mainWindow;
            _mainWindow.SourceInitialized += MainWindow_SourceInitialized;

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

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _windowHandle = new WindowInteropHelper(_mainWindow).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            if (_source != null)
            {
                _source.AddHook(WndProc);
            }
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
            const int WM_KEYUP = 0x0101;

            switch (msg)
            {
                case WM_HOTKEY:
                    int id = wParam.ToInt32();
                    switch (id)
                    {
                        case START_HOTKEY_ID:
                            StartHotkeyPressed?.Invoke();
                            handled = true;
                            break;
                        case STOP_HOTKEY_ID:
                            StopHotkeyPressed?.Invoke();
                            handled = true;
                            break;
                    }
                    break;

                case WM_KEYUP:
                    int vkCode = wParam.ToInt32();
                    if (_startHotkey.HasValue && vkCode == (int)_startHotkey.Value)
                    {
                        StartHotkeyReleased?.Invoke();
                        handled = true;
                    }
                    break;
            }
            
            return IntPtr.Zero;
        }

        // 释放资源
        public void Dispose()
        {
            UnregisterHotKey();
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
            _mainWindow.SourceInitialized -= MainWindow_SourceInitialized;
        }

        public bool RegisterStartHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            try
            {
                _startHotkey = keyCode;
                UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
                return RegisterHotKey(_windowHandle, START_HOTKEY_ID, (uint)modifiers, (uint)keyCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册开始热键异常: {ex.Message}");
                return false;
            }
        }

        public bool RegisterStopHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            try
            {
                _stopHotkey = keyCode;
                UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
                return RegisterHotKey(_windowHandle, STOP_HOTKEY_ID, (uint)modifiers, (uint)keyCode);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"注册停止热键异常: {ex.Message}");
                return false;
            }
        }
    }
} 