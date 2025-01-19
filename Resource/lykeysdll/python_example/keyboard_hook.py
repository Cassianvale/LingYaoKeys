import ctypes
from ctypes import wintypes
import win32con
import win32api
import win32gui
import threading
import logging
import sys
import os

# 定义KBDLLHOOKSTRUCT结构体
class KBDLLHOOKSTRUCT(ctypes.Structure):
    _fields_ = [
        ("vkCode", wintypes.DWORD),
        ("scanCode", wintypes.DWORD),
        ("flags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", ctypes.POINTER(wintypes.ULONG))
    ]

class KeyboardHook:
    """全局键盘钩子类"""
    
    # Windows钩子类型
    WH_KEYBOARD_LL = 13
    
    # 回调函数类型
    HOOKPROC = ctypes.WINFUNCTYPE(
        wintypes.LPARAM,
        ctypes.c_int,
        wintypes.WPARAM,
        wintypes.LPARAM
    )
    
    def __init__(self):
        """初始化键盘钩子"""
        self.user32 = ctypes.windll.user32
        self.kernel32 = ctypes.windll.kernel32
        
        # 定义CallNextHookEx的参数类型
        self.user32.CallNextHookEx.argtypes = [
            wintypes.HHOOK,
            ctypes.c_int,
            wintypes.WPARAM,
            wintypes.LPARAM
        ]
        self.user32.CallNextHookEx.restype = wintypes.LPARAM
        
        self.hook_id = None
        self.hook_thread = None
        self.is_running = False
        self._hook_callback_ptr = None  # 保持回调函数的引用
        
        # 热键回调字典 {vk_code: callback_func}
        self.hotkey_callbacks = {}
        
        # 当前按下的按键集合
        self.pressed_keys = set()
        
    def _hook_callback(self, nCode, wParam, lParam):
        """钩子回调函数"""
        if nCode >= 0:
            try:
                kb_struct = ctypes.cast(lParam, ctypes.POINTER(KBDLLHOOKSTRUCT)).contents
                vk_code = kb_struct.vkCode
                
                # 按键按下
                if wParam == win32con.WM_KEYDOWN:
                    self.pressed_keys.add(vk_code)
                    # 检查是否触发了任何热键组合
                    for hotkey_combo, callback in self.hotkey_callbacks.items():
                        if isinstance(hotkey_combo, tuple):
                            # 组合键
                            if all(key in self.pressed_keys for key in hotkey_combo):
                                callback()
                        else:
                            # 单个键
                            if vk_code == hotkey_combo:
                                callback()
                
                # 按键释放
                elif wParam == win32con.WM_KEYUP:
                    if vk_code in self.pressed_keys:
                        self.pressed_keys.remove(vk_code)
                
            except Exception as e:
                logging.error(f"键盘钩子回调错误: {str(e)}")
        
        # 继续传递给其他钩子
        return self.user32.CallNextHookEx(self.hook_id, nCode, wParam, lParam)
    
    def register_hotkey(self, vk_code, callback):
        """注册热键回调
        
        Args:
            vk_code: 虚拟键码或键码元组(组合键)
            callback: 回调函数
        """
        self.hotkey_callbacks[vk_code] = callback
    
    def unregister_hotkey(self, vk_code):
        """注销热键回调"""
        if vk_code in self.hotkey_callbacks:
            del self.hotkey_callbacks[vk_code]
    
    def start(self):
        """启动键盘钩子"""
        if self.is_running:
            return
            
        def run_hook():
            try:
                # 创建钩子回调函数并保持引用
                self._hook_callback_ptr = self.HOOKPROC(self._hook_callback)
                
                # 获取当前模块句柄
                if hasattr(sys, 'frozen'):
                    # 如果是打包后的可执行文件
                    module_handle = self.kernel32.GetModuleHandleW(None)
                else:
                    # 如果是 Python 脚本，使用当前进程的句柄
                    module_handle = 0  # 对于低级钩子，可以使用0
                
                # 安装钩子
                self.hook_id = self.user32.SetWindowsHookExW(
                    self.WH_KEYBOARD_LL,
                    self._hook_callback_ptr,
                    module_handle,
                    0
                )
                
                if not self.hook_id:
                    raise ctypes.WinError()
                
                self.is_running = True
                logging.info("键盘钩子已启动")
                
                # 消息循环
                msg = wintypes.MSG()
                while self.is_running and self.user32.GetMessageW(ctypes.byref(msg), None, 0, 0) > 0:
                    self.user32.TranslateMessage(ctypes.byref(msg))
                    self.user32.DispatchMessageW(ctypes.byref(msg))
                    
            except Exception as e:
                logging.error(f"键盘钩子启动错误: {str(e)}")
                self.is_running = False
        
        # 在新线程中运行钩子
        self.hook_thread = threading.Thread(target=run_hook, daemon=True)
        self.hook_thread.start()
    
    def stop(self):
        """停止键盘钩子"""
        if not self.is_running:
            return
            
        try:
            self.is_running = False
            
            # 卸载钩子
            if self.hook_id:
                if not self.user32.UnhookWindowsHookEx(self.hook_id):
                    raise ctypes.WinError()
                self.hook_id = None
            
            # 发送退出消息
            self.user32.PostThreadMessageW(
                self.hook_thread.ident,
                win32con.WM_QUIT,
                0,
                0
            )
            
            # 等待线程结束
            if self.hook_thread and self.hook_thread.is_alive():
                self.hook_thread.join(timeout=1.0)
            
            self.hook_thread = None
            self._hook_callback_ptr = None  # 清除回调函数引用
            logging.info("键盘钩子已停止")
            
        except Exception as e:
            logging.error(f"键盘钩子停止错误: {str(e)}") 