# Driver Documentation

## Driver Overview

> [!WARNING]
> Windows 7 is not tested and may cause unpredictable issues

LingYao Keys uses kernel-level driver to implement key simulation functionality, communicating with the driver through DeviceIoControl. The driver supports 32-bit/64-bit systems and is compatible with Windows 10/11.

### Driver Features
- Based on DeviceIoControl implementation
- Supports offline operation
- Comprehensive anti-Hook protection
- Supports hot-plugging
- Supports multiple devices

### System Requirements
- Windows 10/11
- 32-bit/64-bit systems
- Administrator privileges
- Disable Secure Boot (for test driver mode)

### Driver Files
- `lykeysdll.dll`: Core driver dynamic link library
- `lykeys.sys`: Kernel-level driver file
- `lykeys.cat`: Driver signature file
- `README.md`: Driver interface documentation

## Quick Start

### Installing the Driver
1. Run the program as administrator
2. The program will automatically install the driver
3. If installation fails, check system settings

### Uninstalling the Driver
1. The driver will automatically uninstall when the program exits normally
2. If uninstallation fails, use command line:
   ```cmd
   sc stop lykeys
   sc delete lykeys
   ```

3. Quick command:
   ```cmd
   @echo off && sc query lykeys > nul 2>&1 && (echo Service exists, stopping... && sc stop lykeys > nul 2>&1 && timeout /t 2 /nobreak > nul && sc delete lykeys > nul 2>&1 && echo Service deleted successfully && exit) || (echo Service does not exist && exit)
   ```

### Testing the Driver
1. Run the example program
2. Test basic functionality
3. Check driver status

## Notes

### Security Tips
- Do not modify driver files
- Keep driver signatures intact
- Follow the project for latest driver versions

### Performance Optimization
- Set reasonable key intervals
- Avoid frequent operations
- Monitor system resources

### Troubleshooting
- Check driver status
- View error logs
- Update system patches 