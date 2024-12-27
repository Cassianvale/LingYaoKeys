using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows;

namespace WpfApp.Views
{
    /// <summary>
    /// QRCodeView.xaml 的交互逻辑
    /// </summary>
    public partial class QRCodeView : Page, IDisposable
    {
        private readonly ViewModels.QRCodeViewModel _viewModel;
        private bool _disposedValue;
        private BitmapImage _wechatQrImage;
        private BitmapImage _wechatQr1Image;

        public QRCodeView()
        {
            InitializeComponent();
            _viewModel = new ViewModels.QRCodeViewModel();
            DataContext = _viewModel;

            // 预加载并缓存图片
            LoadQRCodeImages();
        }

        private void LoadQRCodeImages()
        {
            try
            {
                // 创建并缓存微信二维码图片
                _wechatQrImage = new BitmapImage();
                _wechatQrImage.BeginInit();
                _wechatQrImage.CacheOption = BitmapCacheOption.OnLoad;
                _wechatQrImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                _wechatQrImage.UriSource = new Uri("pack://application:,,,/Resource/img/wechat_qr.png");
                _wechatQrImage.EndInit();
                _wechatQrImage.Freeze(); // 冻结位图以提高性能

                // 创建并缓存第二个微信二维码图片
                _wechatQr1Image = new BitmapImage();
                _wechatQr1Image.BeginInit();
                _wechatQr1Image.CacheOption = BitmapCacheOption.OnLoad;
                _wechatQr1Image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                _wechatQr1Image.UriSource = new Uri("pack://application:,,,/Resource/img/wechat_qr_1.png");
                _wechatQr1Image.EndInit();
                _wechatQr1Image.Freeze(); // 冻结位图以提高性能

                // 设置图片源
                WechatQRImage.Source = _wechatQrImage;
                WechatQR1Image.Source = _wechatQr1Image;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载二维码图片失败，请检查资源文件是否完整，错误信息:{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // 清理托管资源
                    _wechatQrImage = null;
                    _wechatQr1Image = null;

                    if (_viewModel is IDisposable disposableViewModel)
                    {
                        disposableViewModel.Dispose();
                    }
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
} 