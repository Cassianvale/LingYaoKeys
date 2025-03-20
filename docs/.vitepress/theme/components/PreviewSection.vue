<template>
  <div>
    <h2 class="main-title">{{ i18n.title }}</h2>

    <div class="preview-section">
      <h3>{{ i18n.demoTitle }}</h3>
      <p>{{ i18n.demoDescription }}</p>
      <div class="preview-image-wrapper wide gif-container">
        <img src="/images/screenshots.gif" :alt="i18n.demoAlt" class="preview-image gif-image">
        <div class="image-caption">{{ i18n.demoCaption }}</div>
      </div>
    </div>

    <div class="preview-container">
      <div class="preview-section">
        <h3>{{ i18n.mainTitle }}</h3>
        <p>{{ i18n.mainDescription }}</p>
        <div class="preview-images">
          <div class="preview-image-wrapper">
            <img src="/images/main-interface.png" :alt="i18n.mainAlt" class="preview-image">
            <div class="image-caption">{{ i18n.mainCaption }}</div>
          </div>
          <div class="preview-image-wrapper">
            <img src="/images/coordinate-interface.png" :alt="i18n.coordAlt" class="preview-image">
            <div class="image-caption">{{ i18n.coordCaption }}</div>
          </div>
        </div>
      </div>
      
      <div class="preview-section">
        <h3>{{ i18n.windowTitle }}</h3>
        <p>{{ i18n.windowDescription }}</p>
        <div class="preview-image-wrapper wide">
          <img src="/images/window-selector.png" :alt="i18n.windowAlt" class="preview-image">
          <div class="image-caption">{{ i18n.windowCaption }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, computed, ref } from 'vue'

// 检测当前页面路径来确定语言
const isEnglish = ref(false)

onMounted(() => {
  // 检查当前URL路径是否包含 /en/
  isEnglish.value = window.location.pathname.includes('/en/')
  
  // 选择所有需要添加动画的元素
  const animatedElements = document.querySelectorAll('.preview-section, .preview-image-wrapper, .main-title');
  
  // 计时器变量，用于后备方案
  let fallbackTimer;
  
  // 添加后备方案：确保5秒后所有元素都显示，无论IntersectionObserver是否工作
  fallbackTimer = setTimeout(function() {
    animatedElements.forEach(element => {
      if (!element.classList.contains('visible')) {
        element.classList.add('default-visible');
      }
    });
  }, 5000);
  
  // 检查IntersectionObserver是否被支持
  if ('IntersectionObserver' in window) {
    // 创建一个IntersectionObserver实例
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        // 如果元素进入视口
        if (entry.isIntersecting) {
          // 给元素添加visible类
          entry.target.classList.add('visible');
          // 元素已经显示，不再需要观察
          observer.unobserve(entry.target);
        }
      });
    }, {
      // 设置根元素为null（表示视口）
      root: null,
      // 设置根边距，增大范围以提前触发
      rootMargin: '50px 0px',
      // 设置阈值为0.1（元素有10%进入视口时触发回调）
      threshold: 0.1
    });
    
    // 开始观察所有动画元素
    animatedElements.forEach(element => {
      observer.observe(element);
    });
    
    // 如果页面已经滚动，立即检查元素是否可见
    const checkVisibility = function() {
      animatedElements.forEach(element => {
        const rect = element.getBoundingClientRect();
        if (rect.top < window.innerHeight && rect.bottom > 0) {
          element.classList.add('visible');
          observer.unobserve(element);
        }
      });
    };
    
    // 页面加载时检查一次
    checkVisibility();
    
    // 页面滚动时也检查一次，确保元素可见
    window.addEventListener('scroll', checkVisibility, { passive: true });
  } else {
    // 浏览器不支持IntersectionObserver，立即显示所有元素
    animatedElements.forEach(element => {
      element.classList.add('default-visible');
    });
    
    // 清除后备计时器，因为已经立即显示了
    clearTimeout(fallbackTimer);
  }
  
  // 页面加载2秒后强制显示首屏元素
  setTimeout(function() {
    const visibleNowElements = document.querySelectorAll('.preview-section:first-child, .preview-image-wrapper:first-child, .main-title');
    visibleNowElements.forEach(element => {
      if (!element.classList.contains('visible')) {
        element.classList.add('default-visible');
      }
    });
  }, 2000);
})

// 多语言内容
const i18n = computed(() => {
  if (isEnglish.value) {
    return {
      title: 'Software Interface Preview',
      demoTitle: 'Feature Demonstration',
      demoDescription: 'Real operation demonstration showcasing the smooth user experience and core functionality of LingYaoKeys. LingYaoKeys provides the ultimate user experience and professional-grade key tools to make your operations more efficient.',
      demoAlt: 'LingYaoKeys Operation Demo',
      demoCaption: 'Software actual operation demonstration',
      
      mainTitle: 'Main Interface - Function Switches and Key Settings',
      mainDescription: 'LingYaoKeys provides an intuitive user interface with one-click switches, customizable interval times, and multiple trigger modes. Through a clean interface design, you can easily control every functional detail.',
      mainAlt: 'LingYaoKeys Main Interface',
      mainCaption: 'Main interface showing function switches and basic settings',
      
      coordAlt: 'LingYaoKeys Coordinate Display Interface',
      coordCaption: 'Real-time display of coordinate positions and trigger status',
      
      windowTitle: 'Window Handle Selector',
      windowDescription: 'Precisely locate application windows with support for multiple identification methods and automatic refresh functionality. Quickly lock target windows, obtain accurate handle information, and achieve precise operation control.',
      windowAlt: 'Window Handle Selector',
      windowCaption: 'Easily select target windows and obtain handle information'
    }
  } else {
    return {
      title: '软件界面预览',
      demoTitle: '功能演示',
      demoDescription: '实际操作演示，展示灵曜按键的流畅使用体验和核心功能。灵曜按键提供极致的用户体验和专业级的按键工具，让您的操作更加高效。',
      demoAlt: '灵曜按键操作演示',
      demoCaption: '软件实际操作演示',
      
      mainTitle: '主界面 - 功能开关与按键设置',
      mainDescription: '灵曜按键提供直观的用户界面，支持一键开关、自定义间隔时间和多种触发模式。通过简洁的界面设计，让您轻松掌控每一个功能细节。',
      mainAlt: '灵曜按键主界面',
      mainCaption: '主界面展示功能开关和基本设置',
      
      coordAlt: '灵曜按键坐标显示界面',
      coordCaption: '实时显示坐标位置和触发状态',
      
      windowTitle: '窗口句柄选择器',
      windowDescription: '精确定位应用窗口，支持多种识别方式和自动刷新功能。快速锁定目标窗口，获取准确句柄信息，实现精准操作控制。',
      windowAlt: '窗口句柄选择器',
      windowCaption: '轻松选择目标窗口，获取句柄信息'
    }
  }
})
</script>

<style scoped>
.preview-container {
  margin: 2rem 0;
}

.preview-section {
  margin-bottom: 3rem;
  opacity: 0;
  transform: translateY(30px);
  transition: opacity 0.8s ease, transform 0.8s ease;
}

.preview-section.visible {
  opacity: 1;
  transform: translateY(0);
}

/* 添加默认可见类，确保内容始终可见 */
.preview-section.default-visible {
  opacity: 1;
  transform: translateY(0);
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
  opacity: 0;
  transform: translateY(20px);
  transition: 
    opacity 0.8s ease, 
    transform 0.8s ease,
    box-shadow 0.3s ease, 
    border-color 0.3s ease;
  
  box-shadow: 
    0 5px 15px rgba(0, 0, 0, 0.05),
    0 15px 35px rgba(0, 0, 0, 0.07);
  
  background: linear-gradient(
    to bottom,
    var(--vp-c-bg-soft),
    var(--vp-c-bg)
  );
  
  border: 1px solid var(--vp-c-divider-light);
  
  display: flex;
  flex-direction: column;
  position: relative;
}

/* 添加默认可见类，确保图片始终可见 */
.preview-image-wrapper.default-visible {
  opacity: 1;
  transform: translateY(0);
}

.preview-image-wrapper.visible {
  opacity: 1;
  transform: translateY(0);
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

.main-title {
  opacity: 0;
  transform: translateY(20px);
  transition: opacity 0.8s ease, transform 0.8s ease;
}

.main-title.visible, .main-title.default-visible {
  opacity: 1;
  transform: translateY(0);
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

/* 动画延迟 */
.preview-section:nth-child(1) { transition-delay: 0.1s; }
.preview-section:nth-child(2) { transition-delay: 0.2s; }
.preview-section:nth-child(3) { transition-delay: 0.3s; }

.preview-images .preview-image-wrapper:nth-child(1) { transition-delay: 0.3s; }
.preview-images .preview-image-wrapper:nth-child(2) { transition-delay: 0.5s; }
</style> 