using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;

namespace WpfApp.Views;

/// <summary>
/// WindowHandleDialog.xaml 的交互逻辑
/// </summary>
public partial class WindowHandleDialog : Window
{
    private class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public string ClassName { get; set; }
        public string ProcessName { get; set; }

        public WindowInfo(IntPtr handle, string title, string className, string processName)
        {
            Handle = handle;
            Title = title;
            ClassName = className;
            ProcessName = processName;
        }

        public override string ToString()
        {
            return $"{Title} ({ProcessName})";
        }
    }

    public IntPtr SelectedHandle { get; private set; }
    public string SelectedTitle { get; private set; } = string.Empty;
    public string SelectedClassName { get; private set; } = string.Empty;
    public string SelectedProcessName { get; private set; } = string.Empty;

    private ObservableCollection<WindowInfo> _windowList = new();
    private List<WindowInfo> _allWindows = new();

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public WindowHandleDialog()
    {
        InitializeComponent();
        WindowList.ItemsSource = _windowList;
        RefreshWindowList();
    }

    private void RefreshWindowList()
    {
        _allWindows.Clear();
        _windowList.Clear();

        EnumWindows((hWnd, lParam) =>
        {
            if (IsWindowVisible(hWnd))
            {
                var title = GetWindowTitle(hWnd);
                var className = GetWindowClassName(hWnd);
                var processName = GetProcessName(hWnd);

                if (!string.IsNullOrWhiteSpace(title))
                {
                    var windowInfo = new WindowInfo(hWnd, title, className, processName);
                    _allWindows.Add(windowInfo);
                    _windowList.Add(windowInfo);
                }
            }

            return true;
        }, IntPtr.Zero);
    }

    private string GetWindowTitle(IntPtr hWnd)
    {
        var title = new StringBuilder(256);
        GetWindowText(hWnd, title, title.Capacity);
        return title.ToString().Trim();
    }

    private string GetWindowClassName(IntPtr hWnd)
    {
        var className = new StringBuilder(256);
        GetClassName(hWnd, className, className.Capacity);
        return className.ToString().Trim();
    }

    private string GetProcessName(IntPtr hWnd)
    {
        try
        {
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            var searchText = textBox.Text.ToLower();
            _windowList.Clear();

            foreach (var window in _allWindows)
                if (window.Title.ToLower().Contains(searchText) ||
                    window.ProcessName.ToLower().Contains(searchText) ||
                    window.Handle.ToString().ToLower().Contains(searchText))
                    _windowList.Add(window);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowList();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowList.SelectedItem is WindowInfo selectedWindow)
        {
            SelectedHandle = selectedWindow.Handle;
            SelectedTitle = selectedWindow.Title;
            SelectedClassName = selectedWindow.ClassName;
            SelectedProcessName = selectedWindow.ProcessName;
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show("请选择一个窗口", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void WindowList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WindowList.SelectedItem is WindowInfo selectedWindow)
        {
            SelectedHandle = selectedWindow.Handle;
            SelectedTitle = selectedWindow.Title;
            SelectedClassName = selectedWindow.ClassName;
            SelectedProcessName = selectedWindow.ProcessName;
            DialogResult = true;
            Close();
        }
    }
}