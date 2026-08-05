# 性能与冷启动全面优化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将冷启动时间缩短 4-5 秒，前端首屏体积减少 ~400KB，消除冗余 CPU/GPU 开销

**Architecture:** 三层优化：后端启动链去阻塞 + 前端 bundle 瘦身 + CSS/渲染减负

**Tech Stack:** C# .NET 9 WPF / React 18 + Vite 5 / WebView2

---

## Task 1: 移除人为 Task.Delay（预计省 4-5 秒）

**Files:**
- Modify: `src/MSMC/App.xaml.cs` 第 570/580-641/646/896 行

- [ ] **Step 1: 定位所有 Task.Delay**

搜索 `Task.Delay` 在 App.xaml.cs 中的所有出现位置。

- [ ] **Step 2: 移除 Register/RegisterType/RegisterInstance 中的双 40ms delay**

三个 helper（约第 580-641 行）每个含两个 `await Task.Delay(40)`。删除全部 6 处。

- [ ] **Step 3: 移除 Step 函数中的 100ms delay**

约第 570 行 `await Task.Delay(100)` → 删除。

- [ ] **Step 4: 移除第 646 行的 80ms delay**

- [ ] **Step 5: 移除第 896 行的 600ms 收尾 delay**

注释"短暂延迟让用户看到启动完成"→ 删除 delay，保留 AppendLog。

- [ ] **Step 6: 验证编译**

Run: `dotnet build` 或在 VS 中 Build
Expected: 0 errors

- [ ] **Step 7: Commit**

```bash
git add src/MSMC/App.xaml.cs
git commit -m "perf(boot): 移除人为 Task.Delay 累计 4-5 秒冷启动延迟"
```

---

## Task 2: Serilog 日志清理移到后台

**Files:**
- Modify: `src/MSMC/App.xaml.cs` 第 431-455 行

- [ ] **Step 1: 将日志清理包到 Task.Run**

把第 431-455 行的日志清理代码从同步改为 `Task.Run(() => { ... })` fire-and-forget。

- [ ] **Step 2: 验证编译并 Commit**

```bash
git commit -m "perf(boot): Serilog 日志清理移到后台线程，不阻塞 UI"
```

---

## Task 3: 移除 AppResources.xaml 重复合并

**Files:**
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml` 第 31-37 行

- [ ] **Step 1: 删除 MainWindow.xaml 的 Window.Resources**

App.xaml 已全局合并 AppResources.xaml，MainWindow 不需要再合并一次。删除整个 `<Window.Resources>` 块。

- [ ] **Step 2: 验证编译并 Commit**

```bash
git commit -m "perf(xaml): 移除 AppResources.xaml 在 MainWindow 的重复合并"
```

---

## Task 4: EmbeddedResourceProvider 去掉 AssertEntry O(N) 扫描

**Files:**
- Modify: `src/MSMC/Features/WebView2/Frontend/EmbeddedResourceProvider.cs` 第 74-92 行

- [ ] **Step 1: 删除 6 次 AssertEntry 全表扫描**

`AssertEntry` 用 `_entryMap.Keys.Any(k => k.Contains(...))` 做 O(N) 全表扫描 × 6 次。删除全部 AssertEntry 调用和相关日志。

- [ ] **Step 2: 验证编译并 Commit**

```bash
git commit -m "perf(webview2): 移除 EmbeddedResourceProvider 的 6 次 O(N) AssertEntry 扫描"
```

---

## Task 5: 同步文件读取并行化

**Files:**
- Modify: `src/MSMC/App.xaml.cs` 第 475/494/696 行附近

- [ ] **Step 1: 将 UserAgreementService.Load + earlyThemeService.LoadSettings + ReadEnablePowerManagementEarly 并行化**

这三个同步读盘操作互相独立，可以包到 `Task.WhenAll` 中并行执行。

- [ ] **Step 2: 验证编译并 Commit**

```bash
git commit -m "perf(boot): 三个同步文件读取并行化"
```

---

## Task 6: WebResourceRequested 去掉每请求 MemoryStream 拷贝

**Files:**
- Modify: `src/MSMC/Features/WebView2/Services/WebView2BridgeService.cs` 第 476 行附近

- [ ] **Step 1: 用 Stream 直传替代 MemoryStream 拷贝**

把 `resourceStream.CopyTo(memoryStream)` + `new MemoryStream(...)` 改为直接传 `resourceStream` 给 `VirtualHostNameToResourceRequestedHandler` 的 Response。如果 WebView2 API 不支持直传，至少复用 ArrayBuffer 池。

- [ ] **Step 2: 验证编译并 Commit**

```bash
git commit -m "perf(webview2): WebResourceRequested 去掉每请求 MemoryStream 拷贝"
```

---

## Task 7: recharts 从首屏 modulepreload 移除

**Files:**
- Modify: `src/frontend/vite.config.ts` manualChunks 配置
- Modify: `src/frontend/index.html`（如果有硬编码 preload）

- [ ] **Step 1: 修改 manualChunks 策略**

把 `charts: ['recharts']` 从 manualChunks 对象中移除，改为让 recharts 随 DashboardPage lazy chunk 自然加载。或改为函数形式按需分割。

- [ ] **Step 2: 验证 index.html 不再 preload charts**

Run: `npm run build && grep -i "charts" dist/index.html`
Expected: 无 charts 相关 modulepreload

- [ ] **Step 3: 验证 build 并 Commit**

```bash
git commit -m "perf(frontend): recharts 从首屏预加载移除，减少 ~400KB 首屏体积"
```

---

## Task 8: 修复 icons 双 chunk + manualChunks 函数化

**Files:**
- Modify: `src/frontend/vite.config.ts`
- Modify: `src/frontend/src/utils/icons.tsx`

- [ ] **Step 1: manualChunks 改为函数形式**

```ts
manualChunks(id) {
  if (id.includes('node_modules/react-icons')) return 'icons'
  if (id.includes('node_modules/react') || id.includes('node_modules/react-dom') || id.includes('node_modules/react-router')) return 'vendor'
}
```

- [ ] **Step 2: 移除 icons.tsx 中未使用的图标**

审查 ICON_MAP 的 41 个图标，找出哪些实际未在任何文件中被引用，移除之。

- [ ] **Step 3: 验证 build 只产生一个 icons chunk**

Run: `npm run build && ls dist/assets/icons*`
Expected: 只有一个 icons-*.js

- [ ] **Step 4: Commit**

```bash
git commit -m "perf(frontend): 修复 icons 双 chunk，manualChunks 函数化统一归并"
```

---

## Task 9: CSS keyframes 去重 + 无限动画优化

**Files:**
- Modify: `src/frontend/src/styles/globals.css`

- [ ] **Step 1: 删除重复的 keyframes 定义**

搜索所有 `@keyframes` 定义，删除重复的（mdOrbit × 2、mdFlow × 2、mdFadeIn × 2）。

- [ ] **Step 2: 给非视口内的无限动画加 content-visibility**

对 `mdBrandPulse`、`mdBreathe`、`mdOrbit`、`mdFlow`、`mdScanline` 等装饰动画的容器加 `content-visibility: auto` 或用 `@media (prefers-reduced-motion)` 降级。

- [ ] **Step 3: 验证 build 并 Commit**

```bash
git commit -m "perf(css): 删除 3 处重复 keyframes，优化无限动画 GPU 开销"
```

---

## Task 10: Sidebar 诊断代码移到 DEV only

**Files:**
- Modify: `src/frontend/src/components/Sidebar.tsx`

- [ ] **Step 1: 用 import.meta.env.DEV 包裹诊断代码**

把 `[FE-DIAG]` 相关的 `getComputedStyle` + `bridge.invoke` 日志上报代码用 `if (import.meta.env.DEV)` 包裹，生产构建时会被 Vite 擦除。

- [ ] **Step 2: 验证 build 并 Commit**

```bash
git commit -m "perf(frontend): Sidebar 诊断代码移到 DEV only，生产构建擦除"
```

---

## Task 11: 最终验证 + 推送

- [ ] **Step 1: npm run build 验证前端**

Run: `cd src/frontend && npm run build`
Expected: exit 0，检查 dist 体积变化

- [ ] **Step 2: dotnet build 验证后端**

Run: `dotnet build`（如果环境有 SDK）
Expected: 0 errors

- [ ] **Step 3: 推送全部提交**

```bash
git push origin main
```
