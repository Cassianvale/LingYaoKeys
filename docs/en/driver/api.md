# Driver API Documentation

## API List

### Driver Management APIs

<div class="driver-api-table">

| Function Name     | Parameters                                    | Return Type     | Description  | Example                                |
|------------------|-----------------------------------------------|----------------|--------------|----------------------------------------|
| LoadNTDriver     | char* lpszDriverName, char* lpszDriverPath    | BOOL           | Load NT driver | LoadNTDriver("lykeys", "lykeys.sys")  |
| UnloadNTDriver   | char* szSvrName                               | BOOL           | Unload NT driver | UnloadNTDriver("lykeys")              |
| SetHandle        | void                                          | BOOL           | Set driver handle | SetHandle()                           |
| GetDriverHandle  | void                                          | HANDLE         | Get driver handle | GetDriverHandle()                     |
| GetDriverStatus  | void                                          | DEVICE_STATUS  | Get driver status | GetDriverStatus()                     |
| GetLastCheckTime | void                                          | ULONGLONG      | Get last check time | GetLastCheckTime()                    |

</div>

### Keyboard Operation APIs

<div class="keyboard-api-table">

| Function Name | Parameters           | Return Type | Description      | Example                     |
|--------------|---------------------|-------------|------------------|-----------------------------|
| KeyDown      | USHORT VirtualKey   | void        | Simulate key down | KeyDown(0x41) // Press A    |
| KeyUp        | USHORT VirtualKey   | void        | Simulate key up   | KeyUp(0x41) // Release A    |

</div>

### Mouse Operation APIs

<div class="mouse-api-table">

| Function Name              | Parameters           | Return Type | Description      | Example                          |
|---------------------------|---------------------|-------------|------------------|----------------------------------|
| MouseMoveRELATIVE         | LONG dx, LONG dy    | void        | Mouse relative move | MouseMoveRELATIVE(10, 20)      |
| MouseMoveABSOLUTE         | LONG dx, LONG dy    | void        | Mouse absolute move | MouseMoveABSOLUTE(100, 200)    |
| MouseLeftButtonDown       | void                | void        | Left button down   | MouseLeftButtonDown()           |
| MouseLeftButtonUp         | void                | void        | Left button up     | MouseLeftButtonUp()             |
| MouseRightButtonDown      | void                | void        | Right button down  | MouseRightButtonDown()          |
| MouseRightButtonUp        | void                | void        | Right button up    | MouseRightButtonUp()            |
| MouseMiddleButtonDown     | void                | void        | Middle button down | MouseMiddleButtonDown()         |
| MouseMiddleButtonUp       | void                | void        | Middle button up   | MouseMiddleButtonUp()           |
| MouseXButton1Down         | void                | void        | X1 button down     | MouseXButton1Down()             |
| MouseXButton1Up           | void                | void        | X1 button up       | MouseXButton1Up()               |
| MouseXButton2Down         | void                | void        | X2 button down     | MouseXButton2Down()             |
| MouseXButton2Up           | void                | void        | X2 button up       | MouseXButton2Up()               |
| MouseWheelUp              | USHORT wheelDelta   | void        | Wheel up           | MouseWheelUp(120)               |
| MouseWheelDown            | USHORT wheelDelta   | void        | Wheel down         | MouseWheelDown(120)             |

</div>

## Usage Guide

### Initialize Driver
```c
// Load driver
LoadNTDriver("lykeys", "lykeys.sys");

// Set handle
SetHandle();

// Check status
DEVICE_STATUS status = GetDriverStatus();
if (status == DEVICE_STATUS_READY) {
    // Driver is ready
}
```

### Keyboard Operation
```c
// Press A key
KeyDown(0x41);

// Wait for a while
Sleep(100);

// Release A key
KeyUp(0x41);
```

### Mouse Operation
```c
// Move mouse
MouseMoveRELATIVE(10, 20);

// Click left button
MouseLeftButtonDown();
Sleep(50);
MouseLeftButtonUp();

// Scroll mouse
MouseWheelUp(120);
```

## Notes

### Thread Safety
- All APIs are thread-safe
- No additional synchronization needed in multi-threaded environment
- Recommended to load/unload driver in main thread

### Error Handling
- Check function return values
- Use GetLastError to get error codes
- Implement error retry mechanism

### Performance Optimization
- Set reasonable key intervals
- Avoid frequent calls
- Release resources timely 