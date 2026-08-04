# 将电源管理和 Java 管理从设置页拆分为独立侧栏页面

## 概述

当前 SettingsPage.tsx 共 2688 行，电源管理（两张卡片，约 400 行）和 Java 管理（一张卡片，约 250 行）占了将近 1/4。拆到侧栏独立页面后，设置页只留主题/通知/监管/关于，各页面各司其职。

## 当前状态

**侧栏 5 项**：服务器管理 `/`、系统监控 `/system`、网络监控 `/network`、配置编辑 `/config`、设置 `/settings`

**SettingsPage 卡片分布**：
| 卡片 | 行范围 | 去留 |
|---|---|---|
| 颜色方案/圆角/动画 | 736-1048 | 留 |
| 服务器设置（含 preferJavaw 开关） | 1053-1128 | 留 |
| 进程监管策略 | 1133-1330 | 留 |
| CPU 电源档位与睿频管控 | 1332-1484 | **移走** |
| 用户层最大权限调度 | 1486-1729 | **移走** |
| Java 运行环境 | 1731-1983 | **移走** |
| 关于 MSMC | 1988-2645 | 留 |
| 底部操作栏（重置/应用/保存） | 2650-2672 | 留 |

## 改动清单

### 1. 新建 `src/frontend/src/pages/PowerPage.tsx`

从 SettingsPage 提取电源相关全部逻辑：

**State（原 174-225 行）**：
- `cpuPowerCaps`, `applyingProfile`, `powerError`, `restoringProfile`
- `serverQoSTier`（localStorage `msmc_server_qos`）
- `cpuSetTopology`, `autoPinPCores`（localStorage `msmc_auto_pin_pcores`）
- `timerTier`（localStorage `msmc_timer_tier`）, `timerState`
- `serverBoostMode`（localStorage `msmc_server_boost`）, `powerReqState`
- `timerApplying`, `powerReqApplying`
- 常量 `powerProfileOptions`, `timerOptions`

**Handler（原 227-369 行）**：
- `refreshCpuPowerCaps`, `handleApplyPowerProfile`, `handleRestorePowerProfile`
- `handleSetServerQoS`, `refreshCpuSetTopology`, `refreshTimerState`, `refreshPowerRequestState`
- `handleToggleAutoPinPCores`, `handleSetTimerTier`, `handleSetServerBoostMode`, `handleTogglePowerRequest`

**JSX（原 1332-1729 行）**：两张卡片整体搬过来

**useEffect**：自带一个 `useEffect` 调用 4 个 refresh 函数

**Bridge 导入**：`getCpuPowerCapabilities, applyPowerProfile, restorePowerProfile, getCpuSetTopology, enableTimerResolution, disableTimerResolution, getTimerResolutionState, startPowerRequest, stopPowerRequest, getPowerRequestState`

**类型导入**：`CpuPowerCapabilities, PowerProfile, ProcessQoSTier, CpuSetTopology, TimerResolutionResult, PowerRequestResult`

**图标**：`FaBolt, FaMicrochip, FaClock, FaPlug, FaMoon, FaMemory`

**statusMessage**：用本地 `useState`，和 SettingsPage 一样的模式

**导出**：`export function PowerPage(): JSX.Element`（具名导出，配合 App.tsx 的 lazy 模式）

### 2. 新建 `src/frontend/src/pages/JavaPage.tsx`

从 SettingsPage 提取 Java 管理全部逻辑：

**State（原 100-106 行）**：
- `javaList`, `isScanningJava`, `newJavaPath`, `javaOpInProgress`

**Handler（原 388-396, 505-603 行）**：
- `loadJavaList`（useCallback）
- `handleRescanJava`, `handleBrowseJavaPath`, `handleAddJavaPath`, `handleSetDefaultJava`, `handleRemoveJavaPath`

**JSX（原 1731-1983 行）**：Java 卡片整体搬过来

**useEffect**：自带 `loadJavaList()` 调用

**Bridge 导入**：`getJavaList, rescanJava, addJavaPath, removeJavaPath, setDefaultJava, browseJavaPath`

**类型导入**：`JavaInstallationInfo, JavaListResponse`

**图标**：`FaMugHot, FaRotate, FaStar, FaTrashCan, FaFolderOpen, FaPlus`

**statusMessage**：本地 `useState`

**导出**：`export function JavaPage(): JSX.Element`

### 3. 修改 `src/frontend/src/components/Sidebar.tsx`

在 `navItems` 数组中，`/config` 和 `/settings` 之间插入两项：

```ts
{ path: '/power', label: '电源管理', icon: <FaBolt size={16} /> },
{ path: '/java',  label: 'Java 管理', icon: <FaMugHot size={16} /> },
```

import 中追加 `FaBolt, FaMugHot`。

### 4. 修改 `src/frontend/src/App.tsx`

懒加载追加：
```ts
const PowerPage = lazy(() => import('@/pages/PowerPage').then(m => ({ default: m.PowerPage })))
const JavaPage  = lazy(() => import('@/pages/JavaPage').then(m => ({ default: m.JavaPage })))
```

路由追加：
```tsx
<Route path="/power" element={<PowerPage />} />
<Route path="/java"  element={<JavaPage />} />
```

### 5. 修改 `src/frontend/src/pages/SettingsPage.tsx`

**删除**：
- 电源相关 state（174-225 行的对应声明）
- 电源相关 handler（227-369 行的对应函数）
- Java 相关 state（100-101, 105-106 行）
- Java 相关 handler（388-396, 505-603 行）
- 电源卡片 JSX（1332-1729 行）
- Java 卡片 JSX（1731-1983 行）
- 电源相关 bridge 导入（47-56 行）
- Java 相关 bridge 导入（36-41 行）
- 电源相关类型导入（67-72 行）
- Java 相关类型导入（60-61 行）
- 不再使用的图标导入（FaBolt, FaMicrochip, FaClock, FaPlug, FaMugHot, FaMoon, FaMemory, FaFolderOpen, FaPlus, FaTrashCan, FaStar — 但需检查 FaBolt 和 FaRotate 是否仍被其他卡片使用）

**修改 useEffect（428-449 行）**：
- 移除 `refreshCpuPowerCaps()`, `refreshCpuSetTopology()`, `refreshTimerState()`, `refreshPowerRequestState()`, `loadJavaList()` 调用
- 移除依赖数组中对应的函数

**修改 handleReset（667-704 行）**：
- 从 `keysToRemove` 中移除 `msmc_server_qos`, `msmc_auto_pin_pcores`, `msmc_timer_tier`, `msmc_server_boost`
- 移除 `setServerQoSTier('High')`, `setAutoPinPCores(false)`, `setTimerTier(0)`, `setServerBoostMode('auto')`

**保留不动**：
- `preferJavaw` — 留在「服务器设置」卡片（这是服务器启动行为配置，不是 Java 安装管理）
- `statusMessage` — 设置页仍需本地状态
- `FaRotate` — 仍被监管策略卡片和底部按钮使用
- `FaBolt` — 检查后仍被进程监管策略卡片（1181, 1252 行）使用，需保留

## 代码风格要求

用户明确要求"前端给人看的东西不要写的太具象化"，新页面代码风格：

- **不要** `════════════` 分隔线注释块和 `[CPU POWER]`、`[T3]` 之类的标签注释
- **不要**给每个函数写 JSDoc，只在逻辑不明显处加简短行注释
- **不要**过度的 try-catch + console.error 嵌套，只在真正需要的地方 catch
- 变量命名保持和现有代码一致，不刻意缩短也不刻意拉长
- inline style 保持现有风格（CSS 变量 + 对象），不折腾
- 注释用中文，但少写，写就要写到点上

## 假设与决策

1. **preferJavaw 不跟随 Java 页面** — 它是"服务器启动时用 javaw 还是 java"的开关，语义上属于服务器设置，且与 `handleApplyTheme`/`handleSave`/`loadSettings` 深度耦合，强行移走弊大于利。
2. **statusMessage 不提升到全局 store** — 当前 SettingsPage 用的是本地 useState 而非 appStore，新页面也用本地 useState，保持一致。
3. **电源页的 reset 不与设置页联动** — 设置页 handleReset 只清理设置页管辖的 localStorage 键，电源页的键由电源页自己管理。用户在电源页操作时自行重置。
4. **路由路径** `/power` 和 `/java` — 简洁，不与现有路径冲突，startsWith 匹配无歧义。

## 验证

1. 前端 build 通过（`npm run build` 或 `tsc --noEmit`）
2. 侧栏出现「电源管理」和「Java 管理」两个新项，点击能正确路由
3. 电源页功能完整：CPU 档位切换、QoS 标签、CPU Set 拓扑、定时器精度、Priority Boost、Power Request 均可用
4. Java 页功能完整：扫描、列表展示、设为默认、添加自定义路径、移除均可用
5. 设置页不再显示电源和 Java 卡片，其余卡片正常
6. 设置页 handleReset 不再清理电源相关 localStorage 键
7. 页面切换动画（md-page-enter）正常
