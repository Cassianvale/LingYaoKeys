# 驱动接口文档

## 接口列表

### 驱动管理接口

<div class="driver-api-table">

| 函数名              | 参数列表                                       | 返回值类型         | 功能描述     | 示例                                   |
|------------------|--------------------------------------------|---------------|----------|--------------------------------------|
| LoadNTDriver     | char* lpszDriverName, char* lpszDriverPath | BOOL          | 加载NT驱动程序 | LoadNTDriver("lykeys", "lykeys.sys") |
| UnloadNTDriver   | char* szSvrName                            | BOOL          | 卸载NT驱动程序 | UnloadNTDriver("lykeys")             |
| SetHandle        | void                                       | BOOL          | 设置驱动句柄   | SetHandle()                          |
| GetDriverHandle  | void                                       | HANDLE        | 获取驱动句柄   | GetDriverHandle()                    |
| GetDriverStatus  | void                                       | DEVICE_STATUS | 获取驱动状态   | GetDriverStatus()                    |
| GetLastCheckTime | void                                       | ULONGLONG     | 获取上次检查时间 | GetLastCheckTime()                   |

</div>

### 键盘操作接口

<div class="keyboard-api-table">

| 函数名     | 参数列表              | 返回值类型 | 功能描述     | 示例                    |
|---------|-------------------|-------|----------|-----------------------|
| KeyDown | USHORT VirtualKey | void  | 模拟键盘按键按下 | KeyDown(0x41) // 按下A键 |
| KeyUp   | USHORT VirtualKey | void  | 模拟键盘按键抬起 | KeyUp(0x41) // 抬起A键   |

</div>

### 鼠标操作接口

<div class="mouse-api-table">

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

</div>

## 使用说明

### 初始化驱动
```c
// 加载驱动
LoadNTDriver("lykeys", "lykeys.sys");

// 设置句柄
SetHandle();

// 检查状态
DEVICE_STATUS status = GetDriverStatus();
if (status == DEVICE_STATUS_READY) {
    // 驱动就绪
}
```

### 键盘操作
```c
// 按下A键
KeyDown(0x41);

// 等待一段时间
Sleep(100);

// 抬起A键
KeyUp(0x41);
```

### 鼠标操作
```c
// 移动鼠标
MouseMoveRELATIVE(10, 20);

// 点击左键
MouseLeftButtonDown();
Sleep(50);
MouseLeftButtonUp();

// 滚动鼠标
MouseWheelUp(120);
```

## 注意事项

### 线程安全
- 所有接口都是线程安全的
- 多线程环境下无需额外同步措施
- 建议在主线程中进行驱动加载和卸载

### 错误处理
- 检查函数返回值
- 使用GetLastError获取错误码
- 实现错误重试机制

### 性能优化
- 合理设置按键间隔
- 避免频繁调用
- 及时释放资源 