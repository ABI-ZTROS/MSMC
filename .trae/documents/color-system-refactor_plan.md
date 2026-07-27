# 配色系统重构 + 圆形调色盘 实现计划

## 一、问题总结与设计目标

### 1.1 现有问题
1. **颜色格式混乱**：后端 `Color.Hex` 返回 `#AARRGGBB`（WPF Color 格式），前端 CSS/预设用 `#RRGGBB`，数据接口无明确约定，导致"选蓝色变粉色"
2. **色阶算法简陋**：`lighten`/`darken` 是 RGB 线性混合，深色模式下饱和度漂移、视觉不均匀
3. **转换逻辑碎片化**：`argbToRgb`、`normalizeHex`、`formatHex` 分散在各页面，每个页面自己处理，遗漏即出错
4. **调色盘能力不足**：仅 RGB 滑块，无 HSV 色轮、无色相/饱和度/明度调节
5. **前后端色阶不一致**：WPF 端用 MaterialDesignThemes 调色，前端 JS 自己 lighten/darken，两端结果有差异
6. **CSS 变量命名混乱**：`--md-accent-gradient-*` 实际用主色生成，与强调色无关

### 1.2 设计目标
- **统一格式**：前后端数据接口统一使用 `#RRGGBB`（6位），Alpha 通道独立处理
- **高质量色阶**：基于 OKLCH 均匀色彩空间生成色阶，视觉感知一致
- **单一真相源**：颜色工具集中管理，页面直接消费，不做自行转换
- **完整调色体验**：HSV 圆形色轮 + 明度滑块 + RGB/HEX 输入 + 预设色板
- **前后端一致**：核心色阶算法两端对齐，WPF 和 WebView 显示无差异

## 二、架构设计

### 2.1 颜色格式约定

| 层级 | 格式 | 示例 | 说明 |
|------|------|------|------|
| **数据接口（桥接）** | `#RRGGBB` | `#3B82F6` | 6位十六进制，大写，无 Alpha |
| **内部存储（WPF Color）** | `Color` 结构 | - | 带 A/R/G/B 通道，Alpha=255 |
| **持久化（JSON）** | `#AARRGGBB` | `#FF3B82F6` | 8位十六进制，与现有 ThemeSettings 兼容 |
| **CSS 变量** | `#RRGGBB` / `rgba()` | `#3B82F6` / `rgba(59,130,246,0.1)` | 标准 Web 格式 |

**关键决策**：桥接层做格式转换，对外统一 `#RRGGBB`。内部持久化保持 `#AARRGGBB` 兼容旧数据。

### 2.2 OKLCH 色阶生成

从单个基础色生成 5 级色阶（lighter / light / mid / dark / darker）：

```
更亮 ←———————————————— 基础色 ———————————————→ 更暗
lighter   light       mid        dark      darker
L+18%    L+9%         L0        L-12%     L-22%
C-10%    C-5%         C0        C+8%      C+12%
```

- **L（明度）**：向两端扩展，暗色端跨度更大（深色模式需要更多暗部层次）
- **C（色度/饱和度）**：亮色端略降饱和度避免刺眼，暗色端略升饱和度保持存在感
- **H（色相）**：保持不变

### 2.3 CSS 变量重构

**主色色阶**（从基础色 OKLCH 生成）：
- `--md-primary-50` ~ `--md-primary-900`：9级色阶（类 Tailwind 命名，更通用）
- `--md-primary`：主色基础值（= `--md-primary-500`）

**语义化颜色**（基于色阶派生）：
- `--md-primary-bg` / `--md-primary-border` / `--md-primary-text`
- `--md-success` / `--md-warning` / `--md-error`
- `--md-surface-*`：背景/卡片/边框表面色阶

**导航变量**：复用主色色阶，不单独定义

### 2.4 圆形调色盘组件

前端 HSV 色轮组件结构：
```
┌─────────────────────────┐
│  ○ 色轮（色相+饱和度）    │ ← 圆形画布，点击/拖动选色
│     ◉ 选中点            │
├─────────────────────────┤
│  █████████████████████  │ ← 明度滑块（V）
├─────────────────────────┤
│ 预览色 | HEX输入框       │
├─────────────────────────┤
│ R: [---]  G: [---]  B: [---] │ ← RGB数值显示/微调
│ H: [---]  S: [---]  V: [---] │ ← HSV数值显示
└─────────────────────────┘
```

## 三、文件清单与改动范围

### 3.1 新增文件

| 文件 | 职责 |
|------|------|
| `src/McServerGuard/Services/Color/OkLchColor.cs` | OKLCH 色彩空间转换 + 色阶生成（C# 版） |
| `src/McServerGuard/Services/Color/ColorHelper.cs` | 颜色格式转换、HEX 解析/格式化、ARGB↔RGB 互转 |
| `src/frontend/src/utils/color/oklch.ts` | OKLCH 色彩空间转换 + 色阶生成（TS 版） |
| `src/frontend/src/utils/color/index.ts` | 颜色工具统一出口：格式转换、色阶生成、调色计算 |
| `src/frontend/src/components/ui/ColorWheel.tsx` | HSV 圆形色轮组件（Canvas 绘制） |
| `src/frontend/src/components/ui/ColorPicker.tsx` | 完整调色盘组件（色轮 + 明度滑块 + HEX/RGB/HSV 输入） |

### 3.2 修改文件

| 文件 | 改动内容 |
|------|---------|
| `src/frontend/src/utils/theme.ts` | 重写：使用 OKLCH 生成色阶，统一颜色入口，移除碎片化转换 |
| `src/frontend/src/styles/theme.css` | CSS 变量重命名与整理：引入 9 级色阶命名，合并冗余变量 |
| `src/frontend/src/pages/SettingsPage.tsx` | 替换为新 ColorPicker 组件，移除本地颜色转换逻辑 |
| `src/frontend/src/types/bridge.ts` | `SettingsData` 颜色字段统一为 `#RRGGBB` 格式 |
| `src/McServerGuard/Views/MainWindow.xaml.cs` | 桥接 API 颜色格式统一：入参出参都转 `#RRGGBB` |
| `src/McServerGuard/ViewModels/SettingsViewModel.cs` | 新增 RGB/HEX 统一属性，优化颜色绑定 |
| `src/McServerGuard/Services/ThemeService.cs` | 集成 OKLCH 色阶生成，统一主题色应用逻辑 |
| `src/McServerGuard/Views/Controls/ColorPickerControl.xaml` | WPF 端也升级为圆形色轮（可选，本期前端优先） |
| `src/McServerGuard/Views/Controls/ColorPickerControl.xaml.cs` | 同上 |

### 3.3 废弃/清理

- `argbToRgb` 函数：保留在 color utils 中作为内部工具，但页面不再直接调用
- `normalizeHex` / `formatHex` / `isValidHex`：收编到 color utils 统一管理
- `--md-accent-gradient-start` 等命名混乱的变量：重命名或合并

## 四、实现步骤（共 7 步）

### Step 1：颜色工具库 — 底层能力建设

**目标**：建立统一的颜色工具层，两端对齐 OKLCH 算法。

**前端**：
- 实现 `rgbToOklch(r, g, b)` → `{l, c, h}`
- 实现 `oklchToRgb(l, c, h)` → `{r, g, b}`
- 实现 `generateTints(baseHex, count)` → 生成 N 级色阶数组
- 实现 `hexToRgb` / `rgbToHex` / `hexToHsv` / `hsvToHex`
- 实现 `isValidHex` / `normalizeHex` （收编现有逻辑）
- 统一出口：`src/utils/color/index.ts`

**后端**：
- `ColorHelper.cs`：HEX 解析格式化、ARGB↔RGB 转换、颜色空间转换工具
- `OkLchColor.cs`：OKLCH 结构体、与 RGB/SRGB 互转、色阶生成

**验证**：两端同一输入生成同一色阶，差值 < 2（四舍五入误差）。

### Step 2：主题系统重构 — CSS 变量与应用逻辑

**目标**：用 OKLCH 色阶替代现有 lighten/darken，整理 CSS 变量命名。

**CSS 变量**：
```css
--md-primary-50: ...;   /* 最亮 */
--md-primary-100: ...;
--md-primary-200: ...;
--md-primary-300: ...;
--md-primary-400: ...;
--md-primary-500: ...;  /* 基础色 = --md-primary */
--md-primary-600: ...;
--md-primary-700: ...;
--md-primary-800: ...;
--md-primary-900: ...;  /* 最暗 */

--md-primary: var(--md-primary-500);
--md-primary-bg: var(--md-primary-500 / 10%);
--md-primary-border: var(--md-primary-500 / 20%);
```

**theme.ts**：
- `applySettingsToCss(settings)` 重写：主色/强调色/背景/文字/边框都用 OKLCH 生成色阶
- 移除 `applyPrimaryColor` 独立函数，合并到统一 apply
- 颜色输入统一走 color utils 的 `normalizeHex`，不再需要页面自己转 ARGB

**theme.css 默认值**：用 OKLCH 重新生成默认色阶，与 CSS 变量新命名对应。

**验证**：默认主题视觉与当前差异不大，色阶过渡更均匀自然。

### Step 3：桥接 API 颜色格式统一

**目标**：桥接层作为格式转换边界，前端永远收到 `#RRGGBB`。

**MainWindow.xaml.cs 修改**：
- `settings:getSettings` 返回值：所有 `*ColorHex` 字段转为 `#RRGGBB`（去掉 Alpha）
- `settings:setPrimaryColor` 等入参：接收 `#RRGGBB`，内部加 FF Alpha 再存
- `settings:getPrimarySwatches` / `settings:getAccentSwatches`：保持 `#RRGGBB`（已经是）
- `settings:getPresets`：保持 `#RRGGBB`（已经是）

**types/bridge.ts**：
- 注释明确：所有颜色字段为 `#RRGGBB` 格式（6位十六进制）

**验证**：前端 getSettings 后直接把颜色值当 CSS 用，不需要任何转换。

### Step 4：圆形色轮组件 — ColorWheel

**目标**：实现可交互的 HSV 圆形色轮。

**技术方案**：
- 用 Canvas 2D 绘制色轮（径向渐变 = 饱和度，角度 = 色相）
- 鼠标/触摸事件：点击或拖动时，根据坐标计算角度（H）和半径（S）
- 圆形选区指示器：当前选中点位置

**Props**：
```ts
interface ColorWheelProps {
  color: string           // 当前颜色 #RRGGBB
  onChange?: (hex: string) => void  // 实时变化
  onChangeEnd?: (hex: string) => void // 松手时
  size?: number           // 直径，默认 200
}
```

**验证**：拖动选点流畅，选中颜色与指示器位置对应准确。

### Step 5：完整调色盘组件 — ColorPicker

**目标**：组合色轮 + 明度滑块 + 数值输入 = 完整调色体验。

**组件结构**：
```
ColorPicker
├── ColorWheel (H+S)
├── Slider (明度 V)
├── 颜色预览 + HEX 输入框
├── RGB 数值显示（只读或可微调）
└── HSV 数值显示（只读）
```

**Props**：
```ts
interface ColorPickerProps {
  value: string
  onChange?: (hex: string) => void
  onChangeEnd?: (hex: string) => void
  showRgb?: boolean       // 显示 RGB 数值
  showHsv?: boolean       // 显示 HSV 数值
  presets?: string[]      // 预设快捷色
}
```

**验证**：色轮、滑块、HEX 输入三者联动同步，无延迟、无错位。

### Step 6：设置页重构 — 使用新组件

**目标**：SettingsPage 简化，颜色相关逻辑全部交给 ColorPicker 组件。

**改动**：
- 移除本地 `normalizeHex` / `formatHex` / `isValidHex`
- 移除 `argbToRgb` 导入（桥接层已统一格式）
- 主色/强调色替换为 ColorPicker 组件
- `primaryColorHex` / `accentColorHex` 直接从 settings 取，不再转换
- `handleSetPrimary` / `handleSetAccent` 简化：直接传值给桥接 API

**验证**：设置页功能与之前一致，但代码量减少，颜色显示正确。

### Step 7：后端色阶对齐 + WPF 端升级（可选增强）

**目标**：WPF 端主题色也用 OKLCH 生成，与前端视觉一致。

**ThemeService 改动**：
- 用 `OkLchColor.GenerateTints()` 生成主色/强调色色阶
- 应用到 ResourceDictionary 时使用新色阶
- 替代 MaterialDesignThemes 的调色逻辑（或与其共存）

**WPF ColorPicker 升级**：
- 新增圆形色轮（用 WPF WriteableBitmap 或 DrawingVisual 绘制）
- 与前端 ColorPicker 交互逻辑对齐

**验证**：WPF 原生 UI 和 WebView 前端的主题色视觉一致。

## 五、风险与注意事项

| 风险 | 影响 | 应对 |
|------|------|------|
| OKLCH 转 RGB 有少量色域溢出 | 极鲜艳颜色可能略偏 | 裁剪到 sRGB 范围内，并做 gamut mapping 简单处理 |
| CSS 变量改名影响现有页面 | 改动力度大 | 保留旧变量名做别名（指向新变量），逐步迁移 |
| 桥接格式变更影响其他调用方 | 可能有其他地方用了颜色接口 | 全文搜索 `*ColorHex` 使用点，逐一确认 |
| 圆形色轮 Canvas 性能 | 拖动时重绘频繁 | 用离屏缓存静态色轮图，只更新指示器位置 |
| 旧数据 `#AARRGGBB` 兼容 | 持久化文件已有数据 | 读取时兼容两种格式，写入统一用 `#AARRGGBB` |

## 六、验证清单

- [ ] 默认主题加载正常，色阶均匀自然
- [ ] 选蓝色 → 显示蓝色（不再串味成粉色）
- [ ] 主色/强调色/背景色/文字色/边框色都能正常修改并持久化
- [ ] 刷新页面后颜色设置保留
- [ ] 圆形色轮拖动选色流畅，HEX/RGB/HSV 同步
- [ ] HEX 输入框输入有效颜色后色轮位置同步更新
- [ ] 预设色板点击正确应用
- [ ] 深色模式下色阶视觉层次清晰，无灰蒙蒙感
- [ ] WPF 端与前端颜色显示基本一致（Step 7 后）
