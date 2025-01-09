using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

// 拖动装饰器
namespace WpfApp.Behaviors
{
    public class DragAdorner : Adorner
    {
        private readonly UIElement _draggedElement;
        private System.Windows.Point _offset;
        private System.Windows.Point _currentPosition;

        public DragAdorner(UIElement adornedElement, UIElement draggedElement, System.Windows.Point offset) 
            : base(adornedElement)
        {
            _draggedElement = draggedElement;
            _offset = offset;
            IsHitTestVisible = false;
        }

        public void UpdatePosition(System.Windows.Point currentPosition)
        {
            _currentPosition = currentPosition;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var visualBrush = new VisualBrush(_draggedElement)
            {
                Opacity = 0.7,
                Stretch = Stretch.None
            };

            drawingContext.PushOpacity(0.7);
            drawingContext.DrawRectangle(
                visualBrush,
                null,
                new Rect(
                    new System.Windows.Point(_currentPosition.X - _offset.X, _currentPosition.Y - _offset.Y),
                    new System.Windows.Size(_draggedElement.RenderSize.Width, _draggedElement.RenderSize.Height)
                )
            );
            drawingContext.Pop();
        }
    }
} 