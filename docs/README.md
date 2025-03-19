# 灵曜按键文档

这是灵曜按键的官方文档站点，基于VitePress构建。

## 本地开发

```bash
# 进入文档目录
cd docs

# 安装依赖
npm install

# 启动开发服务器
npm run docs:dev
```

## 构建文档

```bash
# 构建静态文件
npm run docs:build

# 预览构建结果
npm run docs:preview
```

## 文档结构

```
docs/
├── .vitepress/        # VitePress配置
├── public/            # 静态资源
├── guide/             # 用户指南
├── driver/            # 驱动文档
├── faq.md             # 常见问题
└── index.md           # 首页
``` 