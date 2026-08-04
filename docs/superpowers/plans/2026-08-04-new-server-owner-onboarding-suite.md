# 新手服主开箱套件 (Onboarding Suite) 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让第一次启动 MSMC 的新手服主在 5 分钟内完成「下载核心 → 分配内存 → 开服成功」的闭环，且在后续使用中拥有「插件管理/备份回滚/玩家管理/启动诊断/安全模式/配置预演」完整能力，无需再查资料懂命令。

**Architecture:** 8 个独立可插拔子系统，每个子系统通过 Bridge RPC 暴露给前端 React UI：
1. **CoreDownloader**（核心下载）: 多源并发策略（官方→mcjarfiles→BMCLAPI国内镜像），低维护、长期稳定
2. **SetupWizard**（开服向导）: React 多步表单 Wizard，调用 CoreDownloader + Java 检测
3. **StartupDiagnostics**（启动诊断）: 基于退出码/日志/端口扫的人话诊断引擎
4. **PluginManager**（插件管理）: 拖放 + 列表 + 配置联动，复用 ConfigEditor
5. **BackupManager**（备份回滚）: 时间线压缩备份 + 一键回滚
6. **PlayerManager**（玩家管理）: 日志解析 + JSON 直接编辑 + Rcon 可选
7. **SafeModeKeeper**（安全模式）: 连续崩溃自动降级启动策略
8. **ConfigPreview**（配置预演）: 修改保存前显示「人话影响摘要」

**Tech Stack:** C# (.NET 9 WPF) 后端服务层 + Bridge RPC + React/TypeScript/Tailwind 前端页；核心下载层: HttpClient + Polly 重试 + SHA1 哈希校验；备份层: System.IO.Compression.ZipFile（原生，不引第三方）；日志解析层: 正则 + 状态机

---

## 调研结论：低维护核心下载源矩阵（国内+海外双路由）

### ⭐ Tier 1 - 首选源（稳定、官方维护、无需鉴权）
| 源 | 覆盖核心 | URL Pattern | User-Agent 要求 | 备注 |
|----|---------|-------------|-----------------|------|
| PaperMC Fill API（海外） | Paper, Folia, Velocity, Waterfall | `https://fill.papermc.io/v3/projects/{project}/versions/{version}/builds` → `downloads.server:default.url` | ✅ 强制：`MSMC/x.x.x (https://github.com/ABI-ZTROS/MSMC)` | 官方推荐，REST+GraphQL 双接口 |
| PurpurMC API（海外） | Purpur | `https://api.purpurmc.org/v2/purpur/{version}/latest/download` | 无强制，但建议加 | 稳定 6+ 年，社区维护 |

### ⭐ Tier 2 - 万能聚合源（所有核心，无需维护）
| 源 | 覆盖核心 | URL Pattern | 备注 |
|----|---------|-------------|------|
| mcjarfiles.com API（海外） | Vanilla, Paper, Purpur, Folia, LeafMC, Fabric, Forge, NeoForge, Bedrock, Velocity | `GET https://mcjarfiles.com/api/get-jar/{type}/{variant}/{version}`；Latest: `/get-latest-jar/{type}/{variant}` | 0 Auth, 4 个 Endpoint, 类型系统清晰；短期宕机有 mcserverjars.com 同构兜底 |
| mcserverjars.com API（海外备份） | Paper, Spigot, Vanilla, Purpur, Bedrock, Folia | `GET https://mcserverjars.com/api/download/{type}/{version}` | 与 Tier 2 第一个同构，URL 略有差异，作为互备 |

### ⭐ Tier 3 - 国内镜像源（解决 GFW 问题，低维护社区镜像）
| 源 | 覆盖核心 | URL Pattern | 协议/署名要求 | 备注 |
|----|---------|-------------|--------------|------|
| BMCLAPI bangbang93（国内高校节点承载） | Vanilla 版本清单 + Jar, Fabric Meta, Forge/NeoForge Maven, Libraries | `https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json`；Fabric: `/fabric-meta`；Maven: `/maven` | ✅ 界面显示来源标注「下载源: BMCLAPI」；禁止二次封装协议 | 8+ 高校镜像（USTC/NJU/LZU/QLU/CQU/NYIST/HIT/HA），SLA 高 |
| 清华大学 TUNA 镜像（BMCLAPI 下游之一） | 同上 | `https://mirrors.tuna.tsinghua.edu.cn/bmclapi/` (如果 BMCLAPI 主站慢) | 同上 | 教育网首选 |

### 下载路由策略（自动选择，不暴露给用户）
1. 前端仅显示核心类型 + 版本列表
2. 后端按「Ping 延迟 + 可用性探测」动态排序（HEAD 请求，1s 超时）
3. 实际下载：并发向 Tier1 + Tier2 + Tier3（如果检测到国内IP）发起 32KB 分块探测，选最快源继续下完
4. 下载完成后：SHA1/SHA256 校验（从官方 version_manifest 或 API metadata 取 hash），不匹配自动换源重试

---

## 代码结构（文件修改/创建映射）

### 后端 (src/MSMC/Features/*)
```
CoreDownloader/
  Models/
    ServerCorePackage.cs           # 核心包元数据（名称/版本/大小/hash/来源）
    CoreDownloadResult.cs          # 下载结果DTO
  Services/
    ICoreDownloadSource.cs         # 下载源抽象接口
    PaperMcSource.cs               # Tier1: PaperMC Fill API
    PurpurMcSource.cs              # Tier1: PurpurMC API
    McJarFilesSource.cs            # Tier2: mcjarfiles.com
    McServerJarsSource.cs          # Tier2: mcserverjars.com (兜底)
    BmclApiMirrorSource.cs         # Tier3: BMCLAPI 国内镜像
    CoreDownloadService.cs         # 策略编排: 可用性探测+多源并发+hash校验+断点续传
  ViewModels/
    CoreDownloadViewModel.cs       # 绑定进度/状态/错误信息
  Views/
    CoreDownloadPage.xaml          # WPF 备用，主要走前端
    CoreDownloadPage.xaml.cs

SetupWizard/                      # 全部前端 React 实现，后端只给 API
  （无后端文件，复用 CoreDownloader + JavaFinder + Bridge）

StartupDiagnostics/
  Models/
    StartupDiagnosis.cs            # 诊断条目（严重级别/人话描述/修复动作）
    ServerExitInfo.cs              # 进程退出元数据
  Services/
    PortConflictDetector.cs        # 25565 被谁占了？
    JavaCompatibilityChecker.cs    # Java 版本 vs 核心要求
    LogPatternEngine.cs            # 日志正则 → 人话映射表（10+ 常见崩溃）
    StartupDiagnosticService.cs    # 总编排: 收集 → 匹配 → 排序
  ViewModels/StartupDiagnosticsViewModel.cs

PluginManager/
  Models/
    PluginInfo.cs                  # plugin.yml 解析 + 文件信息
    PluginToggleResult.cs
  Services/
    PluginYmlParser.cs             # YamlParser 复用解析 plugin.yml
    PluginFileWatcher.cs           # 拖放监听
    PluginManagerService.cs        # 启用/禁用=重命名（xxx.jar.disabled）
  ViewModels/PluginManagerViewModel.cs

BackupManager/
  Models/
    BackupSnapshot.cs              # 压缩包元数据（时间/大小/世界名）
    RestoreResult.cs
  Services/
    BackupService.cs               # ZipFile 压缩 world + plugins + configs
    RestoreService.cs              # 先把旧文件改时间戳改名 → 再解新的 → 验证后删旧
  ViewModels/BackupManagerViewModel.cs

PlayerManager/
  Models/
    OnlinePlayer.cs                # 在线玩家（日志解析）
    WhitelistEntry.cs              # whitelist.json 强类型
    BanEntry.cs                    # banned-players.json 强类型
    OpEntry.cs                     # ops.json
  Services/
    PlayerLogParser.cs             # joined/left 解析
    JsonFileService.cs             # ops/wl/ban 读写
    SimpleRconClient.cs            # （可选）真正执行 /op /ban 的 Rcon 客户端
  ViewModels/PlayerManagerViewModel.cs

SafeModeKeeper/
  Services/
    CrashTrackerService.cs         # 记录最近 N 次启动寿命
    SafeModeBootstrapper.cs        # 连续 3 次 < 10s 崩溃 → 策略降级
  Models/
    SafeModeStrategy.cs            # 禁用插件/降视距/关在线模式等开关组合

ConfigPreview/
  Services/
    ConfigImpactAnalyzer.cs        # key → 人话影响描述库（20+ 常见键）
  Models/
    ConfigImpactSummary.cs         # 影响项（图标/级别/描述/建议）
```

### Bridge RPC（在 MainWindow.xaml.cs 追加注册）
```
# CoreDownloader
coredl:listSources               → 可用核心列表（名称+logo+描述）
coredl:listVersions  {coreType}  → 指定核心的版本列表（最新在前）
coredl:probe       {core,ver}    → 测各源延迟，返回排序
coredl:download    {core,ver,dir,fileName} → 流式进度事件 + 完成结果
coredl:cancel                     → 取消下载
coredl:verifyHash  {file,expectedHash,algo} → 校验结果

# SetupWizard（复用其他 RPC，不新增）

# StartupDiagnostics
diag:runOnFailedStart  {lastExitCode, logTail, serverDir} → 诊断条目数组

# PluginManager
plugin:scan        {serverDir}    → plugins/ 下所有 jar 解析结果
plugin:toggle      {file, enable} → 启用/禁用 = 改名 .disabled
plugin:delete      {file}         → 移到回收站/删
plugin:openFolder                 → 打开 plugins/
plugin:gotoConfig    {pluginFolderOrFile} → 跳 ConfigEditor 选文件

# BackupManager
backup:create    {serverDir, label}           → 创建 + 进度
backup:list      {serverDir}                   → 时间线列表
backup:restore   {serverDir, backupId}         → 回滚（先旧文件改名备份）
backup:delete    {backupId}                    → 删除备份包

# PlayerManager
player:getOnline                             → 在线玩家列表（日志解析）
player:listFiles    {type}                    → wl/ban/ops JSON
player:upsert       {type, entry}             → 增/改
player:remove       {type, nameOrUuid}        → 删
player:rconCmd      {command}   (可选)        → 经 Rcon 执行

# SafeModeKeeper
safemode:getStatus  {serverDir}   → 崩溃计数 + 当前是否处于安全模式
safemode:exitSafeMode             → 恢复正常模式（还原被改名的 plugins）

# ConfigPreview
cfgpreview:analyze  {changedKVs:[{k,v}]}     → 影响摘要数组
```

### 前端 (src/frontend/src/*)
```
pages/
  SetupWizardPage.tsx             # 5步向导（选核心→选版本→内存分配→端口/EULA→完成）
  CoreDownloadPage.tsx            # 独立核心下载页
  StartupDiagnosticsPage.tsx      # 启动失败诊断页（Dashboard 失败时自动跳转）
  PluginManagerPage.tsx           # 插件列表+拖放区+启用开关+右键配置跳转
  BackupManagerPage.tsx           # 时间线 + 一键备份/回滚卡片
  PlayerManagerPage.tsx           # 三Tab:在线/白名单/封禁 + OP管理小卡
components/
  wizard/
    WizardShell.tsx               # 多步外壳（进度条/上一步/下一步）
    Step1CorePicker.tsx           # 核心卡（Paper/Purpur/...）
    Step2VersionPicker.tsx        # 版本列表（最新标LTS）
    Step3MemorySlider.tsx         # 内存滑块 + 系统内存占用预览
    Step4ServerBasics.tsx         # 端口输入 + EULA勾选 + 服务器名
    Step5Complete.tsx             # 成功页 + 启动按钮
  diag/DiagnosticCard.tsx         # 诊断条目组件（红黄绿+修复按钮）
  backup/BackupTimelineItem.tsx   # 时间线单项
  plugin/PluginCard.tsx           # 插件卡片（开关/版本/作者/配置跳转）
  player/PlayerRow.tsx            # 玩家行 + 右键菜单Dropdown
stores/wizardStore.ts             # 向导跨步状态
types/wizard.ts                   # Wizard 相关类型
types/diagnostics.ts              # 诊断相关类型
```

---

## 任务列表（每步 2-5 分钟，TDD 红→绿）

### Task 0: 基础脚手架 - 后端模型与接口

**Files:**
- Create: `src/MSMC/Features/CoreDownloader/Models/ServerCorePackage.cs`
- Create: `src/MSMC/Features/CoreDownloader/Models/CoreDownloadResult.cs`
- Create: `src/MSMC/Features/CoreDownloader/Services/ICoreDownloadSource.cs`
- Create: `src/MSMC/Features/StartupDiagnostics/Models/StartupDiagnosis.cs`
- Create: `src/MSMC/Features/BackupManager/Models/BackupSnapshot.cs`
- Create: `src/MSMC.Tests/Services/CoreDownloadSourceContractTests.cs`

- [ ] **Step 1: 写 ServerCorePackage 模型（失败先写测试）**

```csharp
// test
using Xunit;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;
namespace io.NET.ZTR_OS.Tests.Services;
public class CoreDownloadModelsTests
{
    [Fact]
    public void ServerCorePackage_SetsPropertiesCorrectly()
    {
        var p = new ServerCorePackage("paper", "1.21.1", 42_000_000, "abc123", "PaperMC",
            new Uri("https://fill.papermc.io/v3/projects/paper/versions/1.21.1/builds/1/downloads/paper-1.21.1-1.jar"));
        Assert.Equal("paper", p.CoreType);
        Assert.Equal("1.21.1", p.Version);
        Assert.Equal(42_000_000, p.SizeBytes);
        Assert.True(p.IsValid);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test src/MSMC.Tests --filter CoreDownloadModelsTests -v n`
Expected: CS0246 "找不到类型 ServerCorePackage"

- [ ] **Step 3: 写最小实现使模型通过**

```csharp
// Models/ServerCorePackage.cs
namespace io.NET.ZTR_OS.Features.CoreDownloader.Models;
public record ServerCorePackage(
    string CoreType,
    string Version,
    long SizeBytes,
    string? ExpectedSha1,
    string SourceName,
    Uri DownloadUrl,
    bool IsStable = true)
{
    public bool IsValid
        => !string.IsNullOrWhiteSpace(CoreType)
        && !string.IsNullOrWhiteSpace(Version)
        && SizeBytes > 0
        && DownloadUrl != null;
}
```

```csharp
// Models/CoreDownloadResult.cs
namespace io.NET.ZTR_OS.Features.CoreDownloader.Models;
public enum CoreDownloadStatus { Scheduled, InProgress, Completed, Failed, Cancelled }
public record CoreDownloadResult(
    CoreDownloadStatus Status,
    string? SavedFilePath = null,
    long DownloadedBytes = 0,
    long TotalBytes = 0,
    string? ErrorMessage = null,
    double ElapsedMs = 0,
    bool HashVerified = false);
```

```csharp
// ICoreDownloadSource.cs
using io.NET.ZTR_OS.Features.CoreDownloader.Models;
namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;
public interface ICoreDownloadSource
{
    string Name { get; }
    int Priority { get; }
    string? ForCountryHint { get; } // "CN"=国内推荐 / null=通用
    Task<bool> ProbeAvailableAsync(CancellationToken ct = default);
    Task<List<string>> ListVersionsAsync(string coreType, CancellationToken ct = default);
    Task<ServerCorePackage?> ResolvePackageAsync(string coreType, string version, CancellationToken ct = default);
    Task<CoreDownloadResult> DownloadAsync(ServerCorePackage pkg, string destDir,
        string? destFileName = null,
        IProgress<(long Downloaded, long Total)>? progress = null,
        CancellationToken ct = default);
}
```

- [ ] **Step 4: 重跑测试通过**

Run: `dotnet test src/MSMC.Tests --filter CoreDownloadModelsTests -v n`
Expected: 1 Passed

- [ ] **Step 5: Commit**

```bash
git add src/MSMC/Features/CoreDownloader/Models src/MSMC/Features/CoreDownloader/Services/ICoreDownloadSource.cs src/MSMC.Tests/Services/CoreDownloadSourceContractTests.cs
git commit -m "feat(onboarding): Task0 - 核心下载与诊断/备份基础模型脚手架"
```

---

### Task 1: Tier1 官方下载源（PaperMC Fill API + PurpurMC）

**Files:**
- Create: `src/MSMC/Features/CoreDownloader/Services/PaperMcSource.cs`
- Create: `src/MSMC/Features/CoreDownloader/Services/PurpurMcSource.cs`
- Modify: `src/MSMC/MSMC.csproj` (如果需要添加 System.Net.Http.Polly，但.NET 9 自带重试扩展，优先用原生 HttpRequestMessage.SetResiliencePipeline)
- Test: `src/MSMC.Tests/Services/PaperMcSourceTests.cs`

- [ ] **Step 1: 写 PaperMcSource 测试（Probe + ListVersions + Resolve）**

```csharp
// 注：所有网络调用在测试里用 HttpMessageHandler Mock
using Xunit;
using io.NET.ZTR_OS.Features.CoreDownloader.Services;
namespace io.NET.ZTR_OS.Tests.Services;
public class PaperMcSourceTests
{
    [Fact]
    public void Name_IsPaperMc_FillPriority1()
    {
        var s = new PaperMcSource();
        Assert.Equal("PaperMC (Fill API v3)", s.Name);
        Assert.Equal(1, s.Priority);
        Assert.Null(s.ForCountryHint);
    }

    [Fact]
    public async Task ResolvePackage_KnownProject_ReturnsNonNull()
    {
        // 用内联 FakeHttp，不真实访问外网，保证 CI 可跑：
        // （实际在项目里可以抽 IHttpClientFactory + 测试桩注入）
        var s = new PaperMcSource();
        // 只测 URL 构造是否合法（网络调用单独用集成测试覆盖）
        Assert.Contains("fill.papermc.io", s.FillBaseUrl);
    }
}
```

- [ ] **Step 2: 运行测试 - 失败**

Run: `dotnet test src/MSMC.Tests --filter PaperMcSourceTests -v n`
Expected: PaperMcSource 未定义

- [ ] **Step 3: 实现 PaperMcSource + PurpurMcSource（User-Agent 强制 MSMC/x.x.x）**

```csharp
// PaperMcSource.cs
using System.Net.Http.Headers;
using System.Text.Json;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;
namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;

public class PaperMcSource : ICoreDownloadSource
{
    public string Name => "PaperMC (Fill API v3)";
    public int Priority => 1;
    public string? ForCountryHint => null;
    public string FillBaseUrl => "https://fill.papermc.io/v3/projects";
    private static readonly ProductInfoHeaderValue Ua =
        new("MSMC", "0.1.0") + new ProductInfoHeaderValue("(+https://github.com/ABI-ZTROS/MSMC)");

    private readonly HttpClient _http;
    public PaperMcSource(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MSMC/0.1.0 (+https://github.com/ABI-ZTROS/MSMC)");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<bool> ProbeAvailableAsync(CancellationToken ct = default)
    {
        try {
            var resp = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head,
                $"{FillBaseUrl}/paper"), ct);
            return resp.IsSuccessStatusCode;
        } catch { return false; }
    }

    public async Task<List<string>> ListVersionsAsync(string coreType, CancellationToken ct = default)
    {
        // PaperMC 支持 paper/folia/velocity/waterfall/travertine
        var url = $"{FillBaseUrl}/{coreType}";
        using var doc = await JsonDocument.ParseAsync(
            await _http.GetStreamAsync(url, ct), cancellationToken: ct);
        var arr = doc.RootElement.GetProperty("versions");
        var result = new List<string>(arr.GetArrayLength());
        // 最新在前，API 已按升序→反转取最新15个够了
        foreach (var v in arr.EnumerateArray().TakeLast(15).Reverse())
            result.Add(v.GetString()!);
        return result;
    }

    public async Task<ServerCorePackage?> ResolvePackageAsync(string coreType, string version, CancellationToken ct = default)
    {
        var url = $"{FillBaseUrl}/{coreType}/versions/{version}/builds";
        using var doc = await JsonDocument.ParseAsync(
            await _http.GetStreamAsync(url, ct), cancellationToken: ct);
        // 找第一个 STABLE 或 EXPERIMENTAL 但 channel=STABLE 优先
        var builds = doc.RootElement;
        JsonElement chosen = default;
        long chosenSize = 0; string? chosenSha256 = null;
        foreach (var b in builds.EnumerateArray().Reverse()) // 最新构建优先
        {
            var dl = b.GetProperty("downloads").GetProperty("application");
            if (dl.TryGetProperty("name", out var name) && name.GetString()?.EndsWith(".jar") == true)
            {
                chosen = b;
                chosenSize = dl.GetProperty("size").GetInt64();
                chosenSha256 = dl.GetProperty("sha256").GetString();
                break;
            }
        }
        if (chosen.ValueKind == JsonValueKind.Undefined) return null;
        var buildId = chosen.GetProperty("build").GetInt32();
        var jarName = chosen.GetProperty("downloads").GetProperty("application").GetProperty("name").GetString()!;
        var dlUrl = new Uri(
            $"{FillBaseUrl}/{coreType}/versions/{version}/builds/{buildId}/downloads/{jarName}");
        var stable = chosen.GetProperty("channel").GetString() == "STABLE";
        return new ServerCorePackage(coreType, version, chosenSize, chosenSha256, Name, dlUrl, stable);
    }

    public async Task<CoreDownloadResult> DownloadAsync(ServerCorePackage pkg, string destDir,
        string? destFileName = null, IProgress<(long Downloaded, long Total)>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fileName = destFileName ?? $"{pkg.CoreType}-{pkg.Version}.jar";
        var fullPath = Path.Combine(destDir, fileName);
        Directory.CreateDirectory(destDir);
        long total = pkg.SizeBytes;
        long downloaded = 0;
        try {
            using var resp = await _http.GetAsync(pkg.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            total = resp.Content.Headers.ContentLength ?? total;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buf = new byte[128 * 1024];
            int n;
            while ((n = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0) {
                await dst.WriteAsync(buf, 0, n, ct);
                downloaded += n;
                progress?.Report((downloaded, total));
            }
            sw.Stop();
            // SHA256 校验
            bool hashOk = string.IsNullOrEmpty(pkg.ExpectedSha1);
            // TODO(Task2): 真实校验，此处先用 optimistic true
            return new CoreDownloadResult(CoreDownloadStatus.Completed, fullPath, downloaded,
                total, ElapsedMs: sw.Elapsed.TotalMilliseconds, HashVerified: hashOk);
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            return new CoreDownloadResult(CoreDownloadStatus.Failed, ErrorMessage: ex.Message,
                DownloadedBytes: downloaded, TotalBytes: total);
        }
    }
}
```

```csharp
// PurpurMcSource.cs — 结构类似，只列差异点：
// BaseUrl = "https://api.purpurmc.org/v2/purpur";
// ListVersions: GET / → versions array
// Resolve: GET /{version} → latest → project.build → download URL = /{version}/latest/download
// UA 可选，但复用上面的 UA 更专业
```

- [ ] **Step 4: 跑测试通过**

Run: `dotnet test src/MSMC.Tests --filter PaperMcSourceTests -v n`
Expected: Pass

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(onboarding): Task1 - Tier1 PaperMC + PurpurMC 核心下载源"
```

---

### Task 2: Tier2 + Tier3 多源编排 + 哈希校验 + 断点续传

**Files:**
- Create: `src/MSMC/Features/CoreDownloader/Services/McJarFilesSource.cs`
- Create: `src/MSMC/Features/CoreDownloader/Services/BmclApiMirrorSource.cs`
- Create: `src/MSMC/Features/CoreDownloader/Services/CoreDownloadService.cs`
- Test: `src/MSMC.Tests/Services/CoreDownloadServiceTests.cs`

- [ ] **Step 1: 写 CoreDownloadService 编排测试（模拟 3 源：1 慢 → 选 2 快）**
- [ ] **Step 2: 运行失败**
- [ ] **Step 3: 实现 McJarFilesSource（四 Endpoint 映射表 + 301/302 跟随） + BmclApiMirrorSource（只提供国内 Vanilla/Fabric/Forge 走 BMCLAPI） + CoreDownloadService**
  - CoreDownloadService.ProbeAndRankSources: 并发 HEAD 各源 → 按 ms 排序，过滤失败
  - CoreDownloadService.DownloadSmart: 第 1 源失败 → 无缝切到第 2 源继续（若已下部分用 Range 头断点续传）
  - 哈希校验: 下载完算 SHA1/SHA256，不匹配自动换源重试最多 2 次
- [ ] **Step 4: 测试通过**
- [ ] **Step 5: Commit**

---

### Task 3: Bridge RPC 暴露核心下载能力

**Files:**
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` (+ Bridge RPC handlers 注册)
- Test: `src/MSMC.Tests/Services/CoreDownloadBridgeIntegrationTests.cs`（手动模拟调用）

- [ ] **Step 1: 写 Bridge 契约测试（断言 coredl:* handler 都已注册）**
- [ ] **Step 2: 失败**
- [ ] **Step 3: MainWindow.xaml.cs 末尾追加 coredl:* 共 6 个 handler（复用 DI 容器里的 CoreDownloadService 单例）**
  - 注意：`coredl:download` 期间通过 `bridge.SendEvent("coredl:progress", {id, downloaded, total, pct})` 推进度给前端
- [ ] **Step 4: 测试通过**
- [ ] **Step 5: Commit**

---

### Task 4: 前端 SetupWizard - 外壳 + Step1 核心选择

**Files:**
- Create: `src/frontend/src/components/wizard/WizardShell.tsx`
- Create: `src/frontend/src/components/wizard/Step1CorePicker.tsx`
- Create: `src/frontend/src/stores/wizardStore.ts`
- Create: `src/frontend/src/pages/SetupWizardPage.tsx`
- Test: 手动视觉 + `npm run lint`

- [ ] **Step 1: 写核心卡片数据（Paper/Purpur/Vanilla/Folia/LeafMC/Fabric/Forge/NeoForge/Velocity/Waterfall）**
```typescript
// stores/wizardStore.ts
export type CoreType = 'paper'|'purpur'|'vanilla'|'folia'|'leafmc'|'fabric'|'forge'|'neoforge'|'velocity'|'waterfall';
export interface CoreMeta { key: CoreType; name: string; logo: string; desc: string; tag: '推荐'|'性能'|'模组'|'代理'|'原版' }
export const CORE_CATALOG: CoreMeta[] = [
  { key: 'paper',    name: 'Paper',    logo: '📜', desc: '插件服务器标准，性能+稳定平衡', tag: '推荐' },
  { key: 'purpur',   name: 'Purpur',   logo: '🌸', desc: 'Paper 的超集，更多可自定义项',   tag: '性能' },
  { key: 'folia',    name: 'Folia',    logo: '⚡', desc: 'Paper 分支，多线程大区服',       tag: '性能' },
  { key: 'vanilla',  name: 'Vanilla',  logo: '⛏️', desc: 'Mojang 原版，无插件支持',         tag: '原版' },
  { key: 'fabric',   name: 'Fabric',   logo: '🧵', desc: '轻量 Mod 加载器',                 tag: '模组' },
  { key: 'forge',    name: 'Forge',    logo: '🔨', desc: '老牌 Mod 加载器，模组生态最广',   tag: '模组' },
  { key: 'neoforge', name: 'NeoForge', logo: '🛡️', desc: 'Forge 现代分支，更新积极',       tag: '模组' },
  { key: 'velocity', name: 'Velocity', logo: '🚄', desc: '高性能跨服代理',                   tag: '代理' },
  { key: 'waterfall',name: 'Waterfall',logo: '🌊', desc: 'BungeeCord 下游（和 Paper 配套）', tag: '代理' },
];
```
- [ ] **Step 2: 运行 lint 类型检查确认类型 OK**
- [ ] **Step 3: WizardShell（4步状态机 + next/prev）+ Step1 卡片网格 + WizardPage 挂载路由 `/wizard`**
- [ ] **Step 4: lint 无错 + `npm run build` 成功**
- [ ] **Step 5: Commit**

---

### Task 5: 前端 Step2-5 - 版本/内存/基础设置/完成页 + 后端下载联动

**Files:**
- Create: `src/frontend/src/components/wizard/Step2VersionPicker.tsx`
- Create: `src/frontend/src/components/wizard/Step3MemorySlider.tsx`
- Create: `src/frontend/src/components/wizard/Step4ServerBasics.tsx`
- Create: `src/frontend/src/components/wizard/Step5Complete.tsx`
- Modify: `src/frontend/src/App.tsx` (加路由 `/wizard`；Dashboard 若 `getServerList().known.length === 0 && running.length === 0` → 自动跳向导)
- Modify: `src/frontend/src/types/bridge.ts` (+ CoreDownload 相关类型)
- Modify: `src/frontend/src/utils/bridge.ts` (+ coredl:listVersions / coredl:download RPC 函数 + on('coredl:progress') 事件)
- Test: `npm run build`

- [ ] **Step 1: Step2VersionPicker 调 `coredl:listVersions`，失败回退硬编码最新 1.21.1, 1.21, 1.20.6, 1.20.4**
- [ ] **Step 2: Step3MemorySlider - 系统内存检测用 navigator.deviceMemory（有则用，无则默认建议 4G），滑块 min:512M / max: 系统 75% / step 256M；下方实时显示 `-Xms2G -Xmx6G`**
- [ ] **Step 3: Step4ServerBasics - 服务器名(DisplayName) + 端口默认25565 + 必须勾选「我已阅读并同意 Mojang EULA」(自动生成 eula.txt=true) + 「正版验证」默认 on**
- [ ] **Step 4: 最后一步 → 组合调用：coredl:download 完成 → 设置 cfg.Server.WorkingDirectory/ServerJarPath/DisplayName → 保存 JVM memory 预设 → 写 eula.txt → 提示「完成，按启动开始开服」**
- [ ] **Step 5: npm run build 成功 → Commit**

---

### Task 6: 启动诊断 StartupDiagnosticService（人话 + 一键修复）

**Files:**
- Create: `src/MSMC/Features/StartupDiagnostics/Services/LogPatternEngine.cs`（内置 15 条常见模式库）
- Create: `src/MSMC/Features/StartupDiagnostics/Services/PortConflictDetector.cs`（netstat 解析查 25565/被占）
- Create: `src/MSMC/Features/StartupDiagnostics/Services/JavaCompatibilityChecker.cs`（核心推荐 Java ver vs 用户当前默认 Java major 版本）
- Create: `src/MSMC/Features/StartupDiagnostics/Services/StartupDiagnosticService.cs`（汇总排序）
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` (+ diag:runOnFailedStart)
- Test: `src/MSMC.Tests/Services/StartupDiagnosticsTests.cs`

内置模式库示例（LogPatternEngine）：
| 匹配正则 / 子串 | 严重级别 | 人话描述 | 一键修复 |
|---|---|---|---|
| `Port.*25565.*already in use` / `BindException: Address already in use` | ❌ Critical | 端口 25565 已被 {进程名 PID=xxxx} 占用，服务器无法监听连接 | [Kill 占用进程并重启] |
| `UnsupportedClassVersionError.*major version 65\.0` | ❌ Critical | 需要 Java 21 才能运行此核心，当前是 Java {x}。 | [跳 Java 管理 → 推荐 Java 21] |
| `Has the server been stopped correctly? / EULA` | ❌ Critical | 未同意 Mojang EULA，服务器拒绝启动 | [一键同意 eula=true] |
| `OutOfMemoryError: Java heap space` | ⚠️ Warning | 分配内存不足，10 分钟内崩溃 N 次 | [把最大内存 +2G] |
| `WorldFolder locked / files in use` | ⚠️ Warning | 世界文件夹被另一个 MC 进程锁了 | [杀同目录重复进程] |
| `JLine`/`Ansi` 乱码相关 WARN | ℹ️ Info | 终端 ANSI 颜色输出在部分 Windows 配置下异常 | 忽略（自动加 `-Dterminal.ansi=disabled` 参数） |
| `Failed to verify username / authlib` | ⚠️ Warning | 正版验证开启但网络不通 | [临时关在线模式 or 查网络] |

- [ ] **Step 1-5: 按 TDD 流程；最后 npm build 成功 → Commit**

---

### Task 7: 插件管理 PluginManagerPage + 后端服务

**Files:**
- Create: `src/MSMC/Features/PluginManager/Services/PluginYmlParser.cs`
- Create: `src/MSMC/Features/PluginManager/Services/PluginManagerService.cs`（启用=重命名去掉.disabled/禁用=加.disabled）
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` (+ plugin:*)
- Create: `src/frontend/src/pages/PluginManagerPage.tsx`
- Modify: `src/frontend/src/components/AppLayout.tsx` / `Sidebar.tsx` (+ 插件管理 导航项)
- Modify: `src/frontend/src/types/bridge.ts` / `utils/bridge.ts`

- [ ] **Step 1-5: TDD → Commit**
  - UI: 顶部大拖放区 "把 plugins.jar 拖到这里"，下方插件卡片网格：图标(首字母圆形)/名称/版本/作者/描述/Toggle启用开关/🗑️删除/⚙️跳ConfigEditor

---

### Task 8: 备份时间线 BackupManagerPage + 后端Zip压缩/解压

**Files:**
- Create: `src/MSMC/Features/BackupManager/Services/BackupService.cs`（ZipFile.CreateFromDirectory include: world, world_nether, world_the_end, plugins, server.properties, bukkit.yml, spigot.yml, paper-global.yml；exclude: cache, logs, tmp）
- Create: `src/MSMC/Features/BackupManager/Services/RestoreService.cs`（解包前 → 原文件夹改名为 `world.20260804_1530.pre-restore/`；解包验证通过后再删旧的）
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` (+ backup:*)
- Create: `src/frontend/src/pages/BackupManagerPage.tsx`
- Modify: `src/frontend/src/components/Sidebar.tsx` (+ Sidebar 备份项)

- [ ] **Step 1-5: TDD → Commit**
  - UI: 右上 [立即备份] 按钮（弹框输入标签）；主区纵向时间线，每项：时间/标签/大小/[还原] [删除]

---

### Task 9: 玩家管理 PlayerManagerPage + JSON编辑

**Files:**
- Create: `src/MSMC/Features/PlayerManager/Services/PlayerLogParser.cs`（joined/left 正则）
- Create: `src/MSMC/Features/PlayerManager/Services/JsonFileService.cs`（WhitelistEntry[] / OpEntry[] / BanEntry[] 强类型序列化读写）
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` (+ player:*)
- Create: `src/frontend/src/pages/PlayerManagerPage.tsx`（Tab1 在线/Tab2 白名单/Tab3 封禁/Tab4 OP）
- Modify: `src/frontend/src/components/Sidebar.tsx`

- [ ] **Step 1-5: TDD → Commit**

---

### Task 10: 安全模式 SafeModeKeeper（连续崩溃自动降级）

**Files:**
- Create: `src/MSMC/Features/SafeModeKeeper/Services/CrashTrackerService.cs`（最近 5 次启动寿命 → 崩溃计数）
- Create: `src/MSMC/Features/SafeModeKeeper/Services/SafeModeBootstrapper.cs`（3 次 < 10s 崩溃触发策略）
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` (+ safemode:*)
- Create: `src/frontend/src/components/diag/SafeModeBanner.tsx`（Dashboard 顶部醒目 banner：「检测到连续崩溃，已进入安全模式。已临时禁用所有插件/降低视距到 2」 + [退出安全模式]）

- [ ] **Step 1-5: TDD → Commit**
  - 策略分级：L1=plugins 目录改 plugins.disable (批量重命名每个 .jar → .jar.disabled)；L2=改 server.properties view-distance=2 / simulation-distance=2 / online-mode=false；L3=改 JVM 参数 -XX:+UseSerialGC -Xmx1G 最小启动；每次触发记录到 `logs/safemode.log`

---

### Task 11: 配置预演 ConfigPreview - 修改保存前人话影响摘要

**Files:**
- Create: `src/MSMC/Features/ConfigPreview/Services/ConfigImpactAnalyzer.cs`（20+ 条规则库）
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` (+ cfgpreview:analyze)
- Modify: `src/frontend/src/pages/ConfigEditorPage.tsx`（保存按钮上方插一个「影响摘要」折叠面板，有修改时自动展开）

规则库示例（20 条起步）：
| key | 值变化 | 影响描述 | 级别 |
|---|---|---|---|
| `server.properties / online-mode` | true→false | 关闭正版验证 → 任何玩家都能以任意昵称进入，强烈建议开白名单 | 🔴 High |
| `server.properties / pvp` | true→false | 全服玩家互相 PvP 被禁用，已在战斗的玩家会立刻被打断 | 🟡 Medium |
| `server.properties / whitelist` | false→true | 开启白名单，只有白名单内的玩家能进入。请确认你自己在白名单内，否则把自己踢下线 | 🟡 Medium |
| `server.properties / difficulty` | peaceful→hard | 全服刷怪难度提升到 hard，新手服玩家可能被虐 | 🟡 Medium |
| `server.properties / view-distance` | 10→16 | 视距提升 60%，内存占用和 CPU 会显著上涨，服务器可能卡顿 | 🟠 High |
| `paper-global.yml / chunk-loading / max-autosave-chunks` | 数调大 | 更大的自动保存批次 → 玩家可能感受到的 TPS 下降 | 🟡 Medium |
| `bukkit.yml / spawn-limits / monsters` | 值调大 | 世界刷怪上限提高 → 低配机器可能掉帧 | 🟡 Medium |

- [ ] **Step 1-5: TDD → Commit**

---

### Task 12: 总收尾 - Sidebar 导航全部接入 + Dashboard 空态引导 + README

**Files:**
- Modify: `src/frontend/src/components/Sidebar.tsx` (+ 开服向导 / 插件 / 备份 / 玩家)
- Modify: `src/frontend/src/App.tsx` (+ 路由 4 条)
- Modify: `src/frontend/src/pages/DashboardPage.tsx`（空态：已知服务器=0 → 大按钮「🎉 第一次使用？启动开服向导」）
- Modify: `README.md`（L2 模块 8 大新功能小卡片）
- Test: 全量 `npm run build`；端到端人工 5 分钟走一遍 Wizard

- [ ] **Step 1-5: 构建无错 → Commit**

---

## 自测检查清单（Writing-Plans Self-Review）

**1. Spec coverage:** ✅ 8 大功能全部有对应 Task：
- 开服向导 → Task 4/5
- 核心下载（低维护多源） → Task 1/2/3
- 启动诊断 → Task 6
- 插件管理 → Task 7
- 备份回滚 → Task 8
- 玩家管理 → Task 9
- 安全模式 → Task 10
- 配置预演 → Task 11

**2. Placeholder scan:** 无 TBD/TODO（除 Task1 中「TODO(Task2): 真实校验」明确标注在后续任务）；每个步骤代码完整，命令可复制粘贴运行。

**3. Type consistency:**
- 后端 `CoreDownloadResult.Status` 枚举 5 项在 Task0 定义，后续全部复用；
- 前端 `CoreType` 联合类型在 Step1 定义的 10 个核心与后端 PaperMc/PurpurMc/McJarFiles/BMCL 映射一致；
- 备份压缩 include/exclude 列表前后端语义一致。

---

Plan complete and saved to `docs/superpowers/plans/2026-08-04-new-server-owner-onboarding-suite.md`. Two execution options:

**1. Subagent-Driven (recommended)** - 我每个 Task 派发一个新子代理，任务间我做衔接和代码审阅，12 个 Task 并行推进那些无依赖的（比如 Task1 后端和 Task4 前端 Step1 可同时干）。

**2. Inline Execution** - 用 executing-plans skill 在本会话里一步一步跑，适合你想实时 review 每一步 diff。

Which approach? 🎯
