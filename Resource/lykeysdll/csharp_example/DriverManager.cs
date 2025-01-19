using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ComponentModel;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Threading.Tasks;

namespace LyKeys
{
    public class DriverManager : IDisposable
    {
        // Win32 API 常量
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        // SCM 访问权限
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint SERVICE_KERNEL_DRIVER = 0x00000001;
        private const uint SERVICE_DEMAND_START = 0x00000003;
        private const uint SERVICE_ERROR_NORMAL = 0x00000001;

        // 服务控制码
        private const uint SERVICE_CONTROL_STOP = 0x00000001;
        private const uint SERVICE_CONTROL_PAUSE = 0x00000002;
        private const uint SERVICE_CONTROL_CONTINUE = 0x00000003;

        // 服务状态
        private const uint SERVICE_STOPPED = 0x00000001;
        private const uint SERVICE_START_PENDING = 0x00000002;
        private const uint SERVICE_STOP_PENDING = 0x00000003;
        private const uint SERVICE_RUNNING = 0x00000004;
        private const uint SERVICE_CONTINUE_PENDING = 0x00000005;
        private const uint SERVICE_PAUSE_PENDING = 0x00000006;
        private const uint SERVICE_PAUSED = 0x00000007;

        // 设备路径
        private const string DEVICE_PATH = "\\\\.\\lykeys";
        private SafeFileHandle deviceHandle;
        private bool disposed = false;

        #region Win32 API 导入

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            uint dwAccess);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateService(
            IntPtr hSCManager,
            string lpServiceName,
            string lpDisplayName,
            uint dwDesiredAccess,
            uint dwServiceType,
            uint dwStartType,
            uint dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            IntPtr lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr OpenService(
            IntPtr hSCManager,
            string lpServiceName,
            uint dwDesiredAccess);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteService(IntPtr hService);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr hSCObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool StartService(
            IntPtr hService,
            uint dwNumServiceArgs,
            string[] lpServiceArgVectors);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ControlService(
            IntPtr hService,
            uint dwControl,
            ref SERVICE_STATUS lpServiceStatus);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_STATUS
        {
            public uint dwServiceType;
            public uint dwCurrentState;
            public uint dwControlsAccepted;
            public uint dwWin32ExitCode;
            public uint dwServiceSpecificExitCode;
            public uint dwCheckPoint;
            public uint dwWaitHint;
        }

        #endregion

        public DriverManager()
        {
            if (!IsAdministrator())
            {
                throw new UnauthorizedAccessException("需要管理员权限才能操作驱动程序。");
            }
        }

        /// <summary>
        /// 检查当前用户是否具有管理员权限
        /// </summary>
        private bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// 安装并启动驱动程序
        /// </summary>
        public async Task<bool> InstallAndStartDriverAsync(string driverName, string driverPath)
        {
            if (string.IsNullOrEmpty(driverName) || string.IsNullOrEmpty(driverPath))
                throw new ArgumentException("驱动名称和路径不能为空");

            if (!File.Exists(driverPath))
                throw new FileNotFoundException("找不到驱动文件", driverPath);

            IntPtr scmHandle = IntPtr.Zero;
            IntPtr serviceHandle = IntPtr.Zero;

            try
            {
                // 打开服务控制管理器
                scmHandle = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
                if (scmHandle == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开服务控制管理器");

                // 尝试打开现有服务
                serviceHandle = OpenService(scmHandle, driverName, SERVICE_ALL_ACCESS);
                
                if (serviceHandle == IntPtr.Zero)
                {
                    // 创建新服务
                    serviceHandle = CreateService(
                        scmHandle,
                        driverName,
                        driverName,
                        SERVICE_ALL_ACCESS,
                        SERVICE_KERNEL_DRIVER,
                        SERVICE_DEMAND_START,
                        SERVICE_ERROR_NORMAL,
                        driverPath,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null);

                    if (serviceHandle == IntPtr.Zero)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "创建驱动服务失败");
                }

                // 启动服务
                if (!StartService(serviceHandle, 0, null))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 1056) // ERROR_SERVICE_ALREADY_RUNNING
                        throw new Win32Exception(error, "启动驱动服务失败");
                }

                // 等待服务完全启动
                await Task.Delay(1000);

                // 尝试打开设备
                deviceHandle = CreateFile(
                    DEVICE_PATH,
                    GENERIC_READ | GENERIC_WRITE,
                    0,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OVERLAPPED,
                    IntPtr.Zero);

                if (deviceHandle.IsInvalid)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开设备句柄");

                return true;
            }
            finally
            {
                if (serviceHandle != IntPtr.Zero)
                    CloseServiceHandle(serviceHandle);
                if (scmHandle != IntPtr.Zero)
                    CloseServiceHandle(scmHandle);
            }
        }

        /// <summary>
        /// 停止并卸载驱动程序
        /// </summary>
        public async Task<bool> StopAndUninstallDriverAsync(string driverName)
        {
            if (string.IsNullOrEmpty(driverName))
                throw new ArgumentException("驱动名称不能为空");

            IntPtr scmHandle = IntPtr.Zero;
            IntPtr serviceHandle = IntPtr.Zero;

            try
            {
                // 关闭设备句柄
                if (deviceHandle != null && !deviceHandle.IsInvalid)
                {
                    deviceHandle.Dispose();
                    deviceHandle = null;
                }

                // 打开服务控制管理器
                scmHandle = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
                if (scmHandle == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "无法打开服务控制管理器");

                // 打开服务
                serviceHandle = OpenService(scmHandle, driverName, SERVICE_ALL_ACCESS);
                if (serviceHandle == IntPtr.Zero)
                    return true; // 服务不存在，视为已卸载

                // 停止服务
                SERVICE_STATUS serviceStatus = new SERVICE_STATUS();
                if (!ControlService(serviceHandle, SERVICE_CONTROL_STOP, ref serviceStatus))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 1062) // ERROR_SERVICE_NOT_ACTIVE
                        throw new Win32Exception(error, "停止驱动服务失败");
                }

                // 等待服务完全停止
                await Task.Delay(1000);

                // 删除服务
                if (!DeleteService(serviceHandle))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "删除驱动服务失败");

                return true;
            }
            finally
            {
                if (serviceHandle != IntPtr.Zero)
                    CloseServiceHandle(serviceHandle);
                if (scmHandle != IntPtr.Zero)
                    CloseServiceHandle(scmHandle);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (deviceHandle != null && !deviceHandle.IsInvalid)
                    {
                        deviceHandle.Dispose();
                        deviceHandle = null;
                    }
                }
                disposed = true;
            }
        }

        ~DriverManager()
        {
            Dispose(false);
        }
    }
} 