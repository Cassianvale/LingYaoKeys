# -*- coding: utf-8 -*-


# 虚拟键码映射
class VirtualKeys:
    # 基本按键
    VK_CODE = {
        # 控制键
        'backspace': 0x08,
        'tab': 0x09,
        'clear': 0x0C,
        'enter': 0x0D,
        'shift': 0x10,
        'ctrl': 0x11,
        'alt': 0x12,
        'pause': 0x13,
        'caps_lock': 0x14,
        'esc': 0x1B,
        'spacebar': 0x20,
        
        # 导航键
        'page_up': 0x21,
        'page_down': 0x22,
        'end': 0x23,
        'home': 0x24,
        'left_arrow': 0x25,
        'up_arrow': 0x26,
        'right_arrow': 0x27,
        'down_arrow': 0x28,
        
        # 功能键
        'select': 0x29,
        'print': 0x2A,
        'execute': 0x2B,
        'print_screen': 0x2C,
        'ins': 0x2D,
        'del': 0x2E,
        'help': 0x2F,
        
        # 数字键
        '0': 0x30, '1': 0x31, '2': 0x32, '3': 0x33, '4': 0x34,
        '5': 0x35, '6': 0x36, '7': 0x37, '8': 0x38, '9': 0x39,
        
        # 字母键
        'a': 0x41, 'b': 0x42, 'c': 0x43, 'd': 0x44, 'e': 0x45,
        'f': 0x46, 'g': 0x47, 'h': 0x48, 'i': 0x49, 'j': 0x4A,
        'k': 0x4B, 'l': 0x4C, 'm': 0x4D, 'n': 0x4E, 'o': 0x4F,
        'p': 0x50, 'q': 0x51, 'r': 0x52, 's': 0x53, 't': 0x54,
        'u': 0x55, 'v': 0x56, 'w': 0x57, 'x': 0x58, 'y': 0x59,
        'z': 0x5A,
        
        # 数字键盘
        'numpad_0': 0x60, 'numpad_1': 0x61, 'numpad_2': 0x62, 'numpad_3': 0x63,
        'numpad_4': 0x64, 'numpad_5': 0x65, 'numpad_6': 0x66, 'numpad_7': 0x67,
        'numpad_8': 0x68, 'numpad_9': 0x69,
        'multiply_key': 0x6A,
        'add_key': 0x6B,
        'separator_key': 0x6C,
        'subtract_key': 0x6D,
        'decimal_key': 0x6E,
        'divide_key': 0x6F,
        
        # F键
        'F1': 0x70, 'F2': 0x71, 'F3': 0x72, 'F4': 0x73, 'F5': 0x74,
        'F6': 0x75, 'F7': 0x76, 'F8': 0x77, 'F9': 0x78, 'F10': 0x79,
        'F11': 0x7A, 'F12': 0x7B, 'F13': 0x7C, 'F14': 0x7D, 'F15': 0x7E,
        'F16': 0x7F, 'F17': 0x80, 'F18': 0x81, 'F19': 0x82, 'F20': 0x83,
        'F21': 0x84, 'F22': 0x85, 'F23': 0x86, 'F24': 0x87,
        
        # 锁定键
        'num_lock': 0x90,
        'scroll_lock': 0x91,
        
        # Shift, Ctrl 等修饰键
        'left_shift': 0xA0,
        'right_shift': 0xA1,
        'left_control': 0xA2,
        'right_control': 0xA3,
        'left_menu': 0xA4,
        'right_menu': 0xA5,
        
        # 浏览器控制键
        'browser_back': 0xA6,
        'browser_forward': 0xA7,
        'browser_refresh': 0xA8,
        'browser_stop': 0xA9,
        'browser_search': 0xAA,
        'browser_favorites': 0xAB,
        'browser_start_and_home': 0xAC,
        
        # 音量控制键
        'volume_mute': 0xAD,
        'volume_down': 0xAE,
        'volume_up': 0xAF,
        
        # 媒体控制键
        'next_track': 0xB0,
        'previous_track': 0xB1,
        'stop_media': 0xB2,
        'play/pause_media': 0xB3,
        'start_mail': 0xB4,
        'select_media': 0xB5,
        'start_application_1': 0xB6,
        'start_application_2': 0xB7,
        
        # 特殊功能键
        'attn_key': 0xF6,
        'crsel_key': 0xF7,
        'exsel_key': 0xF8,
        'play_key': 0xFA,
        'zoom_key': 0xFB,
        'clear_key': 0xFE,
        
        # 符号键
        '+': 0xBB,
        ',': 0xBC,
        '-': 0xBD,
        '.': 0xBE,
        '/': 0xBF,
        '`': 0xC0,
        ';': 0xBA,
        '[': 0xDB,
        '\\': 0xDC,
        ']': 0xDD,
        "'": 0xDE,
        
        # 添加更多特殊符号映射
        '~': 0xC0,  # 与`共用一个键位
        '!': 0x31,  # 与1共用一个键位
        '@': 0x32,  # 与2共用一个键位
        '#': 0x33,  # 与3共用一个键位
        '$': 0x34,  # 与4共用一个键位
        '%': 0x35,  # 与5共用一个键位
        '^': 0x36,  # 与6共用一个键位
        '&': 0x37,  # 与7共用一个键位
        '*': 0x38,  # 与8共用一个键位
        '(': 0x39,  # 与9共用一个键位
        ')': 0x30,  # 与0共用一个键位
        '_': 0xBD,  # 与-共用一个键位
        '+': 0xBB,  # 与=共用一个键位
        '{': 0xDB,  # 与[共用一个键位
        '}': 0xDD,  # 与]共用一个键位
        '|': 0xDC,  # 与\共用一个键位
        ':': 0xBA,  # 与;共用一个键位
        '"': 0xDE,  # 与'共用一个键位
        '<': 0xBC,  # 与,共用一个键位
        '>': 0xBE,  # 与.共用一个键位
        '?': 0xBF,  # 与/共用一个键位
    }
    
    # 添加需要Shift的字符集合
    SHIFT_CHARS = {
        '~', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', 
        '_', '+', '{', '}', '|', ':', '"', '<', '>', '?'
    }
    
    @classmethod
    def get_vk_code(cls, key):
        """获取虚拟键码
        
        Args:
            key: 按键名称
            
        Returns:
            int: 虚拟键码，如果不存在返回None
        """
        if not key:
            return None
            
        # 空格字符特殊处理
        if key == ' ':
            return cls.VK_CODE['spacebar']
            
        key = key.upper()
        
        # 处理功能键 (F1-F24)
        if key.startswith('F') and len(key) <= 3:
            try:
                num = int(key[1:])
                if 1 <= num <= 24:  # 支持到F24
                    # 直接返回对应的键码，F键在字典中就是大写的
                    return cls.VK_CODE.get(key)
            except ValueError:
                pass
        
        # 处理字母键 (A-Z)
        if len(key) == 1 and 'A' <= key <= 'Z':
            return cls.VK_CODE[key.lower()]
        
        # 处理数字键 (0-9)
        if len(key) == 1 and '0' <= key <= '9':
            return cls.VK_CODE[key]
        
        # 处理特殊键
        special_keys = {
            'ENTER': 'enter',
            'RETURN': 'enter',
            'SPACE': 'spacebar',
            'TAB': 'tab',
            'SHIFT': 'shift',
            'CTRL': 'ctrl',
            'CONTROL': 'ctrl',
            'ALT': 'alt',
            'ESC': 'esc',
            'ESCAPE': 'esc',
            'BACKSPACE': 'backspace',
            'DELETE': 'del',
            'INSERT': 'ins',
            'HOME': 'home',
            'END': 'end',
            'PAGEUP': 'page_up',
            'PAGEDOWN': 'page_down',
            'UP': 'up_arrow',
            'DOWN': 'down_arrow',
            'LEFT': 'left_arrow',
            'RIGHT': 'right_arrow'
        }
        
        if key in special_keys:
            mapped_key = special_keys[key]
            return cls.VK_CODE.get(mapped_key)
            
        # 如果是直接的虚拟键码映射
        return cls.VK_CODE.get(key.lower())
    
    @classmethod
    def is_valid_key(cls, key):
        """检查是否是有效的按键"""
        if key == ' ':  # 空格字符特殊处理
            return True
        return key.lower() in cls.VK_CODE
    
    @classmethod
    def needs_shift(cls, char):
        """检查字符是否需要按住Shift键"""
        return char in cls.SHIFT_CHARS

