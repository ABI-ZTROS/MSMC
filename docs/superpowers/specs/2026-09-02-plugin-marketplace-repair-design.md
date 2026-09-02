# MSMC 插件市场修复设计文档

**日期**: 2026-09-02
**状态**: 设计已确认
**根因**: 后端 Bridge 返回包装对象但前端当原始值用；Modrinth 搜索硬编码 `project_type:mod` 搜不到服务器插件；字段名对齐缺失；缺 Hangar/Spiget Provider。

---

## 1. 问题诊断

### 1.1 致命 Bug（直接导致功能不可用）

| ID | 位置 | 描述 |
|----|------|------|
| F1 | Bridge 返回值 | 后端 handler 返回 `{ success, projects }` 包装，前端 `bridge.invoke` 把整个对象 resolve，但前端 bridge API 声明返回原始数组。`setProjects({success:true, projects:[]})` 后 `projects.map()` 对对象调用直接炸 |
| F2 | ModrinthProvider.cs:56 | `facets` 硬编码 `project_type:mod`，但 Bukkit/Spigot/Paper 插件在 Modrinth 是 `project_type:plugin` |
| F3 | Bridge handler 字段 | 后端返回 `title`，前端 MarketProject 用 `name`；后端有 `supportedLoaders`，前端类型缺；后端返回 `downloads` 无 `likes` |

### 1.2 严重 Bug

| ID | 描述 |
|----|------|
| S1 | MarketSource 枚举缺 Hangar、Spiget |
| S2 | DownloadVersionAsync 用 MemoryStream 全量加载，大插件 OOM 风险 |
| S3 | ModLoader 枚举缺 Folia |
| S4 | 只有 Modrinth 一个 Provider，覆盖度不足 |

---

## 2. 修复方案

### 2.1 架构总览

```
┌─────────────────────────────────────────────────────────────────┐
│                     前端 MarketPage                              │
│  searchMarket(query, source?, serverType?)                      │
└──────────────────────────┬──────────────────────────────────────┘
                           │ Bridge: market.search
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│               Bridge Handler (MainWindow.xaml.cs)                │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │ market.search → MarketProviderFactory.SearchAsync()       │ │
│  │ market.versions → factory.GetVersionsAsync()              │ │
│  │ market.install → PluginManagerService.InstallAsync()      │ │
│  │ market.listInstalled → PluginManagerService.GetInstalled() │ │
│  └───────────────────────────────────────────────────────────┘ │
│                                                                 │
│  契约: 直接返回数据，去掉 success 包装。错误时 throw 让         │
│  bridge.invoke reject，前端 catch 显示错误。                     │
└──────────────────────────┬──────────────────────────────────────┘
                           │
              ┌─────────────┼─────────────┐
              ▼             ▼             ▼
     ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
     │  HangarProv. │ │ ModrinthProv│ │ SpigetProv. │
     │  (PaperMC)  │ │             │ │ (SpigotMC)  │
     └─────────────┘ └─────────────┘ └─────────────┘
              │             │             │
              └─────────────┼─────────────┘
                            ▼
                   去重 + 合并排序
                   (按 downloads desc)
```

### 2.2 修改清单

#### 后端

| 层 | 文件 | 修改 |
|---|------|------|
| 模型 | `MarketProject.cs` | MarketSource 加 Hangar/Spiget；ModLoader 加 Folia |
| Provider | `ModrinthProvider.cs` | facets 动态化：`project_type:plugin`(默认) + `mod`；按 Loader 枚举映射 Modrinth loader 字符串 |
| Provider | **新建** `HangarProvider.cs` | PAPER/WATERFALL/VELOCITY 平台插件搜索、版本、下载 |
| Provider | **新建** `SpigetProvider.cs` | SpigotMC 资源搜索、版本、下载 |
| 服务 | **新建** `MarketProviderFactory.cs` | 多源并行搜索 + 去重 + 合并 |
| 服务 | `PluginManagerService.cs` | DownloadVersionAsync 改为流式下载（边写临时文件边报告进度） |
| Bridge | `MainWindow.xaml.cs:3746-3907` | ① 去掉 success 包装，直接 return 数据；② search 加 source/serverType 参数；③ install 加流式下载 |
| DI | `App.xaml.cs` | 注册 HangarProvider, SpigetProvider, MarketProviderFactory |

#### 前端

| 层 | 文件 | 修改 |
|---|------|------|
| 类型 | `bridge.ts:740-794` | MarketProject 加 source/supportedLoaders；字段对齐后端 |
| bridge API | `bridge.ts:972-988` | searchMarket 加 source/serverType 参数；**返回类型去掉包装假设** |
| 页面 | `MarketPage.tsx` | 搜索栏加 Source 下拉；显示来源标签 |

---

## 3. API 契约

### 3.1 market.search

**请求**:
```json
{ "query": "essentials", "source": null, "limit": 20, "serverType": "Paper" }
```

**成功响应**（直接数组，无包装）:
```json
[
  {
    "id": "abc123",
    "slug": "essentialsx",
    "name": "EssentialsX",
    "description": "Essentials for modern Minecraft servers",
    "author": "EssentialsX",
    "iconUrl": "https://cdn.hangar.papermc.io/...",
    "downloads": 2800000,
    "source": "Hangar",
    "supportedLoaders": ["Bukkit", "Spigot", "Paper", "Purpur", "Folia"]
  }
]
```

**失败**: Bridge 框架层返回 `{ success: false, error: "..." }` → bridge.invoke reject → 前端 catch 显示。

### 3.2 market.versions

**请求**: `"abc123"` 或 `{ "projectId": "abc123", "source": "Hangar" }`

**成功**: 直接返回数组。

### 3.3 market.install

**请求**:
```json
{
  "projectId": "abc123",
  "versionId": "v1.0",
  "downloadUrl": "https://cdn.hangar.papermc.io/.../EssentialsX.jar",
  "sha1Hash": "...",
  "serverPath": "C:/servers/survival"
}
```

**成功**: 直接返回 `InstallResult` 对象。
**失败**: throw → reject。

### 3.4 market.listInstalled

**请求**: `"C:/servers/survival"`

**成功**: 直接返回数组。

---

## 4. 三链原则

### 因果链
- ServerType 自动选择 facets/loader 过滤（Paper→bukkit+paper+folia, Velocity→velocity）
- 搜索失败不崩溃，Factory 降级重试或返回空
- 版本不兼容在 UI 标红提示

### 执行链
- 流式下载 + 原子写入（先写 .tmp 再 Move）+ SHA1 校验 + 自动备份
- 失败自动 RestoreFromBackup
- IProgress 每 5% 或 100KB 报告一次

### 返回链
- 全链路结构化日志：SearchStart → ParseResults → DownloadStart → Progress → VerifyHash → WriteFile → RecordInstall → Complete
- 异常统一 Log.Error(ex, "context") 带所有关联 ID

---

## 5. Provider API 参考

### Modrinth (已实现，需修复)
- 基础: `https://api.modrinth.com/v2`
- 搜索: `GET /search?query=&facets=[["project_type:plugin"],["loaders:paper"]]`
- 版本: `GET /project/{id}/version`
- 下载 URL 在版本文件的 `files[].url` 字段
- 限速: 300 req/min

### Hangar (PaperMC 官方)
- 基础: `https://hangar.papermc.io/api/v1`
- 搜索: `GET /projects?query=essentials&platform=PAPER&limit=20`
- 项目: `GET /projects/{slugOrId}`
- 版本: `GET /projects/{slugOrId}/versions/{platform}`
- 下载: `GET /projects/{slugOrId}/versions/{platform}/download/{nameOrId}` (302 重定向到 CDN)
- 平台: PAPER, WATERFALL, VELOCITY
- 限速: 20 req/5s

### Spiget (SpigotMC)
- 基础: `https://api.spiget.org/v2`
- 搜索: `GET /resources?search=essentials&size=20&sort=-downloads`
- 资源: `GET /resources/{id}`
- 版本列表: `GET /resources/{id}/versions?size=10`
- 下载: `GET /resources/{id}/download` (302 重定向)
- 无需认证
