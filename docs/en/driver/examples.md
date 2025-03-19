# Usage Examples

## C# Examples

### Basic Usage
```csharp
using System;
using System.Runtime.InteropServices;

public class LyKeysExample
{
    // Import driver functions
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
            // Load driver
            if (!LoadNTDriver("lykeys", "lykeys.sys"))
            {
                Console.WriteLine("Failed to load driver");
                return;
            }

            // Set handle
            if (!SetHandle())
            {
                Console.WriteLine("Failed to set handle");
                return;
            }

            // Simulate key press
            KeyDown(0x41); // Press A key
            System.Threading.Thread.Sleep(100);
            KeyUp(0x41);   // Release A key
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}
```

### Advanced Features
```csharp
public class LyKeysAdvanced
{
    // Import more driver functions
    [DllImport("lykeysdll.dll")]
    private static extern void MouseMoveRELATIVE(long dx, long dy);

    [DllImport("lykeysdll.dll")]
    private static extern void MouseLeftButtonDown();

    [DllImport("lykeysdll.dll")]
    private static extern void MouseLeftButtonUp();

    public static void SimulateMouseClick(int x, int y)
    {
        // Move mouse
        MouseMoveRELATIVE(x, y);

        // Click left button
        MouseLeftButtonDown();
        System.Threading.Thread.Sleep(50);
        MouseLeftButtonUp();
    }
}
```

## Python Examples

### Running Example Code
- 1. Start VSCode as administrator
- 2. Open the `Python` example library `cd Resource\lykeysdll\python_example`
- 3. Create a `python3.10` environment, install win32gui library using `pip` or `conda`

::: code-group
```python[pip]
pip install pywin32

```

```python[uv]
uv pip install pywin32
```
:::

- 4. Run the example code
**`gui.py` can be run in two modes:**

::: code-group
```python[Normal]
python gui.py
```
```python[Debug]
python gui.py --debug
```
:::

### Basic Usage

```python
import ctypes
import time

# Load DLL
lykeys = ctypes.WinDLL("lykeysdll.dll")

# Set function parameter types
lykeys.LoadNTDriver.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
lykeys.LoadNTDriver.restype = ctypes.c_bool

lykeys.SetHandle.restype = ctypes.c_bool

lykeys.KeyDown.argtypes = [ctypes.c_ushort]
lykeys.KeyUp.argtypes = [ctypes.c_ushort]

def main():
    try:
        # Load driver
        if not lykeys.LoadNTDriver(b"lykeys", b"lykeys.sys"):
            print("Failed to load driver")
            return

        # Set handle
        if not lykeys.SetHandle():
            print("Failed to set handle")
            return

        # Simulate key press
        lykeys.KeyDown(0x41)  # Press A key
        time.sleep(0.1)
        lykeys.KeyUp(0x41)    # Release A key

    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    main()
```

### Advanced Features
```python
class LyKeysAdvanced:
    def __init__(self):
        self.lykeys = ctypes.WinDLL("lykeysdll.dll")
        self._setup_functions()

    def _setup_functions(self):
        # Set function parameter types
        self.lykeys.MouseMoveRELATIVE.argtypes = [ctypes.c_long, ctypes.c_long]
        self.lykeys.MouseLeftButtonDown.restype = None
        self.lykeys.MouseLeftButtonUp.restype = None

    def click_at(self, x, y):
        # Move mouse
        self.lykeys.MouseMoveRELATIVE(x, y)

        # Click left button
        self.lykeys.MouseLeftButtonDown()
        time.sleep(0.05)
        self.lykeys.MouseLeftButtonUp()

def main():
    lykeys = LyKeysAdvanced()
    lykeys.click_at(100, 100)  # Click at coordinates (100,100)

if __name__ == "__main__":
    main()
```

## Notes

### Performance Optimization
- Use high-precision timer
- Set reasonable delays

### Error Handling
- Check return values
- Catch exceptions
- Log errors

### Thread Safety
- Avoid concurrent calls
- Use synchronization mechanisms
- Pay attention to resource release 