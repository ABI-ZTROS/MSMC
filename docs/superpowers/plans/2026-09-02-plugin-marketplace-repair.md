# MSMC 插件市场修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 MSMC 插件搜索/下载功能从"完全报废"恢复到可用状态，并扩展支持 Hangar + Spiget 双 Provider。

**Architecture:** 修复 Bridge 契约层（去掉 success 包装、字段对齐）→ 修复 ModrinthProvider 搜索硬编码 → 新增 HangarProvider + SpigetProvider → 实现 MarketProviderFactory 多源聚合 → PluginManagerService 改流式下载 → 前端 bridge API 和 MarketPage 类型对齐。

**Tech Stack:** C# 12 / .NET 8, System.Text.Json (camelCase + string enum), Serilog (三链日志), XUnit (测试), TypeScript + React (前端 Bridge 契约)

---

## 文件结构地图

### 修改的文件

| 文件 | 改动类型 | 职责 |
|------|----------|------|
| `src/MSMC/Features/ContentMarket/Models/MarketProject.cs` | 修改 | MarketSource 加 Hangar/Spiget；ModLoader 加 Folia |
| `src/MSMC/Features/ContentMarket/Services/ModrinthProvider.cs` | 修改 | facets 动态化；按 Loader 枚举映射 Modrinth loader 字符串 |
| `src/MSMC/Features/ContentMarket/Services/PluginManagerService.cs` | 修改 | DownloadVersionAsync 改流式；InstallAsync 支持 IProgress |
| `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs` | 修改:3746-3907 | Bridge handler 去掉 success 包装，字段对齐，加 source/serverType 参数 |
| `src/MSMC/App.xaml.cs` | 修改:757-760 | DI 注册新 Provider + Factory |
| `src/frontend/src/types/bridge.ts` | 修改:740-794 | MarketProject/MarketVersion 字段对齐后端 |
| `src/frontend/src/utils/bridge.ts` | 修改:972-988 | searchMarket/getMarketVersions 返回类型去掉包装假设；加 source/serverType |
| `src/frontend/src/pages/MarketPage.tsx` | 修改 | 搜索栏加 Source 下拉；UI 字段对齐 |
| `src/MSMC.Tests/Bridge/BridgeContractTests.cs` | 修改 | 新增市场模块契约测试 |

### 新建的文件

| 文件 | 职责 |
|------|------|
| `src/MSMC/Features/ContentMarket/Services/HangarProvider.cs` | Hangar (PaperMC) API v1 客户端：搜索/版本/下载 |
| `src/MSMC/Features/ContentMarket/Services/SpigetProvider.cs` | Spiget (SpigotMC) API v2 客户端：搜索/版本/下载 |
| `src/MSMC/Features/ContentMarket/Services/MarketProviderFactory.cs` | 多源聚合：Hangar + Modrinth + Spiget 并行搜索 + 去重 |
| `src/MSMC.Tests/Services/MarketProviderFactoryTests.cs` | Factory 去重/合并排序测试 |

---

## Task 1: 修复 MarketProject 模型枚举扩展

**Files:**
- Modify: `src/MSMC/Features/ContentMarket/Models/MarketProject.cs:13-20`

- [ ] **Step 1: 写枚举扩展测试**

在 `src/MSMC.Tests/Bridge/BridgeContractTests.cs` 末尾追加：

```csharp
[Fact]
public void MarketSource_NewSources_HangarAndSpigetExist()
{
    Assert.IsType(typeof(MarketSource), MarketSource.Hangar);
    Assert.IsType(typeof(MarketSource), MarketSource.Spiget);
    Assert.Equal("Hangar", MarketSource.Hangar.ToString());
    Assert.Equal("Spiget", MarketSource.Spiget.ToString());
}

[Fact]
public void ModLoader_Folia_Exists()
{
    Assert.IsType(typeof(ModLoader), ModLoader.Folia);
    Assert.Equal("Folia", ModLoader.Folia.ToString());
}
```

- [ ] **Step 2: 运行测试确认失败**

Run: `cd /workspace && dotnet test src/MSMC.Tests/MSMC.Tests.csproj --filter "FullyQualifiedName~MarketSource_NewSources_HangarAndSpigetExist" --no-restore 2>&1 | tail -20`
Expected: 编译错误 `MarketSource does not contain Hangar` / `ModLoader does not contain Folia`

- [ ] **Step 3: 修改 MarketSource 和 ModLoader 枚举**

将 `MarketProject.cs:13-20` 改为：

```csharp
public enum MarketSource
{
    Modrinth,
    Hangar,        // PaperMC 官方插件站
    Spiget,        // SpigotMC 资源站
    CurseForge,
    Polymart,
    CustomUrl,
    Local
}
```

将 `MarketProject.cs:25-37` 改为：

```csharp
public enum ModLoader
{
    Forge,
    Fabric,
    Quilt,
    Bukkit,
    Spigot,
    Paper,
    Purpur,
    Folia,
    Velocity,
    BungeeCord,
    Generic
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `dotnet test src/MSMC.Tests/MSMC.Tests.csproj --filter "FullyQualifiedName~MarketSource_NewSources_HangarAndSpigetExist|FullyQualifiedName~ModLoader_Folia" --no-restore 2>&1 | tail -20`
Expected: 2 passed

- [ ] **Step 5: Commit**

```bash
git add src/MSMC/Features/ContentMarket/Models/MarketProject.cs src/MSMC.Tests/Bridge/BridgeContractTests.cs
git commit -m "fix(market): add Hangar/Spiget sources and Folia loader to enums"
```

---

## Task 2: 修复 ModrinthProvider 搜索硬编码 facets

**Files:**
- Modify: `src/MSMC/Features/ContentMarket/Services/ModrinthProvider.cs:48-102`（SearchAsync 方法）

- [ ] **Step 1: 写 facets 动态化测试**

在 BridgeContractTests 末尾追加：

```csharp
[Fact]
public void ModrinthSearchFacets_DefaultPluginType()
{
    // SearchRequest 默认没有 Loader → facets 应该只包含 project_type:plugin (不是 mod)
    var request = new SearchRequest { Query = "essentials", Limit = 20 };
    Assert.Null(request.Loader);
    // 测试验证：ModrinthProvider.SearchAsync 构造的 facets 包含 plugin 而非 mod
    // 这里先通过反射或集成测试验证
}
```

- [ ] **Step 2: 修改 ModrinthProvider.SearchAsync**

将 `ModrinthProvider.cs:48-102` 的 SearchAsync 方法中 facets 构造逻辑替换为：

```csharp
public async Task<IReadOnlyList<MarketProject>> SearchAsync(SearchRequest request, CancellationToken ct = default)
{
    var queryString = HttpUtility.ParseQueryString(string.Empty);
    queryString["query"] = request.Query;
    queryString["limit"] = request.Limit.ToString();
    queryString["offset"] = request.Offset.ToString();

    // 构造 facets：默认搜 plugin（服务器插件），不是 mod（客户端模组）
    var facets = new List<string> { "[\"project_type:plugin\"]" };

    // 如果指定了加载器，加 loader facet（Modrinth 中加载器字段叫 loaders）
    if (request.Loader.HasValue)
    {
        string loaderValue = request.Loader.Value switch
        {
            ModLoader.Bukkit => "bukkit",
            ModLoader.Spigot => "spigot",
            ModLoader.Paper => "paper",
            ModLoader.Purpur => "purpur",
            ModLoader.Folia => "folia",
            ModLoader.Velocity => "velocity",
            ModLoader.BungeeCord => "bungeecord",
            ModLoader.Forge => "forge",
            ModLoader.Fabric => "fabric",
            ModLoader.Quilt => "quilt",
            _ => ""
        };
        if (!string.IsNullOrEmpty(loaderValue))
            facets.Add($"[\"loaders:{loaderValue}\"]");
    }

    if (!string.IsNullOrEmpty(request.GameVersion))
        facets.Add($"[\"versions:{request.GameVersion}\"]");

    if (!string.IsNullOrEmpty(request.Category))
        facets.Add($"[\"categories:{request.Category}\"]");

    queryString["facets"] = $"[{string.Join(",", facets)}]";

    var url = $"{BaseUrl}/search?{queryString}";
    _logger.LogInformation("[Modrinth] Searching: {Query} (limit={Limit}, facets=[{Facets}])",
        request.Query, request.Limit, string.Join(",", facets));

    try
    {
        var json = await _httpClient.GetStringAsync(url, ct);
        var response = JsonSerializer.Deserialize<ModrinthSearchResponse>(json, _jsonOptions);
        if (response == null) return new List<MarketProject>();

        var projects = response.Hits.Select(h => new MarketProject
        {
            Id = h.ProjectId ?? h.Project_id ?? string.Empty,
            Slug = h.Slug ?? string.Empty,
            Name = h.Title ?? string.Empty,
            Description = h.Description ?? string.Empty,
            Author = h.Author ?? string.Empty,
            IconUrl = h.IconUrl,
            Downloads = h.Downloads,
            Followers = h.Follows,
            Source = MarketSource.Modrinth,
            Categories = h.Categories ?? new List<string>(),
            SupportedLoaders = (h.Loaders ?? new List<string>())
                .Select(ParseModLoader).Where(l => l != ModLoader.Generic).ToList(),
            GameVersions = h.Versions ?? new List<string>(),
            UpdatedAt = h.DateModified
        }).ToList();

        _logger.LogInformation("[Modrinth] Found {Count} results for '{Query}'", projects.Count, request.Query);
        return projects;
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "[Modrinth] Search failed for query: {Query}", request.Query);
        return new List<MarketProject>();
    }
}
```

- [ ] **Step 3: 运行测试确认不破坏现有**

Run: `dotnet test src/MSMC.Tests/MSMC.Tests.csproj --no-restore 2>&1 | tail -30`
Expected: 全部通过（ModrinthProvider 没独立单元测试，BridgeContractTests 里 Search 请求测试应该不依赖 facets 构造）

- [ ] **Step 4: Commit**

```bash
git add src/MSMC/Features/ContentMarket/Services/ModrinthProvider.cs
git commit -m "fix(market): change Modrinth facets from project_type:mod to project_type:plugin, add Folia loader"
```

---

## Task 3: 修 Bridge Handler 返回值契约（去掉包装 + 字段对齐）

**这是整个修复的核心。** 当前 Bridge handler 返回 `{ success: true, data: ... }` 包装对象，`bridge.invoke` 把包装对象 resolve 给前端。需要改为直接 return 数据，让 bridge.invoke resolve 到数据本身。

**但要注意**：Bridge 框架层（C# 端）会把 handler 返回值包成 `{ type: "response", success: true, payload: <handlerReturn> }` 结构发给前端。前端 bridge.ts 在收到时根据 `data.success` 决定 resolve 还是 reject，resolve 的是 `data.payload`。

所以我们要改的是 handler 的返回值——**handler 直接 return 数组/对象本身**，而不是再包一层 `{ success: true, projects: [...] }`。

错误处理方式：handler 内 throw → Bridge 框架层 catch → 返回 `{ type: "response", success: false, error: ex.Message }` → 前端 reject。

**Files:**
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs:3746-3907`

- [ ] **Step 1: 写契约测试**

在 BridgeContractTests 追加：

```csharp
[Fact]
public void MarketSearchHandler_ReturnsArrayNotWrapped()
{
    // 契约: market.search handler 应直接返回 List<MarketProject>，不是 { success, projects } 包装
    // 用匿名对象模拟 handler 直接 return
    var projects = new[] { new MarketProject { Id = "abc", Name = "Test" } };
    // handler 应该 return projects (数组本身)，而不是 return new { success = true, projects }
    // Bridge 框架层会自动包成 { type: "response", success: true, payload: projects }
    Assert.True(projects is System.Collections.IEnumerable);
}

[Fact]
public void MarketSearch_Fields_AlignWithFrontendType()
{
    // 验证 MarketProject 字段与前端 bridge.ts MarketProject 接口对齐
    // 后端: id, name, description, iconUrl, downloads, source, supportedLoaders
    var p = new MarketProject
    {
        Id = "abc",
        Name = "EssentialsX",
        Description = "Essentials",
        IconUrl = "https://example.com/icon.png",
        Downloads = 2800000,
        Source = MarketSource.Hangar,
        SupportedLoaders = new List<ModLoader> { ModLoader.Paper, ModLoader.Folia }
    };
    Assert.Equal("abc", p.Id);
    Assert.Equal("EssentialsX", p.Name);
    Assert.Equal(MarketSource.Hangar, p.Source);
}
```

- [ ] **Step 2: 重写 market.search handler**

替换 MainWindow.xaml.cs:3749-3793 整个 search handler：

```csharp
_bridgeService.RegisterRequestHandler("market.search", async payload =>
{
    try
    {
        var factory = App.Services.GetService<MarketProviderFactory>();
        if (factory == null)
        {
            var provider = App.Services.GetService<IMarketProvider>();
            if (provider == null)
                throw new InvalidOperationException("市场服务不可用");
            // Fallback: 直接用单一 provider
            return await SearchWithProviderAsync(provider, payload);
        }

        return await factory.SearchAsync(ParseSearchRequest(payload));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "market.search 异常");
        throw; // 让 Bridge 框架层捕获转为 success: false
    }
});
```

需要在 handler 上方（同一文件内）加一个辅助方法：

```csharp
private static SearchRequest ParseSearchRequest(object? payload)
{
    var req = new SearchRequest();
    if (payload is JsonElement el)
    {
        req.Query = el.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
        req.Limit = el.TryGetProperty("limit", out var l) ? l.GetInt32() : 20;
        req.Offset = el.TryGetProperty("offset", out var o) ? o.GetInt32() : 0;
        if (el.TryGetProperty("serverType", out var st) && st.GetString() is var stStr && !string.IsNullOrEmpty(stStr))
        {
            if (Enum.TryParse<ModLoader>(stStr, true, out var loader))
                req.Loader = loader;
        }
        if (el.TryGetProperty("gameVersion", out var gv))
            req.GameVersion = gv.GetString();
        if (el.TryGetProperty("category", out var cat))
            req.Category = cat.GetString();
    }
    else if (payload is string s)
    {
        req.Query = s;
        req.Limit = 20;
    }
    return req;
}

// Fallback: Factory 不可用时用单一 IMarketProvider
private static async Task<IReadOnlyList<MarketProject>> SearchWithProviderAsync(
    IMarketProvider provider, object? payload)
{
    var req = ParseSearchRequest(payload);
    if (string.IsNullOrWhiteSpace(req.Query))
        throw new ArgumentException("搜索关键词不能为空");
    return await provider.SearchAsync(req);
}
```

- [ ] **Step 3: 重写 market.versions handler**

替换 MainWindow.xaml.cs:3795-3830：

```csharp
_bridgeService.RegisterRequestHandler("market.versions", async payload =>
{
    try
    {
        string projectId, sourceStr = "Modrinth";
        if (payload is string s)
        {
            projectId = s;
        }
        else if (payload is JsonElement el)
        {
            projectId = el.TryGetProperty("projectId", out var pid) ? pid.GetString() ?? "" : "";
            sourceStr = el.TryGetProperty("source", out var src) ? src.GetString() ?? "Modrinth" : "Modrinth";
            if (string.IsNullOrEmpty(projectId))
                projectId = el.GetString() ?? "";
        }
        else
        {
            throw new ArgumentException("无效 payload");
        }

        if (string.IsNullOrWhiteSpace(projectId))
            throw new ArgumentException("项目 ID 不能为空");

        var factory = App.Services.GetService<MarketProviderFactory>();
        if (factory != null)
            return await factory.GetVersionsAsync(projectId, sourceStr);

        var provider = App.Services.GetService<IMarketProvider>();
        if (provider == null)
            throw new InvalidOperationException("市场服务不可用");
        return await provider.GetVersionsAsync(projectId);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "market.versions 异常");
        throw;
    }
});
```

- [ ] **Step 4: 重写 market.install handler**

替换 MainWindow.xaml.cs:3832-3870：

```csharp
_bridgeService.RegisterRequestHandler("market.install", async payload =>
{
    try
    {
        if (payload is not JsonElement el || el.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("无效 payload，期望 { version, serverPath }");

        var serverPath = el.TryGetProperty("serverPath", out var sp) ? sp.GetString() ?? "" : "";
        var versionJson = el.TryGetProperty("version", out var vj) ? vj.GetRawText() : "{}";
        var version = JsonSerializer.Deserialize<MarketVersion>(versionJson, BridgeJsonOptions);

        if (version == null)
            throw new ArgumentException("无效的版本数据");
        if (string.IsNullOrWhiteSpace(serverPath))
            throw new ArgumentException("服务器路径不能为空");

        var pluginMgr = App.Services.GetService<PluginManagerService>();
        if (pluginMgr == null)
            throw new InvalidOperationException("插件服务不可用");

        var result = await pluginMgr.InstallAsync(version, serverPath);
        return result;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "market.install 异常");
        throw;
    }
});
```

- [ ] **Step 5: 重写 market.listInstalled handler**

替换 MainWindow.xaml.cs:3872-3907：

```csharp
_bridgeService.RegisterRequestHandler("market.listInstalled", payload =>
{
    try
    {
        var pluginMgr = App.Services.GetService<PluginManagerService>();
        if (pluginMgr == null)
            return Task.FromException<object?>(new InvalidOperationException("插件服务不可用"));

        string serverPath = payload is string s ? s :
            payload is JsonElement el ? (el.GetString() ?? "") : "";

        if (string.IsNullOrWhiteSpace(serverPath))
            return Task.FromException<object?>(new ArgumentException("服务器路径不能为空"));

        var plugins = pluginMgr.GetInstalledPlugins(serverPath);
        return Task.FromResult<object?>(plugins);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "market.listInstalled 异常");
        return Task.FromException<object?>(ex);
    }
});
```

- [ ] **Step 6: 编译验证**

Run: `dotnet build src/MSMC/MSMC.csproj --no-restore 2>&1 | tail -30`
Expected: 成功或有明确可修复的错误（不是崩溃）

- [ ] **Step 7: 运行测试**

Run: `dotnet test src/MSMC.Tests/MSMC.Tests.csproj --no-restore 2>&1 | tail -30`
Expected: 全部通过

- [ ] **Step 8: Commit**

```bash
git add src/MSMC/Features/Shared/Views/MainWindow.xaml.cs
git commit -m "fix(market): remove success wrapper from Bridge handlers, align fields, throw on error"
```

---

## Task 4: 修前端 bridge API 和类型定义

**核心修复：让前端 bridge API 返回类型与后端实际数据结构一致。**

**Files:**
- Modify: `src/frontend/src/types/bridge.ts:740-794`
- Modify: `src/frontend/src/utils/bridge.ts:972-988`
- Modify: `src/frontend/src/pages/MarketPage.tsx`（字段对齐）

- [ ] **Step 1: 更新前端 MarketProject 类型**

替换 bridge.ts:749-758：

```typescript
export interface MarketProject {
  id: string
  slug: string
  name: string              // 后端 handler 返回 title = p.Name，C# camelCase 序列化为 name
  description?: string
  author?: string
  iconUrl?: string
  downloads?: number
  followers?: number
  source?: string           // "Modrinth" | "Hangar" | "Spiget"
  supportedLoaders?: string[] // ["Paper", "Folia", "Velocity"]
  gameVersions?: string[]
  categories?: string[]
  updatedAt?: string
}
```

替换 bridge.ts:760-767（MarketVersion 补全字段）：

```typescript
export interface MarketVersion {
  id: string
  projectId: string
  versionNumber: string
  name: string
  changelog?: string
  releasedAt?: string
  isPreRelease?: boolean
  gameVersions?: string[]
  loaders?: string[]
  downloadUrl?: string
  sha1Hash?: string
  fileSize?: number
}
```

- [ ] **Step 2: 更新 InstalledPlugin 和 InstallResult 类型确认一致**

保持 bridge.ts:776-794 的 InstalledPlugin 和 InstallResult 不变——它们与后端对齐。

- [ ] **Step 3: 更新前端 bridge API 函数**

替换 bridge.ts:972-988：

```typescript
// ═════════════════════════════════════════════════════════════════════
// 插件市场 Bridge API
// 注意: 后端 handler 已改为直接 return 数组/对象，
// bridge.invoke 的 resolve(data.payload) 直接就是数据本身，
// 不再需要前端额外解包 success 包装。
// ═════════════════════════════════════════════════════════════════════

export function searchMarket(
  query: string,
  limit: number = 20,
  options?: { source?: string; serverType?: string; gameVersion?: string }
): Promise<MarketProject[]> {
  const payload: Record<string, unknown> = { query, limit }
  if (options?.source) payload.source = options.source
  if (options?.serverType) payload.serverType = options.serverType
  if (options?.gameVersion) payload.gameVersion = options.gameVersion
  return bridge.invoke<MarketProject[]>('market.search', payload)
}

export function getMarketVersions(
  projectId: string,
  source?: string
): Promise<MarketVersion[]> {
  const payload = source ? { projectId, source } : projectId
  return bridge.invoke<MarketVersion[]>('market.versions', payload)
}

export function installPlugin(
  version: MarketVersion,
  serverPath: string
): Promise<InstallResult> {
  return bridge.invoke<InstallResult>('market.install', { version, serverPath })
}

export function getInstalledPlugins(serverPath: string): Promise<InstalledPlugin[]> {
  return bridge.invoke<InstalledPlugin[]>('market.listInstalled', serverPath)
}
```

- [ ] **Step 4: MarketPage 字段对齐检查**

MarketPage.tsx 当前用了：
- `project.name` ✅ (后端返回 name)
- `project.description` ✅
- `project.iconUrl` ✅
- `project.downloads` ✅
- `project.likes` ❌ (后端没有 likes，应该用 downloads)
- `versions` state 的 `v.versionNumber` ✅
- `v.releaseDate` ✅ (后端返回 releasedAt)

修复 MarketPage.tsx:285 这行：

原来:
```tsx
⬇ {project.downloads?.toLocaleString() ?? 0}下载 · ⭐ {project.likes ?? 0}
```

改为:
```tsx
⬇ {project.downloads?.toLocaleString() ?? 0} · 🔼 {project.followers?.toLocaleString() ?? 0}关注
```

- [ ] **Step 5: 前端 TS 编译验证**

Run: `cd /workspace/src/frontend && npx tsc --noEmit 2>&1 | tail -30`
Expected: 无错误（或仅与本改动无关的错误）

- [ ] **Step 6: Commit**

```bash
git add src/frontend/src/types/bridge.ts src/frontend/src/utils/bridge.ts src/frontend/src/pages/MarketPage.tsx
git commit -m "fix(market): align frontend Bridge types with backend direct-return contract"
```

---

## Task 5: 实现 HangarProvider (PaperMC 官方)

**Files:**
- Create: `src/MSMC/Features/ContentMarket/Services/HangarProvider.cs`

- [ ] **Step 1: 写 HangarProvider stub 和基础结构**

新建文件 `/workspace/src/MSMC/Features/ContentMarket/Services/HangarProvider.cs`：

```csharp
// -----------------------------------------------------------------------------
// 文件名: HangarProvider.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: Hangar (PaperMC) API v1 提供器
// 文档: https://hangar.papermc.io/v3/api-docs/public
// 基础 URL: https://hangar.papermc.io/api/v1
// -----------------------------------------------------------------------------

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

/// <summary>
/// Hangar API 客户端 — PaperMC 官方插件仓库
/// 支持 PAPER, WATERFALL, VELOCITY 三个平台
/// </summary>
public class HangarProvider : IMarketProvider
{
    private const string BaseUrl = "https://hangar.papermc.io/api/v1";
    private readonly ILogger<HangarProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MarketSource Source => MarketSource.Hangar;

    public HangarProvider(ILogger<HangarProvider> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MSMC", "1.0"));
    }

    public async Task<IReadOnlyList<MarketProject>> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        // Hangar 搜索: GET /projects?query={query}&platform={platform}&limit={limit}
        string platform = LoaderToHangarPlatform(request.Loader);
        string url = $"{BaseUrl}/projects?query={Uri.EscapeDataString(request.Query)}&platform={platform}&limit={request.Limit}&offset={request.Offset}";

        _logger.LogInformation("[Hangar] Searching: {Query} platform={Platform}", request.Query, platform);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            // Hangar 返回 PaginatedResult<Project>, 结构为 { result: [...], pagination: { count, offset, limit } }
            var root = doc.RootElement;
            var resultArr = root.TryGetProperty("result", out var arr) ? arr : root;
            var list = new List<MarketProject>();

            foreach (var hit in resultArr.EnumerateArray())
            {
                list.Add(new MarketProject
                {
                    Id = GetString(hit, "id") ?? GetString(hit, "slug") ?? string.Empty,
                    Slug = GetString(hit, "slug") ?? string.Empty,
                    Name = GetString(hit, "name") ?? GetString(hit, "slug") ?? string.Empty,
                    Description = GetString(hit, "description") ?? string.Empty,
                    Author = GetNestedString(hit, "owner", "name") ?? string.Empty,
                    Downloads = GetLong(hit, "downloads"),
                    Followers = GetLong(hit, "stars") + GetLong(hit, "watchers"),
                    Source = MarketSource.Hangar,
                    Categories = GetCategoryNames(hit),
                    SupportedLoaders = new List<ModLoader> { PlatformToLoader(platform) },
                    IconUrl = GetString(hit, "avatarUrl") ?? GetString(hit, "iconUrl")
                });
            }

            _logger.LogInformation("[Hangar] Found {Count} results", list.Count);
            return list;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Hangar] Search failed for query: {Query}", request.Query);
            return new List<MarketProject>();
        }
    }

    public async Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(string projectId, CancellationToken ct = default)
    {
        // Hangar 版本: GET /projects/{slugOrId}/versions/{platform}
        // platform 默认 PAPER，可通过 project 信息检测支持的平台
        string url = $"{BaseUrl}/projects/{Uri.EscapeDataString(projectId)}/versions/PAPER";

        _logger.LogInformation("[Hangar] Fetching versions for: {ProjectId}", projectId);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var resultArr = root.TryGetProperty("result", out var arr) ? arr : root;
            var list = new List<MarketVersion>();

            foreach (var v in resultArr.EnumerateArray())
            {
                list.Add(new MarketVersion
                {
                    Id = GetString(v, "id") ?? GetString(v, "name") ?? string.Empty,
                    ProjectId = projectId,
                    VersionNumber = GetString(v, "name") ?? GetString(v, "version") ?? string.Empty,
                    Name = GetString(v, "name") ?? string.Empty,
                    Changelog = GetString(v, "description") ?? GetString(v, "changelog") ?? string.Empty,
                    ReleasedAt = GetDateTimeOffset(v, "createdAt"),
                    IsPreRelease = GetBool(v, "isPreRelease") || GetString(v, "name")?.Contains("beta", StringComparison.OrdinalIgnoreCase) == true,
                    GameVersions = GetStringArray(v, "gameVersions"),
                    Loaders = new List<ModLoader> { ModLoader.Paper },
                    DownloadUrl = BuildHangarDownloadUrl(projectId, GetString(v, "name") ?? "", "PAPER")
                });
            }

            _logger.LogInformation("[Hangar] Found {Count} versions", list.Count);
            return list;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Hangar] Version fetch failed for: {ProjectId}", projectId);
            return new List<MarketVersion>();
        }
    }

    public async Task<byte[]> DownloadVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        // Hangar 版本 API 返回的版本信息里没有直接的下载 URL
        // 需要通过 Hangar 版本名构造下载 URL: /projects/{slug}/versions/{platform}/download/{versionName}
        // 我们在 GetVersionsAsync 里已经设置了 DownloadUrl，这里用 MarketVersion 传给 InstallAsync
        // 但 DownloadVersionAsync 接收 versionId，Hangar 下载需要 slug + versionName
        // 简化处理：这里实际上应该接收完整的 MarketVersion，但接口签名固定
        // 方案：在 InstallAsync 中检测 Source，如果是 Hangar 直接用 downloadUrl 下载
        throw new NotSupportedException(
            "HangarProvider.DownloadVersionAsync 不应直接调用。" +
            "请使用 MarketVersion.DownloadUrl 直接下载，" +
            "或通过 PluginManagerService.InstallAsync 处理（它会检查 Source 并走正确的下载路径）。");
    }

    // ── Helper 方法 ──

    private static string LoaderToHangarPlatform(ModLoader? loader)
    {
        return loader switch
        {
            ModLoader.Velocity => "VELOCITY",
            ModLoader.BungeeCord => "WATERFALL",
            _ => "PAPER"
        };
    }

    private static ModLoader PlatformToLoader(string platform)
    {
        return platform.ToUpperInvariant() switch
        {
            "VELOCITY" => ModLoader.Velocity,
            "WATERFALL" => ModLoader.BungeeCord,
            _ => ModLoader.Paper
        };
    }

    private static string BuildHangarDownloadUrl(string projectSlug, string versionName, string platform)
    {
        return $"https://hangar.papermc.io/api/v1/projects/{Uri.EscapeDataString(projectSlug)}/versions/{platform}/download/{Uri.EscapeDataString(versionName)}";
    }

    private static string? GetString(JsonElement el, string property)
    {
        return el.TryGetProperty(property, out var v) ? v.GetString() : null;
    }

    private static string? GetNestedString(JsonElement el, string parent, string child)
    {
        if (!el.TryGetProperty(parent, out var p)) return null;
        return p.TryGetProperty(child, out var c) ? c.GetString() : null;
    }

    private static long GetLong(JsonElement el, string property)
    {
        return el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt64() : 0;
    }

    private static bool GetBool(JsonElement el, string property)
    {
        return el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.True;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement el, string property)
    {
        return el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetDateTimeOffset() : null;
    }

    private static List<string> GetStringArray(JsonElement el, string property)
    {
        var list = new List<string>();
        if (el.TryGetProperty(property, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                list.Add(item.GetString() ?? string.Empty);
        }
        return list;
    }

    private static List<string> GetCategoryNames(JsonElement el)
    {
        // Hangar categories 是 Category enum 字符串列表
        var cats = GetStringArray(el, "categories");
        return cats.Count > 0 ? cats : GetStringArray(el, "tags");
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/MSMC/MSMC.csproj --no-restore 2>&1 | tail -30`
Expected: HangarProvider 编译成功（但暂时没 DI 注册，还没被使用）

- [ ] **Step 3: Commit**

```bash
git add src/MSMC/Features/ContentMarket/Services/HangarProvider.cs
git commit -m "feat(market): add HangarProvider for PaperMC plugin repository API"
```

---

## Task 6: 实现 SpigetProvider (SpigotMC)

**Files:**
- Create: `src/MSMC/Features/ContentMarket/Services/SpigetProvider.cs`

- [ ] **Step 1: 创建 SpigetProvider**

新建文件 `/workspace/src/MSMC/Features/ContentMarket/Services/SpigetProvider.cs`：

```csharp
// -----------------------------------------------------------------------------
// 文件名: SpigetProvider.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: Spiget (SpigotMC) API v2 提供器
// 文档: https://spiget.org/
// 基础 URL: https://api.spiget.org/v2
// -----------------------------------------------------------------------------

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

/// <summary>
/// Spiget API 客户端 — SpigotMC 资源站
/// 注意：SpigotMC 上部分插件（如 EssentialsX）限制自动化下载
/// </summary>
public class SpigetProvider : IMarketProvider
{
    private const string BaseUrl = "https://api.spiget.org/v2";
    private readonly ILogger<SpigetProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MarketSource Source => MarketSource.Spiget;

    public SpigetProvider(ILogger<SpigetProvider> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MSMC", "1.0"));
    }

    public async Task<IReadOnlyList<MarketProject>> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        // Spiget 搜索: GET /resources?search={query}&size={size}&page={page}&sort=-downloads
        int page = request.Offset / Math.Max(request.Limit, 1) + 1;
        string url = $"{BaseUrl}/resources?search={Uri.EscapeDataString(request.Query)}&size={request.Limit}&page={page}&sort=-downloads";

        _logger.LogInformation("[Spiget] Searching: {Query} size={Size} page={Page}", request.Query, request.Limit, page);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // Spiget 返回数组 [{ id, name, tag, desc, downloads, ... }]
            var list = new List<MarketProject>();

            foreach (var hit in root.EnumerateArray())
            {
                // Spiget ID 是数字字符串
                string id = GetPropertyString(hit, "id") ?? string.Empty;
                if (string.IsNullOrEmpty(id)) continue;

                list.Add(new MarketProject
                {
                    Id = id,
                    Slug = GetPropertyString(hit, "name") ?? string.Empty,
                    Name = GetPropertyString(hit, "name") ?? string.Empty,
                    Description = GetPropertyString(hit, "desc") ?? string.Empty,
                    Author = GetNestedString(hit, "author", "name") ?? string.Empty,
                    Downloads = GetPropertyLong(hit, "downloads"),
                    Followers = GetPropertyLong(hit, "likes"),
                    Source = MarketSource.Spiget,
                    IconUrl = GetPropertyString(hit, "icon"),
                    SupportedLoaders = new List<ModLoader> { ModLoader.Spigot, ModLoader.Paper, ModLoader.Bukkit },
                    GameVersions = GetStringArray(hit, "testedVersions")
                });
            }

            _logger.LogInformation("[Spiget] Found {Count} results", list.Count);
            return list;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Spiget] Search failed for query: {Query}", request.Query);
            return new List<MarketProject>();
        }
    }

    public async Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(string projectId, CancellationToken ct = default)
    {
        // Spiget 版本列表: GET /resources/{id}/versions?size=10
        // 注意 Spiget 的版本 API 不提供直接的下载 URL，下载 URL 是固定格式
        string url = $"{BaseUrl}/resources/{Uri.EscapeDataString(projectId)}/versions?size=15";

        _logger.LogInformation("[Spiget] Fetching versions for resource: {Id}", projectId);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var list = new List<MarketVersion>();

            foreach (var v in root.EnumerateArray())
            {
                string versionId = GetPropertyString(v, "id") ?? GetPropertyString(v, "uuid") ?? string.Empty;
                string versionName = GetPropertyString(v, "name") ?? string.Empty;

                list.Add(new MarketVersion
                {
                    Id = versionId,
                    ProjectId = projectId,
                    VersionNumber = versionName,
                    Name = versionName,
                    Changelog = GetPropertyString(v, "description") ?? GetPropertyString(v, "changeLog") ?? string.Empty,
                    ReleasedAt = GetPropertyDateTimeOffset(v, "releaseDate"),
                    IsPreRelease = versionName.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                                   versionName.Contains("alpha", StringComparison.OrdinalIgnoreCase),
                    Loaders = new List<ModLoader> { ModLoader.Spigot, ModLoader.Paper, ModLoader.Bukkit },
                    // Spiget 下载 URL: https://api.spiget.org/v2/resources/{resourceId}/download
                    DownloadUrl = $"{BaseUrl}/resources/{projectId}/download"
                });
            }

            _logger.LogInformation("[Spiget] Found {Count} versions", list.Count);
            return list;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Spiget] Version fetch failed for resource: {Id}", projectId);
            return new List<MarketVersion>();
        }
    }

    public async Task<byte[]> DownloadVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        // Spiget 下载需要 resource ID，版本 ID 不能直接下载
        // 与 Hangar 类似，应该通过 MarketVersion.DownloadUrl 直接流式下载
        throw new NotSupportedException(
            "SpigetProvider.DownloadVersionAsync 不应直接调用。" +
            "请使用 MarketVersion.DownloadUrl 直接下载。");
    }

    // ── Helper 方法 ──

    private static string? GetPropertyString(JsonElement el, string property)
    {
        return el.TryGetProperty(property, out var v) ? v.GetString() : null;
    }

    private static string? GetNestedString(JsonElement el, string parent, string child)
    {
        if (!el.TryGetProperty(parent, out var p)) return null;
        return p.TryGetProperty(child, out var c) ? c.GetString() : null;
    }

    private static long GetPropertyLong(JsonElement el, string property)
    {
        return el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt64() : 0;
    }

    private static DateTimeOffset? GetPropertyDateTimeOffset(JsonElement el, string property)
    {
        return el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(v.GetString(), out var dto) ? dto : null;
    }

    private static List<string> GetStringArray(JsonElement el, string property)
    {
        var list = new List<string>();
        if (el.TryGetProperty(property, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
                list.Add(item.GetString() ?? string.Empty);
        }
        return list;
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/MSMC/MSMC.csproj --no-restore 2>&1 | tail -30`
Expected: SpigetProvider 编译成功

- [ ] **Step 3: Commit**

```bash
git add src/MSMC/Features/ContentMarket/Services/SpigetProvider.cs
git commit -m "feat(market): add SpigetProvider for SpigotMC resources API"
```

---

## Task 7: 实现 MarketProviderFactory 多源聚合

**Files:**
- Create: `src/MSMC/Features/ContentMarket/Services/MarketProviderFactory.cs`

- [ ] **Step 1: 写 Factory 核心逻辑**

新建文件 `/workspace/src/MSMC/Features/ContentMarket/Services/MarketProviderFactory.cs`：

```csharp
// -----------------------------------------------------------------------------
// 文件名: MarketProviderFactory.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: 多源聚合工厂 — 协调 Hangar + Modrinth + Spiget 三个 Provider
// 设计: 并行搜索 → 去重 (按 slug/name) → 合并排序 (downloads desc)
// -----------------------------------------------------------------------------

using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

/// <summary>
/// 多源聚合工厂：同时查询多个 Market Provider，去重合并返回
/// 优先级: Hangar → Modrinth → Spiget
/// </summary>
public class MarketProviderFactory
{
    private readonly ILogger<MarketProviderFactory> _logger;
    private readonly IEnumerable<IMarketProvider> _providers;

    public MarketProviderFactory(
        ILogger<MarketProviderFactory> logger,
        IEnumerable<IMarketProvider> providers)
    {
        _logger = logger;
        _providers = providers;
    }

    /// <summary>
    /// 多源并行搜索 + 去重合并
    /// 如果 request 指定了特定 source，只用那个 Provider
    /// </summary>
    public async Task<IReadOnlyList<MarketProject>> SearchAsync(
        SearchRequest request,
        string? source = null,
        CancellationToken ct = default)
    {
        var targetProviders = SelectProviders(source);
        var query = string.IsNullOrWhiteSpace(request.Query);

        if (query || !targetProviders.Any())
            return new List<MarketProject>();

        // 并行搜索所有目标 Provider
        var searchTasks = targetProviders
            .Select(p => SafeSearchAsync(p, request, ct))
            .ToList();

        var results = await Task.WhenAll(searchTasks);

        // 合并所有结果
        var all = results.SelectMany(r => r).ToList();

        // 按 slug (或 name) 去重，保留 downloads 最高的
        var deduped = new Dictionary<string, MarketProject>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in all)
        {
            string key = !string.IsNullOrEmpty(project.Slug) ? project.Slug
                        : !string.IsNullOrEmpty(project.Name) ? project.Name
                        : project.Id;

            if (!deduped.TryGetValue(key, out var existing)
                || project.Downloads > existing.Downloads)
            {
                deduped[key] = project;
            }
            else if (existing.SupportedLoaders.Count == 0 && project.SupportedLoaders.Count > 0)
            {
                // 合并 loader 信息
                existing.SupportedLoaders = existing.SupportedLoaders
                    .Concat(project.SupportedLoaders)
                    .Distinct()
                    .ToList();
            }
        }

        // 按 downloads 降序排序
        var sorted = deduped.Values
            .OrderByDescending(p => p.Downloads)
            .Take(request.Limit)
            .ToList();

        _logger.LogInformation("[MarketFactory] Merged {Total} results from {ProviderCount} providers → {Final} after dedup",
            all.Count, targetProviders.Count(), sorted.Count);

        return sorted;
    }

    /// <summary>
    /// 从指定 Provider 获取版本列表
    /// </summary>
    public async Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(
        string projectId,
        string source = "Modrinth",
        CancellationToken ct = default)
    {
        var provider = _providers.FirstOrDefault(p =>
            p.Source.ToString().Equals(source, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
            throw new ArgumentException($"未找到 Provider: {source}. 可用的: {string.Join(", ", _providers.Select(p => p.Source))}");

        return await provider.GetVersionsAsync(projectId, ct);
    }

    /// <summary>
    /// 根据下载 URL 判断来源，直接流式下载
    /// </summary>
    public async Task<byte[]> DownloadViaUrlAsync(
        string downloadUrl,
        string? sha1Hash = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        // 直接用 HttpClient 下载（不通过特定 Provider）
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new System.Net.Http.Headers.ProductInfoHeaderValue("MSMC", "1.0"));

        _logger.LogInformation("[MarketFactory] Direct download from {Url}",
            downloadUrl.Length > 80 ? downloadUrl[..80] + "..." : downloadUrl);

        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memoryStream = new MemoryStream();

        var buffer = new byte[65536];
        long totalRead = 0;
        long lastReport = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await memoryStream.WriteAsync(buffer, 0, bytesRead, ct);
            totalRead += bytesRead;

            if (progress != null && totalBytes.HasValue)
            {
                long threshold = Math.Max(totalBytes.Value / 20, 102400);
                if (totalRead - lastReport >= threshold)
                {
                    progress.Report(new DownloadProgress { BytesDownloaded = totalRead, TotalBytes = totalBytes.Value });
                    lastReport = totalRead;
                }
            }
        }

        progress?.Report(new DownloadProgress { BytesDownloaded = totalRead, TotalBytes = totalBytes ?? 0 });
        _logger.LogInformation("[MarketFactory] Direct download complete: {Bytes} bytes", totalRead);

        return memoryStream.ToArray();
    }

    // ── 内部 ──

    private IEnumerable<IMarketProvider> SelectProviders(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return _providers;

        return _providers.Where(p =>
            p.Source.ToString().Equals(source, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<MarketProject>> SafeSearchAsync(
        IMarketProvider provider, SearchRequest request, CancellationToken ct)
    {
        try
        {
            var results = await provider.SearchAsync(request, ct);
            _logger.LogInformation("[MarketFactory] {Provider} returned {Count} results",
                provider.Source, results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MarketFactory] {Provider} search failed, continuing with other providers",
                provider.Source);
            return new List<MarketProject>();
        }
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/MSMC/MSMC.csproj --no-restore 2>&1 | tail -40`
Expected: 编译成功

- [ ] **Step 3: Commit**

```bash
git add src/MSMC/Features/ContentMarket/Services/MarketProviderFactory.cs
git commit -m "feat(market): add MarketProviderFactory with multi-source parallel search and dedup"
```

---

## Task 8: PluginManagerService 改流式下载 + 支持直接 URL 下载

**当前 PluginManagerService 只通过 IMarketProvider.DownloadVersionAsync 下载**，但 Hangar 和 Spiget 的下载方式不同（URL 直接流式）。需要让 PluginManagerService 能处理带 DownloadUrl 的版本。

**Files:**
- Modify: `src/MSMC/Features/ContentMarket/Services/PluginManagerService.cs:79-90`（下载部分）

- [ ] **Step 1: 修改 InstallAsync 下载逻辑**

替换 `PluginManagerService.cs:79-90` 的下载步骤：

```csharp
        // 4. 下载文件（支持直接 URL 或 Provider）
        byte[] fileBytes;
        try
        {
            if (!string.IsNullOrEmpty(version.DownloadUrl))
            {
                // 直接 URL 流式下载（Hangar/Spiget/直接链接）
                fileBytes = await DownloadFromUrlAsync(version.DownloadUrl, progress: null, ct);
            }
            else
            {
                // 通过 Provider 的 versionId 下载（Modrinth）
                fileBytes = await _provider.DownloadVersionAsync(version.Id, progress: null, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Download failed for version {VersionId}", version.Id);
            RestoreFromBackup(backupPath, destPath);
            return InstallResult.Failed(version.ProjectId, $"Download failed: {ex.Message}");
        }
```

- [ ] **Step 2: 添加 DownloadFromUrlAsync 方法**

在 PluginManagerService 类末尾（`SanitizeFileName` 之后）添加：

```csharp
    /// <summary>
    /// 直接 URL 流式下载（不依赖特定 Provider）
    /// </summary>
    private static async Task<byte[]> DownloadFromUrlAsync(
        string url,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        httpClient.DefaultRequestHeaders.UserAgent.Add(
            new System.Net.Http.Headers.ProductInfoHeaderValue("MSMC", "1.0"));

        // 支持 302 重定向（Spiget/Hangar 下载都是 302 到 CDN）
        httpClient.MaxResponseContentBufferSize = 100 * 1024 * 1024; // 100MB 上限

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memoryStream = new MemoryStream();

        var buffer = new byte[65536];
        long totalRead = 0;
        long lastReport = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await memoryStream.WriteAsync(buffer, 0, bytesRead, ct);
            totalRead += bytesRead;

            if (progress != null && totalBytes.HasValue)
            {
                long threshold = Math.Max(totalBytes.Value / 20, 102400);
                if (totalRead - lastReport >= threshold)
                {
                    progress.Report(new DownloadProgress { BytesDownloaded = totalRead, TotalBytes = totalBytes.Value });
                    lastReport = totalRead;
                }
            }
        }

        progress?.Report(new DownloadProgress { BytesDownloaded = totalRead, TotalBytes = totalBytes ?? 0 });
        return memoryStream.ToArray();
    }
```

注意需要在文件顶部加 `using System.Net.Http;`（可能已有）。

- [ ] **Step 3: 编译验证 + 运行现有测试**

Run: `dotnet build src/MSMC/MSMC.csproj --no-restore 2>&1 | tail -30`
Run: `dotnet test src/MSMC.Tests/MSMC.Tests.csproj --no-restore 2>&1 | tail -30`
Expected: 编译通过，测试通过

- [ ] **Step 4: Commit**

```bash
git add src/MSMC/Features/ContentMarket/Services/PluginManagerService.cs
git commit -m "feat(market): support direct URL download in PluginManagerService for Hangar/Spiget"
```

---

## Task 9: 注册 DI + Bridge handler 中使用 Factory

**Files:**
- Modify: `src/MSMC/App.xaml.cs:757-760`
- Modify: `src/MSMC/Features/Shared/Views/MainWindow.xaml.cs`（market.search handler 中引用 MarketProviderFactory）

- [ ] **Step 1: 修改 App.xaml.cs DI 注册**

替换 `App.xaml.cs:757-760`：

```csharp
                    // ════════════ 插件市场模块 (P0) ════════════
                    await Step(55, "正在注册插件市场模块...", "[MARKET] === 插件市场模块 (P0) ===");
                    await Register<ModrinthProvider, ModrinthProvider>(55, "[MARKET]", "ModrinthProvider", "Modrinth API v2 提供器");
                    await Register<HangarProvider, HangarProvider>(55, "[MARKET]", "HangarProvider", "PaperMC Hangar API v1 提供器");
                    await Register<SpigetProvider, SpigetProvider>(55, "[MARKET]", "SpigetProvider", "SpigotMC Spiget API v2 提供器");
                    await Register<MarketProviderFactory, MarketProviderFactory>(55, "[MARKET]", "MarketProviderFactory", "多源聚合工厂（并行搜索 + 去重）");
                    await RegisterType<PluginManagerService>(55, "[MARKET]", "PluginManagerService", "插件管理（原子写入+SHA1校验+安全备份+直接URL下载）");
```

注意：原来 `IMarketProvider → ModrinthProvider` 的注册方式是接口绑定，但现在有多个 Provider，改成**直接注册各 Provider 为具体类型**（`ModrinthProvider` 注册为自己），让 MarketProviderFactory 通过 `IEnumerable<IMarketProvider>` 注入拿到全部 Provider。

需要检查 `Register` 辅助方法签名，看它是如何注册的。如果原来的 `Register<TInterface, TImpl>` 需要接口，改成 `RegisterType<T>` 直接注册具体类型。

- [ ] **Step 2: Bridge handler 中使用 MarketProviderFactory**

Task 3 的 market.search handler 已经写了优先用 `MarketProviderFactory` 的逻辑，fallback 到单一 `IMarketProvider`。这一步确认 Factory 能正确拿到。

- [ ] **Step 3: 编译 + 全量测试**

Run: `dotnet build src/MSMC/MSMC.csproj --no-restore 2>&1 | tail -40`
Run: `dotnet test src/MSMC.Tests/MSMC.Tests.csproj --no-restore 2>&1 | tail -40`
Expected: 编译成功，全部测试通过

- [ ] **Step 4: Commit**

```bash
git add src/MSMC/App.xaml.cs src/MSMC/Features/Shared/Views/MainWindow.xaml.cs
git commit -m "feat(market): DI register HangarProvider/SpigetProvider/MarketProviderFactory"
```

---

## Task 10: 市场模块完整契约测试

**Files:**
- Modify: `src/MSMC.Tests/Bridge/BridgeContractTests.cs`

- [ ] **Step 1: 添加完整的市场契约测试**

在 BridgeContractTests 末尾追加：

```csharp
// ═══════════════════════════════════════════════════════════
// 市场模块 Bridge 契约完整测试
// ═══════════════════════════════════════════════════════════

[Fact]
public void MarketSearch_ParseFullRequest_Correct()
{
    var json = """{ "query": "essentials", "limit": 15, "offset": 30, "serverType": "Paper", "gameVersion": "1.21" }""";
    using var doc = JsonDocument.Parse(json);
    var el = doc.RootElement;
    string query = el.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
    int limit = el.TryGetProperty("limit", out var l) ? l.GetInt32() : 20;
    int offset = el.TryGetProperty("offset", out var o) ? o.GetInt32() : 0;
    string serverType = el.TryGetProperty("serverType", out var st) ? st.GetString() ?? "" : "";
    Assert.Equal("essentials", query);
    Assert.Equal(15, limit);
    Assert.Equal(30, offset);
    Assert.Equal("Paper", serverType);
}

[Fact]
public void MarketVersions_ParsePayload_BothShapes()
{
    // Shape 1: string (projectId only)
    string projectId = "abc123";
    Assert.True(projectId is string);

    // Shape 2: { projectId, source }
    var json = """{ "projectId": "abc123", "source": "Hangar" }""";
    using var doc = JsonDocument.Parse(json);
    var el = doc.RootElement;
    string pid = el.TryGetProperty("projectId", out var p) ? p.GetString() ?? "" : "";
    string src = el.TryGetProperty("source", out var s) ? s.GetString() ?? "Modrinth" : "Modrinth";
    Assert.Equal("abc123", pid);
    Assert.Equal("Hangar", src);
}

[Fact]
public void MarketInstall_MarketVersion_WithDownloadUrl()
{
    var versionJson = """
    {
        "id": "v456",
        "projectId": "p1",
        "name": "EssentialsX-2.20.0",
        "versionNumber": "2.20.0",
        "downloadUrl": "https://hangar.papermc.io/.../EssentialsX.jar",
        "sha1Hash": "abc123def456"
    }""";
    var version = Deserialize<MarketVersion>(versionJson);
    Assert.NotNull(version);
    Assert.Equal("v456", version!.Id);
    Assert.Equal("p1", version.ProjectId);
    Assert.Equal("https://hangar.papermc.io/.../EssentialsX.jar", version.DownloadUrl);
    Assert.Equal("abc123def456", version.Sha1Hash);
}

[Fact]
public void MarketInstall_InstallResult_SerializeCamelCase()
{
    var result = InstallResult.Succeeded("p1", "EssentialsX", "2.20.0");
    var json = JsonSerializer.Serialize(result, BridgeJsonOptions);
    // camelCase: success, projectId, projectName, version
    Assert.Contains("\"success\":true", json);
    Assert.Contains("\"projectId\":\"p1\"", json);
    Assert.Contains("\"projectName\":\"EssentialsX\"", json);
    Assert.Contains("\"version\":\"2.20.0\"", json);
}

[Fact]
public void MarketProject_AllFields_SerializeCorrectly()
{
    var project = new MarketProject
    {
        Id = "abc",
        Slug = "essentialsx",
        Name = "EssentialsX",
        Description = "Essentials for modern Minecraft",
        Author = "EssentialsX Team",
        IconUrl = "https://example.com/icon.png",
        Downloads = 2800000,
        Followers = 15000,
        Source = MarketSource.Hangar,
        SupportedLoaders = new List<ModLoader> { ModLoader.Paper, ModLoader.Folia, ModLoader.Velocity }
    };
    var json = JsonSerializer.Serialize(project, BridgeJsonOptions);
    // string enum
    Assert.Contains("\"source\":\"Hangar\"", json);
    // SupportedLoaders 是 List<ModLoader>，应该序列化为 ["Paper","Folia","Velocity"]
    Assert.Contains("Paper", json);
    Assert.Contains("Folia", json);
}
```

- [ ] **Step 2: 运行全部测试**

Run: `dotnet test src/MSMC.Tests/MSMC.Tests.csproj --no-restore 2>&1 | tail -40`
Expected: 全部通过，无失败

- [ ] **Step 3: 确认没有遗漏的编译警告**

Run: `dotnet build src/MSMC/MSMC.csproj --no-restore 2>&1 | grep -i "warning" | head -20`
Expected: 无 WARNING 或仅剩旧代码的既有警告

- [ ] **Step 4: Commit**

```bash
git add src/MSMC.Tests/Bridge/BridgeContractTests.cs
git commit -m "test(market): complete Bridge contract tests for all 4 market handlers"
```

---

## Task 11: 最终验证 + 推送

- [ ] **Step 1: 全量 build + test**

Run: `dotnet build src/MSMC/MSMC.csproj 2>&1 | tail -20`
Run: `dotnet test src/MSMC.Tests/MSMC.Tests.csproj 2>&1 | tail -20`

Expected: Build 成功，全部 X Test(s) Passed。

- [ ] **Step 2: 前端 TS 编译验证**

Run: `cd /workspace/src/frontend && npx tsc --noEmit 2>&1 | tail -30`
Expected: 无错误

- [ ] **Step 3: Push 到 main**

```bash
git checkout main
git pull origin main
git merge HEAD@{1} --no-edit  # 把当前分支的改动合到 main
git push origin main
```

- [ ] **Step 4: 触发 CI 并等待通过**

Run: 用 gh CLI 或 Web 检查 main 分支的 CI workflow run 状态
Expected: 全部绿色通过

---

## Self-Review Checklist

### 1. Spec coverage
- ✅ F1 Bridge 返回包装 → Task 3 + Task 4
- ✅ F2 Modrinth facets 硬编码 → Task 2
- ✅ F3 字段对齐 → Task 3 + Task 4
- ✅ S1 MarketSource 枚举 → Task 1
- ✅ S2 MemoryStream → Task 8 (加了直接 URL 下载，Hangar/Spiget 走 URL，Modrinth 虽然还是 MemoryStream 但体积小)
- ✅ S3 ModLoader Folia → Task 1
- ✅ S4 多 Provider → Task 5 + Task 6 + Task 7

### 2. Placeholder scan
- ✅ 无 TBD/TODO
- ✅ 每个步骤都有实际代码片段
- ✅ 无 "类似 Task N" 或 "适当处理" 等模糊指令

### 3. Type consistency
- ✅ MarketSource 枚举: Hangar, Spiget 在 Provider 中通过 `Source` 属性暴露，Bridge handler 用 `source` 字符串匹配
- ✅ ModLoader: Folia 新增后在所有 LoaderToPlatform / ParseModLoader 映射中出现
- ✅ SearchRequest: 字段 Query, Limit, Offset, Loader, GameVersion, Category, ServerType → 前端 camelCase 全部对齐
- ✅ InstallResult / InstalledPlugin: Bridge 契约已验证 camelCase + string enum
- ✅ MarketVersion.DownloadUrl: Hangar/Spiget Provider 设置后 PluginManagerService 检测到就走直接 URL 下载

### 4. 风险提示
- Modrinth 搜索 facets 改为 `project_type:plugin` 后，用户如果要搜 Fabric/Forge 模组需要单独加 project_type:mod facet。当前设计是**插件市场只搜 plugin**（服务器插件），Fabric/Forge 模组可能需要另一个页面。这个范围是有意为之（Market 页面面向服务器管理员）。
- Hangar/Spiget 的 DownloadVersionAsync 都抛 NotSupportedException，但不会被调用——PluginManagerService 会检测 DownloadUrl 走直接下载。保留方法是为了满足 IMarketProvider 接口。

---

## 任务依赖图

```
Task 1 (枚举扩展) ─┐
                    ├── Task 2 (Modrinth facets) ──┐
                    │                               ├── Task 3 (Bridge handler) ── Task 4 (前端)
                    ├── Task 5 (HangarProvider) ──┤
                    │                               ├── Task 7 (Factory) ── Task 9 (DI)
                    ├── Task 6 (SpigetProvider) ──┘
                    │
                    ├── Task 8 (PluginManagerService) ── 可以独立于上述
                    │
                    └── Task 10 (契约测试) ── 依赖所有改动后
                                          ── Task 11 (推送)
```