using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace WpfApp.Views.Controls
{
    /// <summary>
    /// KeyboardLayoutView.xaml 的交互逻辑
    /// </summary>
    public partial class KeyboardLayoutView : System.Windows.Controls.UserControl
    {
        public KeyboardLayoutView()
        {
            InitializeComponent();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
} 