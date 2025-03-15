using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfApp.Services.Models;
using WpfApp.Services.Config;
using WpfApp.Services.Utils;

// 按键映射服务
namespace WpfApp.Services.Core;

public class KeyMappingService
{
    private readonly SerilogManager _logger = SerilogManager.Instance;
    private readonly LyKeysService _lyKeysService;
    private readonly HotkeyService _hotkeyService;
    private ObservableCollection<KeyItem> _keyList;
    private LyKeysCode? _hotkey;    // 触发热键-主按键
    private ModifierKeys _modifiers = ModifierKeys.None; // 触发热键-修饰键

    public event Action<bool>? ExecutionStateChanged;
    public event Action? KeyListChanged;

    public KeyMappingService(
        LyKeysService lyKeysService,
        HotkeyService hotkeyService
        )
    {
        _lyKeysService = lyKeysService;
        _hotkeyService = hotkeyService;
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
        _hotkeyService.StartHotkeyReleased += OnStartHotkeyReleased; // 订阅热键释放事件
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

    // 合并热键设置方法
    public void SetHotkey(LyKeysCode keyCode, ModifierKeys modifiers)
    {
        if (IsKeyInList(keyCode))
        {
            _logger.Warning($"热键 {keyCode} 与现有按键冲突");
            return;
        }

        if (_hotkeyService.RegisterHotkey(keyCode, modifiers))
        {
            _hotkey = keyCode;
            _modifiers = modifiers;
            _logger.Debug($"设置热键: {keyCode}, 修饰键: {modifiers}");
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
        // 根据当前模式决定是启动还是停止
        if (!_lyKeysService.IsHoldMode)
        {
            // 顺序模式下，根据当前执行状态决定启动或停止
            if (IsExecuting)
            {
                StopKeyMapping();
            }
            else
            {
                StartKeyMapping();
            }
        }
        else
        {
            // 按压模式下，按下时启动
            StartKeyMapping(true);
        }
    }

    private void OnStartHotkeyReleased()
    {
        // 在按压模式下，释放热键时停止
        if (_lyKeysService.IsHoldMode && IsExecuting)
        {
            StopKeyMapping();
        }
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
        // 简化为只检查单一热键
        return _hotkey.HasValue && keyCode.Equals(_hotkey.Value);
    }

    public void LoadConfiguration(AppConfig config)
    {
        if (config.keys == null) return;

        _keyList.Clear();
        foreach (var keyConfig in config.keys)
        {
            var keyItem = new KeyItem(keyConfig.Code, _lyKeysService)
            {
                IsSelected = keyConfig.IsSelected
            };
            keyItem.SelectionChanged += (s, isSelected) => KeyListChanged?.Invoke();
            _keyList.Add(keyItem);
        }

        // 只设置一个热键
        if (config.startKey.HasValue) SetHotkey(config.startKey.Value, config.startMods);

        UpdateHotkeyServiceKeyList();
    }

    public void SaveConfiguration(AppConfig config)
    {
        // 保存为相同的启动和停止热键
        config.startKey = _hotkey;
        config.startMods = _modifiers;
        config.stopKey = _hotkey;
        config.stopMods = _modifiers;

        config.keys = _keyList
            .Select(k => new KeyConfig(k.KeyCode, k.IsSelected))
            .ToList();
    }
}