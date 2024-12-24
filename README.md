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
    <img alt="stars" src="https://img.shields.io/github/stars/Cassianvale/LingYaoKeys?style=social">
    <img alt="downloads" src="https://img.shields.io/github/downloads/Cassianvale/LingYaoKeys/total?style=social">
</div>
<br>


❤  如果喜欢本项目可右上角送作者一个`Star`🌟，反馈 QQ 群 `861603314` ❤

</div>
</br>
<!-- markdownlint-restore -->

## 主要功能  
- [x] 支持全局热键，**支持侧键触发**  
- [x] 支持顺序/按压模式触发按键  
- [x] 支持开启/停止语音提醒  
- [x] 支持浮窗置顶显示按键启动状态
- [x] 支持正常/游戏模式切换  
- [x] 支持自定义开启/停止音频  
- [x] 支持按键列表拖拽排序  

## 直接下载

您可以从以下位置下载最新版本：  

- [最新版本下载](https://github.com/Cassianvale/LingYaoKeys/releases/latest)  
- [查看所有版本](https://github.com/Cassianvale/LingYaoKeys/releases)  

> 注意：请始终从 GitHub Releases 页面下载最新版本，以确保获得最新的功能和安全更新。  

## 使用说明

> [!IMPORTANT]
> 经过长时间的测试并且结合其他按键的测试结果，按键速度每秒高于两三百的话会导致按键响应延迟或者造成卡位移(看自己cpu内存性能)，原因可能是windows的消息机制导致的，所以玩游戏的话按键速度，不需要太快。根据测试结果我取了一个合适的区间，所以就加入了针对游戏进行优化的游戏模式  
> DEFAULT_KEY_PRESS_INTERVAL：按键按下到松开的固定时间  
> MIN_KEY_INTERVAL: 最小可设置的按键间隔  

- _**游戏模式打开(默认)**_：测试后平均按键速度为120+，适用于游戏内  
- _**游戏模式关闭**_：解除按键速度限制平均速度320+，适用于一般场景  
- _**自定义音频**_：打开 `C:\Users\用户\.lingyao\sound`，保持文件名替换 `start.mp3`/`stop.mp3` 即可  
- _**Debug模式**_：打开 `C:\Users\用户\.lingyao\AppConfig.json` 将 `"Logging": {"Enabled": false,}` 中的 `false` 设为 `true`，后续的操作记录在 `.lingyao\logs` 目录下生成日志文件  

## 项目展示  
![image](https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/home.png)  

## 开发相关

### 运行

- `dotnet run`  

### 发布打包

- `dotnet publish -c Release`

### 项目结构展示
<details>
<summary>点击展开查看完整项目结构</summary>

```
LingYaoKeys/  
│
├── Commands/         # MVVM 命令  
│   └── RelayCommand.cs         # MVVM 命令类实现  
│
├── Behaviors/        # 行为定义
│   ├── ListBoxDragDropBehavior.cs # 列表框拖放行为
│   ├── DragDropProperties.cs      # 拖放属性定义
│   └── DragAdorner.cs            # 拖放装饰器
│
├── Converters/       # 值转换器  
│   ├── BoolToVisibilityConverter.cs    # 布尔值转可见性  
│   ├── BoolToColorConverter.cs         # 布尔值转颜色  
│   ├── IntToStringConverter.cs         # 整数转字符串  
│   └── ViewModelToHotkeyStatusConverter.cs # 视图模型到热键状态转换器
│  
├── Models/           # 数据模型  
│   ├── AppConfig.cs              # 应用配置模型  
│   └── KeyItem.cs                # 键项模型  
│
├── Resource/         # 资源文件
│   ├── img/         # 图片资源
│   └── sound/       # 音频资源
│
├── Services/         # 服务层  
│   ├── Collections/           # 集合类
│   │   └── ConcurrentPriorityQueue.cs # 并发优先级队列
│   │
│   ├── KeyModes/             # 按键模式
│   │   ├── KeyModeBase.cs         # 按键模式基类
│   │   ├── SequenceKeyMode.cs     # 顺序按键模式
│   │   ├── HoldKeyMode.cs         # 按压按键模式
│   │   └── KeyModeMetrics.cs      # 按键模式度量
│   │
│   ├── AudioService.cs          # 音频服务
│   ├── TaskManager.cs           # 任务管理器
│   ├── CDD.cs                   # DD 驱动类核心  
│   ├── DDDriverService.cs       # DD 驱动服务处理底层按键操作  
│   ├── HotkeyService.cs         # 热键服务管理全局热键  
│   ├── AppConfigService.cs      # 应用配置服务类  
│   ├── ConfigService.cs         # 配置服务类  
│   ├── KeyCodeMapping.cs        # 按键映射类  
│   ├── LogManager.cs            # 日志管理类  
│   ├── DDKeyCodeExtensions.cs   # DD键码扩展类  
│   └── DDKeyCode.cs             # DD按键码定义类  
│  
├── Styles/           # 样式定义  
│   ├── ControlStyles.xaml      # 控件样式  
│   ├── ButtonStyles.xaml       # 按钮样式  
│   └── NavigationStyles.xaml   # 导航样式  
│  
├── ViewModels/       # 视图模型  
│   ├── KeyMappingViewModel.cs    # 按键映射视图模型  
│   ├── MainViewModel.cs          # 主窗口视图模型  
│   ├── ViewModelBase.cs          # 视图模型基类  
│   └── SyncSettingsViewModel.cs  # 同步设置视图模型  
│  
├── Views/            # 视图层  
│   ├── KeyMappingView.xaml(.cs)    # 按键映射视图  
│   ├── SyncSettingsView.xaml(.cs)  # 同步设置视图  
│   └── SyncSettingsPage.xaml       # 同步设置页面  
│  
├── dd/              # DD驱动文件目录  
│   ├── ddx32.dll    # 32位DD驱动文件  
│   └── ddx64.dll    # 64位DD驱动文件  
│  
├── logs/            # 日志文件目录
├── publish/         # 发布输出目录
│
├── App.xaml(.cs)    # 应用程序定义  
├── MainWindow.xaml(.cs) # 主窗口定义  
├── AppConfig.json   # 应用程序配置文件
├── AssemblyInfo.cs  # 程序集信息
├── publish.bat      # 发布打包脚本
├── WpfApp.csproj    # 项目配置文件  
├── app.manifest     # 应用程序清单  
└── WpfApp.sln       # 解决方案文件  
```
</details>

## 鸣谢
 - 该项目是本人利用工作之余首次尝试使用`C#`和`WPF`以及`Cursor AI`技术栈进行开发的实践项目  
 - 在UI/UX与交互逻辑设计上，极简按键提供了优秀的范例，让我在设计过程中获益良多，特此致谢极简按键  
 - 项目中我也融入了一些个人的理解和创新设计，希望能为同样热爱技术的朋友提供参考  

## 许可证
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)  

LingYaoKeys 使用 [GNU General Public License v3.0](LICENSE) 开源许可证  

Copyright © 2024 by Cassianvale.  
