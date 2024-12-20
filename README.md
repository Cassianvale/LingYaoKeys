# LingYaoKeys (灵曜按键)
✨ 基于WPF+DD驱动开发的按键工具✨  

## 特别鸣谢
 该项目是本人利用工作之余首次尝试使用C#和WPF技术栈以及Cursor AI进行开发的实践项目。  
 在UI/UX与交互逻辑设计上，极简按键提供了优秀的范例，让我在学习过程中获益良多，特此致谢极简按键！  
 项目中我也融入了一些个人的理解和创新设计，希望能为同样热爱技术的朋友提供参考。  
 
## 直接下载

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/Cassianvale/LingYaoKeys)](https://github.com/Cassianvale/LingYaoKeys/releases/latest)

您可以从以下位置下载最新版本：

- [最新版本下载](https://github.com/Cassianvale/LingYaoKeys/releases/latest)
- [查看所有版本](https://github.com/Cassianvale/LingYaoKeys/releases)

> 注意：请始终从 GitHub Releases 页面下载最新版本，以确保获得最新的功能和安全更新。

## 主要功能  
- [x] 支持全局热键，**侧键触发**  
- [x] 支持按键列表拖拽排序  
- [x] 支持开启/停止语音提醒  
- [x] 支持顺序模式触发按键
- [ ] 支持按压模式触发按键

## 游戏模式

DEFAULT_KEY_PRESS_INTERVAL：按键按下->松开的固定速度
MIN_KEY_INTERVAL: 最小可设置的按键间隔

经过长时间的测试并且结合其他按键的测试结果，按键速度每秒高于两三百的话会导致按键响应延迟或者造成卡位移(看自己cpu内存性能)，所以游戏的话按键速度不需要太快，根据测试结果我取了一个合适的区间，原因可能是windows的消息机制导致的，所以就增加了默认开启的游戏模式

开启游戏模式后：DEFAULT_KEY_PRESS_INTERVAL=5, MIN_KEY_INTERVAL=1，测试后平均按键速度为113
关闭游戏模式后：DEFAULT_KEY_PRESS_INTERVAL=0, MIN_KEY_INTERVAL=1，解除按键速度限制

## 项目展示  
![image](https://github.com/Cassianvale/LingYaoKeys/raw/main/Resource/img/home.png)  

## 开发相关

### 运行

- `dotnet run`  

### 发布打包

- `.\publish.bat`  

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

## 许可证
LingYaoKeys 使用 [GNU General Public License v3.0](LICENSE) 开源许可证。

Copyright © 2024 by Cassianvale.