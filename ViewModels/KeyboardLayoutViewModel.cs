using System;
using System.Collections.Generic;
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
        private KeyboardLayoutKey _selectedKey;
        private readonly List<LyKeysCode> _conflictKeys;
        private int _rapidFireDelay = 10; // 默认连发延迟时间

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
                }
            }
        }

        public KeyboardLayoutKey SelectedKey
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

        public bool IsKeySelected => SelectedKey != null;

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

        public ICommand KeyClickCommand { get; }
        public ICommand ToggleRapidFireCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

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

        private void OnKeyClick(KeyboardLayoutKey keyConfig)
        {
            if (keyConfig == null) return;

            // 检查是否为冲突按键
            if (_conflictKeys.Contains(keyConfig.KeyCode))
            {
                // TODO: 显示冲突提示
                return;
            }

            // 设置选中的按键
            SelectedKey = keyConfig;
            
            // 如果按键已经是连发模式，则使用其当前延迟值
            if (keyConfig.IsRapidFire)
            {
                RapidFireDelay = keyConfig.RapidFireDelay;
            }
            else
            {
                // 否则使用默认延迟值
                RapidFireDelay = 10;
            }
        }

        private void OnToggleRapidFire(KeyboardLayoutKey keyConfig)
        {
            if (keyConfig == null) return;

            // 检查是否为冲突按键
            if (_conflictKeys.Contains(keyConfig.KeyCode))
            {
                // TODO: 显示冲突提示
                return;
            }

            // 切换连发状态
            keyConfig.IsRapidFire = !keyConfig.IsRapidFire;
            
            if (keyConfig.IsRapidFire)
            {
                keyConfig.RapidFireDelay = RapidFireDelay;
                if (IsRapidFireEnabled)
                {
                    keyConfig.IsDisabled = true;
                }
            }
            else
            {
                keyConfig.IsDisabled = false;
            }

            // 清除选中状态
            SelectedKey = null;

            // 保存配置
            SaveConfiguration();
        }

        private void UpdateRapidFireStatus()
        {
            foreach (var key in GetAllKeys())
            {
                if (key.IsRapidFire)
                {
                    key.IsDisabled = IsRapidFireEnabled;
                }
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

        private void LoadConfiguration()
        {
            // TODO: 从AppConfig加载配置
            // 1. 加载连发状态
            // 2. 加载按键延迟
            // 3. 加载高亮状态
        }

        private void SaveConfiguration()
        {
            // TODO: 保存配置到AppConfig
            // 1. 保存连发状态
            // 2. 保存按键延迟
            // 3. 保存高亮状态
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 