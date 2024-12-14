using System;
using System.Threading;
using System.Windows;
using System.Threading.Tasks;

namespace WpfApp.Services
{
    public class DDDriverService
    {
        private CDD _dd;
        private bool _isInitialized;
        private string? _loadedDllPath;
        private bool _isEnabled;
        private Timer? _timer;

        public event EventHandler<bool>? InitializationStatusChanged;
        public event EventHandler<bool>? EnableStatusChanged;

        // DD驱动服务构造函数
        public DDDriverService()
        {
            _dd = new CDD();
            _isInitialized = false;
            _isEnabled = false;
        }

        public bool LoadDllFile(string dllPath)
        {
            try
            {
                // 确保之前的实例被清理
                if(_isInitialized)
                {
                    _dd = new CDD();
                    _isInitialized = false;
                }

                // 同步加载驱动
                int ret = _dd.Load(dllPath);
                if (ret != 1)
                {
                    MessageBox.Show("驱动加载失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // 等待一小段时间确保驱动加载完成
                Thread.Sleep(100);

                // 初始化驱动 - 不传入窗口句柄
                if (_dd.btn == null)
                {
                    MessageBox.Show("驱动接口未正确加载！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                ret = _dd.btn(0);
                if (ret != 1)
                {
                    MessageBox.Show("驱动初始化失败！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                _isInitialized = true;
                _loadedDllPath = dllPath;
                InitializationStatusChanged?.Invoke(this, true);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"驱动加载异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // 是否启用
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    EnableStatusChanged?.Invoke(this, value);
                    
                    if (_isEnabled)
                    {
                        StartTimer();
                    }
                    else
                    {
                        StopTimer();
                    }
                }
            }
        }

        // 启动定时器
        private void StartTimer()
        {
            _timer?.Dispose();
            _timer = new Timer(TimerCallback, null, 0, 50); // 50ms 间隔
        }

        // 停止定时器
        private void StopTimer()
        {
            _timer?.Dispose();
            _timer = null;
        }

        // 定时器回调
        private void TimerCallback(object? state)
        {
            if (!_isEnabled || !_isInitialized) return;
            
            // 在这里添加定时执行的功能
        }

        // 模拟按键组合（如Ctrl+Alt+Del）
        public async Task SimulateKeyComboAsync(params DDKeyCode[] keyCodes)
        {
            if (!_isInitialized || _dd.key == null) return;

            try
            {
                // 按下所有键
                foreach (var key in keyCodes)
                {
                    _dd.key((int)key, 1);
                    await Task.Delay(5);
                }

                await Task.Delay(50);

                // 释放所有键（反序）
                for (int i = keyCodes.Length - 1; i >= 0; i--)
                {
                    _dd.key((int)keyCodes[i], 2);
                    await Task.Delay(5);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"模拟按键异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 输入文本
        public bool SimulateText(string text)
        {
            if (!_isInitialized || _dd.str == null) return false;
            try
            {
                return _dd.str(text) == 1;
            }
            catch
            {
                return false;
            }
        }

        // 鼠标相关方法
        public bool MouseClick(DDMouseButton button, int delayMs = 50)
        {
            if (!_isInitialized || _dd.btn == null) return false;

            int downCode = button switch
            {
                DDMouseButton.Left => 1,
                DDMouseButton.Right => 4,
                DDMouseButton.Middle => 16,
                DDMouseButton.XButton1 => 64,
                DDMouseButton.XButton2 => 256,
                _ => 0
            };

            if (downCode == 0) return false;

            try
            {
                _dd.btn(downCode);
                Thread.Sleep(delayMs);
                _dd.btn(downCode * 2); // 释放代码是按下代码的2倍
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 键盘按键
        public bool SendKey(int keyCode, bool isKeyDown)
        {
            if (!_isInitialized || _dd.key == null) return false;
            return _dd.key(keyCode, isKeyDown ? 1 : 2) == 1;
        }

        // 鼠标移动(绝对坐标)
        public bool MoveMouse(int x, int y)
        {
            if (!_isInitialized || _dd.mov == null) return false;
            return _dd.mov(x, y) == 1;
        }

        // 鼠标相对移动
        public bool MoveMouseRelative(int dx, int dy)
        {
            if (!_isInitialized || _dd.movR == null) return false;
            return _dd.movR(dx, dy) == 1;
        }

        // 滚轮操作
        public bool MouseWheel(bool isUp)
        {
            if (!_isInitialized || _dd.whl == null) return false;
            return _dd.whl(isUp ? 1 : 2) == 1;
        }

        // 添加以下方法
        public bool SendKey(DDKeyCode keyCode, bool isKeyDown)
        {
            return SendKey((int)keyCode, isKeyDown);
        }

        // 模拟按键按下和释放
        public bool SimulateKeyPress(DDKeyCode keyCode)
        {
            if (!_isInitialized) return false;
            
            // 按下并释放按键
            SendKey(keyCode, true);
            Thread.Sleep(50);
            SendKey(keyCode, false);
            return true;
        }

        public void Dispose()
        {
            _timer?.Dispose();
            // add dispose code here
        }
    }

    // 重命名鼠标按键枚举
    public enum DDMouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2
    }
} 