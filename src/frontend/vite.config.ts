import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { viteObfuscateFile } from 'vite-plugin-obfuscator'
import path from 'path'

export default defineConfig({
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
      debugProtection: true,
      debugProtectionInterval: 2000,
      disableConsoleOutput: true,
      identifierNamesGenerator: 'hexadecimal',
      renameGlobals: false,
      selfDefending: true,
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
