using System.Windows.Input;
using System.Windows;
using WpfApp.Commands;
using WpfApp.Services;

namespace WpfApp.ViewModels
{
    public class QRCodeViewModel : ViewModelBase
    {
        private readonly SerilogManager _logger = SerilogManager.Instance;
        private ICommand? _goBackCommand;

        public ICommand GoBackCommand => _goBackCommand ??= new RelayCommand(GoBack);

        private void GoBack()
        {
            try
            {
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.NavigateCommand.Execute("About");
                }
                _logger.Debug("返回About页面");
            }
            catch (System.Exception ex)
            {
                _logger.Error("返回About页面失败", ex);
            }
        }
    }
} 