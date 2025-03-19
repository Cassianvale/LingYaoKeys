<template>
  <div class="nav-container">
    <slot name="nav-bar-title-before" />
    <slot name="nav-bar-title">
      <div class="title">
        <div class="logo" v-if="logoInfo.src">
          <img
            v-if="logoInfo.src"
            class="logo-img"
            :src="logoInfo.src"
            :alt="logoInfo.alt"
          />
        </div>
        <div class="VPNavBarTitle" v-if="siteTitle">
          {{ siteTitle }}
        </div>
      </div>
    </slot>
    <slot name="nav-bar-title-after" />

    <div class="VPNavBarMenu" v-if="navData.length > 0">
      <slot name="nav-bar-content-before" />
      <slot name="nav-bar-content">
        <div class="menu-items">
          <div v-for="item in navData" :key="item.text" class="menu-item">
            <a class="menu-link" :href="item.link">{{ item.text }}</a>
          </div>
        </div>
      </slot>
      <slot name="nav-bar-content-after" />
    </div>

    <div class="nav-right">
      <slot name="nav-bar-search">
        <div class="search-placeholder"></div>
      </slot>

      <!-- 语言切换器 -->
      <div class="lang-switch">
        <LanguageSwitch />
      </div>

      <slot name="nav-bar-social-links">
        <div
          v-if="theme.socialLinks"
          class="VPNavBarSocialLinks"
        >
          <a
            v-for="link in theme.socialLinks"
            :key="link.link"
            class="social-link"
            :href="link.link"
            target="_blank"
            rel="noopener noreferrer"
          >
            <component
              :is="link.icon"
              class="social-icon"
              v-if="typeof link.icon === 'object'"
            />
            <div v-else-if="link.icon === 'github'" class="social-icon github">
              <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24">
                <path
                  d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"
                />
              </svg>
            </div>
          </a>
        </div>
      </slot>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useData, withBase } from 'vitepress'

const { site, theme, page } = useData()

// 获取导航数据
const navData = computed(() => {
  return theme.value.nav || []
})

// 网站标题
const siteTitle = computed(() => theme.value.siteTitle ?? site.value.title)

// Logo信息
const logoInfo = computed(() => {
  const { logo, logoLink } = theme.value
  
  return {
    src: logo ? withBase(logo) : null,
    alt: site.value.title,
    link: logoLink
  }
})
</script>

<style scoped>
.nav-container {
  display: flex;
  align-items: center;
  height: 64px;
  padding: 0 24px;
  background-color: var(--vp-c-bg);
  border-bottom: 1px solid var(--vp-c-divider);
}

.title {
  display: flex;
  align-items: center;
  font-size: 18px;
  font-weight: 600;
  color: var(--vp-c-text-1);
}

.logo {
  margin-right: 8px;
}

.logo-img {
  height: 32px;
  width: auto;
}

.VPNavBarMenu {
  margin-left: 24px;
  flex: 1;
}

.menu-items {
  display: flex;
  align-items: center;
}

.menu-item {
  position: relative;
  margin-right: 16px;
}

.menu-link {
  display: flex;
  align-items: center;
  padding: 0 12px;
  height: 40px;
  font-size: 14px;
  font-weight: 500;
  color: var(--vp-c-text-1);
  transition: color 0.25s;
}

.menu-link:hover {
  color: var(--vp-c-brand);
}

.nav-right {
  display: flex;
  align-items: center;
}

.search-placeholder {
  width: 200px;
  height: 40px;
}

.lang-switch {
  margin: 0 16px;
}

.VPNavBarSocialLinks {
  display: flex;
  align-items: center;
}

.social-link {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  color: var(--vp-c-text-2);
  transition: color 0.25s;
}

.social-link:hover {
  color: var(--vp-c-brand);
}

.social-icon {
  width: 20px;
  height: 20px;
  fill: currentColor;
}
</style> 