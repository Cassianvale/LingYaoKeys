using System.Windows.Input;
using System.Text.RegularExpressions;

namespace WpfApp.Views
{
    /// <summary>
    /// KeyboardLayoutView.xaml 的交互逻辑
    /// </summary>
    public partial class KeyboardLayoutView
    {
        public KeyboardLayoutView()
        {
            InitializeComponent();
        }

        public void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
} 