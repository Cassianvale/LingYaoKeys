using System;
using System.Threading;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;

// 封装CDD驱动服务
namespace WpfApp.Services
{
    public class DDDriverService
    {
        private CDD _dd;
        private bool _isInitialized;
        private string? _loadedDllPath;
        private bool _isEnabled;
        private Timer? _timer;
        private bool _isSequenceMode;
        private List<DDKeyCode> _keyList = new List<DDKeyCode>();
        private bool _isHoldMode;
        private int _keyInterval = 50;

        public event EventHandler<bool>? InitializationStatusChanged;
        public event EventHandler<bool>? EnableStatusChanged;

        // DD驱动服务构造函数
        public DDDriverService()
        {
            _dd = new CDD();
            _isInitialized = false;
            _isEnabled = false;
        }

        // 添加公共属性用于检查初始化状态
        public bool IsInitialized => _isInitialized;

        public bool LoadDllFile(string dllPath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"开始加载驱动文件: {dllPath}");
                
                // 1. 确保之前的实例被清理
                if(_isInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("清理现有驱动实例");
                    _dd = new CDD();
                    _isInitialized = false;
                }

                // 2. 直接调用Load方法并检查返回值
                System.Diagnostics.Debug.WriteLine("调用DD.Load()...");
                int ret = _dd.Load(dllPath);
                System.Diagnostics.Debug.WriteLine($"DD.Load()返回值: {ret}");
                
                // 检查返回值
                if (ret != 1)
                {
                    string errorMsg = ret switch
                    {
                        -1 => "获取驱动函数地址失败",
                        -2 => "加载驱动文件失败",
                        _ => $"未知错误 ({ret})"
                    };
                    System.Diagnostics.Debug.WriteLine($"驱动加载失败: {errorMsg}");
                    MessageBox.Show($"驱动加载失败: {errorMsg}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // 3. 如果Load成功，设置初始化标志
                _isInitialized = true;
                _loadedDllPath = dllPath;
                InitializationStatusChanged?.Invoke(this, true);
                
                System.Diagnostics.Debug.WriteLine("驱动加载成功");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"驱动加载过程中发生异常：{ex}");
                MessageBox.Show($"驱动加载异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // 修改验证方法
        public bool ValidateDriver()
        {
            if (!_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("驱动未初始化");
                return false;
            }

            try
            {
                // 检查btn函数指针是否为空
                if (_dd.btn == null)
                {
                    System.Diagnostics.Debug.WriteLine("btn函数指针为空");
                    return false;
                }

                // 调用btn函数
                int ret = _dd.btn(0);
                System.Diagnostics.Debug.WriteLine($"驱动状态检查返回值: {ret}");
                return ret == 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"驱动验证时发生异常：{ex}");
                return false;
            }
        }

        // 是否启用
        public bool IsEnabled
        {
            get => _isEnabled;
            set
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

        // 启动定时器
        private void StartTimer()
        {
            _timer?.Dispose();
            _timer = new Timer(TimerCallback, null, 0, 10);
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

            // 使用同步方式执行按键
            try 
            {
                if (_isSequenceMode)
                {
                    foreach (var keyCode in _keyList)
                    {
                        if (!_isEnabled) return;
                        
                        SendKey(keyCode, true);
                        Thread.Sleep(_keyInterval);
                        SendKey(keyCode, false);
                        Thread.Sleep(_keyInterval);
                    }
                }
                else if (_isHoldMode)
                {
                    foreach (var keyCode in _keyList)
                    {
                        if (!_isHoldMode) return;
                        
                        SendKey(keyCode, true);
                        Thread.Sleep(_keyInterval);
                        SendKey(keyCode, false);
                        Thread.Sleep(_keyInterval);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行按键时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _isEnabled = false;
            }
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
            if (!_isInitialized || _dd.str == null)
            {
                System.Diagnostics.Debug.WriteLine("驱动未初始化或str函数指针为空");
                return false;
            }
            try
            {
                return _dd.str(text) == 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"模拟文本输入异常：{ex}");
                return false;
            }
        }

        // 鼠标相关方法
        public bool MouseClick(DDMouseButton button, int delayMs = 50)
        {
            if (!_isInitialized || _dd.btn == null)
            {
                System.Diagnostics.Debug.WriteLine("驱动未初始化或btn函数指针为空");
                return false;
            }

            // 根据文档说明设置按键代码
            (int down, int up) = button switch
            {
                DDMouseButton.Left => (1, 2),      // 左键：按下=1，放开=2
                DDMouseButton.Right => (4, 8),      // 右键：按下=4，放开=8
                DDMouseButton.Middle => (16, 32),   // 中键：按下=16，放开=32
                DDMouseButton.XButton1 => (64, 128), // 4键：按下=64，放开=128
                DDMouseButton.XButton2 => (256, 512), // 5键：按下=256，放开=512
                _ => (0, 0)
            };

            if (down == 0) return false;

            try
            {
                // 按下按键
                int ret = _dd.btn(down);
                if (ret != 1)
                {
                    System.Diagnostics.Debug.WriteLine($"鼠标按下失败，返回值：{ret}");
                    return false;
                }

                // 延迟
                Thread.Sleep(delayMs);

                // 释放按键
                ret = _dd.btn(up);
                if (ret != 1)
                {
                    System.Diagnostics.Debug.WriteLine($"鼠标释放失败，返回值：{ret}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"鼠标点击异常：{ex}");
                return false;
            }
        }

        // 鼠标移动(绝对坐标)，以屏幕左上角为原点
        public bool MoveMouse(int x, int y)
        {
            if (!_isInitialized || _dd.mov == null)
            {
                System.Diagnostics.Debug.WriteLine("驱动未初始化或mov函数指针为空");
                return false;
            }

            try
            {
                int ret = _dd.mov(x, y);
                if (ret != 1)
                {
                    System.Diagnostics.Debug.WriteLine($"鼠标移动失败，返回值：{ret}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"鼠标移动异常：{ex}");
                return false;
            }
        }

        // 滚轮操作：1=前滚，2=后滚
        public bool MouseWheel(bool isForward)
        {
            if (!_isInitialized || _dd.whl == null)
            {
                System.Diagnostics.Debug.WriteLine("驱动未初始化或whl函数指针为空");
                return false;
            }

            try
            {
                int ret = _dd.whl(isForward ? 1 : 2);
                if (ret != 1)
                {
                    System.Diagnostics.Debug.WriteLine($"鼠标滚轮操作失败，返回值：{ret}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"鼠标滚轮操作异常：{ex}");
                return false;
            }
        }

        // 键盘按键
        public bool SendKey(DDKeyCode keyCode, bool isKeyDown)
        {
            if (!_isInitialized || _dd.key == null)
            {
                System.Diagnostics.Debug.WriteLine("驱动未初始化或key函数指针为空");
                return false;
            }

            try
            {
                int ret = _dd.key((int)keyCode, isKeyDown ? 1 : 2);
                if (ret != 1)
                {
                    System.Diagnostics.Debug.WriteLine($"按键操作失败，键码：{keyCode}，状态：{(isKeyDown ? "按下" : "释放")}，返回值：{ret}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"按键操作异常：{ex}");
                return false;
            }
        }

        // 模拟按键按下和释放的完整过程
        public bool SimulateKeyPress(DDKeyCode keyCode, int delayMs = 50)
        {
            if (!_isInitialized) return false;
            
            try
            {
                // 按下按键
                if (!SendKey(keyCode, true))
                {
                    return false;
                }

                // 延迟
                Thread.Sleep(delayMs);

                // 释放按键
                return SendKey(keyCode, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"模拟按键异常：{ex}");
                return false;
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            // add dispose code here
        }

        // 添加属性用于设置按键模式和按键列表
        public bool IsSequenceMode
        {
            get => _isSequenceMode;
            set => _isSequenceMode = value;
        }

        public void SetKeyList(List<DDKeyCode> keyList)
        {
            _keyList = keyList ?? new List<DDKeyCode>();
        }

        public void SetKeyInterval(int interval)
        {
            _keyInterval = Math.Max(1, interval);
        }

        // 添加方法用于控制按压模式
        public void SetHoldMode(bool isHold)
        {
            _isHoldMode = isHold;
            if (!_isSequenceMode)
            {
                _isEnabled = isHold;
                if (isHold)
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

    // 重命名鼠标按键枚举
    public enum DDMouseButton
    {
        Left,   // 左键
        Right,  // 右键
        Middle, // 中键
        XButton1, // 侧键1
        XButton2 // 侧键2
    }
} 