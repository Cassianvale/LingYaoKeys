<template>
  <Layout>
    <template #nav-bar>
      <NavBar />
    </template>
  </Layout>
</template>

<script setup>
import DefaultTheme from 'vitepress/theme'
import NavBar from '../components/NavBar.vue'
import { useData, inBrowser } from 'vitepress'
import { watchEffect } from 'vue'

const { Layout } = DefaultTheme
const { lang } = useData()

// 记忆用户的语言选择
watchEffect(() => {
  if (inBrowser) {
    document.cookie = `nf_lang=${lang.value}; expires=Mon, 1 Jan 2030 00:00:00 UTC; path=/`
  }
})
</script> 