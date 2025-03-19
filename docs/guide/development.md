# 开发相关

## 项目结构

```
LingYaoKeys/
├── Views/                          # View Layer
│   └── Controls/                   # Keyboard and Mouse Layout Logic
├── ViewModels/                     # ViewModel Layer
│   ├── ViewModelBase.cs           # Base ViewModel Class
│   ├── MainViewModel.cs           # Main Window ViewModel
│   ├── KeyMappingViewModel.cs     # Key Mapping ViewModel
│   ├── FloatingStatusViewModel.cs # Floating Status ViewModel
│   ├── QRCodeViewModel.cs         # QR Code ViewModel
│   ├── FeedbackViewModel.cs       # Feedback ViewModel
│   └── AboutViewModel.cs          # About ViewModel
├── Services/                       # Service Layer
│   ├── Core/                      # Core Services
│   │   ├── HotkeyService.cs        # Hotkey Service Implementation Class
│   │   ├── LyKeysService.cs      # Key Service Main Class
│   │   ├── LyKeys.cs             # Key Core Implementation Class
│   │   ├── LyKeysCode.cs         # Key Code Definition Class
│   │   ├── KeyMappingService.cs   # Key Mapping Service
│   │   └── InputMethodService.cs  # Input Method Service
│   ├── Models/                    # Service Models
│   │   ├── KeyItem.cs                # Key Item Model Class
│   │   ├── HoldKeyMode.cs           # Hold Key Mode Model Class
│   │   └── KeyModeBase.cs           # Key Mode Base Class
│   ├── Utils/                    # Utility Services
│   ├── Events/                   # Event Services
│   ├── Cache/                    # Cache Services
│   ├── Audio/                    # Audio Services
│   └── Config/                   # Configuration Services
├── Converters/                    # Value Converters
├── Behaviors/                     # Behavior Definitions
├── Styles/                        # Style Definitions
├── Resource/                      # Resource Files
└── App.xaml                       # Application Definition
```

## 开发环境

### 必需工具
- Visual Studio 2022
- .NET 8.0 SDK
- Windows Driver Kit (WDK)
- Git

### 推荐工具
- Visual Studio Code
- Git Extensions
- Postman (API测试)
- Fiddler (网络调试)

## 开发规范

### 代码规范
- 遵循C#代码规范
- 使用MVVM模式
- 使用XAML定义UI
- 使用WPF控件
- 复杂逻辑分离到服务类
- 使用依赖注入

### 命名规范
- 类名：PascalCase
- 方法名：PascalCase
- 变量名：camelCase
- 常量名：UPPER_CASE
- 接口名：IPascalCase
- 文件名：PascalCase.cs

### 注释规范
- 类注释：说明类的用途
- 方法注释：说明参数和返回值
- 复杂逻辑注释：说明实现思路
- 关键代码注释：说明重要逻辑

## 构建和运行

### 开发环境
```bash
# 克隆项目
git clone https://github.com/Cassianvale/LingYaoKeys.git

# 打开解决方案
start LingYaoKeys.sln

# 运行项目
dotnet run
```

### 发布打包
```bash
# 发布Release版本
# 使用Visual Studio的发布和打包功能
```

## 测试

### 单元测试
- 使用xUnit框架
- 测试ViewModel逻辑
- 测试Service功能
- 测试工具类方法

### 集成测试
- 测试UI交互
- 测试驱动功能
- 测试性能表现
- 测试异常处理

## 调试

### 驱动调试
1. 配置系统生成完整转储
2. 安装调试工具
3. 使用WinDbg分析
4. 查看崩溃日志

### 应用调试
1. 使用Visual Studio调试器
2. 查看日志输出
3. 分析性能数据
4. 检查内存使用

## 贡献指南

### 提交PR
1. Fork项目
2. 创建特性分支
3. 提交更改
4. 发起Pull Request

### 代码审查
1. 遵循代码规范
2. 添加必要注释
3. 编写单元测试
4. 更新文档

## 发布流程

### 版本发布
1. 更新版本号
2. 更新更新日志
3. 构建发布包
4. 创建GitHub Release

### 文档更新
1. 更新API文档
2. 更新使用说明
3. 更新开发文档
4. 更新常见问题 