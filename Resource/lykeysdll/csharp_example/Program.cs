using System;
using System.IO;
using System.Threading.Tasks;

namespace LyKeys
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("LyKeys 驱动管理程序");
            Console.WriteLine("==================");

            try
            {
                using (var driverManager = new DriverManager())
                {
                    string driverName = "lykeys";
                    string driverPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "drivers",
                        "lykeys.sys"
                    );

                    Console.WriteLine("正在安装驱动...");
                    bool installResult = await driverManager.InstallAndStartDriverAsync(driverName, driverPath);
                    if (installResult)
                    {
                        Console.WriteLine("驱动安装成功！");
                        
                        Console.WriteLine("\n按任意键卸载驱动...");
                        Console.ReadKey(true);

                        Console.WriteLine("\n正在卸载驱动...");
                        bool uninstallResult = await driverManager.StopAndUninstallDriverAsync(driverName);
                        if (uninstallResult)
                        {
                            Console.WriteLine("驱动卸载成功！");
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"\n错误: {ex.Message}");
                Console.WriteLine("请以管理员身份运行此程序。");
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"\n错误: {ex.Message}");
                Console.WriteLine("请确保驱动文件存在于正确的位置。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n发生错误: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"详细信息: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey(true);
        }
    }
} 