# 主题系统全量重构计划

## 一、问题分析

用户反馈三大问题：

### 问题 1：按钮大小/字大小/缩放有问题，字显示不完整
- **根因**：MaterialDesign 默认按钮 Padding/Margin 较大，加上 `Space Grotesk Light` 字重偏细，在小按钮里文字容易被裁
- **涉及**：`MaterialDesignRaisedButton` / `MaterialDesignOutlinedButton` / `MaterialDesignFlatButton` 等 MaterialDesign 内置按钮样式，以及自定义 `AnimatedButtonStyle` / `ColorSwatchStyle`
- **现象**：按钮内文字只露半截，特别是中文 + 小字号场景

### 问题 2：圆角控制无法控制所有按钮和控件
- **根因**：
  1. `ThemeService.ApplyTheme()` 只写颜色资源，**完全没写 `CornerRadius` 资源**
  2. `AppResources.xaml` 里大量硬编码 `CornerRadius="12"` / `UniformCornerRadius="12"` / `CornerRadius="10"` / `CornerRadius="8"` 等，不绑定动态资源
  3. MaterialDesign 内置控件（Button/TextBox/Slider/ComboBox 等）的圆角通过 `UniformCornerRadius` 属性控制，但没走主题系统
  4. 导航项 `CornerRadius="10"`、色板 `CornerRadius="14"`、卡片 `UniformCornerRadius="12"`……全是硬编码

### 问题 3：颜色自定义无法完全生效
- **根因**：
  1. `AppResources.xaml` 里大量 `po:Freeze="True"` 的静态笔刷（`#0F172A` / `#3B82F6` 等），不接受运行时覆盖
  2. 部分控件直接写死颜色值（如导航 `SelectedBg` 背景 `#1F3B82F6`）
  3. MaterialDesign 内置主题只覆盖了 `PrimaryColor` / `SecondaryColor`，背景/卡片/文字色是自定义资源，但部分页面内联样式没引用这些资源
  4. `MaterialDesignPaper` / `MaterialDesignBody` 虽然被覆盖了，但 MaterialDesign 内部还有 `MaterialDesignBodyLight` / `MaterialDesignColumnHeader` 等派生色没同步

## 二、总体方案

### 核心策略
1. **建立统一的 `CornerRadius` 资源体系**：定义 `AppCornerRadius` / `AppCornerRadiusSmall` / `AppCornerRadiusLarge` 三级动态资源，ThemeService 运行时更新
2. **全局按钮样式重写**：基于 MaterialDesign 按钮样式，统一 Padding / FontSize / 圆角，解决文字显示不全
3. **颜色资源全量动态化**：所有 `po:Freeze="True"` 的静态笔刷改成 `DynamicResource` 引用，ThemeService 运行时统一写入
4. **控件模板全量走资源**：导航项、色板、卡片、输入框等所有自定义模板里的硬编码颜色/圆角全部替换为动态资源引用

### 改动范围（4 个核心文件 + 各页面 XAML 局部替换）

| 文件 | 改动量 | 说明 |
|---|---|---|
| `Themes/AppResources.xaml` | 大 | 硬编码 CornerRadius → DynamicResource；静态色刷 → DynamicResource；按钮样式优化 |
| `Services/ThemeService.cs` | 中 | `UpdateResources()` 新增 CornerRadius 资源、补齐 MaterialDesign 派生色、确保所有颜色资源被覆盖 |
| `ViewModels/SettingsViewModel.cs` | 小 | 确认 ViewModel → ThemeService 的绑定通路正常 |
| `Views/*.xaml`（各页面） | 小 | 局部硬编码颜色/圆角替换为动态资源（按需） |

## 三、详细变更

### 变更 A：统一圆角资源体系（ThemeService.cs + AppResources.xaml）

**A1 — AppResources.xaml 定义三级圆角资源**
```xml
<CornerRadius x:Key="AppCornerRadius">12</CornerRadius>
<CornerRadius x:Key="AppCornerRadiusSmall">8</CornerRadius>
<CornerRadius x:Key="AppCornerRadiusLarge">16</CornerRadius>
```

**A2 — ThemeService.UpdateResources() 写圆角资源**
- 用 `CornerRadius` 对象写入 `Application.Current.Resources["AppCornerRadius"]` 等
- 三个级别比例：Small = Max(0, CornerRadius - 4)，Large = CornerRadius + 4
- MaterialDesign 控件统一圆角：写入 `MaterialDesignButtonCornerRadius` 等（如果库支持的话），否则用自定义样式覆盖

**A3 — AppResources.xaml 所有硬编码 CornerRadius → DynamicResource**
- `AnimatedCardStyle`：`UniformCornerRadius="12"` → `{DynamicResource AppCornerRadius}`
- `EnhancedCardStyle`：同上
- `CardBorderStyle`：`CornerRadius="12"` → `{DynamicResource AppCornerRadius}`
- `ModernNavListBoxItemStyle`：4 处 `CornerRadius="10"` → `{DynamicResource AppCornerRadiusSmall}`
- `ColorSwatchStyle`：多处 `CornerRadius="14"` / `CornerRadius="15"` → `{DynamicResource AppCornerRadius}`
- `ColorSwatchStyle` 的阴影外环 `CornerRadius="15"` / 外描边 `CornerRadius="14"` → 统一改为 `{DynamicResource AppCornerRadius}`
- 其他页面内联硬编码圆角（SettingsPage.xaml 等）同样替换

### 变更 B：按钮样式优化（AppResources.xaml）

**B1 — 重写全局按钮默认样式**
- 基于 `MaterialDesignRaisedButton` 增加一个 `OptimizedButtonBase` 样式
- 调整 `Padding`：默认 `16,8` → `12,6`（更紧凑但不挤）
- 调整 `FontSize`：与全局正文一致，避免按钮字比正文大
- `UniformCornerRadius` 绑定 `{DynamicResource AppCornerRadius}`
- 确保 `ContentPresenter` 有足够垂直空间，文字不被裁

**B2 — Outlined / Flat 按钮同步优化**
- 对 `MaterialDesignOutlinedButton` 做同样的 Padding/圆角调整
- 对 `MaterialDesignFlatButton` 做同样的调整

**B3 — AnimatedButtonStyle 基于新基样式**
- `BasedOn` 从 `MaterialDesignRaisedButton` 改为新的 `OptimizedButtonBase`
- 保留点击缩放动画

**B4 — ColorSwatchStyle 调整**
- 宽高 28 → 32（色板太小不好点）
- 圆角从 14 → 绑定 `AppCornerRadius`

### 变更 C：颜色资源全量动态化

**C1 — AppResources.xaml 静态 Freeze 笔刷全部改为默认值占位**
- 所有 `po:Freeze="True"` 的自定义笔刷（`CardBackgroundBrush` / `CardHoverBrush` / `NavItemSelectedBrush` 等）保留 x:Key 定义，但这些 Key 会被 ThemeService 运行时覆盖
- XAML 里保留默认值是为了设计时显示 + 首次加载前的 Fallback
- 关键点：ThemeService 已经在 `UpdateResources()` 里通过 `resources["Key"] = brush` 覆盖了，这些静态 Freeze 笔刷在运行时会被替换。但问题是**部分控件模板内联的颜色值没走资源键**。

**C2 — 内联硬编码颜色 → DynamicResource**
- 导航项 `SelectedBg` 的 `Background="#1F3B82F6"` → `{DynamicResource PrimarySubtleBackgroundBrush}`
- 其他模板内的硬编码颜色值同理替换

**C3 — ThemeService 补齐 MaterialDesign 资源覆盖**
- 当前覆盖了：`MaterialDesignPaper` / `MaterialDesignBody` / `MaterialDesignBodyLight`
- 需补充：`MaterialDesignColumnHeader` / `MaterialDesignSubtitleTextBlock` / `MaterialDesignCaptionTextBlock` 等（从 TextColor 派生）
- 确保 `MaterialDesignCardBackground` 也被覆盖（如果存在）

**C4 — 各页面内联硬编码颜色巡检**
- NetworkMonitorPage.xaml / SystemMonitorPage.xaml / ServerDetectionPage.xaml / ConfigEditorPage.xaml
- 重点查 `Background="#"` / `Foreground="#"` / `BorderBrush="#"` 模式
- 替换为对应 DynamicResource 键

### 变更 D：验证项

1. **按钮文字完整**：所有页面按钮（Raised/Outlined/Flat/色板/图标按钮）文字完整显示，无裁剪
2. **圆角统一**：拖动设置页圆角 Slider，所有卡片/按钮/输入框/导航项/色板同步变化
3. **颜色生效**：主色/强调色/背景/卡片/文字/边框 6 个颜色选择器修改后，全应用所有控件同步变色
4. **实时预览**：设置页颜色预览 + 全局效果即时同步，无延迟

## 四、实施顺序

1. Task 1：ThemeService 增加 CornerRadius 资源写入 + 补齐 MaterialDesign 颜色覆盖
2. Task 2：AppResources.xaml 硬编码 CornerRadius → DynamicResource
3. Task 3：AppResources.xaml 按钮样式优化（Padding/字号/圆角）
4. Task 4：AppResources.xaml 内联硬编码颜色 → DynamicResource
5. Task 5：各页面 XAML 巡检，替换剩余硬编码颜色/圆角
6. Task 6：编译验证 + 效果确认
