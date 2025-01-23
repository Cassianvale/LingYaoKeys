using System.Windows;

namespace LyKeys;

public partial class App : System.Windows.Application
{
    private DriverService? _driverService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _driverService = new DriverService();
        MainWindow = new MainWindow(_driverService);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _driverService?.Dispose();
        base.OnExit(e);
    }
}
