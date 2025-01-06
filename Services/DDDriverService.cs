using System;
using System.Threading;
using System.Windows;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Linq;
using WpfApp.Services.KeyModes;
using WpfApp.Services;

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
        private bool _isEnabled;
        private bool _isHoldMode;
        private KeyModeBase? _currentKeyMode;
        private List<DDKeyCode> _keyList = new List<DDKeyCode>();
        private readonly object _stateLock = new object();
        private readonly Stopwatch _sequenceStopwatch = new Stopwatch();
        private readonly SerilogManager _logger = SerilogManager.Instance;
        internal readonly InputMethodService _inputMethodService;
        public event EventHandler<bool>? InitializationStatusChanged;
        public event EventHandler<bool>? EnableStatusChanged;
        public event EventHandler<int>? KeyIntervalChanged;
        public event EventHandler<StatusMessageEventArgs>? StatusMessageChanged;
        public event EventHandler<int>? KeyPressIntervalChanged;

        private readonly TaskManager _taskManager;
        private const int MAX_CONCURRENT_TASKS = 1; // 最大并发任务数
        
        private const int MIN_KEY_INTERVAL = 1;  // 默认最小按键间隔
        private int _keyInterval = 5;   // 默认按键间隔
        private int _keyPressInterval;   // 按键按下时长
        public const int DEFAULT_KEY_PRESS_INTERVAL = 5; // 默认按键按下时长(毫秒)
        private const int MIN_KEY_PRESS_INTERVAL = 0;  // 按键按下时长为0

        private bool _isDisposed;
        private readonly object _disposeLock = new object();

        public bool IsDisposed => _isDisposed;

        // DD驱动服务构造函数
        public DDDriverService()
        {
            _dd = new CDD();
            _isInitialized = false;
            _isEnabled = false;
            _taskManager = new TaskManager(MAX_CONCURRENT_TASKS);
            _inputMethodService = new InputMethodService();
            
            // 初始化时从配置加载，如果没有配置则使用默认值
            _keyPressInterval = AppConfigService.Config.KeyPressInterval ?? DEFAULT_KEY_PRESS_INTERVAL;
        }

        // 添加公共属性用于检查初始化状态
        public bool IsInitialized => _isInitialized;

        public bool LoadDllFile(string dllPath)
        {
            try
            {
                _logger.Debug($"加载驱动文件: {dllPath}");
                
                // 1. 清理现有实例
                if(_isInitialized)
                {
                    _dd = new CDD();
                    _isInitialized = false;
                }

                // 2. 加载驱动 - 只检查Load返回值
                int ret = _dd.Load(dllPath);
                if (ret != 1)
                {
                    _logger.Error($"驱动加载失败，返回值: {ret}");
                    return false;
                }

                // 3. 初始化驱动
                if (_dd?.btn != null)
                {
                    ret = _dd.btn(0);
                    if (ret != 1)
                    {
                        _logger.Error("驱动初始化失败");
                        System.Windows.MessageBox.Show("驱动初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        SendStatusMessage("驱动初始化失败", true);
                        return false;
                    }
                }
                else
                {
                    _logger.Error("驱动对象或 btn 方法为空");
                    return false;
                }

                // 5. 设置初始化标志
                _isInitialized = true;
                InitializationStatusChanged?.Invoke(this, true);
                SendStatusMessage("驱动加载成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("驱动加载异常", ex);
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
                        // 使用 Fire-and-forget 模式启动序列
                        _ = StartKeySequenceAsync().ContinueWith(t =>
                        {
                            if (t.Exception != null)
                            {
                                _logger.Error("按键序列启动异常", t.Exception);
                                _isEnabled = false;
                            }
                        });
                    }
                    else
                    {
                        // 使用 Fire-and-forget 模式停止序列
                        _ = StopKeySequenceAsync().ContinueWith(t =>
                        {
                            if (t.Exception != null)
                            {
                                _logger.Error("按键序列停止异常", t.Exception);
                            }
                            // 恢复输入法状态
                            _inputMethodService.RestorePreviousLayout();
                        });
                    }
                }
            }
        }

        // 异步启动序列
        private async Task StartKeySequenceAsync()
        {
            try
            {
                await StopKeySequenceAsync();
                
                KeyModeBase? modeToStart;
                lock (_stateLock)
                {
                    if (_isEnabled && _currentKeyMode != null)
                    {
                        _sequenceStopwatch.Restart();
                        modeToStart = _currentKeyMode;
                    }
                    else
                    {
                        modeToStart = null;
                    }
                }

                // 在lock外启动任务
                if (modeToStart != null)
                {
                    await _taskManager.StartTask(
                        "KeyMode",
                        async (token) => await modeToStart.StartAsync(),
                        TaskPriority.High
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.Error("启动序列异常", ex);
                await StopKeySequenceAsync();
            }
        }

        // 异步停止
        private async Task StopKeySequenceAsync()
        {
            try
            {
                // 先停止TaskManager中的任务
                await _taskManager.StopTask("KeyMode", TimeSpan.FromSeconds(1));

                if (_currentKeyMode != null)
                {
                    await _currentKeyMode.StopAsync();
                    // _currentKeyMode.LogMetrics();
                }

            }
            catch (Exception ex)
            {
                _logger.Error("停止序列异常", ex);
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

                await Task.Delay(10);

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
                _logger.Error("驱动未初始化或str函数指针为空");
                return false;
            }
            try
            {
                return _dd.str(text) == 1;
            }
            catch (Exception ex)
            {
                _logger.Error("模拟文本输入异常", ex);
                return false;
            }
        }

        // 鼠标相关方法
        public bool MouseClick(DDMouseButton button, int delayMs = 10)
        {
            if (!_isInitialized || _dd.btn == null)
            {
                _logger.Error("驱动未初始化或btn函数指针为空");
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
                    _logger.Warning($"鼠标按下失败，返回值：{ret}");
                    return false;
                }

                // 延迟
                Thread.Sleep(delayMs);

                // 释放按键
                ret = _dd.btn(up);
                if (ret != 1)
                {
                    _logger.Warning($"鼠标释放失败，返回值：{ret}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("鼠标点击异常", ex);
                return false;
            }
        }

        // 鼠标移动(绝对坐标)以屏幕左上角为原点
        public bool MoveMouse(int x, int y)
        {
            if (!_isInitialized || _dd.mov == null)
            {
                _logger.Error("驱动未初始化或mov函数指针为空");
                return false;
            }

            try
            {
                int ret = _dd.mov(x, y);
                if (ret != 1)
                {
                    _logger.Warning($"鼠标移动失败，返回值：{ret}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("鼠标移动异常", ex);
                return false;
            }
        }

        // 滚轮操作：1=前滚，2=后滚
        public bool MouseWheel(bool isForward)
        {
            if (!_isInitialized || _dd.whl == null)
            {
                _logger.Error("驱动未初始化或whl函数指针为空");
                return false;
            }

            try
            {
                int ret = _dd.whl(isForward ? 1 : 2);
                if (ret != 1)
                {
                    _logger.Warning($"鼠标滚轮操作失败，返回值：{ret}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("鼠标滚轮操作异常", ex);
                return false;
            }
        }

        // 键盘按键
        public bool SendKey(DDKeyCode keyCode, bool isKeyDown)
        {
            if (!_isInitialized || _dd.key == null)
            {
                _logger.Error("驱动未初始化或key函数指针为空");
                return false;
            }

            try
            {
                if (!KeyCodeMapping.IsValidDDKeyCode(keyCode))
                {
                    _logger.Error($"无效的DD键码: {keyCode} ({(int)keyCode})");
                    return false;
                }
                
                int ddCode = (int)keyCode;
                int ret = _dd.key(ddCode, isKeyDown ? 1 : 2);
                
                // 记录按键操作
                // _logger.LogKeyOperation(keyCode, isKeyDown, ret);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("按键操作异常", ex);
                return false;
            }
        }

        // 从虚拟键码发送按键的方法
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
                _logger.Error("虚拟按键操作异常", ex);
                return false;
            }
        }

        // 移除通用的按键防抖
        private readonly Dictionary<DDKeyCode, DateTime> _lastKeyPressTimes = new();
        
        // 优化按键检测方法
        public bool SimulateKeyPress(DDKeyCode keyCode, int? customDelay = null, int? customPressInterval = null)
        {
            if (!_isInitialized) return false;
            
            try
            {
                // 使用自定义延迟或配置的间隔
                int delayMs = customDelay ?? _keyInterval;
                int pressIntervalMs = customPressInterval ?? _keyPressInterval;
                
                // 执行按键操作
                if (!SendKey(keyCode, true)) return false;
                Thread.Sleep(pressIntervalMs);
                if (!SendKey(keyCode, false)) return false;
                Thread.Sleep(delayMs);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("模拟按键异常", ex);
                return false;
            }
        }

        // 释放资源
        public async void Dispose()
        {
            try
            {
                _logger.Debug("开始释放驱动资源");
                
                IsEnabled = false;
                
                // 停止所有任务
                await _taskManager.StopAllTasks(TimeSpan.FromSeconds(2));
                
                // 清理资源
                _isEnabled = false;
                _isHoldMode = false;
                _keyList.Clear();
                
                // 恢复输入法状态
                _inputMethodService.RestorePreviousLayout();
                
                _isInitialized = false;
                _dd = new CDD();

                _logger.Debug("驱动资源释放完成");
            }
            catch (Exception ex)
            {
                _logger.Error("释放资源时发生异常", ex);
            }
        }

        // 添加模式切换事件
        public event EventHandler<bool>? ModeSwitched;

        // 顺序模式&按压模式逻辑
        // 添加属性用于设置按键模式和按键列表
        public bool IsSequenceMode
        {
            get => _currentKeyMode is SequenceKeyMode;
            set
            {
                bool wasSequenceMode = IsSequenceMode;
                if (wasSequenceMode == value) return;
                
                _logger.Debug(
                    $"切换模式 - " +
                    $"当前: {(wasSequenceMode ? "顺序模式" : "按压模式")}, " +
                    $"目标: {(value ? "顺序模式" : "按压模式")}");
                
                try
                {
                    // 停止当前运行的序列
                    IsEnabled = false;

                    if (value)
                    {
                        // 切换到顺序模式
                        _currentKeyMode = new SequenceKeyMode(this);
                        _isHoldMode = false;
                    }
                    else
                    {
                        // 切换到按压模式
                        _currentKeyMode = new HoldKeyMode(this);
                        _isHoldMode = true;
                    }

                    // 设置按键列表和间隔
                    _currentKeyMode?.SetKeyList(_keyList);
                    _currentKeyMode?.SetKeyInterval(_keyInterval);
                    if (_currentKeyMode != null)
                    {
                        _currentKeyMode.SetKeyPressInterval(KeyPressInterval);
                    }

                    // 触发模式切换事件
                    ModeSwitched?.Invoke(this, value);
                    
                    _logger.Debug(
                        $"模式切换完成 - " +
                        $"新模式: {(value ? "顺序模式" : "按压模式")}");
                }
                catch (Exception ex)
                {
                    _logger.Error("模式切换异常", ex);
                    // 发生异常时恢复到原始状态
                    if (_currentKeyMode != null)
                    {
                        _currentKeyMode.Dispose();
                    }
                    _currentKeyMode = wasSequenceMode ? 
                        new SequenceKeyMode(this) : 
                        new HoldKeyMode(this) as KeyModeBase;
                    _isHoldMode = !wasSequenceMode;
                    IsEnabled = false;
                }
            }
        }

        // 设置模式而不触发事件
        public void SetModeWithoutEvent(bool isSequenceMode)
        {
            bool wasSequenceMode = IsSequenceMode;
            if (wasSequenceMode == isSequenceMode) return;
            
            try
            {
                // 停止当前运行的序列
                IsEnabled = false;

                if (isSequenceMode)
                {
                    // 切换到顺序模式
                    _currentKeyMode = new SequenceKeyMode(this);
                    _isHoldMode = false;
                }
                else
                {
                    // 切换到按压模式
                    _currentKeyMode = new HoldKeyMode(this);
                    _isHoldMode = true;
                }

                // 设置按键列表和间隔
                _currentKeyMode?.SetKeyList(_keyList);
                _currentKeyMode?.SetKeyInterval(_keyInterval);
                if (_currentKeyMode != null)
                {
                    _currentKeyMode.SetKeyPressInterval(KeyPressInterval);
                }
                
                _logger.Debug(
                    $"静默模式切换完成 - " +
                    $"新模式: {(isSequenceMode ? "顺序模式" : "按压模式")}");
            }
            catch (Exception ex)
            {
                _logger.Error("静默模式切换异常", ex);
                // 发生异常时恢复到原始状态
                if (_currentKeyMode != null)
                {
                    _currentKeyMode.Dispose();
                }
                _currentKeyMode = wasSequenceMode ? 
                    new SequenceKeyMode(this) : 
                    new HoldKeyMode(this) as KeyModeBase;
                _isHoldMode = !wasSequenceMode;
                IsEnabled = false;
            }
        }

        // 设置按键列表
        public void SetKeyList(List<DDKeyCode> keyList)
        {
            try
            {
                if (keyList == null || keyList.Count == 0)
                {
                    _logger.Warning("收到空的按键列表，停止当前运行的序列");
                    // 如果当前正在运行，则停止
                    if (_isEnabled)
                    {
                        IsEnabled = false;
                        SetHoldMode(false);
                    }
                    _keyList.Clear();
                    return;
                }
                
                _keyList = new List<DDKeyCode>(keyList);
                if (_currentKeyMode != null)
                {
                    _currentKeyMode.SetKeyList(new List<DDKeyCode>(_keyList));
                }
                
                _logger.Debug(
                    $"按键列表已更新 - 按键为: {string.Join(", ", _keyList)}, 按键数量: {_keyList.Count}, 当前模式: {(_currentKeyMode?.GetType().Name ?? "无")}");
            }
            catch (Exception ex)
            {
                _logger.Error("设置按键列表异常", ex);
                // 发生异常时清空按键列表并停止服务
                _keyList.Clear();
                IsEnabled = false;
                SetHoldMode(false);
            }
        }

        // 设置按键间隔
        public void SetKeyInterval(int interval)
        {
            KeyInterval = interval;
        }

        // 添加方法用于控制按压模式
        public void SetHoldMode(bool isHold)
        {
            try
            {
                _logger.Debug(
                    $"设置按压模式 - " +
                    $"isHold: {isHold}, " +
                    $"当前模式: {(IsSequenceMode ? "顺序模式" : "按压模式")}");

                if (IsSequenceMode)
                {
                    _logger.Warning("当前为顺序模式，忽略按压模式设置");
                    return;
                }

                lock (_stateLock)
                {
                    if (isHold)
                    {
                        // 先准备好所有状态
                        if (_currentKeyMode is not HoldKeyMode)
                        {
                            _currentKeyMode?.Dispose();
                            _currentKeyMode = new HoldKeyMode(this);
                            _currentKeyMode.SetKeyList(_keyList);
                            _currentKeyMode.SetKeyInterval(_keyInterval);
                            _currentKeyMode.SetKeyPressInterval(KeyPressInterval);
                        }

                        // 设置状态
                        _isHoldMode = true;
                        _isEnabled = true;
                        
                        // 通知状态变更
                        OnEnableStatusChanged(_isEnabled);
                        
                        // 最后启动按压模式
                        if (_currentKeyMode is HoldKeyMode holdMode)
                        {
                            holdMode.HandleKeyPress();
                        }
                    }
                    else
                    {
                        // 先处理按键释放
                        if (_currentKeyMode is HoldKeyMode holdMode)
                        {
                            holdMode.HandleKeyRelease();
                        }
                        
                        // 然后更新状态
                        _isHoldMode = false;
                        _isEnabled = false;
                        OnEnableStatusChanged(_isEnabled);
                    }
                }
                
                _logger.Debug(
                    $"按压模式状态已更新 - " +
                    $"isHold: {isHold}, " +
                    $"isEnabled: {_isEnabled}, " +
                    $"按键数量: {_keyList.Count}");
            }
            catch (Exception ex)
            {
                _logger.Error("设置按压模式异常", ex);
                // 发生异常时确保停止
                _isEnabled = false;
                _isHoldMode = false;
                OnEnableStatusChanged(_isEnabled);
                if (_currentKeyMode is HoldKeyMode holdMode)
                {
                    try
                    {
                        holdMode.HandleKeyRelease();
                    }
                    catch (Exception releaseEx)
                    {
                        _logger.Error("异常处理时释放按键失败", releaseEx);
                    }
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
                await Task.Delay(10);
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
                _logger.Error("模拟组合键异常", ex);
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
                        return IsKeyPressedByDriver(DDKeyCode.LEFT_CTRL) || IsKeyPressedByDriver(DDKeyCode.RIGHT_CTRL);
                    case KeyModifiers.Alt:
                        return IsKeyPressedByDriver(DDKeyCode.LEFT_ALT) || IsKeyPressedByDriver(DDKeyCode.RIGHT_ALT);
                    case KeyModifiers.Shift:
                        return IsKeyPressedByDriver(DDKeyCode.LEFT_SHIFT) || IsKeyPressedByDriver(DDKeyCode.RIGHT_SHIFT);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("检查修饰键状态异常", ex);
                return false;
            }
        }

        // 使用DD驱动检测按键状态
        private bool IsKeyPressedByDriver(DDKeyCode keyCode)
        {
            if (_dd.key == null) return false;
            return _dd.key((int)keyCode, 3) == 1; // 3表示检查按键状态
        }

        // 检查驱动是否就绪
        public bool IsReady => _isInitialized && _dd.key != null;

        // 将Windows虚拟键码转换为DD键码
        public int? ConvertVirtualKeyToDDCode(int vkCode)
        {
            if (!_isInitialized || _dd.todc == null)
            {
                _logger.Error("驱动未初始化或todc函数指针为空");
                return null;
            }

            try
            {
                int ddCode = _dd.todc(vkCode);
                if (ddCode <= 0)
                {
                    _logger.Warning($"虚拟键码转换失败: {vkCode}");
                    return null;
                }
                return ddCode;
            }
            catch (Exception ex)
            {
                _logger.Error("虚拟键码转换异常", ex);
                return null;
            }
        }

        // 发送状态消息的方法
        private void SendStatusMessage(string message, bool isError = false)
        {
            StatusMessageChanged?.Invoke(this, new StatusMessageEventArgs(message, isError));
            if (isError)
                 _logger.Error($"发送状态消息失败: {message}");
            else
                _logger.DriverEvent("Status", message);
        }

        // 修改现有的按键间隔字段和属性
        public int KeyInterval
        {
            get => _keyInterval;
            set
            {
                int validValue = Math.Max(MIN_KEY_INTERVAL, value);
                if (_keyInterval != validValue)
                {
                    _keyInterval = validValue;
                    KeyIntervalChanged?.Invoke(this, validValue);
                    _logger.SequenceEvent($"按键间隔已更新为: {validValue}ms");
                    
                    // 如果有当前模式，同步更新
                    _currentKeyMode?.SetKeyInterval(validValue);
                }
            }
        }

        // 添加新的属性用于控制按键按下的持续时间
        public int KeyPressInterval
        {
            get => _keyPressInterval;
            set
            {
                int validValue = Math.Max(MIN_KEY_PRESS_INTERVAL, value);
                if (_keyPressInterval != validValue)
                {
                    _keyPressInterval = validValue;
                    KeyPressIntervalChanged?.Invoke(this, validValue);
                    _logger.SequenceEvent($"按键按下时长已更新为: {validValue}ms (默认值: {DEFAULT_KEY_PRESS_INTERVAL}ms)");
                    
                    // 保存到配置
                    AppConfigService.Config.KeyPressInterval = validValue;
                    AppConfigService.SaveConfig();
                    
                    // 如果有当前模式，同步更新
                    _currentKeyMode?.SetKeyPressInterval(validValue);
                }
            }
        }

        // 添加重置方法，重置按键按下时长
        public void ResetKeyPressInterval()
        {
            KeyPressInterval = DEFAULT_KEY_PRESS_INTERVAL;
        }

        // 触发状态变化事件
        private void OnEnableStatusChanged(bool isEnabled)
        {
            EnableStatusChanged?.Invoke(this, isEnabled);
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