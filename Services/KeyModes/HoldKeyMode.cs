using System;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp.Services.KeyModes
{
    public class HoldKeyMode : KeyModeBase
    {
        public HoldKeyMode(DDDriverService driverService) : base(driverService)
        {
        }

        public override async Task StartAsync()
        {
            if (!_driverService.IsInitialized) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            LogModeStart();

            try
            {
                while (_isRunning && !_cts.Token.IsCancellationRequested)
                {
                    foreach (var keyCode in _keyList.ToArray())
                    {
                        if (!_isRunning || _cts.Token.IsCancellationRequested) break;
                        
                        _driverService.SendKey(keyCode, true);
                        await Task.Delay(10, _cts.Token);
                        _driverService.SendKey(keyCode, false);
                    }
                    
                    if (_isRunning && !_cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(_keyInterval, _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不需要处理
            }
            catch (Exception ex)
            {
                _logger.LogError("HoldKeyMode", "按压模式异常", ex);
            }
            finally
            {
                LogModeEnd();
            }
        }
    }
} 