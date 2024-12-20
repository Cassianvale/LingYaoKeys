using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WpfApp.Behaviors
{
    public class DragAdorner : Adorner
    {
        private readonly UIElement _draggedElement;
        private Point _offset;
        private Point _currentPosition;

        public DragAdorner(UIElement adornedElement, UIElement draggedElement, Point offset) 
            : base(adornedElement)
        {
            _draggedElement = draggedElement;
            _offset = offset;
            IsHitTestVisible = false;
        }

        public void UpdatePosition(Point currentPosition)
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
                    new Point(_currentPosition.X - _offset.X, _currentPosition.Y - _offset.Y),
                    new Size(_draggedElement.RenderSize.Width, _draggedElement.RenderSize.Height)
                )
            );
            drawingContext.Pop();
        }
    }
} 