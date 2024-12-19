using System;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp.Services.KeyModes
{
    public class SequenceKeyMode : KeyModeBase
    {
        private int _currentKeyIndex;
        private readonly System.Diagnostics.Stopwatch _performanceTimer;
        private long _lastKeyPressTime;

        public SequenceKeyMode(DDDriverService driverService) : base(driverService)
        {
            _performanceTimer = new System.Diagnostics.Stopwatch();
            _currentKeyIndex = 0;
        }

        public override async Task StartAsync()
        {
            if (!_driverService.IsInitialized) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _performanceTimer.Restart();
            _lastKeyPressTime = 0;
            _currentKeyIndex = 0;
            PrepareStart();

            LogModeStart();

            try
            {
                while (_isRunning && !_cts.Token.IsCancellationRequested)
                {
                    DDKeyCode keyCode;
                    lock (this)
                    {
                        if (_currentKeyIndex >= _keyList.Count)
                        {
                            _currentKeyIndex = 0;
                        }
                        keyCode = _keyList[_currentKeyIndex++];
                    }

                    long cycleStartTime = _performanceTimer.ElapsedMilliseconds;
                    
                    // 执行按键
                    if (ExecuteKeyCycle(keyCode))
                    {
                        // 记录实际间隔
                        long currentTime = _performanceTimer.ElapsedMilliseconds;
                        if (_lastKeyPressTime > 0)
                        {
                            Metrics.RecordKeyInterval(TimeSpan.FromMilliseconds(currentTime - _lastKeyPressTime));
                        }
                        _lastKeyPressTime = currentTime;
                    }

                    // 计算剩余延迟时间
                    long elapsedTime = _performanceTimer.ElapsedMilliseconds - cycleStartTime;
                    int remainingDelay = Math.Max(5, _keyInterval - (int)elapsedTime);

                    if (remainingDelay > 0)
                    {
                        await Task.Delay(remainingDelay, _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不需要处理
            }
            catch (Exception ex)
            {
                _logger.LogError("SequenceKeyMode", "序列执行异常", ex);
            }
            finally
            {
                LogModeEnd();
            }
        }

        private bool ExecuteKeyCycle(DDKeyCode keyCode)
        {
            try
            {
                long pressStartTime = _performanceTimer.ElapsedMilliseconds;

                if (!_driverService.SendKey(keyCode, true)) return false;
                if (!_driverService.SendKey(keyCode, false)) return false;

                long pressDuration = _performanceTimer.ElapsedMilliseconds - pressStartTime;
                Metrics.RecordKeyPressDuration(TimeSpan.FromMilliseconds(pressDuration));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("ExecuteKeyCycle", "按键执行异常", ex);
                return false;
            }
        }
    }
} 