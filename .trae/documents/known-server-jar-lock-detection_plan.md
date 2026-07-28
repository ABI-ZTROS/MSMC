# 实施计划：KnownServer JAR 锁定主动检测

## 一、问题陈述（Repo Research Conclusion）

### 当前缺陷
MSMC 内部启动的 Minecraft 服务器在服务器检测面板中经常**无法被识别为已知服务器**，只能退化为「端口扫描兜底发现的未知实例」，表现为：
- DisplayName 显示为 `Unknown @  (PID: xxxx)` 或 `Minecraft Server @  (PID: xxxx)`
- WorkingDirectory 为空字符串
- JAR 路径、服务器类型等元数据全部缺失
- 与 `KnownServers` 列表中的同名服务器条目呈现为两条独立记录

### 根本原因链条
```
StartKnownServerAsync / StartCurrentServerAsync
   │
   ├─ 构造 ServerInstance，调用 _serverManager.StartServer(instance)
   ├─ ProcessStartInfo.UseShellExecute = true
   │     └─ 副作用：进程可能经 cmd/explorer 中继启动，父进程链不等于 MSMC
   │        （ProcessScanner 的"父进程链追溯"策略可能失效）
   ├─ 等待 Task.Delay(1500)
   └─ 调用 DetectAsync() → ServerDetector.DetectAllAsync()
         │
         ├─ 阶段一：ProcessScanner.ScanServerProcessesAsync()
         │     4 重识别规则（JAR关键字 / nogui / 父进程链 / -jar兜底）
         │     可能失败的场景：
         │       ① WMI 在 1.5s 内尚未索引到新 java 进程
         │       ② JAR 文件名不匹配 ServerJarKeywords
         │       ③ UseShellExecute 破坏父进程链
         │       ④ 权限问题导致 WMI 命令行读取失败
         │
         ├─ 如果 processResults.Count == 0 → 走端口扫描兜底（L137）
         │     → DiscoverServersByPortScanAsync 创建 ServerInstance
         │     → ServerType = Unknown, WorkingDirectory = empty
         │
         ├─ 否则逐进程深度检测 BuildServerInstanceAsync（L175）
         │     但 **没有对 KnownServers 做 JAR 锁定交叉核对**
         │
         └─ 阶段四：再跑一次端口扫描（L194）——仍然无法关联到 KnownServer

         ⚠️ 关键遗漏：整个 DetectAllAsync 管道**完全不引用 AppConfigService / KnownServers 列表**，
         也就无法利用"我们知道这台服务器的 JAR 路径"这个先验知识。
```

### 现有未被利用的能力
项目中已实现 **JAR 文件锁定检测**，但**仅用于 `ServerManagerService.IsServerRunning`**，没有进入检测管道：
- [ServerManagerService.cs:L115-L153](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerManagerService.cs#L115-L153) — 双重校验：`IsJarFileLocked` + `FindServerProcess`
- [ServerManagerService.cs:L620-L644](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerManagerService.cs#L620-L644) — `IsJarFileLocked` 核心实现：`FileShare.None` 独占打开 → `IOException` = 被锁定

如果在 `DetectAllAsync` 中加入「KnownServer JAR 锁定」阶段，就能**从源头把已知服务器与运行中进程一一对应**，绕过 ProcessScanner/端口扫描的所有不确定性。

---

## 二、设计思路

采用用户的建议：**从 MSMC 内部启动时，建立 KnownServer → JAR 路径的绑定；检测时，如果发现该 JAR 被占用，则直接反查占用进程，把该进程作为 KnownServer 对应的运行实例补入 servers 列表。**

### 扩展后的 DetectAllAsync 管道
```
DetectAllAsync()
   │
   ├─ 【阶段一】ProcessScanner.ScanServerProcessesAsync()  // 保留不变
   │
   ├─ 【新增 阶段二A】KnownServer JAR 锁定检测（核心新增）
   │     for each knownServer in AppConfigService.GetAllKnownServers():
   │       if JAR路径 不存在 → skip
   │       if IsJarFileLocked(JAR路径) == false → skip
   │       if 此 JAR 对应的 PID 已经在 ProcessScanner 结果中 → skip（避免重复）
   │       pid = FindServerProcessByJarPath(JAR路径, 工作目录)
   │       if pid 找到 且 进程存活:
   │           构造 ServerInstance，从 KnownServer 填充元数据
   │           ServerType = 从 KnownServer 推断（可用 JAR 名+配置文件重新判定）
   │           WorkingDirectory = KnownServer.WorkingDirectory
   │           ServerJarPath = KnownServer.ServerJarPath
   │           DisplayName = 正常显示（非 Unknown）
   │           添加到 servers 列表 + 写入缓存（key = pid）
   │
   ├─ 【阶段二B】逐进程深度检测 BuildServerInstanceAsync  // 保留不变
   │     （缓存命中机制会自动跳过 阶段二A 已添加的 pid）
   │
   └─ 【阶段四】端口扫描兜底  // 保留不变
```

### 额外加固：启动后直接写入「启动时 PID 缓存」
在 `StartKnownServerAsync` / `StartCurrentServerAsync` 成功启动后，**不等 1.5 秒后的 DetectAsync**，立即把 `(KnownServer.Id → pid, jarPath → pid)` 的映射写入 ServerDetector 的**启动时 PID 缓存**，TTL 30 秒。DetectAllAsync 发现缓存中的 pid 仍存活就直接补入 servers，完全绕过 WMI/命令行/父进程链。

### 三个方案的权衡

| 方案 | 描述 | 优点 | 缺点 | 推荐 |
|------|------|------|------|------|
| **A. JAR 锁定交叉检测**（主方案） | DetectAllAsync 中遍历 KnownServers，JAR 锁定 → FindServerProcessByJarPath 找 pid | 不依赖启动时缓存，任意时刻都能正确关联；已被 `IsServerRunning` 验证 | 遍历所有 KnownServer × 调用 IsJarFileLocked（<10ms each），几十台服务器可接受 | ✅ 推荐 |
| **B. 启动时 PID 缓存**（加固方案） | 启动成功立即把 (jarPath→pid, knownId→pid) 写入短期缓存，DetectAsync 直接使用 | 启动后第一次检测 100% 正确，无任何竞态 | 仅覆盖 MSMC 内部启动场景，第三方启动的服务器无效 | ✅ 与 A 叠加 |
| **C. Restart Manager API 拿占用 PID**（远期优化） | P/Invoke RmGetList 直接返回占用进程 PID | 一次调用直接拿 PID，无需枚举进程 | 需要管理员权限；API 复杂；项目中无封装先例 | 暂不做，后续可优化 `FindServerProcessByJarPath` 的性能 |

---

## 三、需要修改的文件和模块

### 3.1 后端 C#

| 文件 | 改动类型 | 说明 |
|------|---------|------|
| **`ServerDetector.cs`** | 新增 + 修改 | ① 新增 `DetectKnownServersByJarLockAsync` 方法；② 在 `DetectAllAsync` 中插入调用；③ 新增 `StartSessionPidCache` 短期缓存（启动时 PID 映射）；④ 注入 `IAppConfigService` 依赖 |
| **`ServerManagerService.cs`** | 新增 + 接口改造 | ① 新增公开方法 `FindServerProcessByJarPath(string jarPath, string workingDir)`（目前 `FindServerProcess` 是私有的，需要公开一个按 JAR 路径查找的重载，或提取为公共方法）；② 或把 `IsJarFileLocked` 提取为 `ServerDetectionHelper` 静态类共享 |
| **`IAppConfigService.cs`** | 无改动 / 确认 | 确认 `GetAllKnownServers()` 已存在且可从 DI 获取 |
| **`ServerDetectionViewModel.cs`** | 小改 | ① `StartKnownServerAsync` 成功后，调用 `_serverDetector.RegisterStartedServerPid(knownServer.Id, knownServer.ServerJarPath, process.Id)` 写入启动时 PID 缓存；② `StartCurrentServerAsync` 相同处理 |
| **`ServerInstance.cs`** | 可能小改 | 增加 `KnownServerId?` 可选字段，用于标记此运行实例对应哪个已知服务器，供前端 `isKnown` 判断 |

### 3.2 桥接层（可选增强）

| 文件 | 改动类型 | 说明 |
|------|---------|------|
| **`MainWindow.xaml.cs`** | `server:list` handler 小改 | 运行中服务器如果 `KnownServerId` 非空 → `isKnown = true`（原来硬编码 false） |

### 3.3 前端（可选同步增强）

| 文件 | 改动类型 | 说明 |
|------|---------|------|
| **`types/bridge.ts`** | `ServerInfo` 接口 | 新增 `knownServerId?: string` 字段 |
| **`DashboardPage.tsx`** | 渲染逻辑 | 运行中服务器 `isKnown === true` 时在列表中显示 ★ 图标或「已知」标签，视觉上与 known 列表条目呼应 |

---

## 四、详细实现步骤

### Step 1：提取共享检测工具
在 `ServerManagerService.cs`（或新建 `ServerDetectionHelper.cs` 静态类）中：
- 将 `IsJarFileLocked(string jarPath)` 提取为 `public static` 方法（访问已具备，只需改修饰符）
- 新增 `FindServerProcessByJarPath(string jarPath, string workingDirectory)`：
  - 枚举 `Process.GetProcessesByName("java")` 和 `("javaw")`
  - 优先用命令行匹配（WMI 或已有 ProcessScanner 的批量查询方法）
  - 降级用工作目录匹配（进程 WorkingDirectory == workingDirectory）
  - 返回匹配的 `Process` 或 null
- （或直接在 ServerDetector 里写一份等价实现，避免跨类依赖爆炸）

### Step 2：在 ServerDetector 中注入 IAppConfigService
- 构造函数新增参数 `IAppConfigService appConfigService`
- 私有字段 `_appConfigService`
- `IServiceCollection` 注册处无需改动（DI 自动解析）

### Step 3：新增启动时 PID 缓存
- 在 ServerDetector 中：
  - `private readonly Dictionary<string, (int Pid, long ExpireTick)> _startSessionPidCache`
  - key = `jarPath`（大小写不敏感），同时维护一份 `knownServerId → pid`
  - `RegisterStartedServerPid(string knownServerId, string jarPath, int pid)`：写入缓存，TTL = 30s
  - 在 `DetectAllAsync` 中检查缓存：若 pid 仍存活 → 直接补入 servers 并写正常缓存
  - 每次调用 Register 时清理过期条目（TickCount64 比较）

### Step 4：新增 DetectKnownServersByJarLockAsync 方法
```
输入：List<ServerInstance> existingServers（已识别的服务器，用于去重）
输出：List<ServerInstance> 新增识别项
逻辑：
  1. var knownServers = _appConfigService.GetAllKnownServers()
  2. var existingJarPaths = existingServers 中所有 ServerJarPath 的 HashSet（去重用）
  3. var existingPids = existingServers 中所有 ProcessId 的 HashSet（去重用）
  4. for each known in knownServers:
       a. 跳过：known.ServerJarPath 为空或文件不存在
       b. 跳过：known.ServerJarPath 已在 existingJarPaths 中（ProcessScanner 已识别）
       c. bool locked = IsJarFileLocked(known.ServerJarPath)
          不锁定 → continue
       d. Process? proc = FindServerProcessByJarPath(known.ServerJarPath, known.WorkingDirectory)
          没找到 → continue（可能被非 java 进程锁定，或权限不足）
       e. 跳过：proc.Id 已在 existingPids 中
       f. 跳过：proc.HasExited
       g. 构造 ServerInstance:
          - ProcessId = proc.Id
          - ServerJarPath = known.ServerJarPath
          - WorkingDirectory = known.WorkingDirectory
          - ServerJarName = Path.GetFileName(known.ServerJarPath)
          - ServerType = 重新调用 ClassifyByJarNameAndConfigFiles(jarName, workingDir)
            （若仍 Unknown → 兜底 Vanilla）
          - JavaPath = known.JavaPath
          - JvmArguments = known.JvmArguments
          - Port = known.Port（然后用 TCP 表验证）
          - MaxHeapMemoryBytes = known.MaxHeapMemoryBytes
          - InitialHeapMemoryBytes = known.InitialHeapMemoryBytes
          - DetectedAt = DateTime.Now
          - KnownServerId = known.Id（新增字段）
       h. 添加到结果列表，并写入 _detectionCache[pid]
```

### Step 5：将新方法插入 DetectAllAsync 管道
插入位置：**阶段一 ProcessScanner 之后，阶段二B逐进程检测之前**（即 L159 之前）。这样新添加的 pid 会被逐进程检测循环中的 `TryGetCachedServer` 命中，避免重复 BuildServerInstanceAsync。

```
原流程：
  阶段一 ProcessScanner
  if Count == 0: 端口扫描兜底 return  ← L129-L157 的提前 return 需要取消
  阶段二B 逐进程深度检测
  阶段四 端口扫描

调整后（关键：取消 processResults 为空时的提前 return，让阶段二A 有机会补入）：
  阶段一 ProcessScanner
  【新增】阶段二A DetectKnownServersByJarLockAsync → 合并到 servers
  if servers.Count == 0 && processResults.Count == 0:
       端口扫描兜底 return  ← 仍然保留此 early return 作为最后兜底
  阶段二B 逐进程深度检测（遍历 processResults）
  阶段四 端口扫描
```

注意 L129-L157 的 `if (processResults.Count == 0) { ... return; }` 需要修改为：
```
if (processResults.Count == 0)
{
   // 先跑阶段二A（KnownServer JAR 锁定检测），因为 ProcessScanner 结果为空
   // 如果阶段二A 有结果，就继续后续阶段；否则才走端口扫描 return
}
```

### Step 6：ViewModels 写入启动时 PID 缓存
- 在 `StartKnownServerAsync` 的 `if (process != null)` 成功分支（约 L1431 之后）：
  - 调用 `_serverDetector.RegisterStartedServerPid(server.Id, server.ServerJarPath, process.Id)`
- 在 `StartCurrentServerAsync` 成功分支（约 L877-L890）：
  - 如果启动的服务器有 KnownServerId（即从 known 列表点启动的）或有 ServerJarPath，也写入缓存
  - 如果是临时构造的服务器（从 running 列表启动的），至少写入 `jarPath → pid` 映射

### Step 7：ServerInstance 新增 KnownServerId 字段
- 在 `ServerInstance.cs` 中增加 `public string? KnownServerId { get; set; }`
- 构造/映射时填充

### Step 8（可选）：桥接层修正 isKnown 判定
- `MainWindow.xaml.cs` 的 `server:list` handler L830：
  原来：`isKnown = false`
  改为：`isKnown = s.KnownServerId != null`（或 _appConfigService.FindByJarPath(s.ServerJarPath) != null，二选一，推荐前者性能更好）

### Step 9（可选）：前端同步增强
- `types/bridge.ts` 的 `ServerInfo` 新增 `knownServerId?: string`
- `DashboardPage.tsx` 的 RunningServerItem 中，当 `server.isKnown === true` 时显示 ★ 图标和「已知」蓝色小标签

---

## 五、潜在依赖与注意事项

### 5.1 DI 依赖注入
- ServerDetector 目前构造函数参数列表：需要检查是否已有 IAppConfigService
- 如果没有，在构造函数加参数并在 `_serverDetector` 注册处（`AppServicesExtensions` 或 `App.xaml.cs`）确认 DI 自动解析（通常没问题，只要注册了 IAppConfigService）

### 5.2 IsJarFileLocked 的 TCO 与性能
- IsJarFileLocked 一次 ~5ms；50 台 KnownServer ~250ms；可接受
- 如果用户服务器很多（>100 台），可做并行 PLINQ，但本项目预期不会到这个规模
- **注意不要对不存在的文件调用 IsJarFileLocked**：会抛 FileNotFoundException，需要提前 `File.Exists` 判断

### 5.3 FindServerProcessByJarPath 的命令行读取权限
- 跨用户场景下，WMI 读取其他用户进程的 CommandLine 需要管理员权限
- 失败时，降级用「进程启动时间 + 工作目录匹配」（Process.StartInfo.WorkingDirectory 在 .NET 中可能拿不到，需要用 WMI Win32_Process 的 ExecutablePath 或用 NtQueryInformationProcess）
- **兜底策略**：即使 FindServerProcess 失败，JAR 锁定状态本身也值得记录一条日志并把该 JAR 标记为「Running (detected by lock only)」，PID 为 0 但状态为 Running。但本次实现优先不做此兜底，只在能拿到 PID 时补入。拿不到 PID 时走端口扫描兜底。

### 5.4 取消 early return 的回归风险
`DetectAllAsync` 原逻辑在 `processResults.Count == 0` 时提前 return，跳过了逐进程循环但也跳过了「阶段四端口扫描」。原代码其实在 early return 前调用了 DiscoverServersByPortScanAsync（L137），然后 return。调整后需确保：当 ProcessScanner 为空 **且** 阶段二A 也为空时，端口扫描仍然执行，行为与原逻辑一致。

### 5.5 ServerManagerService.FindServerProcess 访问修饰符
- 当前 `FindServerProcess(ServerInstance server)` 应为 `private` 方法
- 选项 1：将其改为 `internal` 或 `public`，并新增重载 `FindServerProcessByJarPath(string, string)`
- 选项 2（推荐，低耦合）：直接在 ServerDetector 内写一个等价的 JAR 路径 → PID 查找方法，复用 ProcessScanner 的 WMI 批量查询基础设施（避免重复轮子也避免权限问题）

推荐选项 2：因为 ProcessScanner 已经封装了「批量 WMI 查所有 Java 进程的 PID+CommandLine+ParentId」，可以直接用 `ProcessScanner.CollectJavaProcessIds` + `LoadAllProcessInfoBatch` 拿到所有 java 进程信息，然后在内存中用 `commandLine.Contains(jarFileName)` 或 `commandLine.Contains(jarPath)` 过滤出匹配的进程，**性能远高于重新枚举**。

---

## 六、风险处理

| 风险 | 等级 | 缓解策略 |
|------|------|----------|
| JAR 文件被非 java 进程锁定（如压缩软件、杀毒软件） | 中 | FindServerProcessByJarPath 严格校验进程名 java/javaw，非 java 不补入；记 Debug 日志 |
| 多个 java 进程共享同一个 JAR（极端共享 classpath 场景） | 低 | 取所有命中进程中 CPU 占用最高的那个，或都补入（它们的 PID 不同不会重复），补入后由 BuildServerInstanceAsync 进一步去重 |
| 取消 early return 导致 DetectAllAsync 行为改变 | 中 | 单元级手动验证：① 无 MC 服务运行时返回空；② 端口扫描兜底仍工作；③ ProcessScanner 正常时结果一致 |
| IAppConfigService 在 ServerDetector 中注入失败 | 低 | DI 只要注册过（已有）就自动解析；编译期错误显式暴露，无运行时静默失败 |
| 启动时 PID 缓存 TTL 过短导致过期 | 低 | 设置 30 秒（远大于 WMI 索引延迟的 1.5~3s）；且有阶段二A JAR 锁定检测做兜底 |
| FileShare.None 导致正在运行的 JVM 读 JAR 冲突 | 无 | FileShare.None 是 **本进程尝试独占读取**，不影响 JVM 已持有的句柄；只是本进程拿不到 Read 权限时抛 IOException |

---

## 七、验证清单（成功标准）

1. **MSMC 内部启动 KnownServer**：启动后前端运行中列表立即出现该服务器，DisplayName 为 `Paper/Vanilla/... @ folder (PID: xxx)`（不是 Unknown/空目录）
2. **isKnown 标记**：运行中列表条目显示为 isKnown=true（可选增强后）
3. **与端口扫描兜底去重**：JAR 锁定检测到 pid 后，端口扫描不再为同一个 pid 创建重复的 Unknown 条目
4. **第三方启动**：非 MSMC 启动的 Minecraft 服务器，如果其 JAR 路径存在于 KnownServers 中，也能被关联（JAR 锁定机制不依赖启动时缓存）
5. **无回归**：ProcessScanner 能识别的服务器条目，JAR 锁定检测不会重复添加（去重用 HashSet）
6. **性能**：DetectAllAsync 总耗时相比之前增加 < 200ms（假设 <20 台 KnownServer）
