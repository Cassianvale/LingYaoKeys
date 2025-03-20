<!-- markdownlint-restore -->
<div align="center">

# LingYaoKeys

<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/app.png" width="120px" alt="LingYaoKeys Logo"/>

✨ **An Elegant and Flexible Open-Source Keyboard Tool Based on .NET8.0+WPF** ✨

📚 [Documentation](https://cassianvale.github.io/LingYaoKeys/)

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

## 📌 Table of Contents

- [✨ Main Features](#-main-features)
- [🌏 Quick Download](#-quick-download)
- [📖 Usage Instructions](#-usage-instructions)
- [🖼️ Project Showcase](#️-project-showcase)
- [📃 Common Issues](#-common-issues)
- [🎙 About & Suggestions](#-about--suggestions)
- [⚙️ Development Related](#️-development-related)
- [🔧 Driver Instructions](#-driver-instructions)
- [☕️ Support Project](#️-support-project)
- [📢 Disclaimer](#-disclaimer)
- [📜 Open Source License](#-open-source-license)

## ✨ Main Features

### 🎮 Basic Features

- [x] **Hotkey System**
  - Global hotkey registration support
  - Sequential/Press mode switching
  - Side button and scroll wheel trigger support

- [x] **Mouse Features**
  - Mouse movement to coordinates
  - Coordinate input and editing
  - Independent intervals for each key and coordinate

- [x] **Utility Tools**
  - Window handle detection
  - Voice notification toggle and custom audio
  - Normal/Reduce Sticking mode switching
  - Drag-and-drop key and coordinate sorting
  - Floating window status display
  - Input method switching support

- [x] **Configuration Management**
  - Config export/import
  - Online updates
  - Debug mode support

### 🚀 Driver Features

- [x] **Core Technology**
  - DeviceIoControl kernel-level driver implementation
  - Offline operation support

- [x] **System Compatibility**
  - 32-bit/64-bit system architecture support
  - USB/PS2 keyboard and mouse support
  - Win10/Win11 system compatibility

- [x] **Reliability**
  - Hot-plug driver support
  - Clean uninstallation upon exit

## 🌏 Quick Download

<div align="center">

📥 **[Latest Version Download](https://github.com/Cassianvale/LingYaoKeys/releases/latest)** | 🗂️ **[All Versions](https://github.com/Cassianvale/LingYaoKeys/releases)**

</div>

> **Note**: Always download the latest version from the GitHub Releases page to ensure you have the latest features and security updates.

## 📖 Usage Instructions

### Mode Selection

> [!IMPORTANT]
> Based on testing results, key speeds in games don't need to be too fast, as you need to consider the game client's response. Key speeds above 200-300 per second may cause key response delay or sticking movement.  
> This feature is only for specific gaming scenarios. If you experience sticking movement or skill activation issues, please enable this feature.  
> Setting the press-release interval to 0 can reach thousands of presses per second, but that's unnecessary 🫥  

- **Reduce Sticking Feature (enabled by default)** 
  - Average key speed of 120+ times per second
  - Suitable for specific gaming scenarios, reduces sticking movement phenomena

- **Normal Mode (Reduce Sticking disabled)**
  - Removes key speed limits, average speed of 320+ times per second
  - Suitable for normal application scenarios

## 🖼️ Project Showcase

<div align="center">
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/screenshots.gif" width="700px" alt="LingYaoKeys Interface"/>
</div>

## 📃 Common Issues

<details>
<summary><b>Runtime Environment Issues</b></summary>
<br>
Since this project uses Microsoft's latest <code>.Net Core 8.0</code>, some users may need to download the runtime:
<br><br>
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/download_core.png" height="250px" alt="Download .NET Core Runtime"/>
</details>

<details>
<summary><b>Key Speed Issues</b></summary>
<br>
If you experience suboptimal key speed performance, try disabling the "Reduce Sticking" feature. However, please note that this may cause sticking movement in some games. Choose the appropriate mode based on your actual usage scenario.
</details>

## 🎙 About & Suggestions

- This project is my first attempt at developing with `C#`, `WPF`, and `Cursor AI` technology stack during my spare time
- The project is in its early development stage with new features being continuously added
- If you have any suggestions for the software, feel free to raise them in [Issues](https://github.com/Cassianvale/LingYaoKeys/issues)
- If you're interested in the project, welcome to join the discussion or submit a [Pull Request](https://github.com/Cassianvale/LingYaoKeys/pulls)

## ⚙️ Development Related

### Environment Setup

- Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Recommended to use [Visual Studio 2022](https://visualstudio.microsoft.com/) or higher

### Running the Project

```bash
dotnet run
```

## 🔧 Driver Usage Instructions

### Driver File Description

| File | Description |
|------|-------------|
| `Resource\lykeysdll\lykeysdll.dll` | Core driver DLL (**Required**) |
| `Resource\lykeysdll\lykeys.sys` | Kernel-level driver file (**Required**) |
| `Resource\lykeysdll\lykeys.cat` | Driver signature file |
| `Resource\lykeysdll\README.md` | [Driver Interface & Debug Guide](https://github.com/Cassianvale/LingYaoKeys/blob/main/Resource/lykeysdll/README.md) |
| `Resource\lykeysdll\csharp_example\*` | C# Example Code |
| `Resource\lykeysdll\python_example\*` | Python Example Code |

### ⚠️ Important Notes

1. **Driver Signature**
    - Driver has genuine digital signature
    - Do NOT modify driver files to avoid signature invalidation

2. **System Requirements**
    - Supports Windows 10/11 (x86/x64)
    - Windows 7 has not been tested and may cause unpredictable issues
    - Requires Administrator privileges

3. **Usage Restrictions**
    - **Regarding various game anti-cheat issues, please do not ask about whether it can pass detection tests. This kind of technical support is not provided!**
    - For personal study and research only
    - Reverse engineering or modification prohibited

## ☕️ Support Project

<div align="center">

♥ The driver signature was self-funded. If you like this project, your support would be a great encouragement to me ♥

<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/wechat_qr.png" width="200px" alt="WeChat QR Code"/>
</div>

## 📢 Disclaimer

- **For personal study and research use only, commercial and illegal use is prohibited**
- **The developer reserves the final right of interpretation for this project**
- **Strictly prohibited for any use that violates the laws and regulations of the `People's Republic of China (including Taiwan Province)` or the user's region**
- **Users must comply with relevant laws and regulations when using this project and must not use it for any commercial or illegal purposes. In case of violation, all consequences shall be borne by the user. Meanwhile, users should bear the risks and responsibilities arising from using this project. The project developer makes no warranties regarding the services and content provided by this project**
- **If you encounter merchants charging for this software, any resulting issues and consequences are not related to this project**

## 📜 Open Source License

<div align="center">

[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

LingYaoKeys is licensed under [GNU General Public License v3.0](LICENSE)

Copyright © 2025 by Cassianvale.
</div>
