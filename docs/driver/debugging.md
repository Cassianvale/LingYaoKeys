# 调试指南

## 调试工具

### WinDbg
1. 安装步骤：
   ```powershell
   # 使用winget安装WinDbg
   winget install Microsoft.WinDbg
   ```

2. 配置系统生成完整转储：
   ```
   1. 右键"此电脑" -> 属性
   2. 高级系统设置 -> 高级 -> 启动和故障恢复 -> 设置
   3. 在"写入调试信息"下选择"完整内存转储"
   4. 确保"自动重新启动"已勾选
   ```

3. 使用WinDbg分析：
   ```
   1. 打开 WinDbg
   2. File -> Open Crash Dump
   3. 选择 C:\Windows\MEMORY.DMP 或 Minidump 文件
   ```

4. 常用命令：
   ```
   !analyze -v    # 详细分析崩溃原因
   .bugcheck      # 显示蓝屏代码
   kb             # 显示调用栈
   lmvm lykeys    # 显示驱动信息
   ```

### BlueScreenView
1. 使用步骤：
   ```
   1. 下载并安装 BlueScreenView
   2. 运行后自动显示所有蓝屏记录
   3. 查看：
      - 蓝屏代码
      - 发生时间
      - 导致崩溃的驱动
      - 调用栈信息
   ```

2. 分析信息：
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

## 驱动状态调试

### 使用详细错误码
1. 获取更精确的错误信息：
   ```c
   // 检测设备状态
   CheckDeviceStatus();
   DEVICE_STATUS status = GetDriverStatus();
   
   // 获取详细错误代码
   int errorCode = GetDetailedErrorCode();
   
   // 获取上次检查时间
   ULONGLONG lastCheck = GetLastCheckTime();
   
   printf("设备状态: %d, 详细错误码: %d, 上次检查时间: %llu\n", 
          status, errorCode, lastCheck);
   ```

2. 详细错误码解释：
   | 错误码 | 含义               | 解决方案             |
   |------|-------------------|--------------------|
   | 1001 | 驱动句柄无效          | 重新调用SetHandle函数  |
   | 2001 | 键盘设备不可用         | 检查系统键盘设备是否正常   |
   | 3001 | 鼠标设备不可用         | 检查系统鼠标设备是否正常   |

3. 状态监控示例：
   ```c
   // 定期检查驱动状态
   HANDLE hTimer = CreateWaitableTimer(NULL, FALSE, NULL);
   
   LARGE_INTEGER liDueTime;
   liDueTime.QuadPart = -10000000; // 1秒后第一次触发
   
   SetWaitableTimer(hTimer, &liDueTime, 5000, NULL, NULL, FALSE); // 每5秒检查一次
   
   while (WaitForSingleObject(hTimer, INFINITE) == WAIT_OBJECT_0) {
       CheckDeviceStatus();
       DEVICE_STATUS status = GetDriverStatus();
       
       if (status != DEVICE_STATUS_READY) {
           int errorCode = GetDetailedErrorCode();
           printf("驱动状态异常: %d, 错误码: %d\n", status, errorCode);
           
           // 尝试修复驱动状态
           if (errorCode == 1001) {
               // 重新设置句柄
               SetHandle();
           }
       }
   }
   ```

## 驱动验证

### 验证工具
1. 完整验证（推荐用于开发测试）
   ```cmd
   verifier /flags 0xFF /driver lykeys.sys
   ```

2. 基本验证（推荐用于日常测试）
   ```cmd
   verifier /standard /driver lykeys.sys
   ```

3. 内存验证（针对内存问题）
   ```cmd
   verifier /flags 0x5 /driver lykeys.sys
   ```

4. IRQL验证（针对IRQL问题）
   ```cmd
   verifier /flags 0x2 /driver lykeys.sys
   ```

### 本地内核调试
1. 配置调试：
   ```cmd
   bcdedit /debug on
   bcdedit /dbgsettings local
   ```

2. 添加详细日志：
   ```c
   KdPrint(("Driver State: %d, IRQL: %d\n", state, KeGetCurrentIrql()));
   KdPrint(("Callback Address: 0x%p\n", callback));
   KdPrint(("Memory Region: 0x%p, Size: %d\n", address, size));
   ```

## 常见问题

### 驱动服务问题
1. 手动停止服务：
   ```cmd
   sc query lykeys
   sc stop lykeys
   sc delete lykeys
   ```

2. 快捷命令：
   ```cmd
   @echo off && sc query lykeys > nul 2>&1 && (echo Service exists, stopping... && sc stop lykeys > nul 2>&1 && timeout /t 2 /nobreak > nul && sc delete lykeys > nul 2>&1 && echo Service deleted successfully && exit) || (echo Service does not exist && exit)
   ```

### 驱动签名问题
1. 测试模式设置：
   ```cmd
   # 禁用强制驱动签名 & 测试模式 & 重启
   bcdedit /set nointegritychecks on
   bcdedit /set testsigning on
   shutdown -r -t 0
   ```

2. 签名验证：
   - 检查驱动签名证书
   - 验证签名时间戳
   - 确认签名链完整性

## 调试技巧

### 日志记录
1. 添加详细日志：
   ```c
   // 记录关键操作
   KdPrint(("Operation: %s\n", operation));
   
   // 记录参数信息
   KdPrint(("Parameters: %d, %d\n", param1, param2));
   
   // 记录错误信息
   KdPrint(("Error: %d\n", error));
   ```

2. 日志分析：
   - 使用日志分析工具
   - 过滤关键信息
   - 追踪问题根源

### 增强的状态监控
1. 实现定期状态检查：
   ```c#
   // C#中实现的状态监控服务
   public class DriverStatusMonitor
   {
       private Timer _statusTimer;
       private int _failedChecks = 0;
       
       public void StartMonitoring(int interval = 5000)
       {
           _statusTimer = new Timer(CheckStatus, null, 0, interval);
       }
       
       private void CheckStatus(object state)
       {
           CheckDeviceStatus();
           DEVICE_STATUS status = GetDriverStatus();
           
           if (status != DEVICE_STATUS_READY)
           {
               int errorCode = GetDetailedErrorCode();
               Console.WriteLine($"驱动状态异常: {status}, 错误码: {errorCode}");
               
               _failedChecks++;
               if (_failedChecks >= 3)
               {
                   Console.WriteLine("尝试恢复驱动...");
                   RecoverDriver();
                   _failedChecks = 0;
               }
           }
           else
           {
               _failedChecks = 0;
           }
       }
       
       private void RecoverDriver()
       {
           // 实现驱动恢复逻辑
           // 例如：重新设置驱动句柄
           SetHandle();
       }
   }
   ```
