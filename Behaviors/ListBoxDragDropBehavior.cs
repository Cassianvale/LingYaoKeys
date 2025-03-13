using System.Collections.ObjectModel;
using Microsoft.Xaml.Behaviors;
using WpfApp.Services.Models;
using WpfApp.ViewModels;
using System.Windows.Media.Animation;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows;
using System.Windows.Threading;

// 列表框拖放行为
namespace WpfApp.Behaviors
{
    public class ListBoxDragDropBehavior : Behavior<System.Windows.Controls.ListBox>
    {
        private System.Windows.Point _startPoint;
        private bool _isDragging;
        private ListBoxItem? _draggedItem;
        private int _sourceIndex;
        private DragAdorner? _dragAdorner;
        private AdornerLayer? _adornerLayer;

        protected override void OnAttached()
        {
            base.OnAttached();
            
            if (AssociatedObject != null)
            {
                AssociatedObject.PreviewMouseLeftButtonDown += ListBox_PreviewMouseLeftButtonDown;
                AssociatedObject.PreviewMouseMove += ListBox_PreviewMouseMove;
                AssociatedObject.PreviewMouseLeftButtonUp += ListBox_PreviewMouseLeftButtonUp;
                AssociatedObject.Drop += ListBox_Drop;
                AssociatedObject.DragEnter += ListBox_DragEnter;
                AssociatedObject.DragOver += ListBox_DragOver;
                AssociatedObject.DragLeave += ListBox_DragLeave;
                AssociatedObject.AllowDrop = true;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            
            if (AssociatedObject != null)
            {
                AssociatedObject.PreviewMouseLeftButtonDown -= ListBox_PreviewMouseLeftButtonDown;
                AssociatedObject.PreviewMouseMove -= ListBox_PreviewMouseMove;
                AssociatedObject.PreviewMouseLeftButtonUp -= ListBox_PreviewMouseLeftButtonUp;
                AssociatedObject.Drop -= ListBox_Drop;
                AssociatedObject.DragEnter -= ListBox_DragEnter;
                AssociatedObject.DragOver -= ListBox_DragOver;
                AssociatedObject.DragLeave -= ListBox_DragLeave;
            }
        }

        private void ListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox)
            {
                // 检查点击是否在滚动条或相关组件上
                if (IsScrollBarOrThumb(e.OriginalSource as DependencyObject))
                {
                    // 如果点击在滚动条相关组件上，不开始拖拽操作
                    return;
                }

                // 检查是否点击在滚动条轨道或滚动条区域上
                var scrollViewer = FindAncestor<ScrollViewer>((DependencyObject)e.OriginalSource);
                if (scrollViewer != null)
                {
                    // 获取点击位置
                    var clickPoint = e.GetPosition(scrollViewer);
                    // 判断点击是否在滚动条区域（右侧或底部边缘）
                    if (clickPoint.X > scrollViewer.ActualWidth - SystemParameters.VerticalScrollBarWidth ||
                        clickPoint.Y > scrollViewer.ActualHeight - SystemParameters.HorizontalScrollBarHeight)
                    {
                        // 点击在滚动条区域，不开始拖拽
                        return;
                    }
                }

                _startPoint = e.GetPosition(null);
                _isDragging = false;

                var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (item != null)
                {
                    _draggedItem = item;
                    _sourceIndex = listBox.Items.IndexOf(item.DataContext);
                }
            }
        }

        private void ListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // 如果鼠标左键按下、还未开始拖拽且有拖拽项
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging && _draggedItem != null)
            {
                // 再次检查是否在滚动条或相关组件上
                if (IsScrollBarOrThumb(e.OriginalSource as DependencyObject))
                {
                    // 如果在滚动条相关组件上移动，取消拖拽操作
                    _draggedItem = null;
                    return;
                }

                System.Windows.Point position = e.GetPosition(null);
                
                // 判断移动距离是否足够启动拖拽
                if (Math.Abs(position.X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _startPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    StartDrag(sender as System.Windows.Controls.ListBox, _draggedItem, e);
                }
            }
        }

        private void ListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _draggedItem = null;
        }

        private void StartDrag(System.Windows.Controls.ListBox? listBox, ListBoxItem draggedItem, System.Windows.Input.MouseEventArgs e)
        {
            if (listBox == null) return;

            try
            {
                _isDragging = true;

                // 创建拖拽预览
                var draggedVisual = new Border
                {
                    Width = draggedItem.ActualWidth,
                    Height = draggedItem.ActualHeight,
                    Background = new SolidColorBrush(System.Windows.Media.Colors.White),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Effect = new DropShadowEffect
                    {
                        ShadowDepth = 2,
                        BlurRadius = 5,
                        Opacity = 0.5
                    },
                    Child = new ContentPresenter
                    {
                        Content = draggedItem.Content,
                        ContentTemplate = listBox.ItemTemplate,
                        Margin = draggedItem.Padding
                    }
                };

                // 计算鼠标相对于拖拽项的偏移
                System.Windows.Point mousePos = e.GetPosition(draggedItem);
                _adornerLayer = AdornerLayer.GetAdornerLayer(listBox);
                
                if (_adornerLayer != null)
                {
                    _dragAdorner = new DragAdorner(listBox, draggedVisual, mousePos);
                    _adornerLayer.Add(_dragAdorner);
                }

                // 设置原始项的视觉效果
                draggedItem.Opacity = 0.3;

                // 创建拖拽数据
                var dataObject = new System.Windows.DataObject();
                dataObject.SetData("KeyItem", draggedItem.DataContext);
                dataObject.SetData("DragSource", draggedItem);
                dataObject.SetData("SourceIndex", _sourceIndex);

                try
                {
                    // 开始拖拽操作
                    DragDrop.DoDragDrop(draggedItem, dataObject, System.Windows.DragDropEffects.Move);
                }
                finally
                {
                    // 清理
                    if (_adornerLayer != null && _dragAdorner != null)
                    {
                        _adornerLayer.Remove(_dragAdorner);
                    }
                    _dragAdorner = null;
                    draggedItem.Opacity = 1.0;
                }
            }
            catch (Exception ex)
            {
                // 记录异常
                System.Diagnostics.Debug.WriteLine($"拖拽过程中发生异常: {ex.Message}");
                
                // 确保清理资源
                if (_adornerLayer != null && _dragAdorner != null)
                {
                    _adornerLayer.Remove(_dragAdorner);
                }
                _dragAdorner = null;
                draggedItem.Opacity = 1.0;
            }
        }

        private void ListBox_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && _draggedItem != null && e.Data.GetDataPresent("KeyItem"))
            {
                try
                {
                    var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (targetItem != null)
                    {
                        var sourceData = e.Data.GetData("KeyItem") as KeyItem;
                        var targetData = targetItem.DataContext as KeyItem;

                        if (sourceData != null && targetData != null)
                        {
                            var items = listBox.ItemsSource as ObservableCollection<KeyItem>;
                            if (items != null)
                            {
                                int targetIndex = items.IndexOf(targetData);
                                
                                // 添加动画效果
                                var animation = new DoubleAnimation
                                {
                                    From = 0.5,
                                    To = 1.0,
                                    Duration = TimeSpan.FromMilliseconds(200)
                                };
                                _draggedItem.BeginAnimation(UIElement.OpacityProperty, animation);

                                // 交换位置而不是移动
                                if (_sourceIndex != targetIndex)
                                {
                                    // 使用Move方法而不是临时变量
                                    MoveItem(items, _sourceIndex, targetIndex);
                                    
                                    // 执行成功动画效果
                                    ApplySuccessAnimation(targetItem);
                                }

                                // 清除所有拖拽标记
                                ClearAllDragTargets(listBox);

                                // 触发保存
                                if (listBox.DataContext is KeyMappingViewModel viewModel)
                                {
                                    viewModel.SaveConfig();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"处理拖放时发生异常: {ex.Message}");
                    ClearAllDragTargets(sender as System.Windows.Controls.ListBox);
                }
            }
        }

        // 优化：提取清除拖拽标记的方法
        private void ClearAllDragTargets(System.Windows.Controls.ListBox? listBox)
        {
            if (listBox == null) return;
            
            foreach (var item in listBox.Items)
            {
                if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                {
                    DragDropProperties.SetIsDragTarget(listBoxItem, false);
                }
            }
        }

        // 优化：使用Move方法替代临时变量交换
        private void MoveItem(ObservableCollection<KeyItem> items, int sourceIndex, int targetIndex)
        {
            // 直接交换两个位置的元素，而不是先删除再插入
            var temp = items[targetIndex];
            items[targetIndex] = items[sourceIndex];
            items[sourceIndex] = temp;
        }

        // 添加成功动画效果 - 闪烁提示不改变最终背景
        private void ApplySuccessAnimation(ListBoxItem targetItem)
        {
            var border = FindVisualChild<Border>(targetItem);
            if (border == null) return;

            // 创建闪烁动画效果，从选中色到绿色再返回选中色
            var flashAnimation = new ColorAnimation
            {
                From = Colors.LightGreen,         // 闪烁颜色
                Duration = TimeSpan.FromMilliseconds(200), // 短暂的闪烁
                AutoReverse = true,                // 自动反向动画
                RepeatBehavior = new RepeatBehavior(2),    // 重复两次
                FillBehavior = FillBehavior.Stop           // 结束后停止
            };

            // 在UI线程上执行动画
            targetItem.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 创建一个分离的画刷用于动画，避免影响原背景
                    var overlayBorder = new Border
                    {
                        Width = border.ActualWidth,
                        Height = border.ActualHeight,
                        Background = new SolidColorBrush(Colors.LightGreen),
                        Opacity = 0.5
                    };

                    // 将闪烁边框添加为临时视觉元素
                    var parentPanel = FindAncestor<System.Windows.Controls.Panel>(border);
                    if (parentPanel != null)
                    {
                        int index = parentPanel.Children.IndexOf(border);
                        if (index >= 0)
                        {
                            parentPanel.Children.Insert(index + 1, overlayBorder);
                            
                            // 创建淡出动画
                            var fadeOutAnimation = new DoubleAnimation
                            {
                                From = 0.5,
                                To = 0.0,
                                Duration = TimeSpan.FromMilliseconds(400),
                                FillBehavior = FillBehavior.Stop
                            };
                            
                            // 动画完成后移除临时边框
                            fadeOutAnimation.Completed += (s, e) => 
                            {
                                parentPanel.Children.Remove(overlayBorder);
                            };
                            
                            // 执行淡出动画
                            overlayBorder.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"应用闪烁动画时出错: {ex.Message}");
                }
            }));
        }

        private void ListBox_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("KeyItem"))
            {
                e.Effects = System.Windows.DragDropEffects.None;
                return;
            }

            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
        }

        private void ListBox_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("KeyItem"))
                return;

            // 更新拖拽预览的位置
            if (_dragAdorner != null)
            {
                _dragAdorner.UpdatePosition(e.GetPosition(AssociatedObject));
            }

            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem != null && targetItem != _draggedItem)
            {
                // 清除其他项的拖拽标记
                if (sender is System.Windows.Controls.ListBox listBox)
                {
                    foreach (var item in listBox.Items)
                    {
                        if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem
                            && listBoxItem != targetItem)
                        {
                            DragDropProperties.SetIsDragTarget(listBoxItem, false);
                        }
                    }
                }

                // 设置当前目标的拖拽标记
                DragDropProperties.SetIsDragTarget(targetItem, true);
            }

            e.Handled = true;
        }

        private void ListBox_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox)
            {
                // 清除所有项的拖拽标记
                foreach (var item in listBox.Items)
                {
                    if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem listBoxItem)
                    {
                        DragDropProperties.SetIsDragTarget(listBoxItem, false);
                    }
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T ancestor)
                {
                    return ancestor;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }

        // 查找视觉树中的子元素
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T typedChild)
                {
                    return typedChild;
                }
                
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            
            return null;
        }

        // 判断元素是否为滚动条相关组件
        private bool IsScrollBarOrThumb(DependencyObject element)
        {
            // 检查元素本身
            if (element is System.Windows.Controls.Primitives.ScrollBar || 
                element is System.Windows.Controls.Primitives.Thumb ||
                element is System.Windows.Controls.Primitives.RepeatButton)
            {
                return true;
            }

            // 检查元素的父级元素
            var parent = FindAncestor<System.Windows.Controls.Primitives.ScrollBar>(element);
            if (parent != null)
            {
                return true;
            }

            // 检查是否是滚动条的轨道或按钮
            var thumb = FindAncestor<System.Windows.Controls.Primitives.Thumb>(element);
            var repeatButton = FindAncestor<System.Windows.Controls.Primitives.RepeatButton>(element);
            
            return thumb != null || repeatButton != null;
        }
    }
} 