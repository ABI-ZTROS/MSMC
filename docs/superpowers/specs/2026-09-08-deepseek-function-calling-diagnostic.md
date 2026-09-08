# DeepSeek Function Calling 智能诊断 — 设计文档

> 日期: 2026-09-08  
> 状态: 待评审  
> 作者: MSMC Agent  
> 架构方案: A（Function Calling Engine + ToolRegistry）  
> 原则遵守: P4 诚实返回链 / P5 契约一致性 / P6 异步纪律 / P7 资源原子化 / P12 信任边界

---

## 0. 需求输入清单（brainstorming 产出）

| # | 需求 | 来源 |
|---|------|------|
| 1 | 疑难解答不再强制需要选中服务器 | 用户补充 |
| 2 | 「我不会开服」教程中途可一键转 AI 诊断（主要触发场景） | 用户选择 |
| 3 | DeepSeek 联网 + 下载 + 读文件，**写文件必须用户确认** | 用户选择 |
| 4 | **AI 自主选择看什么**（Tool Use / Function Calling） | 用户选择 |
| 5 | UI 形态：教程左 60% + AI 抽屉右 40% 并排 | 用户选择 |
| 6 | 「一次性全做完」 | 用户选择 |

---

## 1. 架构总览

### 1.1 核心数据流

```
┌─── 前端 WebView2 ────────────────────────────────────────┐
│ TutorialOverlay.tsx  ←→  AIDrawer.tsx（新增）              │
│   教程卡片左 60%       Bridge: troubleshooting.*          │
│                        AI 对话 + 工具执行日志 + FixPanel   │
└───────────┬───────────────────────────┬──────────────────┘
            │ ts → c# action            │ c# → ts event
┌───────────▼───────────────────────────▼──────────────────┐
│ BridgeActionRegistrar.cs （扩展）                          │
│   Register("troubleshooting.aiInit", ...)                  │
│   Register("troubleshooting.aiSend", ...)                  │
│   Register("troubleshooting.aiStop", ...)                  │
│   Register("troubleshooting.confirmFix", ...)              │
└───────────┬──────────────────────────────────────────────┘
            │
┌───────────▼──────────────────────────────────────────────┐
│ FunctionCallingEngine（新增类）                             │
│  ┌─ 多轮对话循环（最多 10 轮，总超时 60s）                  │
│  │  POST DeepSeek.completions                               │
│  │    ├─ tool_calls → ToolRegistry.Execute → ToolResult    │
│  │    └─ final_response → ParseAnalysis → 返回              │
│  └─ 每轮推 Bridge 事件（aiStream / aiToolExec / aiDone）    │
└───────────┬──────────────────────────────────────────────┘
            │ 调用工具
┌───────────▼──────────────────────────────────────────────┐
│ ToolRegistry（新增类）                                     │
│  统一注册 + 路由 + 结果封装 ToolResult                      │
└───────────┬──────────────────────────────────────────────┘
            │ 底层实现
┌───────────▼──────────────────────────────────────────────┐
│ 复用现有服务                                               │
│  DiagnosticEngine / FixExecutor / CheckRunner               │
│  PortScanner / JavaFinder / RegionReader                    │
│  SystemMonitor / core-fetcher / HttpClientFactory           │
└──────────────────────────────────────────────────────────┘
```

### 1.2 关键设计决策

| 决策 | 理由 |
|------|------|
| Function Calling 而非混合模式 | 用户明确选择"AI 自主选择"，新手场景下几乎每次都需要联网，混合模式等于每次跑两轮诊断 |
| 写文件类 Tool 不真正写入，只返回 DiffPreview | 权限边界：写文件必须用户确认 → Tool 只生成 diff → FixPanel 展示 → 用户点确认 → FixExecutor 原子化执行 |
| UI 线程跑，**不建独立 BackgroundService** | 轮次少（≤10）+ 每轮等待短（DeepSeek 响应平均 3-5s）+ Bridge 推事件依赖 Dispatcher，独立线程切换层不值得 |
| 10 轮硬限 + 60s 总超时 | P4 诚实返回链：不允许 AI 无限循环；第 11 次或超时直接终止并返回 partial result |

---

## 2. Tool Registry 设计

### 2.1 IAiTool 接口

```csharp
public interface IAiTool
{
    // Function Calling 的 name（DeepSeek function.name 必须精确匹配）
    string Name { get; }
    // 喂给 DeepSeek 的 description（什么时候该调这个工具）
    string Description { get; }
    // 喂给 DeepSeek 的 JSON schema（parameters）
    System.Text.Json.JsonDocument GetParametersSchema();
    // 执行，返回 ToolResult（三字段：Success/Error/Data）
    Task<ToolResult> ExecuteAsync(System.Text.Json.JsonElement args, CancellationToken ct);
}

// ToolResult 必须诚实返回 —— 任何失败都不能假装成功
public record ToolResult(
    bool Success,
    string? Error,      // 失败时填，不要返回空字符串
    string? Data        // 成功时填 JSON 字符串（AI 看到的结果）
);
```

### 2.2 10 个工具一次性全上

| # | Tool 名 | 作用 | 底层来源 | 备注 |
|---|---------|------|---------|------|
| 1 | `read_log` | 读 server.log / startup.log 最后 N 行 | 新增（File.ReadLines + .SkipLast） | 必须支持 path 参数（用户给了服务器路径） |
| 2 | `check_port` | 端口是否被占、PID、进程名 | 现有 `PortScanner` | 支持 port=0 时随机扫常用端口 |
| 3 | `check_java` | 当前 Java 版本、PATH、JAVA_HOME、可用 JDK 列表 | 现有 `JavaFinder` | 无服务器也能查（独立运行） |
| 4 | `list_regions` | world/region 文件完整性 + 损坏统计 | 现有 `DiagnosticRegionReader` | 无服务器时跳过 |
| 5 | `read_server_properties` | 读 server.properties 并解析 key | 现有 `PropertiesParser` | 无服务器时返回 ToolResult.Success=false + Error |
| 6 | `download_core` | 下载核心（Paper/Spigot/Forge/Fabric/Quilt/NeoForge） | 现有 `tools/core-fetcher/fetch.py` | 写入 `.part` → 校验 → `Move`（P7 原子化） |
| 7 | `search_doc` | 联网搜官方文档（docs.papermc.io / docs.spigotmc.org / mc.wiki） | 新增 `HttpClient`（走 `IHttpClientFactory`） | 超时 10s，失败返回 Success=false |
| 8 | `backup_world` | 原子化备份 world 目录 | 新增 | `.part` → 校验 zip 完整性 → Move |
| 9 | `get_system_info` | OS / CPU / 内存 / 磁盘剩余 / 网络回环 | 现有 `SystemMonitor` | 无服务器也能查 |
| 10 | `list_available_cores` | 列出 knowledge-base 里所有支持的核心类型 + 最新版本 | 现有 `knowledge-base/*.json` + core-fetcher | 静态知识库 + 联网查最新 |

### 2.3 ToolRegistry 核心逻辑

```csharp
public sealed class ToolRegistry
{
    private readonly Dictionary<string, IAiTool> _tools = new();

    // DI 启动时一次性注册
    public void RegisterAll(IEnumerable<IAiTool> tools) { foreach (var t in tools) _tools[t.Name] = t; }

    // AI 调工具 → 路由 → 执行 → 结果回灌对话历史
    public async Task<ToolResult> ExecuteAsync(string toolName, JsonElement args, CancellationToken ct)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            return new ToolResult(false, $"未知工具: {toolName}", null);
        try { return await tool.ExecuteAsync(args, ct).ConfigureAwait(false); }
        catch (Exception ex) { return new ToolResult(false, $"工具执行异常: {ex.Message}", null); }
    }

    // 组装 DeepSeek function.schema
    public JsonDocument BuildToolsSchema() { /* ... */ }
}
```

---

## 3. DeepSeekService 重写

### 3.1 从 Single-shot 到 Multi-round Function Calling

```csharp
public async Task<DeepSeekAnalysis?> AnalyzeReportAsync(
    DiagnosticReport report, string? userQuestion, CancellationToken ct = default)
{
    // 一次性全上 —— 启动 Function Calling 多轮引擎
    return await _functionEngine.RunDiagnosticAsync(
        report, userQuestion, ct);
}
```

### 3.2 FunctionCallingEngine 核心循环

```csharp
// SystemPrompt 格式（Function Calling 专用）
var system = @"你是一名资深 Minecraft Java 版服务器运维专家。
严格规则：
1. 你可以调用下面列出的工具来检查服务器状态、读日志、下载核心、搜官方文档
2. 最多调用 10 轮工具后必须给出最终回答
3. 最终回答必须是纯 JSON（和原 schema 一致）
4. recommendedActions 的 fixId 只能用白名单里的：
   backup.world / server.kill / port.kill.process / config.edit.server-properties /
   java.switch.version / region.clean.entities / player.reset.damage
5. 如果某个工具返回 Error（Success=false），不要编造结果，如实告诉用户
6. 不要一次调所有工具，只调能帮你诊断当前问题的那几个
";

// 对话历史维护（最多保留最近 20 条，老的截断）
var messages = new List<object>
{
    new { role = "system", content = system },
    new { role = "user", content = BuildUserPrompt(report, userQuestion) }
};

for (int i = 0; i < 10; i++)  // 硬限 10 轮
{
    // POST DeepSeek.completions（带 tools + tool_choice: auto）
    var resp = await SendCompletionAsync(messages, toolSchema);
    
    // case 1: 返回 tool_calls
    if (resp.ToolCalls.Count > 0)
    {
        foreach (var call in resp.ToolCalls)
        {
            // Bridge 推事件：aiToolExec { toolName, args, round }
            _bridge.Send("troubleshooting.aiToolExec", new { ... });
            
            var result = await _registry.ExecuteAsync(call.Name, call.Args, ct);
            
            // Bridge 推事件：aiStream { toolName, status, elapsedMs }
            _bridge.Send("troubleshooting.aiStream", new { ... });
            
            // 追加到对话历史
            messages.Add(new { role = "assistant", tool_calls = ... });
            messages.Add(new { role = "tool", content = result.ToJson(), tool_call_id = ... });
        }
        continue;
    }
    
    // case 2: 返回 final content
    return ParseAnalysis(resp.Content, resp.Raw);
}

// 超出 10 轮 → 诚实返回
Serilog.Log.Warning("[DIAG-AI] Function Calling 超出 10 轮上限，强制终止");
return null;
```

### 3.3 SystemPrompt JSON Schema（tools 字段片段）

```json
{
  "type": "function",
  "function": {
    "name": "download_core",
    "description": "从官方源下载 Minecraft 服务端核心 jar。支持 Paper/Spigot/Forge/Fabric/Quilt/NeoForge。如果不确定用户用什么类型，先调 list_available_cores 看看。",
    "parameters": {
      "type": "object",
      "properties": {
        "type": { "type": "string", "enum": ["paper","spigot","forge","fabric","quilt","neoForge"] },
        "version": { "type": "string", "description": "目标版本号，如 '1.21.1' 或 '1.20.4'" },
        "outputDir": { "type": "string", "description": "下载到哪个目录。可选，默认让用户确认" }
      },
      "required": ["type"]
    }
  }
}
```

### 3.4 Token 预算预估

| 轮次 | 预估 tokens | 说明 |
|------|------------|------|
| 1 (初始) | 1200 | system + user prompt + tool schema |
| 2 (AI 看工具结果) | 500-1000 | 取决于工具 Data 字段长度 |
| 3-10 (多轮) | 各 300-600 | 每轮 AI 决定调什么 / 看什么 |
| final | 600-1200 | final_response JSON |
| **总计** | **2500-5500** | 远低于 DeepSeek 128k 上下文 |

---

## 4. Bridge 契约扩展（P5 对齐）

### 4.1 新增 Action（TS → C#）

| Action | 参数 | 说明 |
|--------|------|------|
| `troubleshooting.aiInit` | `{ tutorialStep?: number, selectedServerPath?: string, userQuestion?: string }` | 启动 AI 诊断。tutorialStep 让 AI 知道用户在教程第几步；selectedServerPath 可选（支持"不会开服"场景不传） |
| `troubleshooting.aiSend` | `{ message: string }` | 用户中途插问，追加到对话历史让 AI 再分析 |
| `troubleshooting.aiStop` | `{}` | 用户主动终止诊断 |
| `troubleshooting.confirmFix` | `{ fixId: string, payload: object }` | 用户确认 FixPanel 里的某个修复动作 |

### 4.2 新增 Event（C# → TS）

| Event | Payload | 说明 |
|-------|---------|------|
| `troubleshooting.aiStream` | `{ toolName, status: "started"\|"done", elapsedMs, round }` | 工具执行时流式推，AIDrawer 显示实时日志 |
| `troubleshooting.aiToolExec` | `{ toolName, args, round }` | AI 发起工具调用的那一刻，TS 显示"AI 正在调 XXX" |
| `troubleshooting.aiDone` | `{ analysis: DeepSeekAnalysis, roundsUsed, totalMs }` | 诊断完成，AIDrawer 展示完整结果 + FixPanel |
| `troubleshooting.aiError` | `{ message }` | 异常（联网超时 / 超出轮次上限 / API Key 无效） |

### 4.3 契约测试（BridgeContractTests.cs 扩展）

每新增 action/event 必须在 `src/MSMC.Tests/Bridge/BridgeContractTests.cs` 加对应断言：

```csharp
[Fact]
public void aiInit_contract_matches_ts_direction()
{
    var contract = BridgeContract.Get("troubleshooting.aiInit");
    contract.TSToCSharp.Should().BeTrue();
    contract.Parameters.Should().ContainKey("tutorialStep");
    contract.Parameters.Should().ContainKey("selectedServerPath");
    contract.Parameters["tutorialStep"].Type.Should().Be(typeof(int?));
    // ...
}
```

---

## 5. UI 形态

### 5.1 布局规格

```
┌──────────────────────────────────────────────────────────┐
│  [MSMC Logo]  MSMC Tutorial                    [× 关闭]   │
├────────────────────────┬─────────────────────────────────┤
│   左 60%                │   右 40%                         │
│                          │                                 │
│  TutorialOverlay         │   AIDrawer（新增组件）            │
│  [教程卡片 + 上/下一步]   │   🤖 MSMC AI 诊断               │
│                          │   ────────────────────            │
│  [让 AI 帮我诊断 →]      │   [工具执行日志]                  │
│                          │   ✅ check_java → JDK 21         │
│                          │   ✅ check_port → 25565 空闲       │
│                          │   🔄 download_core → 下载中 45%    │
│                          │                                 │
│                          │   诊断进度：●●●○○○ (6/10 轮)     │
│                          │                                 │
│                          │   AI 建议（FixPanel 复用）：       │
│                          │   1. 下载 Paper 1.21.1            │
│                          │      [确认下载]                    │
│                          │   2. 分配内存 4G                  │
│                          │      [应用]                       │
└────────────────────────┴─────────────────────────────────┘
```

### 5.2 关键交互点

| # | 交互 | 行为 |
|---|------|------|
| 1 | 用户点「让 AI 帮我诊断」 | Bridge.sendEvent("troubleshooting.aiInit", { tutorialStep, selectedServerPath }) → C# 启动 FunctionCallingEngine |
| 2 | AI 调工具 | C# 推 `aiToolExec` → TS 显示"AI 正在 [toolName]…"；工具返回 → C# 推 `aiStream` → TS 显示结果 |
| 3 | 每轮新工具调用 | FixPanel 实时更新（如果 AI 已经给了 fixId） |
| 4 | AI 返回 final_response | C# 推 `aiDone` → TS 展示完整 DeepSeekAnalysis → FixPanel 变成可点击确认 |
| 5 | 用户中途想改问题 | AIDrawer 底部输入框 → Bridge.sendEvent("troubleshooting.aiSend", { message }) → 追加对话历史 → AI 下一轮会看 |
| 6 | 用户想终止 | Bridge.sendEvent("troubleshooting.aiStop") → CancellationToken.Cancel() → 返回 partial result |

---

## 6. 三链审计（设计阶段过一遍）

| 三链 | 审计点 | 设计决策 |
|------|--------|---------|
| **执行链** | 10 轮循环会不会死循环？ | `for (int i = 0; i < 10; i++)` + 总超时 60s + `CancellationToken`，硬限 |
| **执行链** | Tool.ExecuteAsync 抛异常会不会炸掉循环？ | `catch (Exception ex) → return ToolResult(false, ex.Message, null)` |
| **执行链** | Bridge 事件推 TS 时 TS 已销毁？ | `_bridge.Send` 内部 try/catch，绝不向上冒泡 |
| **因果链** | ToolRegistry 真注册了所有工具吗？ | DI 启动时 `ToolRegistry.RegisterAll(IEnumerable<IAiTool>)`，每个 Tool 有工厂模式 |
| **因果链** | core-fetcher/fetch.py 真存在吗？ | `ToolFactory` 启动时 `File.Exists` 检查，不存在 fallback 到 HttpClient |
| **因果链** | search_doc 真能搜到东西吗？ | 走 docs.papermc.io / docs.spigotmc.org 官方 sitemap，不搜全站（省 token + 准） |
| **返回链** | ToolResult.Success=false 会不会被 DeepSeek 忽略？ | SystemPrompt 明确写"工具返回 Error 时不要编造结果" |
| **返回链** | 对话历史无限增长吃 token？ | 每轮只保留最近 20 条 messages，超出截断 |
| **异步纪律 P6** | async void 冒泡？ | 入口 `async Task StartDiagnosticAsync(...)`，Bridge fire-and-forget 走 FireAndForgetTracker |
| **资源原子化 P7** | download_core 半截落地？ | `.part` 临时文件 → 下载完成 → 校验 hash → `File.Move` → 失败 Delete |
| **信任边界 P12** | AI 返回的 HTML 片段会不会 XSS？ | React 默认 escape + Bridge 层 `SecurityElement.Escape` 二次处理 |
| **诚实返回链 P4** | Tool 静默成功？ | 每个 Tool 必须返回 `ToolResult.Success + Error + Data` 三字段，单测强制校验 |

---

## 7. 新增/修改文件清单（预估）

| 操作 | 文件 | 说明 |
|------|------|------|
| 新增 | `DeepSeek/IAiTool.cs` | 工具接口 + ToolResult record |
| 新增 | `DeepSeek/ToolRegistry.cs` | 工具注册表 |
| 新增 | `DeepSeek/Tools/ReadLogTool.cs` | 10 个工具，每个一个文件 |
| 新增 | `DeepSeek/Tools/CheckPortTool.cs` | 同上 |
| 新增 | `DeepSeek/Tools/CheckJavaTool.cs` | 同上 |
| 新增 | `DeepSeek/Tools/ListRegionsTool.cs` | 同上 |
| 新增 | `DeepSeek/Tools/ReadServerPropertiesTool.cs` | 同上 |
| 新增 | `DeepSeek/Tools/DownloadCoreTool.cs` | 同上 |
| 新增 | `DeepSeek/Tools/SearchDocTool.cs` | 同上 |
| 新增 | `DeepSeek/Tools/BackupWorldTool.cs` | 同上 |
| 新增 | `DeepSeek/Tools/GetSystemInfoTool.cs` | 同上 |
| 新增 | `DeepSeek/Tools/ListAvailableCoresTool.cs` | 同上 |
| 新增 | `DeepSeek/FunctionCallingEngine.cs` | 多轮对话引擎 |
| 重写 | `DeepSeekService.cs` | 底层 HttpClient 保留 + 外层包装 FunctionCallingEngine |
| 修改 | `BridgeActionRegistrar.cs` | 注册 4 个新 action |
| 修改 | `DiagnosticTypes.cs` | DeepSeekAnalysis 可能微调字段 |
| 新增 | `AIDrawer.tsx`（frontend） | AI 抽屉组件 |
| 修改 | `TutorialOverlay.tsx`（frontend） | 加「让 AI 帮我」按钮 + 与 AIDrawer 并排 |
| 修改 | `TroubleshootingPage.tsx`（frontend） | AI 抽屉也能在独立页面打开 |
| 修改 | `bridge.ts`（frontend） | 加 4 个新 action + 4 个新 event 类型 |
| 新增 | `BridgeContractTests.cs` 扩展 | 契约测试 |

---

## 8. Token 预算

| 项目 | 预算 | 说明 |
|------|------|------|
| 单次诊断调用 | 2500-5500 tokens | 10 轮上限 |
| SystemPrompt（tools schema） | ~1500 tokens | 10 个 tool 定义 |
| 每月最大用量 | 预留 500 次诊断 | 新手不会开服场景不会高频用 |
