# 使用示例

## C# 示例

### 基本使用
```csharp
using System;
using System.Runtime.InteropServices;

public class LyKeysExample
{
    // 导入驱动函数
    [DllImport("lykeysdll.dll")]
    private static extern bool LoadNTDriver(string driverName, string driverPath);

    [DllImport("lykeysdll.dll")]
    private static extern bool SetHandle();

    [DllImport("lykeysdll.dll")]
    private static extern void KeyDown(ushort virtualKey);

    [DllImport("lykeysdll.dll")]
    private static extern void KeyUp(ushort virtualKey);

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

    public static void SimulateMouseClick(int x, int y)
    {
        // 移动鼠标
        MouseMoveRELATIVE(x, y);

        // 点击左键
        MouseLeftButtonDown();
        System.Threading.Thread.Sleep(50);
        MouseLeftButtonUp();
    }
}
```

## Python 示例

### 基本使用
```python
import ctypes
import time

# 加载DLL
lykeys = ctypes.WinDLL("lykeysdll.dll")

# 设置函数参数类型
lykeys.LoadNTDriver.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
lykeys.LoadNTDriver.restype = ctypes.c_bool

lykeys.SetHandle.restype = ctypes.c_bool

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

    def _setup_functions(self):
        # 设置函数参数类型
        self.lykeys.MouseMoveRELATIVE.argtypes = [ctypes.c_long, ctypes.c_long]
        self.lykeys.MouseLeftButtonDown.restype = None
        self.lykeys.MouseLeftButtonUp.restype = None

    def click_at(self, x, y):
        # 移动鼠标
        self.lykeys.MouseMoveRELATIVE(x, y)

        # 点击左键
        self.lykeys.MouseLeftButtonDown()
        time.sleep(0.05)
        self.lykeys.MouseLeftButtonUp()

def main():
    lykeys = LyKeysAdvanced()
    lykeys.click_at(100, 100)  # 在坐标(100,100)处点击

if __name__ == "__main__":
    main()
```

## 注意事项

### 性能优化
- 使用高精度计时器
- 合理设置延迟

### 错误处理
- 检查返回值
- 捕获异常
- 记录日志

### 线程安全
- 避免多线程同时调用
- 使用同步机制
- 注意资源释放 