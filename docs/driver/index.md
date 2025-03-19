# 驱动文档

## 驱动概述
> [!CAUTION]
> ⚠️**特别提示：关于各种游戏的反作弊问题，类似某某游戏能不能过测之类不要来问我，不提供这种技术支持！！！**

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
2. - 如果卸载失败，请使用以下命令行：
   ```cmd
   sc stop lykeys
   sc delete lykeys
   ```
   - 执行快捷命令（如果lykeys 服务存在，则立刻停止并删除驱动服务）
   ```cmd
   @echo off && sc query lykeys > nul 2>&1 && (echo Service exists, stopping... && sc stop lykeys > nul 2>&1 && timeout /t 2 /nobreak > nul && sc delete lykeys > nul 2>&1 && echo Service deleted successfully && exit) || (echo Service does not exist && exit)
   ```

## 参考资料

- [kmclassdll.dll](https://github.com/BestBurning/kmclassdll/releases) - DLL动态库
- [kmclass.sys](https://github.com/BestBurning/kmclass/releases) - 内核驱动
- 如何编译可参考 [编译dll并在python中使用ctypes调用](https://di1shuai.com/%E7%BC%96%E8%AF%91dll%E5%B9%B6%E5%9C%A8python%E4%B8%AD%E4%BD%BF%E7%94%A8ctypes%E8%B0%83%E7%94%A8.html)
- 错误代码 [Error Codes](https://docs.microsoft.com/zh-cn/windows/win32/debug/system-error-codes)
- [KMDF Hello World](https://docs.microsoft.com/zh-cn/windows-hardware/drivers/gettingstarted/writing-a-very-small-kmdf--driver)
- [WDK 10](https://docs.microsoft.com/en-us/windows-hardware/drivers/download-the-wdk) 

## 注意事项

### 安全提示
- 请勿修改驱动文件
- 保持驱动签名完整
- 关注项目及时获取最新驱动版本

### 故障排除
- 检查驱动状态
- 查看错误日志
- 按照debug文档排查故障
