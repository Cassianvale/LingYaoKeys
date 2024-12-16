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
        [DllImport("Kernel32")]
        private static extern System.IntPtr LoadLibrary(string dllfile);

        [DllImport("Kernel32")]
        private static extern System.IntPtr GetProcAddress(System.IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);


        // 模拟鼠标点击
        // 1 =左键按下 ，2 =左键放开
        // 4 =右键按下 ，8 =右键放开
        // 16 =中键按下 ，32 =中键放开
        // 64 =4键按下 ，128 =4键放开
        // 256 =5键按下 ，512 =5键放开
        // 例子：模拟鼠标右键 只需要连写(中间可添加延迟)
        // dd_btn(4); dd_btn(8);
        public delegate int pDD_btn(int btn);

        // DD_whl(int whl)模拟鼠标滚轮
        // 1=前,2=后
        // 向前滚一格,DD_whl(1)
        public delegate int pDD_whl(int whl);
        public delegate int pDD_key(int ddcode, int flag);

        // 鼠标绝对移动，x、y为左上角为原点
        // 把鼠标移动到分辨率1920*1080 的屏幕正中间，
        // int x = 1920/2 ; int y = 1080/2;
        // DD_mov(x,y) ;
        public delegate int pDD_mov(int x, int y);

        // 模拟鼠标相对移动, dx、dy以当前坐标为原点
        // 把鼠标向左移动10像素, DD_movR(-10,0)
        public delegate int pDD_movR(int dx, int dy);

        // 直接输入键盘上可见字符和空格
        // DD_str(char *str) 参数：单字节字符串
        public delegate int pDD_str(string str);
        public delegate int pDD_todc(int vkcode);

        public pDD_btn btn;         //Mouse button 
        public pDD_whl whl;         //Mouse wheel
        public pDD_mov mov;      //Mouse move abs. 
        public pDD_movR movR;  //Mouse move rel. 

        // 模拟键盘按键
        // 参数：参数1 ，请查看[DD虚拟键盘码表]。
        // DD_key(int ddcode，int flag)
        // 参数2，1=按下，2=放开
        // 单键WIN，
        // DD_key(601, 1);
        // DD_key(601, 2);
        // 组合键：ctrl+alt+del
        // DD_key(600,1);
        // DD_key(602,1);
        // DD_key(706,1);
        // DD_key(706,2);
        // DD_key(602,2);
        // DD_key(600,2);
        public pDD_key key;

        public pDD_str str;

        public pDD_todc todc;      //  转换Windows虚拟键码到 DD 专用键码

        private System.IntPtr m_hinst;

         ~CDD()
        {
             if (!m_hinst.Equals(IntPtr.Zero))
             {
                 bool b = FreeLibrary(m_hinst);
             }
        }


        public int Load(string dllfile)
        {
            m_hinst = LoadLibrary(dllfile);
            if (m_hinst.Equals(IntPtr.Zero))
            {
                return -2;
            }
            else
            {
                return GetDDfunAddress(m_hinst);
            }
        }

        private int GetDDfunAddress(IntPtr hinst)
        {
            IntPtr ptr;

            ptr = GetProcAddress(hinst, "DD_btn");
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            btn = Marshal.GetDelegateForFunctionPointer(ptr, typeof(pDD_btn)) as pDD_btn;

            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            ptr = GetProcAddress(hinst, "DD_whl");
            whl = Marshal.GetDelegateForFunctionPointer(ptr, typeof(pDD_whl)) as pDD_whl;
            
            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            ptr = GetProcAddress(hinst, "DD_mov");
            mov = Marshal.GetDelegateForFunctionPointer(ptr, typeof(pDD_mov)) as pDD_mov;

            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            ptr = GetProcAddress(hinst, "DD_key");
            key = Marshal.GetDelegateForFunctionPointer(ptr, typeof(pDD_key)) as pDD_key;

            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            ptr = GetProcAddress(hinst, "DD_movR");
            movR = Marshal.GetDelegateForFunctionPointer(ptr, typeof(pDD_movR)) as pDD_movR;

            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            ptr = GetProcAddress(hinst, "DD_str");
            str = Marshal.GetDelegateForFunctionPointer(ptr, typeof(pDD_str)) as pDD_str;

            if (ptr.Equals(IntPtr.Zero)) { return -1; }
            ptr = GetProcAddress(hinst, "DD_todc");
            //todc = Marshal.GetDelegateForFunctionPointer(ptr, typeof(pDD_todc)) as pDD_todc;

            return 1 ;
        }
    }

}