# 启动页重构设计文档

> 日期: 2026-09-08
> 状态: 待审批
> 范围: 前端 StartupPage.tsx 彻底重构（拆组件 + 视觉换肤）+ 后端 StartupWindow.xaml.cs 新增 3 个 bridge handler（restart/openTroubleshooting/copyLog）

---

## 1. 背景与驱动力

### 现状

| 文件 | 行数 | 问题 |
|------|------|------|
| `src/frontend/src/pages/StartupPage.tsx` | **1211** | 单文件巨石，UI 渲染 + bridge 事件分发 + CSS keyframes + 日志面板 + 模拟 HUD 全混 |
| `src/MSMC/Features/Startup/Views/StartupWindow.xaml.cs` | **1145** | 承载 WebView2 初始化、桥接脚本注入、事件队列分发 |

### 核心问题

1. **视觉定位错配** — 启动页本质是**视觉占位符**（让用户别以为程序崩了），但当前做成了赛博朋克"装逼页面"：7 层背景装饰（粒子场 + 网格 + 矩阵雨 + CRT 扫描线 + 横向/纵向扫描光带 + 呼吸暗角 + HUD 四角刻度）+ SVG 三层旋转刻度环
2. **性能硬伤** — 大量服务器用集成显卡，粒子连线 + 每帧 RAF 动画 + 多层绝对定位 DOM 导致 WebView2 掉帧
3. **代码结构腐烂** — 14 个 useState 塞在一个组件里，bridge 事件解析揉在 useEffect 中不可单测，CSS keyframes 散在 JSX 里
4. **HUD 自嗨** — FPS/CPU/MEM 是模拟数据（硬编码 `Math.random()`），setInterval + RAF 每帧跑但对启动阶段零价值

### 决策树（用户已确认）

| 维度 | 决策 |
|------|------|
| 定位 | 视觉占位符（核心任务：不让用户焦虑） |
| 背景层 | 砍粒子场 + 砍赛博朋克装饰（CRT/矩阵雨/扫描线/HUD 四角）+ 留极简 1 层径向渐变 + 柔化呼吸光晕 |
| 进度指示 | 一条横条进度条（砍 SVG 三层旋转刻度环） |
| 日志面板 | 常驻 + 自动滚动（保留当前功能） |
| HUD 角标 | 砍 FPS/CPU/MEM 模拟 → 留真实 FPS 监测 + 版本号 + session id |
| 转场节奏 | 成功态停留 2s + 庆祝动画 → 淡出 → 切入主窗口 |
| 动画曲线 | 贝塞尔曲线（`cubic-bezier(0.4, 0, 0.2, 1)` 或类似） |
| 品牌主色 | 芙宁娜水蓝色（待确认具体色值，候选：`#5DC8E8` Pneuma 浅蓝 / `#4A90D9` Ousia 深蓝） |
| ASCII LOGO | 用户单独安排 → 本轮保留 |
| 失败态 | 自动展开日志面板 + 关键错误高亮卡片 + 急救按钮组（重开 / 打开疑难解答 / 复制日志） |
| 代码结构 | 彻底拆 + 保证能跑通 |

---

## 2. 视觉设计

### 品牌色板（候选）

```css
:root {
  /* 主色：芙宁娜水蓝 — 待用户确认具体值 */
  --startup-primary: #5DC8E8;        /* Pneuma 浅蓝（推荐默认） */
  --startup-primary-deep: #4A90D9;    /* Ousia 深蓝 */
  --startup-success: #34d399;         /* 绿色，成功态对勾 */
  --startup-error: #f87171;           /* 红色，失败态 */
  --startup-bg: #081420;              /* 极深水蓝黑背景（替换 #020617） */
  --startup-text: #e2e8f0;            /* 主文字 */
  --startup-text-dim: #94a3b8;        /* 次要文字 */
}
```

> **TODO**: `--startup-primary` 最终值待用户确认。若用户偏好血红（与疑难解答页统一），改为 `#c0392b`。

### 背景层（1 层替代 7 层）

```
旧: 粒子场 + 网格 + 矩阵雨 + CRT + 横向扫描光带 + 纵向扫描光带 + 呼吸暗角 + HUD 四角
新: 径向渐变 + 柔和呼吸光晕（仅 2 层 DOM，零 JS 计算）
```

```css
/* 背景：极深水蓝黑 + 中心柔化光晕 */
.background {
  background: radial-gradient(
    ellipse at center,
    rgba(93, 200, 232, 0.08) 0%,
    rgba(8, 20, 32, 1) 70%
  );
  animation: breathe 6s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}

/* 呼吸光晕（透明度缓动，零位置变化，性能友好） */
@keyframes breathe {
  0%, 100% { opacity: 0.85; }
  50%      { opacity: 1; }
}
```

### 主视觉布局

```
┌──────────────────────────────────────────────────────┐
│  v1.2.0  SESSION A3F9K2  ● BOOT        FPS 60       │ ← HUD 顶部（session/phase + 真实 FPS）
│                                                      │
│              ░░░░░░░░░░░░░░                          │
│              ░  MSMC  ASCII ░                        │ ← ASCII LOGO（用户单独安排）
│              ░░░░░░░░░░░░░░                          │
│                                                      │
│         ┌────────────────────────┐                   │
│         │  ═══════════════░░░░░░ │  68%              │ ← 横条进度条（贝塞尔缓动）
│         └────────────────────────┘                   │
│                                                      │
│              正在初始化服务...                         │ ← 状态文字（bridge 下发）
│                                                      │
│  ┌────────────────────────────────────────────────┐ │
│  │ [BOOT] React 视图挂载完成                       │ │
│  │ [BRIDGE] 桥接握手完成，版本 1.2.0               │ │ ← 日志面板（常驻 + 自动滚动）
│  │ [INFO] 正在加载 DI 容器...                      │ │     tag 染色
│  │ [OK] 42/42 注册完成                             │ │     保留最近 200 条
│  │ ...                                             │ │
│  └────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────┘
```

### 横条进度条设计

- 尺寸：280px × 6px（圆角 3px）
- 背景：`rgba(93,200,232,0.15)`
- 填充：`linear-gradient(90deg, var(--startup-primary), var(--startup-primary-deep))`
- 缓动：`transition: width 400ms cubic-bezier(0.4, 0, 0.2, 1)`
- 成功态：填充颜色变 `#34d399`，条上方弹出 ✓ 对勾（2s 停留 → 淡出）

### 完成态庆祝动画

1. 进度条颜色 `#5DC8E8` → `#34d399`（300ms）
2. 对勾 ✓ 从 `scale(0)` → `scale(1)`（400ms cubic-bezier(0.34, 1.56, 0.64, 1) 弹性）
3. 状态文字变为 "初始化完成 ✓"
4. **停留 2 秒** → 整个页面 opacity 从 1 → 0（500ms cubic-bezier(0.4, 0, 1, 1)）→ 切入主窗口

### 失败态急救箱

触发 `startup:failed` 事件时：

1. 日志面板自动展开（本来就常驻，滚到底部）
2. **关键错误高亮卡片**：过滤最近 50 条中 type=error 的，红色边框卡片展示
3. **急救按钮组**：
   - 🔄 重新启动（发送 `startup:restart` 事件）
   - 🩺 打开疑难解答（发送 `startup:openTroubleshooting` 事件，需要新增 bridge handler）
   - 📋 复制日志（`navigator.clipboard.writeText` 拼接最近 200 条）

---

## 3. 代码架构

### 文件拆分

```
src/frontend/src/
├── pages/
│   └── StartupPage.tsx              ← 主壳，只组合子组件（< 200 行）
│
├── features/startup/                ← 新建启动页专属目录
│   ├── StartupBackground.tsx        ← 极简径向渐变 + 呼吸光晕
│   ├── StartupProgressBar.tsx       ← 横条进度条 + 成功/失败态
│   ├── StartupLogPanel.tsx          ← 日志面板 + 自动滚动 + tag 染色
│   ├── StartupHud.tsx               ← 顶部 HUD（session + phase + 真实 FPS）
│   ├── StartupLogo.tsx              ← ASCII LOGO（单独组件，用户可替换）
│   ├── StartupErrorKit.tsx          ← 失败态急救箱（错误卡片 + 按钮组）
│   ├── useStartupBridge.ts          ← bridge 事件订阅 hook（纯逻辑，可单测）
│   ├── useFpsMonitor.ts             ← 真实 FPS 监测 hook（保留，零额外开销）
│   ├── bootState.ts                 ← phase 状态机类型 + 转换逻辑
│   └── keyframes.ts                 ← 剩余 keyframes 抽离（仅 breathe + successPop）
│
└── types/
    └── bridge.ts                    ← 已有，新增 StartupEvent 类型
```

### State 收敛

**旧 StartupPage.tsx（14 个 useState）**：
```tsx
const [progress, setProgress]
const [currentStatus, setCurrentStatus]
const [logs, setLogs]
const [isFailed, setIsCompleted, setIsFailed]
const [version, setPrimaryColor]
const [bootDone, setPhase]
const [uptime, setFps, setCpu, setMem]   // ← 砍！CPU/MEM 是模拟数据
```

**新架构**：
```
StartupPage.tsx 主壳
├── useStartupBridge()          → { phase, progress, statusText, logs, version, primaryColor, fps }
│                                 bridge 事件订阅 + phase 状态机转换
│                                 内部保留 3 个 state: logs[], phase, fps
│
├── useStartupBridge 内部状态:
│   const [logs, setLogs]
│   const [phase, setPhase]          // 'boot' | 'running' | 'done' | 'failed'
│   const [fps, setFps]              // 真实 FPS（从 useFpsMonitor 来）
│
├── StartupErrorKit 本地状态:
│   const [isExpanded, setIsExpanded]
│
└── 其他组件：纯展示，零 state
```

**砍掉的 state**：
- `isCompleted` + `isFailed` → 合并到 `phase` 的枚举值
- `primaryColor` → 保留（bridge 下发主题色时会变）
- `bootDone` → 保留（用于控制背景动画停/动）
- `uptime` → 砍（启动阶段没有"uptime"概念）
- `cpu` + `mem` → **砍！模拟数据**
- `fps` → **保留真实监测**（`useFpsMonitor` 单独 hook）

### Bridge 契约（保持向后兼容）

**前端 → 后端（发送）**：
```
startup:ready            { ts: number }
startup:dragMove         {}
startup:close            {}
startup:shutdown         {}
startup:restart          {}                    ← 新增
startup:openTroubleshooting {}                 ← 新增
startup:copyLog          { text: string }     ← 可选，前端也能直接 copy
```

**后端 → 前端（接收）**：
```
startup:init             { version, primaryColor }
startup:progress         { percent, status }
startup:log              { message, isError, isSuccess }
startup:completed        { message }
startup:failed           { message }
startup:themeChanged     { primaryColor }
```

### useStartupBridge.ts 设计

```typescript
// 职责：订阅 WebView2 bridge 消息 → 解析 → 更新内部状态
// 不包含任何 UI 逻辑，可单元测试事件解析

export function useStartupBridge(): StartupBridgeState {
  const [phase, setPhase] = useState<BootPhase>('boot')
  const [progress, setProgress] = useState(0)
  const [statusText, setStatusText] = useState('正在初始化...')
  const [logs, setLogs] = useState<LogEntry[]>([])
  const [version, setVersion] = useState('v0.0.0')
  const [primaryColor, setPrimaryColor] = useState('#5DC8E8')

  useEffect(() => {
    const handler = (data: BridgeMessage) => {
      switch (data.action) {
        case 'startup:init':          handleInit(data.payload)
        case 'startup:progress':      handleProgress(data.payload)
        case 'startup:log':           handleLog(data.payload)
        case 'startup:completed':     handleCompleted(data.payload)
        case 'startup:failed':        handleFailed(data.payload)
        case 'startup:themeChanged':  handleThemeChanged(data.payload)
      }
    }
    window.chrome?.webview?.addEventListener('message', handler)
    window.__msmc_bridge__?.sendEvent('startup:ready', { ts: Date.now() })
    return () => window.chrome?.webview?.removeEventListener('message', handler)
  }, [])

  // ... 各 handler 实现
}
```

---

## 4. 性能预算

| 资源 | 旧 | 新 | 改善 |
|------|-----|-----|------|
| 绝对定位 DOM 层 | 7 层（粒子+网格+矩阵雨+CRT+扫描条+暗角+HUD角标） | 1 层（径向渐变+呼吸光晕） | -6 层 |
| JS 每帧计算 | 粒子连线 + RAF FPS + CPU/MEM 模拟 | RAF FPS（仅 1 处） | -2 处 |
| CSS keyframes 数量 | ~10 个（cyberGridMove/cyberScan/cyberScanV/cyberVignette/cyberHudCorner/cyberBoot/cyberRingRotate + 动画） | 2 个（breathe + successPop） | -8 个 |
| useState 数量 | 14 | 6 | -8 |
| 文件行数 | 1211（单文件） | < 150（主壳）+ ~600（拆分子组件） | 可维护性↑ |

---

## 5. 约束与不变量

1. **Bridge 契约不变量** — `startup:ready` / `startup:progress` / `startup:log` / `startup:completed` / `startup:failed` / `startup:init` 这 6 个已有事件的 payload 格式**不得修改**，否则后端会崩
2. **文件尺寸不变量** — 单个文件 < 300 行，超过立即拆
3. **零运行时新依赖** — 不引入新的 npm 包
4. **后端改动范围严格受控** — StartupWindow.xaml.cs 只新增 3 个 bridge handler（`startup:restart` / `startup:openTroubleshooting` / `startup:copyLog`），不改动既有 WebView2 初始化 / 桥接脚本注入 / 事件队列分发逻辑
5. **ASCII LOGO 保留** — 用户单独安排替换内容

---

## 6. 验收清单

- [ ] 前端 `npm run build` 通过
- [ ] 后端 `dotnet build` 通过（不因为前端变更崩）
- [ ] 启动进度条随 bridge 事件 0% → 100% 正确推进
- [ ] 成功态：进度条变绿 + 对勾 + 停留 2s + 淡出
- [ ] 失败态：错误卡片出现 + 急救按钮可点（重开/复制日志）
- [ ] 日志面板自动滚动（用户滚到底部后新日志自动下滚，用户手动上滚时停止自动滚动）
- [ ] 真实 FPS 监测正常显示
- [ ] 背景层 < 2 个 DOM 节点，无粒子/CRT/扫描线
- [ ] 单个组件文件 < 300 行
- [ ] 集成显卡机器（Intel UHD 620 等）WebView2 FPS ≥ 55（无明显掉帧）

---

## 7. 待确认事项（P0 blocker）

| 项 | 候选 | 决策 |
|----|------|------|
| 主色 hex | `#5DC8E8` / `#4A90D9` / 血红 `#c0392b` / 跟随主题 | 待用户拍板 |
| ASCII LOGO 内容 | 保留当前 / 用户提供新的 | 用户单独安排 |
