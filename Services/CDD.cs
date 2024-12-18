using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace WpfApp.Services
{
    public enum KeyModifiers       
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    class CDD
    {
        private readonly LogManager _logger = LogManager.Instance;

        [DllImport("Kernel32")]
        private static extern IntPtr LoadLibrary(string dllfile);

        [DllImport("Kernel32")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);

        // 委托定义
        public delegate int pDD_btn(int btn); // 鼠标按键
        public delegate int pDD_whl(int whl); // 鼠标滚轮
        public delegate int pDD_key(int ddcode, int flag); // 键盘输入
        public delegate int pDD_mov(int x, int y); // 鼠标绝对移动
        public delegate int pDD_movR(int dx, int dy); // 鼠标相对移动
        public delegate int pDD_str(string str); // 输入字符串
        public delegate int pDD_todc(int vkcode);   // 转换Windows虚拟键码到dd驱动键码

        // 使用可空类型声明函数指针
        public pDD_btn? btn { get; private set; }
        public pDD_whl? whl { get; private set; }
        public pDD_mov? mov { get; private set; }
        public pDD_movR? movR { get; private set; }
        public pDD_key? key { get; private set; }
        public pDD_str? str { get; private set; }
        public pDD_todc? todc { get; private set; }

        private IntPtr m_hinst;

        ~CDD()
        {
            if (!m_hinst.Equals(IntPtr.Zero))
            {
                FreeLibrary(m_hinst);
            }
        }

        public int Load(string dllfile)
        {
            try
            {
                m_hinst = LoadLibrary(dllfile);
                if (m_hinst.Equals(IntPtr.Zero))
                {
                    _logger.LogError("CDD", "LoadLibrary failed");
                    return -2;
                }

                return GetDDfunAddress(m_hinst);
            }
            catch (Exception ex)
            {
                _logger.LogError("CDD", "Load exception", ex);
                return -2;
            }
        }

        private int GetDDfunAddress(IntPtr hinst)
        {
            try
            {
                // 获取函数地址
                IntPtr ptr = GetProcAddress(hinst, "DD_btn");
                if (ptr.Equals(IntPtr.Zero))
                {
                    _logger.LogError("CDD", "Failed to get DD_btn address");
                    return -1;
                }
                btn = Marshal.GetDelegateForFunctionPointer<pDD_btn>(ptr);

                ptr = GetProcAddress(hinst, "DD_whl");
                if (ptr.Equals(IntPtr.Zero))
                {
                    _logger.LogError("CDD", "Failed to get DD_whl address");
                    return -1;
                }
                whl = Marshal.GetDelegateForFunctionPointer<pDD_whl>(ptr);

                ptr = GetProcAddress(hinst, "DD_mov");
                if (ptr.Equals(IntPtr.Zero))
                {
                    _logger.LogError("CDD", "Failed to get DD_mov address");
                    return -1;
                }
                mov = Marshal.GetDelegateForFunctionPointer<pDD_mov>(ptr);

                ptr = GetProcAddress(hinst, "DD_key");
                if (ptr.Equals(IntPtr.Zero))
                {
                    _logger.LogError("CDD", "Failed to get DD_key address");
                    return -1;
                }
                key = Marshal.GetDelegateForFunctionPointer<pDD_key>(ptr);

                ptr = GetProcAddress(hinst, "DD_movR");
                if (ptr.Equals(IntPtr.Zero))
                {
                    _logger.LogError("CDD", "Failed to get DD_movR address");
                    return -1;
                }
                movR = Marshal.GetDelegateForFunctionPointer<pDD_movR>(ptr);

                ptr = GetProcAddress(hinst, "DD_str");
                if (ptr.Equals(IntPtr.Zero))
                {
                    _logger.LogError("CDD", "Failed to get DD_str address");
                    return -1;
                }
                str = Marshal.GetDelegateForFunctionPointer<pDD_str>(ptr);

                // todc函数是可选的，不影响主要功能
                ptr = GetProcAddress(hinst, "DD_todc");
                if (!ptr.Equals(IntPtr.Zero))
                {
                    todc = Marshal.GetDelegateForFunctionPointer<pDD_todc>(ptr);
                }

                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError("CDD", "GetDDfunAddress exception", ex);
                return -1;
            }
        }
    }
}