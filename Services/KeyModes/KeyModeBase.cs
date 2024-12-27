using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// 按键模式基类
namespace WpfApp.Services.KeyModes
{
    public abstract class KeyModeBase : IDisposable
    {
        protected readonly DDDriverService _driverService;
        protected readonly SerilogManager _logger;
        protected List<DDKeyCode> _keyList;
        protected bool _isRunning;
        protected CancellationTokenSource? _cts;
        public readonly KeyModeMetrics Metrics;
        protected int KeyPressInterval => _driverService.KeyPressInterval;

        protected KeyModeBase(DDDriverService driverService)
        {
            _driverService = driverService;
            _logger = SerilogManager.Instance;
            _keyList = new List<DDKeyCode>();
            Metrics = new KeyModeMetrics();
        }

        public virtual void SetKeyList(List<DDKeyCode> keyList)
        {
            if (keyList == null)
            {
                _keyList = new List<DDKeyCode>();
                return;
            }
            _keyList = new List<DDKeyCode>(keyList);
            // _logger.Debug($"按键列表已更新: {string.Join(", ", _keyList)}");
        }

        public virtual void SetKeyInterval(int interval)
        {
            _driverService.SetKeyInterval(interval);
        }

        public abstract Task StartAsync();
        
        public virtual async Task StopAsync()
        {
            _isRunning = false;
            _cts?.Cancel();
            await Task.Delay(50); // 给予足够的时间让任务停止

            Metrics.StopTracking();

            // 释放所有按键
            if (_keyList?.Count > 0)
            {
                foreach (var key in _keyList)
                {
                    _driverService.SendKey(key, false);
                }
            }
        }

        // public virtual void LogMetrics()
        // {
        //     Metrics.SequenceEnd(_driverService.KeyInterval);
        // }

        protected virtual void LogModeStart()
        {
            _logger.SequenceEvent("开始", $"模式: {GetType().Name} | 按键列表: {string.Join(", ", _keyList)} | 间隔: {_driverService.KeyInterval}ms");
        }

        protected virtual void LogModeEnd()
        {
            _logger.SequenceEvent("结束", $"模式: {GetType().Name} 已停止");
        }

        protected virtual void PrepareStart()
        {
            Metrics.StartTracking();
        }

        protected int GetInterval() => _driverService.KeyInterval;

        // 设置按键 [按下->松开] 的时间间隔
        public virtual void SetKeyPressInterval(int interval)
        {
            _logger.Debug($"按键按下时长更新: {interval}ms");
        }

        protected virtual async Task PressKeyAsync(DDKeyCode key)
        {
            await Task.Run(() =>
            {
                _driverService.SendKey(key, true);
                Thread.Sleep(_driverService.KeyPressInterval);
                _driverService.SendKey(key, false);
            });
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