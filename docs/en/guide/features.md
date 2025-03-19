# Feature Description

## Basic Features

### Global Hotkeys
- Support for custom global hotkey combinations
- Support for single keys and key combinations
- Hotkey conflict detection

### Side Button & Scroll Wheel Triggers
- Support for mouse side button triggers
- Support for scroll wheel up/down triggers
- Customizable hotkey trigger behavior
- Support for combination trigger modes

### Independent Key Intervals
- Each key can have its own independent interval time
- Support for millisecond-level precise control
- Support for dynamic interval adjustment

### Window Handle Detection
- Intelligent target window identification with automatic polling and window status monitoring
- Support for automatic reacquisition of previous handles after window restart
- Support for manual window specification, after which hotkeys will only trigger for the specified window

### Sequence/Press Mode
- Support for key sequence mode toggle trigger
- Support for press mode continuous trigger when held down

### Coordinate Movement
- Support for coordinate positioning
- Support for absolute mouse movement to corresponding coordinates
- Real-time coordinate changes in edit mode
- Intelligent DPI scaling handling

### Voice Prompts
- Support for voice prompt toggle
- Customizable hotkey trigger sound effects
- Support for volume adjustment, audio device not mandatory
- Support for silent mode

### Anti-Sticking Mode
> [!IMPORTANT]
> This feature is only for specific gaming scenarios. If it affects your gaming experience, please turn it off.
> Key speeds above 200-300 per second may cause key response delay or sticking movement.
> _**Reduce Sticking feature ON (default)**_: Average key speed tested at 120+, suitable for specific gaming scenarios
> _**Reduce Sticking feature OFF**_: Removes key speed limits with average speed of 320+, suitable for normal scenarios

- Support for Normal/Reduce Sticking toggle
- Optimized for gaming scenarios

### Key Sorting
- Key/coordinate lists support drag-and-drop sorting with real-time sequence changes
- Support for batch operations

### Floating Window Display
- Floating window status synchronized with hotkey activation status in real-time
- Support for keeping the floating window on top
- Support for right-click menu display, double-click to show main program
- Support for position memory

### Settings
- Support for import/export
- Support for online update checks
- All configurations automatically saved

### Performance
- Support for debug mode
- Support for global hardware acceleration and rendering optimization
- Support for PerMonitorV2 high DPI awareness

## Advanced Features

### Driver Management
- Support for driver hot-swapping
- Support for driver status monitoring

### Security Protection
- Support for anti-Hook protection
- Support for memory protection
- Support for driver signature verification
- Support for exception handling

### System Compatibility
- Support for 32-bit/64-bit systems
- Support for USB/PS2 devices
- Support for multiple system versions
- Support for high DPI displays 