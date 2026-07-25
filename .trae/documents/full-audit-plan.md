# 项目全面核查与功能落地实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 全面核查项目，消除所有硬编码和假数据，确保所有功能真实落地，前后端数据通路完整通畅。

**Architecture:** 采用"后端数据驱动前端"的原则——所有主题配置、业务数据都由C#后端提供，前端仅负责渲染和交互。主题通过CSS变量动态注入，确保设置页修改即时生效。

**Tech Stack:** WPF + WebView2 + React + TypeScript + Tailwind CSS + CommunityToolkit.Mvvm + Serilog

---

## 文件结构总览

### 后端（C#）修改文件
- `src/McServerGuard/Views/MainWindow.xaml.cs` - 桥接API补充与优化
- `src/McServerGuard/ViewModels/NetworkMonitorPageViewModel.cs` - 新增吞吐量历史数据API
- `src/McServerGuard/Services/WebView2/WebView2BridgeService.cs` - 新增主题推送事件

### 前端（React/TS）修改文件
- `src/frontend/src/hooks/useBridgeInit.ts` - 初始化时注入主题CSS变量
- `src/frontend/src/stores/appStore.ts` - 新增主题CSS变量更新逻辑
- `src/frontend/src/pages/NetworkMonitorPage.tsx` - 移除假数据，接入真实吞吐量历史
- `src/frontend/src/pages/SettingsPage.tsx` - 色板和预设改为从后端获取
- `src/frontend/src/components/ui/ChartPlaceholder.tsx` - 使用CSS变量替代硬编码颜色
- `src/frontend/src/types/bridge.ts` - 补充类型定义
- `src/frontend/src/utils/bridge.ts` - 补充API函数

---

## Task 1: 后端暴露网络吞吐量历史数据API

**Files:**
- Modify: `src/McServerGuard/ViewModels/NetworkMonitorViewModel.cs`
- Modify: `src/McServerGuard/Views/MainWindow.xaml.cs`

### 问题诊断
当前前端 `NetworkMonitorPage.tsx` 的 `HourlyThroughputChart` 组件使用 `Math.sin` 生成假的24小时吞吐量数据。后端 `NetworkMonitorViewModel` 已经有 `_hourlyUploadValues` 和 `_hourlyDownloadValues` 集合，但没有通过桥接API暴露给前端。

### 实现方案
在 `NetworkMonitorViewModel` 中添加公开属性暴露24小时吞吐量历史数据，然后在 `MainWindow.xaml.cs` 中注册桥接API。

- [ ] **Step 1: 在 NetworkMonitorViewModel 中添加公开属性**

在 `NetworkMonitorViewModel.cs` 中添加（注意：已有 `_hourlyUploadValues` 和 `_hourlyDownloadValues` 字段，只需添加公开属性）：
```csharp
/// <summary>24小时上传吞吐量历史（MB/s），索引0-23对应0点-23点</summary>
public double[] HourlyUploadMBArray
{
    get => _hourlyUploadValues.ToArray();
}

/// <summary>24小时下载吞吐量历史（MB/s），索引0-23对应0点-23点</summary>
public double[] HourlyDownloadMBArray
{
    get => _hourlyDownloadValues.ToArray();
}
```

- [ ] **Step 2: 在 MainWindow.xaml.cs 中注册吞吐量历史API**

在 `RegisterNetworkApis()` 方法中添加：
```csharp
// 获取24小时吞吐量历史
_bridgeService.RegisterRequestHandler("network:getHourlyHistory", _ =>
{
    return Task.FromResult<object?>(new
    {
        upload = net?.HourlyUploadMBArray ?? new double[24],
        download = net?.HourlyDownloadMBArray ?? new double[24],
        currentHour = net?.CurrentHour ?? DateTime.Now.Hour,
    });
});
```

- [ ] **Step 3: 验证编译**

运行: `dotnet build src/McServerGuard/McServerGuard.csproj`
Expected: 编译成功，无错误

---

## Task 2: 前端网络监控页移除假数据，接入真实吞吐量历史

**Files:**
- Modify: `src/frontend/src/pages/NetworkMonitorPage.tsx`
- Modify: `src/frontend/src/utils/bridge.ts`
- Modify: `src/frontend/src/types/bridge.ts`

### 问题诊断
`HourlyThroughputChart` 组件第126-133行使用 `Math.sin` 生成假数据，需要替换为从后端获取的真实数据。同时端口分布饼图第55-57行硬编码了颜色值。

- [ ] **Step 1: 在类型定义中添加吞吐量历史类型**

在 `src/frontend/src/types/bridge.ts` 中添加：
```typescript
export interface HourlyHistoryResponse {
  upload: number[]
  download: number[]
  currentHour: number
}
```

- [ ] **Step 2: 在 bridge.ts 中添加获取吞吐量历史的函数**

在网络监控API区域添加：
```typescript
export function getHourlyHistory(): Promise<HourlyHistoryResponse> {
  return bridge.invoke<HourlyHistoryResponse>('network:getHourlyHistory')
}
```

- [ ] **Step 3: 修改 NetworkMonitorPage，接入真实吞吐量历史**

将 `HourlyThroughputChart` 组件从使用 `Math.sin` 假数据改为接收真实数据：
1. 在 `NetworkMonitorPage` 组件中添加 `hourlyHistory` 状态
2. 在 `loadData` 中调用 `getHourlyHistory()`
3. 将真实数据传递给 `HourlyThroughputChart`
4. 移除组件内部的假数据生成逻辑

- [ ] **Step 4: 移除端口分布饼图的硬编码颜色**

将 `PortDistributionPie` 组件中的硬编码颜色：
```tsx
{ value: systemPorts, color: '#F87171', label: '系统' },
{ value: registeredPorts, color: '#60A5FA', label: '注册' },
{ value: dynamicPorts, color: '#4ADE80', label: '动态' },
```
改为使用CSS变量或主题色：
```tsx
{ value: systemPorts, color: 'var(--md-gauge-red)', label: '系统' },
{ value: registeredPorts, color: 'var(--md-primary-hue-mid)', label: '注册' },
{ value: dynamicPorts, color: 'var(--md-gauge-green)', label: '动态' },
```

- [ ] **Step 5: 验证前端构建**

运行: `cd src/frontend && npm run build`
Expected: 构建成功，无TypeScript错误

---

## Task 3: 实现前端主题动态更新（CSS变量注入）

**Files:**
- Modify: `src/frontend/src/stores/appStore.ts`
- Modify: `src/frontend/src/hooks/useBridgeInit.ts`
- Modify: `src/frontend/src/pages/SettingsPage.tsx`

### 问题诊断
当前 `theme.css` 中的CSS变量是硬编码的。后端设置页改变主题色后，前端不会动态更新。需要通过 `document.documentElement.style.setProperty` 动态注入主题变量。

- [ ] **Step 1: 在 appStore 中添加动态主题应用函数**

在 `setTheme` 函数中，除了设置 dark/light 模式外，还要动态设置主色等CSS变量：
```typescript
setTheme: (theme) => {
  if (theme.mode === 'dark') {
    document.documentElement.classList.add('dark')
  } else {
    document.documentElement.classList.remove('dark')
  }
  // 动态设置主色
  if (theme.primaryColor) {
    document.documentElement.style.setProperty('--md-primary-hue-mid', theme.primaryColor)
  }
  set({ theme })
},
```

- [ ] **Step 2: 扩展 SettingsData 类型，确保所有主题色都能传递**

确认 `SettingsData` 类型包含：
- `primaryColorHex`
- `accentColorHex`
- `backgroundColorHex`
- `cardColorHex`
- `textColorHex`
- `borderColorHex`
- `cornerRadius`
- `animationDuration`

- [ ] **Step 3: 创建设置主题工具函数**

在 `utils/theme.ts`（新建）中添加颜色转换和应用设置到CSS变量的函数：
```typescript
import type { SettingsData } from '@/types/bridge'

/**
 * 将 #AARRGGBB 格式转换为 #RRGGBB 格式
 * 后端返回带 alpha 通道的 8 位十六进制颜色，前端 CSS 需要 6 位
 */
function argbToRgb(hex: string): string {
  if (!hex) return '#000000'
  let h = hex.trim().toUpperCase()
  if (h.startsWith('#')) h = h.slice(1)
  if (h.length === 8) {
    // #AARRGGBB -> #RRGGBB
    return '#' + h.slice(2)
  }
  if (h.length === 6) {
    return '#' + h
  }
  return hex
}

export function applySettingsToCss(settings: SettingsData): void {
  const root = document.documentElement.style
  
  // 颜色（注意后端返回 #AARRGGBB 格式，需要转换）
  root.setProperty('--md-primary-hue-mid', argbToRgb(settings.primaryColorHex))
  root.setProperty('--md-accent-text', argbToRgb(settings.accentColorHex))
  root.setProperty('--md-paper', argbToRgb(settings.backgroundColorHex))
  root.setProperty('--md-card-background', argbToRgb(settings.cardColorHex))
  root.setProperty('--md-body', argbToRgb(settings.textColorHex))
  root.setProperty('--md-subtle-border', argbToRgb(settings.borderColorHex))
  
  // 圆角
  root.setProperty('--md-radius', `${settings.cornerRadius}px`)
  root.setProperty('--md-radius-small', `${Math.max(4, settings.cornerRadius - 4)}px`)
  root.setProperty('--md-radius-large', `${settings.cornerRadius + 4}px`)
  
  // 动画
  root.setProperty('--md-duration-normal', `${settings.animationDuration}ms`)
}
```

- [ ] **Step 4: 在 useBridgeInit 中初始化时应用主题**

在获取到设置后调用 `applySettingsToCss`

- [ ] **Step 5: 在 SettingsPage 修改设置后实时更新**

修改颜色、圆角等设置后，立即调用 `applySettingsToCss` 更新前端显示

- [ ] **Step 6: 验证构建**

运行: `cd src/frontend && npm run build`
Expected: 构建成功

---

## Task 4: 设置页色板和预设从后端获取（消除硬编码）

**Files:**
- Modify: `src/McServerGuard/Views/MainWindow.xaml.cs`
- Modify: `src/frontend/src/pages/SettingsPage.tsx`
- Modify: `src/frontend/src/utils/bridge.ts`
- Modify: `src/frontend/src/types/bridge.ts`

### 问题诊断
`SettingsPage.tsx` 第40-75行硬编码了颜色色板和预设主题。这些数据在WPF后端也有定义，存在重复维护问题。应该从后端获取。

- [ ] **Step 1: 后端添加获取预设和色板的API**

在 `RegisterSettingsApis()` 中添加：
```csharp
// 获取预设主题列表
_bridgeService.RegisterRequestHandler("settings:getPresets", _ =>
{
    var presets = new[]
    {
        new { key = "SkyBlue", label = "苍穹蓝", primary = "#3B82F6", accent = "#FB7185" },
        new { key = "BlueOrange", label = "科技蓝", primary = "#1565C0", accent = "#FF9800" },
        new { key = "TealPink", label = "清新绿", primary = "#00897B", accent = "#E91E63" },
        new { key = "RedYellow", label = "火焰红", primary = "#C62828", accent = "#FFD600" },
        new { key = "OceanBlue", label = "海洋蓝", primary = "#0097A7", accent = "#FFD740" },
    };
    return Task.FromResult<object?>(new { presets });
});

// 获取主色色板
_bridgeService.RegisterRequestHandler("settings:getPrimarySwatches", _ =>
{
    var swatches = new[]
    {
        new { color = "#7B1FA2", label = "深紫" },
        new { color = "#1565C0", label = "蓝" },
        new { color = "#00897B", label = "青绿" },
        new { color = "#C62828", label = "红" },
        new { color = "#F57C00", label = "橙" },
        new { color = "#2E7D32", label = "绿" },
        new { color = "#0D47A1", label = "深蓝" },
        new { color = "#4A148C", label = "深紫红" },
    };
    return Task.FromResult<object?>(new { swatches });
});

// 获取强调色色板
_bridgeService.RegisterRequestHandler("settings:getAccentSwatches", _ =>
{
    var swatches = new[]
    {
        new { color = "#CDDC39", label = "青柠" },
        new { color = "#FF9800", label = "橙" },
        new { color = "#E91E63", label = "粉红" },
        new { color = "#FFD600", label = "黄" },
        new { color = "#00BCD4", label = "青" },
        new { color = "#8BC34A", label = "浅绿" },
        new { color = "#FF5722", label = "深橙" },
        new { color = "#6366F1", label = "靛蓝" },
    };
    return Task.FromResult<object?>(new { swatches });
});
```

- [ ] **Step 2: 前端类型定义补充**

在 `types/bridge.ts` 中添加：
```typescript
export interface SwatchInfo {
  color: string
  label: string
}

export interface PresetInfo {
  key: ThemePreset
  label: string
  primary: string
  accent: string
}

export interface SwatchesResponse {
  swatches: SwatchInfo[]
}

export interface PresetsResponse {
  presets: PresetInfo[]
}
```

- [ ] **Step 3: 前端 bridge.ts 补充API函数**

```typescript
export function getPresets(): Promise<PresetsResponse> {
  return bridge.invoke<PresetsResponse>('settings:getPresets')
}

export function getPrimarySwatches(): Promise<SwatchesResponse> {
  return bridge.invoke<SwatchesResponse>('settings:getPrimarySwatches')
}

export function getAccentSwatches(): Promise<SwatchesResponse> {
  return bridge.invoke<SwatchesResponse>('settings:getAccentSwatches')
}
```

- [ ] **Step 4: 修改 SettingsPage，从后端加载色板和预设**

将硬编码的 `primarySwatches`、`accentSwatches`、`presetOptions` 改为从后端获取的状态变量。

- [ ] **Step 5: 验证编译**

运行: `dotnet build src/McServerGuard/McServerGuard.csproj`
运行: `cd src/frontend && npm run build`
Expected: 全部构建成功

---

## Task 5: ChartPlaceholder 使用CSS变量替代硬编码颜色

**Files:**
- Modify: `src/frontend/src/components/ui/ChartPlaceholder.tsx`

### 问题诊断
`ChartPlaceholder.tsx` 中硬编码了渐变色，应使用主题CSS变量。

- [ ] **Step 1: 修改 ChartPlaceholder，使用CSS变量**

将硬编码的颜色：
```tsx
<stop offset="0%" stopColor="#3b82f6" stopOpacity="0.3" />
<stop offset="100%" stopColor="#3b82f6" stopOpacity="0.02" />
```
和：
```tsx
<stop offset="0%" stopColor="#60a5fa" />
<stop offset="100%" stopColor="#8b5cf6" />
```
改为通过 props 传入或使用 CSS 变量。

由于 SVG 的 stopColor 不直接支持 CSS 变量，需要通过 style 属性设置，或者使用 currentColor 技巧。推荐方案：通过 props 接收颜色，由父组件传递主题色。

- [ ] **Step 2: 验证构建**

运行: `cd src/frontend && npm run build`
Expected: 构建成功

---

## Task 6: 验证服务器管理页功能完整性

**Files:**
- Verify: `src/frontend/src/pages/DashboardPage.tsx`
- Verify: `src/McServerGuard/Views/MainWindow.xaml.cs`

### 问题诊断
需要确认服务器管理页的所有功能都已接入真实数据，没有硬编码或假数据。

- [ ] **Step 1: 检查 DashboardPage 数据来源**

通读 `DashboardPage.tsx`，确认：
- 运行中服务器列表来自 `getServerList()`
- 已知服务器列表来自 `getServerList()`
- 选中服务器详情来自 `getSelectedServer()`
- 启动/停止/刷新操作都调用了对应桥接API

- [ ] **Step 2: 补齐缺失的 API（如有）**

如果发现前端调用了但后端未注册的API，在 `MainWindow.xaml.cs` 中补齐。

- [ ] **Step 3: 验证构建**

运行: `dotnet build src/McServerGuard/McServerGuard.csproj`
运行: `cd src/frontend && npm run build`
Expected: 全部构建成功

---

## Task 7: 验证配置编辑页功能完整性

**Files:**
- Verify: `src/frontend/src/pages/ConfigEditorPage.tsx`
- Verify: `src/McServerGuard/Views/MainWindow.xaml.cs`

### 问题诊断
需要确认配置编辑页的所有功能都已接入真实数据。

- [ ] **Step 1: 检查 ConfigEditorPage 数据来源**

通读 `ConfigEditorPage.tsx`，确认：
- 可用服务器列表来自 `getAvailableServers()`
- 配置文件树来自 `getConfigFileTree()`
- 配置条目来自 `getConfigEntries()`
- 保存/重置/撤销操作都调用了对应桥接API

- [ ] **Step 2: 补齐缺失的 API（如有）**

如果发现前端调用了但后端未注册的API，在 `MainWindow.xaml.cs` 中补齐。

- [ ] **Step 3: 验证构建**

运行: `dotnet build src/McServerGuard/McServerGuard.csproj`
运行: `cd src/frontend && npm run build`
Expected: 全部构建成功

---

## Task 8: 全局硬编码扫描与清理

**Files:**
- Scan: `src/frontend/src/**/*.tsx`
- Scan: `src/frontend/src/**/*.ts`
- Scan: `src/McServerGuard/**/*.cs`

### 问题诊断
最后进行一次全面扫描，确保没有遗漏的硬编码和假数据。

- [ ] **Step 1: 前端硬编码颜色扫描**

运行: `grep -rn "#[0-9a-fA-F]\{6\}" src/frontend/src --include="*.tsx" --include="*.ts"`
Expected: 只在类型定义的默认值和注释中出现，不应在组件渲染逻辑中出现

- [ ] **Step 2: 前端假数据扫描**

运行: `grep -rn "Math.random\|Math.sin\|fakeData\|mockData" src/frontend/src --include="*.tsx" --include="*.ts"`
Expected: 无匹配结果（或仅在工具函数/测试中出现）

- [ ] **Step 3: 后端硬编码扫描**

运行: `grep -rn "硬编码\|假数据\|TODO.*硬编码\|TODO.*假数据" src/McServerGuard --include="*.cs"`
Expected: 无匹配结果

- [ ] **Step 4: 最终编译验证**

运行: `dotnet build src/McServerGuard/McServerGuard.csproj`
运行: `cd src/frontend && npm run build`
Expected: 全部构建成功，无错误无警告

---

## Task 9: CI验证

**Files:**
- Verify: `.github/workflows/ci.yml`

- [ ] **Step 1: 确认 CI 配置正确**

检查 `.github/workflows/ci.yml`，确保构建步骤包含：
- NuGet 包还原
- .NET 项目编译
- 前端项目构建（如已集成到 .NET 构建中则跳过）

- [ ] **Step 2: 提交并推送，等待 CI 结果**

提交所有更改并推送到远程仓库，检查 GitHub Actions CI 是否通过。

---

## 验收标准

1. **零假数据**: 前端所有图表、列表的数据都来自后端桥接API，没有 `Math.sin`、`Math.random` 生成的假数据
2. **零硬编码颜色**: 所有组件颜色都通过 CSS 变量或主题系统管理，没有在组件中直接写 `#RRGGBB`（色板/预设展示除外，但数据来源应是后端）
3. **主题实时生效**: 在设置页修改主色、强调色、圆角、动画时长后，前端立即生效，无需刷新
4. **所有API连通**: 前端调用的每个桥接API，后端都有对应的 RegisterRequestHandler
5. **CI 通过**: GitHub Actions 构建成功，无编译错误
6. **类型安全**: TypeScript 类型定义与 C# 返回字段完全一致，没有 any 类型
