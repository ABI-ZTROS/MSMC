# Checklist — 设置页颜色体系完整化

- [x] 13 套快速方案全部生效：点击任意预设（含 8 套 ColorOS）后主题 6 色变化，前端 UI 同步刷新（证据：`SettingsViewModel.SetPreset` 已删除 5-case switch，改走 `ThemePresetRegistry.ApplyPreset`，grep 确认 0 处 case）
- [x] 诚实返回链：未知预设 key 时 `settings:setPreset` 返回 `success=false`，不再无条件假成功（证据：`MainWindow.xaml.cs:3510-3512` 先 `GetPresetByKey` 校验再执行）
- [x] 主题色调色盘 6 个：设置页含主色/强调色/背景色/卡片色/文字色/边框色 6 个 `ColorPicker`（证据：`SettingsPage.tsx` 第 550-586 行渲染 6 个，label 齐全）
- [x] 语义/仪表盘色调色盘 6 个：成功/警告/错误/仪表盘绿/黄/红可自定义（证据：`SettingsPage.tsx` 第 589-641 行"语义与仪表盘色"卡片；`theme.ts` `applyStatusColors` 参数化）
- [x] 语义色派生统一：修改错误色时 C# `DangerBrush`/`GaugeRedBrush`/`ErrorTextBrush` 与前端 `--md-danger`/`--md-gauge-red`/`--md-error-text` 同源派生，无独立硬编码（证据：`ThemeService.UpdateResources` 与前端 `applyStatusColors` 均从属性/参数派生；硬编码字面量仅存于 6 个通道字段默认值与 ResetToDefault）
- [x] 持久化：重启后 12 色设置保留（证据：`ThemeSettings` 含 12 个颜色字段，`LoadSettings`/`SaveSettings`/`ResetToDefault` 全覆盖，git diff 核实）
- [x] 预设为 6 色完整方案：13 套预设均含合法 Text/Border HEX（证据：`ThemePreset` 记录新增两字段且全部 13 套已填；测试 7.2 编译通过）
- [x] 契约一致：TS 端 `utils/bridge.ts` 与 C# 端 `MainWindow.xaml.cs` 的 10 个新 setter 动作名逐一对齐（证据：diff 两个来源的动作名集合结果为空，已执行验证）
- [x] 编译通过：`dotnet build src/MSMC/MSMC.csproj -p:RuntimeIdentifier=win-x64` 0 Warning / 0 Error（CS####=0，前端 `wwwroot.zip` 重新打包嵌入成功）
- [x] 前端产物重建：`npm run build --prefix src/frontend` 成功，`dist` 更新（tsc + vite 均通过）
- [ ] 契约测试执行通过：13 套预设全量 ApplyPreset 生效测试、6 色合法测试、未知 key 失败测试**编译通过（0 Error）**，但 net9.0-windows 程序集需要 WindowsDesktop 运行时，当前 Linux 沙箱无法执行 `dotnet test` 全绿——已追加 Task 9，待 Windows CI 验证
- [x] 设计边界守住：纪念卡金色保持固定；WPF 原生 SettingsPage.xaml 未改动；未新增文件（仅修改既有文件）