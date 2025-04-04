# 常见问题

## 安装相关

### 系统要求
**Q: 灵曜按键支持哪些Windows版本？**  

A: 支持Windows 10/11，推荐使用Windows 10/11，Windows 7 虽然驱动代码做了适配但是未经过测试，可能导致无法预知的问题

**Q: 需要安装.NET运行时吗？**  

A: 是的，需要安装.NET 8.0 Desktop Runtime

**Q: 为什么需要管理员权限？**  

A: 因为需要安装和运行内核级驱动，所以需要管理员权限

### 安装问题
**Q: 安装时提示"驱动签名无效"怎么办？**  

A: 请确保从官方渠道下载软件，不要修改驱动文件。如果是在测试环境，可以：
- 1. 关闭安全启动
- 2. 启用测试模式
- 3. 重启系统

**Q: 安装后程序无法启动怎么办？**  

A: 请按以下步骤检查：
- 1. 确认.NET 8.0 Desktop Runtime已安装
- 2. 检查杀毒软件是否拦截   
- 3. 以管理员身份运行
- 4. 查看错误日志

**Q: 驱动初始化失败是怎么回事？**

A: 请确保运行前电脑已经连接了真实的键盘和鼠标设备（缺一不可），查找不到真实设备句柄时驱动就会初始化失败

## 使用相关

### 基本功能
**Q: 如何设置全局热键？**  

A: 在主界面点击"添加热键"，选择要触发的按键，设置触发条件和间隔时间。

**Q: 如何配置侧键触发？**  

A: 在"侧键设置"中选择要使用的侧键，配置触发行为和间隔时间。

**Q: 如何调整按键间隔？**  

A: 在按键设置中可以设置毫秒级的按键间隔，建议根据实际需求调整。

**Q: 为什么设置了方向键，执行的却是小键盘2468？**

A：需要关闭小键盘左上角的`Num Lock`开关，否则会触发方向键。

### 高级功能
**Q: 什么是"降低卡位"功能？**  

A: 这是一个针对游戏场景优化的功能，通过调整按键间隔来降低游戏中的卡位现象。

**Q: 如何自定义音频提示？**  

A: 打开 `C:\Users\用户名\.lykeys\sound` 目录，替换 `start.mp3`/`stop.mp3` 文件即可。

**Q: 如何配置浮窗显示？**  

A: 在主界面启用浮窗功能，可以调整透明度、位置和显示内容。

## 驱动相关

### 驱动安装
**Q: 驱动安装失败怎么办？**  

A: 请检查：
- 1. 系统是否支持
- 2. 是否有管理员权限
- 3. 是否关闭了安全启动
- 4. 查看错误日志

**Q: 如何手动卸载驱动？**  

A: 使用以下命令：
```cmd
sc stop lykeys
sc delete lykeys
```

### 驱动使用
**Q: 驱动支持哪些设备？**  

A: 支持USB/PS2键鼠设备。

**Q: 驱动是否支持热插拔？**  

A: 是的，支持设备热插拔。

**Q: 如何检查驱动状态？**  

A: 可以通过程序界面或使用驱动接口检查状态。

## 性能相关

### 性能优化
**Q: 如何优化按键速度？**  

A: 建议：
- 1. 合理设置按键间隔
- 2. 开启"降低卡位"功能
- 3. 避免同时触发过多按键

**Q: 程序占用资源过高怎么办？**  

A: 可以：
- 1. 减少同时触发的按键数量
- 2. 增加按键间隔时间
- 3. 关闭不必要的功能

### 稳定性
**Q: 程序偶尔卡顿怎么办？**  

A: 建议：
- 1. 检查系统资源使用情况
- 2. 更新到最新版本
- 3. 清理系统缓存

**Q: 如何提高程序稳定性？**  

A: 可以：
- 1. 定期更新程序
- 2. 保持系统更新
- 3. 避免同时运行其他类似程序

## 其他问题

### 技术支持
**Q: 如何获取技术支持？**  

A: 可以通过：
- 1. GitHub Issues
- 2. 项目文档
- 3. 社区讨论
- 4. 联系作者

**Q: 如何反馈问题？**  

A: 请提供：  
- 1. 系统环境信息
- 2. 问题详细描述
- 3. 错误日志
- 4. 复现步骤

### 更新相关
**Q: 如何更新程序？**  

A: 从GitHub Releases下载最新版本安装包进行更新。

**Q: 更新后需要重新配置吗？**  

A: 不需要，配置信息会自动保留。 
