using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp.Converters;

public class StatusToColorConverter : IValueConverter
{
    // 渐变边框颜色方案 - 高对比度版本
    public static readonly System.Windows.Media.Color[][] BorderGradientColors = new System.Windows.Media.Color[][]
    {
        // 运行中 - 强对比绿色渐变
        new System.Windows.Media.Color[]
        {
            System.Windows.Media.Color.FromRgb(0x00, 0x96, 0x88), // 深青绿色
            System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50), // 中绿色
            System.Windows.Media.Color.FromRgb(0x8B, 0xC3, 0x4A)  // 青黄绿色
        },
        
        // 已禁用 - 强对比灰色渐变
        new System.Windows.Media.Color[]
        {
            System.Windows.Media.Color.FromRgb(0x45, 0x52, 0x5B), // 深蓝灰色
            System.Windows.Media.Color.FromRgb(0x78, 0x90, 0x9C), // 蓝灰色
            System.Windows.Media.Color.FromRgb(0xB0, 0xBE, 0xC5)  // 浅蓝灰色
        },
        
        // 已停止 - 强对比红色渐变
        new System.Windows.Media.Color[]
        {
            System.Windows.Media.Color.FromRgb(0xD3, 0x2F, 0x2F), // 深红色
            System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36), // 中红色
            System.Windows.Media.Color.FromRgb(0xFF, 0x7D, 0x47)  // 橙红色
        }
    };

    // 背景颜色 - 与边框渐变的中间色匹配
    private static readonly System.Windows.Media.Color[] BackgroundColors = new System.Windows.Media.Color[]
    {
        System.Windows.Media.Color.FromArgb(204, 0x4C, 0xAF, 0x50), // 运行中 - 半透明中绿色
        System.Windows.Media.Color.FromArgb(204, 0x78, 0x90, 0x9C), // 已禁用 - 半透明蓝灰色
        System.Windows.Media.Color.FromArgb(204, 0xF4, 0x43, 0x36)  // 已停止 - 半透明中红色
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int index;
        if (value is string status)
            switch (status)
            {
                case "运行中":
                    index = 0;
                    break;
                case "已禁用":
                    index = 1;
                    break;
                default:
                    index = 2; // 已停止
                    break;
            }
        else
            index = 2; // 默认红色

        return new SolidColorBrush(BackgroundColors[index]);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    
    /// <summary>
    /// 获取指定索引的边框颜色数组
    /// </summary>
    /// <param name="index">颜色索引：0=运行中，1=已禁用，2=已停止</param>
    /// <returns>对应的颜色数组</returns>
    public static System.Windows.Media.Color[] GetBorderColors(int index)
    {
        // 确保索引在有效范围内
        if (index < 0 || index >= BorderGradientColors.Length)
            index = 2; // 默认使用红色（已停止）
        
        return BorderGradientColors[index];
    }

    /// <summary>
    /// 根据状态获取对应的渐变画刷
    /// </summary>
    /// <param name="status">状态文本</param>
    /// <returns>对应状态的渐变画刷</returns>
    public static LinearGradientBrush GetStatusGradientBrush(string status)
    {
        int index;
        switch (status)
        {
            case "运行中":
                index = 0;
                break;
            case "已禁用":
                index = 1;
                break;
            default: // 已停止或其他
                index = 2;
                break;
        }
        
        var brush = new LinearGradientBrush();
        
        // 设置GPU友好的属性
        brush.StartPoint = new System.Windows.Point(0, 0);
        brush.EndPoint = new System.Windows.Point(1, 1);
        brush.MappingMode = BrushMappingMode.RelativeToBoundingBox; // 使用相对方式，更适合GPU渲染
        brush.SpreadMethod = GradientSpreadMethod.Pad; // 使用最优的延展方法
        brush.ColorInterpolationMode = ColorInterpolationMode.SRgbLinearInterpolation; // 最高质量的颜色插值
        
        // 设置缓存提示，提高性能（使用硬编码值，确保最大兼容性）
        // 在.NET 8.0中，我们避免使用ResourceDictionary中的常量值，因为可能会有兼容性问题
        RenderOptions.SetCachingHint(brush, CachingHint.Cache);
        RenderOptions.SetCacheInvalidationThresholdMinimum(brush, 0.5);
        RenderOptions.SetCacheInvalidationThresholdMaximum(brush, 2.0);
        
        // 添加渐变色
        var colors = BorderGradientColors[index];
        brush.GradientStops.Add(new GradientStop(colors[0], 0.0));
        brush.GradientStops.Add(new GradientStop(colors[1], 0.5));
        brush.GradientStops.Add(new GradientStop(colors[2], 1.0));
        
        // 添加旋转变换用于动画
        var rotateTransform = new RotateTransform
        {
            CenterX = 0.5,
            CenterY = 0.5
        };
        
        // 使用变换组，允许添加多个变换
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(rotateTransform);
        brush.RelativeTransform = transformGroup;
        
        // 冻结画刷以提高性能（如果不需要动态更改）
        // brush.Freeze(); // 注意：不要冻结需要动画的画刷
        
        return brush;
    }
}