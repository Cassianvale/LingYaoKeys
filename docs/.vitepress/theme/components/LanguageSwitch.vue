<template>
  <div class="language-switch">
    <a 
      v-for="locale in availableLocales"
      :key="locale.link"
      :href="withBase(locale.link)"
      :class="{ active: locale.lang === currentLocale }"
      class="language-link"
    >
      {{ locale.label }}
    </a>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useData, withBase } from 'vitepress'

const { lang, site, theme, page } = useData()

const currentLocale = computed(() => lang.value)

// 获取可用的语言
const availableLocales = computed(() => {
  const path = page.value.relativePath
  const locales = site.value.locales || {}
  const links = {}

  // 根据当前路径生成对应语言的链接
  for (const [key, value] of Object.entries(locales)) {
    let targetPath
    if (key === 'root' && path.startsWith('en/')) {
      targetPath = path.replace(/^en\//, '')
    } else if (key === 'en' && !path.startsWith('en/')) {
      targetPath = `en/${path}`
    } else {
      targetPath = path
    }

    // 处理主页的特殊情况
    if (targetPath === 'index.md') {
      links[key] = { 
        link: key === 'root' ? '/' : `/${key}/`, 
        lang: value.lang, 
        label: value.label 
      }
    } else {
      const link = `/${targetPath.replace(/\.md$/, '.html')}`
      links[key] = { 
        link: key === 'root' ? link : link.replace(/^\//, `/${key}/`).replace(/\/en\/en\//, '/en/'),
        lang: value.lang,
        label: value.label
      }
    }
  }

  return Object.values(links)
})
</script>

<style scoped>
.language-switch {
  display: flex;
  gap: 8px;
  align-items: center;
}

.language-link {
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 14px;
  font-weight: 500;
  color: var(--vp-c-text-2);
  transition: color 0.25s, background-color 0.25s;
  cursor: pointer;
}

.language-link:hover {
  color: var(--vp-c-text-1);
  background-color: var(--vp-c-bg-soft);
}

.language-link.active {
  color: var(--vp-c-brand);
  background-color: var(--vp-c-brand-soft);
}
</style> 