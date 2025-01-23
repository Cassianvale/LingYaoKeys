using WpfApp.Services.Core;

// 按键按压模式
namespace WpfApp.Services.Models
{
    public class HoldKeyMode : KeyModeBase
    {
        private volatile bool _isKeyHeld;
        private readonly SemaphoreSlim _executionLock = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private bool _isExecuting;  // 当前是否有按键序列正在执行
        
        // 添加状态消息更新事件
        public event Action<string, bool>? OnStatusMessageUpdated;
        
        public HoldKeyMode(LyKeysService driverService) : base(driverService)
        {
            _isKeyHeld = false; // 用于按压模式的执行控制
            _isExecuting = false;   // 当前是否有按键序列正在执行
        }

        public override void Start()
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

            try
            {
                _isRunning = true;
                _cts = new CancellationTokenSource();

                LogModeStart();

                var selectedKeys = _keyList.ToList();
                if (selectedKeys.Count == 0)
                {
                    _logger.Warning("没有选中的按键");
                    return;
                }

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
                        PressKey(key);
                        Thread.Sleep(GetInterval());
                        
                        // 更新索引
                        currentIndex = (currentIndex + 1) % selectedKeys.Count;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"按键执行异常: {key}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("按压模式执行异常", ex);
            }
            finally
            {
                lock (_stateLock)
                {
                    _isExecuting = false;
                }
                LogModeEnd();
                Stop();
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
                        new Thread(() => Start()).Start();
                    }
                    else
                    {
                        _logger.Debug("已有按键序列在执行中，忽略此次按键按下");
                    }
                }
            }
        }

        // 处理按键释放
        public void HandleKeyRelease()
        {
            _isKeyHeld = false;
            Stop();
        }

        public override void Stop()
        {
            base.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _executionLock.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}