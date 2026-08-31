# Tasks

- [x] Task 1: 后端主题服务扩展 6 个颜色通道（语义色 + 仪表盘色）
  - [x] SubTask 1.1: `ThemeService.cs` 的 `IThemeService` 接口新增 `SuccessColor` / `WarningColor` / `ErrorColor` / `GaugeGreenColor` / `GaugeYellowColor` / `GaugeRedColor` 属性（set 时触发 `ApplyTheme`，模式同 `PrimaryColor`）
  - [x] SubTask 1.2: `ThemeSettings` 新增 6 个 string 字段（默认 `#FF4CAF50` / `#FFFFC107` / `#FFE53935` / `#FF4CAF50` / `#FFFFC107` / `#FFF4364C`）
  - [x] SubTask 1.3: `LoadSettings` / `SaveSettings` / `ResetToDefault` 覆盖 6 个新字段
  - [x] SubTask 1.4: `UpdateResources` 从 6 个新属性派生成功/警告/错误/仪表盘 brush（含 `SuccessSubtleBackgroundBrush`/`WarningSubtleBackgroundBrush`/`DangerSubtleBackgroundBrush` 等新键；`ErrorTextBrush` 从错误色亮化派生；`GaugeGreen/Yellow/RedBrush`、`DangerBrush` 改用属性值），删除硬编码字面量

- [x] Task 2: SettingsViewModel 增加 6 个颜色通道 + SetPreset 重构（修复快速方案失效根因）
  - [x] SubTask 2.1: 新增 6 个 `[ObservableProperty]` Color 字段 + `XxxColorHex`/`XxxColorBrush` 派生属性 + `OnXxxColorChanged` 通知（模式同现有 6 个颜色属性）
  - [x] SubTask 2.2: 新增 6 个 `[RelayCommand] SetXxxColor(string hex)` 命令（模式同 `SetBackgroundColor`）
  - [x] SubTask 2.3: `LoadSettings` / `ApplyTheme` / `ResetToDefault` 同步 6 个新通道
  - [x] SubTask 2.4: `SetPreset` 删除 5-case switch，改为 `ThemePresetRegistry.ApplyPreset(_themeService, preset)`；命中后用 `BeginBatchUpdate`/`EndBatchUpdate` 包裹回读 6 色到 VM；未命中设置失败状态消息

- [x] Task 3: ThemePresetRegistry 预设补全 + ApplyPreset 扩展
  - [x] SubTask 3.1: `ThemePreset` 记录新增 `TextColorHex` / `BorderColorHex`（`string?`）字段
  - [x] SubTask 3.2: `ApplyPreset` 在自定义字段非空时写入 `service.TextColor` / `service.BorderColor`
  - [x] SubTask 3.3: 13 套预设补全 6 色（5 套旧预设文字/边框沿用原 switch 值；8 套新预设补合理值，须为合法 HEX）

- [x] Task 4: 桥接 API（C#）新增颜色 setter + getSettings 扩展
  - [x] SubTask 4.1: `MainWindow.xaml.cs` 设置桥接区注册 10 个新动作：`settings:setBackgroundColor` / `setCardColor` / `setTextColor` / `setBorderColor` / `setSuccessColor` / `setWarningColor` / `setErrorColor` / `setGaugeGreenColor` / `setGaugeYellowColor` / `setGaugeRedColor`（模式同 `setPrimaryColor`，非法 HEX 返回 `success=false`）
  - [x] SubTask 4.2: `settings:get` 返回值新增 `successColorHex` / `warningColorHex` / `errorColorHex` / `gaugeGreenColorHex` / `gaugeYellowColorHex` / `gaugeRedColorHex`（经 `ArgbToRgb`）
  - [x] SubTask 4.3: `settings:setPreset` 按 `ApplyPreset` 返回值返回真实 success（未命中 → `success=false` + error）
  - [x] SubTask 4.4: `settings:getPresets` 保持返回 key/label/primary/accent

- [x] Task 5: 前端主题系统扩展（theme.ts + types/bridge.ts + utils/bridge.ts）
  - [x] SubTask 5.1: `types/bridge.ts` 的 `SettingsData` 新增 6 个 `xxxColorHex` 字段
  - [x] SubTask 5.2: `utils/theme.ts`：`applyStatusColors` 参数化（success/warning/error/gauge×3）；`applySettingsToCss` 消费新 6 字段；subtle 背景/边框、danger、error-text、gauge 系列改为从基色派生（删除硬编码 rgb 字面量）
  - [x] SubTask 5.3: `utils/theme.ts` 新增预览函数 `applyBackgroundColor` / `applyCardColor` / `applyTextColor` / `applyBorderColor` / `applySemanticColors`（模式同 `applyPrimaryColor`）
  - [x] SubTask 5.4: `utils/bridge.ts` 新增 10 个颜色 setter wrapper（模式同 `setPrimaryColor`）

- [x] Task 6: 前端设置页 UI（SettingsPage.tsx）
  - [x] SubTask 6.1: 主题色区扩为 6 个 `ColorPicker`（主/强调 2×2 保留；新增 背景/卡片 一行、文字/边框 一行）
  - [x] SubTask 6.2: 新增「语义与仪表盘色」卡片：成功/警告/错误/仪表盘绿/黄/红 6 个 `ColorPicker`
  - [x] SubTask 6.3: 每个新增调色盘 `onChange` → 本地预览函数、`onChangeEnd` → 桥接 setter + `loadSettings()`（模式同 `handleSetPrimary`）

- [x] Task 7: 契约测试（P5 诚实返回链 + P8 举证）
  - [x] SubTask 7.1: `ThemePresetsTests` 新增：遍历 13 套 key 全部 `ApplyPreset` 后主色/背景色/文字/边框相对预设值落地（用无窗口 `ThemeService` 实例，沿用现有测试方法）
  - [x] SubTask 7.2: `ThemePresetsTests` 新增：所有预设 6 色 HEX 均合法（含新补的 Text/Border）
  - [x] SubTask 7.3: `ThemePresetsTests` 新增：未知 key 调用 `ApplyPreset` 返回 false 且颜色不变

- [x] Task 8: 编译与前端构建验证
  - [x] SubTask 8.1: `dotnet build src/MSMC/MSMC.csproj`（Linux 忽略 NETSDK1082，关 注 CS#### = 0）—— 实际以 `-p:RuntimeIdentifier=win-x64` 编译通过，**0 Warning / 0 Error**，前端 `wwwroot.zip` 重新打包嵌入成功
  - [x] SubTask 8.2: `dotnet test src/MSMC.Tests/MSMC.Tests.csproj` —— 测试项目**编译通过（0 Error）**；net9.0-windows 测试程序集需 WindowsDesktop 运行时，Linux 沙箱无法执行（项目 CI 为 windows-latest，见 Task 9）
  - [x] SubTask 8.3: 前端 `npm run build --prefix src/frontend` 重建 `dist` 成功（`tsc` + vite 均通过，SettingsPage 36.21 kB 新产物含 12 调色盘）
  - [x] SubTask 8.4: 核对 10 个新 setter 动作名在 TS 端（`utils/bridge.ts`）与 C# 端（`MainWindow.xaml.cs`）**逐一完全一致**（diff 为空），`getSettings` 返回字段与 `SettingsData` 接口对齐

- [ ] Task 9: Windows CI 真实执行契约测试（环境受限遗留项）
  - [ ] SubTask 9.1: 推送/PR 触发 GitHub Actions（windows-latest），确认 `ThemePresetsTests` 全部 4+3 个测试在 `dotnet test` 中全绿（本 Linux 沙箱缺 WindowsDesktop 运行时无法执行，编译已 0 Error 兜底）

# Task Dependencies
- [Task 2] 依赖 [Task 1]（VM 属性依赖 IThemeService 新属性）与 [Task 3]（SetPreset 委托 ApplyPreset）
- [Task 3] 无依赖（可与 Task 1 并行）
- [Task 4] 依赖 [Task 2]（桥接调用 VM 命令）与 [Task 3]（setPreset 走 ApplyPreset）
- [Task 5] 依赖 [Task 4]（TS 类型/桥接与 C# 契约对齐）
- [Task 6] 依赖 [Task 5]
- [Task 7] 依赖 [Task 1] 与 [Task 3]
- [Task 8] 依赖全部前序任务
- [Task 9] 依赖 [Task 7]（在 Windows 环境执行已验证的测试）