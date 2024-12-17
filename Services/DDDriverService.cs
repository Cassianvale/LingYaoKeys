using System;
using System.Threading;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// DD虚拟键盘驱动服务类
/// 核心功能：
/// 1. 驱动初始化与状态管理
///    - 加载DD驱动动态链接库
///    - 初始化驱动环境
///    - 管理驱动启用/禁用状态
///    - 提供驱动状态变化事件通知
///
/// 2. 按键操作控制
///    - 模拟按键按下/释放
///    - 执行按键组合操作
///    - 支持按键序列自动执行
///    - 控制按键触发间隔时间
///
/// 3. 按键模式管理
///    - 顺序模式：按设定顺序循环执行按键序列
///    - 按压模式：按住触发键时持续执行指定按键
///    - 模式切换与状态维护
/// </summary>
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
        public event EventHandler<StatusMessageEventArgs>? StatusMessageChanged;
        public event EventHandler<int>? KeyIntervalChanged;

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
                
                // 1. 清理现有实例
                if(_isInitialized)
                {
                    _dd = new CDD();
                    _isInitialized = false;
                }

                // 2. 简单的加载和初始化 - 只检查Load返回值
                int ret = _dd.Load(dllPath);
                if (ret != 1)
                {
                    System.Diagnostics.Debug.WriteLine($"驱动加载失败: {ret}");
                    return false;
                }

                // 3. 初始化驱动
                ret = _dd.btn(0);
                if (ret != 1)
                {
                    System.Diagnostics.Debug.WriteLine("驱动初始化失败");
                    MessageBox.Show("驱动初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    SendStatusMessage("驱动初始化失败", true);
                    return false;
                }
                // 5. 设置初始化标志
                _isInitialized = true;
                _loadedDllPath = dllPath;
                InitializationStatusChanged?.Invoke(this, true);
                SendStatusMessage("驱动加载成功");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"驱动加载异常: {ex}");
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
            _timer?.Dispose(); // 先释放旧的定时器
            _timer = new Timer(TimerCallback, null, 0, 10); // 创建新的定时器
        }

        // 停止定时器
        private void StopTimer()
        {
            _timer?.Dispose();  // 释放定时器
            _timer = null;       // 将定时器设置为null
        }

        // 定时器回调
        private void TimerCallback(object? state)
        {
            if (!_isEnabled || !_isInitialized) return;

            try 
            {
                if (_isSequenceMode)
                {
                    foreach (var keyCode in _keyList)
                    {
                        if (!_isEnabled) break;
                        System.Diagnostics.Debug.WriteLine($"发送按键: {keyCode} ({(int)keyCode})");
                        
                        if (!SendKey(keyCode, true))
                        {
                            System.Diagnostics.Debug.WriteLine($"按键按下失败: {keyCode}");
                            continue;
                        }
                        Thread.Sleep(_keyInterval);
                        
                        if (!SendKey(keyCode, false))
                        {
                            System.Diagnostics.Debug.WriteLine($"按键释放失败: {keyCode}");
                        }
                        Thread.Sleep(_keyInterval);
                    }
                }
                else if (_isHoldMode)
                {
                    foreach (var keyCode in _keyList)
                    {
                        if (!_isEnabled) break;
                        SendKey(keyCode, true);

                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"定时器回调异常: {ex}");

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
                SendStatusMessage($"模拟按键异常：{ex.Message}", true);
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

        // 鼠标移动(绝对坐标)以屏幕左上角为原点
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
                // 验证键码
                if (!KeyCodeMapping.IsValidDDKeyCode(keyCode))
                {
                    System.Diagnostics.Debug.WriteLine($"无效的DD键码: {keyCode} ({(int)keyCode})");
                    return false;
                }
                
                int ddCode = (int)keyCode;
                System.Diagnostics.Debug.WriteLine($"发送按键 - DD键码: {keyCode} ({ddCode}), 状态: {(isKeyDown ? "按下" : "释放")}");

                // 直接使用DD键码
                int ret = _dd.key(ddCode, isKeyDown ? 1 : 2);
                
                // 记录返回值但不影响执行结果
                System.Diagnostics.Debug.WriteLine($"按键操作返回值: {ret} - DD键码: {(int)keyCode}");

                // 只有在完全无法执行时才返回false
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"按键操作异常：{ex}");
                SendStatusMessage($"按键操作异常: {ex.Message}", true);
                return false;
            }
        }

        // 新增从虚拟键码发送按键的方法
        public bool SendVirtualKey(int virtualKeyCode, bool isKeyDown)
        {
            if (!_isInitialized || _dd.key == null) return false;

            try
            {
                // 用优化后的映射获取DD键码
                DDKeyCode ddKeyCode = KeyCodeMapping.GetDDKeyCode(virtualKeyCode, this);
                if (ddKeyCode == DDKeyCode.None) return false;

                // 显式转换为int
                int ret = _dd.key((int)ddKeyCode, isKeyDown ? 1 : 2);
                return ret == 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"虚拟按键操作异常：{ex}");
                return false;
            }
        }

        // 模拟按键按下和释放的完整过程
        public bool SimulateKeyPress(DDKeyCode keyCode, int? customDelay = null)
        {
            if (!_isInitialized) return false;
            
            try
            {
                // 使用自定义延迟或配置的间隔
                int delayMs = customDelay ?? _keyInterval;
                
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

        // 释放资源
        public void Dispose()
        {
            try
            {
                StopTimer();
                _timer?.Dispose();
                _timer = null;

                // 清理所有状态
                _isEnabled = false;
                _isInitialized = false;
                _isSequenceMode = false;
                _isHoldMode = false;
                _keyList.Clear();

                // 重新创建驱动实例
                _dd = new CDD();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dispose异常: {ex}");
            }
        }

        // 顺序模式&按压模式逻辑
        // 添加属性用于设置按键模式和按键列表
        public bool IsSequenceMode
        {
            get => _isSequenceMode;
            set => _isSequenceMode = value;
        }

        // 设置按键列表
        public void SetKeyList(List<DDKeyCode> keyList)
        {
            _keyList = keyList ?? new List<DDKeyCode>();
        }

        // 设置按键间隔
        public void SetKeyInterval(int interval)
        {
            KeyInterval = Math.Max(1, interval);
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

        // 模拟带修饰键的按键
        public async Task SimulateKeyWithModifiersAsync(DDKeyCode keyCode, KeyModifiers modifiers)
        {
            if (!_isInitialized) return;

            var modifierKeys = new List<DDKeyCode>();
            
            // 添加修饰键
            if (modifiers.HasFlag(KeyModifiers.Control))
                modifierKeys.Add(DDKeyCode.LEFT_CTRL);
            if (modifiers.HasFlag(KeyModifiers.Alt))
                modifierKeys.Add(DDKeyCode.LEFT_ALT);
            if (modifiers.HasFlag(KeyModifiers.Shift))
                modifierKeys.Add(DDKeyCode.LEFT_SHIFT);
            if (modifiers.HasFlag(KeyModifiers.Windows))
                modifierKeys.Add(DDKeyCode.LEFT_WIN);

            try
            {
                // 按下所有修饰键
                foreach (var modifier in modifierKeys)
                {
                    SendKey(modifier, true);
                    await Task.Delay(5);
                }

                // 按下主键
                SendKey(keyCode, true);
                await Task.Delay(50);
                SendKey(keyCode, false);

                // 释放所有修饰键(反序)
                for (int i = modifierKeys.Count - 1; i >= 0; i--)
                {
                    SendKey(modifierKeys[i], false);
                    await Task.Delay(5);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"模拟组合键异常：{ex}");
                // 确保释放所有按键
                foreach (var modifier in modifierKeys)
                {
                    SendKey(modifier, false);
                }
            }
        }

        // 检查修饰键状态
        public bool IsModifierKeyPressed(KeyModifiers modifier)
        {
            if (!_isInitialized) return false;

            try
            {
                switch (modifier)
                {
                    case KeyModifiers.Control:
                        return IsKeyPressed(DDKeyCode.LEFT_CTRL) || IsKeyPressed(DDKeyCode.RIGHT_CTRL);
                    case KeyModifiers.Alt:
                        return IsKeyPressed(DDKeyCode.LEFT_ALT) || IsKeyPressed(DDKeyCode.RIGHT_ALT);
                    case KeyModifiers.Shift:
                        return IsKeyPressed(DDKeyCode.LEFT_SHIFT) || IsKeyPressed(DDKeyCode.RIGHT_SHIFT);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查修饰键状态异常：{ex}");
                return false;
            }
        }

        // 检查按键是否按下
        private bool IsKeyPressed(DDKeyCode keyCode)
        {
            if (_dd.key == null) return false;
            return _dd.key((int)keyCode, 3) == 1; // 3表示检查按键状态
        }

        // 执行按键序列
        public async Task ExecuteKeySequenceAsync(IEnumerable<DDKeyCode> keySequence, int? customInterval = null)
        {
            if (!_isInitialized) return;

            foreach (var keyCode in keySequence)
            {
                if (!_isEnabled) break;

                try
                {
                    await Task.Run(() =>
                    {
                        SendKey(keyCode, true);
                        Thread.Sleep(customInterval ?? _keyInterval);
                        SendKey(keyCode, false);
                        Thread.Sleep(customInterval ?? _keyInterval);
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"执行按键序列异常: {ex.Message}");
                    break;
                }
            }
        }

        // 检查驱动是否就绪
        public bool IsReady => _isInitialized && _dd.key != null;

        // 将Windows虚拟键码转换为DD键码
        public int? ConvertVirtualKeyToDDCode(int vkCode)
        {
            if (!_isInitialized || _dd.todc == null)
            {
                System.Diagnostics.Debug.WriteLine("驱动未初始化或todc函数指针为空");
                return null;
            }

            try
            {
                int ddCode = _dd.todc(vkCode);
                if (ddCode <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"虚拟键码转换失败: {vkCode}");
                    return null;
                }
                return ddCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"虚拟键码转换异常: {ex}");
                return null;
            }
        }

        // 发送状态消息的方法
        private void SendStatusMessage(string message, bool isError = false)
        {
            System.Diagnostics.Debug.WriteLine($"[DDDriver] {(isError ? "错误" : "信息")}: {message}");
            StatusMessageChanged?.Invoke(this, new StatusMessageEventArgs(message, isError));
        }

        // 修改现有的按键间隔字段和属性
        public int KeyInterval
        {
            get => _keyInterval;
            set
            {
                if (value != _keyInterval && value >= 1)
                {
                    _keyInterval = value;
                    KeyIntervalChanged?.Invoke(this, value);
                    System.Diagnostics.Debug.WriteLine($"按键间隔已更新为: {value}ms");
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

    // 添加事件定义，向UI传递消息
    public class StatusMessageEventArgs : EventArgs
    {
        public string Message { get; }
        public bool IsError { get; }
        public StatusMessageEventArgs(string message, bool isError = false)
        {
            Message = message;
            IsError = isError;
        }
    }
} 