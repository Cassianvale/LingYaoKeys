# About the Project

## What is LingYaoKeys?

LingYaoKeys is a modern key tool developed based on .NET8.0 and WPF, offering rich features and an elegant user interface. The tool is designed with MVVM architecture and supports global hotkeys, side button and scroll wheel triggers, independent key intervals, coordinate movement, and more.

## System Requirements

- Windows 10/11 recommended (Windows 7 has not been tested and may cause unexpected issues)
- Run the program as administrator (for driver installation)
- .NET 8.0 Runtime environment (if you get the prompt `You must install .NET Desktop Runtime to run this application.` when running the program, please download: [.NET 8.0 Runtime](https://download.visualstudio.microsoft.com/download/pr/64760cc4-228f-48e4-b57d-55f882dedc69/b181f927cb937ef06fbb6eb41e81fbd0/windowsdesktop-runtime-8.0.14-win-x64.exe))

## Main Features

### Basic Features
- Support for global hotkey registration
- Support for sequence/press hotkey mode switching
- Support for side button and scroll wheel triggers
- Support for mouse movement to corresponding coordinates, with coordinate input and editing capabilities
- Independent intervals for each key and coordinate
- Support for window handle detection, when enabled hotkeys only trigger for the corresponding window
- Voice prompt toggle, volume settings, customizable key on/off voice prompts
- Normal/Reduce Sticking mode toggle switch
- Keys and coordinates support drag-and-drop sorting
- Floating window display showing key start/stop/disabled status
- Support for input method toggle, automatically switches to ENG output when hotkeys are triggered
- Support for configuration export/import, online updates, debug mode, and more

### Driver Features
- Implemented using kernel-level DeviceIoControl driver
- Support for offline operation
- Support for 32-bit/64-bit system architectures
- Support for USB/PS2 keyboard and mouse devices
- Compatible with Win10/Win11 systems
- Support for driver hot-swapping, traceless uninstallation when the program exits

## Technology Stack

- Development Language: C#
- Framework: .NET 8.0
- UI Framework: WPF
- Architecture Pattern: MVVM
- Driver Development: Windows Driver Kit (WDK)
- Build Tool: Visual Studio 2022 