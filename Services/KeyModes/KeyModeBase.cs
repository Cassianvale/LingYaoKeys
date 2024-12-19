using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp.Services.KeyModes
{
    public abstract class KeyModeBase
    {
        protected readonly DDDriverService _driverService;
        protected readonly LogManager _logger;
        protected List<DDKeyCode> _keyList;
        protected int _keyInterval;
        protected bool _isRunning;
        protected CancellationTokenSource? _cts;
        public readonly KeyModeMetrics Metrics;

        protected KeyModeBase(DDDriverService driverService)
        {
            _driverService = driverService;
            _logger = LogManager.Instance;
            _keyList = new List<DDKeyCode>();
            _keyInterval = 50;
            Metrics = new KeyModeMetrics();
        }

        public virtual void SetKeyList(List<DDKeyCode> keyList)
        {
            _keyList = keyList ?? new List<DDKeyCode>();
        }

        public virtual void SetKeyInterval(int interval)
        {
            _keyInterval = Math.Max(5, interval);
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

        public virtual void LogMetrics()
        {
            Metrics.LogSequenceEnd(_keyInterval);
        }

        protected virtual void LogModeStart()
        {
            _logger.LogSequenceEvent("开始", $"模式: {GetType().Name} | 按键列表: {string.Join(", ", _keyList)} | 间隔: {_keyInterval}ms");
        }

        protected virtual void LogModeEnd()
        {
            _logger.LogSequenceEvent("结束", $"模式: {GetType().Name} 已停止");
        }

        protected virtual void PrepareStart()
        {
            Metrics.StartTracking();
        }
    }
} 