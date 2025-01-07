using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using Application = System.Windows.Application;
using TextBox = System.Windows.Controls.TextBox;
using ComboBox = System.Windows.Controls.ComboBox;

namespace WpfApp.Services
{
    /// <summary>
    /// 焦点管理服务 - 统一管理应用程序的焦点状态
    /// </summary>
    public class FocusManagementService : INotifyPropertyChanged
    {
        private static readonly Lazy<FocusManagementService> _instance = 
            new Lazy<FocusManagementService>(() => new FocusManagementService());
        
        public static FocusManagementService Instance => _instance.Value;
        
        private readonly Dictionary<string, WeakReference<FrameworkElement>> _focusableElements = 
            new Dictionary<string, WeakReference<FrameworkElement>>();
            
        private FrameworkElement _currentFocusedElement;
        private readonly SerilogManager _logger = SerilogManager.Instance;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<FocusChangedEventArgs> FocusChanged;

        private FocusManagementService()
        {
            // 私有构造函数，确保单例模式
        }

        /// <summary>
        /// 当前获得焦点的元素
        /// </summary>
        public FrameworkElement CurrentFocusedElement
        {
            get => _currentFocusedElement;
            private set
            {
                if (_currentFocusedElement != value)
                {
                    var oldElement = _currentFocusedElement;
                    _currentFocusedElement = value;
                    OnPropertyChanged(nameof(CurrentFocusedElement));
                    OnFocusChanged(oldElement, value);
                }
            }
        }

        /// <summary>
        /// 注册可获得焦点的元素
        /// </summary>
        public void RegisterFocusableElement(FrameworkElement element, string key = null)
        {
            if (element == null) return;

            key = key ?? GetElementKey(element);
            _focusableElements[key] = new WeakReference<FrameworkElement>(element);
            
            // 添加元素的事件处理
            element.GotFocus += OnElementGotFocus;
            element.LostFocus += OnElementLostFocus;
            element.Unloaded += OnElementUnloaded;

            _logger.Debug($"注册焦点元素: {GetElementIdentifier(element)}");
        }

        /// <summary>
        /// 注销可获得焦点的元素
        /// </summary>
        public void UnregisterFocusableElement(FrameworkElement element)
        {
            if (element == null) return;

            var key = GetElementKey(element);
            if (_focusableElements.ContainsKey(key))
            {
                _focusableElements.Remove(key);
                
                // 移除元素的事件处理
                element.GotFocus -= OnElementGotFocus;
                element.LostFocus -= OnElementLostFocus;
                element.Unloaded -= OnElementUnloaded;

                _logger.Debug($"注销焦点元素: {GetElementIdentifier(element)}");
            }
        }

        /// <summary>
        /// 设置焦点到指定元素
        /// </summary>
        public void SetFocus(FrameworkElement element)
        {
            if (element == null || !element.Focusable) return;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (element is TextBox textBox)
                    {
                        textBox.Focus();
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                    else if (element is ComboBox comboBox)
                    {
                        comboBox.Focus();
                    }
                    else
                    {
                        element.Focus();
                    }
                    
                    CurrentFocusedElement = element;
                    _logger.Debug($"设置焦点到元素: {GetElementIdentifier(element)}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"设置焦点失败: {GetElementIdentifier(element)}", ex);
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        /// <summary>
        /// 清除当前焦点
        /// </summary>
        public void ClearFocus()
        {
            if (CurrentFocusedElement == null) return;

            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    UpdateBindings(CurrentFocusedElement);
                    
                    if (CurrentFocusedElement is ComboBox comboBox)
                    {
                        comboBox.IsDropDownOpen = false;
                    }

                    Keyboard.ClearFocus();
                    FocusManager.SetFocusedElement(
                        FocusManager.GetFocusScope(CurrentFocusedElement), 
                        null);
                    
                    CurrentFocusedElement = null;
                    _logger.Debug("清除当前焦点");
                }
                catch (Exception ex)
                {
                    _logger.Error("清除焦点失败", ex);
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }

        private void OnElementGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                CurrentFocusedElement = element;
                _logger.Debug($"元素获得焦点: {GetElementIdentifier(element)}");
            }
        }

        private void OnElementLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                UpdateBindings(element);
                _logger.Debug($"元素失去焦点: {GetElementIdentifier(element)}");
            }
        }

        private void OnElementUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                UnregisterFocusableElement(element);
            }
        }

        private void UpdateBindings(FrameworkElement element)
        {
            try
            {
                if (element is TextBox textBox)
                {
                    textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }
                else if (element is ComboBox comboBox)
                {
                    comboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                    comboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateSource();
                    comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"更新绑定失败: {GetElementIdentifier(element)}", ex);
            }
        }

        private string GetElementKey(FrameworkElement element)
        {
            return $"{element.GetType().Name}_{element.GetHashCode()}";
        }

        private string GetElementIdentifier(FrameworkElement element)
        {
            return $"{element.GetType().Name}{(string.IsNullOrEmpty(element.Name) ? "" : $"[{element.Name}]")}";
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void OnFocusChanged(FrameworkElement oldElement, FrameworkElement newElement)
        {
            FocusChanged?.Invoke(this, new FocusChangedEventArgs(oldElement, newElement));
        }
    }

    public class FocusChangedEventArgs : EventArgs
    {
        public FrameworkElement OldElement { get; }
        public FrameworkElement NewElement { get; }

        public FocusChangedEventArgs(FrameworkElement oldElement, FrameworkElement newElement)
        {
            OldElement = oldElement;
            NewElement = newElement;
        }
    }
} 