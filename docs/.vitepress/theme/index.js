import DefaultTheme from 'vitepress/theme'
import Layout from './layouts/Layout.vue'
import LanguageSwitch from './components/LanguageSwitch.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  Layout: Layout,
  enhanceApp({ app }) {
    app.component('LanguageSwitch', LanguageSwitch)
  }
} 