using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfApp.Services.Models;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;

namespace WpfApp.Services.Core
{
    public class KeyMappingService
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private readonly LyKeysService _lyKeysService;
        private readonly HotkeyService _hotkeyService;
        private readonly AudioService _audioService;
        private ObservableCollection<KeyItem> _keyList;
        private LyKeysCode? _startHotkey;
        private LyKeysCode? _stopHotkey;
        private ModifierKeys _startModifiers = ModifierKeys.None;
        private ModifierKeys _stopModifiers = ModifierKeys.None;

        public event Action<bool>? ExecutionStateChanged;
        public event Action? KeyListChanged;

        public KeyMappingService(
            LyKeysService lyKeysService,
            HotkeyService hotkeyService,
            AudioService audioService)
        {
            _lyKeysService = lyKeysService;
            _hotkeyService = hotkeyService;
            _audioService = audioService;
            _keyList = new ObservableCollection<KeyItem>();

            InitializeEventHandlers();
        }

        public ObservableCollection<KeyItem> KeyList => _keyList;

        public bool IsExecuting { get; private set; }

        public int KeyInterval
        {
            get => _lyKeysService.KeyInterval;
            set
            {
                if (_lyKeysService.KeyInterval != value)
                {
                    _lyKeysService.KeyInterval = value;
                    UpdateHotkeyServiceKeyList();
                }
            }
        }

        private void InitializeEventHandlers()
        {
            _hotkeyService.StartHotkeyPressed += OnStartHotkeyPressed;
            _hotkeyService.StopHotkeyPressed += OnStopHotkeyPressed;
        }

        public void AddKey(LyKeysCode keyCode)
        {
            if (IsKeyInList(keyCode) || IsHotkeyConflict(keyCode))
            {
                _logger.Warning($"按键 {keyCode} 已存在或与热键冲突");
                return;
            }

            var newKeyItem = new KeyItem(keyCode, _lyKeysService);
            newKeyItem.SelectionChanged += (s, isSelected) =>
            {
                UpdateHotkeyServiceKeyList();
                KeyListChanged?.Invoke();
            };

            _keyList.Add(newKeyItem);
            UpdateHotkeyServiceKeyList();
            KeyListChanged?.Invoke();
        }

        public void RemoveKey(KeyItem keyItem)
        {
            if (_keyList.Remove(keyItem))
            {
                UpdateHotkeyServiceKeyList();
                KeyListChanged?.Invoke();
            }
        }

        public void SetStartHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                _logger.Warning($"启动热键 {keyCode} 与现有按键冲突");
                return;
            }

            if (_hotkeyService.RegisterStartHotkey(keyCode, modifiers))
            {
                _startHotkey = keyCode;
                _startModifiers = modifiers;
                _logger.Debug($"设置启动热键: {keyCode}, 修饰键: {modifiers}");
            }
        }

        public void SetStopHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
        {
            if (IsKeyInList(keyCode))
            {
                _logger.Warning($"停止热键 {keyCode} 与现有按键冲突");
                return;
            }

            if (_hotkeyService.RegisterStopHotkey(keyCode, modifiers))
            {
                _stopHotkey = keyCode;
                _stopModifiers = modifiers;
                _logger.Debug($"设置停止热键: {keyCode}, 修饰键: {modifiers}");
            }
        }

        public void StartKeyMapping(bool isHoldMode = false)
        {
            if (IsExecuting) return;

            var selectedKeys = GetSelectedKeys();
            if (selectedKeys.Count == 0)
            {
                _logger.Warning("没有选中的按键");
                return;
            }

            try
            {
                // 设置按键列表到驱动服务
                _lyKeysService.SetKeyList(selectedKeys);
                
                // 将选中的按键及其间隔传递给HotkeyService
                _hotkeyService.SetKeySequence(
                    _keyList.Where(k => k.IsSelected)
                    .Select(k => new KeyItemSettings
                    {
                        KeyCode = k.KeyCode,
                        Interval = k.KeyInterval
                    }).ToList());
                
                _lyKeysService.IsHoldMode = isHoldMode;
                _lyKeysService.IsEnabled = true;

                IsExecuting = true;
                ExecutionStateChanged?.Invoke(true);
                _logger.Debug($"开始按键映射: 模式={isHoldMode}, 按键数={selectedKeys.Count}");
            }
            catch (Exception ex)
            {
                _logger.Error("启动按键映射失败", ex);
                StopKeyMapping();
            }
        }

        public void StopKeyMapping()
        {
            if (!IsExecuting) return;

            try
            {
                _hotkeyService.StopSequence();
                _lyKeysService.IsEnabled = false;
                _lyKeysService.IsHoldMode = false;

                IsExecuting = false;
                ExecutionStateChanged?.Invoke(false);
                _logger.Debug("停止按键映射");
            }
            catch (Exception ex)
            {
                _logger.Error("停止按键映射失败", ex);
            }
        }

        private void OnStartHotkeyPressed()
        {
            StartKeyMapping();
        }

        private void OnStopHotkeyPressed()
        {
            StopKeyMapping();
        }

        private void UpdateHotkeyServiceKeyList()
        {
            var selectedKeys = GetSelectedKeys();
            
            // 将选中的按键及其间隔传递给HotkeyService
            _hotkeyService.SetKeySequence(
                _keyList.Where(k => k.IsSelected)
                .Select(k => new KeyItemSettings
                {
                    KeyCode = k.KeyCode,
                    Interval = k.KeyInterval
                }).ToList());
            
            _lyKeysService.SetKeyList(selectedKeys);
            _logger.Debug($"更新按键列表 - 选中按键数: {selectedKeys.Count}, 使用独立按键间隔");
        }

        private List<LyKeysCode> GetSelectedKeys()
        {
            return _keyList.Where(k => k.IsSelected).Select(k => k.KeyCode).ToList();
        }

        private bool IsKeyInList(LyKeysCode keyCode)
        {
            return _keyList.Any(k => k.KeyCode.Equals(keyCode));
        }

        private bool IsHotkeyConflict(LyKeysCode keyCode)
        {
            return (_startHotkey.HasValue && keyCode.Equals(_startHotkey.Value)) ||
                   (_stopHotkey.HasValue && keyCode.Equals(_stopHotkey.Value));
        }

        public void LoadConfiguration(AppConfig config)
        {
            if (config.keys == null) return;

            _keyList.Clear();
            foreach (var keyConfig in config.keys)
            {
                var keyItem = new KeyItem(keyConfig.Code, _lyKeysService)
                {
                    IsSelected = keyConfig.IsSelected,
                };
                keyItem.SelectionChanged += (s, isSelected) => KeyListChanged?.Invoke();
                _keyList.Add(keyItem);
            }

            if (config.startKey.HasValue)
            {
                SetStartHotkey(config.startKey.Value, config.startMods);
            }

            if (config.stopKey.HasValue)
            {
                SetStopHotkey(config.stopKey.Value, config.stopMods);
            }

            UpdateHotkeyServiceKeyList();
        }

        public void SaveConfiguration(AppConfig config)
        {
            config.startKey = _startHotkey;
            config.startMods = _startModifiers;
            config.stopKey = _stopHotkey;
            config.stopMods = _stopModifiers;
        }
    }
} 