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
        private KeyConfig _selectedKey;
        private readonly List<LyKeysCode> _conflictKeys; // 存储冲突的按键

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

        public KeyConfig SelectedKey
        {
            get => _selectedKey;
            set
            {
                if (_selectedKey != value)
                {
                    _selectedKey = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand KeyClickCommand { get; }
        public ICommand ToggleRapidFireCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public KeyboardLayoutViewModel(LyKeysService lyKeysService)
        {
            _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
            _keyboardConfig = new KeyboardLayoutConfig();
            _conflictKeys = new List<LyKeysCode>();

            // 初始化命令
            KeyClickCommand = new RelayCommand<KeyConfig>(OnKeyClick);
            ToggleRapidFireCommand = new RelayCommand<KeyConfig>(OnToggleRapidFire);

            // 加载配置
            LoadConfiguration();
        }

        private void OnKeyClick(KeyConfig keyConfig)
        {
            if (keyConfig == null) return;

            SelectedKey = keyConfig;

            // 检查是否为冲突按键
            if (_conflictKeys.Contains(keyConfig.KeyCode))
            {
                // 显示冲突提示
                return;
            }

            // 处理按键点击事件
            if (keyConfig.IsRapidFire && IsRapidFireEnabled)
            {
                // 如果是连发模式且已启用连发
                keyConfig.IsDisabled = true;
            }
            else
            {
                keyConfig.IsDisabled = false;
            }
        }

        private void OnToggleRapidFire(KeyConfig keyConfig)
        {
            if (keyConfig == null) return;

            // 检查是否为冲突按键
            if (_conflictKeys.Contains(keyConfig.KeyCode))
            {
                // 显示冲突提示
                return;
            }

            keyConfig.IsRapidFire = !keyConfig.IsRapidFire;
            
            if (keyConfig.IsRapidFire)
            {
                keyConfig.IsHighlighted = true;
                if (IsRapidFireEnabled)
                {
                    keyConfig.IsDisabled = true;
                }
            }
            else
            {
                keyConfig.IsHighlighted = false;
                keyConfig.IsDisabled = false;
            }

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

        private IEnumerable<KeyConfig> GetAllKeys()
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