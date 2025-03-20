# Driver Documentation

## Driver Overview
> [!CAUTION]
> ⚠️**Special Note: Regarding anti-cheat matters in games, please do not ask me about whether it can bypass certain game detection. No technical support will be provided for such purposes!!!**

> [!WARNING]
> Windows 7 is untested and may cause unexpected issues

LingYaoKeys uses kernel-level drivers to implement key simulation functionality, communicating with the driver through DeviceIoControl. The driver supports 32-bit/64-bit systems and is compatible with Windows 10/11.

### Driver Features
- Based on DeviceIoControl implementation
- Supports offline operation
- Supports hot-plugging

### System Requirements
- Windows 10/11
- 32-bit/64-bit systems
- Administrator privileges
- Secure Boot disabled (Test Mode)

### Driver Files
- `lykeysdll.dll`: Core driver dynamic link library
- `lykeys.sys`: Kernel-level driver file
- `lykeys.cat`: Driver signature file
- `README.md`: Driver interface documentation

## Quick Start

### Driver Installation
1. Run the program as administrator
2. The program will automatically install the driver
3. If installation fails, check system settings

### Driver Uninstallation
1. The driver will automatically uninstall when exiting normally
2. - If uninstallation fails, use these commands:
   ```cmd
   sc stop lykeys
   sc delete lykeys
   ```
   - Execute quick command (If lykeys service exists, stops and removes the driver service immediately)
   ```cmd
   @echo off && sc query lykeys > nul 2>&1 && (echo Service exists, stopping... && sc stop lykeys > nul 2>&1 && timeout /t 2 /nobreak > nul && sc delete lykeys > nul 2>&1 && echo Service deleted successfully && exit) || (echo Service does not exist && exit)
   ```

## References

- [kmclassdll.dll](https://github.com/BestBurning/kmclassdll/releases) - DLL Library
- [kmclass.sys](https://github.com/BestBurning/kmclass/releases) - Kernel Driver
- For compilation guidance, see [Compiling DLL and Using with Python ctypes](https://di1shuai.com/%E7%BC%96%E8%AF%91dll%E5%B9%B6%E5%9C%A8python%E4%B8%AD%E4%BD%BF%E7%94%A8ctypes%E8%B0%83%E7%94%A8.html)
- Error Codes [Error Codes](https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes)
- [KMDF Hello World](https://docs.microsoft.com/en-us/windows-hardware/drivers/gettingstarted/writing-a-very-small-kmdf--driver)
- [WDK 10](https://docs.microsoft.com/en-us/windows-hardware/drivers/download-the-wdk)

## Important Notes

### Security Tips
- Do not modify driver files
- Maintain driver signature integrity
- Follow project updates for latest driver versions

### Troubleshooting
- Check driver status
- Review error logs
- Follow debug documentation