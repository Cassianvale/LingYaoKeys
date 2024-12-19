# jx3wpftools
✨ 基于 WPF 开发的 JX3 按键工具✨  

## 特别鸣谢
 该项目是本人利用工作之余首次尝试使用C#和WPF技术栈以及Cursor AI进行开发的实践项目。  
 在UI/UX与交互逻辑设计上，极简按键提供了优秀的范例，让我在学习过程中获益良多，特此致谢极简按键！  
 项目中我也融入了一些个人的理解和创新设计，希望能为同样热爱技术的朋友提供参考。  
 
## 运行
- `dotnet run`  

## 打包  
- `sh publish.sh`  

## 下载

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/Cassianvale/jx3wpftools)](https://github.com/Cassianvale/jx3wpftools/releases/latest)

您可以从以下位置下载最新版本：

- [最新版本下载](https://github.com/Cassianvale/jx3wpftools/releases/latest)
- [查看所有版本](https://github.com/Cassianvale/jx3wpftools/releases)

> 注意：请始终从 GitHub Releases 页面下载最新版本，以确保获得最新的功能和安全更新。

## 项目展示  
![image](https://github.com/Cassianvale/jx3wpftools/raw/main/Resource/img/home.png)  

## 项目结构
```
jx3wpftools/  
├── Commands/   # MVVM 命令  
│   └── RelayCommand.cs         # MVVM 命令类实现  
│  
├── Converters/ # 值转换器  
│   ├── BoolToVisibilityConverter.cs # 布尔值转可见性  
│   ├── BoolToColorConverter.cs     # 布尔值转颜色  
│   └── IntToStringConverter.cs     # 整数转字符串  
│  
├── Models/ # 数据模型  
│   ├── AppConfig.cs              # 应用配置模型  
│   └── KeyItem.cs               # 键项模型  
│
├── Services/ # 服务层  
│   ├── CDD.cs                 # DD 驱动类核心  
│   ├── DDDriverService.cs     # DD 驱动服务处理底层按键操作  
│   ├── HotkeyService.cs       # 热键服务管理全局热键  
│   ├── AppConfigService.cs     # 应用配置服务类  
│   ├── ConfigService.cs        # 配置服务类  
│   ├── KeyCodeMapping.cs      # 按键映射类  
│   ├── LogManager.cs          # 日志管理类  
│   ├── DDKeyCodeExtensions.cs # DD键码扩展类  
│   └── DDKeyCode.cs           # DD按键码定义类  
│  
├── Styles/ # 样式定义  
│   ├── ControlStyles.xaml # 控件样式  
│   ├── ButtonStyles.xaml # 按钮样式  
│   └── NavigationStyles.xaml # 导航样式  
│  
├── ViewModels/ # 视图模型  
│   ├── KeyMappingViewModel.cs # 按键映射视图模型  
│   ├── MainViewModel.cs # 主窗口视图模型  
│   ├── ViewModelBase.cs # 视图模型基类  
│   └── SyncSettingsViewModel.cs # 同步设置视图模型  
│  
├── Views/ # 视图层  
│   ├── KeyMappingView.xaml(.cs) # 按键映射视图  
│   ├── SyncSettingsView.xaml(.cs) # 同步设置视图  
│   └── SyncSettingsPage.xaml # 同步设置页面  
│  
├── dd/ # DD驱动文件目录  
│   ├── ddx32.dll # 32位DD驱动文件  
│   └── ddx64.dll # 64位DD驱动文件  
│  
├── App.xaml(.cs) # 应用程序定义  
├── MainWindow.xaml(.cs) # 主窗口定义  
├── WpfApp.csproj # 项目配置文件  
├── app.manifest # 应用程序清单  
└── WpfApp.sln # 解决方案文件  
```