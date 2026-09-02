# Spec: 快速预设方案大换血 —— 7 套新主题 + 完整 12 色覆盖

## Problem

MSMC 设置页的"快速方案"存在两个根本性问题：

1. **快速方案颜色修改不完全**：`ThemePreset` record 只定义了 6 个颜色字段（Primary/Accent/Background/Card/Text/Border），`ApplyPreset` 方法也只触达这 6 个通道。但 `ThemeService` 实际有 **12 个独立颜色属性**，另外 6 个是语义色/仪表色（SuccessColor/WarningColor/ErrorColor/GaugeGreenColor/GaugeYellowColor/GaugeRedColor）。这 6 个通道在预设切换时始终保持硬编码的 Material3 默认值，完全不跟随主题变化 —— 用户感知到"换了预设但语义色没变"就是因为这个。

2. **现有 13 套预设全部淘汰**：SkyBlue/BlueOrange/TealPink/RedYellow/OceanBlue + ColorOSBlue/AquarioCyan/AuroraPurple/SunsetOrange/MintGreen/SakuraPink/MidnightGold/ArcticGray 共 13 套方案不再符合用户审美和品牌定位，用户要求全部移除并替换为 7 套新方案。

## Users

* MSMC 应用用户，通过设置页的快速方案切换主题视觉

* 开发者，维护 C# 后端预设注册表和前端下拉选项

## Goals

* **彻底解决"快速方案颜色修改不完全"**：每套预设必须覆盖全部 12 个颜色通道（6 主题 + 6 语义/仪表），`ApplyPreset` 一次性设置全部 12 色

* **移除 13 套旧预设，替换为 7 套新预设**：ColorOS 蓝、芙宁娜蓝、火龙果、青苹果、血红、日落黄、普瑞赛斯紫

* **取色来源可追溯**：每套新方案的主色从参考物（品牌官方色/角色设计/自然参考物）提取，再按 OKLCH 色彩空间衍生同调配色，保证"养眼不单调"

* **契约同步**：C# 端 `ThemePreset` record 扩展后，前端 `ThemePreset` TypeScript 类型、`ApplyPreset` 方法、测试断言全部同步

## Non-Goals

* 不修改 ThemeService 的持久化逻辑（已经支持 12 色）

* 不修改前端 12 调色盘 UI（已经完整）

* 不修改 MaterialDesign WPF 主题系统（只扩展自定义颜色通道）

* 不新增额外预设（严格 7 套）

## Requirements

### Functional Requirements

* **FR-1**：`ThemePreset` record 扩展为 12 个颜色字段，新增 `SuccessColorHex`、`WarningColorHex`、`ErrorColorHex`、`GaugeGreenColorHex`、`GaugeYellowColorHex`、`GaugeRedColorHex` 六个可选字段

* **FR-2**：`ApplyPreset` 方法扩展，在设置 6 个主题色的同时，也设置 6 个语义/仪表色（字段非空时才设置）

* **FR-3**：删除 `ThemePresetRegistry` 中全部 13 套旧预设，新增 7 套新预设，每套预设必须完整定义全部 12 个颜色字段（所有 6 个语义/仪表色字段都有值，不允许 null）

* **FR-4**：新预设必须为**深色模式**优化（Background/Card 为深色基调），符合 MSMC 当前 UI 基调

* **FR-5**：前端 `ThemePreset` TypeScript union type 从 13 个旧 key 替换为 7 个新 key，C# → 前端的 `getPresets` 桥接调用正常返回新预设列表

* **FR-6**：`ThemePresetsTests.cs` 中所有硬编码旧预设 key、预设数量、颜色数量的断言全部更新以匹配新状态

### Non-Functional Requirements

* **NFR-1**：取色来源可追溯（每套方案在 spec 中记录参考物和取色锚点）

* **NFR-2**：每套方案的文字色与背景色对比度 ≥ 4.5:1（WCAG AA）

* **NFR-3**：编译通过（`dotnet build` + `npm run build`）

* **NFR-4**：单元测试通过（`dotnet test`）

## Constraints

* 所有 C# 颜色值必须是合法 HEX（`#RRGGBB` 或 `#AARRGGBB`）

* 前端 `ThemePreset` 类型修改后，TypeScript 编译不能有类型错误

* 必须同时覆盖 12 个颜色通道（不是可选），保证"预设切换 = 全主题切换"

* 7 套新方案的主色必须覆盖不同色相带：蓝系 2 套（ColorOS 冷蓝 + 芙宁娜皇家蓝）、紫系 1 套（普瑞赛斯紫）、红系 1 套（血红）、黄系 1 套（日落黄）、绿系 1 套（青苹果）、洋红系 1 套（火龙果），保证色相多样性

## Assumptions

* 用户在 Spec 阶段已确认 7 套新方案的方向，每个方案的参考物和取色锚点在调研中已确定

* 取色过程可在本地完成（不需要联网图像提取工具，可使用在线取色参考或预设色板生成器）

## Open Questions

* 无

## 取色锚点调研结果（已联网调研确认）

### 7 套新方案的参考物与主色

| 方案 Key         | 中文名       | 主色参考物                           | PrimaryColorHex 锚点 | Accent 方向       |
| -------------- | --------- | ------------------------------- | ------------------ | --------------- |
| `ColorOSBlue`  | ColorOS 蓝 | OPPO ColorOS 品牌蓝（Find X8 极光蓝配色） | `#0066FF` 冷调蓝绿     | 暖粉/珊瑚色强调        |
| `FurinaBlue`   | 芙宁娜蓝      | 原神 4.2 芙宁娜 Royal Blue 服装        | `#1E3A8A` 法国皇家蓝    | 金色 `#D4A017` 强调 |
| `Dragonfruit`  | 火龙果       | 火龙果果实深洋红皮                       | `#C71585` 深洋红      | 亮粉 `#FF69B4` 强调 |
| `GreenApple`   | 青苹果       | 青苹果果实黄绿皮                        | `#9ACD32` 黄绿       | 青绿 `#3CB371` 强调 |
| `BloodRed`     | 血红        | 酒红/暗红色调                         | `#722F37` 酒红       | 橙红 `#DC143C` 强调 |
| `SunsetYellow` | 日落黄       | 日落橙黄场景                          | `#FF8C00` 橙黄       | 金色 `#FFD700` 强调 |
| `PrecePurple`  | 普瑞赛斯紫     | 明日方舟普瑞赛斯紫罗兰双眸                   | `#8B5CF6` 紫罗兰      | 淡紫 `#DA70D6` 强调 |

### 12 色完整配色生成规则

每套方案按以下规则生成全部 12 个颜色（OKLCH 色彩空间衍生，保证色协调）：

| 颜色通道        | 生成规则                                               |
| ----------- | -------------------------------------------------- |
| Primary     | 直接使用主色锚点 HEX                                       |
| Accent      | 主色在 OKLCH 中 Hue 旋转 120-180° 的互补或高对比色               |
| Background  | 主色 Hue 的极深版本（Lightness 5-8%），保证文字可读性               |
| Card        | Background 稍亮（Lightness 10-15%），与 Background 形成层次感 |
| Text        | 主色 Hue 的极浅版本（Lightness 85-95%），保证对比度 ≥ 4.5:1       |
| Border      | 主色 Hue 的中等亮度版本（Lightness 20-30%），与 Card 有明显区分      |
| Success     | 主色的互补色方向（通常绿/青），饱和度适中，适配深色背景                       |
| Warning     | 主色的相邻色相方向（通常黄/橙），饱和度适中                             |
| Error       | 主色的互补色方向（通常红/洋红），饱和度适中                             |
| GaugeGreen  | 同 Success 方向的绿色，仪表专用，可稍亮                           |
| GaugeYellow | 同 Warning 方向的黄色，仪表专用                               |
| GaugeRed    | 同 Error 方向的红色，仪表专用，可稍亮                             |

## Acceptance Criteria

### AC-1（rule）：ThemePreset record 扩展完成 12 个颜色字段

* 证据源：`/workspace/src/MSMC/Features/Settings/Services/ThemePresetRegistry.cs`

* 观察条件：`ThemePreset` record 包含 PrimaryColorHex、AccentColorHex、BackgroundColorHex、CardColorHex、TextColorHex、BorderColorHex、SuccessColorHex、WarningColorHex、ErrorColorHex、GaugeGreenColorHex、GaugeYellowColorHex、GaugeRedColorHex 共 12 个字段

### AC-2（rule）：ApplyPreset 一次性设置全部 12 个颜色通道

* 证据源：`/workspace/src/MSMC/Features/Settings/Services/ThemePresetRegistry.cs` ApplyPreset 方法

* 观察条件：方法体内依次设置 Primary、Accent、Background、Card、Text、Border、Success、Warning、Error、GaugeGreen、GaugeYellow、GaugeRed，每个字段非空时赋值

### AC-3（rule）：旧 13 套预设全部移除，新增 7 套新预设且每套都完整覆盖 12 色

* 证据源：`/workspace/src/MSMC/Features/Settings/Services/ThemePresetRegistry.cs` \_all 列表

* 观察条件：\_all 列表元素数量 == 7，每个元素的 Key ∈ {ColorOSBlue, FurinaBlue, Dragonfruit, GreenApple, BloodRed, SunsetYellow, PrecePurple}，且每个元素的 SuccessColorHex/WarningColorHex/ErrorColorHex/GaugeGreenColorHex/GaugeYellowColorHex/GaugeRedColorHex 均非 null

### AC-4（rule）：前端 ThemePreset TypeScript union type 与新 7 key 对齐

* 证据源：`/workspace/src/frontend/src/types/bridge.ts`

* 观察条件：ThemePreset union type 包含恰好 7 个新 key，无旧 key

### AC-5（rule）：ThemePresetsTests 全部更新并通过

* 证据源：`/workspace/src/MSMC.Tests/Services/ThemePresetsTests.cs` + 测试执行结果

* 观察条件：所有硬编码旧 key、预设数量（应为 7 而非 13）的断言更新后，`dotnet test` 通过；新增测试验证每套预设 ApplyPreset 后 12 个颜色通道全部被正确设置

### AC-6（rubric）：7 套方案的取色质量

* 维度：色彩和谐度、参考还原度、可读性、色相多样性

* 刻度：0-2 分

* 低 (0)：颜色堆砌杂乱，无和谐感，可读性差

* 中 (1)：颜色基本可用，部分方案有单调或对比度不足的问题

* 高 (2)：每套方案颜色和谐养眼，参考还原准确，对比度 ≥ 4.5:1，7 套方案色相分布均匀

* 通过阈值：≥ 1.5 分

### AC-7（rule）：编译与构建通过

* 证据源：`dotnet build` 输出 + `npm run build` 输出

* 观察条件：C# 编译无错误，前端 TypeScript 编译无错误且构建产物正常生成

