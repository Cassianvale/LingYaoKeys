using System.IO;
using System.Diagnostics;
using WpfApp.Services.Models;
using WpfApp.ViewModels;
using WpfApp.Services.Config;
using WpfApp.Services.Events;
using WpfApp.Services.Utils;

namespace WpfApp.Services.Core
{
    /// <summary>
    /// LyKeys服务类 - 提供键盘模拟和按键序列管理功能
    /// </summary>
    public class LyKeysService : IDisposable
    {
        #region 私有字段
        private LyKeys? _lyKeys;
        private readonly SerilogManager _logger;
        private bool _isInitialized;
        private bool _isEnabled;
        private bool _isHoldMode;
        internal readonly InputMethodService _inputMethodService;
        private readonly object _stateLock = new object();
        private readonly Stopwatch _sequenceStopwatch = new Stopwatch();
        private List<LyKeysCode> _keyList = new List<LyKeysCode>();
        private const int MIN_KEY_INTERVAL = 1;  // 最小按键间隔
        public const int DEFAULT_KEY_PRESS_INTERVAL = 5; // 默认按键按下时长
        private int _keyInterval = 5; // 按键间隔
        private int _keyPressInterval = DEFAULT_KEY_PRESS_INTERVAL; // 按键按下时长
        private bool _isDisposed;
        private CancellationTokenSource? _holdModeCts;
        private readonly Dictionary<int, LyKeysCode> _virtualKeyMap;
        private bool _isRapidFireEnabled; // 连发开关状态
        private volatile bool _emergencyStop;
        private const int EMERGENCY_STOP_THRESHOLD = 100; // 100ms内未能停止则强制停止
        private readonly object _emergencyStopLock = new object();
        #endregion

        #region 事件定义
        /// <summary>
        /// 初始化状态变更事件
        /// </summary>
        public event EventHandler<bool>? InitializationStatusChanged;

        /// <summary>
        /// 启用状态变更事件
        /// </summary>
        public event EventHandler<bool>? EnableStatusChanged;

        /// <summary>
        /// 按键间隔变更事件
        /// </summary>
        public event EventHandler<int>? KeyIntervalChanged;

        /// <summary>
        /// 状态消息变更事件
        /// </summary>
        public event EventHandler<StatusMessageEventArgs>? StatusMessageChanged;

        /// <summary>
        /// 按键按下间隔变更事件
        /// </summary>
        public event EventHandler<int>? KeyPressIntervalChanged;

        /// <summary>
        /// 模式切换事件
        /// </summary>
        public event EventHandler<bool>? ModeSwitched;
        #endregion

        #region 属性
        /// <summary>
        /// 获取服务是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 获取或设置服务是否启用
        /// </summary>
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
                        // 启动前保存输入法状态
                        _inputMethodService.StoreCurrentLayout();
                        _inputMethodService.SwitchToEnglish();
                        _logger.Debug("服务启用：已切换到英文输入法");

                        if (_isHoldMode)
                        {
                            StartHoldMode();
                        }
                        else
                        {
                            StartKeySequence();
                        }
                    }
                    else
                    {
                        if (_isHoldMode)
                        {
                            StopHoldMode();
                        }
                        else
                        {
                            StopKeySequence();
                        }

                        // 只在完全停止时恢复输入法
                        RestoreIME();
                        _logger.Debug("服务停用：已恢复输入法");
                    }
                }
            }
        }

        /// <summary>
        /// 获取或设置按键间隔
        /// </summary>
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
                }
            }
        }

        /// <summary>
        /// 获取或设置按键按下时长
        /// </summary>
        public int KeyPressInterval
        {
            get => _keyPressInterval;
            set
            {
                if (value >= 0 && _keyPressInterval != value)
                {
                    _keyPressInterval = value;
                    KeyPressIntervalChanged?.Invoke(this, value);
                    _logger.SequenceEvent($"按键按下时长已更新为: {value}ms");
                }
            }
        }

        /// <summary>
        /// 获取或设置是否为按压模式
        /// </summary>
        public bool IsHoldMode
        {
            get => _isHoldMode;
            set
            {
                if (_isHoldMode != value)
                {
                    bool wasEnabled = _isEnabled;
                    if (wasEnabled)
                    {
                        IsEnabled = false;
                    }

                    _isHoldMode = value;
                    ModeSwitched?.Invoke(this, value);

                    if (wasEnabled)
                    {
                        IsEnabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// 获取或设置连发功能是否启用
        /// </summary>
        public bool IsRapidFireEnabled
        {
            get => _isRapidFireEnabled;
            set
            {
                if (_isRapidFireEnabled != value)
                {
                    _isRapidFireEnabled = value;
                    // 当连发状态改变时，重新应用按键列表过滤
                    if (_keyList.Any())
                    {
                        var currentKeys = new List<LyKeysCode>(_keyList);
                        SetKeyList(currentKeys);
                    }
                }
            }
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 初始化LyKeys服务
        /// </summary>
        public LyKeysService()
        {
            _logger = SerilogManager.Instance;
            _isInitialized = false;
            _isEnabled = false;
            _isHoldMode = false;
            _virtualKeyMap = InitializeVirtualKeyMap();
            _inputMethodService = new InputMethodService();  // 初始化InputMethodService
            _logger.Debug("LyKeysService构造函数：已初始化InputMethodService");
        }
        #endregion

        #region 按键映射
        // 初始化虚拟键码映射
        private Dictionary<int, LyKeysCode> InitializeVirtualKeyMap()
        {
            var map = new Dictionary<int, LyKeysCode>();
            
            // 添加基本按键映射
            foreach (LyKeysCode code in Enum.GetValues(typeof(LyKeysCode)))
            {
                map[(int)code] = code;
            }

            // 添加特殊映射
            map[0x10] = LyKeysCode.VK_SHIFT;    // Shift
            map[0x11] = LyKeysCode.VK_CONTROL;  // Control
            map[0x12] = LyKeysCode.VK_MENU;     // Alt
            map[0x14] = LyKeysCode.VK_CAPITAL;  // Caps Lock
            map[0x1B] = LyKeysCode.VK_ESCAPE;   // Escape
            map[0x20] = LyKeysCode.VK_SPACE;    // Space
            map[0x2E] = LyKeysCode.VK_DELETE;   // Delete

            // 添加鼠标按键映射
            map[0x01] = LyKeysCode.VK_LBUTTON;  // 左键
            map[0x02] = LyKeysCode.VK_RBUTTON;  // 右键
            map[0x04] = LyKeysCode.VK_MBUTTON;  // 中键
            map[0x05] = LyKeysCode.VK_XBUTTON1; // 侧键1
            map[0x06] = LyKeysCode.VK_XBUTTON2; // 侧键2

            return map;
        }

        /// <summary>
        /// 将虚拟键码转换为LyKeys键码
        /// </summary>
        /// <param name="virtualKeyCode">虚拟键码</param>
        /// <returns>对应的LyKeys键码，如果没有对应的键码则返回null</returns>
        public LyKeysCode? GetLyKeysCode(int virtualKeyCode)
        {
            if (_virtualKeyMap.TryGetValue(virtualKeyCode, out LyKeysCode code))
            {
                return code;
            }
            return null;
        }

        /// <summary>
        /// 检查是否为有效的LyKeys键码
        /// </summary>
        /// <param name="code">要检查的键码</param>
        /// <returns>是否有效</returns>
        public bool IsValidLyKeysCode(LyKeysCode code)
        {
            return _virtualKeyMap.ContainsValue(code);
        }

        /// <summary>
        /// 获取所有支持的LyKeys键码
        /// </summary>
        /// <returns>支持的键码列表</returns>
        public IEnumerable<LyKeysCode> GetSupportedKeyCodes()
        {
            return _virtualKeyMap.Values.Distinct();
        }

        /// <summary>
        /// 获取键码的描述信息
        /// </summary>
        /// <param name="code">键码</param>
        /// <returns>描述信息</returns>
        public string GetKeyDescription(LyKeysCode code)
        {
            // 首先处理鼠标按键的特殊描述
            switch (code)
            {
                case LyKeysCode.VK_LBUTTON:
                    return "鼠标左键";
                case LyKeysCode.VK_RBUTTON:
                    return "鼠标右键";
                case LyKeysCode.VK_MBUTTON:
                    return "鼠标中键";
                case LyKeysCode.VK_XBUTTON1:
                    return "鼠标侧键1";
                case LyKeysCode.VK_XBUTTON2:
                    return "鼠标侧键2";
            }

            var field = typeof(LyKeysCode).GetField(code.ToString());
            if (field != null)
            {
                var attributes = (System.ComponentModel.DescriptionAttribute[])field.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
                if (attributes.Length > 0)
                {
                    return attributes[0].Description;
                }
            }
            return code.ToString();
        }
        #endregion

        #region 公共方法
        /// <summary>
        /// 初始化驱动
        /// </summary>
        /// <param name="driverPath">驱动文件路径</param>
        /// <returns>是否初始化成功</returns>
        public async Task<bool> InitializeAsync(string driverPath)
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.Warning("服务已经初始化");
                    return true;
                }

                _logger.Debug($"开始初始化LyKeys服务，驱动路径: {driverPath}");

                // 验证驱动文件
                if (!File.Exists(driverPath))
                {
                    _logger.Error($"驱动文件不存在: {driverPath}");
                    return false;
                }

                // 初始化驱动
                _lyKeys = new LyKeys(driverPath);
                if (!await _lyKeys.Initialize())
                {   
                    _logger.Error("注意：初始化已经失败，返回False");
                    return false; // 初始化失败返回false
                }
                
                _isInitialized = true;
                InitializationStatusChanged?.Invoke(this, true);
                SendStatusMessage("服务初始化成功");
                _logger.Debug("LyKeys服务初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("服务初始化异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 设置按键列表
        /// </summary>
        /// <param name="keyList">按键列表</param>
        public void SetKeyList(List<LyKeysCode> keyList)
        {
            try
            {
                if (keyList == null || keyList.Count == 0)
                {
                    _logger.Warning("收到空的按键列表");
                    if (_isEnabled)
                    {
                        IsEnabled = false;
                    }
                    _keyList.Clear();
                    return;
                }

                // 验证所有按键是否有效
                if (keyList.Any(k => !IsValidLyKeysCode(k)))
                {
                    _logger.Warning("按键列表包含无效的键码");
                    return;
                }

                // 获取所有按键项
                var keyItems = keyList.Select(k => new { Code = k, Item = GetKeyItem(k) }).ToList();

                // 根据连发状态过滤按键
                var filteredKeys = keyList;
                if (_isRapidFireEnabled)
                {
                    // 过滤掉连发按键，如果 KeyItem 为 null，则保留该按键
                    filteredKeys = keyItems
                        .Where(k => k.Item == null || !k.Item.IsKeyBurst)
                        .Select(k => k.Code)
                        .ToList();

                    _logger.Debug($"连发模式已启用，过滤后的按键数量: {filteredKeys.Count}, " +
                                $"过滤掉的连发按键数量: {keyList.Count - filteredKeys.Count}");
                }

                _keyList = new List<LyKeysCode>(filteredKeys);
                _logger.Debug($"按键列表已更新 - 按键数量: {_keyList.Count}");
            }
            catch (Exception ex)
            {
                _logger.Error("设置按键列表异常", ex);
                _keyList.Clear();
                IsEnabled = false;
            }
        }

        /// <summary>
        /// 获取按键对应的KeyItem
        /// </summary>
        private KeyItem? GetKeyItem(LyKeysCode keyCode)
        {
            try
            {
                // 通过反射获取主窗口实例
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow == null)
                {
                    return null;
                }

                var mainViewModel = mainWindow.DataContext as MainViewModel;
                if (mainViewModel == null)
                {
                    return null;
                }

                var keyMappingViewModel = mainViewModel.KeyMappingViewModel;
                if (keyMappingViewModel == null || keyMappingViewModel.IsInitializing)
                {
                    // 只在调试模式下输出日志
                    if (AppConfigService.Config.Debug.IsDebugMode)
                    {
                        _logger.Debug($"KeyMappingViewModel未初始化，跳过获取KeyItem: {keyCode}");
                    }
                    return null;
                }

                if (keyMappingViewModel.KeyList == null)
                {
                    return null;
                }

                return keyMappingViewModel.KeyList.FirstOrDefault(k => k?.KeyCode == keyCode);
            }
            catch (Exception ex)
            {
                // 只记录非初始化阶段的异常
                if (!IsInitializing())
                {
                    _logger.Debug($"获取KeyItem时发生异常: {keyCode}, 错误: {ex.Message}");
                }
                return null;
            }
        }

        /// <summary>
        /// 检查是否处于初始化阶段
        /// </summary>
        private bool IsInitializing()
        {
            try
            {
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow?.DataContext is MainViewModel mainViewModel &&
                    mainViewModel.KeyMappingViewModel != null)
                {
                    return mainViewModel.KeyMappingViewModel.IsInitializing;
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 模拟按键按下
        /// </summary>
        /// <param name="keyCode">按键代码</param>
        /// <returns>是否成功</returns>
        public bool SendKeyDown(LyKeysCode keyCode)
        {
            if (!CheckInitialization()) return false;
            if (!IsValidLyKeysCode(keyCode))
            {
                _logger.Error($"无效的键码: {keyCode}");
                return false;
            }

            try
            {
                // 检查是否为鼠标按键
                if (IsMouseButton(keyCode))
                {
                    return _lyKeys.SendMouseButton(ConvertToMouseButtonType(keyCode), true);
                }
                
                _lyKeys.SendKeyDown((ushort)keyCode);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"按键按下异常: {keyCode}", ex);
                return false;
            }
        }

        /// <summary>
        /// 模拟按键释放
        /// </summary>
        /// <param name="keyCode">按键代码</param>
        /// <returns>是否成功</returns>
        public bool SendKeyUp(LyKeysCode keyCode)
        {
            if (!CheckInitialization()) return false;
            if (!IsValidLyKeysCode(keyCode))
            {
                _logger.Error($"无效的键码: {keyCode}");
                return false;
            }

            try
            {
                // 检查是否为鼠标按键
                if (IsMouseButton(keyCode))
                {
                    return _lyKeys.SendMouseButton(ConvertToMouseButtonType(keyCode), false);
                }
                
                _lyKeys.SendKeyUp((ushort)keyCode);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"按键释放异常: {keyCode}", ex);
                return false;
            }
        }

        private bool IsMouseButton(LyKeysCode keyCode)
        {
            return keyCode == LyKeysCode.VK_LBUTTON ||
                   keyCode == LyKeysCode.VK_RBUTTON ||
                   keyCode == LyKeysCode.VK_MBUTTON ||
                   keyCode == LyKeysCode.VK_XBUTTON1 ||
                   keyCode == LyKeysCode.VK_XBUTTON2 ||
                   keyCode == LyKeysCode.VK_WHEELUP ||
                   keyCode == LyKeysCode.VK_WHEELDOWN;
        }

        private LyKeys.MouseButtonType ConvertToMouseButtonType(LyKeysCode keyCode)
        {
            return keyCode switch
            {
                LyKeysCode.VK_LBUTTON => LyKeys.MouseButtonType.Left,
                LyKeysCode.VK_RBUTTON => LyKeys.MouseButtonType.Right,
                LyKeysCode.VK_MBUTTON => LyKeys.MouseButtonType.Middle,
                LyKeysCode.VK_XBUTTON1 => LyKeys.MouseButtonType.XButton1,
                LyKeysCode.VK_XBUTTON2 => LyKeys.MouseButtonType.XButton2,
                LyKeysCode.VK_WHEELUP => LyKeys.MouseButtonType.WheelUp,
                LyKeysCode.VK_WHEELDOWN => LyKeys.MouseButtonType.WheelDown,
                _ => throw new ArgumentException($"非法的鼠标按键类型: {keyCode}")
            };
        }

        /// <summary>
        /// 模拟按键点击
        /// </summary>
        /// <param name="keyCode">按键代码</param>
        /// <param name="duration">按下持续时间(毫秒)</param>
        /// <returns>是否成功</returns>
        public bool SendKeyPress(LyKeysCode keyCode, int duration = 100)
        {
            if (!CheckInitialization()) return false;
            if (!IsValidLyKeysCode(keyCode))
            {
                _logger.Error($"无效的键码: {keyCode}");
                return false;
            }

            try
            {
                bool isMouseButton = IsMouseButton(keyCode);
                if (isMouseButton)
                {
                    _logger.Debug($"正在执行鼠标按键: {keyCode}, 持续时间: {duration}ms");
                }

                if (!SendKeyDown(keyCode))
                {
                    _logger.Error($"按键按下失败: {keyCode}");
                    return false;
                }
                
                Thread.Sleep(duration);
                
                bool result = SendKeyUp(keyCode);
                if (!result)
                {
                    _logger.Error($"按键释放失败: {keyCode}");
                }
                
                if (isMouseButton)
                {
                    _logger.Debug($"鼠标按键执行完成: {keyCode}, 结果: {result}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"按键点击异常: {keyCode}", ex);
                return false;
            }
        }

        /// <summary>
        /// 模拟组合键
        /// </summary>
        /// <param name="keyCodes">按键代码数组</param>
        public async Task SimulateKeyComboAsync(params LyKeysCode[] keyCodes)
        {
            if (!CheckInitialization()) return;
            if (keyCodes.Any(k => !IsValidLyKeysCode(k)))
            {
                _logger.Error("组合键包含无效的键码");
                return;
            }

            try
            {
                // 按下所有键
                foreach (var key in keyCodes)
                {
                    SendKeyDown(key);
                    await Task.Delay(5);
                }

                await Task.Delay(10);

                // 释放所有键（反序）
                for (int i = keyCodes.Length - 1; i >= 0; i--)
                {
                    SendKeyUp(keyCodes[i]);
                    await Task.Delay(5);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("模拟组合键异常", ex);
                // 确保释放所有按键
                foreach (var key in keyCodes)
                {
                    SendKeyUp(key);
                }
            }
        }
        #endregion

        #region 私有方法
        private bool CheckInitialization()
        {
            if (!_isInitialized)
            {
                _logger.Error("服务未初始化");
                return false;
            }
            return true;
        }

        private void StartKeySequence()
        {
            try
            {
                if (!CheckInitialization()) return;

                _logger.Debug("开始启动按键序列");
                
                // 重置紧急停止标志
                lock (_emergencyStopLock)
                {
                    _emergencyStop = false;
                }
                
                _sequenceStopwatch.Restart();

                if (_keyList.Count > 0)
                {
                    _logger.Debug($"按键列表数量: {_keyList.Count}, 间隔: {_keyInterval}ms");
                    // 在新线程中启动按键序列
                    Thread sequenceThread = new Thread(ExecuteKeySequence) { IsBackground = true };
                    sequenceThread.Start();
                    _logger.Debug("按键序列线程已启动");
                }
                else
                {
                    _logger.Warning("按键列表为空，无法启动序列");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("启动按键序列异常", ex);
                StopKeySequence();
            }
        }

        private void StopKeySequence()
        {
            try
            {
                _sequenceStopwatch.Stop();

                // 确保释放所有可能按下的按键
                foreach (var key in _keyList)
                {
                    SendKeyUp(key);
                }

                // 恢复输入法
                RestoreIME();
                _logger.Debug("按键序列已停止，输入法已恢复");
            }
            catch (Exception ex)
            {
                _logger.Error("停止按键序列异常", ex);
                ForceStop();
            }
        }

        // 新增：紧急停止方法，只在窗口切换时调用
        public void EmergencyStop()
        {
            try
            {
                _logger.Debug("开始执行紧急停止");
                
                // 设置紧急停止标志
                lock (_emergencyStopLock)
                {
                    _emergencyStop = true;
                }

                // 使用计时器确保在阈值时间内停止
                var stopTimer = new System.Timers.Timer(EMERGENCY_STOP_THRESHOLD);
                stopTimer.Elapsed += (s, e) =>
                {
                    try
                    {
                        if (_isEnabled)
                        {
                            _logger.Warning("检测到按键未能及时停止，强制停止");
                            ForceStop();
                        }
                        ((System.Timers.Timer)s).Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("紧急停止时发生异常", ex);
                    }
                };
                stopTimer.Start();

                // 确保释放所有可能按下的按键
                foreach (var key in _keyList)
                {
                    SendKeyUp(key);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("紧急停止异常", ex);
                ForceStop();
            }
        }

        private void ExecuteKeySequence()
        {
            _logger.Debug("开始执行按键序列");
            
            var spinWait = new SpinWait();
            var stopwatch = new Stopwatch();

            while (_isEnabled && !_isHoldMode)
            {
                try
                {
                    // 检查紧急停止标志
                    if (_emergencyStop)
                    {
                        _logger.Debug("检测到紧急停止标志，终止按键序列");
                        break;
                    }

                    foreach (var key in _keyList)
                    {
                        if (!_isEnabled || _isHoldMode || _emergencyStop)
                        {
                            _logger.Debug("检测到停止信号，中断按键序列");
                            return;
                        }

                        stopwatch.Restart();
                        SendKeyPress(key, _keyPressInterval);

                        // 计算剩余等待时间
                        var elapsedMs = stopwatch.ElapsedMilliseconds;
                        var remainingDelay = Math.Max(0, _keyInterval - elapsedMs);

                        if (remainingDelay > 0)
                        {
                            // 对于短延迟使用自旋等待
                            if (remainingDelay <= 2)
                            {
                                while (stopwatch.ElapsedMilliseconds < _keyInterval)
                                {
                                    if (!_isEnabled || _isHoldMode || _emergencyStop)
                                    {
                                        return;
                                    }
                                    spinWait.SpinOnce();
                                }
                            }
                            else
                            {
                                // 分段休眠，以提高响应性
                                var segments = Math.Max(1, remainingDelay / 10);
                                var segmentDelay = remainingDelay / segments;
                                
                                for (int i = 0; i < segments; i++)
                                {
                                    if (!_isEnabled || _isHoldMode || _emergencyStop)
                                    {
                                        return;
                                    }
                                    Thread.Sleep((int)segmentDelay);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("执行按键序列异常", ex);
                    IsEnabled = false;
                    break;
                }
            }
        }

        private void StartHoldMode()
        {
            try
            {
                if (!CheckInitialization()) return;

                StopHoldMode();

                // 重置紧急停止标志
                lock (_emergencyStopLock)
                {
                    _emergencyStop = false;
                    _logger.Debug("已重置紧急停止标志");
                }

                lock (_stateLock)
                {
                    if (_keyList.Count > 0)
                    {
                        _holdModeCts = new CancellationTokenSource();
                        // 在新线程中启动按压模式
                        Task.Run(ExecuteHoldMode, _holdModeCts.Token);
                        _logger.Debug($"按压模式已启动，按键数量: {_keyList.Count}");
                    }
                    else
                    {
                        _logger.Warning("按键列表为空，无法启动按压模式");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("启动按压模式异常", ex);
                StopHoldMode();
            }
        }

        private void StopHoldMode()
        {
            try
            {
                CancellationTokenSource? cts = null;
                lock (_stateLock)
                {
                    cts = _holdModeCts;
                    _holdModeCts = null;
                }

                if (cts != null)
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("取消按压模式异常", ex);
                    }
                }

                // 确保释放所有可能按下的按键
                if (_keyList != null)
                {
                    foreach (var key in _keyList)
                    {
                        try
                        {
                            SendKeyUp(key);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"释放按键异常: {key}", ex);
                        }
                    }
                }

                _logger.Debug("按压模式已停止");
            }
            catch (Exception ex)
            {
                _logger.Error("停止按压模式异常", ex);
            }
        }

        private async Task ExecuteHoldMode()
        {
            CancellationToken token;
            List<LyKeysCode> keyListSnapshot;

            lock (_stateLock)
            {
                if (_holdModeCts == null) return;
                token = _holdModeCts.Token;
                
                if (_keyList == null || _keyList.Count == 0)
                {
                    _logger.Warning("按键列表为空");
                    return;
                }
                keyListSnapshot = new List<LyKeysCode>(_keyList);
            }

            try
            {
                _logger.Debug($"开始执行按压模式循环，按键数量: {keyListSnapshot.Count}");

                int currentIndex = 0;
                var stopwatch = new Stopwatch();
                var spinWait = new SpinWait();

                while (!token.IsCancellationRequested && _isEnabled && _isHoldMode)
                {
                    // 检查紧急停止标志
                    lock (_emergencyStopLock)
                    {
                        if (_emergencyStop)
                        {
                            _logger.Debug("检测到紧急停止标志，终止按压模式循环");
                            return;
                        }
                    }

                    if (currentIndex >= keyListSnapshot.Count)
                    {
                        currentIndex = 0;
                    }

                    stopwatch.Restart();
                    var key = keyListSnapshot[currentIndex];
                    
                    try
                    {
                        // 执行按键操作
                        SendKeyPress(key, _keyPressInterval);
                        _logger.Debug($"按压模式 - 执行按键: {key}, 按下时长: {_keyPressInterval}ms");
                        
                        // 计算剩余等待时间
                        var elapsedMs = stopwatch.ElapsedMilliseconds;
                        var remainingDelay = Math.Max(0, _keyInterval - elapsedMs);
                        
                        if (remainingDelay > 0)
                        {
                            // 对于短延迟使用自旋等待
                            if (remainingDelay <= 2)
                            {
                                while (stopwatch.ElapsedMilliseconds < _keyInterval)
                                {
                                    if (token.IsCancellationRequested || !_isEnabled || !_isHoldMode)
                                    {
                                        return;
                                    }
                                    spinWait.SpinOnce();
                                }
                            }
                            else
                            {
                                // 分段休眠，以提高响应性
                                var segments = Math.Max(1, remainingDelay / 10);
                                var segmentDelay = remainingDelay / segments;
                                
                                for (int i = 0; i < segments; i++)
                                {
                                    if (token.IsCancellationRequested || !_isEnabled || !_isHoldMode)
                                    {
                                        return;
                                    }
                                    await Task.Delay((int)segmentDelay, token);
                                }
                            }
                        }
                        
                        currentIndex = (currentIndex + 1) % keyListSnapshot.Count;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"按压模式执行按键异常: {key}", ex);
                        if (token.IsCancellationRequested || !_isEnabled || !_isHoldMode)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("执行按压模式异常", ex);
            }
            finally
            {
                try
                {
                    // 确保释放所有按键
                    foreach (var key in keyListSnapshot)
                    {
                        try
                        {
                            SendKeyUp(key);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"释放按键异常: {key}", ex);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("按压模式：释放按键时发生异常", ex);
                }
                _logger.Debug("按压模式循环已结束");
            }
        }

        private void SendStatusMessage(string message, bool isError = false)
        {
            var args = new StatusMessageEventArgs(message, isError);
            StatusMessageChanged?.Invoke(this, args);
            if (isError)
                _logger.Error(message);
            else
                _logger.Debug(message);
        }

        private void ForceStop()
        {
            try
            {
                _isEnabled = false;
                _isHoldMode = false;
                
                // 确保所有按键都被释放
                if (_keyList != null)
                {
                    foreach (var key in _keyList)
                    {
                        try
                        {
                            SendKeyUp(key);
                            Thread.Sleep(1); // 给予系统短暂时间处理按键释放
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"强制释放按键时发生异常: {key}", ex);
                        }
                    }
                }

                // 重置所有状态
                _emergencyStop = false;
                EnableStatusChanged?.Invoke(this, false);
                
                // 恢复输入法
                RestoreIME();
                _logger.Debug("已强制停止所有按键操作，输入法已恢复");
            }
            catch (Exception ex)
            {
                _logger.Error("强制停止时发生异常", ex);
            }
        }
        #endregion

        #region IDisposable实现
        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                IsEnabled = false;
                StopKeySequence();
                StopHoldMode();
                _lyKeys?.Dispose();
                _isInitialized = false;
                _isDisposed = true;
            }
            catch (Exception ex)
            {
                _logger.Error("释放资源异常", ex);
            }
        }
        #endregion

        #region 输入法管理
        /// <summary>
        /// 恢复输入法到之前的状态
        /// </summary>
        public void RestoreIME()
        {
            try
            {
                _inputMethodService.RestorePreviousLayout();
                _logger.Debug("已恢复原始输入法");
            }
            catch (Exception ex)
            {
                _logger.Error("恢复输入法失败", ex);
            }
        }
        #endregion
    }
} 