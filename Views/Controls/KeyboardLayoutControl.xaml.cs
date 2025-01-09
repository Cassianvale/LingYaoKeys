using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using WpfApp.Services.Models;

namespace WpfApp.Views.Controls
{
    public partial class KeyboardLayoutControl : System.Windows.Controls.UserControl
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