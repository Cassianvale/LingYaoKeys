using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfApp.Services;
using WpfApp.Services.Models;
using WpfApp.Services.Utils;

namespace WpfApp.ViewModels
{
    public class KeyboardLayoutViewModel : INotifyPropertyChanged
    {
        private readonly LyKeysService _lyKeysService;
        private KeyboardLayoutConfig _keyboardConfig;
        private bool _isRapidFireEnabled;
        private KeyboardLayoutKey? _selectedKey;
        private readonly List<LyKeysCode> _conflictKeys;
        private int _rapidFireDelay = 10; // 默认连发延迟时间
        private int _pressTime = 5; // 默认按压时间
        private readonly HotkeyService _hotkeyService;
        private bool _isInitializing = true;

        public KeyboardLayoutConfig KeyboardConfig
        {
            get => _keyboardConfig;
            set
            {
                if (_keyboardConfig != value)
                {
                    _keyboardConfig = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsRapidFireEnabled
        {
            get => _isRapidFireEnabled;
            set
            {
                if (SetProperty(ref _isRapidFireEnabled, value))
                {
                    // 更新连发状态
                    _hotkeyService.SetRapidFireEnabled(value);
                    
                    // 同步连发按键配置
                    var rapidFireKeys = GetAllKeys()
                        .Where(k => k.IsRapidFire)
                        .Select(k => new KeyBurstConfig(k.KeyCode)
                        {
                            RapidFireDelay = k.RapidFireDelay,
                            PressTime = k.PressTime
                        });
                    _hotkeyService.UpdateRapidFireKeys(rapidFireKeys);

                    // 保存配置
                    if (!_isInitializing)
                    {
                        SaveConfiguration();
                    }
                }
            }
        }

        public KeyboardLayoutKey? SelectedKey
        {
            get => _selectedKey;
            set
            {
                if (_selectedKey != value)
                {
                    _selectedKey = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsKeySelected));
                }
            }
        }

        public bool IsKeySelected => SelectedKey is not null;

        public int RapidFireDelay
        {
            get => _rapidFireDelay;
            set
            {
                if (_rapidFireDelay != value && value >= 1)
                {
                    _rapidFireDelay = value;
                    OnPropertyChanged();
                    if (SelectedKey != null)
                    {
                        SelectedKey.RapidFireDelay = value;
                        SaveConfiguration();
                    }
                }
            }
        }

        public int PressTime
        {
            get => _pressTime;
            set
            {
                if (_pressTime != value && value >= 1)
                {
                    _pressTime = value;
                    OnPropertyChanged();
                    if (SelectedKey != null)
                    {
                        SelectedKey.PressTime = value;
                        SaveConfiguration();
                    }
                }
            }
        }

        public ICommand KeyClickCommand { get; }
        public ICommand ToggleRapidFireCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        // 添加事件用于通知按键连发状态变化
        public event Action<LyKeysCode, bool> KeyBurstStateChanged;

        public KeyboardLayoutViewModel(LyKeysService lyKeysService, HotkeyService hotkeyService)
        {
            _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
            _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
            _keyboardConfig = new KeyboardLayoutConfig(_lyKeysService);
            _conflictKeys = new List<LyKeysCode>();

            // 初始化命令
            KeyClickCommand = new RelayCommand<KeyboardLayoutKey>(OnKeyClick);
            ToggleRapidFireCommand = new RelayCommand<KeyboardLayoutKey>(OnToggleRapidFire);

            // 初始化键盘布局
            _keyboardConfig.InitializeLayout();

            // 加载配置
            LoadConfiguration();

            // 确保连发状态正确同步
            _hotkeyService.SetRapidFireEnabled(_isRapidFireEnabled);

            _isInitializing = false;
        }

        private void OnKeyClick(KeyboardLayoutKey? keyConfig)
        {
            if (keyConfig == null) return;

            try
            {
                // 检查是否为冲突按键
                if (_conflictKeys.Contains(keyConfig.KeyCode))
                {
                    // TODO: 显示冲突提示
                    return;
                }

                // 如果按键已经是连发模式，则直接取消连发
                if (keyConfig.IsRapidFire)
                {
                    keyConfig.IsRapidFire = false;
                    keyConfig.IsDisabled = false;
                    // 触发事件通知KeyMappingViewModel
                    KeyBurstStateChanged?.Invoke(keyConfig.KeyCode, false);
                    SaveConfiguration();
                    return;
                }

                // 设置选中的按键，显示延迟输入框
                SelectedKey = keyConfig;
                
                // 设置默认延迟值
                _rapidFireDelay = 10;
                OnPropertyChanged(nameof(RapidFireDelay));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"处理按键点击失败: {ex.Message}");
            }
        }

        private void OnToggleRapidFire(KeyboardLayoutKey? keyConfig)
        {
            if (keyConfig == null) return;

            try
            {
                // 检查是否为冲突按键
                if (_conflictKeys.Contains(keyConfig.KeyCode))
                {
                    // TODO: 显示冲突提示
                    return;
                }

                // 设置连发状态和延迟值
                keyConfig.IsRapidFire = true;
                keyConfig.RapidFireDelay = RapidFireDelay;
                keyConfig.PressTime = PressTime;
                if (IsRapidFireEnabled)
                {
                    keyConfig.IsDisabled = true;
                }

                // 触发事件通知KeyMappingViewModel
                KeyBurstStateChanged?.Invoke(keyConfig.KeyCode, true);

                // 立即保存配置
                SaveConfiguration();

                // 清除选中状态
                SelectedKey = null;

                // 通知属性变更
                OnPropertyChanged(nameof(KeyboardConfig));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置连发状态失败: {ex.Message}");
            }
        }

        private void UpdateRapidFireStatus()
        {
            try
            {
                var rapidFireKeys = GetAllKeys()
                    .Where(k => k.IsRapidFire)
                    .Select(k => new KeyBurstConfig(k.KeyCode, k.RapidFireDelay, k.PressTime))
                    .ToList();

                foreach (var key in GetAllKeys())
                {
                    if (key.IsRapidFire)
                    {
                        key.IsDisabled = IsRapidFireEnabled;
                        // 通知KeyMappingViewModel更新标记状态
                        KeyBurstStateChanged?.Invoke(key.KeyCode, true);
                    }
                }

                // 更新 HotkeyService 的连发按键列表
                _hotkeyService.UpdateRapidFireKeys(rapidFireKeys);

                // 保存配置以保持连发状态
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新连发状态失败: {ex.Message}");
            }
        }

        private IEnumerable<KeyboardLayoutKey> GetAllKeys()
        {
            if (_keyboardConfig == null) yield break;

            foreach (var key in _keyboardConfig.StandardKeys)
                yield return key;
            foreach (var key in _keyboardConfig.FunctionKeys)
                yield return key;
            foreach (var key in _keyboardConfig.NumpadKeys)
                yield return key;
            foreach (var key in _keyboardConfig.NavigationKeys)
                yield return key;
            foreach (var key in _keyboardConfig.MouseButtons)
                yield return key;
        }

        public void LoadConfiguration()
        {
            try
            {
                var config = AppConfigService.Config;
                
                // 加载连发状态
                _isRapidFireEnabled = config.IsRapidFire;
                OnPropertyChanged(nameof(IsRapidFireEnabled));
                
                // 立即同步连发状态到服务
                _hotkeyService.SetRapidFireEnabled(_isRapidFireEnabled);

                // 加载按键配置
                if (config.KeyBurst != null)
                {
                    var rapidFireKeys = new List<KeyBurstConfig>();
                    foreach (var burstKey in config.KeyBurst)
                    {
                        var key = GetAllKeys().FirstOrDefault(k => k.KeyCode == burstKey.Code);
                        if (key != null)
                        {
                            key.IsRapidFire = true;
                            key.RapidFireDelay = burstKey.RapidFireDelay;
                            key.PressTime = burstKey.PressTime;
                            key.IsDisabled = IsRapidFireEnabled;
                            
                            // 添加到连发按键列表
                            rapidFireKeys.Add(new KeyBurstConfig(burstKey.Code, burstKey.RapidFireDelay, burstKey.PressTime));
                            
                            // 触发事件通知 KeyMappingViewModel
                            KeyBurstStateChanged?.Invoke(key.KeyCode, true);
                        }
                    }

                    // 同步连发按键配置到 HotkeyService
                    _hotkeyService.UpdateRapidFireKeys(rapidFireKeys);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载连发配置失败: {ex.Message}");
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                var config = AppConfigService.Config;
                var existingKeys = config.keys?.ToList() ?? new List<KeyConfig>();

                // 收集所有连发按键的信息
                var rapidFireKeys = GetAllKeys()
                    .Where(k => k.IsRapidFire)
                    .Select(k => new KeyBurstConfig(k.KeyCode, k.RapidFireDelay, k.PressTime))
                    .ToList();

                Console.WriteLine($"发现 {rapidFireKeys.Count} 个连发按键");

                // 更新keys列表中按键的IsKeyBurst状态
                foreach (var existingKey in existingKeys)
                {
                    var isRapidFire = rapidFireKeys.Any(k => k.Code == existingKey.Code);
                    existingKey.IsKeyBurst = isRapidFire;
                    Console.WriteLine($"更新已有按键 {existingKey.Code} 的连发状态为 {isRapidFire}");
                }

                AppConfigService.UpdateConfig(config =>
                {
                    config.KeyBurst = rapidFireKeys;
                    config.keys = existingKeys;
                    config.IsRapidFire = IsRapidFireEnabled;
                    config.IsRapidFireEnabled = IsRapidFireEnabled;  // 同时更新两个状态字段
                });

                // 同步到HotkeyService
                _hotkeyService.UpdateRapidFireKeys(rapidFireKeys);

                Console.WriteLine("配置保存成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存连发配置失败: {ex.Message}");
                throw;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public KeyboardLayoutKey? GetKeyByCode(LyKeysCode keyCode)
        {
            return GetAllKeys().FirstOrDefault(k => k.KeyCode == keyCode);
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void InitializeRapidFire()
        {
            try
            {
                // 从配置加载连发状态
                var config = AppConfigService.Config;
                _isRapidFireEnabled = config.IsRapidFireEnabled ?? false;

                // 立即同步连发状态和配置
                _hotkeyService.SetRapidFireEnabled(_isRapidFireEnabled);
                
                var rapidFireKeys = GetAllKeys()
                    .Where(k => k.IsRapidFire)
                    .Select(k => new KeyBurstConfig(k.KeyCode, k.RapidFireDelay, k.PressTime))
                    .ToList();

                _hotkeyService.UpdateRapidFireKeys(rapidFireKeys);
                
                // 更新UI状态
                OnPropertyChanged(nameof(IsRapidFireEnabled));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化连发功能失败: {ex.Message}");
                _isRapidFireEnabled = false;
            }
        }
    }
} 