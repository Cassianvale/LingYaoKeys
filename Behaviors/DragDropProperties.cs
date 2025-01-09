using System.Windows;

// 拖放属性
namespace WpfApp.Behaviors
{
    public static class DragDropProperties
    {
        public static readonly DependencyProperty IsDragTargetProperty =
            DependencyProperty.RegisterAttached(
                "IsDragTarget",
                typeof(bool),
                typeof(DragDropProperties),
                new PropertyMetadata(false));

        public static void SetIsDragTarget(DependencyObject element, bool value)
        {
            element.SetValue(IsDragTargetProperty, value);
        }

        public static bool GetIsDragTarget(DependencyObject element)
        {
            return (bool)element.GetValue(IsDragTargetProperty);
        }
    }
} 