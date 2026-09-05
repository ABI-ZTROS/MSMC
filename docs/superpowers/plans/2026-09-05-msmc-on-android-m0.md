# MSMC on Android · M0 骨架 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立 MSMC on Android 独立仓库，复制 `MSMC.Shared`（63 cs）+ `frontend` 全量，搭出 `net9.0-android` 最小 APK（无 root 显示引导页），并让 GitHub Actions 双 flavor（internal/external 占位）构建链跑绿。

**Architecture:** 单 APK 项目 `src/MSMC.Android`（.NET for Android）+ `src/MSMC.Shared` 跨平台核心库 + `frontend`（M0 仅复制不托管，网页面板在 M2 接入）。M0 的 MainActivity 为极简门面（root 状态 + 工程信息），不具备真实管理功能。CI 用 `AppFlavor` MSBuild 属性产出 internal/external 两个 APK 占位。

**Tech Stack:** .NET 9 (`net9.0-android`)、.NET for Android workload、Android SDK（GitHub Actions `android-actions/setup-android`）、Serilog、MSBuild 条件属性模拟双 flavor。

---

## 文件结构

```
ABI-ZTROS/MSMC-on-Android
├── MSMC.Android.sln
├── .gitignore
├── README.md
├── .github/workflows/ci.yml
├── src/
│   ├── MSMC.Shared/                  # 从 MSMC-on-Linux 复制（63 个 .cs + csproj）
│   └── MSMC.Android/
│       ├── MSMC.Android.csproj       # net9.0-android；AppFlavor 条件属性
│       ├── AndroidManifest.xml       # INTERNET / 前台服务 / 开机自启权限占位
│       ├── MainActivity.cs           # 极简门面：root 状态 + 版本信息 + 打开网页面板占位按钮
│       ├── App.cs                    # Application：Serilog 日志初始化
│       └── Resources/                # 图标占位（解析器必需）
└── frontend/                         # 从 MSMC-on-Linux 复制（M0 仅保管，M2 托管）
```

---

## Task 1: 创建远端仓库 + 本地克隆

**Files:** 无（纯 git 操作）

- [ ] **Step 1: 用 PAT 调 GitHub REST API 创建公开仓库**

```bash
curl -sL -X POST "https://api.github.com/user/repos" \
  -H "Authorization: Bearer ${GITHUB_PAT}" \
  -H "Accept: application/vnd.github+json" \
  -d '{"name":"MSMC-on-Android","description":"MSMC Minecraft 服务器管理 · Android 版（强制 root / 内置 Termux+JDK / 内网网页管理）","private":false}' \
  | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('html_url') or d.get('message'))"
```

Expected: 输出仓库 URL；若返回 `name already exists on this account` 则直接克隆现有仓库。

- [ ] **Step 2: 克隆到工作区并设置提交者（沿用 MSMC 惯例）**

```bash
cd /workspace
git clone "https://${GITHUB_PAT}@github.com/ABI-ZTROS/MSMC-on-Android.git"
cd MSMC-on-Android
git config user.name "Wis'adel"
git config user.email "ABI-ZTROS@users.noreply.github.com"
```

Expected: 克隆成功，`git config user.name` 输出 `Wis'adel`。

---

## Task 2: 复制 MSMC.Shared 与 frontend

**Files:**
- Copy: `/workspace/MSMC-on-Linux/src/MSMC.Shared/**` → `src/MSMC.Shared/`
- Copy: `/workspace/MSMC-on-Linux/frontend/**`（排除 node_modules/dist）→ `frontend/`

- [ ] **Step 1: 复制 Shared**

```bash
mkdir -p /workspace/MSMC-on-Android/src
cp -r /workspace/MSMC-on-Linux/src/MSMC.Shared /workspace/MSMC-on-Android/src/MSMC.Shared
find /workspace/MSMC-on-Android/src/MSMC.Shared -name "*.cs" | wc -l
```

Expected: 输出 `63`。

- [ ] **Step 2: 复制 frontend（不含构建产物）**

```bash
cp -r /workspace/MSMC-on-Linux/frontend /workspace/MSMC-on-Android/frontend
rm -rf /workspace/MSMC-on-Android/frontend/node_modules /workspace/MSMC-on-Android/frontend/dist
ls /workspace/MSMC-on-Android/frontend | head
```

Expected: `node_modules`/`dist` 不存在，其余（src/package.json/vite.config.ts 等）齐全。

- [ ] **Step 3: 复制 .gitignore 并补 Android 忽略项**

复制 MSMC-on-Linux 的 .gitignore，追加：
```gitignore
# Android
*.apk
*.aab
*.keystore
AndroidConfig/
obj/android/
bin/android/
```

- [ ] **Step 4: 提交**

```bash
git add .gitignore src/MSMC.Shared frontend
git commit -m "chore: 复制 MSMC.Shared 与 frontend（一次导入，独立演进）"
```

---

## Task 3: MSMC.Android 项目骨架

**Files:**
- Create: `src/MSMC.Android/MSMC.Android.csproj`
- Create: `src/MSMC.Android/AndroidManifest.xml`
- Create: `src/MSMC.Android/App.cs`
- Create: `src/MSMC.Android/MainActivity.cs`
- Create: `src/MSMC.Android/Resources/values/strings.xml`（最小资源，满足 aapt 解析）

- [ ] **Step 1: 写 csproj（含 AppFlavor 双 flavor 占位）**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-android</TargetFramework>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>io.NET.ZTR_OS.Android</RootNamespace>
    <AssemblyName>MSMC.Android</AssemblyName>
    <ApplicationId>io.net.ztr_os.msmc</ApplicationId>
    <ApplicationTitle>MSMC on Android</ApplicationTitle>
    <AndroidPackageFormat>apk</AndroidPackageFormat>
    <RuntimeIdentifier>android-arm64</RuntimeIdentifier>
    <AndroidSupportedAbis>arm64-v8a</AndroidSupportedAbis>

    <!-- 双 flavor 占位：internal=捆绑 Termux+JDK（M1 起）；external=不捆绑 -->
    <AppFlavor Condition="'$(AppFlavor)' == ''">internal</AppFlavor>
    <DefineConstants Condition="'$(AppFlavor)' == 'external'">$(DefineConstants);MSMC_EXTERNAL</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\MSMC.Shared\MSMC.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Serilog" Version="4.*" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.*" />
    <PackageReference Include="Serilog.Extensions.Logging" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.*" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: 写 AndroidManifest.xml**

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <uses-sdk android:minSdkVersion="26" android:targetSdkVersion="34" />
  <application android:label="MSMC on Android" android:allowBackup="false">
    <activity android:name=".MainActivity" android:exported="true">
      <intent-filter>
        <action android:name="android.intent.action.MAIN" />
        <category android:name="android.intent.category.LAUNCHER" />
      </intent-filter>
    </activity>
  </application>
  <uses-permission android:name="android.permission.INTERNET" />
  <uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
  <uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
  <uses-permission android:name="android.permission.RECEIVE_BOOT_COMPLETED" />
</manifest>
```

- [ ] **Step 3: 写 App.cs（Serilog 日志初始化）**

```csharp
using Android.App;
using Android.OS;
using Serilog;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// 应用宿主：初始化日志，M0 阶段不建 DI 全量容器（M2 接入 Shared 服务时再补）。
/// </summary>
[Application]
public class App : Application
{
    internal const string Tag = "MSMC.Android";

    public App(IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();

        var logDir = Path.Combine(GetFilesDir()?.AbsolutePath ?? "/data/user/0/io.net.ztr_os.msmc/files", "logs");
        Directory.CreateDirectory(logDir);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDir, "msmc-android-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger();
        Log.Information("[BOOT] MSMC on Android 启动 Flavor={Flavor}",
#if MSMC_EXTERNAL
            "external");
#else
            "internal");
#endif
    }
}
```

- [ ] **Step 4: 写 MainActivity（极简门面，代码构建 UI，不引第三方 UI 库）**

```csharp
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;

namespace io.NET.ZTR_OS.Android;

/// <summary>
/// 极简门面：显示 root 状态与版本信息。M0 无管理功能，M2 起承载网页面板入口。
/// </summary>
[Activity(Label = "MSMC on Android", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetTheme(Android.Resource.Style.ThemeDeviceDefaultDark);

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
            Gravity = GravityFlags.Center,
        };
        layout.SetPadding(48, 48, 48, 48);

        var title = new TextView(this)
        {
            Text = "MSMC on Android",
            TextSize = 24f,
            Gravity = GravityFlags.Center,
        };
        var status = new TextView(this)
        {
            Text = $"Flavor: {(IsExternal ? "external（非内置版）" : "internal（内置版）")}\n"
                 + $"Pid: {Android.OS.Process.MyPid()}",
            TextSize = 15f,
            Gravity = GravityFlags.Center,
        };
        status.SetTextColor(Android.Graphics.Color.Gray);
        var hint = new TextView(this)
        {
            Text = "M0 骨架 · 网页面板与开服能力将在后续里程碑上线（M2）。",
            TextSize = 13f,
            Gravity = GravityFlags.Center,
        };
        hint.SetTextColor(Android.Graphics.Color.Gray);

        layout.AddView(title);
        layout.AddView(status);
        layout.AddView(hint);

        SetContentView(layout);
    }

    private static bool IsExternal =>
#if MSMC_EXTERNAL
        true;
#else
        false;
#endif
}
```

- [ ] **Step 5: 本地/CI 编译双 flavor**

```bash
cd /workspace/MSMC-on-Android
dotnet build src/MSMC.Android/MSMC.Android.csproj -c Debug -p:AppFlavor=internal
dotnet build src/MSMC.Android/MSMC.Android.csproj -c Debug -p:AppFlavor=external
```

Expected: 两个 flavor 均 `Build succeeded`。若沙箱无 Android SDK 导致构建失败，记录报错并跳过本地（以 CI 为准）。

- [ ] **Step 6: 提交**

```bash
git add src/MSMC.Android
git commit -m "feat: MSMC.Android 最小可构建 APK 骨架（双 flavor 占位 + 引导页）"
```

---

## Task 4: 解决方案文件 + README

**Files:**
- Create: `MSMC.Android.sln`
- Create: `README.md`

- [ ] **Step 1: 生成解决方案并加入项目**

```bash
export PATH=/tmp/dotnet:$PATH
cd /workspace/MSMC-on-Android
dotnet new sln -n MSMC.Android
dotnet sln add src/MSMC.Shared/MSMC.Shared.csproj src/MSMC.Android/MSMC.Android.csproj
```

Expected: `dotnet sln list` 输出两个项目。

- [ ] **Step 2: 写 README（要点：强制 root、内置/非内置、里程碑状态）**

核心内容（后续可扩展）：
```markdown
# MSMC on Android 🐧📱

Minecraft 服务器管理 · Android 版 —— **强制 root**，内置 Termux + JDK 17/21/25/26，
无 GUI 管理页，内网网页面板 + 开服自动调起系统浏览器。

- **internal（内置版）**：捆绑完整 Termux + 4 个 JDK，离线开箱即用
- **external（非内置版）**：完整 Termux，JDK 检测已有 → 引导下载兜底

架构与方案见设计文档（MSMC 仓库 docs/superpowers/specs/2026-09-05-msmc-on-android-design.md）。

## 状态
🟡 M0 骨架 —— 最小 APK 可构建（无管理功能）
```

- [ ] **Step 3: 提交**

```bash
git add MSMC.Android.sln README.md
git commit -m "docs: 解决方案与 README（M0 骨架）"
```

---

## Task 5: CI 双 flavor 构建

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: 写 CI（含 Android SDK 安装与双构建）**

```yaml
name: CI

on:
  push:
    branches: [main]

jobs:
  build:
    name: Build internal + external APK
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Setup Android SDK
        uses: android-actions/setup-android@v3
        with:
          cmdline-tools: latest
          platforms: 'android-34'
          build-tools: '34.0.0'

      - name: Install android workload
        run: dotnet workload install android

      - name: Build internal APK
        run: dotnet build src/MSMC.Android/MSMC.Android.csproj -c Release -p:AppFlavor=internal -p:AndroidKeyStore=false

      - name: Build external APK
        run: dotnet build src/MSMC.Android/MSMC.Android.csproj -c Release -p:AppFlavor=external -p:AndroidKeyStore=false

      - name: Upload internal APK
        uses: actions/upload-artifact@v4
        with:
          name: msmc-android-internal
          path: src/MSMC.Android/bin/Release/net9.0-android/*.apk
          if-no-files-found: error

      - name: Upload external APK
        uses: actions/upload-artifact@v4
        with:
          name: msmc-android-external
          path: src/MSMC.Android/bin/Release/net9.0-android/*.apk
          if-no-files-found: error
```

> 注意：两个 flavor 产物同名会在同一输出目录互相覆盖。CI 里两次构建用 `-p:BaseIntermediateOutputPath`/`-p:OutputPath` 隔离，或 schema 改为 `-p:AppFlavor=internal -p:OutputPath=bin/Release/internal/`。落地以实际隔离方案为准。

- [ ] **Step 2: 提交并推送 main**

```bash
cd /workspace/MSMC-on-Android
git add .github/workflows/ci.yml
git commit -m "ci: internal/external 双 flavor APK 构建"
git push "https://${GITHUB_PAT}@github.com/ABI-ZTROS/MSMC-on-Android.git" main
```

- [ ] **Step 3: 轮询 CI 至绿**

```bash
curl -s --max-time 30 -H "Authorization: Bearer ${GITHUB_PAT}" \
  "https://api.github.com/repos/ABI-ZTROS/MSMC-on-Android/actions/runs?per_page=1" \
  | python3 -c "import sys,json; r=json.load(sys.stdin)['workflow_runs'][0]; print(r['status'], r['conclusion'])"
```

Expected: 修复到 `completed success`。常见坑：workload 安装慢、Android SDK 路径、`dotnet build` 需 `-p:AndroidSdkDirectory`。

---

## Task 6: 收尾核对

- [ ] **Step 1: 核对 M0 成功标准**
  - [ ] 新公开仓库存在：`ABI-ZTROS/MSMC-on-Android`
  - [ ] `src/MSMC.Shared` 63 个 .cs 完整复制、可被 Android 项目引用
  - [ ] `frontend` 全量复制（无 node_modules/dist）
  - [ ] `src/MSMC.Android` 双 flavor 均构建出 APK
  - [ ] CI 绿且产出 internal/external 两个 APK 产物

- [ ] **Step 2: 更新 README 状态为 ✅ M0 完成，并提交**

---

## 后续里程碑（独立计划，不在本 M0 计划内）

- **M1** root + libsu Bindings + 完整 Termux 解压 + 4×JDK（internal 捆绑 / external 检测+下载）+ 授权流与引导页真实 root 检测
- **M2** ServerSupervisor 多开 + WebPanel（桥接 MSMC.Shared 服务 + token 鉴权）+ 开服自动开浏览器端到端
- **M3** 监控/性能/网络/调度/通知/市场/配置全接通
- **M4** 开机自启/保活/崩溃重启/真机清单/发布产物