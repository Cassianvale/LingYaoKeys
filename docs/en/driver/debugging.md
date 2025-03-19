# Debugging Guide

## Debugging Tools

### WinDbg
1. Installation Steps:
   ```powershell
   # Install WinDbg using winget
   winget install Microsoft.WinDbg
   ```

2. Configure system to generate complete dump:
   ```
   1. Right-click "This PC" -> Properties
   2. Advanced system settings -> Advanced -> Startup and Recovery -> Settings
   3. Under "Write debugging information" select "Complete memory dump"
   4. Ensure "Automatically restart" is checked
   ```

3. Using WinDbg for analysis:
   ```
   1. Open WinDbg
   2. File -> Open Crash Dump
   3. Select C:\Windows\MEMORY.DMP or Minidump file
   ```

4. Common commands:
   ```
   !analyze -v    # Detailed crash analysis
   .bugcheck      # Display blue screen code
   kb             # Display call stack
   lmvm lykeys    # Display driver information
   ```

### BlueScreenView
1. Usage steps:
   ```
   1. Download and install BlueScreenView
   2. When run, it automatically displays all blue screen records
   3. View:
      - Blue screen code
      - Occurrence time
      - Driver causing the crash
      - Call stack information
   ```

2. Analysis information:
   ```
   Information needed
   {
       1. Basic information:
       - Blue screen code (e.g.: 0x0000007E)
       - Occurrence time
       - System version
       2. Detailed information:
       - Name of driver causing the crash
       - Call stack at time of crash
       - Related memory addresses
       3. Environment information:
       - System configuration
       - Installed drivers
       - Hardware information
   }
   ```

## Driver Verification

### Verification Tools
1. Complete verification (recommended for development testing)
   ```cmd
   verifier /flags 0xFF /driver lykeys.sys
   ```

2. Basic verification (recommended for daily testing)
   ```cmd
   verifier /standard /driver lykeys.sys
   ```

3. Memory verification (for memory issues)
   ```cmd
   verifier /flags 0x5 /driver lykeys.sys
   ```

4. IRQL verification (for IRQL issues)
   ```cmd
   verifier /flags 0x2 /driver lykeys.sys
   ```

### Local Kernel Debugging
1. Configure debugging:
   ```cmd
   bcdedit /debug on
   bcdedit /dbgsettings local
   ```

2. Add detailed logs:
   ```c
   KdPrint(("Driver State: %d, IRQL: %d\n", state, KeGetCurrentIrql()));
   KdPrint(("Callback Address: 0x%p\n", callback));
   KdPrint(("Memory Region: 0x%p, Size: %d\n", address, size));
   ```

## Common Issues

### Driver Service Issues
1. Manually stop service:
   ```cmd
   sc query lykeys
   sc stop lykeys
   sc delete lykeys
   ```

2. Quick command:
   ```cmd
   @echo off && sc query lykeys > nul 2>&1 && (echo Service exists, stopping... && sc stop lykeys > nul 2>&1 && timeout /t 2 /nobreak > nul && sc delete lykeys > nul 2>&1 && echo Service deleted successfully && exit) || (echo Service does not exist && exit)
   ```

### Driver Signature Issues
1. Test mode settings:
   ```cmd
   # Disable forced driver signing & enable test mode & restart
   bcdedit /set nointegritychecks on
   bcdedit /set testsigning on
   shutdown -r -t 0
   ```

2. Signature verification:
   - Check driver signature certificate
   - Verify signature timestamp
   - Confirm signature chain integrity

## Debugging Tips

### Logging
1. Add detailed logs:
   ```c
   // Record key operations
   KdPrint(("Operation: %s\n", operation));
   
   // Record parameter information
   KdPrint(("Parameters: %d, %d\n", param1, param2));
   
   // Record error information
   KdPrint(("Error: %d\n", error));
   ```

2. Log analysis:
   - Use log analysis tools
   - Filter key information
   - Trace problem source 