using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

// 按键按压模式
namespace WpfApp.Services.KeyModes
{
    public class HoldKeyMode : KeyModeBase
    {
        private volatile bool _isKeyHeld;
        private readonly SemaphoreSlim _executionLock = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private bool _isExecuting; 
        // 添加状态消息更新事件
        public event Action<string, bool>? OnStatusMessageUpdated;
        
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
                    _logger.Warning("已有按键序列在执行中");
                    return;
                }
                _isExecuting = true;
            }

            // 创建按键列表的副本
            var keyListCopy = new List<DDKeyCode>(_keyList);
            if (keyListCopy.Count == 0)
            {
                _logger.Warning("按键列表为空，无法启动序列");
                _isExecuting = false;
                OnStatusMessageUpdated?.Invoke("请至少选择一个按键", true);
                return;
            }

            // 检查是否有选中的按键
            var selectedKeys = keyListCopy.Where(k => k != DDKeyCode.None).ToList();
            if (selectedKeys.Count == 0)
            {
                _logger.Warning("没有选中任何按键，无法启动序列");
                _isExecuting = false;
                OnStatusMessageUpdated?.Invoke("请至少选择一个按键", true);
                return;
            }

            try
            {
                _isRunning = true;
                _cts = new CancellationTokenSource();

                LogModeStart();
                PrepareStart();

                _logger.Debug($"开始按键循环 - 按键数量: {selectedKeys.Count}");

                int currentIndex = 0;
                while (_isRunning && _isKeyHeld && !_cts.Token.IsCancellationRequested)
                {
                    var key = selectedKeys[currentIndex];
                    
                    if (!_isRunning || !_isKeyHeld || _cts.Token.IsCancellationRequested)
                    {
                        _logger.Debug("检测到按键释放或取消请求，停止循环");
                        break;
                    }

                    try
                    {
                        // 执行按键操作
                        if (!_driverService.SimulateKeyPress(key, null, KeyPressInterval))
                        {
                            _logger.Error($"按键执行失败: {key}");
                            continue;
                        }

                        Metrics.IncrementKeyCount();
                        
                        // 更新索引到下一个按键
                        currentIndex = (currentIndex + 1) % selectedKeys.Count;
                        
                        // 在每个按键之后添加延迟
                        if (_isRunning && _isKeyHeld && !_cts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(GetInterval(), _cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.Debug("按键延迟被取消");
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"执行按键 {key} 时发生异常", ex);
                        if (!_cts.Token.IsCancellationRequested)
                        {
                            continue;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("按键序列被取消");
            }
            catch (Exception ex)
            {
                _logger.Error("按键序列执行异常", ex);
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
            lock (_stateLock)
            {
                if (_isKeyHeld)
                {
                    _isKeyHeld = false;
                    _logger.Debug("检测到按键释放，准备停止循环");
                    
                    // 触发取消
                    if (_cts != null && !_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                    }
                }
            }
        }

        // 处理按键按下
        public void HandleKeyPress()
        {
            if (!_isExecuting)
            {
                lock (_stateLock)
                {
                    if (!_isExecuting)
                    {
                        _isKeyHeld = true;
                        _logger.Debug("检测到按键按下，准备开始循环");
                        // 启动按键循环
                        Task.Run(async () => await StartAsync());
                    }
                    else
                    {
                        _logger.Debug("已有按键序列在执行中，忽略此次按键按下");
                    }
                }
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

                // 确保所有按键都被释放
                foreach (var key in _keyList)
                {
                    try
                    {
                        await Task.Run(() => _driverService.SendKey(key, false));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"释放按键 {key} 时发生异常", ex);
                    }
                }
            }
            finally
            {
                lock (_stateLock)
                {
                    _isExecuting = false;
                }
                _logger.Debug("按键循环清理完成");
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