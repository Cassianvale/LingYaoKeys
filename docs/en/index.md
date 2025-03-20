---
layout: home
hero:
  name: "LingYaoKeys"
  text: "Elegant and powerful key mapping tool for smooth operation"
  tagline: Developed with .NET8.0+WPF, perfectly combining modern UI with powerful features
  image:
    src: /logo.png
    alt: LingYaoKeys
  actions:
    - theme: brand
      text: About
      link: /en/guide/index
    - theme: alt
      text: Download
      link: https://github.com/Cassianvale/LingYaoKeys/releases/latest
    - theme: alt
      text: View on GitHub
      link: https://github.com/Cassianvale/LingYaoKeys
    - theme: alt
      text: FAQ
      link: /en/faq

features:
  - icon: 🎯
    title: Elegant Interaction
    details: Supports drag-and-drop sorting, floating window status display, and voice feedback for intuitive operation
    linkText: Feature Details
    link: /en/guide/features
  - icon: ⚡
    title: Powerful Features
    details: Global hotkeys, coordinate movement, side button & wheel triggers, independent interval control, window handle detection
    linkText: Tutorial
    link: /en/guide/tutorial
  - icon: 🛡
    title: Safe & Reliable
    details: Kernel-level driver implementation, offline support, open driver files and interface documentation
    linkText: Driver Documentation
    link: /en/driver/opensource
  - icon: 🛠️
    title: Stable & Compatible
    details: Supports 32/64-bit systems, Win10/11 compatible, hot-plug support, clean uninstall
    linkText: System Requirements
    link: /en/guide/#system-requirements
  - icon: 🔄
    title: Flexible Configuration
    details: Import/export settings, online updates, debug mode for easy customization
  - icon: 🎨
    title: Elegant Design
    details: MVVM architecture, modern UI design, smooth animations

---

<h2 class="main-title">Software Interface Preview</h2>

<!-- GIF Animation Showcase Section -->
<div class="preview-section">
  <h3>Feature Demonstration</h3>
  <p>Real operation demonstration showcasing the smooth user experience and core functionality of LingYaoKeys. LingYaoKeys provides the ultimate user experience and professional-grade key tools to make your operations more efficient.</p>
  <div class="preview-image-wrapper wide gif-container">
    <img src="/images/screenshots.gif" alt="LingYaoKeys Operation Demo" class="preview-image gif-image">
    <div class="image-caption">Software actual operation demonstration</div>
  </div>
</div>

<div class="preview-container">
  <div class="preview-section">
    <h3>Main Interface - Function Switches and Key Settings</h3>
    <p>LingYaoKeys provides an intuitive user interface with one-click switches, customizable interval times, and multiple trigger modes. Through a clean interface design, you can easily control every functional detail.</p>
    <div class="preview-images">
      <div class="preview-image-wrapper">
        <img src="/images/main-interface.png" alt="LingYaoKeys Main Interface" class="preview-image">
        <div class="image-caption">Main interface showing function switches and basic settings</div>
      </div>
      <div class="preview-image-wrapper">
        <img src="/images/coordinate-interface.png" alt="LingYaoKeys Coordinate Display Interface" class="preview-image">
        <div class="image-caption">Real-time display of coordinate positions and trigger status</div>
      </div>
    </div>
  </div>
  
  <div class="preview-section">
    <h3>Window Handle Selector</h3>
    <p>Precisely locate application windows with support for multiple identification methods and automatic refresh functionality. Quickly lock target windows, obtain accurate handle information, and achieve precise operation control.</p>
    <div class="preview-image-wrapper wide">
      <img src="/images/window-selector.png" alt="Window Handle Selector" class="preview-image">
      <div class="image-caption">Easily select target windows and obtain handle information</div>
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