using WpfApp.Services;

namespace WpfApp.Models
{
    public class KeyItem
    {
        public DDKeyCode KeyCode { get; set; }
        public string DisplayName { get; set; }
        public bool IsSelected { get; set; }

        public KeyItem(DDKeyCode keyCode)
        {
            KeyCode = keyCode;
            DisplayName = keyCode.ToDisplayName();
            IsSelected = true;
        }
    }
} 