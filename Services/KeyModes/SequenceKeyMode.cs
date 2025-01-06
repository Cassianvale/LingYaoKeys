using System;
using System.Threading;
using System.Threading.Tasks;

// 按键序列模式
namespace WpfApp.Services.KeyModes
{
    public class SequenceKeyMode : KeyModeBase
    {
        private readonly System.Diagnostics.Stopwatch _performanceTimer;
        private long _lastKeyPressTime;

        public SequenceKeyMode(DDDriverService driverService) : base(driverService)
        {
            _performanceTimer = new System.Diagnostics.Stopwatch();
        }

        public override async Task StartAsync()
        {
            if (_keyList.Count == 0) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            
            try
            {
                // 在开始序列前切换输入法
                (_driverService as DDDriverService)?._inputMethodService.SwitchToEnglish();

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
                            _logger.Error($"按键执行失败: {key}");
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
                _logger.Error("按键序列执行异常", ex);
            }
            finally
            {
                LogModeEnd();
            }
        }

        public override async Task StopAsync()
        {
            try
            {
                await base.StopAsync();
            }
            finally
            {
                // 恢复输入法状态
                (_driverService as DDDriverService)?._inputMethodService.RestorePreviousLayout();
            }
        }
    }
} 