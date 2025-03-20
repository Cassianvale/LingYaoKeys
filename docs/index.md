---
layout: home
hero:
  name: "灵曜按键"
  text: "优雅而强大的按键工具，让操作更流畅自然"
  tagline: 基于.NET8.0+WPF开发，完美融合现代UI与强大功能
  image:
    src: /logo.png
    alt: LingYaoKeys
  actions:
    - theme: brand
      text: 关于项目
      link: /guide/index
    - theme: alt
      text: 直接下载
      link: https://github.com/Cassianvale/LingYaoKeys/releases/latest
    - theme: alt
      text: 在GitHub上查看
      link: https://github.com/Cassianvale/LingYaoKeys
    - theme: alt
      text: 常见问题
      link: /faq

features:
  - icon: 🎯
    title: 优雅交互
    details: 支持拖拽排序、浮窗状态显示、语音反馈，让操作更直观自然
    linkText: 功能详情
    link: /guide/features
  - icon: ⚡
    title: 强大功能
    details: 全局热键、坐标移动、侧键滚轮触发、独立间隔控制、窗口句柄嗅探，满足专业需求
    linkText: 使用说明
    link: /guide/tutorial
  - icon: 🛡
    title: 安全可靠
    details: 基于内核级驱动实现，支持离线运行，确保应用稳定安全，开放驱动文件和接口文档，方便开发者了进行二次开发
    linkText: 驱动文档
    link: /driver/opensource
  - icon: 🛠️
    title: 稳定兼容
    details: 支持32位/64位系统，兼容Win10/Win11，支持驱动热插拔，无痕卸载
    linkText: 系统和环境要求
    link: /guide/#系统要求
  - icon: 🔄
    title: 灵活配置
    details: 支持配置导入导出、联网更新、调试模式，轻松实现个性化设置
  - icon: 🎨
    title: 优雅设计
    details: 采用MVVM架构，现代化UI设计，流畅的动画效果

---

<h2 class="main-title">软件界面预览</h2>

<!-- 新增GIF动画展示部分 -->
<div class="preview-section">
  <h3>功能演示</h3>
  <p>实际操作演示，展示灵曜按键的流畅使用体验和核心功能。灵曜按键提供极致的用户体验和专业级的按键工具，让您的操作更加高效。</p>
  <div class="preview-image-wrapper wide gif-container">
    <img src="/images/screenshots.gif" alt="灵曜按键操作演示" class="preview-image gif-image">
    <div class="image-caption">软件实际操作演示</div>
  </div>
</div>

<div class="preview-container">
  <div class="preview-section">
    <h3>主界面 - 功能开关与按键设置</h3>
    <p>灵曜按键提供直观的用户界面，支持一键开关、自定义间隔时间和多种触发模式。通过简洁的界面设计，让您轻松掌控每一个功能细节。</p>
    <div class="preview-images">
      <div class="preview-image-wrapper">
        <img src="/images/main-interface.png" alt="灵曜按键主界面" class="preview-image">
        <div class="image-caption">主界面展示功能开关和基本设置</div>
      </div>
      <div class="preview-image-wrapper">
        <img src="/images/coordinate-interface.png" alt="灵曜按键坐标显示界面" class="preview-image">
        <div class="image-caption">实时显示坐标位置和触发状态</div>
      </div>
    </div>
  </div>
  
  <div class="preview-section">
    <h3>窗口句柄选择器</h3>
    <p>精确定位应用窗口，支持多种识别方式和自动刷新功能。快速锁定目标窗口，获取准确句柄信息，实现精准操作控制。</p>
    <div class="preview-image-wrapper wide">
      <img src="/images/window-selector.png" alt="窗口句柄选择器" class="preview-image">
      <div class="image-caption">轻松选择目标窗口，获取句柄信息</div>
    </div>
  </div>
</div>

<style>
.preview-container {
  margin: 2rem 0;
}

.preview-section {
  margin-bottom: 3rem;
}

.preview-section h3 {
  margin-bottom: 0.75rem;
  font-size: 1.6rem;
  font-weight: 600;
  color: var(--vp-c-brand-1);
  letter-spacing: -0.01em;
  line-height: 1.4;
  transition: color 0.2s ease;
  position: relative;
  display: inline-block;
}

.preview-section h3::after {
  content: '';
  position: absolute;
  bottom: -4px;
  left: 0;
  width: 2rem;
  height: 2px;
  background: var(--vp-c-brand-2);
  border-radius: 2px;
  transition: width 0.3s ease;
}

.preview-section:hover h3::after {
  width: 100%;
}

.preview-section p {
  margin-bottom: 1.75rem;
  font-size: 1.05rem;
  line-height: 1.6;
  color: var(--vp-c-text-2);
  max-width: 42rem;
  margin-left: auto;
  margin-right: auto;
  opacity: 0.9;
  transition: opacity 0.2s ease;
}

.preview-section:hover p {
  opacity: 1;
  color: var(--vp-c-text-1);
}

.preview-images {
  display: flex;
  flex-wrap: wrap;
  gap: 1.5rem;
  justify-content: center;
}

.preview-image-wrapper {
  flex: 1;
  min-width: 300px;
  max-width: 100%;
  border-radius: 12px;
  overflow: hidden;
  
  box-shadow: 
    0 5px 15px rgba(0, 0, 0, 0.05),
    0 15px 35px rgba(0, 0, 0, 0.07);
  
  background: linear-gradient(
    to bottom,
    var(--vp-c-bg-soft),
    var(--vp-c-bg)
  );
  
  border: 1px solid var(--vp-c-divider-light);
  
  transition: 
    transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1),
    box-shadow 0.3s ease, 
    border-color 0.3s ease;
  
  display: flex;
  flex-direction: column;
  position: relative;
}

.preview-image-wrapper:hover {
  transform: translateY(-6px) scale(1.01);
  box-shadow: 
    0 10px 25px rgba(0, 0, 0, 0.07),
    0 20px 45px rgba(0, 0, 0, 0.09);
  border-color: var(--vp-c-brand-soft);
  
  background: linear-gradient(
    to bottom,
    var(--vp-c-bg-soft),
    var(--vp-c-bg-soft) 60%,
    var(--vp-c-bg)
  );
}

.preview-image-wrapper.wide {
  flex-basis: 100%;
  max-width: 800px;
  margin: 0 auto;
}

.preview-image {
  width: 100%;
  height: auto;
  display: block;
  border-radius: 12px 12px 0 0;
  object-fit: cover;
  
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.image-caption {
  padding: 1.1rem 1.25rem;
  font-size: 0.95rem;
  font-weight: 500;
  color: var(--vp-c-text-2);
  text-align: center;
  border-top: 1px solid var(--vp-c-divider-light);
  background: var(--vp-c-bg-soft);
  margin-top: auto;
  transition: color 0.2s ease, background-color 0.2s ease;
  letter-spacing: 0.01em;
}

.preview-image-wrapper:hover .image-caption {
  color: var(--vp-c-text-1);
  background: linear-gradient(
    to right,
    var(--vp-c-bg-soft),
    var(--vp-c-bg-mute),
    var(--vp-c-bg-soft)
  );
}

.gif-container {
  max-width: 800px;
  margin: 0 auto;
  overflow: hidden;
  
  box-shadow: 
    0 8px 20px rgba(0, 0, 0, 0.07),
    0 20px 50px rgba(0, 0, 0, 0.1);
}

.gif-image {
  width: 100%;
  height: 100%;
  object-fit: contain;
  
  border: 1px solid var(--vp-c-divider-light);
  border-radius: 12px 12px 0 0;
  
  backface-visibility: hidden;
}

.dark .preview-image-wrapper {
  box-shadow: 
    0 5px 15px rgba(0, 0, 0, 0.2),
    0 15px 35px rgba(0, 0, 0, 0.25);
  border-color: var(--vp-c-divider-dark);
}

.dark .preview-image-wrapper:hover {
  box-shadow: 
    0 10px 25px rgba(0, 0, 0, 0.25),
    0 20px 45px rgba(0, 0, 0, 0.3);
}

.dark .preview-section h3 {
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
}

.dark .preview-section p {
  text-shadow: 0 1px 1px rgba(0, 0, 0, 0.1);
}

.dark .image-caption {
  border-top-color: var(--vp-c-divider-dark);
}

@media (max-width: 767px) {
  .preview-image-wrapper {
    min-width: 100%;
  }
  
  .preview-images {
    flex-direction: column;
  }
  
  .preview-image-wrapper.wide {
    max-width: 100%;
  }
  
  .preview-section h3 {
    font-size: 1.4rem;
  }
  
  .preview-section p {
    font-size: 1rem;
    margin-bottom: 1.5rem;
  }
  
  .image-caption {
    padding: 0.9rem 1rem;
    font-size: 0.9rem;
  }
}
</style>