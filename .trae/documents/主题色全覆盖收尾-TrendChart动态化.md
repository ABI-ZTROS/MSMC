# 主题色全覆盖收尾 — TrendChart 动态化 实施计划

> **背景:** 原计划 `/workspace/.trae/documents/检测淡入淡出与主题色全覆盖.md` 共 8 个 Task。经核查，Task 1-6 已完成，Task 7 中 [GaugeRingControl.cs](file:///workspace/src/McServerGuard/Views/Controls/GaugeRingControl.cs) 已完成（`TryFindResource` 动态读取 + `Loaded += InvalidateVisual`），但 [TrendChartControl.cs](file:///workspace/src/McServerGuard/Views/Controls/TrendChartControl.cs) 尚未修改，Task 8 验证未做。

**Goal:** 完成原计划剩余工作，让趋势图控件真正跟随主题色变化，彻底实现"自定义颜色覆盖到整个软件，不要硬编码"。

---

## 探勘发现（关键）

1. **TrendChartControl.cs 未修改** — `LineColorProperty` 默认值仍是硬编码 `Color.FromArgb(255, 123, 31, 162)`（#7B1FA2，[第 52 行](file:///workspace/src/McServerGuard/Views/Controls/TrendChartControl.cs#L50-L52)），构造函数（[第 69-75 行](file:///workspace/src/McServerGuard/Views/Controls/TrendChartControl.cs#L69-L75)）无 `Loaded` 处理。

2. **原计划遗漏的 StaticResource 问题** — [SystemMonitorPage.xaml](file:///workspace/src/McServerGuard/Views/SystemMonitorPage.xaml) 两处 `TrendChartControl` 都显式绑定 `LineColor`：
   - [第 119 行](file:///workspace/src/McServerGuard/Views/SystemMonitorPage.xaml#L118-L119): `LineColor="{StaticResource GaugeGreenBrush}"`（CPU 图，语义色固定，StaticResource 可接受但 DynamicResource 更一致）
   - [第 141 行](file:///workspace/src/McServerGuard/Views/SystemMonitorPage.xaml#L140-L141): `LineColor="{StaticResource AccentTextBrush}"`（内存图，**关键问题**）

   [ThemeService.UpdateResources()](file:///workspace/src/McServerGuard/Services/ThemeService.cs#L125) 每次改主题都执行 `resources["AccentTextBrush"] = new SolidColorBrush(_accentColor);` —— **替换为新对象**。`StaticResource` 在加载时解析一次、持有旧对象引用，因此内存图的折线颜色**不会**随强调色变化。这是"自定义颜色覆盖整个软件"要求的真实缺口。

3. **GaugeRingControl.cs 已完成** — 已有 `Loaded += (_, _) => InvalidateVisual();`（[第 97 行](file:///workspace/src/McServerGuard/Views/Controls/GaugeRingControl.cs#L97)）和 `GetTrackBrush()`/`GetLabelBrush()`/`GetWhiteBrush()` 动态读取方法，无需再动。

---

## Task 1: TrendChartControl.cs — LineColor 默认值动态化 + OnLoaded 重绘

**Files:**
- Modify: `/workspace/src/McServerGuard/Views/Controls/TrendChartControl.cs`

**What/Why:** `LineColorProperty` 默认值硬编码 #7B1FA2 是用户明确要求消除的硬编码颜色。改为在 `OnLoaded` 时从 `PrimaryHueMidBrush` 读取并 `SetValue`，让未显式绑定 LineColor 的未来用法也跟随主色。同时加 `Loaded` 触发 `InvalidateVisual()`，与 GaugeRingControl 对称，确保主题资源就绪后重绘。

**How:**

1. 在构造函数（[第 69-75 行](file:///workspace/src/McServerGuard/Views/Controls/TrendChartControl.cs#L69-L75)）末尾、`AddVisualChild(_visual);` 之后添加：
   ```csharp
   Loaded += OnLoaded;
   ```
   并新增私有方法（放在 `OnCollectionChanged` 之后、`OnRender` 之前）：
   ```csharp
   private void OnLoaded(object? sender, RoutedEventArgs e)
   {
       // 若调用方未显式绑定 LineColor，则让默认线色跟随主色（消除硬编码 #7B1FA2）
       var binding = BindingOperations.GetBinding(this, LineColorProperty);
       if (binding == null)
       {
           if (TryFindResource("PrimaryHueMidBrush") is Brush primaryBrush)
               SetValue(LineColorProperty, primaryBrush);
       }
       InvalidateVisual();
   }
   ```
   需在文件顶部 `using` 区补充 `using System.Windows.Data;`（`BindingOperations` 所在命名空间）。

2. **保留** `LineColorProperty` 注册时的默认值不变（`CreateFrozenBrush(Color.FromArgb(255, 123, 31, 162))`）—— 该值仅在控件未加入逻辑树前作回退，`OnLoaded` 会立即覆盖它。删除默认值会让依赖属性注册变复杂，得不偿失。（注：实际两处 XAML 用法都显式绑定 LineColor，此默认值不会被看到，但保留可避免注册期空引用。）

3. **保留** `GridPenBrush`/`AxisLabelBrush`/`NoDataBrush`/`HaloBrush` 为静态（中性色，暗色主题下合理，与原计划一致）。

**关键点:** `BindingOperations.GetBinding(this, LineColorProperty)` 返回 null 表示该属性未被 XAML 绑定（仅用默认值），此时才用 PrimaryHueMidBrush 覆盖；若已被绑定（如 SystemMonitorPage 两处），则尊重绑定、不覆盖，避免与 XAML 绑定冲突。

---

## Task 2: SystemMonitorPage.xaml — LineColor 改 DynamicResource 让主题变化生效

**Files:**
- Modify: `/workspace/src/McServerGuard/Views/SystemMonitorPage.xaml`

**What/Why:** 原计划 Task 6 只替换了 SystemMonitorPage 的硬编码颜色（#4CAF50 → GaugeGreenBrush），未处理 `LineColor` 的 `StaticResource` 绑定。`AccentTextBrush` 被 ThemeService 替换为新对象，StaticResource 持旧引用，内存图折线不跟随强调色——必须改 DynamicResource。

**How:**

1. [第 119 行](file:///workspace/src/McServerGuard/Views/SystemMonitorPage.xaml#L118-L119) CPU 图：
   ```xml
   LineColor="{DynamicResource GaugeGreenBrush}"
   ```
   （GaugeGreenBrush 是固定语义色，StaticResource 本可工作，但改为 DynamicResource 与第 141 行保持一致，且对未来亮色主题更稳健。零风险。）

2. [第 141 行](file:///workspace/src/McServerGuard/Views/SystemMonitorPage.xaml#L140-L141) 内存图：
   ```xml
   LineColor="{DynamicResource AccentTextBrush}"
   ```
   （**关键修复**：AccentTextBrush 随强调色变化且被替换为新对象，必须 DynamicResource 才能跟随。）

**注意:** 改为 DynamicResource 后，主题切换时 `LineColorProperty` 会自动收到新 Brush，因注册时带 `AffectsRender` 标志会自动重绘，无需 InvalidateVisual。

---

## Task 3: 验证编译 + 资源键一致性

**Files:** 无（验证步骤）

**How:**
1. `cd /workspace && dotnet build src/McServerGuard/McServerGuard.csproj` —— Linux 因无 WPF runtime pack 会报 NETSDK1082，忽略该类错误；关注 CS#### 编译错误（应为 0）。
2. 确认 `using System.Windows.Data;` 已添加（`BindingOperations` 编译依赖）。
3. 确认 `PrimaryHueMidBrush` 在 MaterialDesign 主题中存在（MaterialDesignInXaml 默认提供，已验证 AppResources.xaml 引用其 6 次）。
4. 确认 `GaugeGreenBrush`、`AccentTextBrush` 均在 AppResources.xaml 或 ThemeService.UpdateResources() 中定义（已验证：AccentTextBrush 在 ThemeService 第 125 行覆盖，GaugeGreenBrush 在 AppResources）。

---

## Assumptions & Decisions

1. **保留 LineColorProperty 注册默认值** #7B1FA2 不删：仅在 `OnLoaded` 时用 PrimaryHueMidBrush 覆盖（仅当未被绑定时）。删除默认值会增加依赖属性注册复杂度，且实际两处用法都显式绑定、看不到默认值，收益为零。
2. **用 `BindingOperations.GetBinding` 判断是否已绑定**：避免 OnLoaded 的 SetValue 与 XAML 的显式绑定冲突（SetValue 会覆盖绑定，导致主题变化不再生效）。
3. **第 119 行 GaugeGreenBrush 也改 DynamicResource**：虽为固定语义色、StaticResource 本可工作，但统一为 DynamicResource 降低认知负担、为未来亮色主题留余地，零风险。
4. **不动 GaugeRingControl.cs**：已完成，无需重复劳动。
5. **不动其他 XAML 页面**：Task 5/6 已完成，无残留硬编码（SettingsPage 预设色板 HEX 是数据本身，合理保留）。

## Verification Steps

1. 编译：`dotnet build` 无 CS#### 错误（NETSDK1082 忽略）。
2. 主题切换（需 Windows 运行时验证，Linux 无法跑 WPF）：
   - 改强调色 → 内存图折线颜色应立即变化（Task 2 修复点）
   - 改主色 → 若有未绑定 LineColor 的 TrendChartControl，折线应跟随主色（Task 1 修复点）
3. 无回归：CPU 图仍为绿色（GaugeGreenBrush 语义色不变），仪表盘绿/黄/红不变。
