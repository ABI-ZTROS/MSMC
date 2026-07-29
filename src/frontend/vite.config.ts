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
    // 代码混淆：vite-plugin-obfuscator 1.x 在 transformIndexHtml(post) 阶段
    // 对所有产物 chunk 执行 javascript-obfuscator，显著提升逆向难度
    viteObfuscateFile({
      compact: true,
      controlFlowFlattening: true,
      controlFlowFlatteningThreshold: 0.75,
      deadCodeInjection: true,
      deadCodeInjectionThreshold: 0.4,
      // ⚠️ 必须关闭！debugProtection 是检测到 DevTools 打开就 debugger 死循环，
      // 但 WebView2 在某些版本（尤其是前置版本/内核较老）下，
      // 即使没开 DevTools，debugProtectionInterval 也可能被误触发导致脚本卡住白屏。
      // 防逆向够用 selfDefending + stringArray + controlFlowFlattening。
      debugProtection: false,
      debugProtectionInterval: 0,
      // 混淆时禁用 console 会把 console.log/error/warn 全删掉，
      // 但我们需要 [FE-BOOT]/[FE-ERR] 上报给 C#，所以保留 console。
      disableConsoleOutput: false,
      identifierNamesGenerator: 'hexadecimal',
      renameGlobals: false,
      // ⚠️ selfDefending 生成的自校验代码在 ES Module + 懒加载组合下，
      // 个别打包工具/浏览器版本会触发"无法修改只读属性"异常，先关闭，
      // 其他混淆项足够提升破解成本。等确认前端跑通了可以再按需开。
      selfDefending: false,
      stringArray: true,
      stringArrayEncoding: ['rc4'],
      stringArrayThreshold: 0.75,
      transformObjectKeys: true,
      unicodeEscapeSequence: false,
    }),
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
