using System.Runtime.InteropServices;
using System.IO;
using System.Security.Principal;
using System.ComponentModel;
using System.Security;
using WpfApp.Services.Utils;

namespace WpfApp.Services.Core;

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
    private DeviceStatus _lastStatus = DeviceStatus.Unknown;

    #endregion

    #region 驱动状态枚举

    public enum DeviceStatus
    {
        Unknown = 0,     // 未知状态
        Ready = 1,       // 准备就绪
        Error = 2,       // 错误状态
        NoKeyboard = 3,  // 无法找到键盘设备
        NoMouse = 4,     // 无法找到鼠标设备
        InitFailed = 5   // 初始化失败
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

    // 新增：获取详细错误信息
    [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetDetailedErrorCode();

    [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong GetLastCheckTime();

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

    [DllImport("lykeysdll.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetDriverHandle")]
    private static extern IntPtr GetDriverHandle();

    #endregion

    #region 构造函数和初始化

    public LyKeys(string driverPath)
    {
        _driverPath = driverPath ?? throw new ArgumentNullException(nameof(driverPath));
        _logger.Debug($"LyKeys实例化，驱动路径: {driverPath}");
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

            var driverDirectory =
                Path.GetDirectoryName(_driverPath) ?? throw new InvalidOperationException("无法获取驱动文件目录");
            var dllPath = Path.Combine(driverDirectory, "lykeysdll.dll");

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
                        var error = Marshal.GetLastWin32Error();
                        var errorMessage = new Win32Exception(error).Message;
                        _logger.Error($"DLL加载失败 - 错误代码: {error}, 错误信息: {errorMessage}");
                        throw new Win32Exception(error, $"DLL加载失败: {errorMessage}");
                    }

                    _logger.Debug("DLL加载成功");

                    cancellationToken.ThrowIfCancellationRequested();

                    // 加载驱动
                    _logger.Debug($"开始加载驱动: {DriverName}, 路径: {_driverPath}");
                    var loadResult = LoadNTDriver(DriverName, _driverPath);
                    if (!loadResult)
                    {
                        var error = Marshal.GetLastWin32Error();
                        var errorMessage = new Win32Exception(error).Message;
                        _logger.Error($"驱动加载失败 - 错误代码: {error}, 错误信息: {errorMessage}");
                        return false;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    // 初始化设备句柄
                    _logger.Debug("开始初始化设备句柄");
                    var handleResult = SetHandle();
                    _logger.Debug($"设备句柄初始化结果: {handleResult}");
                    if (!handleResult)
                    {
                        var error = Marshal.GetLastWin32Error();
                        var errorMessage = new Win32Exception(error).Message;
                        _logger.Error($"初始化设备句柄失败 - 错误代码: {error}, 错误信息: {errorMessage}");
                        throw new InvalidOperationException($"初始化设备句柄失败: {errorMessage}");
                    }

                    _logger.Debug("设备句柄初始化成功");

                    cancellationToken.ThrowIfCancellationRequested();

                    // 检查设备状态
                    _logger.Debug("开始检查设备状态");
                    CheckDeviceStatus();
                    var status = GetDriverStatus();
                    _lastStatus = status;
                    _logger.Debug($"设备状态: {status}");

                    if (status != DeviceStatus.Ready)
                    {
                        // 尝试重新初始化句柄
                        _logger.Debug("设备状态不正确，尝试重新初始化句柄");
                        if (!SetHandle()) throw new InvalidOperationException("重新初始化设备句柄失败");

                        await Task.Delay(500, cancellationToken);
                        CheckDeviceStatus();
                        status = GetDriverStatus();
                        _lastStatus = status;
                        _logger.Debug($"重新初始化后的设备状态: {status}");

                        // 根据设备状态提供不同的错误信息
                        if (status != DeviceStatus.Ready)
                        {
                            string errorMessage;
                            switch (status)
                            {
                                case DeviceStatus.NoKeyboard:
                                    errorMessage = "找不到键盘设备，请检查您的键盘连接。";
                                    break;
                                case DeviceStatus.NoMouse:
                                    errorMessage = "找不到鼠标设备，请检查您的鼠标连接。";
                                    break;
                                case DeviceStatus.InitFailed:
                                    errorMessage = "驱动初始化失败，请尝试重新启动计算机或重新安装驱动。";
                                    break;
                                default:
                                    errorMessage = $"设备状态异常: {status}";
                                    break;
                            }
                            _logger.Error(errorMessage);
                            throw new InvalidOperationException(errorMessage);
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
        using (var identity = WindowsIdentity.GetCurrent())
        {
            var principal = new WindowsPrincipal(identity);
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
        // 使用内联条件加速检查
        if (_isDisposed || !_isInitialized) return false;

        try
        {
            KeyDown(vkCode);
            return true;
        }
        catch (Exception)
        {
            // 捕获异常时才进行完整状态检查
            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
                {
                    try
                    {
                        KeyDown(vkCode);
                        return true;
                    }
                    catch { /* 已重试，忽略第二次异常 */ }
                }
            }
            catch { /* 忽略异常直接返回失败 */ }
            
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
        // 使用内联条件加速检查
        if (_isDisposed || !_isInitialized) return false;

        try
        {
            KeyUp(vkCode);
            return true;
        }
        catch (Exception)
        {
            // 捕获异常时才进行完整状态检查
            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
                {
                    try
                    {
                        KeyUp(vkCode);
                        return true;
                    }
                    catch { /* 已重试，忽略第二次异常 */ }
                }
            }
            catch { /* 忽略异常直接返回失败 */ }
            
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
    /// 鼠标相对位移
    /// 表示鼠标从当前位置移动指定的偏移量
    /// </summary>
    public bool MoveMouse(int dx, int dy)
    {
        if (_isDisposed || !_isInitialized)
            return false;

        try
        {
            MouseMoveRELATIVE(dx, dy);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"相对移动鼠标失败, dx: {dx}, dy: {dy}", ex);
            
            // 捕获异常时才进行完整状态检查
            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
                {
                    try
                    {
                        MouseMoveRELATIVE(dx, dy);
                        return true;
                    }
                    catch { /* 已重试，忽略第二次异常 */ }
                }
            }
            catch { /* 忽略异常直接返回失败 */ }
            
            return false;
        }
    }

    /// <summary>
    /// 鼠标绝对位移
    /// 表示将鼠标直接定位到屏幕上的指定坐标点
    /// </summary>
    public bool MoveMouseAbsolute(int x, int y)
    {
        if (_isDisposed || !_isInitialized)
            return false;

        try
        {
            MouseMoveABSOLUTE(x, y);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"绝对移动鼠标失败, x: {x}, y: {y}", ex);
            
            // 捕获异常时才进行完整状态检查
            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
                {
                    try
                    {
                        MouseMoveABSOLUTE(x, y);
                        return true;
                    }
                    catch { /* 已重试，忽略第二次异常 */ }
                }
            }
            catch { /* 忽略异常直接返回失败 */ }
            
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
            // 直接尝试执行鼠标操作，不先检查状态
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
            
            // 捕获异常时才进行完整状态检查
            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
                {
                    try
                    {
                        // 重试鼠标操作
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
                    catch { /* 已重试，忽略第二次异常 */ }
                }
            }
            catch { /* 忽略异常直接返回失败 */ }
            
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
            if (isUp) 
                MouseWheelUp(delta);
            else 
                MouseWheelDown(delta);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"鼠标滚轮操作失败, isUp: {isUp}, delta: {delta}", ex);
            
            // 捕获异常时才进行完整状态检查
            try
            {
                var status = GetDriverStatus();
                if (status == DeviceStatus.Ready)
                {
                    try
                    {
                        if (isUp) 
                            MouseWheelUp(delta);
                        else 
                            MouseWheelDown(delta);
                        return true;
                    }
                    catch { /* 已重试，忽略第二次异常 */ }
                }
            }
            catch { /* 忽略异常直接返回失败 */ }
            
            return false;
        }
    }

    #endregion

    #region 公共方法
    
    /// <summary>
    /// 获取最后一次设备状态检查的结果
    /// </summary>
    /// <returns>设备状态</returns>
    public DeviceStatus GetLastStatus()
    {
        return _lastStatus;
    }
    
    #endregion
}