# -*- coding: utf-8 -*-

import time
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
import threading
from driver_manager import DriverManager
from input_tester import InputTester
from virtuakeys_mapping import VirtualKeys
import logging
import os
import json
import sys
import ctypes
from ctypes import wintypes
import argparse
import win32gui
import win32api
import win32con
import math
import random
from keyboard_hook import KeyboardHook

# 添加 MONITORINFO 结构体定义
class MONITORINFO(ctypes.Structure):
    _fields_ = [
        ("cbSize", wintypes.DWORD),
        ("rcMonitor", wintypes.RECT),
        ("rcWork", wintypes.RECT),
        ("dwFlags", wintypes.DWORD)
    ]

# 添加命令行参数解析
# python gui.py --debug
parser = argparse.ArgumentParser()
parser.add_argument('--debug', action='store_true', help='启用调试模式')
args = parser.parse_args()

# 使用命令行参数设置调试模式
DEBUG_MODE = args.debug

# 在类外部或文件开头定义常量
SW_HIDE = 0
SW_SHOWNORMAL = 1
SW_MINIMIZE = 6
SW_MAXIMIZE = 3

class LYKeysGUI:
    def __init__(self):
        """初始化GUI"""
        # 权限检查
        if not self.check_privileges():
            sys.exit(0)
            
        # 创建主窗口（必须最先创建）
        self.root = tk.Tk()
        self.root.title("LingYaoDriver测试工具")
        self.root.geometry("1000x1000")
        
        # 初始化基本变量
        # 驱动相关
        self.driver_mgr = None
        self.input_tester = None
        self.is_driver_loaded = False
        self.is_closing = False
        self.config_file = "driver_config.json"
        
        # 路径配置变量（在创建根窗口后创建）
        self.dll_path_var = tk.StringVar()
        self.sys_path_var = tk.StringVar()
        
        # 测试相关变量
        self.test_key_var = tk.StringVar(value="a")
        self.press_time_var = tk.StringVar(value="1")
        self.interval_time_var = tk.StringVar(value="1")
        self.duration_var = tk.StringVar(value="0")
        
        # 自动移动相关变量
        self.keyboard_hook = None
        self.auto_move_running = False
        self.auto_move_config = {
            'hotkey': 'F8',
            'speed': 10,
            'range': 100
        }
        
        # 配置界面样式
        # 设置全局字体
        self.default_font = ('Microsoft YaHei UI', 10)
        self.style = ttk.Style()
        self.style.configure('.', font=self.default_font)
        
        # 配置各种部件字体
        for widget in ['TLabel', 'TButton', 'TEntry', 'TCheckbutton', 'TRadiobutton']:
            self.style.configure(widget, font=self.default_font)
        self.style.configure('TLabelframe.Label', font=self.default_font)
        self.style.configure('TNotebook.Tab', font=self.default_font)
        
        # 创建主界面布局
        # 配置根窗口网格
        self.root.grid_rowconfigure(0, weight=1)
        self.root.grid_columnconfigure(0, weight=1)
        
        # 创建分隔窗口
        self.paned = ttk.PanedWindow(self.root, orient=tk.HORIZONTAL)
        self.paned.grid(row=0, column=0, sticky="nsew", padx=5, pady=5)
        
        # 创建左右面板
        self.left_panel = ttk.Frame(self.paned)
        self.right_panel = ttk.Frame(self.paned)
        self.paned.add(self.left_panel, weight=2)
        self.paned.add(self.right_panel, weight=3)
        
        # 创建标签页容器
        self.notebook = ttk.Notebook(self.left_panel)
        self.notebook.pack(fill=tk.BOTH, expand=True)
        
        # 初始化功能组件
        # 设置日志处理器
        self._setup_logging()
        
        # 加载配置
        self.load_path_config()
        self.load_auto_move_config()
        
        # 如果没有配置路径，显示提示消息
        if not self.dll_path_var.get() or not self.sys_path_var.get():
            logging.warning("请先在驱动路径配置中设置DLL和SYS文件路径")
        
        # 创建功能标签页
        self.create_driver_tab()
        self.create_input_test_tab()
        self.create_rapid_test_tab()
        self.create_mouse_test_tab()
        
        # 绑定事件
        self.root.protocol("WM_DELETE_WINDOW", self._on_closing)
        
    def create_driver_tab(self):
        """创建驱动控制标签页"""
        driver_tab = ttk.Frame(self.notebook)
        self.notebook.add(driver_tab, text="驱动控制")

        # 驱动控制区域
        driver_frame = ttk.LabelFrame(driver_tab, text="驱动控制", padding="5")
        driver_frame.pack(fill=tk.X, padx=5, pady=5)
        
        self.load_btn = ttk.Button(driver_frame, text="安装驱动", command=self._load_driver)
        self.load_btn.pack(side=tk.LEFT, padx=5)
        
        self.unload_btn = ttk.Button(driver_frame, text="卸载驱动", command=self._unload_driver, state=tk.DISABLED)
        self.unload_btn.pack(side=tk.LEFT, padx=5)
        
        # 状态检查区域
        status_frame = ttk.LabelFrame(driver_tab, text="驱动状态", padding="5")
        status_frame.pack(fill=tk.X, padx=5, pady=5)
        
        self.driver_status = ttk.Label(status_frame, text="驱动状态: 未知")
        self.driver_status.pack(fill=tk.X, padx=5, pady=2)
        
        self.last_check = ttk.Label(status_frame, text="上次检查: 未检查")
        self.last_check.pack(fill=tk.X, padx=5, pady=2)
        
        self.check_status_btn = ttk.Button(status_frame, text="检查驱动状态", 
                                       command=self._check_driver_status, 
                                       state=tk.DISABLED,
                                       width=15)
        self.check_status_btn.pack(side=tk.LEFT, padx=5, pady=2)

        # 路径配置区域
        self.create_path_frame(driver_tab)

    def create_input_test_tab(self):
        """创建输入测试标签页"""
        input_tab = ttk.Frame(self.notebook)
        self.notebook.add(input_tab, text="输入测试")

        # 输入/输出区域
        io_frame = ttk.LabelFrame(input_tab, text="输入/输出", padding="5")
        io_frame.pack(fill=tk.X, padx=5, pady=5)
        
        # 输入区域
        input_frame = ttk.LabelFrame(io_frame, text="输入区域", padding="5")
        input_frame.pack(fill=tk.X, padx=5, pady=(5,2))
        
        # 创建固定宽度的输入框
        self.input_entry = ttk.Entry(input_frame, width=25, font=self.default_font)  # 设置字体
        self.input_entry.pack(side=tk.LEFT, padx=5, pady=5)
        self.input_entry.bind('<Return>', self._on_input_submit)
        
        # 输入区域按钮
        button_frame = ttk.Frame(input_frame)
        button_frame.pack(side=tk.LEFT, padx=5)
        
        self.submit_btn = ttk.Button(button_frame, text="发送", command=self._on_submit_click, width=8)
        self.submit_btn.pack(side=tk.LEFT)
        
        self.clear_btn = ttk.Button(button_frame, text="清除", command=self._clear_text, width=8)
        self.clear_btn.pack(side=tk.LEFT, padx=5)
        
        # 输出区域
        output_frame = ttk.LabelFrame(io_frame, text="输出区域", padding="5")
        output_frame.pack(fill=tk.X, padx=5, pady=(2,5))
        
        # 创建固定宽度的输出框
        self.output_entry = ttk.Entry(output_frame, state='readonly', width=25, font=self.default_font)  # 设置字体
        self.output_entry.pack(side=tk.LEFT, padx=5, pady=5)

        # 测试功能区域
        test_frame = ttk.LabelFrame(input_tab, text="测试功能", padding="5")
        test_frame.pack(fill=tk.X, padx=5, pady=5)
        
        # 创建按钮容器
        test_button_frame = ttk.Frame(test_frame)
        test_button_frame.pack(side=tk.LEFT, padx=5, pady=5)
        
        self.test_key_btn = ttk.Button(test_button_frame, text="按键测试", 
                                     command=self._test_keyboard, 
                                     state=tk.DISABLED, 
                                     width=15)
        self.test_key_btn.pack(side=tk.LEFT, padx=5)
        
        self.test_input_btn = ttk.Button(test_button_frame, text="输入测试", 
                                       command=self._test_input, 
                                       state=tk.DISABLED, 
                                       width=15)
        self.test_input_btn.pack(side=tk.LEFT, padx=5)
        
        self.test_mouse_btn = ttk.Button(test_button_frame, text="鼠标测试", 
                                       command=self._test_mouse, 
                                       state=tk.DISABLED, 
                                       width=15)
        self.test_mouse_btn.pack(side=tk.LEFT, padx=5)

    def create_rapid_test_tab(self):
        """创建高频测试标签页"""
        rapid_tab = ttk.Frame(self.notebook)
        self.notebook.add(rapid_tab, text="高频测试")

        # 创建主容器
        main_container = ttk.Frame(rapid_tab)
        main_container.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        # 创建左右分栏
        settings_container = ttk.Frame(main_container)
        settings_container.pack(fill=tk.BOTH, expand=True)
        
        left_settings = ttk.Frame(settings_container)
        left_settings.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(0,5))
        
        right_settings = ttk.Frame(settings_container)
        right_settings.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(5,0))

        # 左侧 - 按键和时间设置
        # 按键选择
        key_frame = ttk.LabelFrame(left_settings, text="按键设置", padding="5")
        key_frame.pack(fill=tk.X, pady=(0,5))
        
        ttk.Label(key_frame, text="测试按键:").pack(side=tk.LEFT, padx=5)
        self.test_key_var = tk.StringVar(value="a")
        self.test_key_entry = ttk.Entry(key_frame, textvariable=self.test_key_var, width=8, font=self.default_font)
        self.test_key_entry.pack(side=tk.LEFT, padx=5)

        # 时间设置框架
        time_frame = ttk.LabelFrame(left_settings, text="时间设置(毫秒)", padding="5")
        time_frame.pack(fill=tk.X)

        # 按下时间设置
        press_frame = ttk.Frame(time_frame)
        press_frame.pack(fill=tk.X, pady=2)
        ttk.Label(press_frame, text="按下抬起间隔:").pack(side=tk.LEFT, padx=5)
        self.press_time_var = tk.StringVar(value="1")
        self.press_time_entry = ttk.Entry(press_frame, textvariable=self.press_time_var, width=8, font=self.default_font)
        self.press_time_entry.pack(side=tk.LEFT, padx=5)

        # 间隔时间设置
        interval_frame = ttk.Frame(time_frame)
        interval_frame.pack(fill=tk.X, pady=2)
        ttk.Label(interval_frame, text="等待间隔:").pack(side=tk.LEFT, padx=5)
        self.interval_time_var = tk.StringVar(value="1")
        self.interval_time_entry = ttk.Entry(interval_frame, textvariable=self.interval_time_var, width=8, font=self.default_font)
        self.interval_time_entry.pack(side=tk.LEFT, padx=5)

        # 右侧 - 运行时长和状态显示
        # 运行时长设置
        duration_frame = ttk.LabelFrame(right_settings, text="运行设置", padding="5")
        duration_frame.pack(fill=tk.X, pady=(0,5))
        
        ttk.Label(duration_frame, text="运行时长(秒):").pack(side=tk.LEFT, padx=5)
        self.duration_var = tk.StringVar(value="1")
        self.duration_entry = ttk.Entry(duration_frame, textvariable=self.duration_var, width=8, font=self.default_font)
        self.duration_entry.pack(side=tk.LEFT, padx=5)

        # 状态显示框架
        status_frame = ttk.LabelFrame(right_settings, text="运行状态", padding="5")
        status_frame.pack(fill=tk.X)

        # 创建左右两列的容器
        status_left = ttk.Frame(status_frame)
        status_left.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=5)
        
        status_right = ttk.Frame(status_frame)
        status_right.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=5)

        # 左列 - 状态和运行时间
        self.rapid_test_status = ttk.Label(status_left, text="状态: 未开始")
        self.rapid_test_status.pack(fill=tk.X, pady=2)
        
        self.run_time_label = ttk.Label(status_left, text="运行时间: 0秒")
        self.run_time_label.pack(fill=tk.X, pady=2)
        
        # 右列 - 按键次数和频率
        self.press_count = 0
        self.press_count_label = ttk.Label(status_right, text="按键次数: 0")
        self.press_count_label.pack(fill=tk.X, pady=2)
        
        self.click_rate_label = ttk.Label(status_right, text="平均频率: 0次/秒")
        self.click_rate_label.pack(fill=tk.X, pady=2)

        # 控制按钮 - 垂直布局
        control_frame = ttk.Frame(main_container)
        control_frame.pack(fill=tk.X, pady=(10,0))
        
        # 创建一个容器来居中放置按钮
        button_container = ttk.Frame(control_frame)
        button_container.pack(anchor=tk.CENTER)
        
        self.rapid_test_btn = ttk.Button(button_container, 
                                       text="开始测试", 
                                       command=self._toggle_rapid_test, 
                                       width=15)
        self.rapid_test_btn.pack(pady=5)

    def create_mouse_test_tab(self):
        """创建鼠标测试标签页"""
        mouse_tab = ttk.Frame(self.notebook)
        self.notebook.add(mouse_tab, text="鼠标测试")

        # 创建主容器
        main_container = ttk.Frame(mouse_tab)
        main_container.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        # 鼠标点击测试区域
        click_frame = ttk.LabelFrame(main_container, text="鼠标点击测试")
        click_frame.pack(fill=tk.X, padx=5, pady=5)

        # 基本按键测试
        basic_click_frame = ttk.Frame(click_frame)
        basic_click_frame.pack(fill=tk.X, padx=5, pady=5)
        
        # 创建按钮容器并居中
        button_container = ttk.Frame(basic_click_frame)
        button_container.pack(anchor=tk.CENTER)
        
        # 鼠标按键测试按钮 - 2x2布局
        click_buttons = [
            ("左键点击", "left"),
            ("右键点击", "right"),
            ("中键点击", "middle"),
            ("X1键点击", "x1")
        ]
        
        for i, (btn_text, btn_cmd) in enumerate(click_buttons):
            row = i // 2  # 整除得到行号
            col = i % 2   # 取余得到列号
            btn = ttk.Button(button_container, 
                           text=btn_text, 
                           command=lambda cmd=btn_cmd: self._test_mouse_click(cmd),
                           width=15)
            btn.grid(row=row, column=col, padx=5, pady=2)
            
        # X2键点击按钮单独放在下面居中
        ttk.Button(button_container, 
                  text="X2键点击", 
                  command=lambda: self._test_mouse_click("x2"),
                  width=15).grid(row=2, column=0, columnspan=2, padx=5, pady=2)

        # 鼠标移动测试区域
        move_frame = ttk.LabelFrame(main_container, text="鼠标移动测试")
        move_frame.pack(fill=tk.X, padx=5, pady=5)

        # 移动到四角的按钮
        corner_frame = ttk.LabelFrame(move_frame, text="移动到屏幕角落")
        corner_frame.pack(fill=tk.X, padx=5, pady=5)
        
        # 创建按钮容器并居中
        corner_button_container = ttk.Frame(corner_frame)
        corner_button_container.pack(anchor=tk.CENTER)
        
        # 四角移动按钮 - 2x2布局
        corner_buttons = [
            ("左上角", "top_left"),
            ("右上角", "top_right"),
            ("左下角", "bottom_left"),
            ("右下角", "bottom_right")
        ]
        
        for i, (btn_text, btn_cmd) in enumerate(corner_buttons):
            row = i // 2  # 整除得到行号
            col = i % 2   # 取余得到列号
            btn = ttk.Button(corner_button_container, 
                           text=btn_text, 
                           command=lambda cmd=btn_cmd: self._move_to_corner(cmd),
                           width=15)
            btn.grid(row=row, column=col, padx=5, pady=2)

        # 相对移动测试
        rel_move_frame = ttk.LabelFrame(move_frame, text="相对平滑移动测试")
        rel_move_frame.pack(fill=tk.X, padx=5, pady=5)

        # 相对移动距离设置
        distance_frame = ttk.Frame(rel_move_frame)
        distance_frame.pack(anchor=tk.CENTER, pady=5)
        
        ttk.Label(distance_frame, text="移动距离:").pack(side=tk.LEFT, padx=5)
        self.rel_distance_var = tk.StringVar(value="100")
        ttk.Entry(distance_frame, textvariable=self.rel_distance_var, width=8, font=self.default_font).pack(side=tk.LEFT, padx=5)

        # 相对移动方向按钮
        direction_frame = ttk.Frame(rel_move_frame)
        direction_frame.pack(anchor=tk.CENTER, pady=5)
        
        # 上方向按钮
        ttk.Button(direction_frame, text="↑", 
                  command=lambda: self._test_smooth_move_rel("up"),
                  width=8).pack(pady=2)
        
        # 左右方向按钮
        lr_frame = ttk.Frame(direction_frame)
        lr_frame.pack(pady=2)
        ttk.Button(lr_frame, text="←", 
                  command=lambda: self._test_smooth_move_rel("left"),
                  width=8).pack(side=tk.LEFT, padx=2)
        ttk.Button(lr_frame, text="→", 
                  command=lambda: self._test_smooth_move_rel("right"),
                  width=8).pack(side=tk.LEFT, padx=2)
        
        # 下方向按钮
        ttk.Button(direction_frame, text="↓", 
                  command=lambda: self._test_smooth_move_rel("down"),
                  width=8).pack(pady=2)

        # 绝对移动测试
        abs_move_frame = ttk.LabelFrame(move_frame, text="绝对平滑移动测试")
        abs_move_frame.pack(fill=tk.X, padx=5, pady=5)

        # 目标坐标设置
        coords_frame = ttk.Frame(abs_move_frame)
        coords_frame.pack(anchor=tk.CENTER, pady=5)
        
        ttk.Label(coords_frame, text="X:").pack(side=tk.LEFT, padx=5)
        self.abs_x_var = tk.StringVar(value="500")
        ttk.Entry(coords_frame, textvariable=self.abs_x_var, width=8, font=self.default_font).pack(side=tk.LEFT, padx=5)

        ttk.Label(coords_frame, text="Y:").pack(side=tk.LEFT, padx=5)
        self.abs_y_var = tk.StringVar(value="500")
        ttk.Entry(coords_frame, textvariable=self.abs_y_var, width=8, font=self.default_font).pack(side=tk.LEFT, padx=5)

        # 移动按钮
        move_button_frame = ttk.Frame(abs_move_frame)
        move_button_frame.pack(anchor=tk.CENTER, pady=5)
        
        ttk.Button(move_button_frame, 
                  text="移动到指定位置", 
                  command=self._test_smooth_move_abs,
                  width=20).pack()

        # 鼠标滚轮测试区域
        wheel_frame = ttk.LabelFrame(main_container, text="鼠标滚轮测试")
        wheel_frame.pack(fill=tk.X, padx=5, pady=5)

        # 滚动量设置
        wheel_settings_frame = ttk.Frame(wheel_frame)
        wheel_settings_frame.pack(anchor=tk.CENTER, pady=5)
        
        ttk.Label(wheel_settings_frame, text="滚动量:").pack(side=tk.LEFT, padx=5)
        self.wheel_delta_var = tk.StringVar(value="120")
        ttk.Entry(wheel_settings_frame, textvariable=self.wheel_delta_var, width=8, font=self.default_font).pack(side=tk.LEFT, padx=5)

        # 滚轮测试按钮
        wheel_button_frame = ttk.Frame(wheel_frame)
        wheel_button_frame.pack(anchor=tk.CENTER, pady=5)
        
        ttk.Button(wheel_button_frame, 
                  text="向上滚动", 
                  command=lambda: self._test_mouse_wheel("up"),
                  width=15).pack(pady=2)
        ttk.Button(wheel_button_frame, 
                  text="向下滚动", 
                  command=lambda: self._test_mouse_wheel("down"),
                  width=15).pack(pady=2)

        # 添加自动移动设置区域
        auto_move_frame = ttk.LabelFrame(main_container, text="自动移动设置")
        auto_move_frame.pack(fill=tk.X, padx=5, pady=5)

        # 热键设置
        hotkey_frame = ttk.Frame(auto_move_frame)
        hotkey_frame.pack(fill=tk.X, padx=5, pady=5)
        
        ttk.Label(hotkey_frame, text="触发热键:").pack(side=tk.LEFT, padx=5)
        self.hotkey_var = tk.StringVar(value=self.auto_move_config['hotkey'])
        self.hotkey_entry = ttk.Entry(hotkey_frame, textvariable=self.hotkey_var, width=8, font=self.default_font)
        self.hotkey_entry.pack(side=tk.LEFT, padx=5)
        
        # 移动速度设置
        speed_frame = ttk.Frame(auto_move_frame)
        speed_frame.pack(fill=tk.X, padx=5, pady=5)
        
        ttk.Label(speed_frame, text="移动速度:").pack(side=tk.LEFT, padx=5)
        self.speed_var = tk.StringVar(value=str(self.auto_move_config['speed']))
        self.speed_entry = ttk.Entry(speed_frame, textvariable=self.speed_var, width=8, font=self.default_font)
        self.speed_entry.pack(side=tk.LEFT, padx=5)
        
        # 移动范围设置
        range_frame = ttk.Frame(auto_move_frame)
        range_frame.pack(fill=tk.X, padx=5, pady=5)
        
        ttk.Label(range_frame, text="移动范围:").pack(side=tk.LEFT, padx=5)
        self.range_var = tk.StringVar(value=str(self.auto_move_config['range']))
        self.range_entry = ttk.Entry(range_frame, textvariable=self.range_var, width=8, font=self.default_font)
        self.range_entry.pack(side=tk.LEFT, padx=5)
        
        # 状态显示
        status_frame = ttk.Frame(auto_move_frame)
        status_frame.pack(fill=tk.X, padx=5, pady=5)
        
        self.auto_move_status = ttk.Label(status_frame, text="状态: 未启动")
        self.auto_move_status.pack(side=tk.LEFT, padx=5)
        
        # 控制按钮
        button_frame = ttk.Frame(auto_move_frame)
        button_frame.pack(fill=tk.X, padx=5, pady=5)
        
        self.start_hook_btn = ttk.Button(button_frame, 
                                       text="启动热键监听", 
                                       command=self._toggle_keyboard_hook,
                                       width=15)
        self.start_hook_btn.pack(side=tk.LEFT, padx=5)
        
        ttk.Button(button_frame, 
                  text="保存设置", 
                  command=self.save_auto_move_config,
                  width=15).pack(side=tk.LEFT, padx=5)

    def _on_closing(self):
        """处理窗口关闭事件"""
        self.is_closing = True
        
        # 停止键盘钩子
        if self.keyboard_hook:
            self.keyboard_hook.stop()
            
        # 停止自动移动
        self.auto_move_running = False
        
        if self.driver_mgr:
            try:
                self.driver_mgr.cleanup()
            except Exception as e:
                print(f"清理驱动时出错: {str(e)}")  # 使用print而不是logging
        self.root.destroy()
    
    def _setup_logging(self):
        """设置日志处理"""
        class TextHandler(logging.Handler):
            def __init__(self, text_widget, gui):
                logging.Handler.__init__(self)
                self.text_widget = text_widget
                self.gui = gui
                
            def emit(self, record):
                if not self.gui.is_closing:  # 只在程序未关闭时输出日志
                    msg = self.format(record) + '\n'
                    # 使用after方法确保在主线程中更新UI
                    self.gui.root.after(0, self._insert_log, msg)
            
            def _insert_log(self, msg):
                """在主线程中插入日志并滚动"""
                self.text_widget.insert(tk.END, msg)
                self.text_widget.yview_moveto(1.0)  # 确保滚动到底部
        
        # 创建日志区域（放在右侧面板）
        self.log_frame = ttk.LabelFrame(self.right_panel, text="日志输出")
        self.log_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # 配置日志框架的grid权重
        self.log_frame.grid_rowconfigure(0, weight=1)
        self.log_frame.grid_columnconfigure(0, weight=1)
        
        # 创建文本框和滚动条
        self.log_text = tk.Text(self.log_frame, wrap=tk.WORD, width=50)  # 设置初始大小
        self.log_scrollbar = ttk.Scrollbar(self.log_frame, orient="vertical", command=self.log_text.yview)
        self.log_text.configure(yscrollcommand=self.log_scrollbar.set)
        
        # 设置文本框样式
        self.log_text.configure(font=self.default_font)  # 使用全局字体
        
        # 放置文本框和滚动条
        self.log_text.grid(row=0, column=0, sticky="nsew", padx=(5,0), pady=5)  # 添加内边距
        self.log_scrollbar.grid(row=0, column=1, sticky="ns", pady=5)
        
        # 配置日志处理
        handler = TextHandler(self.log_text, self)
        formatter = logging.Formatter('%(asctime)s - %(levelname)s - %(message)s')
        handler.setFormatter(formatter)
        logging.getLogger().addHandler(handler)
        logging.getLogger().setLevel(logging.INFO)
    
    def _update_button_states(self, driver_loaded):
        """更新按钮状态"""
        state = tk.NORMAL if driver_loaded else tk.DISABLED
        self.unload_btn.config(state=state)
        self.test_key_btn.config(state=state)
        self.test_input_btn.config(state=state)
        self.test_mouse_btn.config(state=state)
        self.submit_btn.config(state=state)
        self.clear_btn.config(state=state)
        self.input_entry.config(state=state)
        self.check_status_btn.config(state=state)
        self.load_btn.config(state=tk.DISABLED if driver_loaded else tk.NORMAL)
        self.rapid_test_btn.config(state=state)
    
    def _load_driver(self):
        """加载驱动"""
        try:
            # 获取当前设置的路径
            dll_path = self.dll_path_var.get()
            sys_path = self.sys_path_var.get()
            
            # 检查路径是否已配置
            if not dll_path or not sys_path:
                messagebox.showerror("错误", "请先配置驱动路径")
                return
            
            self.driver_mgr = DriverManager()
            
            # 使用配置的路径
            try:
                self.driver_mgr._setup_paths(dll_path=dll_path, sys_path=sys_path)
            except ValueError as ve:
                messagebox.showerror("错误", str(ve))
                return
            except FileNotFoundError as fe:
                messagebox.showerror("错误", str(fe))
                return
            
            if self.driver_mgr.initialize():
                self.input_tester = InputTester(self.driver_mgr.get_driver())
                self.is_driver_loaded = True
                self._update_button_states(True)
                self._check_driver_status()  # 初始检查驱动状态
            else:
                messagebox.showerror("错误", "驱动加载失败")
        except Exception as e:
            messagebox.showerror("错误", f"驱动加载出错: {str(e)}")
            logging.error(f"驱动加载出错: {str(e)}")
    
    def _unload_driver(self):
        """卸载驱动"""
        try:
            if self.driver_mgr:
                self.driver_mgr.cleanup()
                self.driver_mgr = None
                self.input_tester = None
                self.is_driver_loaded = False
                self._update_button_states(False)
                self.driver_status.config(text="驱动状态: 未加载")
                self.last_check.config(text="上次检查: 未检查")
        except Exception as e:
            messagebox.showerror("错误", f"驱动卸载出错: {str(e)}")
    
    def _test_keyboard(self):
        """测试键盘"""
        def run_test():
            try:
                # 测试一些基本按键
                logging.info(f"键盘测试3s后开始!")
                time.sleep(3)
                test_keys = ['w', 'a', 's', 'd', 'enter']
                for key in test_keys:
                    logging.info(f"测试按键: {key}")
                    self.input_tester.press_key(key)
                    time.sleep(0.5)
                logging.info("键盘测试完成")
            except Exception as e:
                logging.error(f"键盘测试出错: {str(e)}")
        
        threading.Thread(target=run_test, daemon=True).start()
    
    def _test_input(self):
        """测试输入固定字符串"""
        def run_test():
            try:
                logging.info("输入测试3s后开始!")
                time.sleep(3)
                test_string = "Hello, World!"
                logging.info(f"测试输入: {test_string}")
                
                for char in test_string:
                    if char.isupper():
                        self.input_tester.key_down('shift')
                        self.input_tester.press_key(char.lower())
                        self.input_tester.key_up('shift')
                    else:
                        self.input_tester.press_key(char)
                    time.sleep(0.1)
                
                logging.info("输入测试完成")
            except Exception as e:
                logging.error(f"输入测试出错: {str(e)}")
        
        threading.Thread(target=run_test, daemon=True).start()
    
    def _test_mouse(self):
        """测试鼠标移动"""
        def run_test():
            try:
                logging.info("鼠标测试3s后开始!")
                time.sleep(3)  # 给用户准备时间
                
                # 1. 相对移动测试
                logging.info("=== 开始相对移动测试 ===")
                
                # 基本方向测试
                moves = [
                    (100, 0, "右"),
                    (0, 100, "下"),
                    (-100, 0, "左"),
                    (0, -100, "上")
                ]
                
                for dx, dy, direction in moves:
                    logging.info(f"测试{direction}移动: ({dx}, {dy})")
                    self.input_tester.mouse_move_rel(dx, dy)
                    time.sleep(0.5)
                
                # 对角线移动测试
                diag_moves = [
                    (50, 50, "右下"),
                    (-50, 50, "左下"),
                    (-50, -50, "左上"),
                    (50, -50, "右上")
                ]
                
                for dx, dy, direction in diag_moves:
                    logging.info(f"测试{direction}对角移动: ({dx}, {dy})")
                    self.input_tester.mouse_move_rel(dx, dy)
                    time.sleep(0.5)
                
                # 小距离精确移动测试
                small_moves = [(5, 0), (0, 5), (-5, 0), (0, -5)]
                logging.info("测试小距离精确移动")
                for dx, dy in small_moves:
                    self.input_tester.mouse_move_rel(dx, dy)
                    time.sleep(0.3)
                
                # 2. 绝对移动测试
                logging.info("\n=== 开始绝对移动测试 ===")
                
                # 获取虚拟屏幕信息
                virtual_width = ctypes.windll.user32.GetSystemMetrics(78)  # SM_CXVIRTUALSCREEN
                virtual_height = ctypes.windll.user32.GetSystemMetrics(79)  # SM_CYVIRTUALSCREEN
                virtual_x = ctypes.windll.user32.GetSystemMetrics(76)  # SM_XVIRTUALSCREEN
                virtual_y = ctypes.windll.user32.GetSystemMetrics(77)  # SM_YVIRTUALSCREEN
                
                # 屏幕边界点测试
                border_points = [
                    (virtual_x, virtual_y, "左上角"),
                    (virtual_x + virtual_width - 1, virtual_y, "右上角"),
                    (virtual_x, virtual_y + virtual_height - 1, "左下角"),
                    (virtual_x + virtual_width - 1, virtual_y + virtual_height - 1, "右下角")
                ]
                
                for x, y, position in border_points:
                    logging.info(f"测试移动到屏幕{position}: ({x}, {y})")
                    self.input_tester.mouse_move_abs(x, y)
                    time.sleep(0.8)
                
                # 屏幕中心点测试
                center_x = virtual_x + virtual_width // 2
                center_y = virtual_y + virtual_height // 2
                logging.info(f"测试移动到屏幕中心: ({center_x}, {center_y})")
                self.input_tester.mouse_move_abs(center_x, center_y)
                time.sleep(0.8)
                
                # 多显示器测试（如果有）
                monitor_count = ctypes.windll.user32.GetSystemMetrics(80)  # SM_CMONITORS
                if monitor_count > 1:
                    logging.info(f"\n检测到{monitor_count}个显示器，开始多显示器测试")
                    
                    # 在每个显示器上进行测试
                    def enum_monitor_proc(hMonitor, hdcMonitor, lprcMonitor, dwData):
                        monitor_info = MONITORINFO()
                        monitor_info.cbSize = ctypes.sizeof(monitor_info)
                        ctypes.windll.user32.GetMonitorInfoW(hMonitor, ctypes.byref(monitor_info))
                        
                        # 获取显示器工作区
                        work_area = monitor_info.rcWork
                        center_x = (work_area.left + work_area.right) // 2
                        center_y = (work_area.top + work_area.bottom) // 2
                        
                        logging.info(f"测试显示器 工作区域: ({work_area.left}, {work_area.top}, {work_area.right}, {work_area.bottom})")
                        logging.info(f"移动到显示器中心点: ({center_x}, {center_y})")
                        self.input_tester.mouse_move_abs(center_x, center_y)
                        time.sleep(0.8)
                        
                        return True
                    
                    EnumMonitorProc = ctypes.WINFUNCTYPE(
                        ctypes.c_bool,
                        ctypes.c_ulong,
                        ctypes.c_ulong,
                        ctypes.POINTER(MONITORINFO),
                        ctypes.c_ulong
                    )
                    
                    callback = EnumMonitorProc(enum_monitor_proc)
                    ctypes.windll.user32.EnumDisplayMonitors(None, None, callback, 0)
                
                # 3. 鼠标按键测试
                logging.info("\n=== 开始鼠标按键测试 ===")
                
                # 左键测试
                logging.info("测试左键点击")
                self.input_tester.mouse_click()
                time.sleep(0.5)
                
                # 右键测试
                logging.info("测试右键点击")
                self.input_tester.mouse_right_click()
                time.sleep(0.5)
                
                # 中键测试
                logging.info("测试中键点击")
                self.input_tester.mouse_middle_click()
                time.sleep(0.5)
                
                # 额外按键测试
                logging.info("测试X1按键点击")
                self.input_tester.mouse_x1_click()
                time.sleep(0.5)
                
                logging.info("测试X2按键点击")
                self.input_tester.mouse_x2_click()
                time.sleep(0.5)
                
                logging.info("鼠标测试完成")
                
            except Exception as e:
                logging.error(f"鼠标测试出错: {str(e)}")
        
        threading.Thread(target=run_test, daemon=True).start()
    
    def _on_input_submit(self, event):
        """处理Enter键提交事件"""
        if self.is_driver_loaded:
            self._send_input_text()

    def _on_submit_click(self):
        """处理提交按钮点击事件"""
        if self.is_driver_loaded:
            self._send_input_text()

    def _clear_text(self):
        """清除输入和输出文本"""
        self.input_entry.delete(0, tk.END)
        self.output_entry.config(state='normal')
        self.output_entry.delete(0, tk.END)
        self.output_entry.config(state='readonly')

    def _send_input_text(self):
        """发送输入框中的文本"""
        text = self.input_entry.get()
        if not text:
            return
            
        def send_text():
            try:
                logging.info(f"发送文本: {text}")
                result = ""
                
                for char in text:
                    if VirtualKeys.needs_shift(char):
                        # 特殊字符处理
                        self.input_tester.press_key(char)
                    elif char.isupper():
                        # 大写字母处理
                        self.input_tester.key_down('shift')
                        self.input_tester.press_key(char.lower())
                        self.input_tester.key_up('shift')
                    else:
                        # 普通字符处理
                        self.input_tester.press_key(char)
                    result += char
                    # 更新输出框
                    self.root.after(0, self._update_output, result)
                    time.sleep(0.05)  # 适当的延迟
                
                logging.info("文本发送完成")
                # 清空输入框
                self.root.after(0, self.input_entry.delete, 0, tk.END)
                
            except Exception as e:
                logging.error(f"发送文本出错: {str(e)}")
        
        # 在新线程中执行发送操作
        threading.Thread(target=send_text, daemon=True).start()

    def _update_output(self, text):
        """更新输出框的文本"""
        self.output_entry.config(state='normal')
        self.output_entry.delete(0, tk.END)
        self.output_entry.insert(0, text)
        self.output_entry.config(state='readonly')
    
    def _get_status_text(self, status):
        """获取状态文本"""
        status_map = {
            0: "未知",
            1: "正常",
            2: "错误"
        }
        return status_map.get(status, "未知")
    
    def _check_driver_status(self):
        """检查驱动状态"""
        if not self.input_tester:
            return
            
        def check():
            try:
                logging.info("开始检查驱动状态")
                self.input_tester.check_device_status()
                
                # 获取状态
                driver_status = self.input_tester.get_driver_status()
                last_check = self.input_tester.get_last_check_time()
                
                # 更新显示
                self.root.after(0, lambda: self.driver_status.config(
                    text=f"驱动状态: {self._get_status_text(driver_status)}"))
                
                # 格式化时间（DLL返回的是Unix时间戳，毫秒为单位）
                if last_check > 0:
                    time_str = time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(last_check/1000))
                    self.root.after(0, lambda: self.last_check.config(
                        text=f"上次检查: {time_str}"))
                
                logging.info(f"驱动状态: {self._get_status_text(driver_status)}")
            except Exception as e:
                logging.error(f"检查驱动状态出错: {str(e)}")
        
        threading.Thread(target=check, daemon=True).start()
    
    def create_path_frame(self, parent=None):
        """创建路径配置区域
        
        Args:
            parent: 父级容器，如果为None则使用self.left_panel
        """
        # 如果没有指定父级容器，则使用left_panel
        if parent is None:
            parent = self.left_panel
            
        path_frame = ttk.LabelFrame(parent, text="驱动路径配置", padding="5")
        path_frame.pack(fill="x", padx=5, pady=5)
        
        # DLL路径配置
        ttk.Label(path_frame, text="DLL路径:").grid(row=0, column=0, sticky="w")
        # 使用已经在__init__中创建的StringVar
        self.dll_path_entry = ttk.Entry(path_frame, textvariable=self.dll_path_var, width=30, font=self.default_font)
        self.dll_path_entry.grid(row=0, column=1, padx=5)
        ttk.Button(path_frame, text="浏览", command=lambda: self.browse_file("dll")).grid(row=0, column=2)
        
        # SYS路径配置
        ttk.Label(path_frame, text="SYS路径:").grid(row=1, column=0, sticky="w")
        # 使用已经在__init__中创建的StringVar
        self.sys_path_entry = ttk.Entry(path_frame, textvariable=self.sys_path_var, width=30, font=self.default_font)
        self.sys_path_entry.grid(row=1, column=1, padx=5)
        ttk.Button(path_frame, text="浏览", command=lambda: self.browse_file("sys")).grid(row=1, column=2)
        
        # 保存配置按钮
        ttk.Button(path_frame, text="保存配置", command=self.save_path_config).grid(row=2, column=1, pady=5)
    
    def browse_file(self, file_type):
        """浏览文件对话框
        
        Args:
            file_type (str): 文件类型，"dll" 或 "sys"
        """
        filetypes = [("DLL files", "*.dll")] if file_type == "dll" else [("SYS files", "*.sys")]
        filename = filedialog.askopenfilename(filetypes=filetypes)
        if filename:
            if file_type == "dll":
                self.dll_path_var.set(filename)
            else:
                self.sys_path_var.set(filename)
    
    def save_path_config(self):
        """保存路径配置到文件"""
        # 获取当前路径设置
        dll_path = self.dll_path_var.get().strip()
        sys_path = self.sys_path_var.get().strip()
        
        # 验证路径
        if not dll_path or not sys_path:
            logging.error("DLL路径和SYS路径不能为空")
            return
            
        config = {
            # 驱动路径配置
            "dll_path": dll_path,
            "sys_path": sys_path,
            
            # 高频按键测试配置
            "rapid_test": {
                "test_key": self.test_key_var.get(),
                "press_time": self.press_time_var.get(),
                "interval_time": self.interval_time_var.get(),
                "duration": self.duration_var.get()
            }
        }
        
        try:
            with open(self.config_file, "w") as f:
                json.dump(config, f, indent=4)
            logging.info("配置保存成功")
        except Exception as e:
            error_msg = f"保存配置失败: {str(e)}"
            logging.error(error_msg)
            messagebox.showerror("错误", error_msg)
    
    def load_path_config(self):
        """加载保存的路径配置"""
        try:
            if os.path.exists(self.config_file):
                with open(self.config_file, "r") as f:
                    config = json.load(f)
                    
                # 加载驱动路径配置
                dll_path = config.get("dll_path", "")
                sys_path = config.get("sys_path", "")
                
                # 设置路径变量
                if dll_path:
                    self.dll_path_var.set(dll_path)
                    
                if sys_path:
                    self.sys_path_var.set(sys_path)
                
                # 加载高频按键测试配置
                rapid_test_config = config.get("rapid_test", {})
                if rapid_test_config:
                    self.test_key_var.set(rapid_test_config.get("test_key", "a"))
                    self.press_time_var.set(rapid_test_config.get("press_time", "1"))
                    self.interval_time_var.set(rapid_test_config.get("interval_time", "1"))
                    self.duration_var.set(rapid_test_config.get("duration", "0"))
                    logging.info("已加载高频按键测试配置")
                    
        except Exception as e:
            logging.error(f"加载配置失败: {str(e)}")
    
    def initialize_driver(self):
        """初始化驱动"""
        try:
            dll_path = self.dll_path_var.get()
            sys_path = self.sys_path_var.get()
            
            if not dll_path or not sys_path:
                logging.error("请先设置DLL和SYS文件路径！")
                return False
            
            self.driver_mgr = DriverManager()
            self.driver_mgr._setup_paths(dll_path=dll_path, sys_path=sys_path)
            
            if self.driver_mgr.initialize():
                self.input_tester = InputTester(self.driver_mgr.get_driver())
                logging.info("驱动初始化成功！")
                return True
            else:
                messagebox.showerror("错误", "驱动初始化失败！")
                return False
                
        except Exception as e:
            messagebox.showerror("错误", f"驱动初始化失败: {str(e)}")
            return False
    
    def run(self):
        """运行GUI"""
        self.root.mainloop()
        
        # 确保程序退出时清理资源
        if self.driver_mgr:
            self.driver_mgr.cleanup()
    
    def _toggle_rapid_test(self):
        """切换高频按键测试状态"""
        if not hasattr(self, 'rapid_test_running'):
            self.rapid_test_running = False
            
        if not self.rapid_test_running:
            # 开始测试
            try:
                # 检查驱动状态
                if not self.is_driver_loaded or not self.input_tester:
                    messagebox.showerror("错误", "请先加载驱动！")
                    return
                
                # 获取参数
                test_key = self.test_key_var.get()
                if not test_key:
                    messagebox.showerror("错误", "请输入测试按键！")
                    return
                    
                press_time = int(self.press_time_var.get())
                interval_time = int(self.interval_time_var.get())
                
                # 获取运行时长并验证
                try:
                    duration = float(self.duration_var.get())
                    if duration < 1:
                        messagebox.showerror("错误", "运行时长必须大于等于1秒！")
                        return
                except ValueError:
                    messagebox.showerror("错误", "请输入有效的运行时长！")
                    return
                
                # if press_time <= 0 or interval_time <= 0:
                #     messagebox.showerror("错误", "时间间隔必须大于0！")
                #     return
                
                # 检查按键是否有效
                if not VirtualKeys.is_valid_key(test_key):
                    messagebox.showerror("错误", f"无效的按键: {test_key}")
                    return
                
                # 重置所有计数和统计数据
                self.press_count = 0
                self._update_press_count()
                self.run_time_label.config(text="运行时间: 0秒")
                self.click_rate_label.config(text="平均频率: 0次/秒")
                
                # 更新UI状态
                self.rapid_test_btn.config(text="停止测试")
                self.rapid_test_status.config(text="状态: 运行中")
                self.rapid_test_running = True
                
                # 启动测试线程
                self.rapid_test_thread = threading.Thread(
                    target=self._run_rapid_test,
                    args=(test_key, press_time/1000.0, interval_time/1000.0),
                    daemon=True
                )
                self.rapid_test_thread.start()
                
            except ValueError:
                messagebox.showerror("错误", "请输入有效的时间间隔！")
                return
        else:
            # 停止测试
            self.rapid_test_running = False
            self.rapid_test_btn.config(text="开始测试")
            self.rapid_test_status.config(text="状态: 已停止")
    
    def _run_rapid_test(self, key, press_time, interval_time):
        """运行高频按键测试
        
        Args:
            key (str): 要测试的按键
            press_time (float): 按键按下时间（秒）
            interval_time (float): 按键间隔时间（秒）
        """
        try:
            if not self.input_tester:
                raise RuntimeError("输入测试器未初始化")
            
            # 获取运行时长设置
            try:
                duration = float(self.duration_var.get())
            except ValueError:
                duration = 0  # 默认无限运行
            
            # 初始化计时和计数
            start_time = time.perf_counter()  # 使用高精度计时器
            last_update_time = start_time
            last_press_time = start_time
            
            while self.rapid_test_running and self.is_driver_loaded:
                current_time = time.perf_counter()
                elapsed_time = current_time - start_time
                
                # 检查是否达到运行时长
                if duration > 0 and elapsed_time >= duration:
                    # 确保最后一次更新显示准确的运行时间
                    self.root.after(0, self._update_status, duration, self.press_count / duration if duration > 0 else 0)
                    self.root.after(0, self._stop_test)
                    break
                
                # 每秒更新一次状态
                if current_time - last_update_time >= 1.0:
                    # 计算并更新频率
                    rate = self.press_count / elapsed_time if elapsed_time > 0 else 0
                    self.root.after(0, self._update_status, elapsed_time, rate)
                    last_update_time = current_time
                
                # 检查驱动状态
                if not self.input_tester._check_device_status():
                    raise RuntimeError("驱动状态异常")
                
                # 按下按键
                if not self.input_tester.key_down(key):
                    raise RuntimeError("按键按下失败")
                
                # 使用高精度计时器等待按下时间
                while time.perf_counter() - current_time < press_time:
                    pass
                
                # 释放按键
                if not self.input_tester.key_up(key):
                    raise RuntimeError("按键释放失败")
                
                # 更新计数
                self.press_count += 1
                self.root.after(0, self._update_press_count)
                
                # 使用高精度计时器等待间隔时间
                while time.perf_counter() - current_time < (press_time + interval_time):
                    pass
                
        except Exception as e:
            self.rapid_test_running = False
            error_msg = f"高频按键测试出错: {str(e)}"
            logging.error(error_msg)
            self.root.after(0, lambda: messagebox.showerror("错误", error_msg))
            self.root.after(0, lambda: self.rapid_test_btn.config(text="开始测试"))
            self.root.after(0, lambda: self.rapid_test_status.config(text="状态: 出错"))
    
    def _update_status(self, elapsed_time, rate):
        """更新状态显示
        
        Args:
            elapsed_time (float): 运行时间（秒）
            rate (float): 点击频率（次/秒）
        """
        self.run_time_label.config(text=f"运行时间: {round(elapsed_time, 1)}秒")
        self.click_rate_label.config(text=f"平均频率: {rate:.1f}次/秒")
    
    def _stop_test(self):
        """停止测试"""
        self.rapid_test_running = False
        self.rapid_test_btn.config(text="开始测试")
        self.rapid_test_status.config(text="状态: 已完成")
    
    def _update_press_count(self):
        """更新按键计数显示"""
        self.press_count_label.config(text=f"按键次数: {self.press_count}")
    
    def check_privileges(self):
        """检查权限并在需要时请求提升
        
        Returns:
            bool: 如果已经有管理员权限返回True，否则返回False
        """
        try:
            if ctypes.windll.shell32.IsUserAnAdmin():
                return True
                
            # 需要提权
            response = messagebox.askyesno(
                "权限提示",
                "此程序需要管理员权限才能正常使用驱动功能。\n是否以管理员身份重新启动？\n\n" +
                "点击'否'将以普通权限运行，但驱动相关功能可能无法使用。"
            )
            
            if response:
                # 准备以管理员身份重启
                script = os.path.abspath(sys.argv[0])
                
                # 根据调试模式选择python解释器
                if DEBUG_MODE:
                    python_exe = sys.executable  # 使用python.exe
                    window_mode = SW_SHOWNORMAL  # 显示窗口
                else:
                    # 使用pythonw.exe
                    pythonw_path = os.path.join(os.path.dirname(sys.executable), 'pythonw.exe')
                    python_exe = pythonw_path if os.path.exists(pythonw_path) else sys.executable
                    window_mode = SW_HIDE  # 隐藏窗口
                    
                # 构建参数字符串
                params = ' '.join([f'"{arg}"' for arg in sys.argv[1:]])
                
                ret = ctypes.windll.shell32.ShellExecuteW(
                    None, 
                    "runas",
                    python_exe,
                    f'"{script}" {params}',
                    None,
                    window_mode
                )
                
                # 返回值大于32表示成功
                if ret > 32:
                    return False  # 返回False以关闭当前实例
                else:
                    messagebox.showerror(
                        "错误",
                        f"无法获取管理员权限 (错误码: {ret})"
                    )
                    return True  # 继续以普通权限运行
            else:
                # 用户选择不提升权限
                messagebox.showwarning(
                    "权限提示",
                    "您选择了以普通权限运行程序。\n" +
                    "请注意：驱动相关功能将无法使用，但其他功能不受影响。\n" +
                    "如需使用完整功能，请重新启动程序并选择以管理员身份运行。"
                )
                return True  # 继续以普通权限运行
            
        except Exception as e:
            messagebox.showerror("错误", f"权限检查失败: {str(e)}")
            return True  # 出错时也继续运行

    def _test_mouse_click(self, button: str) -> None:
        """测试鼠标点击
        
        Args:
            button: 鼠标按钮，"left", "right", "middle", "x1", "x2"
        """
        def run_test():
            try:
                # 检查驱动状态
                if not self.is_driver_loaded or not self.input_tester:
                    logging.error("驱动未加载或测试器未初始化")
                    messagebox.showerror("错误", "请先加载驱动")
                    return

                # 获取屏幕信息
                screen_width = ctypes.windll.user32.GetSystemMetrics(0)  # SM_CXSCREEN
                screen_height = ctypes.windll.user32.GetSystemMetrics(1)  # SM_CYSCREEN
                
                # 计算测试位置（屏幕中心偏右）
                test_x = screen_width * 3 // 4
                test_y = screen_height // 2

                # 移动到测试位置
                logging.info(f"移动鼠标到测试位置: ({test_x}, {test_y})")
                if not self.input_tester.mouse_move_abs(test_x, test_y):
                    raise Exception("鼠标移动失败")
                time.sleep(1)  # 等待移动完成

                # 根据按钮类型执行不同的测试
                if button == "left":
                    logging.info("测试左键点击 - 请观察鼠标位置的选择效果")
                    # 双击测试
                    self.input_tester.mouse_click(duration=0.1)
                    time.sleep(0.1)
                    self.input_tester.mouse_click(duration=0.1)
                    
                elif button == "right":
                    logging.info("测试右键点击 - 请观察是否出现右键菜单")
                    self.input_tester.mouse_right_click()
                    
                elif button == "middle":
                    # 先移动到浏览器标签区域位置
                    test_y = screen_height // 8
                    logging.info(f"移动鼠标到标签栏位置: ({test_x}, {test_y})")
                    self.input_tester.mouse_move_abs(test_x, test_y)
                    time.sleep(1)
                    
                    logging.info("测试中键点击 - 如果在浏览器中，应该会打开新标签页")
                    self.input_tester.mouse_middle_click()
                    
                elif button == "x1":
                    logging.info("测试X1键点击 - 如果在浏览器中，应该会后退")
                    self.input_tester.mouse_x1_click()
                    
                elif button == "x2":
                    logging.info("测试X2键点击 - 如果在浏览器中，应该会前进")
                    self.input_tester.mouse_x2_click()

                logging.info(f"{button}键测试完成")
                    
            except Exception as e:
                logging.error(f"鼠标点击测试出错: {str(e)}")
                messagebox.showerror("错误", f"鼠标点击测试出错: {str(e)}")

        # 在新线程中运行测试
        threading.Thread(target=run_test, daemon=True).start()

    def _test_mouse_wheel(self, direction: str) -> None:
        """测试鼠标滚轮
        
        Args:
            direction: 滚动方向，"up" 或 "down"
        """
        def run_test():
            try:
                # 检查驱动状态
                if not self.is_driver_loaded or not self.input_tester:
                    logging.error("驱动未加载或测试器未初始化")
                    messagebox.showerror("错误", "请先加载驱动")
                    return

                # 获取滚动量
                wheel_delta = int(self.wheel_delta_var.get())
                logging.info(f"滚轮测试 - 方向: {direction}, 滚动量: {wheel_delta}")
                
                if wheel_delta <= 0:
                    logging.error(f"无效的滚动量: {wheel_delta}")
                    messagebox.showerror("错误", "滚动量必须大于0")
                    return
                    
                # 检查驱动状态
                self.input_tester.check_device_status()
                driver_status = self.input_tester.get_driver_status()
                if driver_status != 1:  # DEVICE_STATUS_READY
                    logging.error(f"驱动状态异常: {driver_status}")
                    messagebox.showerror("错误", "驱动状态异常")
                    return

                # 添加3秒延时
                logging.info("滚轮测试将在3秒后开始...")
                time.sleep(3)
                    
                # 根据方向调用相应函数
                if direction == "up":
                    logging.info("执行向上滚动")
                    if not self.input_tester.mouse_wheel_up(wheel_delta):
                        logging.error("鼠标滚轮向上滚动失败")
                        messagebox.showerror("错误", "鼠标滚轮向上滚动失败")
                    else:
                        logging.info("向上滚动成功")
                else:
                    logging.info("执行向下滚动")
                    if not self.input_tester.mouse_wheel_down(wheel_delta):
                        logging.error("鼠标滚轮向下滚动失败")
                        messagebox.showerror("错误", "鼠标滚轮向下滚动失败")
                    else:
                        logging.info("向下滚动成功")
                        
            except ValueError as ve:
                logging.error(f"数值转换错误: {str(ve)}")
                messagebox.showerror("错误", "请输入有效的滚动量数值")
            except Exception as e:
                logging.error(f"鼠标滚轮测试出错: {str(e)}")
                messagebox.showerror("错误", f"鼠标滚轮测试出错: {str(e)}")

        # 使用新线程运行滚轮测试，避免阻塞GUI
        threading.Thread(target=run_test, daemon=True).start()

    def _move_to_corner(self, corner: str) -> None:
        """移动鼠标到主显示器的四角
        
        Args:
            corner: 目标角落，可选值："top_left", "top_right", "bottom_left", "bottom_right"
        """
        def run_move():
            try:
                # 检查驱动状态
                if not self.is_driver_loaded or not self.input_tester:
                    logging.error("驱动未加载或测试器未初始化")
                    messagebox.showerror("错误", "请先加载驱动")
                    return

                # 获取主显示器信息
                screen_width = ctypes.windll.user32.GetSystemMetrics(0)   # SM_CXSCREEN
                screen_height = ctypes.windll.user32.GetSystemMetrics(1)  # SM_CYSCREEN

                # 设置边缘偏移量（避免完全处于边缘）
                EDGE_OFFSET = 1

                # 根据选择的角落确定目标坐标
                if corner == "top_left":
                    target_x, target_y = EDGE_OFFSET, EDGE_OFFSET
                    corner_name = "左上角"
                elif corner == "top_right":
                    target_x, target_y = screen_width - EDGE_OFFSET, EDGE_OFFSET
                    corner_name = "右上角"
                elif corner == "bottom_left":
                    target_x, target_y = EDGE_OFFSET, screen_height - EDGE_OFFSET
                    corner_name = "左下角"
                else:  # bottom_right
                    target_x, target_y = screen_width - EDGE_OFFSET, screen_height - EDGE_OFFSET
                    corner_name = "右下角"

                logging.info(f"移动鼠标到主显示器{corner_name}: ({target_x}, {target_y})")
                self.input_tester.mouse_move_abs(target_x, target_y)

            except Exception as e:
                logging.error(f"移动到{corner_name}失败: {str(e)}")
                messagebox.showerror("错误", f"移动到{corner_name}失败: {str(e)}")

        # 在新线程中运行移动操作
        threading.Thread(target=run_move, daemon=True).start()

    def _test_smooth_move_rel(self, direction: str) -> None:
        """测试相对平滑移动
        
        Args:
            direction: 移动方向，可选值："left", "right", "up", "down"
        """
        def run_test():
            try:
                # 检查驱动状态
                if not self.is_driver_loaded or not self.input_tester:
                    logging.error("驱动未加载或测试器未初始化")
                    messagebox.showerror("错误", "请先加载驱动")
                    return

                # 获取移动距离
                distance = int(self.rel_distance_var.get())
                if distance <= 0:
                    logging.error(f"无效的移动距离: {distance}")
                    messagebox.showerror("错误", "移动距离必须大于0")
                    return

                # 根据方向设置移动参数
                dx, dy = 0, 0
                if direction == "left":
                    dx = -distance
                elif direction == "right":
                    dx = distance
                elif direction == "up":
                    dy = -distance
                else:  # down
                    dy = distance

                logging.info(f"开始相对平滑移动测试 - 方向: {direction}, 距离: {distance}")
                
                # 执行平滑移动
                STEPS = 10  # 将移动分成10步
                step_x = dx / STEPS
                step_y = dy / STEPS
                
                time.sleep(3)
                logging.info("3秒后开始移动")
                
                for _ in range(STEPS):
                    self.input_tester.mouse_move_rel(int(step_x), int(step_y))
                    time.sleep(0.02)  # 20ms的延迟使移动更平滑
                    
                logging.info("相对平滑移动完成")

            except ValueError as ve:
                logging.error(f"数值转换错误: {str(ve)}")
                messagebox.showerror("错误", "请输入有效的移动距离")
            except Exception as e:
                logging.error(f"相对平滑移动测试出错: {str(e)}")
                messagebox.showerror("错误", f"相对平滑移动测试出错: {str(e)}")

        # 在新线程中运行测试
        threading.Thread(target=run_test, daemon=True).start()

    def _test_smooth_move_abs(self) -> None:
        """测试绝对平滑移动"""
        def run_test():
            try:
                # 检查驱动状态
                if not self.is_driver_loaded or not self.input_tester:
                    logging.error("驱动未加载或测试器未初始化")
                    messagebox.showerror("错误", "请先加载驱动")
                    return

                # 获取目标坐标
                target_x = int(self.abs_x_var.get())
                target_y = int(self.abs_y_var.get())

                # 获取当前鼠标位置
                cursor = win32gui.GetCursorPos()
                start_x, start_y = cursor[0], cursor[1]

                # 计算移动距离
                dx = target_x - start_x
                dy = target_y - start_y

                logging.info(f"开始绝对平滑移动测试 - 目标位置: ({target_x}, {target_y})")
                
                # 执行平滑移动
                STEPS = 20  # 将移动分成20步
                for i in range(STEPS + 1):
                    # 使用缓动函数使移动更自然
                    progress = i / STEPS
                    eased_progress = progress * (2 - progress)  # 二次缓动
                    
                    current_x = int(start_x + dx * eased_progress)
                    current_y = int(start_y + dy * eased_progress)
                    
                    self.input_tester.mouse_move_abs(current_x, current_y)
                    time.sleep(0.01)  # 10ms的延迟使移动更平滑
                    
                logging.info("绝对平滑移动完成")

            except ValueError as ve:
                logging.error(f"数值转换错误: {str(ve)}")
                messagebox.showerror("错误", "请输入有效的坐标值")
            except Exception as e:
                logging.error(f"绝对平滑移动测试出错: {str(e)}")
                messagebox.showerror("错误", f"绝对平滑移动测试出错: {str(e)}")

        # 在新线程中运行测试
        threading.Thread(target=run_test, daemon=True).start()

    def _toggle_keyboard_hook(self):
        """切换键盘钩子状态"""
        if not self.keyboard_hook:
            try:
                # 创建并启动键盘钩子
                self.keyboard_hook = KeyboardHook()
                
                # 注册热键回调
                hotkey = self.hotkey_var.get().upper()
                vk_code = VirtualKeys.get_vk_code(hotkey)
                if not vk_code:
                    messagebox.showerror("错误", f"无效的热键: {hotkey}")
                    return
                    
                self.keyboard_hook.register_hotkey(vk_code, self._toggle_auto_move)
                self.keyboard_hook.start()
                
                # 更新UI
                self.start_hook_btn.config(text="停止热键监听")
                self.auto_move_status.config(text="状态: 等待热键触发")
                
            except Exception as e:
                messagebox.showerror("错误", f"启动键盘钩子失败: {str(e)}")
                self.keyboard_hook = None
                
        else:
            try:
                # 停止键盘钩子
                self.keyboard_hook.stop()
                self.keyboard_hook = None
                
                # 确保自动移动也停止
                self.auto_move_running = False
                
                # 更新UI
                self.start_hook_btn.config(text="启动热键监听")
                self.auto_move_status.config(text="状态: 未启动")
                
            except Exception as e:
                messagebox.showerror("错误", f"停止键盘钩子失败: {str(e)}")

    def _toggle_auto_move(self):
        """切换自动移动状态"""
        if not self.is_driver_loaded or not self.input_tester:
            messagebox.showerror("错误", "请先加载驱动")
            return
            
        if not self.auto_move_running:
            try:
                # 获取设置
                speed = float(self.speed_var.get())
                move_range = float(self.range_var.get())
                
                if speed <= 0 or move_range <= 0:
                    messagebox.showerror("错误", "速度和范围必须大于0")
                    return
                
                # 启动自动移动
                self.auto_move_running = True
                self.auto_move_status.config(text="状态: 自动移动中")
                
                # 在新线程中运行自动移动
                threading.Thread(target=self._run_auto_move,
                              args=(speed, move_range),
                              daemon=True).start()
                
            except ValueError:
                messagebox.showerror("错误", "请输入有效的数值")
                return
                
        else:
            # 停止自动移动
            self.auto_move_running = False
            self.auto_move_status.config(text="状态: 等待热键触发")

    def _run_auto_move(self, speed, move_range):
        """运行自动移动
        
        Args:
            speed: 移动速度
            move_range: 移动范围
        """
        try:
            # 获取屏幕尺寸
            screen_width = win32api.GetSystemMetrics(win32con.SM_CXSCREEN)
            screen_height = win32api.GetSystemMetrics(win32con.SM_CYSCREEN)
            
            # 获取初始鼠标位置
            cursor_pos = win32gui.GetCursorPos()
            center_x, center_y = cursor_pos[0], cursor_pos[1]
            
            # 减小移动速度和范围的影响
            actual_speed = speed * 0.01  # 降低速度
            actual_range = move_range * 0.5  # 减小范围
            
            angle = 0.0
            last_x = center_x
            last_y = center_y
            
            while self.auto_move_running:
                try:
                    # 计算新位置（圆形轨迹）
                    new_x = center_x + int(actual_range * math.cos(angle))
                    new_y = center_y + int(actual_range * math.sin(angle))
                    
                    # 计算相对移动距离
                    dx = new_x - last_x
                    dy = new_y - last_y
                    
                    # 确保移动距离不会太大
                    if abs(dx) > 5 or abs(dy) > 5:
                        dx = max(min(dx, 5), -5)
                        dy = max(min(dy, 5), -5)
                    
                    # 移动鼠标（使用相对移动）
                    if dx != 0 or dy != 0:
                        self.input_tester.mouse_move_rel(dx, dy)
                        last_x += dx
                        last_y += dy
                    
                    # 更新角度
                    angle += actual_speed
                    if angle >= 2 * math.pi:
                        angle -= 2 * math.pi
                    
                    # 增加延迟以减缓移动速度
                    time.sleep(0.016)  # 约60fps的更新率
                    
                except Exception as e:
                    logging.error(f"自动移动出错: {str(e)}")
                    time.sleep(0.1)  # 出错时等待较长时间
                    
        except Exception as e:
            logging.error(f"自动移动线程出错: {str(e)}")
            self.auto_move_running = False
            self.root.after(0, lambda: self.auto_move_status.config(text="状态: 出错"))

    def save_auto_move_config(self):
        """保存自动移动配置"""
        try:
            # 更新配置
            self.auto_move_config.update({
                'hotkey': self.hotkey_var.get(),
                'speed': float(self.speed_var.get()),
                'range': float(self.range_var.get())
            })
            
            # 保存到文件
            config = {
                'dll_path': self.dll_path_var.get(),
                'sys_path': self.sys_path_var.get(),
                'rapid_test': {
                    'test_key': self.test_key_var.get(),
                    'press_time': self.press_time_var.get(),
                    'interval_time': self.interval_time_var.get(),
                    'duration': self.duration_var.get()
                },
                'auto_move': self.auto_move_config
            }
            
            with open(self.config_file, 'w') as f:
                json.dump(config, f, indent=4)
                
            messagebox.showinfo("成功", "配置保存成功！")
            
        except Exception as e:
            messagebox.showerror("错误", f"保存配置失败: {str(e)}")

    def load_auto_move_config(self):
        """加载自动移动配置"""
        try:
            if os.path.exists(self.config_file):
                with open(self.config_file, 'r') as f:
                    config = json.load(f)
                    
                if 'auto_move' in config:
                    self.auto_move_config.update(config['auto_move'])
                    
        except Exception as e:
            logging.error(f"加载自动移动配置失败: {str(e)}")


if __name__ == '__main__':
    try:
        app = LYKeysGUI()
        app.run()
        
    except Exception as e:
        # 其他未预期的错误
        messagebox.showerror("错误", f"程序运行出错: {str(e)}") 