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
                if (_isRapidFireEnabled != value)
                {
                    _isRapidFireEnabled = value;
                    OnPropertyChanged();
                    UpdateRapidFireStatus();
                    SaveConfiguration();
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

        public KeyboardLayoutViewModel(LyKeysService lyKeysService)
        {
            _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
            _keyboardConfig = new KeyboardLayoutConfig(_lyKeysService);
            _conflictKeys = new List<LyKeysCode>();

            // 初始化命令
            KeyClickCommand = new RelayCommand<KeyboardLayoutKey>(OnKeyClick);
            ToggleRapidFireCommand = new RelayCommand<KeyboardLayoutKey>(OnToggleRapidFire);

            // 初始化键盘布局
            _keyboardConfig.InitializeLayout();

            // 加载配置
            LoadConfiguration();
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
                foreach (var key in GetAllKeys())
                {
                    if (key.IsRapidFire)
                    {
                        key.IsDisabled = IsRapidFireEnabled;
                        // 通知KeyMappingViewModel更新标记状态
                        KeyBurstStateChanged?.Invoke(key.KeyCode, key.IsRapidFire);
                    }
                }
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

                // 加载按键配置
                if (config.KeyBurst != null)
                {
                    foreach (var burstKey in config.KeyBurst)
                    {
                        var key = GetAllKeys().FirstOrDefault(k => k.KeyCode == burstKey.Code);
                        if (key != null)
                        {
                            key.IsRapidFire = true;
                            key.RapidFireDelay = burstKey.RapidFireDelay;
                            key.PressTime = burstKey.PressTime;
                            key.IsDisabled = IsRapidFireEnabled;
                            
                            // 触发事件通知 KeyMappingViewModel
                            KeyBurstStateChanged?.Invoke(key.KeyCode, true);
                        }
                    }
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
                var rapidFireKeys = new List<KeyBurstConfig>();

                // 收集所有连发按键的信息
                var rapidFireKeyInfos = GetAllKeys()
                    .Where(k => k.IsRapidFire)
                    .Select(k => new { Code = k.KeyCode, Delay = k.RapidFireDelay, PressTime = k.PressTime })
                    .ToList();

                Console.WriteLine($"发现 {rapidFireKeyInfos.Count} 个连发按键");

                // 保存所有连发按键到KeyBurst
                foreach (var rapidFireInfo in rapidFireKeyInfos)
                {
                    Console.WriteLine($"添加连发按键: {rapidFireInfo.Code}，延迟值: {rapidFireInfo.Delay}ms，按压时间: {rapidFireInfo.PressTime}ms");
                    rapidFireKeys.Add(new KeyBurstConfig(rapidFireInfo.Code, rapidFireInfo.Delay, rapidFireInfo.PressTime));
                }

                // 更新keys列表中按键的IsKeyBurst状态
                foreach (var existingKey in existingKeys)
                {
                    var isRapidFire = rapidFireKeyInfos.Any(k => k.Code == existingKey.Code);
                    existingKey.IsKeyBurst = isRapidFire;
                    Console.WriteLine($"更新已有按键 {existingKey.Code} 的连发状态为 {isRapidFire}");
                }

                AppConfigService.UpdateConfig(config =>
                {
                    config.KeyBurst = rapidFireKeys;
                    config.keys = existingKeys;
                    config.IsRapidFire = IsRapidFireEnabled;
                });

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
    }
} 