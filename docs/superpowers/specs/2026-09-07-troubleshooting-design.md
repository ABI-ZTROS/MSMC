# MSMC 疑难解答（Troubleshooting）设计文档

> **创建日期**: 2026-09-07  
> **状态**: Draft（待用户确认）  
> **作者**: Agent + 用户启发式对话生成

---

## 1. 一句话定位

**服务端 Minecraft 运维专家系统** — 全链路一站式体检，二叉树式诊断 + 预设词约束的 DeepSeek AI 推理引擎 + 存档 NBT/Region 离线分析 + 可视化问题树 + 可验证的自动修复。

---

## 2. 用户故事

| 编号 | 用户故事 | 优先级 |
|------|---------|--------|
| US-1 | 服主点侧边栏「🔧 疑难解答」（血红色强制配色）→ 自动扫描 10-30 秒 → 看到分级问题树 | P0 |
| US-2 | 扫描结果喂给 DeepSeek → AI 输出严格 JSON 格式诊断结论 → 渲染成可交互问题树 | P0 |
| US-3 | 用户对某个问题点「追问更多」→ 二叉树对话（自动分支或用户回答跳分支） | P0 |
| US-4 | 存档扫描：玩家 .dat NBT 异常 + 财富 Top10；Region .mca 物品堆叠 / 实体堆叠 / 方块异常 | P0 |
| US-5 | 修复前：diff 预览 + 信任确认弹窗（全自动 = 一键执行；需确认 = 问每步） | P0 |
| US-6 | 修复中：进度可视化 + 每步可取消 + 自动备份 | P1 |
| US-7 | 导出 Markdown / JSON 报告 → 自动归档到日志目录 | P1 |
| US-8 | DeepSeek API Key 用户输入 + DPAPI 加密存储 | P1 |
| US-9 | 双 AI 模式：后处理（快）/ 引导式（准）— 用户可切换 | P2 |
| US-10 | 内置预设 Prompt 防止 DeepSeek 降智抽风 | P0 |

---

## 3. 架构总览

```
┌─────────────────────────────────────────────────────────────────────┐
│                          前端 (React+Vite)                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│  │ 诊断页面      │  │ 问题树渲染    │  │ 二叉树对话 UI            │  │
│  │ (血红色配色)  │  │ (红/黄/绿)   │  │ (气泡 + 选项按钮)        │  │
│  └──────┬───────┘  └──────┬───────┘  └──────────┬───────────────┘  │
│         │                 │                       │                  │
│  ┌──────┴─────────────────┴───────────────────────┴───────────────┐ │
│  │                     Bridge API                                 │ │
│  │  diagnostic.runDiagnostic  diagnostic.askAI                    │ │
│  │  diagnostic.getReport      diagnostic.setApiKey                │ │
│  └─────────────────────────────┬──────────────────────────────────┘ │
└────────────────────────────────┼────────────────────────────────────┘
                                 │
┌────────────────────────────────┼────────────────────────────────────┐
│                     后端 (.NET 9 WPF)                                │
│  ┌─────────────────────────────┴──────────────────────────────────┐ │
│  │                 DiagnosticEngine (后端推理引擎)                  │ │
│  │                                                                │ │
│  │  ┌────────────┐ ┌──────────────┐ ┌──────────────────────────┐ │ │
│  │  │ CheckRunner │ │ DecisionTree │ │ DeepSeekClient            │ │ │
│  │  │ (无状态体检 │ │ (硬编码二叉树 │ │ (Tool Calls + strict JSON │ │ │
│  │  │  方法集合)  │ │  节点 + 跳转) │ │  + Files API)             │ │ │
│  │  └─────┬──────┘ └──────┬───────┘ └──────────┬───────────────┘ │ │
│  │        │               │                     │                │ │
│  │  ┌─────┴───────────────┴─────────────────────┴──────────────┐ │ │
│  │  │              DiagnosticReport DTO (统一输出)               │ │ │
│  │  └───────────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────┘ │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  Minecraft 存档扫描层（新增）                                    │ │
│  │  NbtReader + RegionReader + PlayerAnalyzer + ChunkAnalyzer      │ │
│  └────────────────────────────────────────────────────────────────┘ │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  复用已有服务                                                    │ │
│  │  IServerManagerService / IJavaInstallationService /             │ │
│  │  ICpuPowerService / INetworkService                             │ │
│  └────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────┘
```

### 设计选择回顾

| 选项 | 我们选了什么 | 为什么 |
|------|-------------|--------|
| 架构 | **方案 B：后端推理引擎** | AI 输出必须是 MSMC 能解析的 JSON，后端统一控制 schema + 预设词 + strict 模式 |
| AI 定位 | **大脑 + 对话辅助** 双角色 | 综合分析生成结构化诊断结论（大脑）+ 对话中辅助理解术语（助手） |
| AI 介入 | **双模式可切换** | 后处理模式快（先扫完再问 AI）/ 引导模式准（AI 决定下一轮查什么） |
| 存档来源 | **直接读存档文件（离线）** | 不依赖服务器运行；Java 版 NBT/Region 格式 |
| 修复激进程度 | **用户决定** — 全自动 / 每步确认 / 测试模式 | 尊重用户选择权 |
| 报告输出 | **全部都要** — 页面内 + 可导出 Markdown/JSON + 自动归档 | 方便分享 / 存档 / 远程求助 |
| UI 配色 | **血红色强制配色** — 不被主题系统干扰 | 醒目、有紧急/告警感 |

---

## 4. 核心数据结构

### 4.1 DiagnosticReport（后端 → 前端统一 DTO）

```csharp
public record DiagnosticReport(
    DateTime GeneratedAt,
    string MsmcVersion,
    string ServerJarPath,
    string WorldPath,
    ServerInfo Server,
    List<CheckResult> Checks,
    DiagnosticSummary Summary,
    List<Issue> Issues,
    List<PlayerStat> TopPlayers,
    DeepSeekAnalysis? AiAnalysis,
    string? DeepSeekRawResponse
);

public record CheckResult(
    string CheckId,           // "java.version" / "region.entity.stack" ...
    Severity Severity,        // Info / Warning / Error / Critical / OK
    string Category,          // "Java" / "Process" / "Network" / "Config" / "Storage" / "Player" / "Region"
    string Title,             // "Java 版本与服务器不兼容"
    string Detail,            // "服务器需要 Java 21，但检测到运行时是 Java 17"
    bool AutoFixable,
    FixAction? SuggestedFix,
    object? RawData           // 原始数值（进程 CPU / 区块实体数 ...）
);

public enum Severity { OK = 0, Info = 1, Warning = 2, Error = 3, Critical = 4 }

public record FixAction(
    string FixId,             // "java.switch.version" / "port.kill.process"
    string Label,             // "自动切换到 Java 21"
    bool Dangerous,           // 是否有数据丢失风险
    string? DiffPreview,      // 修复前后 diff（文本）
    List<FixStep> Steps       // 分步执行计划
);

public record FixStep(
    string Label,             // "备份存档目录到 .bak-时间戳"
    string ActionType,        // "backup" / "command" / "config_edit" / "kill_process" / "restart"
    Dictionary<string, object?> Params,
    bool ConfirmRequired      // 该步是否需要用户确认
);

public record Issue(
    string IssueId,
    Severity Severity,
    string Category,
    string Title,
    string Detail,
    string? Hint,             // "这通常是因为..."
    string? Suggestion,       // "建议你..."
    FixAction? Fix,
    Dictionary<string, object?> Context // 上下文（坐标 / 玩家 UUID / 端口 ...）
);

public record DeepSeekAnalysis(
    string Summary,                        // AI 自然语言总结
    List<string> KeyFindings,              // AI 提炼的关键发现
    List<FixAction> RecommendedActions,    // AI 推荐的修复（可能多个）
    string RawJson                         // AI 原始 JSON 输出（用于调试）
);
```

### 4.2 二叉树节点（前端 JSON 配置）

```typescript
interface TreeNode {
  id: string
  category: 'Player' | 'Map' | 'Plugin' | 'PluginConfig' | 'Network' | 'System'
  question: string                        // "服务器启动后几秒就崩了？"
  options?: { label: string; next?: string; severity?: Severity }[]
  // AI 辅助: 当用户回答不在预设选项里时
  aiHint?: string                         // 把用户自由文本喂给 AI，AI 判断下一个节点
  checkReference?: string                 // 关联 CheckId（前端可以在此处触发特定检查）
}
```

### 4.3 DeepSeek 输出 JSON Schema（预设词严格约束）

```json
{
  "schema": {
    "type": "object",
    "properties": {
      "summary": { "type": "string", "description": "AI 对诊断结果的一句话总结" },
      "key_findings": {
        "type": "array",
        "items": { "type": "string" },
        "description": "3-5 条关键发现，按严重程度排序"
      },
      "recommended_fixes": {
        "type": "array",
        "items": {
          "type": "object",
          "properties": {
            "fix_id": { "type": "string", "enum": ["backup", "java_switch", "port_kill", "config_edit", "restart"] },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
            "rationale": { "type": "string" },
            "steps": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "label": { "type": "string" },
                  "action_type": { "type": "string", "enum": ["backup", "command", "config_edit", "kill_process", "restart"] },
                  "dangerous": { "type": "boolean" },
                  "confirm_required": { "type": "boolean" }
                },
                "required": ["label", "action_type", "dangerous", "confirm_required"]
              }
            }
          },
          "required": ["fix_id", "confidence", "rationale", "steps"]
        }
      },
      "need_more_info": {
        "type": "boolean",
        "description": "是否需要引导式诊断模式（当前信息不足）"
      },
      "suggested_questions": {
        "type": "array",
        "items": { "type": "string" },
        "description": "如果 need_more_info=true，AI 建议的下一个追问"
      }
    },
    "required": ["summary", "key_findings", "recommended_fixes"]
  }
}
```

---

## 5. 后端检查点清单（CheckRunner 实现）

### 5.1 系统/配置层（大部分复用已有服务）

| CheckId | 方法 | Severity 规则 | 复用服务 |
|---------|------|---------------|---------|
| `java.version` | 检测服务器 jar 要求的 Java 版本 vs 运行时版本 | 不匹配 → Error | `IJavaInstallationService` |
| `java.heap.size` | 分析启动参数 -Xmx/-Xms 是否合理 | < 物理内存 25% → Warning | 新增 |
| `java.gc.flags` | GC flags 配置（-XX:+UseG1GC / -XX:+ParallelRefProcEnabled） | 缺失推荐 GC flag → Warning | 新增 |
| `process.priority` | 服务器进程优先级（应该是 AboveNormal / High） | Normal → Warning | `ICpuPowerService` |
| `process.t1.qos` | T1 用户态 QoS 调度是否生效 | 未生效 → Error | `ICpuPowerService` |
| `process.t3.tuning` | T3 最大权限调度（CPU Set + Priority Boost + timer precision） | 部分未生效 → Warning | `ICpuPowerService` |
| `port.availability` | server.properties 里的 server-port 是否被占用 | 被占用 → Error | `INetworkService` |
| `port.firewall` | Windows 防火墙是否放行该端口 | 未放行 → Error | 新增 |
| `config.syntax` | server.properties / bukkit.yml / spigot.yml 语法校验 | 语法错误 → Error | 新增 |
| `config.max-players` | max-players 配置与 hardware 建议匹配度 | > 建议 2 倍 → Warning | 新增 |

### 5.2 文件系统层（新增）

| CheckId | 方法 | Severity 规则 |
|---------|------|---------------|
| `storage.world.size` | world 目录总大小 | > 50GB → Warning |
| `storage.region.count` | .mca 文件数量 + 平均文件大小 | 平均 > 10MB 且有卡顿 → Info |
| `storage.player.count` | players 目录下 .dat 数量 | 正常是 warning，但异常数量（10k+）→ Error |
| `plugin.count` | plugins/mods 目录文件数 | > 50 → Warning（过多插件可能冲突） |
| `plugin.jar.validity` | 校验所有 .jar 是否可被 Java ClassLoader 加载 | 加载失败 → Error |

### 5.3 存档分析层（新增）

| CheckId | 方法 | Severity 规则 |
|---------|------|---------------|
| `player.nbt.anomaly` | 扫描每个 player .dat：物品数量 > 99、damage > 正常上限、非法 enchant、NBT 大 payload | 每项异常 → Warning；同一玩家 > 10 项 → Error |
| `player.wealth.top` | 统计每个玩家物品总价值（按 MC Wiki 价值表） | 输出 Top 10 榜单（Info 级别） |
| `region.entity.stack` | 扫描所有 .mca 区块：同坐标实体数 > 阈值（僵尸堆叠/掉落物堆叠） | > 50 个 → Warning；> 200 → Error |
| `region.item.stack` | 扫描区块里 item_entity：同坐标同类型数量 > 64 | > 256 → Error |
| `region.block.anomaly` | 扫描区块里的 TNT 爆炸残留 / command_block / structure_block / 非法方块状态 | command_block 未禁用 → Warning；大量 TNT → Warning |

### 5.4 日志分析层（新增）

| CheckId | 方法 | Severity 规则 |
|---------|------|---------------|
| `log.startup.failure` | 解析服务器 latest.log / logs/ 目录 | 有 ERROR / WARN 关键字 → 按频率分级 |
| `log.outmemory` | 搜索 `java.lang.OutOfMemoryError` | 存在 → Critical |
| `log.chunk.generation` | 搜索 `Could not pass event CHUNK_GENERATION` | 高频 → Warning |

---

## 6. DeepSeek 集成细节

### 6.1 API Key 存储

- 用户在疑难解答页面首次输入 API Key
- 后端用 **DPAPI（Data Protection API）** 加密后存入 `%AppData%/io.NET.ZTR_OS/config/deepseek.key`
- DPAPI 绑定当前 Windows 用户，其他用户无法解密
- 设置页有「清除 AI Key」按钮

### 6.2 预设词系统（防降智）

**System Prompt 结构**（固定，不暴露给用户）：

```
[角色声明]
你是 Minecraft Java 版服务器运维专家，熟悉 1.7-1.21 所有版本、Paper/Spigot/Fabric/Forge 各核心。
你的任务是分析 MSMC 工具收集的诊断数据，输出结构化 JSON。

[输出约束 — 核心！]
1. 你必须严格按照以下 JSON Schema 输出，不得添加任何 schema 之外的字段
2. 不得输出 markdown、不得输出自然语言段落、所有字段都是 JSON 原生类型
3. 不确定时 confidence 设低，不要瞎编
4. fix_id 必须从已知列表选择：backup / java_switch / port_kill / config_edit / restart
5. 如果数据不足以做任何有信心的推荐，设 need_more_info=true 并给出 suggested_questions

[MC 知识上下文]
(这里硬编码 Minecraft 各版本 Java 要求、常见 NBT 异常类型、区块实体堆叠阈值等知识库)

[诊断报告 JSON Schema]
[内嵌 4.3 节的完整 Schema]
```

**User Prompt 结构**（动态拼接）：

```
以下是 MSMC 工具对 {server.jar.name} 的诊断结果：
---
{CheckRunner 输出的原始 JSON，截断到 token 上限}
---
请分析并输出符合 Schema 的 JSON 诊断结论。
```

### 6.3 API 调用参数（strict 模式）

```http
POST https://api.deepseek.com/v1/chat/completions
Content-Type: application/json
Authorization: Bearer {encrypted_api_key}

{
  "model": "deepseek-v4-pro",
  "thinking": { "type": "enabled" },
  "reasoning_effort": "high",
  "messages": [
    { "role": "system", "content": SYSTEM_PROMPT },
    { "role": "user", "content": USER_PROMPT }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "output_diagnosis",
        "description": "输出诊断结论，必须严格按照给定的 JSON Schema",
        "parameters": { /* 完整 Schema */ },
        "strict": true    // ← 关键！强制 JSON Schema 校验
      }
    }
  ],
  "tool_choice": { "type": "function", "function": { "name": "output_diagnosis" } }
}
```

### 6.4 双模式实现

| 模式 | 流程 | 适用场景 |
|------|------|---------|
| **后处理（默认）** | CheckRunner 全部跑完 → 一次性喂给 AI → 输出完整 DiagnosticReport | 绝大多数情况 |
| **引导式（高级）** | CheckRunner 先跑快速子集（10 秒）→ AI 判断还需要什么 → 后端跑针对性检查（按 AI 建议）→ 循环 ≤ 3 轮 → 最终输出 | 疑难杂症 / AI 觉得信息不足时 |

引导式模式的 AI tool_calls：后端把"可执行检查清单"暴露给 AI，AI 通过 tool call 说「请执行 check_id=region.entity.stack 范围=x:100-200,z:100-200」，后端执行后返回结果，AI 再决定下一步。

### 6.5 Token/成本保护

- 预设词里明确要求 **JSON 紧凑输出**（禁止额外解释）
- DiagnosticReport 原始 JSON 截断到 **8000 tokens** 再喂 AI
- 后端设 **单次调用超时 30 秒 + 总 token 上限 20k**
- 失败降级：AI 挂了就只展示 CheckRunner 原始结果（无 AI 分析也能看）

---

## 7. 存档扫描技术选型

### 7.1 NBT 解析

| 方案 | 说明 | 决定 |
|------|------|------|
| 自研 | 纯 .NET + System.IO.Compression 解析 GZIP/ ZLIB + 手写 NBT tag 解析 | ✅ 选这个 — 无外部依赖、体积可控、能精准控制要扫哪些 tag |
| fork 开源 | 用 Hyperion/NBT.NET 等 | ❌ 引入依赖且多数不支持 .NET 9 |

### 7.2 Region 解析

Region = GZIP 压缩的 NBT chunk 数组，每个 .mca 文件 1024 chunks（32×32）。自研 GZIP 解压 + NBT 解析即可，无需额外库。

### 7.3 扫描范围策略

全量扫描世界目录可能耗时数十秒（世界 50GB+）。策略：

- **首次扫描**：只扫 `region/` 目录，跳过 `entities/` / `poi/` / `data/`（按需）
- **增量**：记住上次扫描时间戳，只扫变动的 .mca
- **可中断**：按区块粒度中断，已完成的区块结果保留

---

## 8. 前端页面设计

### 8.1 页面位置与配色

- 侧边栏新增独立项：`🔧 疑难解答`
- **强制血红色配色** — 完全不走主题系统，硬编码 `#c0392b` 为主色、`#e74c3c` 为高亮色
- 背景可随主题变化，但标题栏、按钮、进度条固定血红

### 8.2 双轨并行布局

```
┌───────────────────────────────────────────────────────────────────┐
│  🔧 疑难解答                              [状态: 准备就绪]         │
│                                                                   │
│  ┌─ 左轨: 自动扫描 ──────────────┐  ┌─ 右轨: 症状导航 ──────────┐ │
│  │                                │  │                            │ │
│  │  [🔍 开始一键体检]            │  │  服务器启动失败  🟥         │ │
│  │                                │  │  服务器卡顿     🟧         │ │
│  │  ─── 进度 ───                 │  │  崩溃/假死       🟥         │ │
│  │  Java 检查     ✅             │  │  连接不上       🟧         │ │
│  │  进程调度      ✅             │  │  物品异常       🟧         │ │
│  │  端口检查      ⏳             │  │  地图区块异常   🟥         │ │
│  │  存档扫描      ⏳ (60%)       │  │  插件问题       🟨         │ │
│  │  DeepSeek AI   ⏳             │  │  系统问题       🟨         │ │
│  │                                │  │  配置自检       🟨         │ │
│  └────────────────────────────────┘  └────────────────────────────┘ │
│                                                                   │
│  ┌─ 问题树（扫描完成后展开）────────────────────────────────────┐ │
│  │ 🟥 Critical  (2)                                             │ │
│  │   ├─ region.entity.stack   #1005 Zombie 挤在 (x:1523, z:89) │ │
│  │   │    [🔧 自动修复]  [📄 diff预览]  [💬 追问AI]             │ │
│  │   └─ player.nbt.anomaly    Steve 持有 damage=99999 钻石剑   │ │
│  │ 🟧 Warning  (5)                                              │ │
│  │   ├─ ...                                                      │ │
│  │ 🟢 OK (12)                                                   │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                   │
│  ┌─ 修复面板（点"🔧 自动修复"后展开）───────────────────────────┐ │
│  │ ┌─ 信任确认 ──────────────────────────────────────────────┐  │ │
│  │ │ ⚠️  修复将执行以下操作:                                  │  │ │
│  │ │    1. [备份] world 目录 → .bak-20260907-230000           │  │ │
│  │ │    2. [清理] 删除 (1523,89) 区块内的僵尸实体              │  │ │
│  │ │    3. [改属性] 把 Steve 的钻石剑 damage 重置为 0         │  │ │
│  │ │                                                         │  │ │
│  │ │   ○ 全自动执行（推荐）                                   │  │ │
│  │ │   ● 每步都问我确认                                        │  │ │
│  │ │   ○ 先模拟一遍（测试模式）                               │  │ │
│  │ │                                                         │  │ │
│  │ │   [确认修复]  [取消]                                     │  │ │
│  │ └───────────────────────────────────────────────────────────┘  │ │
│  └───────────────────────────────────────────────────────────────┘ │
│                                                                   │
│  [📋 导出报告]  [🤖 AI 分析模式: 后处理 ⇄ 引导式]  [⚙️ AI 设置]  │
└───────────────────────────────────────────────────────────────────┘
```

### 8.3 二叉树对话

用户从右轨点击「服务器启动失败」→ 跳到 `node.server.boot.fail` → 显示问题：

```
服务器启动后几秒就崩了？

  ├─ 没崩，但一直卡在 Preparing spawn area
  │   → 跳到 node.boot.stuck.spawn
  ├─ 崩了，有 crash-reports
  │   → 跳到 node.boot.crash.with-report（自动关联 log.startup.failure 检查结果）
  ├─ 崩了，但没 crash-reports
  │   → 跳到 node.boot.crash.no-report
  └─ 不确定 / 让 AI 帮我分析
      → DeepSeek 辅助: 把 latest.log 尾部 500 行喂给 AI，AI 判断下一个节点
```

---

## 9. 桥接接口定义

```typescript
interface DiagnosticBridge {
  /** 一键全链路体检 */
  runDiagnostic(): Promise<{ success: boolean; report?: DiagnosticReport; error?: string }>

  /** 增量体检（AI 引导式用，只跑指定 CheckId） */
  runChecks(checkIds: string[]): Promise<{ success: boolean; report?: DiagnosticReport }>

  /** 二叉树对话：用户回答问题 → 返回下一个节点 */
  advanceDialog(nodeId: string, answer: string): Promise<{ nextNode: TreeNode; matchedCheckIds: string[] }>

  /** 执行修复（带信任模式参数） */
  executeFix(fixId: string, trustMode: 'auto' | 'step-by-step' | 'dry-run'): Promise<FixResult>

  /** 继续修复（step-by-step 模式下，用户确认了当前步） */
  continueFix(fixId: string): Promise<FixStepResult>

  /** 中断修复 */
  cancelFix(fixId: string): Promise<{ success: boolean }>

  /** 获取修复 diff 预览 */
  getFixPreview(fixId: string): Promise<{ diff: string; operations: string[] }>

  /** 导出报告 */
  exportReport(format: 'markdown' | 'json', path?: string): Promise<{ path: string; size: number }>

  /** AI 设置 */
  setDeepseekApiKey(key: string): Promise<{ success: boolean }>
  getDeepseekSettings(): Promise<{ configured: boolean; maskedKey: string; model: string }>

  /** 手动触发 AI 引导式诊断 */
  startGuidedDiagnosis(): Promise<{ success: boolean }>
}
```

---

## 10. 错误处理

| 场景 | 行为 |
|------|------|
| DeepSeek API Key 未配置 | 跳过 AI 分析，只展示 CheckRunner 原始结果 + 提示「配置 API Key 解锁 AI 分析」 |
| DeepSeek API 调用失败（超时/429/5xx） | 降级：展示原始检查结果 + 错误标记（不阻塞主流程） |
| DeepSeek 输出不符合 JSON Schema | 用预设词兜底 + 重试 1 次；再不行 → 降级 |
| 存档扫描读到损坏的 .mca | 跳过该文件 + 记录 Warning + 继续扫其他 |
| 修复操作中途异常 | 自动回滚到修复前的备份 + 展示回滚结果 |
| 用户取消修复 | 停止当前步 + 已执行的操作展示状态（哪些完成了、哪些没做） |

---

## 11. 性能预期

| 操作 | 目标耗时 |
|------|---------|
| 全链路一键体检 | 10-30 秒（取决于世界大小） |
| 存档扫描（世界 10GB） | < 20 秒 |
| DeepSeek AI 分析 | 5-15 秒 |
| 首次加载（含所有检查） | < 25 秒 |
| 增量体检 | < 5 秒 |

---

## 12. 实施阶段划分

| 阶段 | 内容 | 交付物 |
|------|------|--------|
| **P0 核心** | DiagnosticEngine 骨架 + 10 个系统/配置检查点 + 血红色页面骨架 + 问题树渲染 | 能跑的诊断页面（无存档扫描、无 AI） |
| **P0 核心** | 存档扫描：NbtReader + RegionReader + 玩家 NBT 异常检测 + 实体堆叠检测 | 存档分析能力 |
| **P0 核心** | 修复框架：FixAction/FixStep 模型 + 自动备份 + 执行引擎（支持 1 个 sample fix） | 可扩展的修复框架 |
| **P1 AI** | DeepSeekClient + 预设词 + strict JSON + DPAPI Key 存储 | AI 后处理模式 |
| **P1 AI** | DeepSeek Files API 读日志 + 引导式诊断模式（Tool Calls 控制检查流程） | AI 引导模式 |
| **P1 UX** | 修复面板（信任确认 + 进度可视化 + 每步可取消） + Diff 预览 + 测试模式 | 完整修复 UX |
| **P2 增强** | 财富 Top10 榜单 + 更多修复动作 + 报告导出 + 会话分享 | 增强功能 |

---

## 13. 已知约束 / 技术债务

1. **Bedrock 版不支持** — NBT 格式相同但存档用 LevelDB + 压缩方式不同，留 V2
2. **Minecraft 版本覆盖** — 硬编码知识库到 1.7-1.21，Fabric/Forge 模组兼容性需更多测试
3. **Region 全量扫描** — 大地图（100GB+）扫描耗时不可避免，增量机制只补偿增量变化
4. **非管理员运行 MSMC** — SetCurrentProcessExplicitAppUserModelID 等 COM/P/Invoke 在非管理员下可用但 Toast 通知受限（已在 Toast 模块处理）
5. **DeepSeek strict 模式限制** — Tool Calls + strict 依赖 deepseek-v3.2+ / v4 模型，API 版本不够时降级

---

## 14. 不在 scope 里的东西（YAGNI）

- RCON 实时连接服务器（只扫存档离线分析）
- Web 控制台远程协助（会话分享留 P3）
- Windows Crash Dump 自动分析（可以读 dump 但不做自动化 WinDbg 分析）
- 自动修复插件冲突（只检测 .jar 合法性，不帮用户卸载）

---

## 15. 启发式对话锁定的实施细节（2026-09-07）

> 本轮通过启发式问答从用户获取的最终决策，覆盖之前 spec 留白区域。

### 15.1 TreeNode 节点来源
**硬编码 TS 常量**（`src/frontend/src/data/troubleshootingNodes.ts`）。理由：结构清晰、版本跟前端走、P0 最快交付。后续加节点直接改数组。

### 15.2 P0 FixAction 数量：7 个全落地
| fix_id | 真实实现 | 后端文件 |
|--------|---------|---------|
| `backup.world` | 复制 world/ → `.bak-{timestamp}/`，File.Copy 递归 + 进度 | `FixExecutor.cs` |
| `java.switch.version` | 调 IJavaInstallationService 切换 Java → 改启动脚本 | `FixExecutor.cs` |
| `port.kill.process` | Process.GetProcessById.Kill() + 端口重检 | `FixExecutor.cs` |
| `config.edit.server-properties` | 改 server.properties max-players / server-port → 写回 | `FixExecutor.cs` |
| `region.clean.entities` | 读 .mca → 删怪物/掉落物 entity → GZIP 重写区块 NBT | `FixExecutor.cs` + 复用 DiagnosticRegionReader |
| `player.reset.damage` | 读 .dat NBT → ItemStack.damage 归 0 → GZIP 重写 | `FixExecutor.cs` + 复用 DiagnosticNbtReader |
| `server.kill` | Process.Kill() + WaitForExit | `FixExecutor.cs` |

### 15.3 进入页面触发方式
**分阶段：快速自动 + 深度手动**
- 进入页面 → 自动跑系统/配置层 10 个检查点（3-5s）→ 实时渲染问题树骨架
- 存档扫描 + 日志分析等用户点「🔬 深度扫描」按钮才触发（原因：可能耗时 10-30s + 需要服务器不在运行中）

### 15.4 运行中服务器存档处理
**先检测运行态 → 问是否杀进程 → 不运行就全力扫**
1. 深度扫描前先调 `checkServerRunning` bridge handler 检查目标服务器进程
2. 如果运行中 → 前端弹确认框「服务器正在运行，杀掉它再扫？」
3. 用户确认 → 后端先杀进程再扫；用户拒绝 → 跳过存档/日志层，只展示系统/配置层结果 + Warning 标记
4. 不运行 → 全力扫

### 15.5 日志分析范围
**CheckRunner 内建 3 个检查点，聚焦 ERROR/WARN**
- `log.startup.failure`: 扫描 latest.log / logs/ 目录中 ERROR / WARN 关键字出现频率
- `log.outmemory`: 搜索 `java.lang.OutOfMemoryError` → Critical
- `log.chunk.generation`: 搜索 `Could not pass event CHUNK_GENERATION` 高频 → Warning

### 15.6 目标服务器选择
**跟随 MainViewModel 当前选中的 ServerInstance**。页面顶部加切换器可临时换目标。

### 15.7 实施节奏：Bridge 契约先锁死 → 并行开发
1. 第一步：锁死 `DiagnosticTypes.cs` record ↔ `types/bridge.ts` TypeScript interface 零偏差
2. 前后端并行：后端补 FixExecutor + LogAnalyzer + Bridge handler；前端写页面 + 组件 + TreeNode
3. 最后联调

### 15.8 Bridge 接口（最终版）
```
diagnostic.runDiagnostic          → 快速扫描（系统/配置层）
diagnostic.runDeepScan            → 深度扫描（存档+日志+可选杀进程）
diagnostic.executeFix             → 执行修复（支持 auto / step-by-step / dry-run）
diagnostic.cancelFix              → 中断修复
diagnostic.exportReport           → 导出 Markdown / JSON
diagnostic.checkServerRunning     → 检测目标服务器是否在运行
diagnostic.killServerAndScan      → 杀进程 + 立即深度扫描
```

### 15.9 红线约束
- **绝不写空壳子**：每个 UI 组件必须接真实数据；每个 FixAction 必须真执行；每个 bridge handler 必须有后端逻辑
- **证据对齐**：汇报时每一项变更必须能对应到可观察的文件修改片段
