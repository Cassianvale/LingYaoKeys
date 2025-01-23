using WpfApp.Services.Core;
using WpfApp.Services.Utils;

// 按键模式基类
namespace WpfApp.Services.Models
{
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
            {
                foreach (var key in _keyList)
                {
                    _driverService.SendKeyUp(key);
                }
            }
        }
        
        protected virtual void LogModeStart()
        {
            _logger.SequenceEvent("开始", $"模式: {GetType().Name} | 按键列表: {string.Join(", ", _keyList)} | 间隔: {_driverService.KeyInterval}ms");
        }

        protected virtual void LogModeEnd()
        {
            _logger.SequenceEvent("结束", $"模式: {GetType().Name} 已停止");
        }


        protected int GetInterval() => _driverService.KeyInterval;

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
            if (disposing)
            {
                _cts?.Dispose();
            }
        }
    }
} 