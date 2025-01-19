# -*- coding: utf-8 -*-

import os
import logging
from ctypes import CDLL, c_int, c_ulonglong
import json
import sys
import ctypes


class DriverManager:
    def __init__(self):
        self.driver = None
        self._setup_logging()
        
        # 在初始化时就检查权限
        if not self.check_and_elevate_privileges():
            raise PermissionError("需要管理员权限")
            
    def _setup_paths(self, dll_path=None, sys_path=None):
        """设置驱动路径
        
        Args:
            dll_path (str, optional): DLL文件的完整路径
            sys_path (str, optional): SYS文件的完整路径
        """
        # 尝试从配置文件加载路径
        config_file = "driver_config.json"
        if os.path.exists(config_file):
            try:
                with open(config_file, "r") as f:
                    config = json.load(f)
                    dll_path = dll_path or config.get("dll_path")
                    sys_path = sys_path or config.get("sys_path")
                    logging.info("从配置文件加载驱动路径")
            except Exception as e:
                logging.warning(f"加载配置文件失败: {str(e)}")
        
        # 如果没有提供路径且配置文件不存在或加载失败，则抛出异常
        if not dll_path or not sys_path:
            raise ValueError("未配置驱动路径，请在配置文件中设置或手动指定路径")
        
        self.dll_path = dll_path
        self.sys_path = sys_path
        
        # 验证路径是否存在
        if not os.path.exists(self.dll_path):
            raise FileNotFoundError(f"找不到DLL文件: {self.dll_path}")
            
        if not os.path.exists(self.sys_path):
            raise FileNotFoundError(f"找不到SYS文件: {self.sys_path}")
    
    def _setup_logging(self):
        """设置日志"""
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(levelname)s - %(message)s'
        )
    
    def check_and_elevate_privileges(self):
        """检查并提升权限
        
        Returns:
            bool: 如果已经有管理员权限返回True，需要提权返回False
        """
        try:
            if not self.is_admin():
                logging.warning("需要管理员权限才能加载驱动")
                return False
            return True
        except Exception as e:
            logging.error(f"权限检查失败: {str(e)}")
            return False
    
    def is_admin(self):
        """检查是否具有管理员权限"""
        try:
            return ctypes.windll.shell32.IsUserAnAdmin()
        except:
            return False
            
    def run_as_admin(self):
        """请求管理员权限"""
        if not self.is_admin():
            logging.info("正在请求管理员权限...")
            script = os.path.abspath(sys.argv[0])
            params = ' '.join(sys.argv[1:])
            try:
                ret = ctypes.windll.shell32.ShellExecuteW(
                    None, 
                    "runas",
                    sys.executable,
                    f'"{script}" {params}',
                    None,
                    1
                )
                if ret > 32:
                    return True
                else:
                    logging.error(f"权限提升失败,错误码: {ret}")
                    return False
            except Exception as e:
                logging.error(f"权限提升异常: {str(e)}")
                return False
        return True

    def initialize(self):
        """初始化驱动"""
        try:
            # 1. 检查并请求管理员权限
            if not self.is_admin():
                logging.warning("需要管理员权限才能加载驱动")
                if not self.run_as_admin():
                    raise PermissionError("无法获取管理员权限,请以管理员身份运行程序")
                return False  # 程序将重启
                
            logging.info("开始加载驱动...")
            self.driver = CDLL(self.dll_path)
            
            # 设置返回类型
            self.driver.GetDriverStatus.restype = c_int
            self.driver.GetLastCheckTime.restype = c_ulonglong
            
            # 先尝试卸载已存在的驱动
            self._unload_driver()
            
            # 加载驱动
            if not self.driver.LoadNTDriver(b'lykeys', self.sys_path.encode()):
                raise RuntimeError("驱动加载失败")
            
            # 设置句柄
            self.driver.SetHandle()
            logging.info("驱动初始化完成")
            return True
            
        except Exception as e:
            if isinstance(e, WindowsError) and e.winerror == 5:  # ERROR_ACCESS_DENIED
                logging.error("访问被拒绝,请确保以管理员身份运行")
                if not self.is_admin():
                    logging.info("尝试重新以管理员身份启动...")
                    self.run_as_admin()
            else:
                logging.error(f"初始化失败: {str(e)}")
            self.cleanup()
            return False
    
    def _unload_driver(self):
        """卸载驱动"""
        try:
            if self.driver:
                self.driver.UnloadNTDriver(b'lykeys')
        except Exception as e:
            logging.warning(f"卸载驱动时出现异常(可忽略): {str(e)}")
    
    def cleanup(self):
        """清理资源"""
        try:
            if self.driver:
                self._unload_driver()
                logging.info("驱动卸载成功")
        except Exception as e:
            logging.error(f"清理失败: {str(e)}")
    
    def get_driver(self):
        """获取驱动实例"""
        return self.driver 