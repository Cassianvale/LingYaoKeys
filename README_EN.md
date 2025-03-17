<!-- markdownlint-restore -->
<div align="center">

# LingYaoKeys

✨**An Elegant and Flexible Open-Source Keyboard Tool Based on .NET8.0+WPF**✨

<div>
    <img alt="platform" src="https://img.shields.io/badge/platform-Windows-blueviolet">
    <img alt="commit" src="https://img.shields.io/github/commit-activity/m/Cassianvale/LingYaoKeys?color=blue">
    <img alt="release" src="https://img.shields.io/github/v/release/Cassianvale/LingYaoKeys?include_prereleases&style=flat">
    <br>
    <img alt="last-commit" src="https://img.shields.io/github/last-commit/Cassianvale/LingYaoKeys">
    <img alt="issues" src="https://img.shields.io/github/issues/Cassianvale/LingYaoKeys">
    <img alt="license" src="https://img.shields.io/github/license/Cassianvale/LingYaoKeys">
</div>
<div>
    <a href="https://github.com/Cassianvale/LingYaoKeys"><img alt="stars" src="https://img.shields.io/github/stars/Cassianvale/LingYaoKeys?style=social"></a>
    <a href="https://github.com/Cassianvale/LingYaoKeys/releases/latest"><img alt="downloads" src="https://img.shields.io/github/downloads/Cassianvale/LingYaoKeys/total?style=social"></a>
</div>
<br>

[简体中文](./README.md) / English

❤ If you like this project, please give it a `Star`🌟 ❤
</br>
</div>

<!-- markdownlint-restore -->

## ✨ Main Features

### 🎮 Basic Features

- [x] Global hotkey support, **including side buttons and scroll wheel triggers**
- [x] Window handle detection support
- [x] Sequential/Press mode key trigger support
- [x] Voice notification on/off support
- [x] Normal/Game mode switching support
- [x] Custom start/stop audio support
- [x] Drag-and-drop key list sorting support
- [x] Floating window display for key activation status

### 🚀 Driver Features

- [x] Kernel-level driver implementation based on DeviceIoControl
- [x] Offline operation support
- [x] Comprehensive anti-Hook and memory protection mechanisms
- [x] 32-bit/64-bit system architecture support
- [x] USB/PS2 keyboard and mouse device support
- [x] Compatible with Win7/Win10/Win11 systems
- [x] Hot-plug driver support with clean uninstallation upon program exit

## 🌏 Direct Download

You can download the latest version from:

- [Latest Version Download](https://github.com/Cassianvale/LingYaoKeys/releases/latest)
- [View All Versions](https://github.com/Cassianvale/LingYaoKeys/releases)

> Note: Always download the latest version from the GitHub Releases page to ensure you have the latest features and
> security updates.

## 🔧 Driver Usage Instructions

### Driver File Description

- `Resource\lykeysdll\lykeysdll.dll`: Core driver DLL (*Required)
- `Resource\lykeysdll\lykeys.sys`: Kernel-level driver file (*Required)
- `Resource\lykeysdll\lykeys.cat`: Driver signature file
-
`Resource\lykeysdll\README.md`: [Driver Interface & Debug Guide](https://github.com/Cassianvale/LingYaoKeys/blob/main/Resource/lykeysdll/README.md)
- `Resource\lykeysdll\csharp_example\*`: C# Example Code
- `Resource\lykeysdll\python_example\*`: Python Example Code

### ⚠️ Important Notes

1. **Driver Signature**
    - Driver has genuine digital signature
    - Do NOT modify driver files to avoid signature invalidation

2. **System Requirements**
    - Supports Windows 7/10/11 (x86/x64)
    - Requires Administrator privileges

3. **Usage Restrictions**
    - For personal study and research only
    - Reverse engineering or modification prohibited

## 📖 Usage Instructions

> [!IMPORTANT]
> After extensive testing and comparing with other key tools, key speeds exceeding 200-300 per second can cause input
> lag or position shifts (depending on CPU and memory performance). This is likely due to Windows' message handling
> mechanism. For gaming, extremely high key speeds are not necessary. Based on test results, I've implemented a game mode
> with optimized settings.
> DEFAULT_KEY_PRESS_INTERVAL: Fixed time between key press and release
> MIN_KEY_INTERVAL: Minimum configurable interval between keys

- _**Game Mode ON (Default)**_: Average key speed of 120+, suitable for gaming
- _**Game Mode OFF**_: Unlimited key speed with average of 320+, suitable for general use
- _**Custom Audio**_: Open `C:\Users\username\.lykeys\sound` and replace `start.mp3`/`stop.mp3` files

## 📃 Common Issues

Since this project uses Microsoft's latest `.Net Core 8.0`, some users may need to download the runtime
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/download_core.png" heigh="400px"/>

## 🖼️ Project Showcase

<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/home.png" width="500px"/>
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/about.png" width="500px"/>
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/keys.png" width="700px"/>

## 🎙 About & Suggestions

- This project is my first attempt at developing with `C#`, `WPF`, and `Cursor AI` technology stack during my spare time
- The project is in its early development stage with new features being continuously added. If you have any suggestions
  for the software, feel free to raise them in `Issues`. If you're interested in the project, welcome to join the
  discussion
- If you like the design philosophy of this software, feel free to submit a `pr`. Thank you very much for your support!

## ⚙️ Development

### Run

- `dotnet run`

### Build & Package

- `dotnet publish -c Release`

## ☕️ Buy me a coffee

**The driver signature was self-funded. If you like this project, your support would be a great encouragement to me**

<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/wechat_qr.png" width="200px"/>

## 📢 Disclaimer

- **For personal study and research use only, commercial and illegal use is prohibited**
- **The developer reserves the final right of interpretation for this project**
- **Strictly prohibited for any use that violates the laws and regulations of
  the `People's Republic of China (including Taiwan Province)` or the user's region**
- **Users must comply with relevant laws and regulations when using this project and must not use it for any commercial
  or illegal purposes. In case of violation, all consequences shall be borne by the user. Meanwhile, users should bear
  the risks and responsibilities arising from using this project. The project developer makes no warranties regarding
  the services and content provided by this project**
- **If you encounter merchants charging for this software, any resulting issues and consequences are not related to this
  project**

## 📜 Open Source License

[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

LingYaoKeys is licensed under [GNU General Public License v3.0](LICENSE)

Copyright © 2025 by Cassianvale.
