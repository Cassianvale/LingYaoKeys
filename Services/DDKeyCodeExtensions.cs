using System.ComponentModel;
using System.Reflection;

// 主要提供将DDKeyCode转换为显示名称的功能
namespace WpfApp.Services
{
    public static class DDKeyCodeExtensions
    {
        public static string ToDisplayName(this DDKeyCode keyCode)
        {
            return keyCode switch
            {
                // 特殊键
                DDKeyCode.ESC => "ESC",
                DDKeyCode.BACKSPACE => "Backspace",
                DDKeyCode.TAB => "Tab",
                DDKeyCode.ENTER => "Enter",
                DDKeyCode.LEFT_CTRL => "Left Ctrl",
                DDKeyCode.RIGHT_CTRL => "Right Ctrl",
                DDKeyCode.LEFT_ALT => "Left Alt",
                DDKeyCode.RIGHT_ALT => "Right Alt",
                DDKeyCode.LEFT_SHIFT => "Left Shift",
                DDKeyCode.RIGHT_SHIFT => "Right Shift",
                DDKeyCode.CAPS_LOCK => "Caps Lock",
                DDKeyCode.SPACE => "Space",
                DDKeyCode.ARROW_UP => "↑",
                DDKeyCode.ARROW_LEFT => "←",
                DDKeyCode.ARROW_DOWN => "↓",
                DDKeyCode.ARROW_RIGHT => "→",
                
                // 数字键
                DDKeyCode.NUM_0 => "0",
                DDKeyCode.NUM_1 => "1",
                DDKeyCode.NUM_2 => "2",
                DDKeyCode.NUM_3 => "3",
                DDKeyCode.NUM_4 => "4",
                DDKeyCode.NUM_5 => "5",
                DDKeyCode.NUM_6 => "6",
                DDKeyCode.NUM_7 => "7",
                DDKeyCode.NUM_8 => "8",
                DDKeyCode.NUM_9 => "9",
                
                // 字母键
                DDKeyCode.A => "A",
                DDKeyCode.B => "B",
                DDKeyCode.C => "C",
                DDKeyCode.D => "D",
                DDKeyCode.E => "E",
                DDKeyCode.F => "F",
                DDKeyCode.G => "G",
                DDKeyCode.H => "H",
                DDKeyCode.I => "I",
                DDKeyCode.J => "J",
                DDKeyCode.K => "K",
                DDKeyCode.L => "L",
                DDKeyCode.M => "M",
                DDKeyCode.N => "N",
                DDKeyCode.O => "O",
                DDKeyCode.P => "P",
                DDKeyCode.Q => "Q",
                DDKeyCode.R => "R",
                DDKeyCode.S => "S",
                DDKeyCode.T => "T",
                DDKeyCode.U => "U",
                DDKeyCode.V => "V",
                DDKeyCode.W => "W",
                DDKeyCode.X => "X",
                DDKeyCode.Y => "Y",
                DDKeyCode.Z => "Z",
                
                // 功能键
                DDKeyCode.F1 => "F1",
                DDKeyCode.F2 => "F2",
                DDKeyCode.F3 => "F3",
                DDKeyCode.F4 => "F4",
                DDKeyCode.F5 => "F5",
                DDKeyCode.F6 => "F6",
                DDKeyCode.F7 => "F7",
                DDKeyCode.F8 => "F8",
                DDKeyCode.F9 => "F9",
                DDKeyCode.F10 => "F10",
                DDKeyCode.F11 => "F11",
                DDKeyCode.F12 => "F12",
                
                // 符号键
                DDKeyCode.MINUS => "-",
                DDKeyCode.EQUALS => "=",
                DDKeyCode.LEFT_BRACKET => "[",
                DDKeyCode.RIGHT_BRACKET => "]",
                DDKeyCode.SEMICOLON => ";",
                DDKeyCode.QUOTE => "'",
                DDKeyCode.COMMA => ",",
                DDKeyCode.PERIOD => ".",
                DDKeyCode.SLASH => "/",
                DDKeyCode.BACKSLASH => "\\",
                DDKeyCode.TILDE => "~",
                
                // 鼠标按键
                DDKeyCode.LBUTTON => "Left Click",
                DDKeyCode.RBUTTON => "Right Click",
                DDKeyCode.MBUTTON => "Middle Click",
                DDKeyCode.XBUTTON1 => "Mouse X1",
                DDKeyCode.XBUTTON2 => "Mouse X2",
                
                // 默认情况
                _ => keyCode.ToString()
            };
        }
    }
} 