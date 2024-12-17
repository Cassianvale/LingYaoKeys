using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using WpfApp.Models;
using WpfApp.Services;
using WpfApp.Commands;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

/// <summary>
/// 按键映射核心业务逻辑层
/// 核心功能：
/// 1. 热键管理
///    - 配置开始/停止热键
///    - 注册和响应全局热键
///    - 热键状态监控
/// 
/// 2. 按键列表管理
///    - 添加/删除按键
///    - 按键冲突检测
///    - 按键序列维护
/// 
/// 3. 映射模式控制
///    - 顺序模式：按设定顺序循环触发按键
///    - 按压模式：按住热键时持续触发
///    - 按键间隔时间控制
/// 
/// 4. 配置持久化
///    - 加载已保存的配置
///    - 保存当前配置到文件
///    - 配置有效性验证
/// 
/// 5. 驱动交互
///    - 与DD驱动服务通信
///    - 控制按键映射的启动/停止
///    - 监控驱动状态变化
/// </summary>
namespace WpfApp.ViewModels
{
    public class KeyMappingViewModel : ViewModelBase
    {
        private readonly DDDriverService _ddDriver;
        private readonly ConfigService _configService;
        private DDKeyCode? _currentKey;
        private string _currentKeyText = string.Empty;
        private ObservableCollection<KeyItem> _keyList;
        private string _startHotkeyText = string.Empty;
        private string _stopHotkeyText = string.Empty;
        private DDKeyCode? _startHotkey;
        private DDKeyCode? _stopHotkey;
        private int _keyInterval = 50; // 默认按键间隔为50
        private int _selectedKeyMode;
        private ModifierKeys _startModifiers = ModifierKeys.None;
        private ModifierKeys _stopModifiers = ModifierKeys.None;
        private readonly HotkeyService _hotkeyService;
        private bool _isHotkeyEnabled;
        private string _hotkeyStatus;

        // 按键列表
        public ObservableCollection<KeyItem> KeyList
        {
            get => _keyList;
            set => SetProperty(ref _keyList, value);
        }

        // 当前按键文本
        public string CurrentKeyText
        {
            get => _currentKeyText;
            set
            {
                _currentKeyText = value;
                OnPropertyChanged(nameof(CurrentKeyText));
            }
        }

        // 开始热键文本
        public string StartHotkeyText
        {
            get => _startHotkeyText;
            set => SetProperty(ref _startHotkeyText, value);
        }

        // 停止热键文本
        public string StopHotkeyText
        {
            get => _stopHotkeyText;
            set => SetProperty(ref _stopHotkeyText, value);
        }

        // 按键间隔
        public int KeyInterval
        {
            get => _keyInterval;
            set => SetProperty(ref _keyInterval, value);
        }

        // 添加按键命令
        public ICommand AddKeyCommand { get; }

        // 删除选中的按键命令
        public ICommand DeleteSelectedKeysCommand { get; }

        // 按键模式选项
        public List<string> KeyModes { get; } = new List<string> 
        { 
            "顺序模式",
            "按压模式" 
        };

        // 选中的按键模式
        public int SelectedKeyMode
        {
            get => _selectedKeyMode;
            set
            {
                if (SetProperty(ref _selectedKeyMode, value))
                {
                    Trace.WriteLine($"Selected key mode changed to: {value}");
                }
            }
        }

        // 热键是否启用
        public bool IsHotkeyEnabled
        {
            get => _isHotkeyEnabled;
            set
            {
                SetProperty(ref _isHotkeyEnabled, value);
                HotkeyStatus = value ? "热键已启用" : "热键已禁用";
            }
        }

        public string HotkeyStatus
        {
            get => _hotkeyStatus;
            set => SetProperty(ref _hotkeyStatus, value);
        }

        /// <summary>
        /// 按键映射视图模型类
        /// </summary>
        /// <remarks>
        /// 主要职责:
        /// 1. 管理按键映射的核心业务逻辑
        /// 2. 处理热键注册和响应
        /// 3. 维护按键序列和映射状态
        /// 4. 与驱动服务和配置服务交互
        /// </remarks>
        /// <param name="ddDriver">DD驱动服务实例,用于模拟按键操作</param>
        /// <param name="configService">配置服务实例,用于加载和保存配置</param>
        /// <param name="hotkeyService">热键服务实例,用于注册和响应全局热键</param>
        public KeyMappingViewModel(DDDriverService ddDriver, ConfigService configService, HotkeyService hotkeyService)
        {
            _ddDriver = ddDriver;
            _configService = configService;
            _hotkeyService = hotkeyService;

            // 订阅驱动服务的状态变化
            _ddDriver.EnableStatusChanged += (s, enabled) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsHotkeyEnabled = enabled;
                });
            };

            // 修改热键事件处理
            _hotkeyService.StartHotkeyPressed += OnStartHotkeyPressed;
            _hotkeyService.StopHotkeyPressed += OnStopHotkeyPressed;

            // 加载配置并自动注册热键
            var config = _configService.LoadConfig();
            _keyList = new ObservableCollection<KeyItem>();
            
            // 加载开始热键
            if (config.startKey.HasValue)
            {
                SetStartHotkey(config.startKey.Value, config.startMods);
                System.Diagnostics.Debug.WriteLine($"已加载开始热键: {config.startKey.Value}, 修饰键: {config.startMods}");
            }

            // 加载停止热键
            if (config.stopKey.HasValue)
            {
                SetStopHotkey(config.stopKey.Value, config.stopMods);
                System.Diagnostics.Debug.WriteLine($"已加载停止热键: {config.stopKey.Value}, 修饰键: {config.stopMods}");
            }
            
            // 设置跟踪监听器
            Trace.Listeners.Add(new TextWriterTraceListener("debug.log"));
            Trace.AutoFlush = true;
            
            // 设置按键列表
            foreach (var key in config.keyList)
            {
                KeyList.Add(new KeyItem(key));
            }
            
            // 设置其他选项
            KeyInterval = config.interval;
            SelectedKeyMode = config.keyMode;
            Trace.WriteLine($"Initialized key mode to: {config.keyMode}");

            AddKeyCommand = new RelayCommand(AddKey, CanAddKey);
            DeleteSelectedKeysCommand = new RelayCommand(DeleteSelectedKeys);

            IsHotkeyEnabled = false;
            HotkeyStatus = "热键已禁用";

            // 订阅热键服务的事件
            _hotkeyService.SequenceModeStarted += () =>
            {
                IsHotkeyEnabled = true;
            };

            _hotkeyService.SequenceModeStopped += () =>
            {
                IsHotkeyEnabled = false;
            };
        }

        // 设置当前按键
        public void SetCurrentKey(DDKeyCode keyCode)
        {
            System.Diagnostics.Debug.WriteLine($"设置当前按键: {keyCode}");
            _currentKey = keyCode;
            CurrentKeyText = keyCode.ToDisplayName();
        }

        // 设置开始热键
        public void SetStartHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                MessageBox.Show("该按键已在按键列表中，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_stopHotkey.HasValue && _stopHotkey.Value == keyCode && _stopModifiers == modifiers)
            {
                MessageBox.Show("该按键已被设置为停止键，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _startHotkey = keyCode;
            _startModifiers = modifiers;
            UpdateHotkeyText(keyCode, modifiers, true);
            _hotkeyService.RegisterStartHotkey(keyCode, modifiers);
            System.Diagnostics.Debug.WriteLine($"设置开始热键: {keyCode}, 修饰键: {modifiers}");
        }

        // 更新热键显示文本
        private void UpdateHotkeyText(DDKeyCode keyCode, ModifierKeys modifiers, bool isStart)
        {
            StringBuilder keyText = new StringBuilder();
            
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                keyText.Append("Ctrl + ");
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                keyText.Append("Alt + ");
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                keyText.Append("Shift + ");
            
            keyText.Append(keyCode.ToDisplayName());
            
            if (isStart)
                StartHotkeyText = keyText.ToString();
            else
                StopHotkeyText = keyText.ToString();
        }

        // 设置停止热键
        public void SetStopHotkey(DDKeyCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                MessageBox.Show("该按键已在按键列表中，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_startHotkey.HasValue && _startHotkey.Value == keyCode && _startModifiers == modifiers)
            {
                MessageBox.Show("该按键已被设置为启动键，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _stopHotkey = keyCode;
                _stopModifiers = modifiers;
                UpdateHotkeyText(keyCode, modifiers, false);
                
                bool result = _hotkeyService.RegisterStopHotkey(keyCode, modifiers);
                if (!result)
                {
                    System.Diagnostics.Debug.WriteLine($"注册停止热键失败: {keyCode}, 修饰键: {modifiers}");
                    MessageBox.Show("停止热键注册失败，请尝试其他按键", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"设置停止热键: {keyCode}, 修饰键: {modifiers}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置停止热键异常: {ex.Message}");
                MessageBox.Show($"设置停止热键失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 检查是否可以添加按键
        private bool CanAddKey()
        {
            return _currentKey.HasValue;
        }

        // 添加按键
        private void AddKey()
        {
            System.Diagnostics.Debug.WriteLine($"尝试添加按键，当前按键: {_currentKey}");
            
            if (!_currentKey.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("当前按键为空，无法添加");
                return;
            }

            if (_startHotkey.HasValue && _currentKey.Value == _startHotkey.Value)
            {
                MessageBox.Show("该按键已被设置为启动热键，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_stopHotkey.HasValue && _currentKey.Value == _stopHotkey.Value)
            {
                MessageBox.Show("该按键已被设置为停止热键，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (IsKeyInList(_currentKey.Value))
            {
                MessageBox.Show("该按键已在列表中，请选择其他按键", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            KeyList.Add(new KeyItem(_currentKey.Value));
        }

        // 删除选中的按键
        private void DeleteSelectedKeys()
        {
            var selectedKeys = KeyList.Where(k => k.IsSelected).ToList();
            foreach (var key in selectedKeys)
            {
                KeyList.Remove(key);
            }
        }

        // 保存配置
        public void SaveConfig()
        {
            try
            {
                var selectedKeys = KeyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
                
                // 检查热键冲突
                if (_startHotkey.HasValue && selectedKeys.Contains(_startHotkey.Value))
                {
                    MessageBox.Show("启动热键与按键列表存在冲突，请修改后再保存", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (_stopHotkey.HasValue && selectedKeys.Contains(_stopHotkey.Value))
                {
                    MessageBox.Show("停止热键与按键列表存在冲突，请修改后再保存", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _configService.SaveConfig(
                    _startHotkey,
                    _startModifiers,
                    _stopHotkey,  // 确保停止热键被保存
                    _stopModifiers,
                    selectedKeys,
                    SelectedKeyMode,
                    KeyInterval,
                    true  // soundEnabled
                );

                System.Diagnostics.Debug.WriteLine($"配置已保存 - 开始热键: {_startHotkey}, 停止热键: {_stopHotkey}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置异常: {ex.Message}");
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 启动按键映射
        public void StartKeyMapping()
        {
            if (_ddDriver == null) return;
            
            try
            {
                var keys = KeyList.Select(k => k.KeyCode).ToList();
                if (keys.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("警告：按键列表为空");
                    IsHotkeyEnabled = false;
                    return;
                }

                // 打印所有要执行的按键
                foreach (var key in keys)
                {
                    System.Diagnostics.Debug.WriteLine($"按键列表项: {key} ({(int)key})");
                }

                // 设置按键列表和间隔时间
                _hotkeyService.SetKeySequence(keys, KeyInterval);
                
                // 设置驱动服务
                _ddDriver.SetKeyList(keys);
                _ddDriver.IsSequenceMode = SelectedKeyMode == 0;
                _ddDriver.SetKeyInterval(KeyInterval);
                _ddDriver.IsEnabled = true;
                IsHotkeyEnabled = true;

                System.Diagnostics.Debug.WriteLine($"按键映射已启动: 模式={SelectedKeyMode}, 按键数={keys.Count}, 间隔={KeyInterval}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动按键映射失败: {ex.Message}");
                IsHotkeyEnabled = false;
            }
        }

        // 停止按键映射
        public void StopKeyMapping()
        {
            try
            {
                _ddDriver.IsEnabled = false;
                _hotkeyService.StopSequence(); // 确保热键服务也停止
                IsHotkeyEnabled = false; // 更新UI状态
                System.Diagnostics.Debug.WriteLine("按键映射已停止");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止按键映射失败: {ex.Message}");
                IsHotkeyEnabled = false; // 确保错误时状态正确
            }
        }

        // 设置按压模式
        public void SetHoldMode(bool isHold)
        {
            _ddDriver?.SetHoldMode(isHold);
        }

        // 检查按键是否已在列表中
        private bool IsKeyInList(DDKeyCode keyCode)
        {
            return KeyList.Any(k => k.KeyCode == keyCode);
        }

        // 开始热键按下事件处理
               private void OnStartHotkeyPressed()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"开始热键按下 - 当前模式: {SelectedKeyMode}");
                
                // 检查按键列表
                var keys = KeyList.Select(k => k.KeyCode).ToList();
                if (!keys.Any())
                {
                    System.Diagnostics.Debug.WriteLine("按键列表为空");
                    return;
                }
                // 设置按键列表和参数
                _ddDriver.SetKeyList(keys);
                _ddDriver.IsSequenceMode = SelectedKeyMode == 0;
                _ddDriver.SetKeyInterval(KeyInterval);
                // 根据模式启动
                if (SelectedKeyMode == 0) // 顺序模式
                {
                    System.Diagnostics.Debug.WriteLine("启动顺序模式");
                    _ddDriver.IsEnabled = true;
                }
                else // 按压模式
                {
                    System.Diagnostics.Debug.WriteLine("启动按压模式");
                    _ddDriver.SetHoldMode(true);
                }
                IsHotkeyEnabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动按键映射异常: {ex}");
                IsHotkeyEnabled = false;
            }
        }

        // 停止热键按下事件处理
        private void OnStopHotkeyPressed()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("停止热键按下");
                _ddDriver.IsEnabled = false;
                _ddDriver.SetHoldMode(false);
                IsHotkeyEnabled = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止按键映射异常: {ex}");
            }
        }
    }
} 