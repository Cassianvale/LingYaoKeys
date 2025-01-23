using System.Windows;
using WpfApp.Services.Models;
using WpfApp.Services.Config;

namespace WpfApp.Views
{
    public partial class KeyboardLayoutControl
    {
        public static readonly DependencyProperty KeyboardConfigProperty =
            DependencyProperty.Register(nameof(KeyboardConfig), typeof(KeyboardLayoutConfig), typeof(KeyboardLayoutControl),
                new PropertyMetadata(null, OnKeyboardConfigChanged));

        public KeyboardLayoutConfig KeyboardConfig
        {
            get => (KeyboardLayoutConfig)GetValue(KeyboardConfigProperty);
            set => SetValue(KeyboardConfigProperty, value);
        }

        public event EventHandler<KeyConfig> KeyClicked;

        public KeyboardLayoutControl()
        {
            InitializeComponent();
        }

        private static void OnKeyboardConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KeyboardLayoutControl control)
            {
                control.DataContext = e.NewValue;
            }
        }

        private void OnKeyClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.DataContext is KeyConfig keyConfig)
            {
                KeyClicked?.Invoke(this, keyConfig);
            }
        }
    }
} 