import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { viteObfuscateFile } from 'vite-plugin-obfuscator'
import path from 'path'

export default defineConfig({
  // 【关键】file:// 协议下绝对路径(/assets/...)会解析到磁盘根目录(I:\assets\..)
  // 必须用相对路径 './'，打包后 HTML 里的引用都变成 ./assets/...
  // 才能在 file:///I:/.../dist/index.html 时正确定位到同目录下的 assets/
  base: './',
  plugins: [
    react(),
    // ═══════════════════════════════════════════════════════════
    // [WARN] TROUBLESHOOTING MODE：暂时禁用混淆器验证能否出界面
    // 已知：vite-plugin-obfuscator 在某些 WebView2 内核下，
    //       即使关闭 debugProtection/selfDefending，
    //       stringArrayEncoding:rc4 + controlFlowFlattening + deadCodeInjection
    //       三者叠加 + ES Module chunk 懒加载时，
    //       也可能在 main.js 这种较大 chunk 上产生无法被 window.onerror 捕获的
    //       早期 SyntaxError / ReferenceError，表现为：
    //       「NavigationCompleted 成功但 __msmcMainScriptLoaded 永远 false」
    //       （诊断层显示 Warning 8 秒超时，但没有 ERROR 行）
    // 定位策略：先 100% 关闭混淆器 → 能出界面 → 再逐条打开。
    // ═══════════════════════════════════════════════════════════
    // viteObfuscateFile({
    //   compact: true,
    //   controlFlowFlattening: true,
    //   controlFlowFlatteningThreshold: 0.75,
    //   deadCodeInjection: true,
    //   deadCodeInjectionThreshold: 0.4,
    //   debugProtection: false,
    //   debugProtectionInterval: 0,
    //   disableConsoleOutput: false,
    //   identifierNamesGenerator: 'hexadecimal',
    //   renameGlobals: false,
    //   selfDefending: false,
    //   stringArray: true,
    //   stringArrayEncoding: ['rc4'],
    //   stringArrayThreshold: 0.75,
    //   transformObjectKeys: true,
    //   unicodeEscapeSequence: false,
    // }),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    minify: 'esbuild',
    // modulePreload 开启 polyfill：file:// 协议下个别 WebView2 内核
    // 对 <link rel="modulepreload"> 的实现有瑕疵，关闭能避免一些懒加载 chunk
    // 无法触发预加载的问题。真正需要预加载时浏览器回退到普通 import。
    modulePreload: { polyfill: false },
    rollupOptions: {
      input: {
        main: path.resolve(__dirname, 'index.html'),
        startup: path.resolve(__dirname, 'startup.html'),
      },
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom', 'react-router-dom'],
          charts: ['recharts'],
          icons: ['react-icons'],
        },
      },
    },
  },
})
