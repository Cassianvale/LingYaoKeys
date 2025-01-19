using System;
using System.IO;
using System.Threading.Tasks;

namespace LyKeys;

public class DriverService : IDisposable
{
    private readonly DriverManager _driverManager;
    private readonly string _driverName;
    private string _driverPath = string.Empty;
    private string _dllPath = string.Empty;
    private bool _isDriverInstalled;
    private bool _disposed;

    public event EventHandler<string>? StatusChanged;

    public DriverService()
    {
        _driverManager = new DriverManager();
        _driverName = "lykeys";
        _isDriverInstalled = false;
    }

    public bool IsDriverInstalled => _isDriverInstalled;
    
    public string DriverPath => _driverPath;
    public string DllPath => _dllPath;

    public void SetDriverPaths(string sysPath, string dllPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(sysPath);
        ArgumentException.ThrowIfNullOrEmpty(dllPath);
        
        _driverPath = sysPath;
        _dllPath = dllPath;
        
        ValidateDriverFiles();
    }

    private void ValidateDriverFiles()
    {
        if (string.IsNullOrEmpty(_driverPath))
        {
            OnStatusChanged("警告：未设置驱动文件路径");
            return;
        }

        if (string.IsNullOrEmpty(_dllPath))
        {
            OnStatusChanged("警告：未设置DLL文件路径");
            return;
        }

        if (!File.Exists(_driverPath))
        {
            OnStatusChanged($"警告：驱动文件不存在: {_driverPath}");
        }
        
        if (!File.Exists(_dllPath))
        {
            OnStatusChanged($"警告：DLL文件不存在: {_dllPath}");
        }
    }

    public async Task InitializeDriverAsync()
    {
        ArgumentException.ThrowIfNullOrEmpty(_driverPath, nameof(_driverPath));

        try
        {
            OnStatusChanged($"正在查找驱动文件: {_driverPath}");
            
            if (!File.Exists(_driverPath))
            {
                OnStatusChanged($"驱动文件不存在！请确保驱动文件位于: {_driverPath}");
                throw new FileNotFoundException("找不到驱动文件", _driverPath);
            }

            OnStatusChanged("正在安装驱动...");
            _isDriverInstalled = await _driverManager.InstallAndStartDriverAsync(_driverName, _driverPath);
            
            if (_isDriverInstalled)
            {
                OnStatusChanged("驱动安装成功！");
            }
        }
        catch (Exception ex)
        {
            OnStatusChanged($"驱动安装失败: {ex.Message}");
            throw;
        }
    }

    public async Task UninstallDriverAsync()
    {
        try
        {
            if (!_isDriverInstalled)
            {
                OnStatusChanged("驱动未安装");
                return;
            }

            OnStatusChanged("正在卸载驱动...");
            bool result = await _driverManager.StopAndUninstallDriverAsync(_driverName);
            
            if (result)
            {
                _isDriverInstalled = false;
                OnStatusChanged("驱动卸载成功！");
            }
        }
        catch (Exception ex)
        {
            OnStatusChanged($"驱动卸载失败: {ex.Message}");
            throw;
        }
    }

    private void OnStatusChanged(string message)
    {
        StatusChanged?.Invoke(this, message);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _driverManager?.Dispose();
            }
            _disposed = true;
        }
    }

    ~DriverService()
    {
        Dispose(false);
    }
} 