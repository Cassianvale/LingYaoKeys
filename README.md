<!-- markdownlint-restore -->
<div align="center">

# LingYaoKeys - 灵曜按键

<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/app.png" width="120px" alt="LingYaoKeys Logo"/>

✨ **基于.NET8.0+WPF开发的灵动、优雅的开源按键工具** ✨

📚 [官方文档](https://cassianvale.github.io/LingYaoKeys/)

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

简体中文 | [English](./README_EN.md)

❤ 如果喜欢本项目可以右上角送作者一个 `Star` 🌟 ❤
</div>

<!-- markdownlint-restore -->

## 📌 目录

- [✨ 主要功能](#-主要功能)
- [🌏 快速下载](#-快速下载)
- [📖 使用说明](#-使用说明)
- [🖼️ 项目展示](#️-项目展示)
- [📃 常见问题](#-常见问题)
- [🎙 关于与建议](#-关于与建议)
- [⚙️ 开发相关](#️-开发相关)
- [🔧 驱动使用说明](#-驱动使用说明)
- [☕️ 支持项目](#️-支持项目)
- [📢 免责声明](#-免责声明)
- [📜 开源许可](#-开源许可)

## ✨ 主要功能

### 🎮 基础功能

- [x] **热键系统**
  - 支持全局热键注册
  - 支持顺序/按压热键模式切换
  - 支持侧键和滚轮触发

- [x] **鼠标功能**
  - 支持鼠标移动至对应坐标
  - 可进行坐标录入和编辑
  - 每个按键及坐标设有独立间隔

- [x] **实用工具**
  - 窗口句柄嗅探（仅对应窗口可触发热键）
  - 语音提醒开关与自定义音频
  - 正常/降低卡位模式切换
  - 按键和坐标支持拖拽排序
  - 浮窗置顶显示按键状态
  - 输入法切换支持

- [x] **配置管理**
  - 配置导出/导入
  - 联网更新
  - 调试模式支持

### 🚀 驱动特性

- [x] **核心技术**
  - 基于DeviceIoControl内核级驱动实现
  - 完善的反Hook和内存保护机制
  - 支持离线运行

- [x] **系统兼容**
  - 支持32位/64位系统架构
  - 支持USB/PS2键鼠设备
  - 兼容Win10/Win11系统

- [x] **可靠性**
  - 支持驱动热插拔
  - 程序退出无痕卸载

## 🌏 快速下载

<div align="center">

📥 **[最新版本下载](https://github.com/Cassianvale/LingYaoKeys/releases/latest)** | 🗂️ **[所有版本](https://github.com/Cassianvale/LingYaoKeys/releases)**

</div>

> **注意**：请始终从 GitHub Releases 页面下载最新版本，以确保获得最新的功能和安全更新。

## 📖 使用说明

> [!IMPORTANT]
> 经过长时间的测试并且结合其他按键的测试结果，按键速度每秒高于两三百会导致按键响应延迟或者造成卡位移(系统IO输出响应时间+游戏客户端响应时间，取决于CPU性能)。这可能是由Windows消息机制导致的，因此游戏中的按键速度不需要太快。根据测试结果，我们设定了合适的区间并加入了针对游戏优化的降低卡位功能。
> 
> - `DEFAULT_KEY_PRESS_INTERVAL`：按键按下到松开的固定时间
> - `MIN_KEY_INTERVAL`: 最小可设置的按键间隔

### 模式选择

- **降低卡位功能(默认开启)** 
  - 平均按键速度为120+次/秒
  - 适用于特定游戏场景，减少卡位移现象

- **常规模式(降低卡位关闭)**
  - 解除按键速度限制，平均速度320+次/秒
  - 适用于普通应用场景

## 🖼️ 项目展示

<div align="center">
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/screenshots.gif" width="700px" alt="LingYaoKeys 界面展示"/>
</div>

## 📃 常见问题

<details>
<summary><b>运行环境问题</b></summary>
<br>
由于本项目使用了微软最新的 <code>.Net Core 8.0</code>，部分用户可能需要下载运行环境：
<br><br>
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/download_core.png" height="250px" alt="下载.NET Core运行环境"/>
</details>

<details>
<summary><b>按键速度问题</b></summary>
<br>
如果你遇到按键速度不理想的情况，可以尝试关闭"降低卡位"功能。但请注意，这可能在某些游戏中导致卡位移现象。根据你的实际使用场景选择合适的模式。
</details>

## 🎙 关于与建议

- 该项目是本人利用工作之余首次尝试使用`C#`和`WPF`以及`Cursor AI`技术栈进行开发的实践项目
- 目前项目处于开发初期，新功能正在持续添加中
- 如果你对软件有任何功能与建议，欢迎在 [Issues](https://github.com/Cassianvale/LingYaoKeys/issues) 中提出
- 如果对项目感兴趣，欢迎参与讨论或提交 [Pull Request](https://github.com/Cassianvale/LingYaoKeys/pulls)

## ⚙️ 开发相关

### 环境准备

- 安装 [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 推荐使用 [Visual Studio 2022](https://visualstudio.microsoft.com/) 或更高版本

### 运行项目

```bash
dotnet run
```

## 🔧 驱动使用说明

### 驱动文件说明

| 文件 | 说明 |
|------|------|
| `Resource\lykeysdll\lykeysdll.dll` | 核心驱动动态链接库 (**必须**) |
| `Resource\lykeysdll\lykeys.sys` | 内核级驱动文件 (**必须**) |
| `Resource\lykeysdll\lykeys.cat` | 驱动签名文件 |
| `Resource\lykeysdll\README.md` | [驱动接口&调试说明](https://github.com/Cassianvale/LingYaoKeys/blob/main/Resource/lykeysdll/README.md) |
| `Resource\lykeysdll\csharp_example\*` | C#示例代码 |
| `Resource\lykeysdll\python_example\*` | Python示例代码 |

### ⚠️ 注意事项

1. **驱动签名**
    - 驱动已通过正版签名认证
    - 请勿修改驱动文件，否则会导致签名失效

2. **系统要求**
    - 支持 Windows 10/11 (x86/x64)
    - Windows 7 未经过测试，可能导致无法预知的问题
    - 需要管理员权限运行

3. **使用限制**
    - **关于各种游戏的反作弊问题，类似能不能过测之类不要来问我，不提供这种技术支持！**
    - 仅供个人学习研究使用
    - 禁止修改或反编译驱动文件

## ☕️ 支持项目

<div align="center">

♥ 驱动签名为自费购买，如果您喜欢这个项目可以支持一下作者，这将是对我极大的鼓励 ♥

<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/wechat_qr.png" width="200px" alt="微信赞赏码"/>  
</div>

## 📢 免责声明

- **仅供个人学习研究使用，禁止用于商业及非法用途**
- **开发者拥有本项目的最终解释权**
- **严禁用于任何违反`中华人民共和国(含台湾省)`或使用者所在地区法律法规的用途**
- **请使用者在使用本项目时遵守相关法律法规，不要将本项目用于任何商业及非法用途。如有违反，一切后果由使用者自负。同时，使用者应该自行承担因使用本项目而带来的风险和责任。本项目开发者不对本项目所提供的服务和内容做出任何保证**
- **若您遇到商家使用本软件进行收费，产生的任何问题及后果与本项目无关**

## 📜 开源许可

<div align="center">

[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

LingYaoKeys 使用 [GNU General Public License v3.0](LICENSE) 开源许可证

Copyright © 2025 by Cassianvale.  
</div>
