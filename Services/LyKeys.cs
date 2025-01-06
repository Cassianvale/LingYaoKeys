using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

namespace WpfApp.Services
{
    /// <summary>
    /// LyKeys驱动接口封装类
    /// </summary>
    public class LyKeys : IDisposable
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private bool _isDisposed;
        private IntPtr _driverHandle = IntPtr.Zero;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1); // 状态检查间隔

        #region 设备状态枚举
        public enum DeviceStatus
        {
            Unknown = 0,
            Ready = 1,
            Error = 2
        }
        #endregion

        #region DLL导入
        [DllImport("lykeysdll.dll")]
        private static extern bool LoadNTDriver(string lpszDriverName, string lpszDriverPath);

        [DllImport("lykeysdll.dll")]
        private static extern bool UnloadNTDriver(string szSvrName);

        [DllImport("lykeysdll.dll")]
        private static extern bool SetHandle();

        [DllImport("lykeysdll.dll")]
        private static extern IntPtr GetDriverHandle();

        [DllImport("lykeysdll.dll")]
        private static extern int GetDriverStatus();

        [DllImport("lykeysdll.dll")]
        private static extern ulong GetLastCheckTime();

        // 键盘操作
        [DllImport("lykeysdll.dll")]
        private static extern void KeyDown(ushort VirtualKey);

        [DllImport("lykeysdll.dll")]
        private static extern void KeyUp(ushort VirtualKey);

        // 鼠标操作
        [DllImport("lykeysdll.dll")]
        private static extern void MouseMoveRELATIVE(int dx, int dy);

        [DllImport("lykeysdll.dll")]
        private static extern void MouseMoveABSOLUTE(int dx, int dy);

        [DllImport("lykeysdll.dll")]
        private static extern void MouseLeftButtonDown();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseLeftButtonUp();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseRightButtonDown();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseRightButtonUp();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseMiddleButtonDown();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseMiddleButtonUp();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseXButton1Down();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseXButton1Up();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseXButton2Down();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseXButton2Up();

        [DllImport("lykeysdll.dll")]
        private static extern void MouseWheelUp(ushort wheelDelta);

        [DllImport("lykeysdll.dll")]
        private static extern void MouseWheelDown(ushort wheelDelta);
        #endregion

        #region 初始化和释放
        /// <summary>
        /// 初始化驱动
        /// </summary>
        /// <param name="driverPath">驱动文件路径</param>
        /// <returns>是否初始化成功</returns>
        public bool Initialize(string driverPath)
        {
            try
            {
                // 检查驱动文件是否存在
                string sysPath = Path.Combine(driverPath, "lykeys.sys");
                string dllPath = Path.Combine(driverPath, "lykeysdll.dll");
                string catPath = Path.Combine(driverPath, "lykeys.cat");

                if (!File.Exists(sysPath))
                {
                    _logger.Error($"Driver file not found: {sysPath}");
                    return false;
                }

                if (!File.Exists(dllPath))
                {
                    _logger.Error($"DLL file not found: {dllPath}");
                    return false;
                }

                if (!File.Exists(catPath))
                {
                    _logger.Error($"Catalog file not found: {catPath}");
                    return false;
                }

                // 加载驱动
                if (!LoadNTDriver("lykeys", sysPath))
                {
                    _logger.Error("Failed to load lykeys driver");
                    return false;
                }

                if (!SetHandle())
                {
                    _logger.Error("Failed to set driver handle");
                    return false;
                }

                _driverHandle = GetDriverHandle();
                if (_driverHandle == IntPtr.Zero)
                {
                    _logger.Error("Failed to get driver handle");
                    return false;
                }

                _logger.InitLog("Driver initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Driver initialization failed", ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                try
                {
                    UnloadNTDriver("lykeys");
                    _driverHandle = IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    _logger.Error("Driver disposal failed", ex);
                }
                _isDisposed = true;
            }
        }
        #endregion

        #region 状态检查
        /// <summary>
        /// 检查设备状态
        /// </summary>
        /// <returns>是否检查成功</returns>
        public bool CheckDeviceStatus()
        {
            var currentTime = DateTime.Now;
            if (currentTime - _lastCheckTime >= _checkInterval)
            {
                var status = (DeviceStatus)GetDriverStatus();
                _lastCheckTime = currentTime;

                if (status != DeviceStatus.Ready)
                {
                    _logger.Warning($"Driver status abnormal: {status}");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 获取驱动状态
        /// </summary>
        public DeviceStatus GetStatus() => (DeviceStatus)GetDriverStatus();

        /// <summary>
        /// 获取上次检查时间
        /// </summary>
        public ulong GetLastCheck() => GetLastCheckTime();
        #endregion

        #region 键盘操作
        /// <summary>
        /// 按下按键
        /// </summary>
        /// <param name="vkCode">虚拟键码</param>
        public void SendKeyDown(ushort vkCode)
        {
            try
            {
                if (CheckDeviceStatus())
                {
                    KeyDown(vkCode);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Key down failed for vkCode: {vkCode}", ex);
            }
        }

        /// <summary>
        /// 释放按键
        /// </summary>
        /// <param name="vkCode">虚拟键码</param>
        public void SendKeyUp(ushort vkCode)
        {
            try
            {
                if (CheckDeviceStatus())
                {
                    KeyUp(vkCode);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Key up failed for vkCode: {vkCode}", ex);
            }
        }

        /// <summary>
        /// 按下并释放按键
        /// </summary>
        /// <param name="vkCode">虚拟键码</param>
        /// <param name="duration">按下持续时间(毫秒)</param>
        public void SendKeyPress(ushort vkCode, int duration = 100)
        {
            SendKeyDown(vkCode);
            Thread.Sleep(duration);
            SendKeyUp(vkCode);
        }
        #endregion

        #region 鼠标按键枚举
        public enum MouseButton
        {
            Left,
            Right,
            Middle,
            XButton1,
            XButton2
        }
        #endregion

        #region 鼠标操作
        /// <summary>
        /// 相对移动鼠标
        /// </summary>
        public void MoveMouse(int dx, int dy) => MouseMoveRELATIVE(dx, dy);

        /// <summary>
        /// 绝对移动鼠标
        /// </summary>
        public void MoveMouseAbsolute(int x, int y) => MouseMoveABSOLUTE(x, y);

        /// <summary>
        /// 鼠标左键操作
        /// </summary>
        public void MouseLeft(bool isDown)
        {
            try
            {
                if (CheckDeviceStatus())
                {
                    if (isDown) MouseLeftButtonDown();
                    else MouseLeftButtonUp();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Mouse left button operation failed", ex);
            }
        }

        /// <summary>
        /// 鼠标右键操作
        /// </summary>
        public void MouseRight(bool isDown)
        {
            try
            {
                if (CheckDeviceStatus())
                {
                    if (isDown) MouseRightButtonDown();
                    else MouseRightButtonUp();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Mouse right button operation failed", ex);
            }
        }

        /// <summary>
        /// 鼠标中键操作
        /// </summary>
        public void MouseMiddle(bool isDown)
        {
            try
            {
                if (CheckDeviceStatus())
                {
                    if (isDown) MouseMiddleButtonDown();
                    else MouseMiddleButtonUp();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Mouse middle button operation failed", ex);
            }
        }

        /// <summary>
        /// 鼠标X1键操作
        /// </summary>
        public void MouseX1(bool isDown)
        {
            try
            {
                if (CheckDeviceStatus())
                {
                    if (isDown) MouseXButton1Down();
                    else MouseXButton1Up();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Mouse X1 button operation failed", ex);
            }
        }

        /// <summary>
        /// 鼠标X2键操作
        /// </summary>
        public void MouseX2(bool isDown)
        {
            try
            {
                if (CheckDeviceStatus())
                {
                    if (isDown) MouseXButton2Down();
                    else MouseXButton2Up();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Mouse X2 button operation failed", ex);
            }
        }

        /// <summary>
        /// 鼠标点击操作
        /// </summary>
        /// <param name="button">鼠标按键类型</param>
        /// <param name="duration">点击持续时间(毫秒)</param>
        public void MouseClick(MouseButton button, int duration = 100)
        {
            switch (button)
            {
                case MouseButton.Left:
                    MouseLeft(true);
                    Thread.Sleep(duration);
                    MouseLeft(false);
                    break;
                case MouseButton.Right:
                    MouseRight(true);
                    Thread.Sleep(duration);
                    MouseRight(false);
                    break;
                case MouseButton.Middle:
                    MouseMiddle(true);
                    Thread.Sleep(duration);
                    MouseMiddle(false);
                    break;
                case MouseButton.XButton1:
                    MouseX1(true);
                    Thread.Sleep(duration);
                    MouseX1(false);
                    break;
                case MouseButton.XButton2:
                    MouseX2(true);
                    Thread.Sleep(duration);
                    MouseX2(false);
                    break;
            }
        }

        /// <summary>
        /// 鼠标滚轮操作
        /// </summary>
        /// <param name="isUp">是否向上滚动</param>
        /// <param name="delta">滚动量</param>
        public void MouseWheel(bool isUp, ushort delta = 120)
        {
            try
            {
                if (CheckDeviceStatus())
                {
                    if (isUp) MouseWheelUp(delta);
                    else MouseWheelDown(delta);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Mouse wheel operation failed", ex);
            }
        }
        #endregion
    }
}
