# Frequently Asked Questions

## Installation Related

### System Requirements
**Q: Which Windows versions does LingYaoKeys support?**  

A: Windows 10/11 are supported. Windows 10/11 are recommended. Windows 7 has not been tested and may cause unexpected issues.

**Q: Do I need to install the .NET runtime?**  

A: Yes, .NET 8.0 Runtime is required.

**Q: Why does it need administrator privileges?**  

A: Administrator privileges are required to install and run the kernel-level driver.

### Installation Issues
**Q: What should I do if I get a "Driver signature invalid" message during installation?**  

A: Please ensure you download the software from official channels and do not modify the driver files. If you are in a testing environment, you can:
1. Disable secure boot
2. Enable test mode
3. Restart your system

**Q: The program doesn't start after installation. What could be wrong?**

A: Please check the following:
1. Verify that .NET 8.0 Runtime is properly installed
2. Check if your antivirus software is blocking the application
3. Run the application as administrator
4. Check the error logs

## Usage Related

### Basic Operations
**Q: How to set up global hotkeys?**

A: Click "Add Hotkey" on the main interface, select the keys you want to trigger, and set the trigger conditions and interval time.

**Q: How to configure side button triggers?**

A: In the "Side Button Settings", select the side button you want to use, and configure the trigger behavior and interval time.

**Q: How to adjust key intervals?**

A: You can set millisecond-level key intervals in the key settings. It's recommended to adjust based on your actual needs.

### Advanced Features
**Q: What is the "Reduce Sticking" feature?**

A: This is a feature optimized for gaming scenarios that reduces in-game sticking phenomena by adjusting key intervals.

**Q: How to customize audio prompts?**

A: Open the `C:\Users\username\.lykeys\sound` directory, and replace the `start.mp3`/`stop.mp3` files.

**Q: How to configure the floating window display?**

A: Enable the floating window feature on the main interface. You can adjust transparency, position, and display content.

## Driver Related

### Driver Installation
**Q: What should I do if driver installation fails?**

A: Please check:
1. If your system is supported
2. If you have administrator privileges
3. If secure boot is disabled
4. Review the error logs

**Q: How to manually uninstall the driver?**

A: Use the following commands:
```cmd
sc stop lykeys
sc delete lykeys
```

### Driver Usage
**Q: What devices does the driver support?**

A: USB and PS2 keyboard and mouse devices are supported.

**Q: Does the driver support hot-swapping?**

A: Yes, device hot-swapping is supported.

**Q: How to check driver status?**

A: You can check the status through the program interface or using the driver API.

## Performance Related

### Performance Optimization
**Q: How to optimize key speed?**

A: Recommendations:
1. Set reasonable key intervals
2. Enable the "Reduce Sticking" feature
3. Avoid triggering too many keys simultaneously

**Q: What to do if the program uses too many resources?**

A: You can:
1. Reduce the number of simultaneously triggered keys
2. Increase key interval times
3. Turn off unnecessary features

### Stability
**Q: What to do if the program occasionally lags?**

A: Recommendations:
1. Check system resource usage
2. Update to the latest version
3. Clean system cache

**Q: How to improve program stability?**

A: You can:
1. Update the program regularly
2. Keep your system updated
3. Avoid running other similar programs simultaneously

## Other Issues

### Technical Support
**Q: How to get technical support?**

A: You can get support through:
1. GitHub Issues
2. Project documentation
3. Community discussions

**Q: How to report issues?**

A: Please provide:
1. System environment information
2. Detailed problem description
3. Error logs
4. Steps to reproduce

### Update Related
**Q: How to update the program?**

A: Download the latest version installation package from GitHub Releases to update.

**Q: Do I need to reconfigure after updating?**

A: No, configuration information will be automatically preserved. 