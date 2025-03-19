# 关于项目

## 什么是灵曜按键？

灵曜按键是一个基于.NET8.0和WPF开发的现代化按键工具，它提供了丰富的功能和优雅的用户界面。该工具采用MVVM架构设计，支持全局热键、侧键滚轮触发、按键独立间隔、坐标移动等功能。

## 系统要求

- 推荐Windows 10/11（Windows 7 未经过测试，可能导致无法预知的问题）
- 以管理员身份运行程序（用于驱动安装）
- .NET 8.0 Runtime 环境 （如果运行程序提示`You must install .NET Desktop Runtime to run this application.`，请下载：[.NET 8.0 Runtime](https://download.visualstudio.microsoft.com/download/pr/64760cc4-228f-48e4-b57d-55f882dedc69/b181f927cb937ef06fbb6eb41e81fbd0/windowsdesktop-runtime-8.0.14-win-x64.exe)）

## 主要特性

### 基础功能
- 支持全局热键注册
- 支持顺序/按压热键模式切换
- 支持侧键和滚轮触发
- 支持鼠标移动至对应坐标，可进行坐标录入和编辑
- 每个按键及坐标设有独立间隔
- 支持窗口句柄嗅探，开启后仅对应窗口可触发热键
- 语音提醒开关，音量大小设置，自定义按键开启/按键关闭语音
- 正常/降低卡位模式切换开关
- 按键和坐标支持拖拽排序
- 浮窗置顶显示按键启动/关闭/禁用状态
- 支持输入法切换开关，开启后触发热键自动切换ENG输出
- 支持配置导出/导入、联网更新、调试模式等功能

### 驱动特性
- 基于DeviceIoControl内核级驱动实现
- 支持离线运行
- 完善的反Hook和内存保护机制
- 支持32位/64位系统架构
- 支持USB/PS2键鼠设备
- 兼容Win10/Win11系统
- 支持驱动热插拔，程序退出无痕卸载

## 技术栈

- 开发语言：C#
- 框架：.NET 8.0
- UI框架：WPF
- 架构模式：MVVM
- 驱动开发：Windows Driver Kit (WDK)
- 构建工具：Visual Studio 2022
