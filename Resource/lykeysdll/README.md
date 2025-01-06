# LyKeys DLL 接口文档

## 接口列表

### 驱动管理接口

| 函数名 | 参数列表 | 返回值类型 | 功能描述 | 示例 |
|--------|----------|------------|----------|------|
| LoadNTDriver | char* lpszDriverName, char* lpszDriverPath | BOOL | 加载NT驱动程序 | LoadNTDriver("lykeys", "lykeys.sys") |
| UnloadNTDriver | char* szSvrName | BOOL | 卸载NT驱动程序 | UnloadNTDriver("lykeys") |
| SetHandle | void | BOOL | 设置驱动句柄 | SetHandle() |
| GetDriverHandle | void | HANDLE | 获取驱动句柄 | GetDriverHandle() |
| GetDriverStatus | void | DEVICE_STATUS | 获取驱动状态 | GetDriverStatus() |
| GetLastCheckTime | void | ULONGLONG | 获取上次检查时间 | GetLastCheckTime() |

### 键盘操作接口

| 函数名 | 参数列表 | 返回值类型 | 功能描述 | 示例 |
|--------|----------|------------|----------|------|
| KeyDown | USHORT VirtualKey | void | 模拟键盘按键按下 | KeyDown(0x41) // 按下A键 |
| KeyUp | USHORT VirtualKey | void | 模拟键盘按键抬起 | KeyUp(0x41) // 抬起A键 |

### 鼠标操作接口

| 函数名 | 参数列表 | 返回值类型 | 功能描述 | 示例 |
|--------|----------|------------|----------|------|
| MouseMoveRELATIVE | LONG dx, LONG dy | void | 鼠标相对移动 | MouseMoveRELATIVE(10, 20) |
| MouseMoveABSOLUTE | LONG dx, LONG dy | void | 鼠标绝对移动 | MouseMoveABSOLUTE(100, 200) |
| MouseLeftButtonDown | void | void | 鼠标左键按下 | MouseLeftButtonDown() |
| MouseLeftButtonUp | void | void | 鼠标左键抬起 | MouseLeftButtonUp() |
| MouseRightButtonDown | void | void | 鼠标右键按下 | MouseRightButtonDown() |
| MouseRightButtonUp | void | void | 鼠标右键抬起 | MouseRightButtonUp() |
| MouseMiddleButtonDown | void | void | 鼠标中键按下 | MouseMiddleButtonDown() |
| MouseMiddleButtonUp | void | void | 鼠标中键抬起 | MouseMiddleButtonUp() |
| MouseXButton1Down | void | void | 鼠标X1键按下 | MouseXButton1Down() |
| MouseXButton1Up | void | void | 鼠标X1键抬起 | MouseXButton1Up() |
| MouseXButton2Down | void | void | 鼠标X2键按下 | MouseXButton2Down() |
| MouseXButton2Up | void | void | 鼠标X2键抬起 | MouseXButton2Up() |
| MouseWheelUp | USHORT wheelDelta | void | 鼠标滚轮向上 | MouseWheelUp(120) |
| MouseWheelDown | USHORT wheelDelta | void | 鼠标滚轮向下 | MouseWheelDown(120) |

## 状态码说明

## 驱动初始化返回值
| 返回值 | 错误码 | 说明 |
|--------|--------|------|
| STATUS_SUCCESS | 0x00000000 | 驱动初始化成功，非0就是初始化失败 |
| STATUS_UNSUCCESSFUL | 0xC0000001 | 操作失败 |
| STATUS_NOT_SUPPORTED | 0xC00000BB | 不支持的操作 |
| STATUS_INVALID_PARAMETER | 0xC000000D | 无效的参数 |
| STATUS_INSUFFICIENT_RESOURCES | 0xC000009A | 资源不足 |
| STATUS_DEVICE_NOT_CONNECTED | 0xC000009D | 设备未连接 |

### DEVICE_STATUS 枚举值
| 设备状态枚举值 | 含义 |
|----|------|
| DEVICE_STATUS_UNKNOWN (0) | 设备状态未知，驱动刚加载时的状态 |
| DEVICE_STATUS_READY (1) | 设备就绪，设备已经成功初始化可以正常接收和处理输入 |
| DEVICE_STATUS_ERROR (2) | 设备错误，设备初始化失败回调函数设置失败，需要重新初始化或者排查问题 |

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


