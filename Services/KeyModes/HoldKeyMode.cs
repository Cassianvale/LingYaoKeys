using System;
using System.Threading;
using System.Threading.Tasks;

// 按键按压模式
namespace WpfApp.Services.KeyModes
{
    public class HoldKeyMode : KeyModeBase
    {
        private volatile bool _isKeyHeld;
        private readonly SemaphoreSlim _executionLock = new SemaphoreSlim(1, 1);

        public HoldKeyMode(DDDriverService driverService) : base(driverService)
        {
        }

        public override async Task StartAsync()
        {
            if (_keyList.Count == 0) return;

            // 确保同时只有一个执行实例
            if (!await _executionLock.WaitAsync(0))
            {
                _logger.LogWarning("HoldKeyMode", "已有按键序列在执行中");
                return;
            }

            try
            {
                _isRunning = true;
                _isKeyHeld = true;
                _cts = new CancellationTokenSource();

                LogModeStart();
                PrepareStart();

                while (_isRunning && _isKeyHeld && !_cts.Token.IsCancellationRequested)
                {
                    foreach (var key in _keyList)
                    {
                        if (!_isRunning || !_isKeyHeld || _cts.Token.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            if (!_driverService.SimulateKeyPress(key, null, KeyPressInterval))
                            {
                                _logger.LogError("HoldKeyMode", $"按键执行失败: {key}");
                                continue;
                            }

                            Metrics.IncrementKeyCount();
                            
                            // 检查是否需要继续执行
                            if (!_isKeyHeld)
                            {
                                _logger.LogDebug("HoldKeyMode", "检测到按键释放，停止序列");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("HoldKeyMode", $"执行按键 {key} 时发生异常", ex);
                        }
                    }

                    if (_isRunning && _isKeyHeld && !_cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(GetInterval(), _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("HoldKeyMode", "按键序列被取消");
            }
            catch (Exception ex)
            {
                _logger.LogError("HoldKeyMode", "按键序列执行异常", ex);
            }
            finally
            {
                _isKeyHeld = false;
                _isRunning = false;
                LogModeEnd();
                _executionLock.Release();
            }
        }

        public override async Task StopAsync()
        {
            _isKeyHeld = false;
            await base.StopAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _executionLock.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}