# 项目重构计划：io.NET.ZTR_OS 命名空间迁移 + Feature 重组 + MSMC 品牌切换 + 前端加固

> **计划生成日期**：2026-07-30
> **影响范围**：C# 后端 110+ 文件、XAML 11 个、csproj/sln 3 个、前端全部源码、构建配置
> **预估改动文件数**：~150 个（不含文档）
> **文档策略**：51 个 .md 全部不动（含历史计划、设计文档、Agent.md、README.md）

---

## 一、Summary（任务概述）

将 C# 项目根命名空间从 `McServerGuard` 全量替换为 `io.NET.ZTR_OS`，保留现有分层子命名空间结构（`io.NET.ZTR_OS.Services.Network` 等）；同步把程序集名 / exe 输出名改为 `MSMC`；按 Feature 维度重组 `src/McServerGuard/` 目录为 `Features/{领域}/` 结构；前端引入 vite-plugin-obfuscator + React.lazy 路由懒加载 + 动态 import + Bridge 层符号混淆四件套，全面提升逆向成本。

**品牌全称**（用于用户协议、关于框、版权声明）：`Minecraft Server Management Console`。
**简称**（用于 exe 名、进程名、内部缩写）：`MSMC`。
**代码命名空间根**：`io.NET.ZTR_OS`（Java 风格反域名，对 C# 不常规但用户明确要求）。

---

## 二、Current State Analysis（当前状态分析）

### 2.1 项目结构现状

- 解决方案：`/workspace/McServerGuard.sln`（含 2 个项目）
- 主项目：`/workspace/src/McServerGuard/McServerGuard.csproj`
  - `RootNamespace=McServerGuard`、`AssemblyName=McServerGuard`、`TargetFramework=net9.0-windows10.0.22000.0`
  - `TreatWarningsAsErrors=true`、`AnalysisMode=All`（极严格）
  - 前端集成：MSBuild Target `BuildFrontend` + `PackFrontendToZip`（LogicalName=`McServerGuard.wwwroot.zip`）
- 测试项目：`/workspace/src/McServerGuard.Tests/McServerGuard.Tests.csproj`
- 前端：`/workspace/src/frontend/`（Vite 5 + React 18 + TS + Tailwind + Zustand）

### 2.2 `McServerGuard` 字符串使用统计

| 分类 | 数量 | 位置 |
|---|---|---|
| `namespace McServerGuard.*` 声明 | 110 个 .cs | 全部源码 |
| XAML `clr-namespace` | 10 处 / 6 文件 | App.xaml + 5 个 Page.xaml |
| XAML `x:Class` | 11 处 / 11 文件 | 全部 XAML |
| XAML `pack URI` | 2 处 | AppResources.xaml、MainWindow.xaml |
| Serilog `MinimumLevel.Override` 字符串 | 5 处 | App.xaml.cs:65-69 |
| csproj `RootNamespace`/`AssemblyName` | 4 处 / 2 文件 | 两个 csproj |
| sln Project 声明 | 2 处 | McServerGuard.sln |
| `App.Services` 静态属性 | 1 处 | App.xaml.cs:41 |
| 前端 `.ts`/`.tsx` 字面量 | 0 处 | — |
| 文档 `.md` | 51 文件 | **不动** |

### 2.3 前端现状（关键点）

- 无任何代码混淆（仅 Vite 默认 esbuild minify）
- 无 React.lazy / 动态 import / 模块联邦
- 仅手动拆分 vendor chunk（react/react-dom/react-router-dom）
- 双入口 MPA：`main`（index.html）+ `startup`（startup.html）
- Bridge 通信：`window.chrome.webview`（主）+ `window.__msmc_bridge__`（native 注入兜底）
- Bridge 实现位于 `src/utils/bridge.ts`，类型位于 `src/types/bridge.ts`

---

## 三、Proposed Changes（具体修改方案）

### 阶段 0：用户侧准备（沙箱外操作）

**目的**：建立安全网，避免重构失败导致代码丢失。

1. 用户在 Windows 端 `git checkout -b refactor/io-net-ztr-os-msmc` 创建专属分支
2. 用户确认工作区干净（`git status` 无未提交改动）
3. 用户备份当前 `dist/` 目录（前端构建产物，用于回滚参照）

**沙箱侧无需操作**。

---

### 阶段 1：csproj / sln 配置切换（基础设施先就位）

**目的**：先把构建系统的 AssemblyName / RootNamespace 改了，让后续命名空间替换有目标根。

#### 1.1 修改 `/workspace/src/McServerGuard/McServerGuard.csproj`

| 字段 | 旧值 | 新值 |
|---|---|---|
| `<RootNamespace>` | `McServerGuard` | `io.NET.ZTR_OS` |
| `<AssemblyName>` | `McServerGuard` | `MSMC` |

**保留不动**：
- `TargetFramework`、`UseWPF`、`Nullable`、`ImplicitUsings`、`TreatWarningsAsErrors`、`AnalysisMode`、`RuntimeIdentifiers`
- `<EmbeddedResource Include="wwwroot.zip" LogicalName="McServerGuard.wwwroot.zip" />` —— **LogicalName 是字符串字面量，C# 代码里 `Assembly.GetManifestResourceStream("McServerGuard.wwwroot.zip")` 必须同步改**（见阶段 2）
- MSBuild Target `BuildFrontend` / `PackFrontendToZip` 内部逻辑不动

#### 1.2 修改 `/workspace/src/McServerGuard.Tests/McServerGuard.Tests.csproj`

| 字段 | 旧值 | 新值 |
|---|---|---|
| `<RootNamespace>` | `McServerGuard.Tests` | `io.NET.ZTR_OS.Tests` |

**ProjectReference** 路径不动（仍是 `..\McServerGuard\McServerGuard.csproj`，因为目录名暂不改，见阶段 4 决策）。

#### 1.3 修改 `/workspace/McServerGuard.sln`

| 字段 | 旧值 | 新值 |
|---|---|---|
| 第 6 行 Project 名 | `McServerGuard` | `MSMC` |
| 第 8 行 Project 名 | `McServerGuard.Tests` | `MSMC.Tests` |

Project GUID、路径、配置段（Debug|Any CPU / Release|Any CPU）保持不动。

#### 1.4 验证

用户在 VS 上 `生成 → 清理解决方案` → `生成 → 重新生成解决方案`，此时必然报大量 `找不到命名空间 McServerGuard.*` 错误（预期，下一阶段修复）。**只验证 csproj/sln 本身能被 VS 正常加载**。

---

### 阶段 2：C# 命名空间全量替换（机械文本替换）

**目的**：把所有 `namespace McServerGuard.X` 改为 `namespace io.NET.ZTR_OS.X`，所有 `using McServerGuard.X` 改为 `using io.NET.ZTR_OS.X`，所有字符串字面量 `"McServerGuard..."` 改为对应新值。

#### 2.1 替换规则（精确正则）

| 旧模式 | 新模式 | 影响范围 |
|---|---|---|
| `namespace McServerGuard` | `namespace io.NET.ZTR_OS` | 110 个 .cs |
| `using McServerGuard` | `using io.NET.ZTR_OS` | 所有 .cs 的 using 块 |
| `"McServerGuard.Services.Server"` | `"io.NET.ZTR_OS.Services.Server"` | App.xaml.cs Serilog override 5 处 |
| `"McServerGuard.wwwroot.zip"` | `"MSMC.wwwroot.zip"` | 引用嵌入资源的 C# 代码（需 grep 定位） |
| csproj `LogicalName="McServerGuard.wwwroot.zip"` | `LogicalName="MSMC.wwwroot.zip"` | McServerGuard.csproj |

#### 2.2 执行方式

用脚本批量处理（沙箱内执行）：

```bash
# 沙箱内用 Python 脚本遍历 src/McServerGuard/**/*.cs 和 src/McServerGuard.Tests/**/*.cs
# 对每个文件：
#   1. 替换 "namespace McServerGuard" → "namespace io.NET.ZTR_OS"
#   2. 替换 "using McServerGuard" → "using io.NET.ZTR_OS"
#   3. 替换字符串字面量 "McServerGuard.wwwroot.zip" → "MSMC.wwwroot.zip"
#   4. 替换 Serilog override 5 处 "McServerGuard.Services.xxx" → "io.NET.ZTR_OS.Services.xxx"
```

#### 2.3 注意事项

- **不替换**：文件头注释里的"命名空间: McServerGuard.xxx"中文描述（用户决策：文档/注释保留原样，便于追溯历史）
- **不替换**：`App.Services` 静态属性名（这是 API，不是命名空间）
- **不替换**：`App.xaml.cs` 第 41 行的 `App.Services` 属性定义本身
- **替换后立即验证**：grep 残留 `McServerGuard` 应只剩注释和文档

#### 2.4 验证

用户在 VS 上重新生成解决方案。预期：编译通过（命名空间已全部对齐）。若仍有错误，多为漏替换或 XAML 未同步（见阶段 3）。

---

### 阶段 3：XAML 同步（clr-namespace / x:Class / pack URI）

**目的**：XAML 文件里的命名空间引用必须与 .cs 同步。

#### 3.1 替换清单

| 文件 | 行 | 旧 | 新 |
|---|---|---|---|
| App.xaml | 5 | `xmlns:converters="clr-namespace:McServerGuard.Converters"` | `xmlns:converters="clr-namespace:io.NET.ZTR_OS.Converters"` |
| App.xaml | 4 | `x:Class="McServerGuard.App"` | `x:Class="io.NET.ZTR_OS.App"` |
| ServerDetectionPage.xaml | 8 | `clr-namespace:McServerGuard.Views.Controls` | `clr-namespace:io.NET.ZTR_OS.Views.Controls` |
| ServerDetectionPage.xaml | 9 | `clr-namespace:McServerGuard.Converters` | `clr-namespace:io.NET.ZTR_OS.Converters` |
| ServerDetectionPage.xaml | 10 | `clr-namespace:McServerGuard.Models` | `clr-namespace:io.NET.ZTR_OS.Models` |
| ServerDetectionPage.xaml | 1 | `x:Class="McServerGuard.Views.ServerDetectionPage"` | `x:Class="io.NET.ZTR_OS.Views.ServerDetectionPage"` |
| ConfigEditorPage.xaml | 7 | `clr-namespace:McServerGuard.Views.Controls` | `clr-namespace:io.NET.ZTR_OS.Views.Controls` |
| ConfigEditorPage.xaml | 8 | `clr-namespace:McServerGuard.Converters` | `clr-namespace:io.NET.ZTR_OS.Converters` |
| ConfigEditorPage.xaml | 9 | `clr-namespace:McServerGuard.Selectors"` | `clr-namespace:io.NET.ZTR_OS.Selectors"` |
| ConfigEditorPage.xaml | 1 | `x:Class="McServerGuard.Views.ConfigEditorPage"` | `x:Class="io.NET.ZTR_OS.Views.ConfigEditorPage"` |
| NetworkMonitorPage.xaml | 7 | `clr-namespace:McServerGuard.Views.Controls` | `clr-namespace:io.NET.ZTR_OS.Views.Controls` |
| NetworkMonitorPage.xaml | 1 | `x:Class="McServerGuard.Views.NetworkMonitorPage"` | `x:Class="io.NET.ZTR_OS.Views.NetworkMonitorPage"` |
| SystemMonitorPage.xaml | 7 | `clr-namespace:McServerGuard.Views.Controls` | `clr-namespace:io.NET.ZTR_OS.Views.Controls` |
| SystemMonitorPage.xaml | 1 | `x:Class="McServerGuard.Views.SystemMonitorPage"` | `x:Class="io.NET.ZTR_OS.Views.SystemMonitorPage"` |
| SettingsPage.xaml | 8 | `clr-namespace:McServerGuard.Views.Controls` | `clr-namespace:io.NET.ZTR_OS.Views.Controls` |
| SettingsPage.xaml | 1 | `x:Class="McServerGuard.Views.SettingsPage"` | `x:Class="io.NET.ZTR_OS.Views.SettingsPage"` |
| MainWindow.xaml | 1 | `x:Class="McServerGuard.Views.MainWindow"` | `x:Class="io.NET.ZTR_OS.Views.MainWindow"` |
| MainWindow.xaml | 34 | `pack://application:,,,/McServerGuard;component/Themes/AppResources.xaml` | `pack://application:,,,/MSMC;component/Themes/AppResources.xaml` |
| MainWindow.xaml | 1 注释 | `<!-- 🏠 主窗口 —— McServerGuard 的大本营 -->` | **保留不动**（注释） |
| StartupWindow.xaml | 1 | `x:Class="McServerGuard.Views.StartupWindow"` | `x:Class="io.NET.ZTR_OS.Views.StartupWindow"` |
| UserAgreementWindow.xaml | 1 | `x:Class="McServerGuard.Views.UserAgreementWindow"` | `x:Class="io.NET.ZTR_OS.Views.UserAgreementWindow"` |
| Controls/IndependentLoadingIcon.xaml | 1 | `x:Class="McServerGuard.Views.Controls.IndependentLoadingIcon"` | `x:Class="io.NET.ZTR_OS.Views.Controls.IndependentLoadingIcon"` |
| Controls/ColorPickerControl.xaml | 1 | `x:Class="McServerGuard.Views.Controls.ColorPickerControl"` | `x:Class="io.NET.ZTR_OS.Views.Controls.ColorPickerControl"` |
| Themes/AppResources.xaml | 167 | `pack://application:,,,/McServerGuard;component/Resources/Fonts/...` | `pack://application:,,,/MSMC;component/Resources/Fonts/...` |

**关键点**：`pack URI` 里的程序集名用 `MSMC`（与 AssemblyName 一致），**不用** `io.NET.ZTR_OS`。

#### 3.2 验证

用户在 VS 上重新生成解决方案。预期：XAML 编译通过，`InitializeComponent()` 能找到对应 BAML 资源。手动启动应用确认主窗口能加载。

---

### 阶段 4：Feature 目录重组（最大工作量，最复杂）

**目的**：把扁平的 `Views/ + ViewModels/ + Models/ + Services/ + Constants/ + Selectors/` 重组为 `Features/{领域}/` 结构。

#### 4.1 目标目录结构

```
src/McServerGuard/                          ← 目录名暂不改（见决策 D1）
├── App.xaml / App.xaml.cs                  ← 保留根
├── McServerGuard.csproj                    ← 保留根（仅内容已改）
├── app.manifest
├── Resources/                              ← 保留根
├── Themes/
└── Features/
    ├── ServerDetection/
    │   ├── Services/                       ← 16 个 Service
    │   ├── Views/                          ← ServerDetectionPage.xaml(.cs)
    │   ├── ViewModels/                     ← ServerDetectionViewModel.cs
    │   └── Models/                         ← ServerInstance, KnownServer, ServerStatus, DetectionResult, ServerConfigEntry, StartupScriptInfo
    ├── SystemMonitoring/
    │   ├── Services/                       ← 9 个 Service + CpuIdentifier
    │   ├── Views/                          ← SystemMonitorPage.xaml(.cs)
    │   ├── ViewModels/                     ← SystemMonitorViewModel.cs
    │   └── Models/                         ← SystemMetrics, MetricsHistoryPoint, ProcessAffinityInfo, Hardware/HardwareInfo
    ├── NetworkMonitor/
    │   ├── Services/                       ← 7 个 Service
    │   ├── Views/                          ← NetworkMonitorPage.xaml(.cs)
    │   ├── ViewModels/                     ← NetworkMonitorViewModel.cs
    │   ├── Models/                         ← PortInfo, PortBridgeRule, PortBridgeResult, CommonPort
    │   └── Constants/                      ← CommonPorts.cs, IpAddresses.cs
    ├── ConfigEditor/
    │   ├── Services/                       ← 6 个 Service
    │   ├── Views/                          ← ConfigEditorPage.xaml(.cs)
    │   ├── ViewModels/                     ← ConfigEditorViewModel.cs
    │   └── Selectors/                      ← ConfigEditorTemplateSelector.cs
    ├── Settings/
    │   ├── Services/                       ← AppConfigService, IAppConfigService, ThemeService, AnimationSettings, ToastNotificationService
    │   ├── Views/                          ← SettingsPage.xaml(.cs)
    │   ├── ViewModels/                     ← SettingsViewModel.cs, JavaInstallationViewModel.cs
    │   └── Color/                          ← ColorHelper, OkLchColor
    ├── UserAgreement/
    │   ├── Services/                       ← UserAgreementService, IUserAgreementService
    │   └── Views/                          ← UserAgreementWindow.xaml(.cs)
    ├── WebView2/
    │   ├── Services/                       ← BridgeMessage, IWebView2BridgeService, WebView2BridgeService
    │   └── Frontend/                       ← 5 个 resource provider
    ├── JavaInstallation/
    │   └── Services/                       ← JavaFinderService, IJavaFinderService
    │   └── Constants/                      ← JvmArgumentConstants.cs, ServerConstants.cs
    ├── Startup/
    │   ├── Services/                       ← TimeService, PrivilegeService, Privilege/AdminPrivilegeService, MemoryOptimizerService
    │   └── Views/                          ← StartupWindow.xaml(.cs)
    └── Shared/
        ├── Views/                          ← MainWindow.xaml(.cs)
        ├── ViewModels/                     ← MainViewModel.cs
        ├── Models/                         ← NavItem.cs
        ├── Converters/                     ← ServerStatusConverters.cs, ValueConverters.cs
        ├── Controls/                       ← ColorPickerControl, GaugeRingControl, IndependentLoadingIcon
        ├── Helpers/                        ← AnimationHelper.cs
        └── Themes/                         ← AppResources.xaml
```

#### 4.2 归类冲突决策（基于探索结果）

| 冲突点 | 决策 | 理由 |
|---|---|---|
| `Models/PortInfo.cs` | 归 NetworkMonitor | 实际仅被 NetworkMonitorViewModel 和 NetworkService 使用，ServerDetection 16 个 Service 均未引用 |
| `Models/ServerConfigEntry.cs` | 归 ConfigEditor | 是 ConfigEditorViewModel 和 ConfigEditorTemplateSelector 的核心数据模型，ServerDetectionViewModel 不直接使用 |
| `Constants/JvmArgumentConstants.cs` | 归 JavaInstallation | 用户原始意图，被 ServerDetectionViewModel 跨域使用但语义上属于 Java 配置 |
| `Constants/ServerConstants.cs` | 归 JavaInstallation | 同上，被 ServerPortResolver 跨域使用但语义属于服务端常量 |
| `Models/ServerInstance.cs` | 归 ServerDetection | 作为 owner，被 ConfigEditor/SystemMonitor/MainViewModel 跨域使用，但主语义属于服务端检测 |
| `JavaInstallationViewModel.cs` | 归 Settings/ViewModels/ | 无独立 View，嵌入 SettingsPage 渲染 |

#### 4.3 执行步骤（沙箱内）

1. **创建目标目录结构**：用 `mkdir -p` 一次性创建所有 `Features/*/Services`、`Features/*/Views` 等子目录
2. **物理移动文件**：用 `git mv`（保留 git 历史）按 4.1 结构图移动每个文件
3. **更新命名空间声明**：移动后的文件命名空间应反映新位置。例如：
   - `Features/ServerDetection/Services/ServerDetector.cs` 的 `namespace` 从 `io.NET.ZTR_OS.Services.ServerDetection` 改为 `io.NET.ZTR_OS.Features.ServerDetection.Services`
   - `Features/ServerDetection/Views/ServerDetectionPage.xaml.cs` 从 `io.NET.ZTR_OS.Views` 改为 `io.NET.ZTR_OS.Features.ServerDetection.Views`
   - `Features/Shared/Controls/GaugeRingControl.cs` 从 `io.NET.ZTR_OS.Views.Controls` 改为 `io.NET.ZTR_OS.Features.Shared.Controls`
4. **批量更新 using**：所有引用了被移动类型的文件，其 `using` 语句需同步更新
5. **更新 XAML clr-namespace**：所有 `clr-namespace:io.NET.ZTR_OS.Views.Controls` 改为 `clr-namespace:io.NET.ZTR_OS.Features.Shared.Controls` 等
6. **更新 XAML x:Class**：所有 `x:Class="io.NET.ZTR_OS.Views.XxxPage"` 改为 `x:Class="io.NET.ZTR_OS.Features.{领域}.Views.XxxPage"`
7. **更新 pack URI**：`pack://application:,,,/MSMC;component/Themes/AppResources.xaml` → `pack://application:,,,/MSMC;component/Features/Shared/Themes/AppResources.xaml`；字体同理
8. **更新 csproj 资源路径**：`<Resource Include="Resources\Fonts\SpaceGrotesk.ttf" />` 等保持不动（Resources/ 仍在根）；`<EmbeddedResource Include="wwwroot.zip" />` 不动
9. **更新 csproj 的 Page/Compile Include**：csproj 用 SDK 风格默认通配，无需手动改 Include（除非有显式 `<Page Include="...">`，需 grep 确认）

#### 4.4 验证

用户在 VS 上：
1. 关闭 VS → 删除 `.vs/`、`obj/`、`bin/` → 重新打开解决方案（清理设计器缓存）
2. 重新生成解决方案，预期编译通过
3. 启动应用，依次访问：仪表盘、服务端检测、配置编辑、系统监控、网络监控、设置页，全部功能正常
4. 运行单元测试（`dotnet test`），全部通过

---

### 阶段 5：前端加固（混淆 + 懒加载 + Bridge 加固）

**目的**：提升前端逆向成本。

#### 5.1 引入 vite-plugin-obfuscator

**文件**：`/workspace/src/frontend/package.json` + `/workspace/src/frontend/vite.config.ts`

1. 在 package.json 的 devDependencies 添加：
   ```json
   "javascript-obfuscator": "^4.1.1",
   "vite-plugin-obfuscator": "^0.6.0"
   ```
2. 用户在 Windows 端执行 `npm install`（沙箱无 node_modules 写权限，仅改配置）
3. 修改 vite.config.ts，在 plugins 数组添加：
   ```typescript
   import obfuscator from 'vite-plugin-obfuscator'

   // plugins 数组中追加：
   obfuscator({
     options: {
       compact: true,
       controlFlowFlattening: true,              // 控制流平坦化
       controlFlowFlatteningThreshold: 0.75,
       deadCodeInjection: true,                  // 死代码注入
       deadCodeInjectionThreshold: 0.4,
       debugProtection: true,                    // 反调试
       debugProtectionInterval: 2000,
       disableConsoleOutput: true,               // 禁用 console
       identifierNamesGenerator: 'hexadecimal',  // 变量名十六进制化
       renameGlobals: false,                     // 不重命名全局（避免桥接 API 失效）
       selfDefending: true,                      // 自防御
       stringArray: true,                        // 字符串数组加密
       stringArrayEncoding: ['rc4'],
       stringArrayThreshold: 0.75,
       transformObjectKeys: true,                // 对象键名转换
       unicodeEscapeSequence: false,
     },
     // 仅混淆业务代码，不混淆 vendor（vendor 是 react 等公开库）
     exclude: [/node_modules/, /vendor/],
   })
   ```
4. 修改 vite.config.ts 的 build.minify 为 `'terser'`（obfuscator 与 terser 配合更好）

#### 5.2 路由懒加载 + 动态 import

**文件**：`/workspace/src/frontend/src/App.tsx`

1. 把所有 `import { XxxPage } from '@/pages/XxxPage'` 改为：
   ```typescript
   const DashboardPage = lazy(() => import('@/pages/DashboardPage'))
   const ConfigEditorPage = lazy(() => import('@/pages/ConfigEditorPage'))
   const NetworkMonitorPage = lazy(() => import('@/pages/NetworkMonitorPage'))
   const SettingsPage = lazy(() => import('@/pages/SettingsPage'))
   const SystemMonitorPage = lazy(() => import('@/pages/SystemMonitorPage'))
   ```
2. 用 `<Suspense fallback={<Loading />}>` 包裹 `<Routes>`
3. 修改 vite.config.ts 的 manualChunks，按页面拆分 chunk：
   ```typescript
   build: {
     rollupOptions: {
       output: {
         manualChunks: {
           vendor: ['react', 'react-dom', 'react-router-dom'],
           charts: ['recharts'],
           icons: ['react-icons'],
         },
       },
     },
   }
   ```
   页面 chunk 由 Rollup 自动按动态 import 拆分，无需手动配置。

#### 5.3 Bridge 层符号混淆 + 域分隔

**文件**：`/workspace/src/frontend/src/utils/bridge.ts`

1. 把 `Bridge` 类的所有公开方法名通过 `/** @obfuscate */` 注释标记（vite-plugin-obfuscator 默认会处理）
2. 把 `bridge` 单例的导出方式改为：
   ```typescript
   // 不再 export const bridge = new Bridge()
   // 改为工厂函数，避免全局对象被逆向者直接 inspect
   let _bridge: Bridge | null = null
   export function getBridge(): Bridge {
     if (!_bridge) _bridge = new Bridge()
     return _bridge
   }
   ```
3. 在 bridge.ts 顶部加入防篡改自检：
   ```typescript
   // 启动时校验 window.chrome.webview 是否被覆写
   const _originalPostMessage = window.chrome?.webview?.postMessage
   Object.defineProperty(window.chrome?.webview || {}, 'postMessage', {
     get: () => _originalPostMessage,
     set: () => { throw new Error('tampered') },
     configurable: false,
   })
   ```
4. 同步更新所有调用方：`import { bridge } from '@/utils/bridge'` → `import { getBridge } from '@/utils/bridge'; const bridge = getBridge()`（或在文件顶部一次性 `const bridge = getBridge()`）

#### 5.4 验证

用户在 Windows 端：
1. `cd src/frontend && npm install`
2. `npm run build`
3. 检查 `dist/assets/` 下：
   - 应有多个 `*.js` chunk（main + 各 page + vendor + charts + icons）
   - 用浏览器打开 `dist/index.html`，按 F12 检查源码：应看到十六进制变量名、控制流平坦化、字符串加密
4. 启动 WPF 应用，访问每个页面，确认懒加载正常（首次切换页面有短暂 loading）
5. 确认 Bridge 通信正常（所有数据加载、按钮点击、设置保存等功能正常）

---

### 阶段 6：最终验证 + 提交

#### 6.1 全量验证清单

- [ ] VS 重新生成解决方案：0 错误 0 警告（`TreatWarningsAsErrors=true`）
- [ ] `dotnet test` 全部通过
- [ ] 启动应用：用户协议窗口正常弹出（首次/版本升级时）
- [ ] 启动窗口正常显示进度条和日志
- [ ] 主窗口正常加载（WebView2 渲染前端）
- [ ] 仪表盘、服务端检测、配置编辑、系统监控、网络监控、设置页全部可访问
- [ ] 服务端检测：能扫描出运行中的 MC 服务端
- [ ] 配置编辑：能加载并编辑 server.properties
- [ ] 系统监控：CPU/内存/磁盘仪表盘正常
- [ ] 网络监控：端口列表正常
- [ ] 设置页：主题切换、主色修改正常
- [ ] 用户协议：滚动限制、倒计时、同意/不同意流程正常
- [ ] 进程名显示为 `MSMC.exe`（任务管理器查看）
- [ ] 前端 dist 产物已混淆（F12 看不到可读源码）

#### 6.2 提交策略

分阶段提交，每个阶段一个 commit：

```
refactor(naming): 将 C# 命名空间从 McServerGuard 全量替换为 io.NET.ZTR_OS

- 影响 110 个 .cs 文件的 namespace 声明和 using
- 同步更新 XAML clr-namespace、x:Class、pack URI
- Serilog override 字符串字面量同步更新
- 嵌入资源 LogicalName 改为 MSMC.wwwroot.zip

问题背景：原命名空间 McServerGuard 与品牌名混淆，需统一为 io.NET.ZTR_OS
影响范围：全部 C# 源码、XAML、csproj 配置
```

```
refactor(assembly): 将程序集名/输出 exe 改为 MSMC

- csproj RootNamespace=io.NET.ZTR_OS, AssemblyName=MSMC
- sln 项目名改为 MSMC / MSMC.Tests
- 输出 exe 从 McServerGuard.exe 变为 MSMC.exe

影响范围：构建产物文件名、进程名、CI 缓存路径
```

```
refactor(structure): 按 Feature 重组 src/McServerGuard 目录

- 新建 Features/{ServerDetection,SystemMonitoring,NetworkMonitor,
  ConfigEditor,Settings,UserAgreement,WebView2,JavaInstallation,
  Startup,Shared}/ 子目录
- 物理移动 110+ 文件到对应 Feature
- 命名空间同步更新为 io.NET.ZTR_OS.Features.{领域}.xxx
- XAML clr-namespace、x:Class、pack URI 全部同步

归类决策：
- PortInfo 归 NetworkMonitor（实际使用方）
- ServerConfigEntry 归 ConfigEditor（核心数据模型）
- JvmArgumentConstants/ServerConstants 归 JavaInstallation（语义归属）

影响范围：全部源码文件位置、命名空间层级
```

```
feat(frontend): 引入代码混淆 + 路由懒加载 + Bridge 加固

- 集成 vite-plugin-obfuscator（控制流平坦化、字符串加密、反调试）
- App.tsx 改用 React.lazy + Suspense 实现路由懒加载
- 拆分 vendor/charts/icons chunk
- Bridge 层改为工厂模式 + 防篡改自检

问题背景：前端 dist 产物可被轻松逆向，需提升破解成本
影响范围：前端构建配置、App.tsx、bridge.ts、所有调用方
```

---

## 四、Assumptions & Decisions（假设与决策）

### D1：`src/McServerGuard/` 目录名暂不改

**决策**：保留 `src/McServerGuard/` 目录名不动，仅改内部命名空间和 csproj 配置。

**理由**：
- 改目录名会破坏 sln 里的相对路径 `src\McServerGuard\McServerGuard.csproj`
- 改目录名会破坏 ProjectReference 路径
- 改目录名会破坏 CI 脚本 `.github/workflows/ci.yml` 里的路径
- 改目录名会让 git history 难以追溯（虽然 git mv 能保留，但 blame 会变复杂）
- 用户决策"文档全不动"，CI 脚本也算文档性质，避免动它

**风险**：目录名与命名空间不一致（`src/McServerGuard/` 里是 `io.NET.ZTR_OS.*`），但这是 C# 项目常见情况（目录与命名空间解耦）。

### D2：测试项目目录名也保留

`src/McServerGuard.Tests/` 目录名不动，仅改 RootNamespace 为 `io.NET.ZTR_OS.Tests`。

### D3：`App.Services` 静态属性名保留

不改名为 `App.MsmcServices` 等，避免破坏所有 `App.Services.GetRequiredService<T>()` 调用点。

### D4：文档全部不动

51 个 .md 文件（含 docs/、.trae/documents/、Agent.md、README.md）保持原样。历史计划文档里的 `McServerGuard.*` 命名空间示例代码不再准确，但作为历史记录保留。

### D5：注释里的"McServerGuard"保留

文件头注释 `// 命名空间: McServerGuard.ViewModels` 等保留原样，便于追溯历史。仅改实际代码（namespace 声明、using、字符串字面量）。

### D6：前端加固不引入 Web Worker / WASM

考虑过用 Web Worker 隔离 Bridge 通信、用 WASM 编译关键逻辑，但工作量过大、调试困难、性能影响不明，本次不做。

### D7：`__msmc_bridge__` 全局对象名保留

`window.__msmc_bridge__` 是 native 注入的，改名需要同步改 C# 端注入代码，工作量大且无明显收益（逆向者可通过 native 端代码找到），保留不动。

### D8：混淆配置对 vendor chunk 排除

`vite-plugin-obfuscator` 的 `exclude: [/node_modules/, /vendor/]` 确保公开库（react 等）不被混淆，避免运行时错误。仅混淆业务代码。

---

## 五、Verification Steps（验证步骤）

### 5.1 沙箱内可验证项

- 阶段 2 完成后：`grep -r "namespace McServerGuard" src/` 应返回 0 结果（除注释）
- 阶段 2 完成后：`grep -r "using McServerGuard" src/` 应返回 0 结果
- 阶段 3 完成后：`grep -r "McServerGuard" src/McServerGuard/**/*.xaml` 应仅剩注释
- 阶段 4 完成后：`find src/McServerGuard -name "*.cs" | wc -l` 应与重构前一致（无文件丢失）
- 阶段 5 完成后：`grep -r "import { bridge }" src/frontend/src/` 应返回 0 结果（已改为 getBridge）

### 5.2 用户侧验证项

- 阶段 1 后：VS 能正常加载解决方案
- 阶段 2 后：VS 重新生成解决方案，C# 编译通过
- 阶段 3 后：VS 重新生成解决方案，XAML 编译通过，应用能启动
- 阶段 4 后：VS 重新生成解决方案，全部功能正常，单元测试通过
- 阶段 5 后：前端构建成功，dist 产物已混淆，应用功能正常
- 阶段 6 后：全部验证清单通过，分阶段提交 commit

---

## 六、Risk & Mitigation（风险与缓解）

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| 命名空间替换漏改导致编译错误 | 中 | 低（编译器会报错） | 阶段 2 用脚本批量替换 + grep 验证残留 |
| Feature 重组后跨域 using 漏改 | 高 | 中（编译错误） | 阶段 4 后立即编译验证，逐个修复 |
| XAML pack URI 未同步导致资源加载失败 | 中 | 高（运行时崩溃） | 阶段 3 专门处理 XAML，逐项核对 |
| 前端混淆导致运行时错误 | 中 | 高（功能失效） | obfuscator 配置排除 vendor，debugProtection 仅在 production 启用 |
| 前端懒加载导致首屏白屏 | 低 | 中（体验下降） | Suspense fallback 用 loading 动画 |
| Bridge 工厂模式改造遗漏调用点 | 中 | 中（运行时错误） | 阶段 5 后全量 grep `import { bridge }` 验证 |
| git history 因大量 mv 丢失 | 高 | 低（可追溯性下降） | 用 `git mv` 保留历史，commit message 说明 |
| 沙箱无 dotnet SDK 无法编译验证 | 高 | 中（无法即时验证） | 每个阶段设计为用户侧可独立验证，沙箱只做文本改动 |

---

## 七、Out of Scope（明确不做的事）

- 不改 `src/McServerGuard/` 目录名（见 D1）
- 不改 `src/McServerGuard.Tests/` 目录名（见 D2）
- 不改 `App.Services` 属性名（见 D3）
- 不改任何 .md 文档（见 D4）
- 不改文件头注释里的"McServerGuard"字样（见 D5）
- 不引入 Web Worker / WASM 加固（见 D6）
- 不改 `window.__msmc_bridge__` 全局对象名（见 D7）
- 不改 CI 脚本 `.github/workflows/ci.yml`（路径仍指向 `src/McServerGuard/`）
- 不改 `app.manifest`
- 不改 `NuGet.Config`
- 不改 `test-server/` 目录
- 不改 `MiniServer.java`
- 不重构前端 `src/` 目录结构（仅加固，不重组）
- 不改前端 `package.json` 的 `name` 字段（`msmc-frontend` 已符合品牌）

---

## 八、执行顺序总览

```
阶段 0（用户侧）→ 阶段 1（csproj/sln）→ 阶段 2（C# 命名空间）
                                            ↓
                                         阶段 3（XAML 同步）
                                            ↓
                                         阶段 4（Feature 重组）← 最大工作量
                                            ↓
                                         阶段 5（前端加固）← 可与阶段 4 并行
                                            ↓
                                         阶段 6（验证 + 提交）
```

**关键路径**：阶段 1 → 2 → 3 → 4 → 6（C# 重构）
**可并行**：阶段 5（前端加固）与阶段 4（C# 重组）无依赖，可并行执行

**沙箱限制**：沙箱无 dotnet SDK 和 node 工具链，所有编译验证由用户在 Windows VS 上完成。沙箱仅负责文本改动和配置文件生成。
