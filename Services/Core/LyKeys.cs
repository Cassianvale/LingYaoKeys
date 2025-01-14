using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using System.Security.Principal;
using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using WpfApp.Services;

namespace WpfApp.Services
{
    /// <summary>
    /// LyKeys驱动接口封装类
    /// </summary>
    public sealed class LyKeys : IDisposable
    {
        #region 字段和属性
        private static readonly SerilogManager _logger = SerilogManager.Instance;
        private bool _isDisposed;
        private bool _isInitialized;
        private const string DriverName = "lykeys";
        private readonly string _driverPath;
        private IntPtr _dllHandle = IntPtr.Zero;
        #endregion

        #region 驱动状态枚举
        public enum DeviceStatus
        {
            Unknown = 0,
            Ready = 1,
            Error = 2
        }
        #endregion

        #region 驱动状态定义
        private enum NTSTATUS : uint
        {
            STATUS_SUCCESS = 0x00000000,
            STATUS_UNSUCCESSFUL = 0xC0000001,
            STATUS_NOT_SUPPORTED = 0xC00000BB,
            STATUS_INVALID_PARAMETER = 0xC000000D,
            STATUS_INSUFFICIENT_RESOURCES = 0xC000009A,
            STATUS_DEVICE_NOT_CONNECTED = 0xC000009D
        }
        #endregion

        #region DLL导入
        // 驱动管理函数
        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SetHandle();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool LoadNTDriver(string lpszDriverName, string lpszDriverPath);

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool UnloadNTDriver(string szSvrName);

        // 状态管理函数
        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void CheckDeviceStatus();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern DeviceStatus GetDriverStatus();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong GetLastCheckTime();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetDriverHandle")]
        private static extern IntPtr GetDriverHandle();

        // 键盘操作函数
        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void KeyDown(ushort vkCode);

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void KeyUp(ushort vkCode);

        // 鼠标操作函数
        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseLeftButtonDown();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseLeftButtonUp();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseRightButtonDown();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseRightButtonUp();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseMiddleButtonDown();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseMiddleButtonUp();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseXButton1Down();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseXButton1Up();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseXButton2Down();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseXButton2Up();

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseMoveRELATIVE(int dx, int dy);

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseMoveABSOLUTE(int x, int y);

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseWheelUp(ushort wheelDelta);

        [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void MouseWheelDown(ushort wheelDelta);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private string GetNTStatusMessage(NTSTATUS status)
        {
            return status switch
            {
                NTSTATUS.STATUS_SUCCESS => "操作成功",
                NTSTATUS.STATUS_UNSUCCESSFUL => "操作失败",
                NTSTATUS.STATUS_NOT_SUPPORTED => "不支持的操作",
                NTSTATUS.STATUS_INVALID_PARAMETER => "无效的参数",
                NTSTATUS.STATUS_INSUFFICIENT_RESOURCES => "资源不足",
                NTSTATUS.STATUS_DEVICE_NOT_CONNECTED => "设备未连接",
                _ => $"未知错误: 0x{(uint)status:X8}"
            };
        }
        #endregion

        #region 构造函数和初始化
        public LyKeys(string driverPath)
        {
            _driverPath = driverPath ?? throw new ArgumentNullException(nameof(driverPath));
        }

        public async Task<bool> Initialize(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!IsAdministrator())
                {
                    _logger.Error("需要管理员权限运行");
                    throw new SecurityException("需要管理员权限运行");
                }

                string driverDirectory = Path.GetDirectoryName(_driverPath) ?? throw new InvalidOperationException("无法获取驱动文件目录");
                string dllPath = Path.Combine(driverDirectory, "lykeysdll.dll");
                
                // 检查文件是否存在
                if (!File.Exists(_driverPath))
                {
                    _logger.Error($"sys驱动文件不存在: {_driverPath}");
                    throw new FileNotFoundException($"sys驱动文件不存在: {_driverPath}");
                }
                
                if (!File.Exists(dllPath))
                {
                    _logger.Error($"dll文件不存在: {dllPath}");
                    throw new FileNotFoundException($"dll文件不存在: {dllPath}");
                }

                // 检查文件权限
                try
                {
                    using (var fs = File.OpenRead(_driverPath))
                    {
                        _logger.Debug("成功验证驱动文件访问权限");
                    }
                    using (var fs = File.OpenRead(dllPath))
                    {
                        _logger.Debug("成功验证DLL文件访问权限");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"无法访问文件: {ex.Message}");
                    throw new UnauthorizedAccessException($"无法访问文件: {ex.Message}", ex);
                }

                // 使用Task.Run包装同步操作
                return await Task.Run(async () =>
                {
                    try
                    {
                        // 加载DLL
                        _logger.Debug($"开始加载DLL: {dllPath}");
                        _dllHandle = LoadLibrary(dllPath);
                        if (_dllHandle == IntPtr.Zero)
                        {
                            int error = Marshal.GetLastWin32Error();
                            string errorMessage = new Win32Exception(error).Message;
                            _logger.Error($"DLL加载失败 - 错误代码: {error}, 错误信息: {errorMessage}");
                            throw new Win32Exception(error, $"DLL加载失败: {errorMessage}");
                        }
                        _logger.Debug("DLL加载成功");

                        cancellationToken.ThrowIfCancellationRequested();

                        // 加载驱动
                        _logger.Debug($"开始加载驱动: {DriverName}, 路径: {_driverPath}");
                        bool loadResult = LoadNTDriver(DriverName, _driverPath);
                        if (!loadResult)
                        {
                            int error = Marshal.GetLastWin32Error();
                            string errorMessage = new Win32Exception(error).Message;
                            _logger.Error($"驱动加载失败 - 错误代码: {error}, 错误信息: {errorMessage}");
                            return false;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        // 初始化设备句柄
                        _logger.Debug("开始初始化设备句柄");
                        bool handleResult = SetHandle();
                        _logger.Debug($"设备句柄初始化结果: {handleResult}");
                        if (!handleResult)
                        {
                            int error = Marshal.GetLastWin32Error();
                            string errorMessage = new Win32Exception(error).Message;
                            _logger.Error($"初始化设备句柄失败 - 错误代码: {error}, 错误信息: {errorMessage}");
                            throw new InvalidOperationException($"初始化设备句柄失败: {errorMessage}");
                        }
                        _logger.Debug("设备句柄初始化成功");

                        cancellationToken.ThrowIfCancellationRequested();

                        // 检查设备状态
                        _logger.Debug("开始检查设备状态");
                        CheckDeviceStatus();
                        await Task.Delay(100, cancellationToken); // 等待状态更新
                        var status = GetDriverStatus();
                        _logger.Debug($"设备状态: {status}");
                        
                        if (status != DeviceStatus.Ready)
                        {
                            // 尝试重新初始化句柄
                            _logger.Debug("设备状态不正确，尝试重新初始化句柄");
                            if (!SetHandle())
                            {
                                throw new InvalidOperationException("重新初始化设备句柄失败");
                            }

                            await Task.Delay(500, cancellationToken);
                            CheckDeviceStatus();
                            status = GetDriverStatus();
                            _logger.Debug($"重新初始化后的设备状态: {status}");

                            if (status != DeviceStatus.Ready)
                            {
                                throw new InvalidOperationException($"设备状态异常: {status}");
                            }
                        }

                        _logger.Debug("设备句柄初始化成功");
                        _isInitialized = true;
                        _logger.InitLog("LyKeys驱动初始化成功");
                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Warning("驱动初始化操作被取消");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"驱动初始化失败: {ex.Message}", ex);
                        return false;
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Error($"LyKeys驱动初始化失败: {ex.Message}", ex);
                return false;
            }
        }

        public async Task UnloadDriverAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed || !_isInitialized)
                return;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        if (_isInitialized)
                        {
                            UnloadNTDriver(DriverName);
                            _isInitialized = false;
                        }

                        if (_dllHandle != IntPtr.Zero)
                        {
                            FreeLibrary(_dllHandle);
                            _dllHandle = IntPtr.Zero;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("卸载驱动或DLL失败", ex);
                        throw;
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("驱动卸载操作被取消");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"驱动卸载失败: {ex.Message}", ex);
                throw;
            }
        }

        private bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        #endregion

        #region IDisposable实现
        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                if (_isInitialized)
                {
                    // 同步卸载驱动，因为这是Dispose方法
                    UnloadNTDriver(DriverName);
                    _isInitialized = false;
                }

                if (_dllHandle != IntPtr.Zero)
                {
                    FreeLibrary(_dllHandle);
                    _dllHandle = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("卸载驱动或DLL失败", ex);
            }
            finally
            {
                _isDisposed = true;
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
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
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
            if (_isDisposed || !_isInitialized)
                return false;

            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
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
            if (_isDisposed || !_isInitialized)
                return false;

            try
            {
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
            XButton2,
            WheelUp,
            WheelDown
        }

        /// <summary>
        /// 相对移动鼠标
        /// </summary>
        public bool MoveMouse(int dx, int dy)
        {
            if (_isDisposed || !_isInitialized)
                return false;

            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
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
            if (_isDisposed || !_isInitialized)
                return false;

            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
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
            if (_isDisposed || !_isInitialized)
                return false;

            try
            {
                var status = GetDriverStatus();
                if (status != DeviceStatus.Ready)
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
                    case MouseButtonType.WheelUp:
                        if (isDown) MouseWheelUp(120);
                        break;
                    case MouseButtonType.WheelDown:
                        if (isDown) MouseWheelDown(120);
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
            if (_isDisposed || !_isInitialized)
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
            if (_isDisposed || !_isInitialized)
                return false;

            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
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
