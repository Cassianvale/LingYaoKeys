# 使用示例

## C# 示例

### 基本使用
```csharp
using System;
using System.Runtime.InteropServices;

public class LyKeysExample
{
    // 枚举设备状态
    public enum DEVICE_STATUS
    {
        DEVICE_STATUS_UNKNOWN = 0,     // 未知状态
        DEVICE_STATUS_READY = 1,       // 准备就绪
        DEVICE_STATUS_ERROR = 2,       // 错误状态
        DEVICE_STATUS_NO_KEYBOARD = 3, // 无法找到键盘设备
        DEVICE_STATUS_NO_MOUSE = 4,    // 无法找到鼠标设备
        DEVICE_STATUS_INIT_FAILED = 5  // 初始化失败
    }

    // 导入驱动函数
    [DllImport("lykeysdll.dll")]
    private static extern bool LoadNTDriver(string driverName, string driverPath);

    [DllImport("lykeysdll.dll")]
    private static extern bool SetHandle();

    [DllImport("lykeysdll.dll")]
    private static extern void KeyDown(ushort virtualKey);

    [DllImport("lykeysdll.dll")]
    private static extern void KeyUp(ushort virtualKey);
    
    [DllImport("lykeysdll.dll")]
    private static extern DEVICE_STATUS GetDriverStatus();
    
    [DllImport("lykeysdll.dll")]
    private static extern int GetDetailedErrorCode();
    
    [DllImport("lykeysdll.dll")]
    private static extern void CheckDeviceStatus();

    public static void Main()
    {
        try
        {
            // 加载驱动
            if (!LoadNTDriver("lykeys", "lykeys.sys"))
            {
                Console.WriteLine("驱动加载失败");
                return;
            }

            // 设置句柄
            if (!SetHandle())
            {
                Console.WriteLine("句柄设置失败");
                return;
            }
            
            // 检查设备状态
            CheckDeviceStatus();
            DEVICE_STATUS status = GetDriverStatus();
            
            if (status != DEVICE_STATUS_READY)
            {
                int errorCode = GetDetailedErrorCode();
                Console.WriteLine($"设备未就绪，状态: {status}, 错误码: {errorCode}");
                return;
            }

            // 模拟按键
            KeyDown(0x41); // 按下A键
            System.Threading.Thread.Sleep(100);
            KeyUp(0x41);   // 抬起A键
        }
        catch (Exception ex)
        {
            Console.WriteLine($"错误: {ex.Message}");
        }
    }
}
```

### 高级功能
```csharp
public class LyKeysAdvanced
{
    // 导入更多驱动函数
    [DllImport("lykeysdll.dll")]
    private static extern void MouseMoveRELATIVE(long dx, long dy);

    [DllImport("lykeysdll.dll")]
    private static extern void MouseLeftButtonDown();

    [DllImport("lykeysdll.dll")]
    private static extern void MouseLeftButtonUp();
    
    [DllImport("lykeysdll.dll")]
    private static extern ulong GetLastCheckTime();

    public static void SimulateMouseClick(int x, int y)
    {
        // 移动鼠标
        MouseMoveRELATIVE(x, y);

        // 点击左键
        MouseLeftButtonDown();
        System.Threading.Thread.Sleep(50);
        MouseLeftButtonUp();
    }
    
    // 驱动状态监控示例
    public static void MonitorDriverStatus()
    {
        Timer statusTimer = new Timer(state => 
        {
            CheckDeviceStatus();
            DEVICE_STATUS status = GetDriverStatus();
            if (status != DEVICE_STATUS_READY)
            {
                int errorCode = GetDetailedErrorCode();
                ulong lastCheckTime = GetLastCheckTime();
                
                Console.WriteLine($"驱动状态异常: {status}");
                Console.WriteLine($"详细错误码: {errorCode}");
                Console.WriteLine($"上次检查时间: {lastCheckTime}");
                
                // 执行恢复逻辑...
            }
        }, null, 0, 5000); // 每5秒检查一次
    }
}
```

## Python 示例

### 运行示例代码
- 1. 以管理员方式启动VSCode
- 2. 打开 `Python` 示例库 `cd Resource\lykeysdll\python_example`
- 3. 创建`python3.10`环境，使用`pip`或`conda`安装win32gui库

::: code-group
```python[pip]
pip install pywin32

```

```python[uv]
uv pip install pywin32
```
:::

- 4. 运行示例代码
**`gui.py` 分为两种模式运行：**

::: code-group
```python[Normal]
python gui.py
```
```python[Debug]
python gui.py --debug
```
:::



### 基本使用

```python
import ctypes
import time

# 定义设备状态枚举
DEVICE_STATUS_UNKNOWN = 0     # 未知状态
DEVICE_STATUS_READY = 1       # 准备就绪
DEVICE_STATUS_ERROR = 2       # 错误状态
DEVICE_STATUS_NO_KEYBOARD = 3 # 无法找到键盘设备
DEVICE_STATUS_NO_MOUSE = 4    # 无法找到鼠标设备
DEVICE_STATUS_INIT_FAILED = 5 # 初始化失败

# 加载DLL
lykeys = ctypes.WinDLL("lykeysdll.dll")

# 设置函数参数类型
lykeys.LoadNTDriver.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
lykeys.LoadNTDriver.restype = ctypes.c_bool

lykeys.SetHandle.restype = ctypes.c_bool
lykeys.GetDriverStatus.restype = ctypes.c_int
lykeys.GetDetailedErrorCode.restype = ctypes.c_int
lykeys.CheckDeviceStatus.restype = None

lykeys.KeyDown.argtypes = [ctypes.c_ushort]
lykeys.KeyUp.argtypes = [ctypes.c_ushort]

def main():
    try:
        # 加载驱动
        if not lykeys.LoadNTDriver(b"lykeys", b"lykeys.sys"):
            print("驱动加载失败")
            return

        # 设置句柄
        if not lykeys.SetHandle():
            print("句柄设置失败")
            return
            
        # 检查设备状态
        lykeys.CheckDeviceStatus()
        status = lykeys.GetDriverStatus()
        
        if status != DEVICE_STATUS_READY:
            error_code = lykeys.GetDetailedErrorCode()
            print(f"设备未就绪，状态: {status}, 错误码: {error_code}")
            
            # 根据状态进行处理
            if status == DEVICE_STATUS_NO_KEYBOARD:
                print("无法找到键盘设备")
            elif status == DEVICE_STATUS_NO_MOUSE:
                print("无法找到鼠标设备")
            return

        # 模拟按键
        lykeys.KeyDown(0x41)  # 按下A键
        time.sleep(0.1)
        lykeys.KeyUp(0x41)    # 抬起A键

    except Exception as e:
        print(f"错误: {e}")

if __name__ == "__main__":
    main()
```

### 高级功能
```python
class LyKeysAdvanced:
    def __init__(self):
        self.lykeys = ctypes.WinDLL("lykeysdll.dll")
        self._setup_functions()
        self.DEVICE_STATUS_READY = 1  # 设备就绪状态码

    def _setup_functions(self):
        # 设置函数参数类型和返回值类型
        self.lykeys.MouseMoveRELATIVE.argtypes = [ctypes.c_long, ctypes.c_long]
        self.lykeys.MouseLeftButtonDown.restype = None
        self.lykeys.MouseLeftButtonUp.restype = None
        self.lykeys.GetDriverStatus.restype = ctypes.c_int
        self.lykeys.GetDetailedErrorCode.restype = ctypes.c_int
        self.lykeys.GetLastCheckTime.restype = ctypes.c_ulonglong
        
    def check_driver_health(self):
        """检查驱动健康状态"""
        self.lykeys.CheckDeviceStatus()
        status = self.lykeys.GetDriverStatus()
        
        if status != self.DEVICE_STATUS_READY:
            error_code = self.lykeys.GetDetailedErrorCode()
            last_check = self.lykeys.GetLastCheckTime()
            print(f"驱动状态异常: {status}, 错误码: {error_code}, 上次检查: {last_check}")
            return False
        return True

    def click_at(self, x, y):
        """在指定位置点击"""
        # 先检查驱动状态
        if not self.check_driver_health():
            return False
            
        # 移动鼠标
        self.lykeys.MouseMoveRELATIVE(x, y)

        # 点击左键
        self.lykeys.MouseLeftButtonDown()
        time.sleep(0.05)
        self.lykeys.MouseLeftButtonUp()
        return True
        
    def start_monitoring(self, interval=5.0):
        """启动状态监控"""
        import threading
        
        def monitor():
            while True:
                self.check_driver_health()
                time.sleep(interval)
                
        threading.Thread(target=monitor, daemon=True).start()

def main():
    lykeys = LyKeysAdvanced()
    lykeys.start_monitoring()  # 后台驱动状态监控(可选)
    if lykeys.click_at(100, 100):  # 在坐标(100,100)处点击
        print("点击操作成功")

if __name__ == "__main__":
    main()
```
