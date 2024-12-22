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
        private readonly object _stateLock = new object();
        private bool _isExecuting;

        public HoldKeyMode(DDDriverService driverService) : base(driverService)
        {
        }

        public override async Task StartAsync()
        {
            // 防止重复启动
            lock (_stateLock)
            {
                if (_isExecuting)
                {
                    _logger.LogWarning("HoldKeyMode", "已有按键序列在执行中");
                    return;
                }
                _isExecuting = true;
            }

            if (_keyList.Count == 0)
            {
                _isExecuting = false;
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
                            _logger.LogDebug("HoldKeyMode", "检测到按键释放或取消请求，停止循环");
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
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("HoldKeyMode", $"执行按键 {key} 时发生异常", ex);
                        }
                    }

                    if (_isRunning && _isKeyHeld && !_cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(GetInterval(), _cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogDebug("HoldKeyMode", "按键延迟被取消");
                            break;
                        }
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
                await CleanupAsync();
            }
        }

        public override async Task StopAsync()
        {
            try
            {
                _isKeyHeld = false;
                await base.StopAsync();
            }
            finally
            {
                lock (_stateLock)
                {
                    _isExecuting = false;
                }
            }
        }

        // 处理按键释放
        public void HandleKeyRelease()
        {
            if (_isKeyHeld)
            {
                _isKeyHeld = false;
                _logger.LogDebug("HoldKeyMode", "检测到按键释放，准备停止循环");
                
                // 触发取消
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }
            }
        }

        // 处理按键按下
        public void HandleKeyPress()
        {
            if (!_isExecuting)
            {
                _isKeyHeld = true;
                _logger.LogDebug("HoldKeyMode", "检测到按键按下，准备开始循环");
            }
        }

        private async Task CleanupAsync()
        {
            try
            {
                _isKeyHeld = false;
                _isRunning = false;
                
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

                LogModeEnd();
                
                // 确保所有按键都被释放
                foreach (var key in _keyList)
                {
                    await Task.Run(() => _driverService.SendKey(key, false));
                }
            }
            finally
            {
                lock (_stateLock)
                {
                    _isExecuting = false;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _executionLock.Dispose();
                CleanupAsync().Wait();
            }
            base.Dispose(disposing);
        }
    }
}