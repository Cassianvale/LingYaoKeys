using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp.ViewModels;

public class FloatingStatusViewModel : INotifyPropertyChanged
{
    private string _statusText = "已停止";
    private bool _isExecuting = false;
    private bool _isHotkeyControlEnabled = true;

    // 当前状态文本
    public string StatusText
    {
        get
        {
            // 根据热键总开关和执行状态决定显示的状态文本
            if (!_isHotkeyControlEnabled) return "已禁用";

            return _isExecuting ? "运行中" : "已停止";
        }
        set
        {
            // 兼容旧的设置方式，根据值判断执行状态
            if (value == "运行中")
            {
                SetExecutingState(true);
            }
            else if (value == "已停止")
            {
                SetExecutingState(false);
            }
            else if (value != _statusText)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    // 是否正在执行
    public bool IsExecuting
    {
        get => _isExecuting;
        set => SetExecutingState(value);
    }

    // 热键总开关状态
    public bool IsHotkeyControlEnabled
    {
        get => _isHotkeyControlEnabled;
        set
        {
            if (_isHotkeyControlEnabled != value)
            {
                _isHotkeyControlEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText)); // 通知状态文本更新
            }
        }
    }

    // 设置执行状态
    private void SetExecutingState(bool isExecuting)
    {
        if (_isExecuting != isExecuting)
        {
            _isExecuting = isExecuting;
            OnPropertyChanged(nameof(IsExecuting));
            OnPropertyChanged(nameof(StatusText)); // 通知状态文本更新
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}