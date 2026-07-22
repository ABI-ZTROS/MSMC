# 网络模块全量重构计划

## 摘要

调研发现网络模块"几乎无法使用"的根因是 **2 个 P0 致命缺陷 + 8 个 P1/P2 稳定性问题**，不是缺第三方包：

1. **P0**：[`PortToProcessMapper.ntohs`](file:///workspace/src/McServerGuard/Services/ServerDetection/PortToProcessMapper.cs#L131) 是恒等函数（注释说交换字节，实际未交换）→ 所有端口号字节序错乱（25565 显示成 40291）
2. **P0**：[`PortBridgeService.GetAllBridgeRules`](file:///workspace/src/McServerGuard/Services/Network/PortBridgeService.cs#L241) 格式假设错（注释说 `地址:端口` 合列，netsh 实际分列输出）→ 规则列表恒空

第三方包调研结论：**桥接无现成可替代包**（NetConduit/Smart.Ports/Core.TCP 是通信框架非转发器；UDP_Relay_Core 只做 UDP；EgressPool 需 .NET 10）。最稳方案是 .NET 原生 `TcpListener`+`TcpClient` 用户态转发（~80 行），无 locale/iphlpsvc/PATH 依赖。端口映射继续用 Vanara.PInvoke（已引入），只需修 bug + 补 IPv6/UDP。

**用户决策**：桥接**两者并存**（TcpForwarder 默认 + netsh 兜底）+ **全量修复**（P0+P1+P2）。

---

## 现状分析（基于 Phase 1 探索）

### 缺陷清单与优先级

| 优先级 | 模块 | 问题 | 影响 |
|---|---|---|---|
| **P0 致命** | PortToProcessMapper | `ntohs` 恒等函数，端口字节序全错 | 端口列表显示乱码（25565→40291），Kill 端口永远失败 |
| **P0 致命** | PortBridgeService | `GetAllBridgeRules` 格式假设错 | 规则列表恒空，加了规则看不见，重复添加报"对象已存在" |
| P1 严重 | PortToProcessMapper | 仅 IPv4、仅 TCP、仅 LISTENER | IPv6 双栈服务器、UDP（Bedrock 19132）不可见 |
| P1 严重 | NetworkTrafficService | 不过滤虚拟网卡（Hyper-V/WSL/Docker/VMware） | 流量虚高 5-10 倍 |
| P1 严重 | NetworkTrafficService | 网卡切换/缓存刷新致基线跳变 | 切 Wi-Fi 时流量被吞，缓存刷新时假峰值污染历史 |
| P1 严重 | PortBridgeService | `RemoveBridgeRule` 硬编码 `v4tov4` | v6tov6/v4tov6/v6tov4 规则删不掉 |
| P2 中 | NetworkService | PID 缓存非线程安全 | 自动+手动刷新并发时可能 InvalidOperationException |
| P2 中 | NetworkService | `GetUsedPercentage` 分母 65535 | 占用率恒为 0% |
| P2 中 | PortScanner | 范围仅 25565-25575 | 漏检 Bedrock(19132)/BungeeCord(25577)/自定义高位端口 |
| P2 中 | NetworkTrafficService | `Save` 非原子写 | 写入中崩溃 → 30 天历史丢失 |
| P2 中 | PortBridgeService | `sc start` 后立即查询的 START_PENDING 竞态 | 服务实际能起来但被判失败 |
| P3 低 | PortScanner | `SemaphoreSlim` 未 Dispose | 频繁扫描轻量资源泄漏 |
| P3 低 | IPortBridgeService | 混入防火墙职责、缺 Protocol 参数、全同步 | 接口设计问题 |
| P3 低 | PortBridgeService | `EnableFirewallRule` 硬编码 TCP | 无法为 UDP 放行 |

### 第三方包可行性

| 模块 | 候选包 | 结论 |
|---|---|---|
| 桥接 | NetConduit / Smart.Ports / Core.TCP / Nager.TcpClient | 均为通信框架非端口转发器，不可用 |
| 桥接 | UDP_Relay_Core | 仅 UDP，不覆盖 TCP |
| 桥接 | EgressPool | 需 .NET 10，项目当前 net9.0 |
| 端口映射 | Vanara.PInvoke.IpHlpApi | **已使用**，是最佳方案，修 bug 即可 |
| 流量 | Oakrey.Network.Tools | 需 .NET 10，原生 `GetIPv4Statistics()` 已够用 |

**结论**：不引入新包，全部用 .NET 原生 + 已有的 Vanara 修复。

---

## 提议变更

### 变更 1：修复 PortToProcessMapper ntohs + 补 IPv6/UDP

**文件**：[Services/ServerDetection/PortToProcessMapper.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/PortToProcessMapper.cs)

**改动**：
1. **修 ntohs**（131-137 行）：`(highByte << 8) | lowByte` → `(lowByte << 8) | highByte`
2. **补 IPv6**：新增 `AF_INET6 = 23` 常量，第二次调用 `GetExtendedTcpTable` 传 IPv6 + `MIB_TCP6ROW_OWNER_PID` 结构体解析
3. **补 UDP**：新增 `GetListeningUdpPortToPidMap()` 方法，调用 `GetExtendedUdpTable`（Vanara 已提供）+ `MIB_UDPROW_OWNER_PID`
4. **统一入口**：`GetListeningPortToPidMap()` 改名 `GetTcpListeningPortToPidMap()`，新增 `GetAllListeningPortToPidMap()` 合并 TCP(v4+v6)+UDP(v4+v6)
5. **同端口多 PID**：返回 `Dictionary<int, List<int>>` 而非 `Dictionary<int, int>`，保留 SO_REUSEPORT 场景的所有进程

**为什么**：ntohs 修复后端口号正确，Kill 端口能成功；IPv6/UDP 补全后 Bedrock(19132) 和双栈服务器可见。

### 变更 2：新增 TcpForwarderService（用户态 TCP 转发引擎）

**新文件**：[Services/Network/TcpForwarderService.cs](file:///workspace/src/McServerGuard/Services/Network/TcpForwarderService.cs)（~120 行）

**接口**：[Services/Network/ITcpForwarder.cs](file:///workspace/src/McServerGuard/Services/Network/ITcpForwarder.cs)（新文件）

```csharp
public interface ITcpForwarder
{
    /// <summary>启动一条 TCP 转发规则。监听端口被占用或监听失败返回 false，LastError 含原因。</summary>
    bool AddForward(PortBridgeRule rule);

    /// <summary>停止转发规则。规则不存在返回 false。</summary>
    bool RemoveForward(string listenAddress, int listenPort, string protocol);

    /// <summary>当前活跃的转发规则列表（基于内部字典，无 netsh 解析依赖）。</summary>
    List<PortBridgeRule> GetActiveForwards();

    /// <summary>每条规则的实时统计：连接数、累计转发字节。</summary>
    IReadOnlyDictionary<(string, int), ForwardStats> GetForwardStats();

    string LastError { get; }
}

public record ForwardStats(int ActiveConnections, long TotalBytesRelayed);
```

**实现要点**：
- 内部 `ConcurrentDictionary<(string ListenAddr, int ListenPort, string Protocol), ForwardSession>` 维护活跃规则
- `ForwardSession` 持有 `TcpListener` + `CancellationTokenSource` + 统计计数器
- `AddForward`：`new TcpListener(IPAddress.Parse(addr), port)` → `Start()` → `AcceptTcpClientAsync` 循环（后台 Task）
- 每个客户端：`TcpClient remote = new(); await remote.ConnectAsync(connectAddr, connectPort);` → 双向 `CopyToAsync`（client→remote + remote→client，两个 Task 并发，任一完成取消另一个）
- `RemoveForward`：取消 CTS、Stop listener、Dispose 所有活动连接
- `GetActiveForwards`：直接读 ConcurrentDictionary 的 Key，无需解析任何外部输出
- 协议字段：`v4tov4`/`v6tov6` 决定 `IPAddress.Parse` 后用 `AddressFamily.InterNetwork`/`InterNetworkV6` 校验

**为什么**：用户态转发完全可控、可观测、无 locale/iphlpsvc 依赖、规则增删即启停、能记录转发字节量。本机端口搬移场景吞吐完全够用。

### 变更 3：PortBridgeService 修复 + 降级为兜底

**文件**：[Services/Network/PortBridgeService.cs](file:///workspace/src/McServerGuard/Services/Network/PortBridgeService.cs)

**改动**：
1. **修 GetAllBridgeRules 解析**（241-272 行）：netsh `portproxy show all` 实际输出格式：
   ```
   Protocol  Address         Port    Address         Port
   v4tov4    127.0.0.1       25565   127.0.0.1       25566
   ```
   改为：表头后每行按空格切分 5 列（Protocol/ListenAddr/ListenPort/ConnectAddr/ConnectPort），不再假设 `地址:端口` 合列。IPv6 地址（`::1`）单列无 `:`，不再触发 Split 崩溃。
2. **修 RemoveBridgeRule**（167-198 行）：签名加 `string protocol = "v4tov4"` 参数，用传入协议而非硬编码
3. **修 IP Helper 检查**（102-143 行）：`sc query/start` 替换为 `System.ServiceProcess.ServiceController`：
   ```csharp
   using var sc = new ServiceController("iphlpsvc");
   if (sc.Status != ServiceControllerStatus.Running)
   {
       sc.Start();
       sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
   }
   ```
   需在 csproj 加 `<PackageReference Include="System.ServiceProcess.ServiceController" Version="9.*" />`
4. **LastError 线程安全**：用 `lock` 保护 `LastError` 字段读写
5. **EnableFirewallRule 协议参数化**：签名加 `string protocol = "TCP"` 参数
6. **catch 块不再吞异常**：`EnsureIpHelperServiceRunning` 的 catch 不再返回 true 放行，改为记录真实错误

**为什么**：即便 TcpForwarder 是默认引擎，netsh 兜底仍需正确工作（用户可能想用内核态转发获取更高吞吐）。修解析后规则列表能正确显示，修 RemoveBridgeRule 后能删非 v4tov4 规则。

### 变更 4：IPortBridgeService 接口改进

**文件**：[Services/Network/IPortBridgeService.cs](file:///workspace/src/McServerGuard/Services/Network/IPortBridgeService.cs)

**改动**：
- `RemoveBridgeRule` 签名加 `string protocol = "v4tov4"` 参数（默认值保证向后兼容）
- `EnableFirewallRule` 签名加 `string protocol = "TCP"` 参数
- 保留 `LastError`（netsh 兜底仍需暴露错误）

**不拆分 IFirewallService**：拆分会让 VM 注入更多依赖，且当前防火墙仅在 netsh 兜底路径下使用，保留在接口内更简单（遵循"不过度工程化"原则）。

### 变更 5：CompositePortBridgeService（两者并存外观）

**新文件**：[Services/Network/CompositePortBridgeService.cs](file:///workspace/src/McServerGuard/Services/Network/CompositePortBridgeService.cs)（~80 行）

**职责**：实现 `IPortBridgeService`，内部持有 `ITcpForwarder`（默认）+ `IPortBridgeService`（netsh 实现，命名 `NetshPortBridgeService`）。

**策略**：
- `AddBridgeRule`：先调 `TcpForwarder.AddForward`
  - 成功 → 返回 true
  - 失败（监听端口被占用/地址无效）→ 降级调 `NetshPortBridgeService.AddBridgeRule`
  - 两者都失败 → `LastError` 合并两个引擎的错误
- `RemoveBridgeRule`：先停 TcpForwarder，再删 netsh 规则（两者都尝试，幂等）
- `GetAllBridgeRules`：合并 `TcpForwarder.GetActiveForwards()` + `NetshPortBridgeService.GetAllBridgeRules()`，去重
- `BridgeRuleExists`：基于合并后的列表判断
- `EnableFirewallRule`/`DisableFirewallRule`：直接委托 netsh 实现（用户态转发不需要防火墙规则，但用户勾选时仍可添加）

**重命名**：现有 `PortBridgeService` → `NetshPortBridgeService`（明确职责）

**DI 注册**（[App.xaml.cs](file:///workspace/src/McServerGuard/App.xaml.cs)）：
```csharp
services.AddSingleton<ITcpForwarder, TcpForwarderService>();
services.AddSingleton<NetshPortBridgeService>();  // 具体类，非接口
services.AddSingleton<IPortBridgeService, CompositePortBridgeService>();
```

### 变更 6：NetworkService 修复

**文件**：[Services/Network/NetworkService.cs](file:///workspace/src/McServerGuard/Services/Network/NetworkService.cs)

**改动**：
1. **PID 缓存加锁**（67-103 行）：`_pidNameCache`/`_pidNameCacheTime` 读写用 `lock(_pidCacheLock)` 保护
2. **GetUsedPercentage 分母改**（143-147 行）：分母从 65535 改为 `49151`（注册端口段上限），或直接返回 `UsedPorts` 计数（移除百分比，避免误导）。**决策**：分母改为 49151（系统+注册端口段），动态端口段不计入"占用"
3. **支持 UDP 端口**（25-60 行）：`GetAllListeningPorts` 同时查 TCP 和 UDP，`Protocol` 字段正确标记 "TCP"/"UDP"
4. **KillProcessByPort 优雅停止**（111-130 行）：保留 `Kill()` 但先尝试 `CloseMainWindow()` + 等 3 秒，超时再强杀

### 变更 7：NetworkTrafficService 修复

**文件**：[Services/Network/NetworkTrafficService.cs](file:///workspace/src/McServerGuard/Services/Network/NetworkTrafficService.cs)

**改动**：
1. **过滤虚拟网卡**（212-229 行）：`GetActiveInterfaces` 增加 `NetworkInterfaceType` 白名单过滤：
   ```csharp
   var physicalTypes = new[] {
       NetworkInterfaceType.Ethernet,
       NetworkInterfaceType.Wireless80211,
       NetworkInterfaceType.GigabitEthernet
   };
   ```
   排除 `NetworkInterfaceType.Tunnel`/`Loopback`/`Unknown` 及名称含 "Hyper-V"/"WSL"/"Docker"/"VMware"/"VirtualBox"/"vEthernet"/"TAP" 的网卡
2. **Stopwatch 替代 DateTime**（97-155 行）：`_stopwatch = Stopwatch.StartNew()`，`elapsed = _stopwatch.Elapsed.TotalSeconds`，避免系统时钟回拨
3. **原子写**（253-269 行）：写临时文件 `traffic.json.tmp` → `File.Replace`/`File.Move` 原子替换
4. **缓存读写加锁**：`_cachedInterfaces`/`_interfaceCacheTime` 用 `lock` 保护
5. **网卡切换基线重置**：检测到网卡集合变化时（数量或名称不同）重置 `_lastBytesSent/Received` 为当前汇总值，跳过本次 delta 计算（避免负 delta 吞流量或正 delta 假峰值）

### 变更 8：PortScanner 扩展范围 + 资源修复

**文件**：[Services/ServerDetection/PortScanner.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/PortScanner.cs) + [Constants/ServerConstants.cs](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs)

**改动**：
1. **扩展扫描范围**（ServerConstants 334-339 行）：
   - 新增 `BedrockPort = 19132`
   - `PortScanStart` / `PortScanEnd` 改为扫描多个区间：`25565-25575` + `25577-25590`（BungeeCord/Velocity 常用）+ `30000-30010`（自定义高位）
   - 新增 `AdditionalScanPorts = new[] { 19132, 25577, 26000, 30000, 40000 }`
2. **SemaphoreSlim Dispose**（105 行）：`using var semaphore = new SemaphoreSlim(...)` 或类级别字段 + Dispose
3. **修复 race**（63-65 行）：超时分支先检查 `connectTask.IsCompletedSuccessfully`，若已完成则按成功处理（避免漏报）

### 变更 9：NetworkMonitorViewModel 适配

**文件**：[ViewModels/NetworkMonitorViewModel.cs](file:///workspace/src/McServerGuard/ViewModels/NetworkMonitorViewModel.cs)

**改动**：
- 注入 `ITcpForwarder` 用于显示转发统计（可选，UI 显示连接数/字节量）
- `AddBridge` 调用 `CompositePortBridgeService.AddBridgeRule`（外观自动决定走 TcpForwarder 还是 netsh）
- `RefreshPorts` 中 `BridgeRules` 来源现在是合并后的列表，无需改 VM 逻辑
- `RemoveBridge` 传入 `rule.Protocol` 给新的 `RemoveBridgeRule` 签名

---

## 假设与决策

1. **不引入新第三方包**：调研确认无合适包，全部用 .NET 原生 + 已有 Vanara
2. **桥接两者并存策略**：TcpForwarder 为默认引擎（用户态），netsh 为兜底（内核态）。AddBridge 时先 TcpForwarder，失败再 netsh
3. **PortBridgeService 重命名为 NetshPortBridgeService**：明确职责，避免与 CompositePortBridgeService 混淆
4. **GetUsedPercentage 分母改为 49151**：系统端口(0-1023)+注册端口(1024-49151)，动态端口段不计入"可占用"
5. **网卡过滤用白名单 + 名称黑名单**：白名单按 `NetworkInterfaceType`，黑名单按名称关键词（应对 Hyper-V vEthernet 类型为 Ethernet 的特殊情况）
6. **KillProcessByPort 保留强杀兜底**：先 `CloseMainWindow` + 3s 等待，超时再 `Kill()`。Java 服务器收到 `CloseMainWindow` 在 Windows 下相当于 Ctrl+C，能触发 shutdown hook
7. **扫描范围扩展为多区间**：用 `AdditionalScanPorts` 数组 + 主区间，避免改 PortScanner 的 ScanRangeAsync 签名
8. **System.ServiceProcess.ServiceController 需新增 PackageReference**：`System.ServiceProcess.ServiceController` 是独立包（非 BCL 内置）

---

## 验证步骤

1. **P0 验证**：
   - 启动本地 MC 服务器（监听 25565），打开应用 → 端口列表应显示 25565（而非 40291）
   - 选中 25565 → 点"结束进程" → 服务器应被关闭
2. **桥接验证**：
   - 添加桥接 `127.0.0.1:25565 → 127.0.0.1:25566` → 规则列表应立即显示（TcpForwarder 路径）
   - 用 `netstat -an | findstr 25565` 确认监听存在
   - 客户端连 25565 应转发到 25566
   - 删除规则 → 监听消失，连接断开
   - 关闭 TcpForwarder（模拟失败）→ 添加规则应降级到 netsh，规则列表仍能显示（修复后的解析）
3. **IPv6/UDP 验证**：
   - 启动 Bedrock 服务器（19132 UDP）→ 端口列表应显示 19132/UDP
   - 双栈绑定的服务器（`::`）→ 应在 IPv6 列表中显示
4. **流量统计验证**：
   - 带 WSL/Hyper-V 的机器 → 流量值应接近物理网卡实际值（不再虚高 5-10 倍）
   - 切换 Wi-Fi → 不应出现假峰值或流量吞掉
   - 强制 kill 进程 → `traffic.json` 不应损坏
5. **编译验证**：`dotnet build src/McServerGuard/McServerGuard.csproj`（`TreatWarningsAsErrors=true` 下零警告）
6. **回归验证**：图表控件（LiveCharts2）、仪表盘动画、线程模型不受影响

---

## 执行顺序

1. **修 PortToProcessMapper**（P0 ntohs + IPv6 + UDP）—— 单文件改动，影响面最大
2. **新增 ITcpForwarder + TcpForwarderService**—— 新文件，不影响现有代码
3. **修 PortBridgeService → 重命名 NetshPortBridgeService**（解析 + RemoveBridgeRule 协议 + ServiceController + LastError 锁）
4. **改 IPortBridgeService 接口**（RemoveBridgeRule/EnableFirewallRule 加参数）
5. **新增 CompositePortBridgeService**—— 外观整合两者
6. **改 App.xaml.cs DI 注册**
7. **修 NetworkService**（PID 锁 + 占用率分母 + UDP + Kill 优雅停止）
8. **修 NetworkTrafficService**（网卡过滤 + Stopwatch + 原子写 + 锁 + 基线重置）
9. **修 PortScanner + ServerConstants**（范围扩展 + SemaphoreSlim Dispose + race 修复）
10. **改 NetworkMonitorViewModel**（注入适配 + RemoveBridge 传 Protocol）
11. **编译验证**
