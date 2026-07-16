# MSMC 全面增强实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 全面提升 MSMC (Minecraft Server Management Console) 的 CI 稳定性、视觉品质、代码健壮性和本地开发体验。

**Architecture:** 6 个独立子项目，各自产出可验证的交付物。按优先级排序：CI 稳定 → 本地编译 → 视觉优化 → 代码健壮性。

**Tech Stack:** WPF / .NET 10 / MaterialDesignInXamlToolkit / MahApps.Metro.IconPacks / GitHub Actions / Serilog

---

## 子项目 1：修复 GitHub Actions CI 抽风问题

### 问题诊断

当前 CI 配置在 `.github/workflows/ci.yml` 中存在以下隐患：

1. **.NET 10 预览版不稳定**：使用 `10.0.x`，可能拉到有 bug 的预览版
2. **audit job 过于严格**：`dotnet format` + 空壳代码扫描 + 安全扫描，任何一个失败都阻断 build
3. **空壳代码扫描使用 "fuck" 字样**，且正则可能误报
4. **build job 依赖 audit**：audit 挂了 build 根本不跑，拿不到编译产物
5. **缺少缓存**：每次都 restore 所有 NuGet 包，慢且容易超时
6. **WPF 项目必须 windows-latest**：跨平台构建不行，但可以优化 Windows runner 上的效率

### Task 1.1：优化 CI 工作流结构

**Files:**
- Modify: `.github/workflows/ci.yml`

**改动要点：**

1. **audit job 改为可选**：`continue-on-error: true`，失败不阻断 build，只发警告
2. **build job 不再依赖 audit**：直接跑，编译是核心验证
3. **添加 NuGet 缓存**：使用 `actions/cache` 缓存 `~/.nuget/packages`
4. **dotnet format 改为警告模式**：不返回非零退出码
5. **空壳代码扫描改为 info 级别**：不 `exit 1`，只打印发现的问题
6. **安全漏洞扫描直接依赖**保持阻断，传递依赖保持警告

**具体修改（直接替换 ci.yml）：**

```yaml
name: MSMC CI

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

# 并发控制：同分支只跑最新一次，旧的自动取消
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  build:
    name: 🏗️ 编译 + 测试 + 发布
    runs-on: windows-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      # NuGet 缓存
      - name: Cache NuGet packages
        uses: actions/cache@v4
        with:
          path: ~/.nuget/packages
          key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/*.props') }}
          restore-keys: |
            ${{ runner.os }}-nuget-

      - name: Restore dependencies
        run: dotnet restore

      # 严格编译主项目
      - name: Build main project (strict)
        run: dotnet build src/McServerGuard/McServerGuard.csproj --configuration Release --no-restore -p:TreatWarningsAsErrors=true -p:CodeAnalysisTreatWarningsAsErrors=true -p:AnalysisMode=All

      - name: Build test project
        run: dotnet build src/McServerGuard.Tests/McServerGuard.Tests.csproj --configuration Release --no-restore

      - name: Test
        run: |
          echo "📋 列出所有可发现的测试..."
          dotnet test src/McServerGuard.Tests/McServerGuard.Tests.csproj --configuration Release --no-build --list-tests
          echo "🧪 运行测试..."
          dotnet test src/McServerGuard.Tests/McServerGuard.Tests.csproj --configuration Release --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: src/McServerGuard.Tests/TestResults/

      # 单文件发布（即使测试失败也发布，方便诊断）
      - name: Publish (single-file)
        if: success() || failure()
        run: |
          dotnet publish src/McServerGuard/McServerGuard.csproj `
            --configuration Release `
            -p:RuntimeIdentifier=win-x64 `
            -p:SelfContained=true `
            -p:PublishSingleFile=true `
            -p:EnableCompressionInSingleFile=true `
            -o publish/win-x64
        shell: pwsh

      # 发布产物检查
      - name: Verify Artifact
        shell: pwsh
        run: |
          $exe = "publish/win-x64/McServerGuard.exe"
          if (-not (Test-Path $exe)) {
            echo "❌ 发布产物中找不到 McServerGuard.exe！"
            exit 1
          }
          $size = (Get-Item $exe).Length
          echo "📦 McServerGuard.exe 大小: $([math]::Round($size/1MB, 2)) MB"
          if ($size -lt 1MB) {
            echo "❌ EXE 文件太小（$([math]::Round($size/1KB, 2)) KB），可能发布失败！"
            exit 1
          }
          echo "✅ 发布产物检查通过！"

      - name: Upload Artifact
        uses: actions/upload-artifact@v4
        with:
          name: MSMC-win-x64-singlefile
          path: publish/win-x64/
          retention-days: 30

  # 代码质量检查（非阻断，仅作参考）
  audit:
    name: 🔍 代码质量检查
    runs-on: windows-latest
    needs: build  # 等 build 过了再跑，节省资源
    continue-on-error: true  # 失败不阻断
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET 10.0
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      # 代码格式检查（仅警告）
      - name: Code Format Check
        run: |
          dotnet format --verbosity diagnostic --verify-no-changes
          if ($LASTEXITCODE -ne 0) {
            echo "⚠️ 代码格式有差异，建议运行 dotnet format 修复（不阻断构建）"
          } else {
            echo "✅ 代码格式检查通过"
          }
        shell: pwsh
        continue-on-error: true

      # 代码问题扫描（仅报告，不阻断）
      - name: Code Quality Scan
        shell: pwsh
        continue-on-error: true
        run: |
          $issues = @()

          # 扫描 NotImplementedException
          $notImpl = Get-ChildItem -Path "src/McServerGuard" -Filter "*.cs" -Recurse | Select-String -Pattern "throw new NotImplementedException"
          if ($notImpl) {
            Write-Host "⚠️ 发现 NotImplementedException（空壳代码）："
            $notImpl | ForEach-Object { Write-Host "  $($_.FileName):$($_.LineNumber)" }
            $issues += "NotImplementedException: $($notImpl.Count) 处"
          }

          # 扫描 TODO 注释
          $todos = Get-ChildItem -Path "src/McServerGuard" -Filter "*.cs" -Recurse | Select-String -Pattern "//\s*TODO"
          if ($todos) {
            Write-Host "ℹ️ 发现 TODO 注释："
            $todos | ForEach-Object { Write-Host "  $($_.FileName):$($_.LineNumber) - $($_.Line.Trim())" }
            $issues += "TODO: $($todos.Count) 处"
          }

          # 扫描空 catch 块
          $emptyCatch = Get-ChildItem -Path "src/McServerGuard" -Filter "*.cs" -Recurse | ForEach-Object {
            $content = Get-Content $_.FullName -Raw
            if ($content -match 'catch\s*\([^)]*\)\s*\{\s*(?:\/\/[^\n]*\s*)*\}') {
              Write-Host "⚠️ 发现空 catch 块: $($_.FullName)"
              $true
            }
          }
          if ($emptyCatch) { $issues += "空 catch 块" }

          if ($issues.Count -eq 0) {
            Write-Host "✅ 代码质量扫描通过！"
          } else {
            Write-Host "📊 扫描发现：$($issues -join ', ')"
          }

      # 安全漏洞扫描（直接依赖阻断，传递依赖警告）
      - name: Security Vulnerability Scan (direct)
        run: |
          $direct = dotnet list package --vulnerable 2>$null | Out-String
          echo $direct
          if ($direct -match "has the following vulnerable packages") {
            echo "❌ 直接依赖中发现安全漏洞！请升级受影响的 NuGet 包。"
            exit 1
          }
          echo "✅ 直接依赖安全扫描通过！"
        shell: pwsh

      - name: Security Vulnerability Scan (transitive)
        continue-on-error: true
        run: |
          $transitive = dotnet list package --vulnerable --include-transitive 2>$null | Out-String
          echo $transitive
          if ($transitive -match "has the following vulnerable packages") {
            echo "⚠️ 传递依赖中发现安全漏洞（不阻断 CI）"
          } else {
            echo "✅ 传递依赖安全扫描通过！"
          }
        shell: pwsh
```

**验证步骤：**
- [ ] 提交修改后，检查 CI 是否能正常触发
- [ ] 确认 build job 独立运行，不依赖 audit
- [ ] 确认 audit job 失败时不会导致整体 workflow 失败
- [ ] 确认 NuGet 缓存命中（第二次运行时 restore 更快）

---

## 子项目 2：消灭所有紫色

### 问题诊断

当前紫色位置：
- `SettingsPage.xaml:242` - 强调色预设中的紫色色板 `#9C27B0`

MaterialDesignInXamlToolkit 的默认主题也可能带紫色（取决于 PrimaryColor 设置）。当前 App.xaml 中 `PrimaryColor="Cyan"`，AppResources 默认色是青绿 `#64FFDA`，所以主色应该没问题。但需要确认 MDIX 的内部资源有没有紫色残留。

### Task 2.1：移除紫色强调色预设

**Files:**
- Modify: `src/McServerGuard/Views/SettingsPage.xaml:241-245`

将紫色色板替换为另一种非紫色的强调色，比如粉红 `#E91E63` 或保持现状只删紫色。

**修改：**

```xml
<!-- 把紫色这组 Button 整个删掉，或者替换为粉红 -->
<Button Style="{StaticResource ColorSwatchStyle}" 
        Background="#E91E63"
        Command="{Binding SetAccentColorCommand}"
        CommandParameter="#FFE91E63"
        ToolTip="粉红" />
```

推荐：直接删除紫色色板，保持现有的 6 个色板（青、蓝、绿、黄、橙、红）就够了。

### Task 2.2：检查并替换 MDIX 中的紫色资源

**Files:**
- Modify: `src/McServerGuard/Themes/AppResources.xaml`
- Modify: `src/McServerGuard/App.xaml`
- Check: `src/McServerGuard/Services/ThemeService.cs`

**检查清单：**
- [ ] App.xaml 中的 `PrimaryColor` 和 `AccentColor` 设置是否非紫色
- [ ] ThemeService 中所有硬编码颜色是否非紫色
- [ ] AppResources.xaml 中所有 Brush/Color 资源是否非紫色
- [ ] 所有 XAML 页面中是否有硬编码紫色值

**验证步骤：**
- [ ] 全局搜索 `Purple|Violet|Indigo|#9C|#7B|#673|#6A5|#8A2|#AA0` 无结果
- [ ] 运行程序，所有界面看不到紫色
- [ ] 设置页所有色板都不含紫色

---

## 子项目 3：升级图标系统（使用 MahApps.Metro.IconPacks）

### 问题诊断

当前使用 `MaterialDesignInXamlToolkit` 自带的 `materialDesign:PackIcon`，图标选择有限，且风格偏通用。需要引入更丰富、更有象征意义的图标库。

**推荐方案：MahApps.Metro.IconPacks**
- 包含 30+ 图标库，69,000+ 图标
- 支持 Material Design Icons、Font Awesome、Phosphor Icons、Simple Icons 等
- 独立 NuGet 包，可以按需安装
- 与 WPF 原生兼容，使用方式类似 MDIX 的 PackIcon

### Task 3.1：安装 NuGet 包

**Files:**
- Modify: `src/McServerGuard/McServerGuard.csproj`

添加包引用：

```xml
<PackageReference Include="MahApps.Metro.IconPacks.FontAwesome" Version="6.2.1" />
<PackageReference Include="MahApps.Metro.IconPacks.Material" Version="6.2.1" />
```

只装 FontAwesome 和 Material 两个最常用的，后续可以再加。

### Task 3.2：定义图标映射方案

为每个功能区域选择更有象征意义的图标：

| 页面/功能 | 当前图标 | 新图标 (FontAwesome) | 理由 |
|----------|---------|---------------------|------|
| 服务器检测 | Server | Server (FA) | 服务器机柜图标更形象 |
| 系统监控 | Heart | GaugeHigh (FA) | 仪表盘=监控，更直观 |
| AI 防护 | ShieldStar | ShieldHalved (FA) | 盾牌对半=AI检测 |
| 配置编辑器 | Settings | FilePen (FA) | 文件+笔=编辑配置 |
| 插件管理 | Download | PuzzlePiece (FA) | 拼图块=插件 |
| 关于/收藏 | Star / Heart | CircleInfo (FA) | 信息=关于 |
| 警告/Alert | Alert | TriangleExclamation (FA) | 更醒目的警告 |
| 删除 | Delete | TrashCan (FA) | 垃圾桶更形象 |
| 保存 | ContentSave | FloppyDisk (FA) | 软盘=保存经典认知 |
| 搜索 | Search | MagnifyingGlass (FA) | 放大镜更直观 |

### Task 3.3：全局替换图标

**Files (需要修改的 XAML 文件):**
- Modify: `src/McServerGuard/Views/MainWindow.xaml` - 导航图标
- Modify: `src/McServerGuard/Views/ServerDetectionPage.xaml` - 检测页图标
- Modify: `src/McServerGuard/Views/SystemMonitorPage.xaml` - 监控页图标
- Modify: `src/McServerGuard/Views/AIGuardPage.xaml` - AI防护页图标
- Modify: `src/McServerGuard/Views/ConfigEditorPage.xaml` - 配置编辑页图标
- Modify: `src/McServerGuard/Views/SettingsPage.xaml` - 设置页图标
- Modify: `src/McServerGuard/Views/Controls/*.xaml` - 自定义控件图标

**替换步骤：**

1. 在每个 XAML 文件根元素添加命名空间：
   ```xml
   xmlns:iconPacks="http://metro.mahapps.com/winfx/xaml/iconpacks"
   ```

2. 替换每个 `materialDesign:PackIcon`：
   ```xml
   <!-- 旧 -->
   <materialDesign:PackIcon Kind="Server" Width="24" Height="24" />
   
   <!-- 新 -->
   <iconPacks:PackIconFontAwesome Kind="Solid_Server" Width="24" Height="24" />
   ```

3. 对于绑定图标的情况（如 BoolToPackIconConverter），需要创建新的转换器或直接用 DataTrigger

### Task 3.4：更新 BoolToPackIconConverter

**Files:**
- Modify: `src/McServerGuard/Converters/ValueConverters.cs`

如果现有 `BoolToPackIconConverter` 返回的是 MDIX 的 PackIconKind 枚举，需要改为返回 FontAwesome 图标类型，或者改用纯 XAML DataTrigger 实现。

**验证步骤：**
- [ ] 所有页面图标正常显示，不出现空白或默认图标
- [ ] 导航栏图标语义清晰，一看就知道对应什么功能
- [ ] 图标大小、颜色、对齐方式与原来一致
- [ ] 编译无错误

---

## 子项目 4：提升代码健壮性与智能化

### 问题诊断

需要从以下维度增强：
1. **全局异常处理**：AppDomain / Dispatcher / TaskScheduler 未处理异常
2. **崩溃日志**：崩溃时自动写日志文件，方便排查
3. **配置文件容错**：JSON 损坏时自动重建默认配置，而不是直接崩
4. **异步错误处理**：fire-and-forget 任务的异常捕获
5. **内存保护**：大对象、长生命周期对象的释放
6. **重试机制**：文件 I/O、网络请求等易失败操作的自动重试
7. **智能提示**：用户操作前的预判和建议

### Task 4.1：全局异常处理

**Files:**
- Modify: `src/McServerGuard/App.xaml.cs`

添加三个级别的异常捕获：

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // UI 线程未处理异常
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        
        // 非 UI 线程未处理异常
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        
        // Task 未观察异常
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        
        base.OnStartup(e);
    }
    
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "UI 线程未处理异常");
        ShowCrashDialog(e.Exception);
        e.Handled = true; // 不让程序直接崩
    }
    
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "非UI线程未处理异常 (终止进程={0})", e.IsTerminating);
        }
    }
    
    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Task未观察异常");
        e.SetObserved(); // 标记为已观察，不让进程崩
    }
}
```

### Task 4.2：配置文件容错

**Files:**
- Modify: `src/McServerGuard/Services/AppConfigService.cs`
- Modify: `src/McServerGuard/Services/ThemeService.cs`

**改动：**
- `LoadConfig` 时捕获 `JsonException` / `IOException`
- 配置文件损坏时，自动备份坏文件（`.bak`），然后加载默认配置
- 提供"恢复出厂设置"功能

```csharp
public AppConfig LoadConfig()
{
    try
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOpts);
            return config ?? new AppConfig();
        }
    }
    catch (JsonException ex)
    {
        Log.Warning(ex, "配置文件格式损坏，正在备份并重建...");
        try { File.Copy(ConfigPath, ConfigPath + ".corrupt.bak", true); } catch { }
    }
    catch (IOException ex)
    {
        Log.Warning(ex, "读取配置文件失败，使用默认配置");
    }
    
    var defaultConfig = new AppConfig();
    SaveConfig(defaultConfig);
    return defaultConfig;
}
```

### Task 4.3：智能重试帮助类

**Files:**
- Create: `src/McServerGuard/Helpers/RetryHelper.cs`

创建一个通用的重试工具，用于文件 I/O 和网络操作：

```csharp
public static class RetryHelper
{
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> action,
        int maxRetries = 3,
        int delayMs = 500,
        Action<Exception, int>? onRetry = null)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (i < maxRetries - 1)
            {
                onRetry?.Invoke(ex, i + 1);
                await Task.Delay(delayMs * (i + 1));
            }
        }
        throw new InvalidOperationException("重试次数耗尽");
    }
}
```

### Task 4.4：内存优化 - 大图/日志的及时释放

**Files:**
- Check: `src/McServerGuard/Views/MainWindow.xaml.cs`
- Check: `src/McServerGuard/Services/`

**改动：**
- 页面切换时清理上一页的大资源
- 日志文件大小限制 + 自动滚动清理
- 弱事件管理器避免内存泄漏

### Task 4.5：智能预判与用户提示

**Files:**
- Modify: `src/McServerGuard/ViewModels/ServerDetectionViewModel.cs`
- Modify: `src/McServerGuard/Views/ServerDetectionPage.xaml`

**智能功能示例：**
- 检测到服务器配置不合理时自动提示（如内存分配超过物理内存）
- 一键优化建议
- Aikar 标志自动推荐
- 备份提醒

**验证步骤：**
- [ ] 故意弄坏配置文件，程序能正常启动并自动重建
- [ ] 触发一个未捕获异常，程序不崩，写日志，显示友好提示
- [ ] 文件 I/O 失败时能自动重试
- [ ] 长时间运行后内存不持续增长

---

## 子项目 5：本地部署 .NET 编译环境

### 问题诊断

当前环境没有 dotnet CLI，无法本地编译验证。需要安装 .NET 10 SDK。

### Task 5.1：安装 .NET 10 SDK

**命令：**

```bash
# 使用 dotnet-install 脚本安装 .NET 10 SDK
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --install-dir $HOME/.dotnet

# 添加到 PATH
export PATH="$HOME/.dotnet:$PATH"
echo 'export PATH="$HOME/.dotnet:$PATH"' >> ~/.bashrc

# 验证安装
dotnet --version
```

### Task 5.2：验证编译

**命令：**

```bash
cd /workspace
dotnet restore
dotnet build src/McServerGuard/McServerGuard.csproj --configuration Release
```

### Task 5.3：添加编译脚本（可选）

**Files:**
- Create: `build.sh` (可选)

```bash
#!/bin/bash
set -e
echo "🔧 正在编译 MSMC..."
dotnet build src/McServerGuard/McServerGuard.csproj -c Release $@
echo "✅ 编译完成"
```

**验证步骤：**
- [ ] `dotnet --version` 能正常显示版本号
- [ ] `dotnet build` 能成功编译主项目
- [ ] 编译后有输出 DLL

> **注意：** WPF 项目只能在 Windows 上完整编译运行，Linux 上的 dotnet SDK 可能无法编译 WPF（因为需要 Windows Desktop SDK）。如果编译失败，改为验证语法错误和非 WPF 部分的编译。

---

## 子项目 6：参考样例研究与灵感整合

### 参考资料

已调研的参考项目/设计灵感：

1. **ChunkPanel** - Minecraft 服务器 Web 管理面板
   - 特点：实时控制台、文件管理器、权限系统、多主题
   - 可借鉴：状态指示器、KPI 卡片布局

2. **CloudGuard - Server Monitoring Dashboard** (Dribbble)
   - 特点：深空蓝 + 霓虹青配色、环形仪表盘、趋势图
   - 可借鉴：卡片悬浮效果、数据可视化样式

3. **Dashboard UI 设计最佳实践 (aidesigner.ai)**
   - 特点：240-280px 侧边栏、顶部 4-6 个 KPI 卡、F 型视觉动线
   - 可借鉴：信息层级、色彩纪律、布局结构

4. **MahApps.Metro.IconPacks**
   - 30+ 图标库，69,000+ 图标
   - 已选定用于子项目 3

### Task 6.1：整合设计灵感到现有 UI

**可落地的改进点：**

1. **KPI 卡片布局**：系统监控页顶部增加 4 个核心指标卡（CPU、内存、磁盘、网络）
2. **状态徽章**：服务器状态用颜色+图标+文字的徽章表示
3. **趋势图优化**：参考 CloudGuard 的渐变填充趋势图
4. **面包屑导航**：在内容区顶部显示当前位置路径
5. **空状态插画**：没有数据时显示友好的空状态提示（带图标）

这些改进可以作为后续迭代，不在本次计划的必做范围内。

---

## 执行顺序建议

按依赖关系和价值排序：

1. **子项目 5：本地 .NET 环境** → 先有编译环境，后续改动能验证
2. **子项目 1：CI 修复** → 让 CI 稳定，后续提交不会被误报阻断
3. **子项目 2：消灭紫色** → 最简单，改完就见效
4. **子项目 4：代码健壮性** → 核心质量提升
5. **子项目 3：图标升级** → 视觉提升，工作量较大
6. **子项目 6：设计灵感** → 长期优化方向

---

## 风险与注意事项

1. **.NET 10 是预览版**：API 可能变动，遇到奇怪错误先检查是否是版本问题
2. **WPF 在 Linux 上无法编译**：本地编译验证可能只能检查 C# 语法，XAML 需要靠 CI
3. **图标替换工作量大**：要确保每个图标都找对，不漏改
4. **全局异常处理可能掩盖 bug**：要配合完善的日志，不能默默吞异常
5. **MahApps.Metro.IconPacks 可能与 MDIX 有命名空间冲突**：注意区分 `materialDesign:` 和 `iconPacks:`
