# 设置页颜色体系完整化 Spec

## Why

1. **"快速方案"大面积失效**：设置页渲染 13 套快速方案，但 `SettingsViewModel.SetPreset` 的 `switch` 只写了 5 套（SkyBlue/OceanBlue/BlueOrange/TealPink/RedYellow），新增的 8 套 ColorOS 预设（ColorOSBlue/AquarioCyan/AuroraPurple/SunsetOrange/MintGreen/SakuraPink/MidnightGold/ArcticGray）没有任何分支命中 → 点了颜色完全不变；且桥接无条件返回 `success=true`，是典型 P4「诚实返回链」违规（假成功）。
2. **调色盘修改受限**：后端 `ThemeService`/`SettingsViewModel` 已支持 6 个主题色通道（主色/强调色/背景色/卡片色/文字色/边框色），前端 `applySettingsToCss` 也消费全部 6 色，但设置页 UI 只暴露了主色调 + 强调色 2 个调色盘，且桥接只有 2 个 setter → 6 色中 4 色不可自定义；成功/警告/错误/仪表盘等语义色全部硬编码。

## What Changes

- **根因修复**：`SettingsViewModel.SetPreset` 的 5-case `switch` 删除，改为委托 `ThemePresetRegistry.ApplyPreset`（单一真相源），13 套预设全部生效；预设未命中时诚实返回失败，不再假成功。
- **预设补全**：`ThemePreset` 记录扩展 `TextColorHex`/`BorderColorHex`（可选），13 套预设补全为 6 色完整方案（主/强调/背景/卡片/文字/边框），一次切换全界面换肤。
- **调色盘扩为 12 个**（6 主题色 + 6 语义/仪表盘色，全部可独立自定义并持久化）：
  - 主题色 6：主色、强调色、背景色、卡片色、文字色、边框色（补齐 UI 控件 + 桥接 setter）
  - 语义色 3：成功色、警告色、错误色
  - 仪表盘色 3：仪表盘绿、仪表盘黄、仪表盘红
- **语义色接线**：`ThemeService.UpdateResources` 与前端 `applyStatusColors` 从语义色派生 subtle 背景/边框、danger/error-text、gauge 系列，替换现有硬编码。
- **前端 UI**：设置页新增 4 个主题色调色盘 + 6 个语义/仪表盘色调色盘（复用现有 `ColorPicker` 组件），全部支持实时预览 + 松手提交（`onChange` 预览 / `onChangeEnd` 走桥接）。
- **诚实返回链**：`settings:setPreset` 按预设是否命中返回真实 success；所有新增颜色 setter 解析失败时返回 `success=false`（沿用现有 catch 模式）。

**BREAKING**：无。旧 `theme-settings.json` 缺少新字段时走 `LoadSettings` 的默认值兜底；`ThemeSettings` 属性带默认值，兼容旧文件。

## Impact

- 受影响能力：设置（外观设置 / 快速方案 / 颜色自定义）、主题系统（C# 主题服务 + 前端 CSS 变量）、桥接契约。
- 受影响代码：
  - C#：`src/MSMC/Features/Settings/Services/ThemePresetRegistry.cs`、`ThemeService.cs`、`ViewModels/SettingsViewModel.cs`、`Features/Shared/Views/MainWindow.xaml.cs`（设置桥接区）、`src/MSMC.Tests/Services/ThemePresetsTests.cs`
  - 前端：`src/frontend/src/types/bridge.ts`、`utils/theme.ts`、`utils/bridge.ts`、`pages/SettingsPage.tsx`
  - 产物：前端 `dist`（构建后由 MSBuild 自动打包为 `wwwroot.zip` 嵌入，无需手工处理）

## ADDED Requirements

### Requirement: 13 套快速方案全部生效且诚实返回

系统 SHALL 让设置页展示的所有 13 套快速方案实际改变主题颜色，且应用结果与操作一致。

#### Scenario: 点击 ColorOS 系列预设
- **WHEN** 用户点击任一快速方案按钮（含此前失效的 8 套 ColorOS 预设）
- **THEN** 全局主题切换为该预设的 6 色完整方案（主/强调/背景/卡片/文字/边框），前端 `loadSettings()` 刷新后 UI 同步；预设未命中时桥接返回 `success=false` 且前端提示失败

#### Scenario: 未知预设 Key
- **WHEN** 桥接收到不存在的预设 key
- **THEN** `settings:setPreset` 返回 `{ success = false, error = ... }`，不修改任何颜色，不再假成功

### Requirement: 12 个颜色通道全部可自定义

系统 SHALL 提供 12 个独立可调的调色盘并持久化：主色、强调色、背景色、卡片色、文字色、边框色、成功色、警告色、错误色、仪表盘绿、仪表盘黄、仪表盘红。

#### Scenario: 修改背景/卡片/文字/边框色
- **WHEN** 用户在设置页调整任一新增主题色（背景/卡片/文字/边框）
- **THEN** 拖动过程实时预览（前端局部 CSS 变量更新），松手后通过桥接持久化；`getSettings` 返回值随变更同步

#### Scenario: 修改语义色与仪表盘色
- **WHEN** 用户调整成功/警告/错误/仪表盘三色之一
- **THEN** 所有派生色（subtle 背景/边框、danger、error-text、gauge）随基色联动；重启应用后设置保留

#### Scenario: 应用快速方案后新增调色盘数据同步
- **WHEN** 用户点击快速方案
- **THEN** 6 个主题色调色盘显示值刷新为预设值（语义/仪表盘色保持用户自定义值不变）

### Requirement: 语义色派生一致性（前后端对照）

系统 SHALL 使 WPF 资源与前端 CSS 变量从同一语义基色派生，消除两处独立硬编码漂移。

#### Scenario: 修改错误色
- **WHEN** 用户修改错误色
- **THEN** C# 侧 `DangerBrush`/`GaugeRedBrush`/`ErrorTextBrush`(亮化派生) 与前端 `--md-danger`/`--md-gauge-red`/`--md-error-text` 使用同一基色（或同一派生规则）

### Requirement: 契约测试覆盖预设完整性

系统 SHALL 用自动化测试锁住 13 套预设的完整性与应用有效性。

#### Scenario: 全量预设测试
- **WHEN** 运行 `dotnet test`
- **THEN** 遍历 13 个 key 全部能通过 `ApplyPreset` 改变主题色、且 6 色 HEX 合法；未知 key 调用返回失败

## MODIFIED Requirements

### Requirement: settings:getSettings 返回完整颜色集
- 在现有 6 个颜色字段基础上，新增 `successColorHex` / `warningColorHex` / `errorColorHex` / `gaugeGreenColorHex` / `gaugeYellowColorHex` / `gaugeRedColorHex`（`#RRGGBB`，经 `ArgbToRgb` 转换，与现有字段一致）。
- 桥接在 `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` 设置区；`types/bridge.ts` 的 `SettingsData` 同步扩展。

### Requirement: ThemeService 新增 6 个颜色属性并持久化
- `IThemeService` + `ThemeService` 新增 `SuccessColor` / `WarningColor` / `ErrorColor` / `GaugeGreenColor` / `GaugeYellowColor` / `GaugeRedColor`（默认值沿用现硬编码：`#4CAF50` / `#FFC107` / `#E53935` / `#4CAF50` / `#FFC107` / `#F4364C`）。
- `ThemeSettings` 持久化 6 个新字段；`LoadSettings` / `SaveSettings` / `ResetToDefault` 全部覆盖。
- `UpdateResources`：成功/警告/错误/仪表盘系 brush 从 6 个新属性派生（subtle 用 alpha 派生，error-text 从错误色亮化派生），删除硬编码字面量。

### Requirement: SettingsViewModel 增加 6 个颜色通道
- 新增 6 个 `[ObservableProperty]` Color 字段及 `XxxColorHex`（只读派生，`#RRGGBB`）+ `XxxColorBrush` 派生属性 + `SetXxxColor(string hex)` 命令（模式同现有 `SetBackgroundColor`）。
- `LoadSettings` / `ApplyTheme` / `ResetToDefault` 同步 6 个新通道。
- `SetPreset` 重构：委托 `ThemePresetRegistry.ApplyPreset(_themeService, key)`；命中后回读 6 色到 VM（用 `BeginBatchUpdate`/`EndBatchUpdate` 包裹避免多次 `ApplyTheme`）；未命中置失败状态信息。

### Requirement: 前端主题应用与预览闭环
- `utils/theme.ts`：
  - `applyStatusColors` 参数化（success/warning/error/gauge×3），`applySettingsToCss` 消费新 6 色字段；语义色派生 subtle/error-text/danger/gauge 系列（替换硬编码）。
  - 新增单通道预览函数：`applyBackgroundColor` / `applyCardColor` / `applyTextColor` / `applyBorderColor` / `applySemanticColors(...)`（供 `onChange` 实时预览用，模式同 `applyPrimaryColor`）。
- `utils/bridge.ts`：新增 10 个颜色 setter wrapper（背景/卡片/文字/边框/成功/警告/错误/仪表盘×3，模式同 `setPrimaryColor`）。
- `pages/SettingsPage.tsx`：
  - 主题色区从 2 个调色盘扩为 6 个（主/强调 2×2 保留，新增背景/卡片/文字/边框）。
  - 新增「语义与仪表盘色」卡片：成功/警告/错误/仪表盘绿/黄/红 6 个调色盘。
  - 每个调色盘 `onChange` → 本地预览函数，`onChangeEnd` → 对应桥接 setter + `loadSettings()`。

### Requirement: ThemePreset 记录扩展
- `ThemePreset` 新增 `TextColorHex` / `BorderColorHex`（`string?`，null 时 `ApplyPreset` 跳过该通道）。
- 13 套预设全部补全 6 色：5 套旧预设沿用现 `SettingsViewModel.SetPreset` switch 中的文字/边框值（值以注册表为准）；8 套新预设补合理文字/边框色（实现时可用 OKLCH 按卡片色派生或手工协调，须通过合法性测试）。

## REMOVED Requirements

### Requirement: SettingsViewModel.SetPreset 的 5-case switch
**Reason**: 与 `ThemePresetRegistry` 重复定义真相源，且只有 5 个分支导致 8 套预设静默失效；值已与注册表漂移（如 OceanBlue 背景 `#0A1929` vs 注册表 `#04181C`）。
**Migration**: 全部走 `ThemePresetRegistry.ApplyPreset`（含批量更新）单一入口；5 套旧预设的视觉值通过注册表补全保持一致，用户无感迁移。

### Requirement: 前端主题色区仅 2 个调色盘
**Reason**: 无法满足「整个项目颜色全部可自定义」。
**Migration**: 扩为 12 个调色盘（见 ADDED）；旧 `localStorage` 键不受影响。

## 设计边界（不做）

- 纪念卡金色（`--md-memorial-gold-*` / `ApplyMemorialColors`）保持固定，不开放自定义。
- WPF 原生 `Features/Settings/Views/SettingsPage.xaml` 为遗留死代码（全部 UI 由 WebView2 渲染），不修改。
- 12 个调色盘统一使用现有 `ColorPicker` 组件，不新增紧凑型控件。
- 预设只定义 6 主题色；语义/仪表盘色独立持久化，不随预设切换。