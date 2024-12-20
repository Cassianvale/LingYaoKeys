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
            if (_keyList.Count == 0) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            try
            {
                LogModeStart();
                PrepareStart();

                while (_isRunning && !_cts.Token.IsCancellationRequested)
                {
                    foreach (var key in _keyList)
                    {
                        if (!_isRunning || _cts.Token.IsCancellationRequested) break;

                        // 使用 SimulateKeyPress 替代直接的 SendKey 调用
                        // 这样可以确保按键有足够的按下时间
                        if (!_driverService.SimulateKeyPress(key, null, KeyPressInterval))
                        {
                            _logger.LogError("SequenceKeyMode", $"按键执行失败: {key}");
                            continue;
                        }

                        Metrics.IncrementKeyCount();
                        
                        // 使用配置的间隔时间
                        await Task.Delay(GetInterval(), _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常的取消操作，不需要特殊处理
            }
            catch (Exception ex)
            {
                _logger.LogError("SequenceKeyMode", "按键序列执行异常", ex);
            }
            finally
            {
                LogModeEnd();
            }
        }
    }
} 