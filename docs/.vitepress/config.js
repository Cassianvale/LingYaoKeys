export default {
  title: "灵曜按键",
  description: "基于.NET8.0+WPF开发的灵动、优雅的开源按键工具",
  base: "/LingYaoKeys/", // 基础路径，应与GitHub仓库名匹配
  
  lastUpdated: true,
  cleanUrls: true,
  
  // 多语言支持
  locales: {
    root: {
      label: '简体中文',
      lang: 'zh-CN',
      title: "灵曜按键",
      description: "基于.NET8.0+WPF开发的灵动、优雅的开源按键工具",
      themeConfig: {
        nav: [
          { text: "首页", link: "/" },
          { text: "使用说明", link: "/guide/tutorial" },
          { text: "驱动文档", link: "/driver/" },
          { text: "常见问题", link: "/faq" },
          { text: "GitHub", link: "https://github.com/Cassianvale/LingYaoKeys" }
        ],
        sidebar: {
          "/guide/": [
            {
              text: "指南",
              items: [
                { text: "关于项目", link: "/guide/" },
                { text: "功能说明", link: "/guide/features" },
                { text: "使用教程", link: "/guide/tutorial" },
                { text: "开发相关", link: "/guide/development" }
              ]
            }
          ],
          "/driver/": [
            {
              text: "驱动文档",
              items: [
                { text: "驱动概述", link: "/driver/" },
                { text: "接口文档", link: "/driver/api" },
                { text: "状态码说明", link: "/driver/status-codes" },
                { text: "使用示例", link: "/driver/examples" },
                { text: "调试指南", link: "/driver/debugging" }
              ]
            }
          ]
        },
        docFooter: {
          prev: "上一页",
          next: "下一页"
        },
        lastUpdated: {
          text: "最后更新于"
        },
        editLink: {
          pattern: "https://github.com/Cassianvale/LingYaoKeys/edit/main/docs/:path",
          text: "在 GitHub 上编辑此页"
        },
        outline: {
          level: [2, 3],
          label: "页面导航"
        }
      }
    },
    en: {
      label: 'English',
      lang: 'en-US',
      title: "LingYaoKeys",
      description: "An elegant open-source keyboard tool based on .NET8.0+WPF",
      themeConfig: {
        nav: [
          { text: "Home", link: "/en/" },
          { text: "Guide", link: "/en/guide/" },
          { text: "Driver", link: "/en/driver/" },
          { text: "FAQ", link: "/en/faq" },
          { text: "GitHub", link: "https://github.com/Cassianvale/LingYaoKeys" }
        ],
        sidebar: {
          "/en/guide/": [
            {
              text: "Guide",
              items: [
                { text: "Introduction", link: "/en/guide/" },
                { text: "Getting Started", link: "/en/guide/getting-started" },
                { text: "Features", link: "/en/guide/features" },
                { text: "Tutorial", link: "/en/guide/tutorial" },
                { text: "Development", link: "/en/guide/development" }
              ]
            }
          ],
          "/en/driver/": [
            {
              text: "Driver Documentation",
              items: [
                { text: "Overview", link: "/en/driver/" },
                { text: "API", link: "/en/driver/api" },
                { text: "Status Codes", link: "/en/driver/status-codes" },
                { text: "Examples", link: "/en/driver/examples" },
                { text: "Debugging", link: "/en/driver/debugging" }
              ]
            }
          ]
        },
        docFooter: {
          prev: "Previous",
          next: "Next"
        },
        lastUpdated: {
          text: "Last Updated"
        },
        editLink: {
          pattern: "https://github.com/Cassianvale/LingYaoKeys/edit/main/docs/:path",
          text: "Edit this page on GitHub"
        },
        outline: {
          level: [2, 3],
          label: "On this page"
        }
      }
    }
  },
  
  head: [
    ["link", { rel: "icon", href: "/favicon.ico" }]
  ],
  
  themeConfig: {
    logo: "/logo.png",
    siteTitle: "灵曜按键",
    // 共享配置移到此处
    socialLinks: [
      { icon: "github", link: "https://github.com/Cassianvale/LingYaoKeys" }
    ],
    footer: {
      message: "Released under the GPL-3.0 License.",
      copyright: "Copyright © 2025-present Cassianvale"
    },
    search: {
      provider: "local"
    },
    lastUpdated: {
      formatOptions: {
        dateStyle: "full",
        timeStyle: "medium"
      }
    }
  }
} 