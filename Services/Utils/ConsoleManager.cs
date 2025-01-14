using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

namespace WpfApp.Services.Utils
{
    public static class ConsoleManager
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleCP(uint wCodePageID);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private static StreamWriter? _standardOutput;
        private static StreamWriter? _standardError;
        private static StreamReader? _standardInput;
        private static readonly object _lock = new object();
        private static bool _isInitialized;

        public static void Show()
        {
            lock (_lock)
            {
                var handle = GetConsoleWindow();
                if (handle == IntPtr.Zero)
                {
                    try
                    {
                        AllocConsole();
                        Console.Title = "LingYaoKeys Debug Console";

                        // 设置控制台编码为UTF-8
                        SetConsoleOutputCP(65001);  // UTF-8的代码页
                        SetConsoleCP(65001);        // 输入的代码页也设置为UTF-8
                        Console.OutputEncoding = Encoding.UTF8;
                        Console.InputEncoding = Encoding.UTF8;
                        
                        // 重定向标准输出
                        _standardOutput = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8)
                        { 
                            AutoFlush = true 
                        };
                        Console.SetOut(_standardOutput);
                        
                        // 重定向标准错误
                        _standardError = new StreamWriter(Console.OpenStandardError(), Encoding.UTF8)
                        {
                            AutoFlush = true
                        };
                        Console.SetError(_standardError);
                        
                        // 重定向标准输入
                        _standardInput = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
                        Console.SetIn(_standardInput);

                        _isInitialized = true;


                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"控制台初始化失败: {ex.Message}");
                        Release();  // 发生异常时清理资源
                    }
                }
                else
                {
                    ShowWindow(handle, SW_SHOW);
                }
            }
        }

        public static void Hide()
        {
            var handle = GetConsoleWindow();
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, SW_HIDE);
            }
        }

        public static void Release()
        {
            lock (_lock)
            {
                if (!_isInitialized) return;

                try
                {
                    // 恢复默认的控制台输出
                    var defaultOutput = new StreamWriter(Console.OpenStandardOutput())
                    {
                        AutoFlush = true
                    };
                    Console.SetOut(defaultOutput);

                    var defaultError = new StreamWriter(Console.OpenStandardError())
                    {
                        AutoFlush = true
                    };
                    Console.SetError(defaultError);

                    var defaultInput = new StreamReader(Console.OpenStandardInput());
                    Console.SetIn(defaultInput);

                    // 关闭重定向的流
                    if (_standardOutput != null)
                    {
                        _standardOutput.Flush();
                        _standardOutput.Dispose();
                        _standardOutput = null;
                    }

                    if (_standardError != null)
                    {
                        _standardError.Flush();
                        _standardError.Dispose();
                        _standardError = null;
                    }

                    if (_standardInput != null)
                    {
                        _standardInput.Dispose();
                        _standardInput = null;
                    }

                    // 释放控制台
                    FreeConsole();
                    _isInitialized = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"控制台释放失败: {ex.Message}");
                }
            }
        }
    }
} 