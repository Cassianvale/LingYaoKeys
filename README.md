<!-- markdownlint-restore -->
<div align="center">

# LingYaoKeys - 灵曜按键  

✨**基于.NET8.0+WPF开发的灵动、优雅的开源按键工具**✨  

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

简体中文 / [English](./README_EN.md)

❤  如果喜欢本项目可右上角送作者一个`Star`🌟 ❤
</div>
</br>
<!-- markdownlint-restore -->

## ✨ 主要功能

### 🎮 基础功能
- [x] 支持全局热键，**支持侧键滚轮触发**  
- [x] 支持窗口句柄嗅探  
- [x] 支持顺序/按压模式触发按键  
- [x] 支持开启/停止语音提醒  
- [x] 支持正常/游戏模式切换  
- [x] 支持自定义开启/停止音频  
- [x] 支持按键列表拖拽排序  
- [x] 支持浮窗置顶显示按键启动状态  

### 🚀 驱动特性
- [x] 基于DeviceIoControl内核级驱动实现  
- [x] 支持离线运行  
- [x] 完善的反Hook和内存保护机制  
- [x] 支持32位/64位系统架构  
- [x] 支持USB/PS2键鼠设备  
- [x] 兼容Win7/Win10/Win11系统  
- [x] 支持驱动热插拔，程序退出无痕卸载  
  
## 🌏 直接下载  

您可以从以下位置下载最新版本：  

- [最新版本下载](https://github.com/Cassianvale/LingYaoKeys/releases/latest)  
- [查看所有版本](https://github.com/Cassianvale/LingYaoKeys/releases)  

> 注意：请始终从 GitHub Releases 页面下载最新版本，以确保获得最新的功能和安全更新。  

## 📖 使用说明

> [!IMPORTANT]
> 经过长时间的测试并且结合其他按键的测试结果，按键速度每秒高于两三百的话会导致按键响应延迟或者造成卡位移(看自己cpu内存性能)，原因可能是windows的消息机制导致的，所以玩游戏的话按键速度，不需要太快。根据测试结果我取了一个合适的区间，所以就加入了针对游戏进行优化的游戏模式  
> DEFAULT_KEY_PRESS_INTERVAL：按键按下到松开的固定时间  
> MIN_KEY_INTERVAL: 最小可设置的按键间隔  

- _**游戏模式打开(默认)**_：测试后平均按键速度为120+，适用于游戏内  
- _**游戏模式关闭**_：解除按键速度限制平均速度320+，适用于一般场景  
- _**自定义音频**_：打开 `C:\Users\用户\.lykeys\sound`，保持文件名替换 `start.mp3`/`stop.mp3` 即可  


## 📃常见问题
因为本项目使用的微软最新的`.Net Core 8.0`，有部分用户可能需要下载内核  
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/download_core.png" heigh="400px"/> 


## 🖼️ 项目展示  

<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/home.png" width="500px"/>  
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/about.png" width="500px"/>  
<img src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/keys.png" width="700px"/>  

## 🎙 关于与建议  
- 该项目是本人利用工作之余首次尝试使用`C#`和`WPF`以及`Cursor AI`技术栈进行开发的实践项目  
- 目前项目处于开发初期，新功能正在持续添加中，如果你对软件有任何功能与建议，欢迎在 `Issues` 中提出，如果对项目感兴趣，欢迎参与讨论  
- 如果你也喜欢本软件的设计思想，欢迎提交 `pr`，非常感谢你对本项目的支持！  

## ⚙️ 开发相关

### 运行

- `dotnet run`  

### 发布打包

- `dotnet publish -c Release`  

## 🔧 驱动使用说明

### 驱动文件说明
- `lykeysdll.dll`: 核心驱动动态链接库(*必须)  
- `lykeys.sys`: 内核级驱动文件(*必须)  
- `lykeys.cat`: 驱动签名文件  
- `Resource\lykeysdll\README.md`: 驱动接口使用说明  
- `Resource\lykeysdll\csharp_example`: C#示例代码  
- `Resource\lykeysdll\python_example`: Python示例代码  

### ⚠️ 注意事项
1. **驱动签名**  
   - 驱动已通过正版签名认证  
   - 请勿修改驱动文件，否则会导致签名失效  

2. **系统要求**  
   - 支持 Windows 7/10/11 (x86/x64)  
   - 需要管理员权限运行  

3. **使用限制**  
   - 仅供个人学习研究使用  
   - 严禁用于商业用途！！！  
   - 禁止修改或反编译驱动文件  

## ☕️ Buy me a coffee

 ♥ 驱动签名为自费购买，如果您喜欢这个项目可以支持一下作者，这将是对我极大的鼓励 ♥ 
  
<img  src="https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/wechat_qr.png" width="200px"/>  

## 📢 免责声明  
- **仅供个人学习研究使用，禁止用于商业及非法用途**  
- **开发者拥有本项目的最终解释权**  
- **严禁用于任何违反`中华人民共和国(含台湾省)`或使用者所在地区法律法规的用途**  
- **请使用者在使用本项目时遵守相关法律法规，不要将本项目用于任何商业及非法用途。如有违反，一切后果由使用者自负。 同时，使用者应该自行承担因使用本项目而带来的风险和责任。本项目开发者不对本项目所提供的服务和内容做出任何保证**  
- **若您遇到商家使用本软件进行收费，产生的任何问题及后果与本项目无关**  

## 📜 开源许可
[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)  

LingYaoKeys 使用 [GNU General Public License v3.0](LICENSE) 开源许可证  

Copyright © 2025 by Cassianvale.  
