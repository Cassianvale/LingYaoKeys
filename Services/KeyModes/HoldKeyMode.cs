using System;
using System.Threading;
using System.Threading.Tasks;

// 按键按压模式
namespace WpfApp.Services.KeyModes
{
    public class HoldKeyMode : KeyModeBase
    {
        public HoldKeyMode(DDDriverService driverService) : base(driverService)
        {
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

                        if (!_driverService.SimulateKeyPress(key, null, KeyPressInterval))
                        {
                            _logger.LogError("HoldKeyMode", $"按键执行失败: {key}");
                            continue;
                        }

                        Metrics.IncrementKeyCount();
                    }

                    await Task.Delay(GetInterval(), _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常的取消操作，不需要特殊处理
            }
            catch (Exception ex)
            {
                _logger.LogError("HoldKeyMode", "按键序列执行异常", ex);
            }
            finally
            {
                LogModeEnd();
            }
        }
    }
}