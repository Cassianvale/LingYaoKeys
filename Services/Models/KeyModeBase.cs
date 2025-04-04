using WpfApp.Services.Core;
using WpfApp.Services.Utils;

// 按键模式基类
namespace WpfApp.Services.Models;

public abstract class KeyModeBase
{
    protected readonly LyKeysService _driverService;
    protected readonly SerilogManager _logger;
    protected List<LyKeysCode> _keyList;
    protected bool _isRunning;
    protected CancellationTokenSource? _cts;
    protected int KeyPressInterval => _driverService.KeyPressInterval;

    protected KeyModeBase(LyKeysService driverService)
    {
        _driverService = driverService;
        _logger = SerilogManager.Instance;
        _keyList = new List<LyKeysCode>();
    }

    public virtual void SetKeyList(List<LyKeysCode> keyList)
    {
        if (keyList == null)
        {
            _keyList = new List<LyKeysCode>();
            return;
        }

        _keyList = new List<LyKeysCode>(keyList);
    }

    public virtual void SetKeyInterval(int interval)
    {
        _driverService.KeyInterval = interval;
    }

    public abstract void Start();

    public virtual void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        Thread.Sleep(50); // 给予足够的时间让任务停止

        // 释放所有按键
        if (_keyList?.Count > 0)
            foreach (var key in _keyList)
                _driverService.SendKeyUp(key);
    }

    protected virtual void LogModeStart()
    {
        _logger.SequenceEvent("开始", $"模式: {GetType().Name} | 按键列表: {string.Join(", ", _keyList)} | 使用独立按键间隔");
    }

    protected virtual void LogModeEnd()
    {
        _logger.SequenceEvent("结束", $"模式: {GetType().Name} 已停止");
    }


    // 修改为接收按键代码并获取独立间隔
    protected int GetInterval(LyKeysCode keyCode)
    {
        // 通过驱动服务获取特定按键的间隔
        return _driverService.GetKeyInterval(keyCode);
    }

    // 保留无参数版本作为后备（返回默认间隔）
    protected int GetInterval()
    {
        return _driverService.KeyInterval;
    }

    // 设置按键 [按下->松开] 的时间间隔
    public virtual void SetKeyPressInterval(int interval)
    {
        _logger.Debug($"按键按下时长更新: {interval}ms");
    }

    protected virtual void PressKey(LyKeysCode key)
    {
        _driverService.SendKeyDown(key);
        Thread.Sleep(_driverService.KeyPressInterval);
        _driverService.SendKeyUp(key);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) _cts?.Dispose();
    }
}