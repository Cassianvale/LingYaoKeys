# 驱动文档

## 驱动概述

> [!WARNING]
> Windows 7 未经过测试，可能导致无法预知的问题

灵曜按键使用内核级驱动实现按键模拟功能，通过DeviceIoControl与驱动通信。驱动支持32位/64位系统，兼容Windows 10/11

### 驱动特性
- 基于DeviceIoControl实现
- 支持离线运行
- 完善的反Hook保护
- 支持热插拔
- 支持多设备

### 系统要求
- Windows 10/11
- 32位/64位系统
- 管理员权限
- 关闭安全启动（测试驱动模式）

### 驱动文件
- `lykeysdll.dll`: 核心驱动动态链接库
- `lykeys.sys`: 内核级驱动文件
- `lykeys.cat`: 驱动签名文件
- `README.md`: 驱动接口说明

## 快速开始

### 安装驱动
1. 以管理员身份运行程序
2. 程序会自动安装驱动
3. 如果安装失败，检查系统设置

### 卸载驱动
1. 正常退出程序驱动会自动卸载
2. 如果卸载失败，使用命令行：
   ```cmd
   sc stop lykeys
   sc delete lykeys
   ```

3. 快捷命令：
   ```cmd
   @echo off && sc query lykeys > nul 2>&1 && (echo Service exists, stopping... && sc stop lykeys > nul 2>&1 && timeout /t 2 /nobreak > nul && sc delete lykeys > nul 2>&1 && echo Service deleted successfully && exit) || (echo Service does not exist && exit)
   ```

### 测试驱动
1. 运行示例程序
2. 测试基本功能
3. 检查驱动状态

## 注意事项

### 安全提示
- 请勿修改驱动文件
- 保持驱动签名完整
- 关注项目及时获取最新驱动版本

### 性能优化
- 合理设置按键间隔
- 避免频繁操作
- 监控系统资源

### 故障排除
- 检查驱动状态
- 查看错误日志
- 更新系统补丁 