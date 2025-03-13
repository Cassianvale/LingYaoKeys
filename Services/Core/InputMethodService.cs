using System.Runtime.InteropServices;
using WpfApp.Services.Utils;

// 输入法服务
namespace WpfApp.Services.Core
{
    public class InputMethodService
    {
        private const int WM_INPUTLANGCHANGEREQUEST = 0x0050;
        private const int INPUTLANGCHANGE_FORWARD = 0x0002;

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        private readonly SerilogManager _logger = SerilogManager.Instance;
        private IntPtr _previousLayout;
        private bool _hasStoredLayout;

        // 保存当前输入法状态
        public void StoreCurrentLayout()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    uint threadId = GetWindowThreadProcessId(hwnd, IntPtr.Zero);
                    _previousLayout = GetKeyboardLayout(threadId);
                    _hasStoredLayout = true;
                    _logger.Debug($"已保存当前输入法状态: 0x{_previousLayout.ToInt64():X8}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("保存输入法状态异常", ex);
            }
        }

        // 切换到英文输入法
        public void SwitchToEnglish()
        {
            try
            {
                // 如果还没有保存当前状态，先保存
                if (!_hasStoredLayout)
                {
                    StoreCurrentLayout();
                }

                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    // 加载英文键盘布局
                    IntPtr layout = LoadKeyboardLayout("00000409", 1);
                    if (layout != IntPtr.Zero)
                    {
                        // 发送切换输入法消息
                        PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, (IntPtr)INPUTLANGCHANGE_FORWARD, layout);
                        _logger.Debug("已切换到英文输入法");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("切换输入法异常", ex);
            }
        }

        // 恢复之前的输入法状态
        public void RestorePreviousLayout()
        {
            try
            {
                if (!_hasStoredLayout)
                {
                    _logger.Warning("没有保存的输入法状态可供恢复");
                    return;
                }

                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero && _previousLayout != IntPtr.Zero)
                {
                    // 发送切换输入法消息，恢复到之前的状态
                    PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, (IntPtr)INPUTLANGCHANGE_FORWARD, _previousLayout);
                    _logger.Debug($"已恢复到之前的输入法状态: 0x{_previousLayout.ToInt64():X8}");
                    _hasStoredLayout = false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("恢复输入法状态异常", ex);
            }
        }
    }
} 