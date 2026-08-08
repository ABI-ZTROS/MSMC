# MSMC V2 规划 · 三链原则落地版

> 审阅人：傻狍子（用户）
> 编制时间：2026-08-09
> 核心约束：所有模块设计与代码实现必须严格遵循三链原则

---

## 0. 三链原则（定义与落地标准）

| 链 | 核心定义 | 代码层面可观察性 | 否决条件 |
|----|---------|-----------------|---------|
| **因果链** | 因为干了什么，所以要执行什么 | 每个"果"必须能追溯到一个明确的"因"（事件 / 触发器 / 命令），不得存在"无因之果" | 无法回答"为什么执行这段代码？" |
| **执行链** | 每一个按钮、每一次循环都要形成咬合闭环；有冗余、有兜底 | (1) 入口→处理→异常→收尾 四段式完整<br>(2) 关键操作有重试 / 防重入 / 并发保护<br>(3) 失败有明确 fallback | 出现"悬空按钮" / "无限重试" / "并发踩踏" 任一 |
| **返回链** | 日志是神圣不可侵犯的兜底 | 每一个因果链的"果"和执行链的"段"都必须有结构化日志（Information 起始 → Information/Error 收尾），异常必须 LogError | 任何关键路径发生异常后无日志或日志不可检索 |

### 0.1 三链自检 Checklist（每个 PR 必须打勾）

- [ ] **因果链**：能在 3 行内写出"因 → 果"的对应关系（事件名 / 触发源 / 目标动作）
- [ ] **执行链·咬合**：按钮/函数 有 `try { 主路径 } catch { 兜底 } finally { 收尾 }`
- [ ] **执行链·冗余**：关键 I/O 操作有重试（指数退避 + 最大次数 + 取消令牌）
- [ ] **执行链·防重入**：并发敏感操作用 `SemaphoreSlim` / `Interlocked` / 原子集合
- [ ] **执行链·兜底**：每一个失败分支都有"下一个合理动作"（重试 → 降级 → 禁用 → 仅日志）
- [ ] **返回链·起始**：每个 public 入口第一行 `LogInformation("... starting ...")`
- [ ] **返回链·过程**：关键分支节点（成功/失败/重试）有对应级别日志
- [ ] **返回链·终止**：每个 public 方法退出前有 `LogInformation("... completed (Success/Fail)")`
- [ ] **返回链·异常**：`catch` 里必须 `LogError(ex, ...)` 并保留上下文（ID、参数、耗时）

---

## 1. P0 模块（本轮交付重点）

### 1.1 通知系统（Notifications）

| 文件 | 角色 | 因果链 | 执行链 | 返回链 |
|------|------|--------|--------|--------|
| `Features/Notifications/Models/NotificationChannel.cs` | 枚举/配置 | 定义"因"（EventType）与"果"（ChannelType） | — | — |
| `Features/Notifications/Models/NotificationEvent.cs` | 事件模型 | 事件 = 因果链的"因" | — | — |
| `Features/Notifications/Services/INotificationService.cs` | 唯一入口接口 | 对外唯一通知触发点 | — | — |
| `Features/Notifications/Services/NotificationService.cs` | 路由调度 | 因→果：按 EventType 决定哪些通道启用 | 串行 try/catch 调度各通道，单通道失败不阻塞其他 | 起始/成功/失败日志；Error 记录异常 |
| `Features/Notifications/Services/IDiscordWebhookSender.cs` | 发送接口 | — | 接口便于 Mock | — |
| `Features/Notifications/Services/DiscordWebhookSender.cs` | Discord 发送 | — | 指数退避 + 429 速率限制 + OperationCanceled 处理 + 最大重试上限 | 每次尝试记录；429 Warning；异常 Error |

**代码骨架关键点**：

```
NotificationService.DispatchAsync(NotificationEvent evt, CancellationToken ct)
  { 因果链：evt.EventType 决定哪些通道启用 }
  LogInformation("[Notify] Dispatching event {EventType}")
  try 对每个通道 Send → catch 单通道失败 → LogError 并继续
  LogInformation("[Notify] dispatched: X/Y succeeded")
  return NotificationDispatchResult
```

### 1.2 计划任务调度器（Scheduler）

| 文件 | 角色 | 因果链 | 执行链 | 返回链 |
|------|------|--------|--------|--------|
| `Features/Scheduler/Models/ScheduledTask.cs` | 任务模型 | Trigger(因) → Action(果) | — | — |
| `Features/Scheduler/Services/CronParser.cs` | Cron 解析 | Cron 表达式 → 下次运行时间 | 最大 366 天扫描防死循环 | — |
| `Features/Scheduler/Services/ISchedulerService.cs` | 接口 | — | — | — |
| `Features/Scheduler/Services/SchedulerService.cs` | 核心调度 | 定时器扫描到期任务（因）→ 并行触发（果） | SemaphoreSlim 防并发；try/catch/finally 包裹 ExecuteTaskAsync；连续失败超阈值自动禁用 | 起始/成功/失败/禁用全链路日志 |

**代码骨架关键点**：

```
ExecuteTaskAsync(ScheduledTask task)
  SemaphoreSlim.WaitAsync(0) —— 已在跑则跳过（执行链·防重入）
  LogInformation("[Scheduler] Executing task: {Name}")
  try 根据 ActionType 分派执行
    Success → record.Status=Completed; task.ConsecutiveFailures=0
  catch Exception ex
    record.Status=Failed; task.ConsecutiveFailures++
    LogError(ex, ...)
    if ConsecutiveFailures >= Max → task.Enabled=false; LogWarning("auto-disabled")
  finally 记录 ExecutionRecord；计算 NextRunTime; Semaphore.Release
```

### 1.3 插件市场（ContentMarket）

| 文件 | 角色 | 因果链 | 执行链 | 返回链 |
|------|------|--------|--------|--------|
| `Features/ContentMarket/Models/MarketProject.cs` | 数据模型 | — | — | — |
| `Features/ContentMarket/Services/IMarketProvider.cs` | 供应商接口 | — | 接口便于替换（Modrinth/自建源） | — |
| `Features/ContentMarket/Services/ModrinthProvider.cs` | Modrinth 集成 | 用户搜索 → API 查询（因）；版本选择 → 文件下载（果） | 进度回调；CancellationToken 支持；错误时返回空集合 | 关键请求 LogInformation；HTTP 错误 LogError |
| `Features/ContentMarket/Services/PluginManagerService.cs` | 安装管理 | 安装请求（因）→ 备份→下载→校验→写入→记录（果） | 原子写入（先备份后替换）；SHA1 校验；失败回滚；安装记录持久化 | 每步 LogInformation；校验失败 LogError |

**代码骨架关键点**：

```
PluginManagerService.InstallAsync(MarketVersion version, string serverPath)
  因果链：版本选中 → 触发安装
  1. 原子备份已存在文件（Copy → .bak-{ts}）
  2. 下载到临时路径 tmpFile
  3. SHA1 哈希校验（失败 → 删除 tmp + 回滚备份）
  4. 原子替换：原文件 → bak；tmp → 原位置
  5. 写入 InstallationRecord（版本、时间、来源、备份路径）
  LogInformation("[Market] Installed plugin: {ProjectId} v{Version}")
```

---

## 2. 三链验收用例（每个模块 P0 级场景）

### 2.1 通知系统

| # | 场景 | 三链覆盖 | 预期 |
|---|------|---------|------|
| N1 | ServerCrashed 事件触发 | 因果：Crash→Discord+Toast；执行：两通道并行；返回：日志完整 | Discord 收到红色 embed，Toast 弹出，日志含事件 ID |
| N2 | Discord 429 限频 | 执行链：退避 + Retry-After 尊重 | 最多 3 次重试，间隔递增；最终失败 Toast 仍成功 |
| N3 | Discord 网络异常 | 执行链：单通道失败不阻塞 | 继续投递到 Toast；日志 Error 记录异常 |
| N4 | 所有通道禁用 | 执行链：兜底 | 返回 IsSuccess=true、TotalChannels=0，不抛异常 |

### 2.2 计划任务

| # | 场景 | 三链覆盖 | 预期 |
|---|------|---------|------|
| S1 | Cron 解析：`0 9 * * MON-FRI` | 因果链：因→时间 | 非工作日跳过 |
| S2 | 并发点击 RunNow | 执行链：防重入 | 第二次被 Semaphore 跳过并 LogWarning |
| S3 | 任务连续失败 N 次 | 执行链：失败阈值 → 禁用 | ConsecutiveFailures=N；Enabled=false；LogWarning |
| S4 | 任务成功执行 | 返回链：日志 | 记录 ExecutionRecord；重置 ConsecutiveFailures；LogInformation |

### 2.3 插件市场

| # | 场景 | 三链覆盖 | 预期 |
|---|------|---------|------|
| M1 | 搜索 "iris" | 因果链：查询→结果 | 返回 Modrinth 前 20 条 |
| M2 | 下载带进度回调 | 执行链：进度上报 | 进度 0→100% 单调递增 |
| M3 | SHA1 校验失败 | 执行链：兜底 | 抛异常；删除 tmp；保留原文件 |
| M4 | 安装已存在插件 | 执行链：备份 | 原文件复制为 .bak-{ts}；安装完成记录含备份路径 |

---

## 3. 技术红线（执行过程中不得违反）

1. **不得跳过三链自检**：每个 PR 提交前必须在 Checklist（0.1 节）打勾，Reviewer 抽检未通过直接退回。
2. **不得在 catch 中吞异常**：`catch { }` 或 `catch (Exception) { return; }` 视为致命代码异味，必须有 `LogError(ex, ...)`。
3. **不得使用静态共享状态承载业务**：所有业务状态必须注册为 DI 服务（Scoped/Singleton），静态仅允许用于 HttpClient / 不可变配置。
4. **不得引入第三方重试库**：重试由执行链显式实现（指数退避 + 最大次数 + 取消令牌），拒绝 Polly 等隐式包装。
5. **不得省略 CancellationToken**：所有 public async Task 方法需接受 `CancellationToken ct = default`。
6. **不得以"为了性能"移除日志**：返回链日志视为功能的一部分，不允许通过 #if 或 LOG_LEVEL 宏移除。
7. **不得在单元测试中依赖真实 I/O**：所有外部依赖（HTTP、文件、DB）通过接口注入并 Mock。

---

## 4. 文件级任务分解（已完成项）

### 4.1 已落地（✅）

```
src/MSMC/Features/Notifications/
  ✅ Models/NotificationChannel.cs
  ✅ Models/NotificationEvent.cs
  ✅ Services/INotificationService.cs
  ✅ Services/NotificationService.cs          (v2: 注入 IDiscordWebhookSender 以便测试)
  ✅ Services/IDiscordWebhookSender.cs
  ✅ Services/DiscordWebhookSender.cs

src/MSMC/Features/Scheduler/
  ✅ Models/ScheduledTask.cs
  ✅ Services/CronParser.cs
  ✅ Services/ISchedulerService.cs
  ✅ Services/SchedulerService.cs

src/MSMC/Features/ContentMarket/
  ✅ Models/MarketProject.cs
  ✅ Services/IMarketProvider.cs
  ✅ Services/ModrinthProvider.cs
  ✅ Services/PluginManagerService.cs

src/MSMC.Tests/
  ✅ Services/CronParserTests.cs            (11 cases)
  ✅ Services/NotificationServiceTests.cs   (6 cases)
  ✅ Services/SchedulerServiceTests.cs      (8 cases)
  ✅ MSMC.Tests.csproj 已添加 Moq 4.* 引用
```

### 4.2 待落地（🔲）

| 优先级 | 模块 | 文件 | 关键职责 |
|--------|------|------|---------|
| P0 | DI 注册 | `src/MSMC/App.xaml.cs` | 注册 IDiscordWebhookSender、INotificationService、ISchedulerService、IMarketProvider |
| P0 | 桥接 | `src/MSMC/Services/WebViewBridge.cs` | 注册通知/调度/市场 action，贯通前端→后端的因果链 |
| P1 | 通知扩展 | `EmailNotificationService` / `GenericWebhookSender` | 邮件 / 通用 Webhook 通道 |
| P1 | 通知扩展 | `ToastNotificationService`（已有） | 对接 Windows Toast 实际实现 |
| P1 | 调度扩展 | `SchedulerStorageService` | 任务 JSON 持久化（启动时加载、变更时保存） |
| P1 | 市场扩展 | `PluginFavoritesService` | 收藏夹 + 版本固定 |
| P1 | 市场扩展 | `PluginInstallerUI` ViewModel | 安装进度 UI |
| P2 | 长期历史 | `HistoryAlertService` | 历史数据聚合 + 阈值告警 |
| P2 | 移动 companion | `CompanionBridgeService` | 局域网 WebSocket 通知 |
| P2 | 自动更新 | `AutoUpdateService` | GitHub Release 检测 + 下载 + 校验 |
| P2 | 测试 | `DiscordWebhookSenderTests` | 真实 HTTP Mock 测试 429/退避 |
| P2 | 测试 | `ModrinthProviderTests` | 真实 API Mock 测试 |
| P2 | 测试 | `PluginManagerServiceTests` | 文件系统 Mock + SHA1 校验 |

---

## 5. 三链反模式与修正策略

| 反模式 | 识别信号 | 修正 |
|--------|---------|------|
| 无因之果 | 定时回调里直接写 `DoSomething()`，无事件来源 | 用 Event/Trigger 抽象包裹，在调用点前增加 LogInformation("因：xxx") |
| 悬空按钮 | UI 按钮 Click 后无反馈、无日志 | Click 事件 → 调度 service → 日志 + Toast 反馈 |
| 无限循环 | `while(true)` 无 break/guard | 用 Semaphore + 最大迭代次数 + CancellationToken 守卫 |
| 吞异常 | `catch {}` 或 `catch(e) { throw; }` 无日志 | `catch (Exception ex) { LogError(ex, ...); ... }` |
| 静默失败 | 关键 I/O 失败后方法返回 void | 返回 `Result`/`bool`，并由调用方决定降级 |
| 并发踩踏 | 静态字段累加/字典在多线程读写 | ConcurrentBag / ConcurrentDictionary / Interlocked / Semaphore |
| 日志噪声 | 无级别区分的 LogInformation 淹没关键信息 | 入口 Information → 分支 Debug → 成功 Information → 错误 Error/Critical |

---

## 6. 审阅焦点（请傻狍子重点看）

1. **第 0 节**：三链定义与 Checklist —— 这是后续所有代码的标尺，如定义需调整请直接改。
2. **第 2 节**：验收用例（N1–N4、S1–S4、M1–M4）—— 如场景不对、预期不符请明确指出。
3. **第 3 节**：技术红线 —— 如有禁区要增减在此追加。
4. **第 4.2 节**：文件级待办 —— 如需重排优先级请改 P0/P1/P2。
5. **第 5 节**：反模式表 —— 如有项目内已存在的反模式案例请补充。

---

> **审阅后动作**：傻狍子确认 → 进入 P0 模块的 DI 注册 + WebViewBridge 落地 + 真实 HTTP/Mock 测试扩展。
