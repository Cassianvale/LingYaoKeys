# -*- coding: utf-8 -*-

from virtuakeys_mapping import VirtualKeys
import logging
import time
from typing import Optional


class InputTester:
    """输入测试器类，用于测试键盘和鼠标输入"""
    
    def __init__(self, driver):
        """初始化输入测试器
        
        Args:
            driver: 驱动实例
        """
        self.driver = driver
        self.last_check_time = 0
        self.check_interval = 1.0  # 状态检查间隔(秒)
        # self.min_key_interval = 0.001  # 最小按键间隔(秒)
    
    # ===== 状态检查相关方法 =====
    def _check_device_status(self) -> bool:
        """检查驱动状态"""
        current_time = time.time()
        if current_time - self.last_check_time >= self.check_interval:
            self.driver.CheckDeviceStatus()
            self.last_check_time = current_time
            
            driver_status = self.driver.GetDriverStatus()
            
            if driver_status != 1:  # 1 = DEVICE_STATUS_READY
                logging.warning(f"驱动状态异常: {driver_status}")
                return False
        return True
    
    def check_device_status(self) -> None:
        """检查驱动状态"""
        if not self.driver:
            return
        self.driver.CheckDeviceStatus()
    
    def get_driver_status(self) -> int:
        """获取驱动状态"""
        if not self.driver:
            return 0
        return self.driver.GetDriverStatus()
    
    def get_last_check_time(self) -> int:
        """获取上次检查时间"""
        if not self.driver:
            return 0
        return self.driver.GetLastCheckTime()
    
    # ===== 键盘操作相关方法 =====
    def key_down(self, key: str) -> bool:
        """按键按下
        
        Args:
            key: 按键名称
            
        Returns:
            bool: 操作是否成功
        """
        vk_code = VirtualKeys.get_vk_code(key)
        if vk_code is None:
            logging.error(f"无效的按键: {key}")
            return False
        
        try:
            self.driver.KeyDown(vk_code)
            # time.sleep(self.min_key_interval)  # 添加最小延时
            return True
        except Exception as e:
            logging.error(f"按键按下失败: {str(e)}")
            return False
    
    def key_up(self, key: str) -> bool:
        """按键释放
        
        Args:
            key: 按键名称
            
        Returns:
            bool: 操作是否成功
        """
        vk_code = VirtualKeys.get_vk_code(key)
        if vk_code is None:
            logging.error(f"无效的按键: {key}")
            return False
        
        try:
            self.driver.KeyUp(vk_code)
            # time.sleep(self.min_key_interval)  # 添加最小延时
            return True
        except Exception as e:
            logging.error(f"按键释放失败: {str(e)}")
            return False
    
    def press_key(self, key: str, duration: float = 0.1) -> bool:
        """按下并释放按键
        
        Args:
            key: 按键名称
            duration: 按下持续时间(秒)
            
        Returns:
            bool: 操作是否成功
        """
        try:
            # 检查是否需要Shift
            if VirtualKeys.needs_shift(key):
                # 按下Shift
                self.key_down('shift')
                time.sleep(0.01)  # 短暂延时确保Shift按下
                
                # 获取对应的基础键码(不含Shift)
                success = self.key_down(key)
                if success:
                    time.sleep(duration)
                    self.key_up(key)
                
                # 释放Shift
                time.sleep(0.01)  # 短暂延时确保按键动作完成
                self.key_up('shift')
                return success
            else:
                # 普通按键处理
                if self.key_down(key):
                    time.sleep(duration)
                    return self.key_up(key)
            return False
        except Exception as e:
            logging.error(f"按键操作失败: {str(e)}")
            return False
    
    # ===== 鼠标操作相关方法 =====
    def mouse_move_rel(self, dx: int, dy: int) -> bool:
        """相对移动鼠标
        
        Args:
            dx: X轴相对移动距离
            dy: Y轴相对移动距离
            
        Returns:
            bool: 操作是否成功
        """
        self.driver.MouseMoveRELATIVE(dx, dy)
        return True
    
    def mouse_move_abs(self, x: int, y: int) -> bool:
        """绝对移动鼠标
        
        Args:
            x: 目标X坐标
            y: 目标Y坐标
            
        Returns:
            bool: 操作是否成功
        """
        self.driver.MouseMoveABSOLUTE(x, y)
        return True
    
    # 鼠标左键操作
    def mouse_left_down(self) -> bool:
        """鼠标左键按下"""
        self.driver.MouseLeftButtonDown()
        return True
    
    def mouse_left_up(self) -> bool:
        """鼠标左键释放"""
        self.driver.MouseLeftButtonUp()
        return True
    
    def mouse_click(self, duration: float = 0.1) -> bool:
        """鼠标左键点击"""
        if self.mouse_left_down():
            time.sleep(duration)
            return self.mouse_left_up()
        return False
    
    # 鼠标右键操作
    def mouse_right_down(self) -> bool:
        """鼠标右键按下"""
        self.driver.MouseRightButtonDown()
        return True
    
    def mouse_right_up(self) -> bool:
        """鼠标右键释放"""
        self.driver.MouseRightButtonUp()
        return True
    
    def mouse_right_click(self, duration: float = 0.1) -> bool:
        """鼠标右键点击"""
        if self.mouse_right_down():
            time.sleep(duration)
            return self.mouse_right_up()
        return False
    
    # 鼠标中键操作
    def mouse_middle_down(self) -> bool:
        """鼠标中键按下"""
        self.driver.MouseMiddleButtonDown()
        return True
    
    def mouse_middle_up(self) -> bool:
        """鼠标中键释放"""
        self.driver.MouseMiddleButtonUp()
        return True
    
    def mouse_middle_click(self, duration: float = 0.1) -> bool:
        """鼠标中键点击"""
        if self.mouse_middle_down():
            time.sleep(duration)
            return self.mouse_middle_up()
        return False
    
    # 鼠标X1键操作
    def mouse_x1_down(self) -> bool:
        """鼠标X1键按下"""
        self.driver.MouseXButton1Down()
        return True
    
    def mouse_x1_up(self) -> bool:
        """鼠标X1键释放"""
        self.driver.MouseXButton1Up()
        return True
    
    def mouse_x1_click(self, duration: float = 0.1) -> bool:
        """鼠标X1键点击"""
        if self.mouse_x1_down():
            time.sleep(duration)
            return self.mouse_x1_up()
        return False
    
    # 鼠标X2键操作
    def mouse_x2_down(self) -> bool:
        """鼠标X2键按下"""
        self.driver.MouseXButton2Down()
        return True
    
    def mouse_x2_up(self) -> bool:
        """鼠标X2键释放"""
        self.driver.MouseXButton2Up()
        return True
    
    def mouse_x2_click(self, duration: float = 0.1) -> bool:
        """鼠标X2键点击"""
        if self.mouse_x2_down():
            time.sleep(duration)
            return self.mouse_x2_up()
        return False
    
    # ===== 鼠标滚轮操作 =====
    def mouse_wheel_up(self, wheel_delta: int = 120) -> bool:
        """鼠标滚轮向上滚动
        
        Args:
            wheel_delta: 滚动量，默认为120（标准滚动单位）
            
        Returns:
            bool: 操作是否成功
        """
        try:
            self.driver.MouseWheelUp(wheel_delta)
            return True
        except Exception as e:
            logging.error(f"滚轮向上滚动失败: {str(e)}")
            return False
    
    def mouse_wheel_down(self, wheel_delta: int = 120) -> bool:
        """鼠标滚轮向下滚动
        
        Args:
            wheel_delta: 滚动量，默认为120（标准滚动单位）
            
        Returns:
            bool: 操作是否成功
        """
        try:
            self.driver.MouseWheelDown(wheel_delta)
            return True
        except Exception as e:
            logging.error(f"滚轮向下滚动失败: {str(e)}")
            return False
    
    def mouse_wheel(self, delta: int) -> bool:
        """鼠标滚轮滚动
        需要动态控制滚动方向时使用，可接受正负值，自动处理方向
        Args:
            delta: 滚动量，正值向上滚动，负值向下滚动
            
        Returns:
            bool: 操作是否成功
        """
        try:
            if delta > 0:
                return self.mouse_wheel_up(abs(delta))
            elif delta < 0:
                return self.mouse_wheel_down(abs(delta))
            return True
        except Exception as e:
            logging.error
            (f"滚轮滚动失败: {str(e)}")
            return False 