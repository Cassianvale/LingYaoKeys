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
        private HwndSource _source;
        private bool _isRegistered;

        public event Action? HotkeyPressed;

        // 构造函数
        public HotkeyService(Window mainWindow)
        {
            _mainWindow = mainWindow;
            _windowHandle = new WindowInteropHelper(_mainWindow).Handle;
            _source = HwndSource.FromHwnd(_windowHandle);
            _source.AddHook(WndProc);
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

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }

            return IntPtr.Zero;
        }

        // 释放资源
        public void Dispose()
        {
            UnregisterHotKey();
            _source.RemoveHook(WndProc);
        }
    }
} 