using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfApp.Services.Core;

namespace WpfApp.Services.Models;

/// <summary>
/// 按键项目类型枚举
/// </summary>
public enum KeyItemType
{
    /// <summary>
    /// 键盘按键
    /// </summary>
    Keyboard,
    
    /// <summary>
    /// 鼠标坐标点击
    /// </summary>
    Coordinates
}

public class KeyItem : INotifyPropertyChanged
{
    private readonly LyKeysService _lyKeysService;
    private bool _isSelected = true;
    private LyKeysCode _keyCode;
    private int _keyInterval = 5;
    private int _x;
    private int _y;
    private KeyItemType _type = KeyItemType.Keyboard;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<bool>? SelectionChanged;
    public event EventHandler<int>? KeyIntervalChanged;

    /// <summary>
    /// 创建键盘按键类型的项目
    /// </summary>
    public KeyItem(LyKeysCode keyCode, LyKeysService lyKeysService)
    {
        _keyCode = keyCode;
        _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
        _type = KeyItemType.Keyboard;
    }

    /// <summary>
    /// 创建鼠标坐标类型的项目
    /// </summary>
    public KeyItem(int x, int y, LyKeysService lyKeysService)
    {
        _x = x;
        _y = y;
        _lyKeysService = lyKeysService ?? throw new ArgumentNullException(nameof(lyKeysService));
        _type = KeyItemType.Coordinates;
    }

    /// <summary>
    /// 按键项目类型
    /// </summary>
    public KeyItemType Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// 键盘按键码（仅当Type为Keyboard时有效）
    /// </summary>
    public LyKeysCode KeyCode
    {
        get => _keyCode;
        set
        {
            if (_keyCode != value)
            {
                _keyCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// X坐标（仅当Type为Coordinates时有效）
    /// </summary>
    public int X
    {
        get => _x;
        set
        {
            if (_x != value)
            {
                _x = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Y坐标（仅当Type为Coordinates时有效）
    /// </summary>
    public int Y
    {
        get => _y;
        set
        {
            if (_y != value)
            {
                _y = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
                SelectionChanged?.Invoke(this, value);
            }
        }
    }

    public int KeyInterval
    {
        get => _keyInterval;
        set
        {
            if (_keyInterval != value)
            {
                _keyInterval = value;
                OnPropertyChanged();
                KeyIntervalChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// 在列表中显示的名称，根据类型显示不同格式
    /// </summary>
    public string DisplayName
    {
        get
        {
            return _type switch
            {
                KeyItemType.Keyboard => _lyKeysService.GetKeyDescription(_keyCode),
                KeyItemType.Coordinates => $"坐标 [{_x}, {_y}]",
                _ => "未知类型"
            };
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}