using System.IO;
using System.Diagnostics;
using WpfApp.Services.Models;
using WpfApp.ViewModels;
using WpfApp.Services.Config;
using WpfApp.Services.Events;
using WpfApp.Services.Utils;
using System.Text;

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
        private volatile bool _emergencyStop;
        private const int EMERGENCY_STOP_THRESHOLD = 100; // 100ms内未能停止则强制停止
        private readonly object _emergencyStopLock = new object();
        private bool _autoSwitchIME = true; // 是否自动切换输入法
        // 存储每个按键的间隔信息
        private Dictionary<LyKeysCode, int> _keyIntervals = new Dictionary<LyKeysCode, int>();
        // 添加初始化标志
        private bool _isGettingKeyItem = false;
        // 添加缓存字典，用于存储按键配置
        private Dictionary<LyKeysCode, KeyItem> _keyItemCache = new Dictionary<LyKeysCode, KeyItem>();
        // 缓存过期时间戳
        private DateTime _cacheExpirationTime = DateTime.MinValue;
        // 是否正在执行SetKeyList
        private bool _isSettingKeyList = false;
        // 添加坐标列表
        private List<(int X, int Y, int Interval)> _coordinatesList = new List<(int X, int Y, int Interval)>();
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
                        // 根据设置决定是否切换输入法
                        if (_autoSwitchIME)
                        {
                            // 启动前保存输入法状态
                            _inputMethodService.StoreCurrentLayout();
                            _inputMethodService.SwitchToEnglish();
                            _logger.Debug("服务启用：已切换到英文输入法");
                        }
                        else
                        {
                            _logger.Debug("服务启用：保持当前输入法不变");
                        }

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
            
            // 从配置中读取是否自动切换输入法
            try
            {
                var config = AppConfigService.Config;
                _autoSwitchIME = config.AutoSwitchToEnglishIME ?? true;
                _logger.Debug($"LyKeysService构造函数：输入法自动切换设置为 {(_autoSwitchIME ? "开启" : "关闭")}");
            }
            catch (Exception ex)
            {
                _logger.Error("读取输入法切换配置失败，使用默认值(开启)", ex);
                _autoSwitchIME = true;
            }
            
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
                // 防循环调用保护
                if (_isSettingKeyList)
                {
                    _logger.Warning("检测到SetKeyList正在执行中，跳过重复调用");
                    return;
                }
                
                _isSettingKeyList = true;
                
                // 验证输入
                if (keyList == null || keyList.Count == 0)
                {
                    _logger.Warning("收到空的按键列表");
                    if (_isEnabled) IsEnabled = false;
                    _keyList.Clear();
                    return;
                }

                // 验证按键有效性
                if (keyList.Any(k => !IsValidLyKeysCode(k)))
                {
                    _logger.Warning("按键列表包含无效的键码");
                    return;
                }

                // 清理不再需要的按键缓存
                CleanupUnusedKeyCaches(keyList);
                
                // 处理按键间隔设置
                ProcessKeyIntervals(keyList);

                // 更新按键列表
                _keyList = keyList.ToList();
                _logger.Debug($"按键列表已更新 - 按键数量: {_keyList.Count}, 间隔信息已存储: {_keyIntervals.Count}个");
            }
            catch (Exception ex)
            {
                _logger.Error("设置按键列表异常", ex);
                _keyList.Clear();
                IsEnabled = false;
            }
            finally
            {
                _isSettingKeyList = false;
            }
        }

        /// <summary>
        /// 清理不再使用的按键缓存
        /// </summary>
        private void CleanupUnusedKeyCaches(List<LyKeysCode> activeKeys)
        {
            var keysToRemove = _keyIntervals.Keys
                .Where(cachedKey => !activeKeys.Contains(cachedKey))
                .ToList();
                
            foreach (var keyToRemove in keysToRemove)
            {
                _keyIntervals.Remove(keyToRemove);
                _keyItemCache.Remove(keyToRemove);
            }
            
            _cacheExpirationTime = DateTime.MinValue;
        }

        /// <summary>
        /// 处理按键间隔设置
        /// </summary>
        private void ProcessKeyIntervals(List<LyKeysCode> keyList)
        {
            bool isInitPhase = IsInitializing();
            var intervalLog = new StringBuilder();
            intervalLog.AppendLine(isInitPhase
                ? $"初始化阶段设置按键列表 - 按键数量: {keyList.Count}，保留已设置的间隔"
                : $"按键列表详情 [{keyList.Count}个]:");
            
            foreach (var code in keyList)
            {
                if (!_keyIntervals.ContainsKey(code))
                {
                    // 按键没有缓存的间隔值
                    if (!isInitPhase)
                    {
                        // 非初始化阶段：尝试从KeyItem获取间隔
                        var item = GetKeyItem(code);
                        if (item != null)
                        {
                            _keyIntervals[code] = item.KeyInterval;
                            intervalLog.AppendLine($"  按键: {code}, 间隔: {item.KeyInterval}ms");
                        }
                        else
                        {
                            _keyIntervals[code] = _keyInterval;
                            intervalLog.AppendLine($"  按键: {code}, KeyItem为空，使用默认间隔: {_keyInterval}ms");
                        }
                    }
                    else
                    {
                        // 初始化阶段：使用默认间隔
                        _keyIntervals[code] = _keyInterval;
                        intervalLog.AppendLine($"  按键: {code}, 未设置间隔，使用默认间隔: {_keyInterval}ms");
                    }
                }
                else
                {
                    // 已有缓存的间隔值，保留
                    intervalLog.AppendLine($"  按键: {code}, 保留已设置间隔: {_keyIntervals[code]}ms");
                }
            }
            
            _logger.Debug(intervalLog.ToString());
        }

        /// <summary>
        /// 获取按键对应的KeyItem
        /// </summary>
        private KeyItem? GetKeyItem(LyKeysCode keyCode)
        {
            // 防止循环调用
            if (_isGettingKeyItem)
            {
                return null;
            }
            
            _isGettingKeyItem = true;
            
            try
            {
                // 首先检查缓存是否有效
                if (_keyItemCache.ContainsKey(keyCode) && DateTime.Now < _cacheExpirationTime)
                {
                    var cachedItem = _keyItemCache[keyCode];
                    _logger.Debug($"[GetKeyItem] 从缓存获取按键{keyCode}的KeyItem, 间隔值: {cachedItem?.KeyInterval ?? _keyInterval}ms");
                    _isGettingKeyItem = false;
                    return cachedItem;
                }
                
                // 检查是否处于初始化阶段
                if (IsInitializing())
                {
                    _isGettingKeyItem = false;
                    return null;
                }
                
                // 通过反射获取主窗口实例
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow == null)
                {
                    _logger.Debug($"[GetKeyItem] 主窗口为空");
                    _isGettingKeyItem = false;
                    return null;
                }

                var mainViewModel = mainWindow.DataContext as MainViewModel;
                if (mainViewModel == null)
                {
                    _logger.Debug($"[GetKeyItem] MainViewModel为空");
                    _isGettingKeyItem = false;
                    return null;
                }

                var keyMappingViewModel = mainViewModel.KeyMappingViewModel;
                if (keyMappingViewModel == null || keyMappingViewModel.IsInitializing)
                {
                    // 只在调试模式下输出日志
                    if (AppConfigService.Config.Debug.IsDebugMode)
                    {
                        _logger.Debug($"[GetKeyItem] KeyMappingViewModel未初始化，跳过获取KeyItem: {keyCode}");
                    }
                    _isGettingKeyItem = false;
                    return null;
                }

                if (keyMappingViewModel.KeyList == null)
                {
                    _logger.Debug($"[GetKeyItem] KeyList为空");
                    _isGettingKeyItem = false;
                    return null;
                }

                var keyItem = keyMappingViewModel.KeyList.FirstOrDefault(k => k?.KeyCode == keyCode);
                
                // 更新缓存
                if (keyItem != null)
                {
                    _keyItemCache[keyCode] = keyItem;
                    // 设置缓存过期时间为5秒
                    _cacheExpirationTime = DateTime.Now.AddSeconds(5);
                    _logger.Debug($"[GetKeyItem] 找到按键{keyCode}的KeyItem, 间隔值: {keyItem.KeyInterval}ms，已缓存");
                }
                else
                {
                    if (!IsInitializing())
                    {
                        _logger.Debug($"[GetKeyItem] 未找到按键{keyCode}的KeyItem");
                    }
                }
                
                return keyItem;
            }
            catch (Exception ex)
            {
                // 只记录非初始化阶段的异常
                if (!IsInitializing())
                {
                    _logger.Debug($"[GetKeyItem] 获取KeyItem时发生异常: {keyCode}, 错误: {ex.Message}");
                }
                return null;
            }
            finally
            {
                _isGettingKeyItem = false;
            }
        }

        /// <summary>
        /// 检查是否处于初始化阶段
        /// </summary>
        private bool IsInitializing()
        {
            try
            {
                // 检查Application是否已经初始化
                if (System.Windows.Application.Current == null)
                {
                    return true;
                }
                
                // 检查主窗口是否存在
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow == null)
                {
                    return true;
                }
                
                // 检查MainViewModel是否存在
                if (!(mainWindow.DataContext is MainViewModel mainViewModel))
                {
                    return true;
                }
                
                // 检查KeyMappingViewModel是否存在和是否正在初始化
                if (mainViewModel.KeyMappingViewModel == null || 
                    mainViewModel.KeyMappingViewModel.IsInitializing)
                {
                    return true;
                }
                
                return false;
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

        /// <summary>
        /// 为特定按键设置独立间隔
        /// </summary>
        /// <param name="keyCode">需要设置间隔的按键代码</param>
        /// <param name="interval">间隔值(毫秒)</param>
        public void SetKeyIntervalForKey(LyKeysCode keyCode, int interval)
        {
            try
            {
                // 验证按键是否有效
                if (!IsValidLyKeysCode(keyCode))
                {
                    _logger.Warning($"无效的按键代码：{keyCode}，无法设置间隔");
                    return;
                }

                // 确保间隔值合法
                int validInterval = Math.Max(MIN_KEY_INTERVAL, interval);
                
                // 直接更新缓存
                _keyIntervals[keyCode] = validInterval;
                
                // 同时更新KeyItem缓存
                if (_keyItemCache.TryGetValue(keyCode, out KeyItem? item) && item != null)
                {
                    item.KeyInterval = validInterval;
                }
                
                _logger.Debug($"已设置按键 {keyCode} 的间隔为 {validInterval}ms");
            }
            catch (Exception ex)
            {
                _logger.Error($"设置按键 {keyCode} 间隔时发生异常", ex);
            }
        }

        /// <summary>
        /// 为特定坐标设置独立间隔
        /// </summary>
        /// <param name="x">坐标X位置</param>
        /// <param name="y">坐标Y位置</param>
        /// <param name="interval">间隔值(毫秒)</param>
        public void SetCoordinateInterval(int x, int y, int interval)
        {
            try
            {
                // 确保间隔值合法
                int validInterval = Math.Max(MIN_KEY_INTERVAL, interval);
                
                // 查找并更新坐标的间隔
                bool found = false;
                
                for (int i = 0; i < _coordinatesList.Count; i++)
                {
                    var coord = _coordinatesList[i];
                    if (coord.X == x && coord.Y == y)
                    {
                        // 更新坐标间隔
                        _coordinatesList[i] = (coord.X, coord.Y, validInterval);
                        found = true;
                        _logger.Debug($"已更新坐标 ({x}, {y}) 的间隔为 {validInterval}ms");
                        break;
                    }
                }
                
                if (!found && x != 0 && y != 0) // 避免添加无效坐标
                {
                    // 如果未找到匹配坐标但应用正在运行，可以添加新坐标
                    _coordinatesList.Add((x, y, validInterval));
                    _logger.Debug($"已添加新坐标 ({x}, {y}) 的间隔为 {validInterval}ms");
                }
                
                if (!found && !_isInitialized)
                {
                    _logger.Warning($"未找到要更新间隔的坐标 ({x}, {y})");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"设置坐标 ({x}, {y}) 间隔时发生异常", ex);
            }
        }

        /// <summary>
        /// 移动鼠标到指定的绝对坐标位置
        /// </summary>
        /// <param name="x">屏幕X坐标</param>
        /// <param name="y">屏幕Y坐标</param>
        /// <returns>操作是否成功</returns>
        public bool MoveMouseToPosition(int x, int y)
        {
            if (!CheckInitialization())
            {
                _logger.Error($"鼠标移动失败：驱动未初始化，坐标: ({x}, {y})");
                return false;
            }

            try
            {
                if (_lyKeys != null)
                {
                    _logger.Debug($"移动鼠标到坐标: ({x}, {y})");
                    return _lyKeys.MoveMouseAbsolute(x, y);
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动鼠标到坐标({x}, {y})失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 设置按键和坐标列表，支持混合操作
        /// </summary>
        /// <param name="keyboard">键盘按键列表</param>
        /// <param name="coordinates">坐标列表</param>
        public void SetKeyItemsListWithCoordinates(List<LyKeysCode> keyboard, List<(int X, int Y, int Interval)> coordinates)
        {
            try
            {
                // 防循环调用保护
                if (_isSettingKeyList)
                {
                    _logger.Warning("检测到SetKeyItemsListWithCoordinates正在执行中，跳过重复调用");
                    return;
                }
                
                _isSettingKeyList = true;
                
                // 验证键盘按键输入
                if (keyboard == null)
                {
                    keyboard = new List<LyKeysCode>();
                }

                // 验证坐标输入
                if (coordinates == null)
                {
                    coordinates = new List<(int X, int Y, int Interval)>();
                }

                // 验证按键列表为空的情况
                if (keyboard.Count == 0 && coordinates.Count == 0)
                {
                    _logger.Warning("按键和坐标列表均为空");
                    if (_isEnabled) IsEnabled = false;
                    _keyList.Clear();
                    _coordinatesList.Clear();
                    return;
                }

                // 验证按键有效性
                if (keyboard.Any(k => !IsValidLyKeysCode(k)))
                {
                    _logger.Warning("按键列表包含无效的键码");
                    return;
                }

                // 清理不再需要的按键缓存
                CleanupUnusedKeyCaches(keyboard);
                
                // 处理按键间隔设置
                ProcessKeyIntervals(keyboard);

                // 更新键盘按键列表
                _keyList = keyboard.ToList();
                
                // 更新坐标列表
                _coordinatesList = coordinates.ToList();
                
                _logger.Debug($"完整按键列表已更新 - 键盘按键数量: {_keyList.Count}, 坐标点数量: {_coordinatesList.Count}");
            }
            catch (Exception ex)
            {
                _logger.Error("设置按键和坐标列表异常", ex);
                _keyList.Clear();
                _coordinatesList.Clear();
                IsEnabled = false;
            }
            finally
            {
                _isSettingKeyList = false;
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

                // 同时检查键盘按键和坐标列表
                bool hasKeyboardKeys = _keyList.Count > 0;
                bool hasCoordinates = _coordinatesList.Count > 0;
                
                if (hasKeyboardKeys || hasCoordinates)
                {
                    _logger.Debug($"准备执行序列 - 键盘按键: {_keyList.Count}, 坐标点: {_coordinatesList.Count}, 基础间隔: {_keyInterval}ms");
                    // 在新线程中启动按键序列
                    Thread sequenceThread = new Thread(ExecuteKeySequence) { IsBackground = true };
                    sequenceThread.Start();
                    _logger.Debug("按键序列线程已启动");
                }
                else
                {
                    _logger.Warning("按键和坐标列表均为空，无法启动序列");
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

                    // 遍历键盘按键列表
                    foreach (var key in _keyList)
                    {
                        if (!_isEnabled || _isHoldMode || _emergencyStop)
                        {
                            _logger.Debug("检测到停止信号，中断按键序列");
                            return;
                        }

                        // 使用异步方法的同步等待版本
                        ExecuteSingleKeyWithDelayAsync(key, _keyPressInterval, stopwatch, spinWait, 
                            () => !_isEnabled || _isHoldMode || _emergencyStop,
                            "顺序模式", CancellationToken.None).GetAwaiter().GetResult();
                    }
                    
                    // 遍历坐标列表
                    foreach (var coord in _coordinatesList)
                    {
                        if (!_isEnabled || _isHoldMode || _emergencyStop)
                        {
                            _logger.Debug("检测到停止信号，中断坐标序列");
                            return;
                        }
                        
                        // 使用异步方法的同步等待版本
                        ExecuteCoordinateWithDelayAsync(coord.X, coord.Y, coord.Interval, stopwatch, spinWait,
                            () => !_isEnabled || _isHoldMode || _emergencyStop,
                            "顺序模式", CancellationToken.None).GetAwaiter().GetResult();
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

        /// <summary>
        /// 执行单个按键并等待指定间隔（异步版本）
        /// </summary>
        private async Task<bool> ExecuteSingleKeyWithDelayAsync(
            LyKeysCode key, 
            int keyPressInterval,
            Stopwatch stopwatch, 
            SpinWait spinWait,
            Func<bool> shouldStopFunc,
            string modeDescription,
            CancellationToken token)
        {
            stopwatch.Restart();
            int keyInterval = GetKeyInterval(key);
            
            // 发送按键
            SendKeyPress(key, keyPressInterval);
            _logger.Debug($"{modeDescription} - 执行按键: {key}, 按下时长: {keyPressInterval}ms, 使用间隔: {keyInterval}ms");
            
            // 计算并等待剩余延迟时间
            return await WaitRemainingDelayAsync(keyInterval, stopwatch, spinWait, shouldStopFunc, token);
        }

        /// <summary>
        /// 等待剩余延迟时间（异步版本）
        /// </summary>
        private async Task<bool> WaitRemainingDelayAsync(
            int targetDelay, 
            Stopwatch stopwatch, 
            SpinWait spinWait, 
            Func<bool> shouldStopFunc,
            CancellationToken token)
        {
            var elapsedMs = stopwatch.ElapsedMilliseconds;
            var remainingDelay = Math.Max(0, targetDelay - elapsedMs);
            
            if (remainingDelay <= 0) return true;
            
            // 对于短延迟使用自旋等待
            if (remainingDelay <= 2)
            {
                while (stopwatch.ElapsedMilliseconds < targetDelay)
                {
                    if (shouldStopFunc())
                    {
                        return false;
                    }
                    spinWait.SpinOnce();
                }
                return true;
            }
            
            // 分段休眠，以提高响应性
            var segments = Math.Max(1, remainingDelay / 10);
            var segmentDelay = remainingDelay / segments;
            
            for (int i = 0; i < segments; i++)
            {
                if (shouldStopFunc())
                {
                    return false;
                }
                await Task.Delay((int)segmentDelay, token);
            }
            
            return true;
        }

        /// <summary>
        /// 执行鼠标移动到指定坐标并等待指定间隔（异步版本）
        /// </summary>
        private async Task<bool> ExecuteCoordinateWithDelayAsync(
            int x,
            int y,
            int interval,
            Stopwatch stopwatch,
            SpinWait spinWait,
            Func<bool> shouldStopFunc,
            string modeDescription,
            CancellationToken token)
        {
            stopwatch.Restart();
            
            // 移动鼠标到指定坐标
            MoveMouseToPosition(x, y);
            _logger.Debug($"{modeDescription} - 移动鼠标到坐标: ({x}, {y}), 使用间隔: {interval}ms");
            
            // 计算并等待剩余延迟时间
            return await WaitRemainingDelayAsync(interval, stopwatch, spinWait, shouldStopFunc, token);
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
        /// 设置是否自动切换输入法
        /// </summary>
        /// <param name="autoSwitch">是否自动切换</param>
        public void SetAutoSwitchIME(bool autoSwitch)
        {
            _autoSwitchIME = autoSwitch;
            _logger.Debug($"输入法自动切换设置已更新: {(autoSwitch ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 恢复输入法到之前的状态
        /// </summary>
        public void RestoreIME()
        {
            try
            {
                // 只有在自动切换输入法开启时才恢复
                if (_autoSwitchIME)
                {
                    _inputMethodService.RestorePreviousLayout();
                    _logger.Debug("已恢复原始输入法");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("恢复输入法失败", ex);
            }
        }
        #endregion

        /// <summary>
        /// 获取按键的独立间隔，如果没有找到则使用默认间隔
        /// </summary>
        public int GetKeyInterval(LyKeysCode keyCode)
        {
            // 首先尝试从缓存字典中获取间隔
            if (_keyIntervals.TryGetValue(keyCode, out int interval))
            {
                // 只在调试模式记录此日志，减少冗余日志
                if (AppConfigService.Config.Debug.IsDebugMode && !IsInitializing())
                {
                    _logger.Debug($"从缓存中获取按键{keyCode}的间隔: {interval}ms");
                }
                return interval;
            }
            
            // 如果缓存中没有且不在初始化阶段，尝试从KeyItem获取间隔
            if (!IsInitializing())
            {
                var keyItem = GetKeyItem(keyCode);
                if (keyItem != null)
                {
                    interval = keyItem.KeyInterval;
                    // 更新缓存
                    _keyIntervals[keyCode] = interval;
                    _logger.Debug($"已找到按键{keyCode}的KeyItem，使用独立间隔: {interval}ms");
                    return interval;
                }
            }
            
            // 使用默认间隔
            _logger.Debug($"未找到按键{keyCode}的间隔信息，使用默认间隔: {_keyInterval}ms");
            // 更新缓存以避免频繁查询
            _keyIntervals[keyCode] = _keyInterval;
            return _keyInterval;
        }

        /// <summary>
        /// 启动按压模式
        /// </summary>
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
                    // 同时检查键盘按键和坐标列表
                    bool hasKeyboardKeys = _keyList != null && _keyList.Count > 0;
                    bool hasCoordinates = _coordinatesList != null && _coordinatesList.Count > 0;
                    
                    if (hasKeyboardKeys || hasCoordinates)
                    {
                        _holdModeCts = new CancellationTokenSource();
                        // 在新线程中启动按压模式
                        Task.Run(ExecuteHoldMode, _holdModeCts.Token);
                        _logger.Debug($"按压模式已启动 - 键盘按键: {_keyList.Count}, 坐标点: {_coordinatesList.Count}");
                    }
                    else
                    {
                        _logger.Warning("按键和坐标列表均为空，无法启动按压模式");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("启动按压模式异常", ex);
                StopHoldMode();
            }
        }

        /// <summary>
        /// 停止按压模式
        /// </summary>
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

        /// <summary>
        /// 执行按压模式循环
        /// </summary>
        private async Task ExecuteHoldMode()
        {
            CancellationToken token;
            List<LyKeysCode> keyListSnapshot;
            List<(int X, int Y, int Interval)> coordListSnapshot;

            lock (_stateLock)
            {
                if (_holdModeCts == null) return;
                token = _holdModeCts.Token;
                
                // 检查按键和坐标列表
                bool hasKeyboard = _keyList != null && _keyList.Count > 0;
                bool hasCoordinates = _coordinatesList != null && _coordinatesList.Count > 0;
                
                if (!hasKeyboard && !hasCoordinates)
                {
                    _logger.Warning("按键和坐标列表均为空");
                    return;
                }
                
                // 创建快照
                keyListSnapshot = new List<LyKeysCode>(_keyList);
                coordListSnapshot = new List<(int X, int Y, int Interval)>(_coordinatesList);
                
                _logger.Debug($"已创建执行快照 - 键盘按键: {keyListSnapshot.Count}, 坐标点: {coordListSnapshot.Count}");
            }

            try
            {
                _logger.Debug($"开始执行按压模式循环，总操作数: {keyListSnapshot.Count + coordListSnapshot.Count}");

                int currentKeyIndex = 0;
                int currentCoordIndex = 0;
                bool processingKeyboard = keyListSnapshot.Count > 0; // 如果有键盘按键，先处理键盘
                
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

                    try
                    {
                        if (processingKeyboard && keyListSnapshot.Count > 0)
                        {
                            // 处理键盘按键
                            if (currentKeyIndex >= keyListSnapshot.Count)
                            {
                                // 如果还有坐标点，切换到处理坐标
                                if (coordListSnapshot.Count > 0)
                                {
                                    processingKeyboard = false;
                                    currentCoordIndex = 0;
                                    continue;
                                }
                                else
                                {
                                    // 否则重新开始处理键盘
                                    currentKeyIndex = 0;
                                }
                            }

                            var key = keyListSnapshot[currentKeyIndex];
                            
                        // 执行单个按键并等待
                        await ExecuteSingleKeyWithDelayAsync(key, _keyPressInterval, stopwatch, spinWait, 
                            () => token.IsCancellationRequested || !_isEnabled || !_isHoldMode,
                            "按压模式", token);
                        
                            currentKeyIndex++;
                            
                            // 如果键盘按键处理完毕且有坐标点，切换到处理坐标
                            if (currentKeyIndex >= keyListSnapshot.Count && coordListSnapshot.Count > 0)
                            {
                                processingKeyboard = false;
                                currentCoordIndex = 0;
                            }
                        }
                        else if (!processingKeyboard && coordListSnapshot.Count > 0)
                        {
                            // 处理坐标点
                            if (currentCoordIndex >= coordListSnapshot.Count)
                            {
                                // 如果还有键盘按键，切换到处理键盘
                                if (keyListSnapshot.Count > 0)
                                {
                                    processingKeyboard = true;
                                    currentKeyIndex = 0;
                                    continue;
                                }
                                else
                                {
                                    // 否则重新开始处理坐标
                                    currentCoordIndex = 0;
                                }
                            }

                            var coord = coordListSnapshot[currentCoordIndex];
                            
                            // 执行鼠标坐标移动并等待
                            await ExecuteCoordinateWithDelayAsync(coord.X, coord.Y, coord.Interval, stopwatch, spinWait,
                                () => token.IsCancellationRequested || !_isEnabled || !_isHoldMode,
                                "按压模式", token);
                            
                            currentCoordIndex++;
                            
                            // 如果坐标点处理完毕且有键盘按键，切换到处理键盘
                            if (currentCoordIndex >= coordListSnapshot.Count && keyListSnapshot.Count > 0)
                            {
                                processingKeyboard = true;
                                currentKeyIndex = 0;
                            }
                        }
                        else
                        {
                            // 既没有键盘按键也没有坐标点，退出循环
                            _logger.Warning("没有可执行的操作，退出按压模式循环");
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        string operationDesc = processingKeyboard 
                            ? $"键盘按键: {keyListSnapshot[currentKeyIndex]}" 
                            : $"坐标点: ({coordListSnapshot[currentCoordIndex].X}, {coordListSnapshot[currentCoordIndex].Y})";
                            
                        _logger.Error($"按压模式执行异常: {operationDesc}", ex);
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
    }
} 