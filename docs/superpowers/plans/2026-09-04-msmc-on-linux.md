# MSMC on Linux 实施计划

> **⚠️ 变更记录（2026-09-04 当日，用户决策）**：**梦幻联动（自动同步）已取消**。MSMC 与 MSMC-on-Linux 改为**一次导入、独立演进**：
> - MSMC-on-Linux 仓库已独立落地：从 MSMC 导入跨平台逻辑为 `src/MSMC.Shared`，前端复制为 `frontend/`，Linux 系统服务为 `src/MSMC.Linux`（Avalonia 壳 + 自建同源 HTTP 桥 + `/proc` 解析）。
> - **不部署 `sync-to-linux.yml`**、不做 push 镜像；两仓库各自直推 main，代码可自由分叉演进。
> - 本项目落地状态见仓库 [MSMC-on-Linux](https://github.com/ABI-ZTROS/MSMC-on-Linux) README（✅ 已落地：骨架 + 共享库 + Linux 服务 + 桥宿主 + 冒烟测试 + CI）。
> 下方第 0/1/2 节仍保留原调研与联动设计作为历史决策记录。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Linux 上实现与 Windows MSMC 功能对等的 Minecraft 服务器管理客户端——Avalonia 壳 + WebKitGTK WebView 承载现有 React 前端；共享代码采用**一次导入、独立演进**（原方案为"共享库 + 自动同步工作流"实现梦幻联动，已于 2026-09-04 取消）。

**Architecture:** 抽取 MSMC 全部跨平台纯托管逻辑为 `MSMC.Shared` 库（调度/通知/插件市场/配置编辑器/持久化/桥协议，约 50% 功能面），Windows 版与 Linux 版共同引用。MSMC-on-Linux = Avalonia 窗口壳 + Avalonia.WebView(WebKitGTK) 承载 React 前端 + 一层 Linux 系统服务（`/proc`、`taskset`、`cgroup`、`systemd`、`notify-send` 替代 WMI/netsh/Job Object/注册表/Toast）。"梦幻联动" = MSMC 仓库 push 到 main 时，GitHub Actions `sync-to-linux.yml` 自动把 `MSMC.Shared` 与 `frontend` 镜像到 MSMC-on-Linux 仓库并提交推送。

**Tech Stack:** .NET 9、Avalonia 11（Skia 渲染）、Avalonia.WebView（WebKitGTK）、React 18 + Vite + TS + Tailwind（现有前端原样复用）、CommunityToolkit.Mvvm、YamlDotNet、Serilog、MSBuild 前端构建（沿用 MSMC.csproj 的 BuildFrontend 模式）、systemd/taskset/cgroup 集成。

---

## 0. 决策记录（调研 + 用户确认，2026-09-04）

| 决策点 | 结论 | 依据 |
|---|---|---|
| 产品形态 | **Avalonia 壳 + WebView 承载 React**（与 Windows 版"WPF 壳 + WebView2"同构） | 用户选择；前端复用率 90% |
| 部署目标 | **多发行版全支持**（Ubuntu/Debian/Fedora/Arch + x64/ARM64） | 用户选择；对应 `linux-x64;linux-arm64` RID |
| 联动语义 | **代码级自动同步**（非数据同步）：MSMC 功能更新 → Linux 版自动收到 | 用户澄清；方案 = 共享库 + push-based 同步工作流 |
| 新仓库 | `ABI-ZTROS/MSMC-on-Linux`（public，已创建 2026-09-04） | 用户选择 |
| 分支策略 | 两端均直推 main（沿用现有习惯，不建分支） | 用户既有约定 |

---

## 1. 技术栈盘点结论（证据见审计）

### 1.1 可复用层（移入 `MSMC.Shared`，Linux 直接编译运行）

| 模块 | 关键文件 | Windows-only? |
|---|---|---|
| 调度器 | `Scheduler/Services/CronParser.cs`、`SchedulerService.cs`、`SchedulerStorageService.cs` | 否 |
| 通知 | `Notifications/Services/*`（Discord/Generic/Email/NotificationService） | 否（纯 HTTP/SMTP） |
| 插件市场 | `ContentMarket/Services/{Modrinth,Hangar,Spiget}Provider.cs`、`MarketProviderFactory.cs`、`PluginManagerService.cs` | 否（HTTP + SHA1 + 原子写） |
| 配置编辑器 | `ConfigEditor/Services/{YamlParser,PropertiesParser,ConfigManager,ConfigDescriptorRegistry,ConfigFormatDetector}.cs` | 否（YamlDotNet） |
| 指标持久化 | `SystemMonitoring/Services/MetricsPersistenceService.cs`、`MetricsDownsampler.cs` | 否（自研二进制 + 降采样） |
| 配色/oklch | `Settings/Colors/{OkLchColor,ColorHelper}.cs`、`ThemePresetRegistry.cs` | 否 |
| 流量采集 | `NetworkMonitor/Services/NetworkTrafficService.cs`（`System.Net.NetworkInformation`，网卡名白名单需按 Linux iface 微调） | 否 |
| 端口转发 | `NetworkMonitor/Services/TcpForwarderService.cs`（纯 TcpListener） | 否 |
| 工具/解析 | `Shared/Services/UndoRedoStack.cs`、`CommandLineParser.cs`、`ServerTypeClassifier.cs`、`JarCoreIdentifier.cs`（Zip 流式）、`ServerConstants.cs`、`JvmArgumentConstants.cs`、`StartupScriptDetector.cs` | 否 |
| 桥协议 | `WebView2/Services/BridgeMessage.cs`、`Shared/Models/*`、前端 `types/bridge.ts` | 否（纯消息模型） |
| **前端全部** | `src/frontend/**`（React/Vite/TS/Tailwind，dist 为纯静态资产） | 否 |

### 1.2 必须重写层（Linux 侧，MSMC-on-Linux 内实现）

| Windows 依赖 | Linux 替代方案 |
|---|---|
| WMI/PerformanceCounter（SystemMonitor/MemoryMonitor/ThreadAnalyzer/CpuIdentifier/DiskSpaceMonitor） | `/proc/stat`、`/proc/meminfo`、`/proc/diskstats`、`/proc/<pid>/stat` 解析 |
| `GetExtendedTcpTable`（PortToProcessMapper） | `/proc/net/tcp{,6}` + `/proc/net/udp{,6}` + `/proc/*/fd`（或调用 `ss -ltnp`） |
| `netsh portproxy`/ServiceController（NetshPortBridgeService） | 用户态 TcpForwarder（已有可复用）或 iptables/nftables（root） |
| Win32 Job Objects / CreateProcessW（ProcessSupervisor/ServerManager） | 进程组（`setsid`）+ `kill(-pgid)` + cgroup v2（可选）+ `systemd-run` |
| `SetProcessInformation`/powercfg/CPU Set（CpuPowerService） | `taskset`/`sched_setaffinity`、`cpupower`、cpuset cgroup |
| 注册表/where.exe（JavaFinder） | `$PATH`、`JAVA_HOME`、`/usr/lib/jvm`、`update-alternatives --list java` |
| WindowsPrincipal/UAC（PrivilegeService） | `sudo`/polkit、`geteuid()`、capabilities |
| UWP Toast（ToastNotificationService） | `notify-send`（libnotify）/ Avalonia `NotificationManager` |
| DWM/Mica（WindowEffectsService） | Avalonia 原生无边框 + 阴影（平台无关） |
| WebView2 Runtime（WebView2BridgeService） | WebKitGTK（Avalonia.WebView），同一套 `BridgeMessage` 协议 + `window.__msmc_bridge__` 注入 |

---

## 2. 梦幻联动（代码自动同步）机制设计

**核心：MSMC（上游）→ MSMC-on-Linux（下游）单向镜像共享代码，push-based 自动同步。**

```
MSMC 仓库 main
  ├─ src/MSMC.Shared/**        ← 抽取的跨平台核心（新增）
  ├─ src/frontend/**           ← React 前端（已有）
  └─ .github/workflows/sync-to-linux.yml   ← 新增同步工作流
        │  推送命中路径时触发：
        │  checkout MSMC(当前) → checkout MSMC-on-Linux → rsync/copy 共享目录
        │  → commit "sync: mirror MSMC@<sha>" → push MSMC-on-Linux main
        ▼
MSMC-on-Linux 仓库 main
  ├─ deps/MSMC.Shared/**       ← 自动镜像（与上游逐字节一致）
  ├─ deps/frontend/**          ← 自动镜像
  └─ src/MSMC.Linux/**         ← Linux 壳/系统服务（本仓库独有，不被覆盖）
```

- **目录白名单**（触发 + 复制范围）：`src/MSMC.Shared/`、`src/frontend/`、`src/MSMC/Features/WebView2/Services/BridgeMessage.cs`（桥协议单文件若未被 MSMC.Shared 收纳则单独同步）。
- **凭据**：GitHub Actions 默认 `GITHUB_TOKEN` 无法写另一仓库 → 需在 MSMC 仓库 Secrets 配 `LINUX_SYNC_PAT`（对 MSMC-on-Linux 有 `Contents: write` 的 fine-grained PAT）。
- **为什么不用**：
  - *git submodule*：依赖双方手动 `git submodule update`，做不到"自动"。
  - *一次性两份代码*（用户提到的备选）：必然漂移，违背"自动同步"。
- **冲突纪律**：镜像目录为只读（CI 或约定），MSMC-on-Linux 开发者不改 `deps/**`，避免下次同步覆盖本地改动（三链：返回链——同步即"上游覆盖"语义）。

---

## 3. 文件结构映射

### 3.1 MSMC 仓库（本次改动）

```
src/MSMC.Shared/                      # 新增：跨平台核心库
  MSMC.Shared.csproj                  # net9.0（非 -windows）
  Scheduler/  Notifications/  ContentMarket/
  ConfigEditor/  SystemMonitoring.Services/  NetworkMonitor.Services/
  Settings.Colors/  Shared/  BridgeMessage.cs
src/MSMC/MSMC.csproj                  # 改：ProjectReference -> ..\MSMC.Shared\MSMC.Shared.csproj
  Features/...                         # 保留 Windows-only 服务 + ViewModels + WPF/WebView2 壳
.github/workflows/sync-to-linux.yml   # 新增：自动同步到 MSMC-on-Linux
docs/superpowers/plans/2026-09-04-msmc-on-linux.md  # 本计划
```

### 3.2 MSMC-on-Linux 仓库（新建，public）

```
MSMC.sln
src/MSMC.Linux/                       # Avalonia 壳 + Linux 服务 + 桥宿主
  MSMC.Linux.csproj                   # net9.0;linux-x64;linux-arm64
  Program.cs  App.axaml(.cs)  MainWindow.axaml(.cs)
  Hosting/BridgeHost.cs               # WebView JS 桥 → C#（Avalonia.WebView）
  Hosting/BridgeActionRegistrar.cs    # 注册 ~120 action 的 Linux 实现
  LinuxServices/                       # /proc 解析、taskset、notify-send 等
    LinuxProcessScanner.cs  LinuxSystemMonitor.cs
    LinuxPortMapper.cs  LinuxJavaFinder.cs  LinuxSupervisor.cs  ...
deps/MSMC.Shared/                     # 自动镜像（只读，勿手改）
deps/frontend/                        # 自动镜像（构建时 vite build）
.github/workflows/ci.yml              # linux-x64 + linux-arm64 构建/测试/产物
docs/superpowers/plans/2026-09-04-msmc-on-linux.md  # 本计划副本
```

---

## 4. Milestone 0：骨架 + 共享库抽取 + 自动同步 + 首个页面跑通（详细任务）

> 本里程碑产出**可运行的最小闭环**：Linux 窗口弹出并渲染 React Dashboard，通过桥拿到真实 `ping`/`app:getInfo`，且 MSMC push 后 Linux 仓库自动收到 `MSMC.Shared`/`frontend`。

### Task 1: 抽取 `MSMC.Shared` 并让 MSMC 引用

**Files:**
- Create: `src/MSMC.Shared/MSMC.Shared.csproj`
- Modify: `src/MSMC/MSMC.csproj`（加 ProjectReference + 移除已移出文件的编译项——SDK 自动 glob 会避免重复）
- Move（git mv，保持历史）: 1.1 表内列出的全部纯托管 `.cs` 文件 → `src/MSMC.Shared/<Feature>/...`

- [ ] **Step 1: 创建共享库 csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>io.NET.ZTR_OS</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="16.*" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="9.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.*" />
    <PackageReference Include="Serilog" Version="4.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 移动共享文件**

```bash
cd /workspace/MSMC
git mv src/MSMC/Features/Scheduler/Services/CronParser.cs src/MSMC.Shared/Scheduler/
# ...对 1.1 表内所有文件执行 git mv（按 Feature 目录组织）
```

- [ ] **Step 3: MSMC.csproj 引用共享库**

在 `<ItemGroup>` 中加入：

```xml
<ItemGroup>
  <ProjectReference Include="..\MSMC.Shared\MSMC.Shared.csproj" />
</ItemGroup>
```

- [ ] **Step 4: 编译验证 Windows 侧不破**

Run: `dotnet build src/MSMC/MSMC.csproj -c Release -p:EnableWindowsTargeting=true`
Expected: `Build succeeded`（若个别 Windows-only 类误入 Shared 导致编译错，git mv 回 MSMC 并在 1.2 表登记）

- [ ] **Step 5: 提交**

```bash
git add -A src/MSMC.Shared src/MSMC/MSMC.csproj
git commit -m "refactor(shared): 抽取跨平台 MSMC.Shared 库（调度/通知/市场/配置编辑器/持久化/桥协议）"
```

### Task 2: 新建 MSMC-on-Linux Avalonia 壳工程

**Files:**
- Create: `MSMC.sln`
- Create: `src/MSMC.Linux/MSMC.Linux.csproj`
- Create: `src/MSMC.Linux/Program.cs`
- Create: `src/MSMC.Linux/App.axaml` / `App.axaml.cs`
- Create: `src/MSMC.Linux/MainWindow.axaml` / `MainWindow.axaml.cs`
- Create: `src/MSMC.Linux/Hosting/BridgeHost.cs`

- [ ] **Step 1: 创建 csproj（多发行版 + 多架构）**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>io.NET.ZTR_OS.Linux</RootNamespace>
    <RuntimeIdentifiers>linux-x64;linux-arm64</RuntimeIdentifiers>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.*" />
    <PackageReference Include="Avalonia.WebView" Version="0.11.*" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\deps\MSMC.Shared\MSMC.Shared.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Program.cs 引导**

```csharp
using Avalonia;

namespace io.NET.ZTR_OS.Linux;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();
}
```

- [ ] **Step 3: App.axaml.cs 装配 DI（复用 MSMC.Shared 的纯逻辑）**

```csharp
using Microsoft.Extensions.DependencyInjection;
using io.NET.ZTR_OS.Scheduler.Services;
using io.NET.ZTR_OS.Notifications.Services;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = new ServiceCollection()
            .AddSingleton<ISchedulerService, SchedulerService>()
            // ... 其余 MSMC.Shared 内服务（调度/通知/市场/持久化）
            .AddSingleton<Hosting.BridgeHost>()
            .BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 4: MainWindow.axaml 放置 WebView**

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:web="using:Avalonia.WebView"
        x:Class="io.NET.ZTR_OS.Linux.MainWindow"
        Width="1280" Height="800" Title="MSMC on Linux">
  <web:WebView x:Name="Host" />
</Window>
```

- [ ] **Step 5: 提交**

```bash
git add -A
git commit -m "feat(linux): Avalonia 壳骨架 + WebView 占位 + DI 装配共享库"
```

### Task 3: 桥宿主（WebKitGTK JS 桥 → C#）

> 复用 MSMC 的 `BridgeMessage` 协议（`{type,id,action,payload}`）。与 WebView2 的差异：用 `WebView.ExecuteScriptAsync` 注入 `window.__msmc_bridge__`，用 `WebMessageReceived`（或自定义 JS→C# 通道）收请求。

**Files:**
- Create: `src/MSMC.Linux/Hosting/BridgeHost.cs`

- [ ] **Step 1: 注入桥脚本（与 MSMC `AddScriptToExecuteOnDocumentCreatedAsync` 等价）**

```csharp
public async Task InjectBridgeAsync(WebView webView)
{
    const string script = """
        (function () {
          if (window.__msmc_bridge__) return;
          let seq = 0; const pending = new Map();
          window.__msmc_bridge__ = {
            invoke(action, payload) {
              return new Promise((resolve, reject) => {
                const id = String(++seq);
                pending.set(id, { resolve, reject });
                window.__msmc_native_host__(JSON.stringify({ type:'request', id, action, payload }));
                setTimeout(() => { if (pending.has(id)) { pending.delete(id); reject(new Error('timeout')); } }, 30000);
              });
            },
            on(action, cb) { /* 订阅 C# 事件 */ }
          };
          window.__msmc_resolve__ = (msg) => {
            const m = JSON.parse(msg);
            const p = pending.get(m.id);
            if (p) { pending.delete(m.id); m.error ? p.reject(new Error(m.error)) : p.resolve(m.payload); }
          };
        })();
        """;
    await webView.ExecuteScriptAsync(script);
    // 并注册 webView.WebMessageReceived -> OnNativeRequest(webView, json)
}
```

- [ ] **Step 2: 路由请求到 Linux 实现（对照 MSMC 的 ~120 个 action）**

```csharp
public async Task<string> OnNativeRequestAsync(WebView webView, string json)
{
    var msg = JsonSerializer.Deserialize<BridgeMessage>(json);
    var result = msg.Action switch
    {
        "ping" => new { success = true, pong = true },
        "app:getInfo" => await GetAppInfoAsync(),
        "app:getTime" => new { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
        // ... 其余 action 委托给 LinuxServices + MSMC.Shared 服务
        _ => throw new NotSupportedException($"未实现 action: {msg.Action}")
    };
    var resp = new { msg.Type, msg.Id, payload = result, error = (string?)null, success = true };
    await webView.ExecuteScriptAsync($"window.__msmc_resolve__({JsonSerializer.Serialize(JsonSerializer.Serialize(resp))})");
    return "{}";
}
```

- [ ] **Step 3: 加载 React 前端（从 deps/frontend/dist 起本地 http 或 file 服务）**

首版用 `webView.Source = new Uri("file:///.../deps/frontend/dist/index.html")`；后续引入 Kestrel 静态托管以规避 CORS（与 MSMC `SetVirtualHostMapping` 等价）。

- [ ] **Step 4: 提交**

```bash
git add -A
git commit -m "feat(linux): WebKitGTK JS 桥宿主，注入 __msmc_bridge__ 并路由 ping/app:*"
```

### Task 4: 自动同步工作流（梦幻联动核心）

**Files:**
- Create: `.github/workflows/sync-to-linux.yml`（MSMC 仓库）

- [ ] **Step 1: 写工作流**

```yaml
name: sync-to-linux
on:
  push:
    branches: [ main ]
    paths:
      - 'src/MSMC.Shared/**'
      - 'src/frontend/**'
      - 'src/MSMC/Features/WebView2/Services/BridgeMessage.cs'
jobs:
  sync:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout MSMC (upstream)
        uses: actions/checkout@v5
        with:
          fetch-depth: 0
          path: upstream
      - name: Checkout MSMC-on-Linux (downstream)
        uses: actions/checkout@v5
        with:
          repository: ABI-ZTROS/MSMC-on-Linux
          token: ${{ secrets.LINUX_SYNC_PAT }}
          path: downstream
      - name: Mirror shared code
        run: |
          rm -rf downstream/deps/MSMC.Shared downstream/deps/frontend
          cp -r upstream/src/MSMC.Shared  downstream/deps/MSMC.Shared
          cp -r upstream/src/frontend      downstream/deps/frontend
      - name: Commit + push downstream
        run: |
          cd downstream
          git config user.name "msmc-sync-bot"
          git config user.email "msmc-sync-bot@users.noreply.github.com"
          git add -A deps/
          if git diff --cached --quiet; then echo "no changes"; exit 0; fi
          git commit -m "sync: mirror MSMC@$(git -C ../upstream rev-parse --short HEAD)"
          git push origin main
```

- [ ] **Step 2: 在 MSMC 仓库配置 Secret `LINUX_SYNC_PAT`**（对 MSMC-on-Linux `Contents:write` 的 fine-grained PAT；说明写入计划"凭据"节）。

- [ ] **Step 3: 触发一次验证**：push 一个 `MSMC.Shared` 假改动 → 观察下游仓库收到 `sync: mirror MSMC@<sha>` 提交。

- [ ] **Step 4: 提交**

```bash
git add .github/workflows/sync-to-linux.yml
git commit -m "ci(sync): 新增 MSMC→MSMC-on-Linux 共享代码自动同步工作流"
```

### Task 5: Linux CI（多发行版产物）

**Files:**
- Create: `.github/workflows/ci.yml`（MSMC-on-Linux 仓库）

- [ ] **Step 1: 写 CI（沿用 Windows 版矩阵思路）**

```yaml
name: MSMC-on-Linux CI
on:
  push: { branches: [ main ] }
  pull_request: { branches: [ main ] }
jobs:
  build:
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        rid: [ linux-x64, linux-arm64 ]
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-dotnet@v5
        with: { dotnet-version: '9.0.x' }
      - name: Build frontend (deps/frontend)
        working-directory: deps/frontend
        run: |
          npm ci
          npm run build
      - name: Publish self-contained
        run: dotnet publish src/MSMC.Linux/MSMC.Linux.csproj -c Release -r ${{ matrix.rid }} --self-contained true -o publish
      - name: Upload artifact
        uses: actions/upload-artifact@v5
        with:
          name: MSMC-on-Linux-${{ matrix.rid }}
          path: publish/
          retention-days: 30
```

- [ ] **Step 2: 提交**（在 MSMC-on-Linux 仓库）

```bash
git add .github/workflows/ci.yml
git commit -m "ci: linux-x64/linux-arm64 构建 + 前端构建 + 自包含产物"
git push origin main
```

### Task 6: 首个页面跑通（Dashboard + 真实 ping）

**Files:**
- Modify: `src/MSMC.Linux/Hosting/BridgeHost.cs`（补齐 `systemMonitor:getMetrics` 首个真实数据）

- [ ] **Step 1: 写失败测试（桥协议，xunit，放 `deps/MSMC.Shared` 或本仓库 Tests）**

```csharp
[Fact]
public async Task Bridge_Routes_Ping()
{
    var host = new BridgeHost(/* DI */);
    var resp = await host.OnNativeRequestAsync(null!, """{"type":"request","id":"1","action":"ping"}""");
    Assert.Contains("\"success\":true", resp);
}
```

- [ ] **Step 2: 实现 `ping` + `app:getInfo` 返回 Linux 真实信息**

`app:getInfo` 返回：OS = `Environment.OSVersion` + `/etc/os-release` 读取、架构 = `RuntimeInformation.ProcessArchitecture`、内核 = `uname -r`（`System.Runtime.InteropServices` + 读 `/proc/sys/kernel/osrelease`）。

- [ ] **Step 3: 运行测试**

Run: `dotnet test tests/` Expected: PASS

- [ ] **Step 4: 提交** → `feat(linux): ping/app:getInfo 桥实现 + 桥协议测试`

- [ ] **Step 5: 端到端验收**：`dotnet run -p src/MSMC.Linux` → 弹出窗口 → React Dashboard 显示本机架构/系统信息（桥链路通）。

---

## 5. Milestone 1+：功能面移植（子计划入口，按模块拆分为独立 plan）

> 每模块 = 一个独立 sub-plan（writing-plans 各自展开为 bite-size 任务），全部完成后与 Windows 版功能对等。共用模式：**Linux 实现服务 → 在 BridgeHost 注册 action → 前端不动（已自动同步）→ CI 绿灯 → 提交**。

| 里程碑 | 模块 | 关键 Linux 实现 | 复用（MSMC.Shared/前端） |
|---|---|---|---|
| M1 | 系统监控 | `LinuxSystemMonitor`（/proc/stat+meminfo+diskstats+net/dev）、`LinuxCpuIdentifier`（/proc/cpuinfo+`lscpu`）、`LinuxThreadAnalyzer`（/proc/<pid>/stat） | MetricsPersistence/Downsampler、SystemMonitorPage、GaugeRing/DualLineChart |
| M1 | 服务器检测/启停 | `LinuxProcessScanner`（/proc 枚举+cmdline+env）、`LinuxPortMapper`（/proc/net/tcp）、`LinuxSupervisor`（setsid+kill(-pgid)+cgroup） | JarCoreIdentifier、ServerTypeClassifier、ServerDetector 判定逻辑、Dashboard/ConfigEditor |
| M2 | 网络监控 | `/proc/net/dev` 流量、`ss`/`/proc/net/tcp` 端口、TcpForwarder（复用） | NetworkTrafficService、NetworkMonitorPage |
| M2 | Java 管理 | `LinuxJavaFinder`（update-alternatives/$PATH/JAVA_HOME） | JvmArgumentConstants、JavaPage |
| M2 | 调度/通知/市场/配置编辑器 | 无系统依赖（全复用）——仅接桥 | 全量复用，前端已同步 |
| M3 | 主题/设置/权限 | Avalonia 主题绑定替代 DWM；`sudo`/polkit 权限检测；`notify-send` Toast | ThemeService 配色逻辑、SettingsPage、oklch |
| M3 | 发布/联动 | systemd unit 模板、`notify-send` 集成、sync 工作流调优 | 无 |

---

## 6. Self-Review

**Spec 覆盖**：
- ✅ 建仓（Task 0 / 决策记录）——已建 public `ABI-ZTROS/MSMC-on-Linux`
- ✅ 技术栈调研（§1，证据 file:line）
- ✅ 跨平台方案评估（§0 决策 + §1.2 Linux 替代矩阵，联网核验 Avalonia/WebKitGTK）
- ✅ 梦幻联动（§2 同步机制 + Task 4 工作流）
- ✅ Avalonia 壳 + WebView 承载 React（Task 2/3）
- ✅ 多发行版全支持（Task 5 matrix linux-x64/arm64）
- ✅ 写方案（本文件）

**Placeholder 扫描**：Task 1-6 全部含可执行代码/命令；§5 里程碑仅列模块清单（明确标注为独立 sub-plan 入口，非"待定"填充）。

**类型一致性**：`BridgeMessage`（type/id/action/payload）与 MSMC 现有协议一致；`BridgeHost.OnNativeRequestAsync(WebView?, string)` 在 Task 3 定义、Task 6 测试复用同一签名；`window.__msmc_bridge__.invoke(action,payload)` 与前端 `utils/bridge.ts` 现有调用约定一致（`bridge.invoke(action, payload)`）。

---

## 7. 执行交接

计划已保存：`docs/superpowers/plans/2026-09-04-msmc-on-linux.md`（本仓库）+ 将同步至 `ABI-ZTROS/MSMC-on-Linux`。

**两种执行方式：**
1. **Subagent-Driven（推荐）**：每个 Task 派发独立 subagent，任务间人工 review，迭代快
2. **Inline Execution**：本会话用 executing-plans 批量执行，带检查点

**选哪种？**
