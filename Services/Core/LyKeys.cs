using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using System.Security.Principal;

namespace WpfApp.Services
{
    /// <summary>
    /// LyKeys驱动接口封装类
    /// </summary>
    public sealed class LyKeys : IDisposable
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private volatile bool _isDisposed;
        private volatile bool _isInitialized;
        private IntPtr _driverHandle = IntPtr.Zero;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(1);
        private readonly object _lockObject = new object();
        private readonly SemaphoreSlim _driverSemaphore = new SemaphoreSlim(1, 1);
        private const int DRIVER_OPERATION_TIMEOUT = 5000; // 5秒超时

        #region 设备状态枚举
        public enum DeviceStatus
        {
            DEVICE_STATUS_UNKNOWN = 0,    // 未知状态
            DEVICE_STATUS_READY = 1,      // 设备就绪
            DEVICE_STATUS_ERROR = 2       // 设备错误
        }
        #endregion

        #region DLL导入
        private const string DLL_NAME = "lykeysdll.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool LoadNTDriver([MarshalAs(UnmanagedType.LPStr)] string lpszDriverName, [MarshalAs(UnmanagedType.LPStr)] string lpszDriverPath);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool UnloadNTDriver([MarshalAs(UnmanagedType.LPStr)] string szSvrName);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SetHandle();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GetDriverHandle();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int GetDriverStatus();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong GetLastCheckTime();

        // 键盘操作
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void KeyDown(ushort VirtualKey);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void KeyUp(ushort VirtualKey);

        // 鼠标操作
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseMoveRELATIVE(int dx, int dy);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseMoveABSOLUTE(int dx, int dy);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseLeftButtonDown();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseLeftButtonUp();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseRightButtonDown();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseRightButtonUp();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseMiddleButtonDown();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseMiddleButtonUp();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseXButton1Down();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseXButton1Up();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseXButton2Down();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseXButton2Up();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseWheelUp(ushort wheelDelta);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseWheelDown(ushort wheelDelta);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetLastError();

        [DllImport("kernel32.dll")]
        private static extern void SetLastError(uint dwErrCode);
        #endregion

        #region 设备状态常量
        private const uint STATUS_SUCCESS = 0x00000000;
        private const uint STATUS_UNSUCCESSFUL = 0xC0000001;
        private const uint STATUS_NOT_SUPPORTED = 0xC00000BB;
        private const uint STATUS_INVALID_PARAMETER = 0xC000000D;
        private const uint STATUS_INSUFFICIENT_RESOURCES = 0xC000009A;
        private const uint STATUS_DEVICE_NOT_CONNECTED = 0xC000009D;
        #endregion

        #region 初始化和释放
        private const string DRIVER_RESOURCE_DIR = "Resource\\lykeysdll";
        private readonly string[] REQUIRED_FILES = { "lykeys.sys", "lykeysdll.dll", "lykeys.cat" };

        /// <summary>
        /// 卸载驱动
        /// </summary>
        private void UnloadDriver()
        {
            if (!_isInitialized) return;

            try
            {
                _logger.Debug("尝试卸载现有驱动...");
                UnloadNTDriver("lykeys");
                Thread.Sleep(1000); // 等待驱动完全卸载
                _isInitialized = false;
                _logger.Debug("现有驱动卸载完成");
            }
            catch (Exception ex)
            {
                _logger.Warning($"卸载驱动异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载驱动
        /// </summary>
        private bool LoadDriver(string sysPath)
        {
            if (_isInitialized)
            {
                _logger.Warning("驱动已经初始化");
                return true;
            }

            try
            {
                _logger.Debug($"开始加载驱动文件: {sysPath}");
                
                // 规范化路径并验证
                sysPath = Path.GetFullPath(sysPath);
                if (!ValidateDriverFile(sysPath)) return false;

                // 重置错误码
                SetLastError(0);
                
                // 使用服务名称作为驱动名称
                const string driverName = "lykeys";
                _logger.Debug($"加载驱动路径: {sysPath}");
                
                bool loadResult = LoadNTDriver(driverName, sysPath);
                uint error = GetLastError();
                
                // 只要错误码为0，就认为是成功的
                if (error == 0)
                {
                    if (!loadResult)
                    {
                        _logger.Debug("驱动已存在，继续初始化");
                    }
                }
                else
                {
                    _logger.Error($"加载驱动失败，错误码: 0x{error:X8}");
                    return false;
                }
                
                _logger.Debug("等待驱动初始化...");
                Thread.Sleep(100);
                
                var status = (DeviceStatus)GetDriverStatus();
                _logger.Debug($"驱动状态: {status}");
                
                // 只有ERROR状态才是错误
                if (status == DeviceStatus.DEVICE_STATUS_ERROR)
                {
                    _logger.Error("驱动初始化失败");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"加载驱动异常: {ex.Message}");
                return false;
            }
        }

        private bool ValidateDriverFile(string sysPath)
        {
            if (!File.Exists(sysPath))
            {
                _logger.Error($"驱动文件不存在: {sysPath}");
                return false;
            }

            try
            {
                using var fs = File.OpenRead(sysPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"无法访问驱动文件: {ex.Message}");
                return false;
            }
        }

        private string GetStatusDescription(uint status)
        {
            return status switch
            {
                STATUS_SUCCESS => "成功",
                STATUS_UNSUCCESSFUL => "操作失败",
                STATUS_NOT_SUPPORTED => "不支持的操作",
                STATUS_INVALID_PARAMETER => "无效的参数",
                STATUS_INSUFFICIENT_RESOURCES => "资源不足",
                STATUS_DEVICE_NOT_CONNECTED => "设备未连接",
                _ => $"未知错误: 0x{status:X8}"
            };
        }

        private bool ValidateDriverStatus()
        {
            var status = (DeviceStatus)GetDriverStatus();
            _logger.Debug($"驱动状态: {status}");
            
            if (status == DeviceStatus.DEVICE_STATUS_ERROR)
            {
                _logger.Error($"驱动加载失败: 设备状态 {status}");
                return false;
            }else{
                _logger.Debug("驱动状态正常");
                return true;
            }
            

        }

        /// <summary>
        /// 初始化驱动
        /// </summary>
        public bool Initialize(string driverPath)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(LyKeys));

            if (_isInitialized)
            {
                _logger.Warning("驱动已经初始化");
                return true;
            }

            try
            {
                _driverSemaphore.Wait();

                if (!IsAdministrator())
                {
                    _logger.Error("需要管理员权限才能加载驱动，请以管理员身份运行程序");
                    return false;
                }

                _logger.Debug($"开始初始化驱动，路径: {driverPath}");

                if (!InitializeDriverEnvironment(driverPath))
                    return false;

                // 卸载现有驱动
                UnloadDriver();

                // 加载驱动
                string sysPath = Path.Combine(driverPath, "lykeys.sys");
                if (!LoadDriver(sysPath))
                    return false;

                // 设置驱动句柄
                if (!InitializeDriverHandle())
                    return false;

                _isInitialized = true;
                _logger.InitLog("驱动初始化成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("驱动初始化过程中发生异常", ex);
                return false;
            }
            finally
            {
                _driverSemaphore.Release();
            }
        }

        private bool InitializeDriverEnvironment(string driverPath)
        {
            // 设置DLL搜索路径
            if (!SetDllDirectory(driverPath))
            {
                _logger.Error($"设置DLL搜索路径失败: {driverPath}, 错误码: {Marshal.GetLastWin32Error()}");
                return false;
            }

            // 验证文件路径
            foreach (var file in REQUIRED_FILES)
            {
                string filePath = Path.Combine(driverPath, file);
                if (!File.Exists(filePath))
                {
                    _logger.Error($"找不到必需的驱动文件: {filePath}");
                    return false;
                }
                
                var fileInfo = new FileInfo(filePath);
                _logger.Debug($"已找到驱动文件: {filePath}, 大小: {fileInfo.Length} 字节");
            }

            // 尝试预加载DLL
            string dllPath = Path.Combine(driverPath, "lykeysdll.dll");
            try
            {
                if (LoadLibrary(dllPath) == IntPtr.Zero)
                {
                    _logger.Error($"预加载DLL失败: {dllPath}, 错误码: {Marshal.GetLastWin32Error()}");
                    return false;
                }
                _logger.Debug("DLL预加载成功");
            }
            catch (Exception ex)
            {
                _logger.Error($"预加载DLL异常: {ex.Message}");
                return false;
            }

            return true;
        }

        private bool InitializeDriverHandle()
        {
            try
            {
                _logger.Debug("开始设置驱动句柄");

                // 检查驱动状态
                var status = (DeviceStatus)GetDriverStatus();
                _logger.Debug($"设置句柄前驱动状态: {status}");
                
                // 只有ERROR状态才是错误
                if (status == DeviceStatus.DEVICE_STATUS_ERROR)
                {
                    _logger.Error("驱动状态错误，无法设置句柄");
                    return false;
                }

                // 设置句柄
                if (!SetHandle())
                {
                    uint error = GetLastError();
                    // 错误码为0时表示成功
                    if (error == 0)
                    {
                        _logger.Debug("驱动句柄设置成功");
                    }
                    else
                    {
                        _logger.Error($"设置驱动句柄失败，错误码: 0x{error:X8}");
                        return false;
                    }
                }

                // 获取句柄
                _driverHandle = GetDriverHandle();
                if (_driverHandle == IntPtr.Zero)
                {
                    uint error = GetLastError();
                    if (error == 0)
                    {
                        _logger.Debug("驱动句柄获取成功");
                    }
                    else
                    {
                        _logger.Error($"获取驱动句柄失败，错误码: 0x{error:X8}");
                        return false;
                    }
                }

                _logger.Debug($"驱动句柄初始化完成: {_driverHandle}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"初始化驱动句柄时发生异常: {ex.Message}");
                return false;
            }
        }

        private bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                _logger.Error("检查管理员权限时发生异常", ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                lock (_lockObject)
                {
                    if (!_isDisposed)
                    {
                        try
                        {
                            UnloadDriver();
                            _driverHandle = IntPtr.Zero;
                            _driverSemaphore.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("释放驱动资源时发生异常", ex);
                        }
                        finally
                        {
                            _isDisposed = true;
                        }
                    }
                }
            }
        }
        #endregion

        #region 状态检查
        /// <summary>
        /// 检查设备状态
        /// </summary>
        /// <returns>设备状态</returns>
        public DeviceStatus CheckDeviceStatus()
        {
            if (_isDisposed)
                return DeviceStatus.DEVICE_STATUS_UNKNOWN;

            try
            {
                var currentTime = DateTime.Now;
                if (currentTime - _lastCheckTime >= _checkInterval)
                {
                    lock (_lockObject)
                    {
                        if (currentTime - _lastCheckTime >= _checkInterval)
                        {
                            var status = (DeviceStatus)GetDriverStatus();
                            _lastCheckTime = currentTime;
                            return status;
                        }
                    }
                }
                return DeviceStatus.DEVICE_STATUS_READY;
            }
            catch (Exception ex)
            {
                _logger.Error("检查设备状态时发生异常", ex);
                return DeviceStatus.DEVICE_STATUS_ERROR;
            }
        }

        /// <summary>
        /// 获取上次检查时间
        /// </summary>
        public DateTime GetLastCheckTimeAsDateTime()
        {
            try
            {
                return DateTime.FromFileTime((long)GetLastCheckTime());
            }
            catch (Exception ex)
            {
                _logger.Error("获取上次检查时间时发生异常", ex);
                return DateTime.MinValue;
            }
        }
        #endregion

        #region 键盘操作
        /// <summary>
        /// 按下按键
        /// </summary>
        /// <param name="vkCode">虚拟键码</param>
        /// <returns>操作是否成功</returns>
        public bool SendKeyDown(ushort vkCode)
        {
            if (_isDisposed || !_isInitialized)
                return false;

            try
            {
                if (CheckDeviceStatus() == DeviceStatus.DEVICE_STATUS_READY)
                {
                    KeyDown(vkCode);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"按下按键失败, vkCode: {vkCode}", ex);
                return false;
            }
        }

        /// <summary>
        /// 释放按键
        /// </summary>
        /// <param name="vkCode">虚拟键码</param>
        /// <returns>操作是否成功</returns>
        public bool SendKeyUp(ushort vkCode)
        {
            if (_isDisposed)
                return false;

            try
            {
                if (CheckDeviceStatus() == DeviceStatus.DEVICE_STATUS_READY)
                {
                    KeyUp(vkCode);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"释放按键失败, vkCode: {vkCode}", ex);
                return false;
            }
        }

        /// <summary>
        /// 按下并释放按键
        /// </summary>
        /// <param name="vkCode">虚拟键码</param>
        /// <param name="duration">按下持续时间(毫秒)</param>
        /// <returns>操作是否成功</returns>
        public async Task<bool> SendKeyPressAsync(ushort vkCode, int duration = 100)
        {
            if (_isDisposed)
                return false;

            try
            {
                if (CheckDeviceStatus() != DeviceStatus.DEVICE_STATUS_READY)
                    return false;

                if (!SendKeyDown(vkCode))
                    return false;

                await Task.Delay(duration);
                return SendKeyUp(vkCode);
            }
            catch (Exception ex)
            {
                _logger.Error($"按键操作失败, vkCode: {vkCode}", ex);
                return false;
            }
        }
        #endregion

        #region 鼠标操作
        public enum MouseButtonType
        {
            Left,
            Right,
            Middle,
            XButton1,
            XButton2
        }

        /// <summary>
        /// 相对移动鼠标
        /// </summary>
        public bool MoveMouse(int dx, int dy)
        {
            if (_isDisposed)
                return false;

            try
            {
                if (CheckDeviceStatus() == DeviceStatus.DEVICE_STATUS_READY)
                {
                    MouseMoveRELATIVE(dx, dy);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"相对移动鼠标失败, dx: {dx}, dy: {dy}", ex);
                return false;
            }
        }

        /// <summary>
        /// 绝对移动鼠标
        /// </summary>
        public bool MoveMouseAbsolute(int x, int y)
        {
            if (_isDisposed)
                return false;

            try
            {
                if (CheckDeviceStatus() == DeviceStatus.DEVICE_STATUS_READY)
                {
                    MouseMoveABSOLUTE(x, y);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"绝对移动鼠标失败, x: {x}, y: {y}", ex);
                return false;
            }
        }

        /// <summary>
        /// 鼠标按键操作
        /// </summary>
        public bool SendMouseButton(MouseButtonType button, bool isDown)
        {
            if (_isDisposed)
                return false;

            try
            {
                if (CheckDeviceStatus() != DeviceStatus.DEVICE_STATUS_READY)
                    return false;

                switch (button)
                {
                    case MouseButtonType.Left:
                        if (isDown) MouseLeftButtonDown();
                        else MouseLeftButtonUp();
                        break;
                    case MouseButtonType.Right:
                        if (isDown) MouseRightButtonDown();
                        else MouseRightButtonUp();
                        break;
                    case MouseButtonType.Middle:
                        if (isDown) MouseMiddleButtonDown();
                        else MouseMiddleButtonUp();
                        break;
                    case MouseButtonType.XButton1:
                        if (isDown) MouseXButton1Down();
                        else MouseXButton1Up();
                        break;
                    case MouseButtonType.XButton2:
                        if (isDown) MouseXButton2Down();
                        else MouseXButton2Up();
                        break;
                    default:
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"鼠标按键操作失败, button: {button}, isDown: {isDown}", ex);
                return false;
            }
        }

        /// <summary>
        /// 鼠标点击操作
        /// </summary>
        public async Task<bool> MouseClickAsync(MouseButtonType button, int duration = 100)
        {
            if (_isDisposed)
                return false;

            try
            {
                if (!SendMouseButton(button, true))
                    return false;

                await Task.Delay(duration);
                return SendMouseButton(button, false);
            }
            catch (Exception ex)
            {
                _logger.Error($"鼠标点击操作失败, button: {button}", ex);
                return false;
            }
        }

        /// <summary>
        /// 鼠标滚轮操作
        /// </summary>
        public bool MouseWheel(bool isUp, ushort delta = 120)
        {
            if (_isDisposed)
                return false;

            try
            {
                if (CheckDeviceStatus() == DeviceStatus.DEVICE_STATUS_READY)
                {
                    if (isUp) MouseWheelUp(delta);
                    else MouseWheelDown(delta);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"鼠标滚轮操作失败, isUp: {isUp}, delta: {delta}", ex);
                return false;
            }
        }
        #endregion
    }
}
