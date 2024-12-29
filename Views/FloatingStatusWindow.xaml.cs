using System.Windows;
using System.Windows.Input;
using WpfApp.Models;
using WpfApp.Services;

namespace WpfApp.Views
{
    public partial class FloatingStatusWindow : Window
    {
        private readonly AppConfig _config;

        public FloatingStatusWindow()
        {
            InitializeComponent();
            _config = AppConfigService.Config;
            
            // 加载上次保存的位置
            var left = _config.FloatingWindowLeft;
            var top = _config.FloatingWindowTop;
            
            // 确保窗口在屏幕范围内
            if (left >= 0 && top >= 0 && 
                left <= SystemParameters.WorkArea.Right - Width &&
                top <= SystemParameters.WorkArea.Bottom - Height)
            {
                Left = left;
                Top = top;
            }
            else
            {
                // 默认位置：屏幕右下角
                Left = SystemParameters.WorkArea.Right - Width - 10;
                Top = SystemParameters.WorkArea.Bottom - Height - 10;
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
                // 保存新位置
                AppConfigService.UpdateConfig(config =>
                {
                    config.FloatingWindowLeft = Left;
                    config.FloatingWindowTop = Top;
                });
            }
        }
    }
} 