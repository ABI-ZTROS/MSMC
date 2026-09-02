# Tasks: 快速预设方案大换血 —— 7 套新主题 + 完整 12 色覆盖

## Task 1: ThemePreset record 扩展 + 取色调研生成 7 套完整 12 色方案

**优先级**: high\
**状态**: pending\
**依赖**: 无

### 目标

1. 扩展 `ThemePreset` record，新增 6 个语义/仪表色可选字段
2. 联网调研 7 个参考物取主色，按 OKLCH 规则生成每套方案完整 12 个颜色

### 取色锚点参考（需联网确认精确 HEX）

| 方案 Key         | 中文名       | 参考物                           | Primary 候选 |
| -------------- | --------- | ----------------------------- | ---------- |
| `ColorOSBlue`  | ColorOS 蓝 | OPPO ColorOS 品牌蓝（Find X8 极光蓝） | `#0066FF`  |
| `FurinaBlue`   | 芙宁娜蓝      | 原神 4.2 芙宁娜 Royal Blue 服装      | `#1E3A8A`  |
| `Dragonfruit`  | 火龙果       | 火龙果果实深洋红皮                     | `#C71585`  |
| `GreenApple`   | 青苹果       | 青苹果果实黄绿皮                      | `#9ACD32`  |
| `BloodRed`     | 血红        | 酒红/暗红色调                       | `#722F37`  |
| `SunsetYellow` | 日落黄       | 日落橙黄场景                        | `#FF8C00`  |
| `PrecePurple`  | 普瑞赛斯紫     | 明日方舟普瑞赛斯紫罗兰双眸                 | `#8B5CF6`  |

### 实施步骤

#### Step 1: ThemePreset record 扩展

文件：`/workspace/src/MSMC/Features/Settings/Services/ThemePresetRegistry.cs`

在现有 ThemePreset record 的构造函数参数中，**新增 6 个可选字段**（全部 nullable，默认 null，与现有 BackgroundColorHex/CardColorHex/TextColorHex/BorderColorHex 风格一致）：

```csharp
public record ThemePreset(
    string Key,
    string Label,
    string PrimaryColorHex,
    string AccentColorHex,
    string? BackgroundColorHex = null,
    string? CardColorHex = null,
    string? TextColorHex = null,
    string? BorderColorHex = null,
    // ── 新增 6 个语义/仪表色字段 ──
    string? SuccessColorHex = null,
    string? WarningColorHex = null,
    string? ErrorColorHex = null,
    string? GaugeGreenColorHex = null,
    string? GaugeYellowColorHex = null,
    string? GaugeRedColorHex = null)
```

> 注意：新增字段必须放在现有可选字段之后（可选字段排在末尾），避免破坏现有构造调用签名。但 Task 3 会全部重新 new() 调用，所以顺序不影响编译兼容性。

#### Step 2: 联网调研确认 7 个主色 HEX

对每个方案，联网搜索参考物的精确颜色值：

* ColorOS 蓝 → OPPO 官网品牌规范/Find X8 产品图取色

* 芙宁娜蓝 → 原神官方角色立绘/Wiki 取色（确认 Royal Blue 精确 HEX）

* 火龙果 → 火龙果高清照片取色（深洋红皮）

* 青苹果 → 青苹果高清照片取色（黄绿皮）

* 血红 → 酒红/暗红参考色板取色

* 日落黄 → 日落场景高清照片取色（橙黄主色）

* 普瑞赛斯紫 → 明日方舟普瑞赛斯角色立绘取色（紫罗兰双眸）

#### Step 3: 为每套方案生成完整 12 色

使用 spec.md 中定义的 OKLCH 衍生规则，为每个方案生成完整 12 个颜色 HEX 值（全部 6 个语义/仪表色必须有值，不允许 null）。生成工具可选：

* OKLCH 在线调色板生成器（如 oklch.com、evilmartians.com 工具）

* 使用项目内已有的 `/workspace/src/frontend/src/utils/color/oklch.ts` 或 `/workspace/src/MSMC/Features/Settings/Colors/OkLchColor.cs` 辅助计算

* 参考 Material Design 3 color roles 映射

每套方案最终产出格式示例：

```csharp
new(
    Key: "ColorOSBlue",
    Label: "ColorOS 蓝",
    PrimaryColorHex: "#0066FF",
    AccentColorHex: "#FF6B81",
    BackgroundColorHex: "#050B1A",
    CardColorHex: "#0E1C35",
    TextColorHex: "#E6EFFC",
    BorderColorHex: "#24406E",
    SuccessColorHex: "#10B981",
    WarningColorHex: "#F59E0B",
    ErrorColorHex: "#EF4444",
    GaugeGreenColorHex: "#22C55E",
    GaugeYellowColorHex: "#EAB308",
    GaugeRedColorHex: "#F43F5E"),
```

### Test Requirements

#### TR-T1-1（rule）：ThemePreset record 包含 12 个颜色字段

* 观察条件：编译无错误，`dotnet build` 通过

* 证据源：`ThemePresetRegistry.cs` 中 ThemePreset record 构造签名

#### TR-T1-2（rubric）：取色质量

* 维度：色彩和谐度、参考还原度、对比度、色相多样性

* 刻度：0-2 分

* 低 (0)：颜色杂乱无和谐感，对比度严重不足

* 中 (1)：颜色基本可用，1-2 套方案有单调或对比度不足问题

* 高 (2)：每套方案颜色和谐养眼，还原参考物调性，Text/Background 对比度 ≥ 4.5:1，7 套方案覆盖蓝/紫/红/黄/绿/洋红 6 个色相带

* 证据源：直接目视检查生成的 7 套方案 12 色调色板

* 通过阈值：≥ 1.5 分

***

## Task 2: ApplyPreset 方法扩展，设置全部 12 个颜色通道

**优先级**: high\
**状态**: pending\
**依赖**: Task 1（ThemePreset record 已扩展）

### 目标

修改 `ThemePresetRegistry.ApplyPreset` 方法，在设置 6 个主题色的同时，也设置 6 个语义/仪表色。

### 实施步骤

文件：`/workspace/src/MSMC/Features/Settings/Services/ThemePresetRegistry.cs` ApplyPreset 方法

在现有代码块：

```csharp
if (!string.IsNullOrEmpty(preset.BorderColorHex))
    service.BorderColor = ParseHexSafe(preset.BorderColorHex);
```

之后，新增 6 个语义/仪表色的赋值块：

```csharp
// ── 新增：语义色（Success/Warning/Error） ──
if (!string.IsNullOrEmpty(preset.SuccessColorHex))
    service.SuccessColor = ParseHexSafe(preset.SuccessColorHex);
if (!string.IsNullOrEmpty(preset.WarningColorHex))
    service.WarningColor = ParseHexSafe(preset.WarningColorHex);
if (!string.IsNullOrEmpty(preset.ErrorColorHex))
    service.ErrorColor = ParseHexSafe(preset.ErrorColorHex);

// ── 新增：仪表色（GaugeGreen/GaugeYellow/GaugeRed） ──
if (!string.IsNullOrEmpty(preset.GaugeGreenColorHex))
    service.GaugeGreenColor = ParseHexSafe(preset.GaugeGreenColorHex);
if (!string.IsNullOrEmpty(preset.GaugeYellowColorHex))
    service.GaugeYellowColor = ParseHexSafe(preset.GaugeYellowColorHex);
if (!string.IsNullOrEmpty(preset.GaugeRedColorHex))
    service.GaugeRedColor = ParseHexSafe(preset.GaugeRedColorHex);
```

### Test Requirements

#### TR-T2-1（rule）：ApplyPreset 后 12 个颜色通道全部被正确设置

* 观察条件：对任意一套新预设调用 ApplyPreset 后，ThemeService 的 12 个颜色属性值与预设一致

* 证据源：可通过新写的单元测试验证（在 Task 5 中补充）

***

## Task 3: ThemePresetRegistry 删除旧 13 套 + 新增 7 套完整 12 色预设

**优先级**: high\
**状态**: pending\
**依赖**: Task 1（ThemePreset 字段已扩展且 7 套完整 12 色方案已生成）、Task 2（ApplyPreset 已扩展）

### 目标

将 `ThemePresetRegistry._all` 列表中的 13 套旧预设全部删除，替换为 Task 1 中生成的 7 套新预设。

### 实施步骤

文件：`/workspace/src/MSMC/Features/Settings/Services/ThemePresetRegistry.cs`

#### Step 1: 删除全部 13 套旧预设

删除注释 `// ── 原有 5 套（保持向后兼容...）──` 开头到 `// ── README L3 品牌系统新增 8 套 ──` 注释块内的全部 13 个 `new(...)` 调用。

#### Step 2: 插入 7 套新预设

将 Task 1 Step 3 中生成的 7 套完整 12 色方案替换到 `_all` 列表中。每套方案全部 12 个颜色字段都必须有值（不允许任何一个 SuccessColorHex/WarningColorHex/.../GaugeRedColorHex 为 null）。

#### Step 3: 更新文件头部注释

将文件开头的功能描述（行 58-64）更新为反映 7 套新方案：

```
/// <summary>
/// 7 套主题预设注册表
/// </summary>
/// <remarks>
/// 7 套品牌/参考主题：
/// ColorOS 蓝 / 芙宁娜蓝（原神 Furina Royal Blue）/ 火龙果 / 青苹果 /
/// 血红（酒红）/ 日落黄（橙黄）/ 普瑞赛斯紫（明日方舟 Prece 紫瞳）
/// 每套预设覆盖全部 12 个颜色通道（6 主题 + 6 语义/仪表）
/// </remarks>
```

### Test Requirements

#### TR-T3-1（rule）：\_all 列表恰好 7 套预设，且每套 12 个颜色字段全部非 null

* 观察条件：`ThemePresetRegistry.GetAllPresets().Count == 7`，遍历每套预设验证 SuccessColorHex/WarningColorHex/ErrorColorHex/GaugeGreenColorHex/GaugeYellowColorHex/GaugeRedColorHex 均为非 null 且为合法 HEX

* 证据源：`ThemePresetRegistry.cs` 源码 + 单元测试

#### TR-T3-2（rule）：7 套预设 Key 与 spec 中定义的一致

* 观察条件：所有 Key ∈ {ColorOSBlue, FurinaBlue, Dragonfruit, GreenApple, BloodRed, SunsetYellow, PrecePurple}

* 证据源：`ThemePresetRegistry.cs` 源码

***

## Task 4: 前端 ThemePreset TypeScript union type 更新

**优先级**: medium\
**状态**: pending\
**依赖**: Task 1（C# 侧 7 个新 Key 字符串已确定）

### 目标

将前端 `ThemePreset` TypeScript union type 从 13 个旧 key 替换为 7 个新 key。

### 实施步骤

文件：`/workspace/src/frontend/src/types/bridge.ts`

找到第 382-395 行的 `ThemePreset` type 定义：

```typescript
export type ThemePreset =
  | 'SkyBlue'
  | 'OceanBlue'
  | 'BlueOrange'
  | 'TealPink'
  | 'RedYellow'
  | 'ColorOSBlue'
  | 'AquarioCyan'
  | 'AuroraPurple'
  | 'SunsetOrange'
  | 'MintGreen'
  | 'SakuraPink'
  | 'MidnightGold'
  | 'ArcticGray'
```

替换为：

```typescript
export type ThemePreset =
  | 'ColorOSBlue'
  | 'FurinaBlue'
  | 'Dragonfruit'
  | 'GreenApple'
  | 'BloodRed'
  | 'SunsetYellow'
  | 'PrecePurple'
```

### Test Requirements

#### TR-T4-1（rule）：前端 TypeScript 编译通过

* 观察条件：`npm run build` 无类型错误

* 证据源：`npm run build` 输出

#### TR-T4-2（rule）：PresetInfo/PresetsResponse 接口保持兼容

* 观察条件：`PresetInfo` 接口中 `key: ThemePreset` 与新 union type 对齐，`getPresets()` 桥接调用返回的数据能正确映射到新类型

* 证据源：`npm run build` + 运行时下拉列表渲染正常

***

## Task 5: ThemePresetsTests 全部更新 + 新增 12 色完整覆盖测试

**优先级**: high\
**状态**: pending\
**依赖**: Task 1（新 ThemePreset record 结构确定）、Task 2（ApplyPreset 扩展）、Task 3（7 套新预设已注册）

### 目标

更新 ThemePresetsTests.cs 中所有硬编码旧预设 key、预设数量、颜色数量的断言；新增测试验证 ApplyPreset 后 12 个颜色通道全部被正确设置。

### 实施步骤

文件：`/workspace/src/MSMC.Tests/Services/ThemePresetsTests.cs`

#### Step 1: 更新预设数量断言

* `ThemeService_GetAllPresets_ReturnsAtLeast13DistinctPresets`：`Assert.True(presets.Count >= 13)` → `Assert.True(presets.Count >= 7)`（或精确 `Assert.Equal(7, presets.Count)`）

* `EachPreset_HasDistinctPrimaryAndAccentColors`：`primaries.Count >= 10` → `primaries.Count >= 5`（7 套去重后主色至少 5 种）

#### Step 2: 重写 `PresetNames_MatchReadmeMarketingNames` 测试

删除全部 13 个旧 key 的 expected 数组，替换为 7 个新 key：

```csharp
var expected = new[]
{
    "ColorOSBlue",    // ColorOS 蓝
    "FurinaBlue",     // 芙宁娜蓝
    "Dragonfruit",    // 火龙果
    "GreenApple",     // 青苹果
    "BloodRed",       // 血红
    "SunsetYellow",   // 日落黄
    "PrecePurple",    // 普瑞赛斯紫
};
```

#### Step 3: 重写 `MissingPresetNames` 辅助方法中的硬编码数组

替换 13 个旧 key 为 7 个新 key。

#### Step 4: 新增 12 色完整覆盖测试

在 `EveryPreset_WhenApplied_SetsAllSixColors` 测试之后，新增测试：

```csharp
[Fact]
public void EveryPreset_HasCompleteTwelveColorScheme()
{
    var presets = ThemePresetRegistry.GetAllPresets();
    Assert.Equal(7, presets.Count);

    foreach (var p in presets)
    {
        // 6 个语义/仪表色字段必须全部非 null 且为合法 HEX
        Assert.NotNull(p.SuccessColorHex);
        Assert.NotNull(p.WarningColorHex);
        Assert.NotNull(p.ErrorColorHex);
        Assert.NotNull(p.GaugeGreenColorHex);
        Assert.NotNull(p.GaugeYellowColorHex);
        Assert.NotNull(p.GaugeRedColorHex);

        Assert.True(Regex.IsMatch(p.SuccessColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"));
        Assert.True(Regex.IsMatch(p.WarningColorHex!, "^#(?:[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$"));
        // ... 其余 4 个
    }
}
```

#### Step 5: 新增 ApplyPreset 后验证 12 色的测试

重写 `EveryPreset_WhenApplied_SetsAllSixColors` 为验证 12 色版本，或新增测试覆盖全部 12 个 ThemeService 属性：

```csharp
[Fact]
public void EveryPreset_WhenApplied_SetsAllTwelveColors()
{
    var presets = ThemePresetRegistry.GetAllPresets();
    foreach (var p in presets)
    {
        var svc = new ThemeService();
        ThemePresetRegistry.ApplyPreset(svc, p.Key);

        // 验证 6 个主题色（与现有测试风格一致）
        Assert.Equal(p.PrimaryColor, svc.PrimaryColor);
        Assert.Equal(p.AccentColor, svc.AccentColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.BackgroundColorHex!)!, svc.BackgroundColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.CardColorHex!)!, svc.CardColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.TextColorHex!)!, svc.TextColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.BorderColorHex!)!, svc.BorderColor);

        // 验证 6 个语义/仪表色（新增）
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.SuccessColorHex!)!, svc.SuccessColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.WarningColorHex!)!, svc.WarningColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.ErrorColorHex!)!, svc.ErrorColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.GaugeGreenColorHex!)!, svc.GaugeGreenColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.GaugeYellowColorHex!)!, svc.GaugeYellowColor);
        Assert.Equal((Color)ColorConverter.ConvertFromString(p.GaugeRedColorHex!)!, svc.GaugeRedColor);
    }
}
```

### Test Requirements

#### TR-T5-1（rule）：全部单元测试通过

* 观察条件：`dotnet test` 退出码 0，无测试失败

* 证据源：`dotnet test` 输出

***

## Task 6: 编译与构建验证

**优先级**: high\
**状态**: pending\
**依赖**: Task 1-5 全部完成

### 目标

验证 C# 后端和 TypeScript 前端编译构建均成功通过。

### 实施步骤

#### Step 1: C# 编译

```bash
cd /workspace/src && dotnet build MSMC.sln -c Release
```

#### Step 2: C# 测试

```bash
cd /workspace/src && dotnet test MSMC.sln -c Release
```

#### Step 3: 前端构建

```bash
cd /workspace/src/frontend && npm run build
```

### Test Requirements

#### TR-T6-1（rule）：C# 编译通过

* 观察条件：`dotnet build` 退出码 0，无错误

* 证据源：构建输出

#### TR-T6-2（rule）：前端构建通过

* 观察条件：`npm run build` 退出码 0，无 TypeScript 错误

* 证据源：构建输出

#### TR-T6-3（rule）：C# 测试通过

* 观察条件：`dotnet test` 退出码 0，所有测试通过

* 证据源：测试输出

***

## Completion Evidence Schema

每个 Task 完成后填写：

```markdown
### Completion Evidence
- 执行命令：`[command]`
- 退出码：`[code]`
- 关键输出摘要：`[first N lines of output that confirm success]`
- 代码修改文件列表：
  - `path/to/file.cs`
  - `path/to/file.ts`
```

## Task Priority Summary

| Task   | Priority | Reason                              |
| ------ | -------- | ----------------------------------- |
| Task 1 | high     | 所有后续任务的基础，取色质量直接影响用户体验              |
| Task 2 | high     | ApplyPreset 扩展是"快速方案颜色修改不完全"问题的根因修复 |
| Task 3 | high     | 替换预设列表是用户明确要求                       |
| Task 4 | medium   | 前端类型更新可与 C# 改动并行，但不阻塞后端逻辑           |
| Task 5 | high     | 测试验证是 CI 必过项，必须同步更新                 |
| Task 6 | high     | 最终验证关卡                              |

