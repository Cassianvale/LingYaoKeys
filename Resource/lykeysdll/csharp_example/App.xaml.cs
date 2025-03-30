using System.Windows;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace LyKeys;

public partial class App : System.Windows.Application
{
    private DriverService? _driverService;
    private IConfiguration? _configuration;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 加载配置文件
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _driverService = new DriverService();
        
        // 从配置中获取驱动路径并设置
        var sysPath = _configuration["Driver:DefaultPaths:Sys"];
        var dllPath = _configuration["Driver:DefaultPaths:Dll"];
        
        if (!string.IsNullOrEmpty(sysPath) && !string.IsNullOrEmpty(dllPath))
        {
            _driverService.SetDriverPaths(sysPath, dllPath);
        }
        
        MainWindow = new MainWindow(_driverService);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _driverService?.Dispose();
        base.OnExit(e);
    }
}
