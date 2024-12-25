using System.Windows.Controls;

namespace WpfApp.Views
{
    public partial class QRCodeView : Page
    {
        public QRCodeView()
        {
            InitializeComponent();
            DataContext = new ViewModels.QRCodeViewModel();
        }
    }
} 