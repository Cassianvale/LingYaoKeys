# LyKeys DLL 接口文档

## 接口列表

### 驱动管理接口

| 函数名              | 参数列表                                       | 返回值类型         | 功能描述     | 示例                                   |
|------------------|--------------------------------------------|---------------|----------|--------------------------------------|
| LoadNTDriver     | char* lpszDriverName, char* lpszDriverPath | BOOL          | 加载NT驱动程序 | LoadNTDriver("lykeys", "lykeys.sys") |
| UnloadNTDriver   | char* szSvrName                            | BOOL          | 卸载NT驱动程序 | UnloadNTDriver("lykeys")             |
| SetHandle        | void                                       | BOOL          | 设置驱动句柄   | SetHandle()                          |
| GetDriverHandle  | void                                       | HANDLE        | 获取驱动句柄   | GetDriverHandle()                    |
| GetDriverStatus  | void                                       | DEVICE_STATUS | 获取驱动状态   | GetDriverStatus()                    |
| GetLastCheckTime | void                                       | ULONGLONG     | 获取上次检查时间 | GetLastCheckTime()                   |

### 键盘操作接口

| 函数名     | 参数列表              | 返回值类型 | 功能描述     | 示例                    |
|---------|-------------------|-------|----------|-----------------------|
| KeyDown | USHORT VirtualKey | void  | 模拟键盘按键按下 | KeyDown(0x41) // 按下A键 |
| KeyUp   | USHORT VirtualKey | void  | 模拟键盘按键抬起 | KeyUp(0x41) // 抬起A键   |

### 鼠标操作接口

| 函数名                   | 参数列表              | 返回值类型 | 功能描述    | 示例                          |
|-----------------------|-------------------|-------|---------|-----------------------------|
| MouseMoveRELATIVE     | LONG dx, LONG dy  | void  | 鼠标相对移动  | MouseMoveRELATIVE(10, 20)   |
| MouseMoveABSOLUTE     | LONG dx, LONG dy  | void  | 鼠标绝对移动  | MouseMoveABSOLUTE(100, 200) |
| MouseLeftButtonDown   | void              | void  | 鼠标左键按下  | MouseLeftButtonDown()       |
| MouseLeftButtonUp     | void              | void  | 鼠标左键抬起  | MouseLeftButtonUp()         |
| MouseRightButtonDown  | void              | void  | 鼠标右键按下  | MouseRightButtonDown()      |
| MouseRightButtonUp    | void              | void  | 鼠标右键抬起  | MouseRightButtonUp()        |
| MouseMiddleButtonDown | void              | void  | 鼠标中键按下  | MouseMiddleButtonDown()     |
| MouseMiddleButtonUp   | void              | void  | 鼠标中键抬起  | MouseMiddleButtonUp()       |
| MouseXButton1Down     | void              | void  | 鼠标X1键按下 | MouseXButton1Down()         |
| MouseXButton1Up       | void              | void  | 鼠标X1键抬起 | MouseXButton1Up()           |
| MouseXButton2Down     | void              | void  | 鼠标X2键按下 | MouseXButton2Down()         |
| MouseXButton2Up       | void              | void  | 鼠标X2键抬起 | MouseXButton2Up()           |
| MouseWheelUp          | USHORT wheelDelta | void  | 鼠标滚轮向上  | MouseWheelUp(120)           |
| MouseWheelDown        | USHORT wheelDelta | void  | 鼠标滚轮向下  | MouseWheelDown(120)         |

## 状态码说明

### DEVICE_STATUS 枚举值

| 定义                        | 含义               |
|---------------------------|------------------|
| DEVICE_STATUS_UNKNOWN (0) | 设备状态未知，设备刚初始化的状态 |
| DEVICE_STATUS_READY (1)   | 设备就绪，设备初始化完成的状态  |
| DEVICE_STATUS_ERROR (2)   | 设备错误，设备初始化错误的状态  |

## 使用注意事项

1. 使用前必须先调用 `LoadNTDriver` 加载驱动
2. 加载驱动后需要调用 `SetHandle` 获取设备句柄
3. 所有操作前建议通过 `GetDriverStatus` 检查驱动状态
4. 程序退出前应调用 `UnloadNTDriver` 卸载驱动
5. 鼠标移动坐标使用屏幕坐标系(左上角为原点)
6. 键盘操作使用Windows虚拟键码(Virtual-Key Codes)

## 错误处理

- 所有返回BOOL类型的函数，返回FALSE表示操作失败
- 失败时可通过 `GetLastError()` 获取具体错误码
- 建议实现错误重试机制，特别是对于驱动加载和设备句柄获取操作

## 线程安全性

- 所有接口都是线程安全的
- 多线程环境下无需额外同步措施
- 建议在主线程中进行驱动加载和卸载操作

## Python 使用示例

- 以管理员方式打开`Python调用示例库`
    - `cd Resource\lykeysdll\python_example`

- 创建`python3.10`环境，使用`pip`或`conda`安装win32gui库
    - `pip install pywin32`

- 运行示例代码
    - **`gui.py` 分为两种模式运行：**
        - 正常模式：`python gui.py`
        - Debug模式：`python gui.py --debug`

- ⚠ python下构建高频按键需要特别注意
    - Python中 `time.sleep()` 函数的精度问题:
        - windows下 `time.sleep(0.001)` 的最小精度约为15ms，需要使用 `time.perf_counter()` 作为高精度计时器，才可以精确到纳秒，使用
          `time.sleep()` 函数会大幅降低按键运行速度

## 相关资料

1. Windows虚拟键码：[Virtual-Key Codes键码表](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes)
1. 错误代码查询：[Error Codes](https://docs.microsoft.com/zh-cn/windows/win32/debug/system-error-codes)
2. WDK下载: [WDK](https://learn.microsoft.com/en-us/windows-hardware/drivers/download-the-wdk)
3. (已签名驱动不需要此步骤)驱动测试请关闭Bios中的`安全启动`，并打开`cmd`输入以下指令

```shell
# `禁用强制驱动签名` & `测试模式` & `重启`  
bcdedit /set nointegritychecks on
bcdedit /set testsigning on
shutdown  -r -t 0
```

## 调试&蓝屏分析工具

### 1. WinDbg  

1. 配置系统生成完整转储：

```
1. 右键"此电脑" -> 属性
2. 高级系统设置 -> 高级 -> 启动和故障恢复 -> 设置
3. 在"写入调试信息"下选择"完整内存转储"
4. 确保"自动重新启动"已勾选
```

2. 安装调试工具：

```
1. 下载并安装 WDK (Windows Driver Kit)
2. 安装 Windows SDK
3. 安装 WinDbg (Windows Debugger)
```

3. 使用 WinDbg 分析：

使用powershell管理员方式执行以下命令安装 WinDbg

```
winget install Microsoft.WinDbg
```

1. 打开 WinDbg
2. File -> Open Crash Dump
3. 选择 C:\Windows\MEMORY.DMP 或 Minidump 文件

4. 在命令窗口输入：

```
   !analyze -v    # 详细分析崩溃原因
   .bugcheck      # 显示蓝屏代码
   kb             # 显示调用栈
   lmvm lykeys    # 显示我们的驱动信息
```

### 2. BlueScreenView

1. 使用 BlueScreenView（更简单的方法）：

```
1. 下载并安装 BlueScreenView
2. 运行后自动显示所有蓝屏记录
3. 查看：
   - 蓝屏代码
   - 发生时间
   - 导致崩溃的驱动
   - 调用栈信息
```

2. 在驱动代码中添加详细日志

```
KdPrint(("Driver State: %d, IRQL: %d\n", state, KeGetCurrentIrql()));
KdPrint(("Callback Address: 0x%p\n", callback));
KdPrint(("Memory Region: 0x%p, Size: %d\n", address, size));
```

3. 配置本地内核调试：

```
bcdedit /debug on
bcdedit /dbgsettings local
```

4. 收集到的信息应该包括：

```
需要提供的信息
{
    1. 基本信息:
    - 蓝屏代码 (例如: 0x0000007E)
    - 发生时间
    - 系统版本
    2. 详细信息:
    - 导致崩溃的驱动名称
    - 崩溃时的调用栈
    - 相关的内存地址
    3. 环境信息:
    - 系统配置
    - 已安装的驱动
    - 硬件信息
}
```

## 验证工具

1. 完整验证（推荐用于开发测试）
   `verifier /flags 0xFF /driver lykeys.sys`
2. 基本验证（推荐用于日常测试）
   `verifier /standard /driver lykeys.sys`
3. 内存验证（针对内存问题）
   `verifier /flags 0x5 /driver lykeys.sys`
4. IRQL验证（针对IRQL问题）
   `verifier /flags 0x2 /driver lykeys.sys`

## 常见问题

### 驱动程序闪退导致无法正常结束驱动服务

```
// 手动cmd命令停止服务
sc query lykeys
sc stop lykeys
sc delete lykeys
```

```
// 执行cmd快捷命令停止服务
@echo off && sc query lykeys > nul 2>&1 && (echo Service exists, stopping... && sc stop lykeys > nul 2>&1 && timeout /t 2 /nobreak > nul && sc delete lykeys > nul 2>&1 && echo Service deleted successfully && exit) || (echo Service does not exist && exit)
```