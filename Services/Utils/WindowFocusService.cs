using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.ComponentModel;
using System.Diagnostics;


namespace WpfApp.Services
{
    public class WindowFocusService : IDisposable
    {
        private static readonly Lazy<WindowFocusService> _instance = 
            new Lazy<WindowFocusService>(() => new WindowFocusService());
        
        public static WindowFocusService Instance => _instance.Value;

        private readonly SerilogManager _logger = SerilogManager.Instance;
        private IntPtr _targetWindowHandle;
        private bool _isEnabled;
        private bool _isTargetWindowActive;
        private string _targetWindowTitle;
        private string _targetWindowClassName;
        private string _targetWindowProcessName;
        private readonly object _lockObject = new object();

        public event EventHandler<bool> TargetWindowActiveChanged;
        public event EventHandler<IntPtr> ActiveWindowChanged;

        private WindowFocusService()
        {
            // 开始监听窗口焦点变化
            StartWindowFocusMonitor();
        }

        public bool IsTargetWindowActive
        {
            get => _isTargetWindowActive;
            private set
            {
                if (_isTargetWindowActive != value)
                {
                    _isTargetWindowActive = value;
                    TargetWindowActiveChanged?.Invoke(this, value);
                    _logger.Debug($"目标窗口活动状态变化: {value}, 句柄: {_targetWindowHandle}");
                }
            }
        }

        public void SetTargetWindow(IntPtr handle, string title, string className, string processName)
        {
            lock (_lockObject)
            {
                _targetWindowHandle = handle;
                _targetWindowTitle = title;
                _targetWindowClassName = className;
                _targetWindowProcessName = processName;
                _isEnabled = handle != IntPtr.Zero;

                // 立即检查当前活动窗口
                CheckActiveWindow();
                
                _logger.Info($"设置目标窗口 - 句柄: {handle}, 标题: {title}, 类名: {className}, 进程名: {processName}");
            }
        }

        public void ClearTargetWindow()
        {
            lock (_lockObject)
            {
                _targetWindowHandle = IntPtr.Zero;
                _targetWindowTitle = string.Empty;
                _targetWindowClassName = string.Empty;
                _targetWindowProcessName = string.Empty;
                _isEnabled = false;
                IsTargetWindowActive = false;
                
                _logger.Info("清除目标窗口");
            }
        }

        private void StartWindowFocusMonitor()
        {
            try
            {
                // 注册到应用程序消息循环
                if (System.Windows.Application.Current?.MainWindow != null)
                {
                    var source = HwndSource.FromHwnd(new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle);
                    source?.AddHook(WndProc);
                    _logger.Debug("窗口焦点监听已启动");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("启动窗口焦点监听失败", ex);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_ACTIVATEAPP = 0x001C;
            const int WM_ACTIVATE = 0x0006;

            try
            {
                if ((msg == WM_ACTIVATEAPP || msg == WM_ACTIVATE) && _isEnabled)
                {
                    CheckActiveWindow();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("处理窗口消息时发生异常", ex);
            }

            return IntPtr.Zero;
        }

        private void CheckActiveWindow()
        {
            if (!_isEnabled) return;

            try
            {
                var activeWindow = GetForegroundWindow();
                ActiveWindowChanged?.Invoke(this, activeWindow);

                if (_targetWindowHandle != IntPtr.Zero)
                {
                    // 获取活动窗口的进程ID
                    GetWindowThreadProcessId(activeWindow, out uint activeProcId);
                    GetWindowThreadProcessId(_targetWindowHandle, out uint targetProcId);

                    // 首先比较进程ID
                    if (activeProcId == targetProcId)
                    {
                        // 如果进程ID相同，进一步比较窗口类名和标题
                        var activeClassName = GetWindowClassName(activeWindow);
                        var activeTitle = GetWindowTitle(activeWindow);

                        bool isTargetWindow = activeClassName == _targetWindowClassName &&
                                           !string.IsNullOrEmpty(activeTitle) &&
                                           activeTitle.Contains(_targetWindowTitle);

                        IsTargetWindowActive = isTargetWindow;
                    }
                    else
                    {
                        IsTargetWindowActive = false;
                    }
                }
                else
                {
                    IsTargetWindowActive = false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("检查活动窗口时发生异常", ex);
                IsTargetWindowActive = false;
            }
        }

        private string GetWindowClassName(IntPtr hWnd)
        {
            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            return className.ToString().Trim();
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            StringBuilder title = new StringBuilder(256);
            GetWindowText(hWnd, title, title.Capacity);
            return title.ToString().Trim();
        }

        public void Dispose()
        {
            // 清理资源
            ClearTargetWindow();
        }

        #region Win32 API
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        #endregion
    }
} 