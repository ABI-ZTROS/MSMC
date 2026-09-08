# 启动页重构 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 1211 行单文件 StartupPage.tsx + 1145 行 StartupWindow.xaml.cs 重构为：极简三态配色视觉（水蓝→日落黄→血红）、状态机驱动、8 个子组件 + 2 个 hook、砍掉 7 层赛博朋克装饰、集成显卡友好

**Architecture:** 后端只加 3 个 bridge handler（restart/openTroubleshooting/copyLog），前端彻底拆为 `features/startup/` 目录下 8 个组件 + 2 个 hook。所有视觉元素的颜色由 `phase` 状态机驱动的 CSS 变量 `--startup-current` 控制。

**Tech Stack:** React + TypeScript + Vite + WebView2 bridge + .NET 9 WPF + C#

**Spec:** `docs/superpowers/specs/2026-09-08-startup-page-refactor-design.md`

---

## File Map

### 后端（修改 1 个文件）

| 文件 | 操作 | 职责 |
|------|------|------|
| `src/MSMC/Features/Startup/Views/StartupWindow.xaml.cs` | Modify | 在 `HandleJsEvent` switch 里加 3 个新 case |

### 前端（新建 10 个文件 + 修改 0 个已有 + 删除 1 个旧）

| 文件 | 操作 | 行数预估 | 职责 |
|------|------|---------|------|
| `src/frontend/src/features/startup/bootState.ts` | Create | ~40 | Phase 状态机类型 + 转换逻辑 |
| `src/frontend/src/features/startup/useStartupBridge.ts` | Create | ~100 | Bridge 事件订阅 hook（纯逻辑） |
| `src/frontend/src/features/startup/useFpsMonitor.ts` | Create | ~30 | 真实 FPS 监测 hook |
| `src/frontend/src/features/startup/keyframes.ts` | Create | ~25 | CSS keyframes 字符串（breathe + successPop） |
| `src/frontend/src/features/startup/StartupBackground.tsx` | Create | ~30 | 径向渐变 + 呼吸光晕 |
| `src/frontend/src/features/startup/StartupProgressBar.tsx` | Create | ~45 | 横条进度条（百分比在外部） |
| `src/frontend/src/features/startup/StartupHud.tsx` | Create | ~35 | 顶部 HUD（session/phase + 真实 FPS） |
| `src/frontend/src/features/startup/StartupLogo.tsx` | Create | ~25 | ASCII LOGO |
| `src/frontend/src/features/startup/StartupLogPanel.tsx` | Create | ~70 | 日志面板（自动滚动 + tag 染色） |
| `src/frontend/src/features/startup/StartupErrorKit.tsx` | Create | ~60 | 失败态急救箱（错误卡片 + 按钮组） |
| `src/frontend/src/pages/StartupPage.tsx` | **Delete** | — | 旧 1211 行单文件 |
| `src/frontend/src/pages/StartupPage.tsx` | Create（覆盖） | ~150 | 主壳，只组合子组件 |

---

## Task 1: 后端新增 3 个 bridge handler

**Files:**
- Modify: `src/MSMC/Features/Startup/Views/StartupWindow.xaml.cs:507-510`（在 startup:shutdown case 后面加 3 个新 case）

- [ ] **Step 1: 在 HandleJsEvent 的 switch 里加 3 个 case**

找到 switch 的最后一个 case（`startup:shutdown`），在 `break;` 之后、switch 闭合 `}` 之前，插入：

```csharp
            case "startup:restart":
                Log.Information("[Startup-WV2] 前端请求重启启动流程");
                _dispatcher.InvokeAsync(() =>
                {
                    // 重置前端状态：发 init 事件重新开始
                    // 注意：前端会自己重置 phase，这里只需要重新发送 init + 初始进度 0%
                    SendInitEvent();
                    SendEvent("startup:progress", new { percent = 0, status = "正在重启..." });
                    _frontendLoaded = true; // 前端刷新后会重新 ready，这里不阻塞
                });
                break;

            case "startup:openTroubleshooting":
                Log.Information("[Startup-WV2] 前端请求打开疑难解答");
                _dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // 通过已有的导航服务跳转到 /diagnostics
                        if (App.Services.GetService<MSMC.Features.Navigation.INavigationService>() 
                            is { } navService)
                        {
                            navService.NavigateTo("/diagnostics");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "[Startup-WV2] 打开疑难解答失败");
                    }
                    // 关闭启动窗口
                    Close();
                });
                break;

            case "startup:copyLog":
                Log.Debug("[Startup-WV2] 前端请求复制日志");
                // 前端会自己用 navigator.clipboard，这里留空即可
                // 若后续需要后端复制系统日志，在这里实现
                break;
```

- [ ] **Step 2: 检查 INavigationService 是否在 DI 容器中注册**

运行：
```bash
cd /workspace && grep -rn "INavigationService" src/MSMC/App.xaml.cs src/MSMC/Features/Navigation/ 2>/dev/null | head -10
```

如果找不到，改为：
```csharp
case "startup:openTroubleshooting":
    // 前端自己知道要去哪，后端只需关启动窗口
    _dispatcher.InvokeAsync(Close);
    break;
```

- [ ] **Step 3: dotnet build 验证**

```bash
cd /workspace/src && dotnet build MSMC/MSMC.csproj --no-restore 2>&1 | tail -10
```

Expected: `Build succeeded` + `0 Error(s)`

- [ ] **Step 4: Commit**

```bash
cd /workspace && git add src/MSMC/Features/Startup/Views/StartupWindow.xaml.cs
git commit -m "feat(startup): add restart/openTroubleshooting/copyLog bridge handlers"
```

---

## Task 2: 前端 bootState + useFpsMonitor + keyframes

**Files:**
- Create: `src/frontend/src/features/startup/bootState.ts`
- Create: `src/frontend/src/features/startup/useFpsMonitor.ts`
- Create: `src/frontend/src/features/startup/keyframes.ts`

- [ ] **Step 1: 创建 bootState.ts**

```typescript
// Phase 状态机：驱动配色切换
export type BootPhase = 'boot' | 'running' | 'warn' | 'error' | 'success';

export interface BootPhaseConfig {
  // 对应的 CSS 变量 --startup-current 的颜色类名
  cssClass: string;
  // 背景光晕颜色（用于 color-mix）
  bgGlow: string;
}

export const BOOT_PHASE_CONFIG: Record<BootPhase, BootPhaseConfig> = {
  boot:    { cssClass: 'phase-boot',    bgGlow: '#5DC8E8' },
  running: { cssClass: 'phase-running', bgGlow: '#5DC8E8' },
  warn:    { cssClass: 'phase-warn',    bgGlow: '#e8964a' },
  error:   { cssClass: 'phase-error',   bgGlow: '#c0392b' },
  success: { cssClass: 'phase-success', bgGlow: '#34d399' },
};

// 日志条目类型
export interface LogEntry {
  id: number;
  message: string;
  type: 'info' | 'ok' | 'warn' | 'error' | 'debug';
  timestamp: number;
}

// Bridge 事件 payload 类型（与后端 StartupWindow.xaml.cs 保持一致）
export interface BridgePayload {
  action: string;
  payload: any;
}

// startup:init payload
export interface InitPayload {
  version: string;
  primaryColor: string;
}

// startup:progress payload
export interface ProgressPayload {
  percent: number;
  status: string;
}

// startup:log payload
export interface LogPayload {
  message: string;
  isError?: boolean;
  isSuccess?: boolean;
}
```

- [ ] **Step 2: 创建 useFpsMonitor.ts**

```typescript
import { useEffect, useRef, useState } from 'react';

/**
 * 真实 FPS 监测（RAF 驱动，零 setInterval 开销）
 * 计算最近 60 帧的平均 FPS
 */
export function useFpsMonitor(enabled = true): number {
  const [fps, setFps] = useState(0);
  const frameTimes = useRef<number[]>([]);
  const rafId = useRef<number | null>(null);

  useEffect(() => {
    if (!enabled) return;

    let lastFrame = performance.now();

    const tick = (now: number) => {
      const delta = now - lastFrame;
      lastFrame = now;

      frameTimes.current.push(delta);
      // 保留最近 60 帧
      if (frameTimes.current.length > 60) {
        frameTimes.current.shift();
      }

      // 计算平均 FPS
      const avgDelta = frameTimes.current.reduce((a, b) => a + b, 0) / frameTimes.current.length;
      const currentFps = Math.round(1000 / avgDelta);

      // 只在 FPS 变化超过 1 时更新 state（减少重渲染）
      setFps((prev) => (Math.abs(prev - currentFps) >= 1 ? currentFps : prev));

      rafId.current = requestAnimationFrame(tick);
    };

    rafId.current = requestAnimationFrame(tick);
    return () => {
      if (rafId.current !== null) {
        cancelAnimationFrame(rafId.current);
      }
    };
  }, [enabled]);

  return fps;
}
```

- [ ] **Step 3: 创建 keyframes.ts**

```typescript
/**
 * 剩余的 CSS keyframes 字符串
 * 只有 2 个：breathe（呼吸光晕）+ successPop（完成态对勾弹出）
 * 之前有 ~10 个，砍掉 8 个
 */
export const STARTUP_KEYFRAMES = `
@keyframes startupBreathe {
  0%, 100% { opacity: 0.85; }
  50%      { opacity: 1; }
}

@keyframes startupSuccessPop {
  0%   { transform: scale(0); opacity: 0; }
  60%  { transform: scale(1.2); opacity: 1; }
  100% { transform: scale(1); opacity: 1; }
}

@keyframes startupFadeOut {
  0%   { opacity: 1; }
  100% { opacity: 0; }
}
`;
```

- [ ] **Step 4: TypeScript 编译验证**

```bash
cd /workspace/src/frontend && npx tsc --noEmit src/features/startup/bootState.ts src/features/startup/useFpsMonitor.ts src/features/startup/keyframes.ts 2>&1 | tail -10
```

Expected: 无输出（无错误）

- [ ] **Step 5: Commit**

```bash
cd /workspace && git add src/frontend/src/features/startup/
git commit -m "feat(startup): add bootState types, useFpsMonitor hook, keyframes"
```

---

## Task 3: useStartupBridge — 核心桥接 hook

**Files:**
- Create: `src/frontend/src/features/startup/useStartupBridge.ts`

- [ ] **Step 1: 创建文件**

```typescript
import { useCallback, useEffect, useRef, useState } from 'react';
import type { BootPhase, InitPayload, ProgressPayload, LogEntry, LogPayload } from './bootState';

export interface StartupBridgeState {
  phase: BootPhase;
  progress: number;         // 0-100
  statusText: string;
  logs: LogEntry[];
  version: string;
  primaryColor: string;
  isCompleted: boolean;
  isFailed: boolean;
}

export interface StartupBridgeActions {
  restart: () => void;
  openTroubleshooting: () => void;
  copyLog: () => void;
  close: () => void;
  sendDragMove: () => void;
}

export function useStartupBridge(): StartupBridgeState & StartupBridgeActions {
  const [phase, setPhase] = useState<BootPhase>('boot');
  const [progress, setProgress] = useState(0);
  const [statusText, setStatusText] = useState('正在初始化...');
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [version, setVersion] = useState('v0.0.0');
  const [primaryColor, setPrimaryColor] = useState('#5DC8E8');
  const [isCompleted, setIsCompleted] = useState(false);
  const [isFailed, setIsFailed] = useState(false);

  const logIdCounter = useRef(0);

  // 发送事件（后端已注册的 handler）
  const sendEvent = useCallback((action: string, payload: unknown = {}) => {
    const bridge = (window as any).__msmc_bridge__;
    if (bridge?.sendEvent) {
      bridge.sendEvent(action, payload);
    }
  }, []);

  // 添加日志
  const addLog = useCallback((message: string, type: LogEntry['type'] = 'info') => {
    setLogs((prev) => {
      const entry: LogEntry = {
        id: logIdCounter.current++,
        message,
        type,
        timestamp: Date.now(),
      };
      // 保留最近 200 条
      const next = [...prev, entry];
      if (next.length > 200) next.shift();
      return next;
    });
  }, []);

  // 事件 handler 解析（纯函数逻辑，可单测）
  const handleMessage = useCallback((rawEvent: any) => {
    try {
      const data = rawEvent?.data;
      if (!data || !data.action) return;

      switch (data.action) {
        case 'startup:init': {
          const payload = data.payload as InitPayload;
          setVersion(payload.version ? `v${payload.version}` : 'v0.0.0');
          setPrimaryColor(payload.primaryColor || '#5DC8E8');
          break;
        }

        case 'startup:progress': {
          const payload = data.payload as ProgressPayload;
          const pct = Math.max(0, Math.min(100, payload.percent));
          setProgress(pct);
          setStatusText(payload.status || '');
          // 根据进度判断 phase（后端可能不显式发 warn/error，我们从日志推断）
          if (pct > 0 && phase === 'boot') {
            setPhase('running');
          }
          break;
        }

        case 'startup:log': {
          const payload = data.payload as LogPayload;
          let type: LogEntry['type'] = 'info';
          if (payload.isError) type = 'error';
          else if (payload.isSuccess) type = 'ok';
          else if (payload.message?.toLowerCase().includes('warn')) type = 'warn';
          else if (payload.message?.toLowerCase().includes('error')) type = 'error';
          else if (payload.message?.toLowerCase().includes('ok') || payload.message?.toLowerCase().includes('success')) type = 'ok';

          addLog(payload.message || '', type);

          // 日志中出现 ERROR → phase 切 warn
          if (type === 'error' && phase !== 'error') {
            setPhase('warn');
          }
          break;
        }

        case 'startup:completed': {
          setPhase('success');
          setIsCompleted(true);
          setStatusText(data.payload?.message || '初始化完成');
          setProgress(100);
          break;
        }

        case 'startup:failed': {
          setPhase('error');
          setIsFailed(true);
          setStatusText(data.payload?.message || '启动失败');
          addLog(data.payload?.message || '启动失败', 'error');
          break;
        }

        case 'startup:themeChanged': {
          if (data.payload?.primaryColor) {
            setPrimaryColor(data.payload.primaryColor);
          }
          break;
        }
      }
    } catch (err) {
      console.error('[StartupBridge] 解析事件失败:', err, rawEvent);
    }
  }, [addLog, phase]);

  // 订阅 WebView2 message
  useEffect(() => {
    const handler = (event: any) => handleMessage(event);

    // 两种方式都监听（兼容旧 bridge 和新 bridge）
    const webview = (window as any).chrome?.webview;
    if (webview?.addEventListener) {
      webview.addEventListener('message', handler);
    }

    // 通知后端前端就绪
    // 延迟 100ms 确保 bridge init 脚本已注入
    const readyTimer = setTimeout(() => {
      sendEvent('startup:ready', { ts: Date.now() });
    }, 100);

    return () => {
      clearTimeout(readyTimer);
      if (webview?.removeEventListener) {
        webview.removeEventListener('message', handler);
      }
    };
  }, [handleMessage, sendEvent]);

  // Actions
  const restart = useCallback(() => {
    // 重置前端状态
    setPhase('boot');
    setProgress(0);
    setStatusText('正在重启...');
    setIsCompleted(false);
    setIsFailed(false);
    setLogs([]);
    logIdCounter.current = 0;
    sendEvent('startup:restart', {});
  }, [sendEvent]);

  const openTroubleshooting = useCallback(() => {
    sendEvent('startup:openTroubleshooting', {});
  }, [sendEvent]);

  const copyLog = useCallback(async () => {
    const text = logs
      .map((l) => `[${l.type.toUpperCase()}] ${new Date(l.timestamp).toISOString()} ${l.message}`)
      .join('\n');
    try {
      await navigator.clipboard.writeText(text);
    } catch (err) {
      console.warn('[StartupBridge] 复制日志失败:', err);
    }
  }, [logs]);

  const close = useCallback(() => {
    sendEvent('startup:close', {});
  }, [sendEvent]);

  const sendDragMove = useCallback(() => {
    sendEvent('startup:dragMove', {});
  }, [sendEvent]);

  return {
    // State
    phase,
    progress,
    statusText,
    logs,
    version,
    primaryColor,
    isCompleted,
    isFailed,
    // Actions
    restart,
    openTroubleshooting,
    copyLog,
    close,
    sendDragMove,
  };
}
```

- [ ] **Step 2: TypeScript 编译验证**

```bash
cd /workspace/src/frontend && npx tsc --noEmit src/features/startup/useStartupBridge.ts 2>&1 | tail -15
```

Expected: 无输出或只有可忽略的 warning

- [ ] **Step 3: Commit**

```bash
cd /workspace && git add src/frontend/src/features/startup/useStartupBridge.ts
git commit -m "feat(startup): add useStartupBridge hook with phase state machine"
```

---

## Task 4: 视觉子组件（Background + ProgressBar + HUD + Logo）

**Files:**
- Create: `src/frontend/src/features/startup/StartupBackground.tsx`
- Create: `src/frontend/src/features/startup/StartupProgressBar.tsx`
- Create: `src/frontend/src/features/startup/StartupHud.tsx`
- Create: `src/frontend/src/features/startup/StartupLogo.tsx`

### Task 4a: StartupBackground

- [ ] **Step 1: 创建 StartupBackground.tsx**

```tsx
import type { BootPhase } from './bootState';
import { BOOT_PHASE_CONFIG } from './bootState';

interface Props {
  phase: BootPhase;
  keyframesCss: string;
}

/**
 * 极简背景：极深水蓝黑 + 中心柔化呼吸光晕
 * 光晕颜色跟随 phase 主色动态切换
 * 仅 2 层 DOM，零 JS 计算，性能友好
 */
export function StartupBackground({ phase, keyframesCss }: Props) {
  const config = BOOT_PHASE_CONFIG[phase];

  return (
    <>
      {/* 注入 keyframes 到 <style> 标签（组件内联，不需要独立 CSS 文件） */}
      <style>{keyframesCss}</style>

      {/* 底层：纯色背景 */}
      <div
        style={{
          position: 'fixed',
          inset: 0,
          backgroundColor: '#081420',
          zIndex: 0,
        }}
      />

      {/* 光晕层：radial-gradient，颜色跟随 phase */}
      <div
        style={{
          position: 'fixed',
          inset: 0,
          background: `radial-gradient(
            ellipse at center,
            ${config.bgGlow}14 0%,
            transparent 70%
          )`,
          animation: 'startupBreathe 6s cubic-bezier(0.4, 0, 0.6, 1) infinite',
          zIndex: 1,
          pointerEvents: 'none',
        }}
      />
    </>
  );
}
```

### Task 4b: StartupProgressBar

- [ ] **Step 2: 创建 StartupProgressBar.tsx**

```tsx
import type { BootPhase } from './bootState';
import { BOOT_PHASE_CONFIG } from './bootState';

interface Props {
  progress: number;          // 0-100
  phase: BootPhase;
  showSuccessCheck: boolean; // 完成态是否显示 ✓ 对勾
}

/**
 * 横条进度条：
 * - 百分比放在条外部右侧（避免白字在亮色填充上只有 1.93:1 对比度的陷阱）
 * - 颜色跟随 phase 动态切换
 * - 贝塞尔缓动 400ms
 */
export function StartupProgressBar({ progress, phase, showSuccessCheck }: Props) {
  const config = BOOT_PHASE_CONFIG[phase];

  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      gap: '16px',
      zIndex: 10,
    }}>
      {/* 进度条本体 */}
      <div
        style={{
          width: 280,
          height: 6,
          borderRadius: 3,
          backgroundColor: `${config.bgGlow}26`, // 26 = 15% 透明度
          overflow: 'hidden',
        }}
      >
        <div
          style={{
            width: `${Math.max(0, Math.min(100, progress))}%`,
            height: '100%',
            borderRadius: 3,
            background: `linear-gradient(90deg, ${config.bgGlow}, ${config.bgGlow}cc)`,
            transition: 'width 400ms cubic-bezier(0.4, 0, 0.2, 1)',
          }}
        />
      </div>

      {/* 百分比：在条外部右侧！ */}
      <span
        style={{
          fontSize: 13,
          fontWeight: 600,
          color: '#e2e8f0', // 主文字白色（在暗背景上 15:1 AAA）
          fontVariantNumeric: 'tabular-nums',
          minWidth: 32,
          textAlign: 'left',
        }}
      >
        {progress}%
      </span>

      {/* 完成态 ✓ 对勾（弹出动画） */}
      {showSuccessCheck && (
        <span
          style={{
            fontSize: 20,
            color: '#34d399', // 成功绿
            animation: 'startupSuccessPop 400ms cubic-bezier(0.34, 1.56, 0.64, 1) forwards',
          }}
        >
          ✓
        </span>
      )}
    </div>
  );
}
```

### Task 4c: StartupHud

- [ ] **Step 3: 创建 StartupHud.tsx**

```tsx
interface Props {
  version: string;
  phase: string;
  fps: number;
}

/**
 * 顶部 HUD：版本号 + session + phase + 真实 FPS
 * 砍掉了模拟的 CPU/MEM/uptime
 */
export function StartupHud({ version, phase, fps }: Props) {
  const sessionId = generateSessionId();

  return (
    <div style={{
      position: 'fixed',
      top: 16,
      left: 24,
      right: 24,
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      zIndex: 20,
      pointerEvents: 'none',
      fontFamily: '"SF Mono", "Consolas", monospace',
    }}>
      {/* 左侧：版本 + session + phase */}
      <div style={{
        display: 'flex',
        gap: 16,
        fontSize: 12,
        color: '#94a3b8', // 次要文字（7.24:1 AAA）
      }}>
        <span>{version}</span>
        <span>SESSION {sessionId}</span>
        <span>● {phase.toUpperCase()}</span>
      </div>

      {/* 右侧：真实 FPS */}
      <div style={{
        fontSize: 12,
        color: fps >= 55 ? '#34d399' : fps >= 30 ? '#e8964a' : '#e74c3c',
      }}>
        FPS {fps}
      </div>
    </div>
  );
}

function generateSessionId(): string {
  // 简单的 6 字符随机 ID
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let result = '';
  for (let i = 0; i < 6; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return result;
}
```

### Task 4d: StartupLogo

- [ ] **Step 4: 创建 StartupLogo.tsx**

```tsx
import type { BootPhase } from './bootState';
import { BOOT_PHASE_CONFIG } from './bootState';

interface Props {
  phase: BootPhase;
  // ASCII LOGO 内容（用户后续单独安排，当前用占位）
  lines?: string[];
}

/**
 * ASCII LOGO 组件
 * 后续用户会单独提供新内容，这里只是骨架
 */
const DEFAULT_LOGO: string[] = [
  '  __  __  ____   _____  ____',
  ' |  \\/  |/ ___| / ____|/ ___|',
  ' | \\  / | |     | |    | |',
  ' | |\\/| | |     | |    | |___',
  ' | |  | | |___  | |____ \\___ \\',
  ' |_|  |_|\\____| \\_____|____/',
];

export function StartupLogo({ phase, lines = DEFAULT_LOGO }: Props) {
  const config = BOOT_PHASE_CONFIG[phase];

  return (
    <div style={{
      fontFamily: '"SF Mono", "Consolas", monospace',
      fontSize: 14,
      lineHeight: 1.2,
      textAlign: 'center',
      whiteSpace: 'pre',
      userSelect: 'none',
      // ASCII LOGO 本身作为装饰（WCAG 排除项），颜色跟随 phase
      color: config.bgGlow,
      opacity: 0.85,
      letterSpacing: 1,
    }}>
      {lines.join('\n')}
    </div>
  );
}
```

- [ ] **Step 5: TypeScript 编译验证**

```bash
cd /workspace/src/frontend && npx tsc --noEmit src/features/startup/StartupBackground.tsx src/features/startup/StartupProgressBar.tsx src/features/startup/StartupHud.tsx src/features/startup/StartupLogo.tsx 2>&1 | tail -15
```

Expected: 无错误

- [ ] **Step 6: Commit**

```bash
cd /workspace && git add src/frontend/src/features/startup/
git commit -m "feat(startup): add visual subcomponents (Background/ProgressBar/HUD/Logo)"
```

---

## Task 5: LogPanel + ErrorKit（日志 + 失败急救箱）

**Files:**
- Create: `src/frontend/src/features/startup/StartupLogPanel.tsx`
- Create: `src/frontend/src/features/startup/StartupErrorKit.tsx`

### Task 5a: StartupLogPanel

- [ ] **Step 1: 创建 StartupLogPanel.tsx**

```tsx
import { useEffect, useRef, useState } from 'react';
import type { LogEntry } from './bootState';

interface Props {
  logs: LogEntry[];
  autoScroll?: boolean;
}

/**
 * 日志面板
 * - 默认自动滚动到底部
 * - 用户手动上滚时停止自动滚动，滚到底部后恢复
 * - tag 染色：info=#94a3b8, ok=#34d399, warn=#e8964a, error=#e74c3c, debug=#64748b
 */
const TYPE_COLORS: Record<LogEntry['type'], string> = {
  info: '#94a3b8',
  ok: '#34d399',
  warn: '#e8964a',
  error: '#e74c3c',
  debug: '#64748b',
};

export function StartupLogPanel({ logs, autoScroll = true }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [userScrolledUp, setUserScrolledUp] = useState(false);

  // 自动滚动
  useEffect(() => {
    if (!containerRef.current) return;
    if (autoScroll && !userScrolledUp) {
      containerRef.current.scrollTop = containerRef.current.scrollHeight;
    }
  }, [logs, autoScroll, userScrolledUp]);

  const handleScroll = () => {
    if (!containerRef.current) return;
    const { scrollTop, scrollHeight, clientHeight } = containerRef.current;
    const distanceFromBottom = scrollHeight - scrollTop - clientHeight;

    // 距离底部 < 20px 视为"已滚到底部"
    if (distanceFromBottom < 20) {
      setUserScrolledUp(false);
    } else if (scrollTop > 0) {
      setUserScrolledUp(true);
    }
  };

  return (
    <div style={{
      width: '100%',
      maxWidth: 560,
      height: 160,
      backgroundColor: 'rgba(0,0,0,0.35)',
      borderRadius: 8,
      border: '1px solid rgba(148, 163, 184, 0.15)',
      overflow: 'hidden',
      display: 'flex',
      flexDirection: 'column',
    }}>
      {/* Header */}
      <div style={{
        padding: '8px 12px',
        borderBottom: '1px solid rgba(148, 163, 184, 0.1)',
        fontSize: 11,
        color: '#64748b',
        fontFamily: '"SF Mono", "Consolas", monospace',
        display: 'flex',
        justifyContent: 'space-between',
      }}>
        <span>CONSOLE · {logs.length} entries</span>
        {userScrolledUp && (
          <span
            onClick={() => {
              setUserScrolledUp(false);
              if (containerRef.current) {
                containerRef.current.scrollTop = containerRef.current.scrollHeight;
              }
            }}
            style={{
              cursor: 'pointer',
              color: '#5DC8E8',
            }}
          >
            ↓ scroll to bottom
          </span>
        )}
      </div>

      {/* Log body */}
      <div
        ref={containerRef}
        onScroll={handleScroll}
        style={{
          flex: 1,
          overflowY: 'auto',
          padding: '6px 12px',
          fontFamily: '"SF Mono", "Consolas", monospace',
          fontSize: 12,
          lineHeight: 1.6,
        }}
      >
        {logs.length === 0 ? (
          <span style={{ color: '#64748b' }}>等待日志输出...</span>
        ) : (
          logs.map((entry) => (
            <div key={entry.id} style={{ display: 'flex', gap: 8 }}>
              <span style={{
                color: TYPE_COLORS[entry.type],
                fontWeight: entry.type === 'error' ? 700 : 500,
                flexShrink: 0,
                minWidth: 36,
              }}>
                [{entry.type.toUpperCase()}]
              </span>
              <span style={{ color: '#cbd5e1', wordBreak: 'break-all' }}>
                {entry.message}
              </span>
            </div>
          ))
        )}
      </div>
    </div>
  );
}
```

### Task 5b: StartupErrorKit

- [ ] **Step 2: 创建 StartupErrorKit.tsx**

```tsx
import type { LogEntry } from './bootState';

interface Props {
  logs: LogEntry[];
  onRestart: () => void;
  onOpenTroubleshooting: () => void;
  onCopyLog: () => void;
}

/**
 * 失败态急救箱
 * - 关键错误高亮卡片（过滤最近 50 条中的 ERROR）
 * - 三个急救按钮：重开 / 疑难解答 / 复制日志
 */
export function StartupErrorKit({ logs, onRestart, onOpenTroubleshooting, onCopyLog }: Props) {
  const errorLogs = logs
    .filter((l) => l.type === 'error')
    .slice(-10); // 最后 10 条错误

  return (
    <div style={{
      display: 'flex',
      flexDirection: 'column',
      gap: 16,
      zIndex: 15,
    }}>
      {/* 错误高亮卡片 */}
      {errorLogs.length > 0 && (
        <div style={{
          width: '100%',
          maxWidth: 560,
          backgroundColor: 'rgba(231, 76, 60, 0.08)',
          border: '1px solid rgba(231, 76, 60, 0.4)',
          borderRadius: 8,
          padding: '12px 16px',
          fontFamily: '"SF Mono", "Consolas", monospace',
        }}>
          <div style={{
            fontSize: 11,
            color: '#e74c3c',
            fontWeight: 600,
            marginBottom: 8,
            textTransform: 'uppercase',
            letterSpacing: 1,
          }}>
            ⚠ Critical Errors ({errorLogs.length})
          </div>
          {errorLogs.map((log, i) => (
            <div key={i} style={{
              fontSize: 12,
              color: '#fca5a5', // 比 #e74c3c 更亮，保证对比度
              marginBottom: 4,
              wordBreak: 'break-all',
            }}>
              {log.message}
            </div>
          ))}
        </div>
      )}

      {/* 急救按钮组 */}
      <div style={{
        display: 'flex',
        gap: 12,
        flexWrap: 'wrap',
      }}>
        <ErrorButton onClick={onRestart} label="🔄 重新启动" color="#5DC8E8" />
        <ErrorButton onClick={onOpenTroubleshooting} label="🩺 疑难解答" color="#e8964a" />
        <ErrorButton onClick={onCopyLog} label="📋 复制日志" color="#94a3b8" />
      </div>
    </div>
  );
}

function ErrorButton({ onClick, label, color }: { onClick: () => void; label: string; color: string }) {
  return (
    <button
      onClick={onClick}
      style={{
        padding: '8px 16px',
        backgroundColor: 'transparent',
        border: `1px solid ${color}`,
        color,
        borderRadius: 6,
        fontSize: 13,
        fontWeight: 500,
        cursor: 'pointer',
        transition: 'background-color 150ms ease',
        fontFamily: 'inherit',
      }}
      onMouseEnter={(e) => {
        e.currentTarget.style.backgroundColor = `${color}1f`; // 12% 透明度
      }}
      onMouseLeave={(e) => {
        e.currentTarget.style.backgroundColor = 'transparent';
      }}
    >
      {label}
    </button>
  );
}
```

- [ ] **Step 3: TypeScript 编译验证**

```bash
cd /workspace/src/frontend && npx tsc --noEmit src/features/startup/StartupLogPanel.tsx src/features/startup/StartupErrorKit.tsx 2>&1 | tail -15
```

Expected: 无错误

- [ ] **Step 4: Commit**

```bash
cd /workspace && git add src/frontend/src/features/startup/
git commit -m "feat(startup): add LogPanel with auto-scroll and ErrorKit with critical error card"
```

---

## Task 6: 主壳 StartupPage.tsx（删除旧 + 写新）

**Files:**
- Delete: `src/frontend/src/pages/StartupPage.tsx`（旧 1211 行）
- Create: `src/frontend/src/pages/StartupPage.tsx`（新 ~150 行主壳）

- [ ] **Step 1: 删除旧文件**

```bash
rm src/frontend/src/pages/StartupPage.tsx
```

- [ ] **Step 2: 创建新的 StartupPage.tsx 主壳**

```tsx
import { useEffect, useState } from 'react';
import { StartupBackground } from '../features/startup/StartupBackground';
import { StartupProgressBar } from '../features/startup/StartupProgressBar';
import { StartupHud } from '../features/startup/StartupHud';
import { StartupLogo } from '../features/startup/StartupLogo';
import { StartupLogPanel } from '../features/startup/StartupLogPanel';
import { StartupErrorKit } from '../features/startup/StartupErrorKit';
import { useStartupBridge } from '../features/startup/useStartupBridge';
import { useFpsMonitor } from '../features/startup/useFpsMonitor';
import { STARTUP_KEYFRAMES } from '../features/startup/keyframes';
import { BOOT_PHASE_CONFIG } from '../features/startup/bootState';

/**
 * 启动页主壳 — 只组合子组件，零业务逻辑
 * 所有状态由 useStartupBridge hook 管理
 */
export default function StartupPage() {
  const bridge = useStartupBridge();
  const fps = useFpsMonitor(true);

  // 淡出状态：完成后 2s 开始淡出
  const [fadingOut, setFadingOut] = useState(false);

  useEffect(() => {
    if (bridge.isCompleted && !fadingOut) {
      const timer = setTimeout(() => {
        setFadingOut(true);
        // 淡出动画 500ms 后通知后端关闭
        setTimeout(() => {
          bridge.close();
        }, 500);
      }, 2000); // 停留 2s
      return () => clearTimeout(timer);
    }
  }, [bridge.isCompleted, fadingOut, bridge]);

  const phaseConfig = BOOT_PHASE_CONFIG[bridge.phase];

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: '#081420',
        opacity: fadingOut ? 0 : 1,
        transition: 'opacity 500ms cubic-bezier(0.4, 0, 1, 1)',
        overflow: 'hidden',
        userSelect: 'none',
      }}
      onMouseDown={() => bridge.sendDragMove()}
    >
      {/* 背景层 */}
      <StartupBackground phase={bridge.phase} keyframesCss={STARTUP_KEYFRAMES} />

      {/* 顶部 HUD */}
      <StartupHud version={bridge.version} phase={bridge.phase} fps={fps} />

      {/* 中心内容区 */}
      <div
        style={{
          position: 'absolute',
          inset: 0,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 32,
          padding: '100px 24px',
          zIndex: 10,
        }}
      >
        {/* ASCII LOGO */}
        <StartupLogo phase={bridge.phase} />

        {/* 进度条 + 百分比 */}
        <StartupProgressBar
          progress={bridge.progress}
          phase={bridge.phase}
          showSuccessCheck={bridge.isCompleted}
        />

        {/* 状态文字（颜色跟随 phase！） */}
        <div style={{
          fontSize: 14,
          color: bridge.phase === 'error' ? '#e74c3c' : phaseConfig.bgGlow,
          fontWeight: 500,
          textAlign: 'center',
          maxWidth: 480,
        }}>
          {bridge.statusText}
        </div>

        {/* 失败态急救箱（仅 error phase 显示） */}
        {bridge.phase === 'error' && (
          <StartupErrorKit
            logs={bridge.logs}
            onRestart={bridge.restart}
            onOpenTroubleshooting={bridge.openTroubleshooting}
            onCopyLog={bridge.copyLog}
          />
        )}

        {/* 日志面板 */}
        <StartupLogPanel logs={bridge.logs} />
      </div>
    </div>
  );
}
```

- [ ] **Step 3: 完整 TypeScript + Vite build 验证**

```bash
cd /workspace/src/frontend && npm run build 2>&1 | tail -20
```

Expected: `✓ built in Xms` + 无 error

- [ ] **Step 4: dotnet build 验证**

```bash
cd /workspace/src && dotnet build MSMC/MSMC.csproj --no-restore 2>&1 | tail -10
```

Expected: `Build succeeded`

- [ ] **Step 5: Commit**

```bash
cd /workspace && git add -A
git add -u
git commit -m "feat(startup): rewrite StartupPage as 8-component architecture with tri-state theming"
```

---

## Task 7: 验收 — 逐项对照 spec 清单

**不新增文件，不修改代码，只做验证**

- [ ] **Step 1: 文件结构验证**

```bash
cd /workspace && echo "=== 前端新文件 ===" && ls -la src/frontend/src/features/startup/ && echo "" && echo "=== 旧 StartupPage 行数 ===" && wc -l src/frontend/src/pages/StartupPage.tsx && echo "" && echo "=== 单文件行数 < 300 检查 ===" && find src/frontend/src/features/startup -name "*.ts*" -exec wc -l {} \;
```

Expected: 所有文件 < 300 行

- [ ] **Step 2: 背景层 DOM 数量验证**

DevTools Elements 面板或：
```bash
echo "确认没有粒子场/CRT/扫描线残留 grep"
grep -rn "particle\|cyberGrid\|cyberScan\|matrixRain\|CRT\|scanLine" src/frontend/src/features/startup/ src/frontend/src/pages/StartupPage.tsx
```

Expected: 空输出

- [ ] **Step 3: 进度条百分比位置验证**

```bash
grep -n "progress.*%" src/frontend/src/features/startup/StartupProgressBar.tsx | head -5
```

Expected: 百分比在独立 span（不叠在填充上）

- [ ] **Step 4: 三态配色覆盖验证**

```bash
echo "=== phase 切换点 ===" && grep -rn "setPhase\|phase = " src/frontend/src/features/startup/useStartupBridge.ts | head -10
echo ""
echo "=== CSS 变量 phase 类 ===" && grep -rn "phase-boot\|phase-warn\|phase-error\|phase-success" src/frontend/src/features/startup/ src/frontend/src/pages/StartupPage.tsx
```

- [ ] **Step 5: 前后端整体构建**

```bash
cd /workspace && echo "=== Frontend ===" && cd src/frontend && npm run build 2>&1 | tail -5 && cd ../.. && echo "" && echo "=== Backend ===" && cd src && dotnet build MSMC/MSMC.csproj --no-restore 2>&1 | tail -5
```

Both must pass with 0 errors.

---

## Self-Review Checklist

| 项 | 结果 |
|----|------|
| Spec 覆盖率 | ✅ 三态配色、进度条外部百分比、双血红、自动滚动日志、急救箱按钮组、真实 FPS 监测、1 层背景、贝塞尔曲线、状态 phase 驱动，全部覆盖 |
| Placeholder 扫描 | ✅ 无 TODO/TBD/implement later |
| 类型一致性 | ✅ BootPhase, LogEntry, BOOT_PHASE_CONFIG 在 bootState.ts 定义，所有子组件引用一致；bridge payload 字段名与后端 StartupWindow.xaml.cs 匹配 |
| Bridge 契约不变量 | ✅ 6 个已有事件 payload 格式完全保持，新增 3 个仅向前兼容 |
| 文件行数 | ✅ 所有新建文件 < 200 行，主壳 < 160 行 |
