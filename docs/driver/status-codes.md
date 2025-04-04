# 状态码说明

## DEVICE_STATUS 枚举值

<div class="status-code-table">

| 定义                         | 值  | 含义                |
|----------------------------|----|--------------------|
| DEVICE_STATUS_UNKNOWN      | 0  | 设备状态未知，设备刚初始化的状态 |
| DEVICE_STATUS_READY        | 1  | 设备就绪，设备初始化完成的状态  |
| DEVICE_STATUS_ERROR        | 2  | 设备错误，设备初始化错误的状态  |
| DEVICE_STATUS_NO_KEYBOARD  | 3  | 无法找到键盘设备         |
| DEVICE_STATUS_NO_MOUSE     | 4  | 无法找到鼠标设备         |
| DEVICE_STATUS_INIT_FAILED  | 5  | 初始化失败            |

</div>

## 详细错误码

<div class="detailed-error-code-table">

| 错误码 | 含义                | 解决方案           |
|------|-------------------|----------------|
| 1001 | 句柄无效错误           | 检查驱动初始化状态      |
| 2001 | 键盘设备不可用          | 检查系统键盘设备是否正常   |
| 3001 | 鼠标设备不可用          | 检查系统鼠标设备是否正常   |

</div>

## 错误码说明

### 驱动加载错误
<div class="error-code-table">

| 错误码 | 含义 | 解决方案 |
|--------|------|----------|
| 0x0001 | 驱动文件不存在 | 检查驱动文件路径 |
| 0x0002 | 驱动签名无效 | 验证驱动签名 |
| 0x0003 | 驱动版本不匹配 | 更新驱动版本 |
| 0x0004 | 系统不支持 | 检查系统版本 |

</div>

### 设备操作错误
<div class="error-code-table">

| 错误码 | 含义 | 解决方案 |
|--------|------|----------|
| 0x0101 | 设备未就绪 | 检查设备状态 |
| 0x0102 | 设备忙 | 等待设备空闲 |
| 0x0103 | 设备未连接 | 检查设备连接 |
| 0x0104 | 设备响应超时 | 检查设备状态 |

</div>

### 参数错误
<div class="error-code-table">

| 错误码 | 含义 | 解决方案 |
|--------|------|----------|
| 0x0201 | 参数无效 | 检查参数值 |
| 0x0202 | 参数超出范围 | 调整参数范围 |
| 0x0203 | 参数类型不匹配 | 检查参数类型 |
| 0x0204 | 参数缺失 | 补充必要参数 |

</div>

### 系统错误
<div class="error-code-table">

| 错误码 | 含义 | 解决方案 |
|--------|------|----------|
| 0x0301 | 内存不足 | 释放系统资源 |
| 0x0302 | 系统资源不足 | 检查系统资源 |
| 0x0303 | 权限不足 | 检查用户权限 |
| 0x0304 | 系统不支持 | 检查系统要求 |

</div>

## 状态检查

### 驱动状态检查
```c
DEVICE_STATUS status = GetDriverStatus();
switch (status) {
    case DEVICE_STATUS_UNKNOWN:
        // 设备未初始化
        break;
    case DEVICE_STATUS_READY:
        // 设备就绪
        break;
    case DEVICE_STATUS_ERROR:
        // 设备错误
        break;
    case DEVICE_STATUS_NO_KEYBOARD:
        // 键盘设备不可用
        break;
    case DEVICE_STATUS_NO_MOUSE:
        // 鼠标设备不可用
        break;
    case DEVICE_STATUS_INIT_FAILED:
        // 初始化失败
        break;
}
```

### 获取详细错误码
```c
// 获取详细错误码
int detailedError = GetDetailedErrorCode();

// 处理错误
switch (detailedError) {
    case 1001:
        // 处理句柄无效错误
        break;
    case 2001:
        // 处理键盘设备不可用错误
        break;
    case 3001:
        // 处理鼠标设备不可用错误
        break;
}
```

### 错误处理
```c
// 获取错误码
DWORD error = GetLastError();

// 处理错误
if (error != 0) {
    // 错误处理逻辑
    switch (error) {
        case 0x0001:
            // 处理驱动文件不存在错误
            break;
        case 0x0002:
            // 处理驱动签名无效错误
            break;
        // ... 其他错误处理
    }
}
```

## 调试建议

### 状态监控
- 定期检查设备状态
- 记录状态变化
- 分析错误原因

### 错误恢复
- 实现错误重试机制
- 提供错误恢复方案
- 记录错误日志

### 性能优化
- 避免频繁状态检查
- 优化错误处理流程
- 合理设置超时时间 