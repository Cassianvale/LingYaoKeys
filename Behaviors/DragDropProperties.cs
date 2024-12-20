using System.Windows;

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

        public static void SetIsDragTarget(UIElement element, bool value)
        {
            element.SetValue(IsDragTargetProperty, value);
        }

        public static bool GetIsDragTarget(UIElement element)
        {
            return (bool)element.GetValue(IsDragTargetProperty);
        }
    }
} 