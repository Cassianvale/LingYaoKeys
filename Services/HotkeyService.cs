using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

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

        public event Action? HotkeyPressed;
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
            // 等待窗口初始化完成后再初始化热键服务
            _mainWindow.SourceInitialized += MainWindow_SourceInitialized;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _windowHandle = new WindowInteropHelper(_mainWindow).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source?.AddHook(WndProc);
        }

        // 注册快捷键
        public bool RegisterHotKey(Key key, ModifierKeys modifiers = ModifierKeys.None)
        {
            if (_isRegistered) return false;

            _isRegistered = RegisterHotKey(_windowHandle, HOTKEY_ID, (uint)modifiers, (uint)KeyInterop.VirtualKeyFromKey(key));
            
            if (!_isRegistered)
            {
                MessageBox.Show("快捷键被占用！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return _isRegistered;
        }

        // 取消注册快捷键
        public void UnregisterHotKey()
        {
            if (!_isRegistered) return;
            
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
        }

        // 处理热键事件
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            const int WM_KEYUP = 0x0101;

            if (msg == WM_HOTKEY)
            {
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
            }
            else if (msg == WM_KEYUP)
            {
                int vkCode = wParam.ToInt32();
                if (_startHotkey.HasValue && vkCode == (int)_startHotkey.Value)
                {
                    StartHotkeyReleased?.Invoke();
                    handled = true;
                }
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
            _startHotkey = keyCode;  // 保存开始热键
            UnregisterHotKey(_windowHandle, START_HOTKEY_ID);
            return RegisterHotKey(_windowHandle, START_HOTKEY_ID, (uint)modifiers, (uint)keyCode);
        }

        public bool RegisterStopHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            _stopHotkey = keyCode;  // 保存停止热键
            UnregisterHotKey(_windowHandle, STOP_HOTKEY_ID);
            return RegisterHotKey(_windowHandle, STOP_HOTKEY_ID, (uint)modifiers, (uint)keyCode);
        }
    }
} 