# 网络模块重构收尾计划（Task 8–11）

## 摘要

本计划是 [`network-module-overhaul.md`](file:///workspace/.trae/documents/network-module-overhaul.md) 的收尾部分。原计划 11 步中 **Task 1–7 已完成并落地**（PortToProcessMapper 修复、TcpForwarder 新建、NetshPortBridgeService 重命名修复、IPortBridgeService 接口改进、CompositePortBridgeService 外观、App.xaml.cs DI 注册、NetworkService 修复），剩余 **Task 8/9/10 + 编译验证** 待执行。

本文件聚焦这 4 项收尾工作，所有设计决策沿用原 plan，仅在执行细节上做最小化澄清。

---

## 剩余任务清单

| # | 文件 | 修复点 | 优先级 |
|---|---|---|---|
| 8 | NetworkTrafficService.cs | 网卡过滤 + Stopwatch + 原子写 + 缓存锁 + 基线重置 | P1 |
| 9 | PortScanner.cs + ServerConstants.cs + ServerDetector.cs | 范围扩展 + SemaphoreSlim Dispose + race 修复 | P2/P3 |
| 10 | NetworkMonitorViewModel.cs | RemoveBridge 传 Protocol | P1（接口契约对齐） |
| 11 | — | 编译验证（沙箱无 dotnet SDK，需用户本机执行） | — |

---

## 变更 A：NetworkTrafficService 修复（Task 8）

**文件**：[Services/Network/NetworkTrafficService.cs](file:///workspace/src/McServerGuard/Services/Network/NetworkTrafficService.cs)

### A1. 过滤虚拟网卡（GetActiveInterfaces，212-229 行）

当前只排除 `Loopback`，Hyper-V/WSL/Docker/VMware 虚拟网卡全部计入 → 流量虚高 5-10 倍。

**改动**：白名单（`NetworkInterfaceType`）+ 黑名单（名称关键词）双重过滤。

```csharp
private static readonly HashSet<NetworkInterfaceType> PhysicalInterfaceTypes = new()
{
    NetworkInterfaceType.Ethernet,
    NetworkInterfaceType.Wireless80211,
    NetworkInterfaceType.GigabitEthernet,
    NetworkInterfaceType.FastEthernetFx,
    NetworkInterfaceType.FastEthernetT
};

private static readonly string[] VirtualInterfaceNameMarkers =
    { "Hyper-V", "WSL", "Docker", "VMware", "VirtualBox", "vEthernet", "TAP", "Loopback Pseudo-Interface" };

private static bool IsPhysicalInterface(NetworkInterface ni)
{
    if (!PhysicalInterfaceTypes.Contains(ni.NetworkInterfaceType))
        return false;
    var name = ni.Name;
    foreach (var marker in VirtualInterfaceNameMarkers)
        if (name.Contains(marker, StringComparison.OrdinalIgnoreCase))
            return false;
    return true;
}
```

`GetActiveInterfaces` 的 Where 改为：`ni.OperationalStatus == Up && IsPhysicalInterface(ni)`。

### A2. Stopwatch 替代 DateTime（Sample，97-155 行）

当前用 `DateTime.Now` 计算 elapsed，系统时钟回拨/夏令时跳变会产生负 delta 或假峰值。

**改动**：
- 新增字段 `private readonly Stopwatch _stopwatch = Stopwatch.StartNew();`（构造函数里已在跑）
- 删除 `_lastSampleTime` 字段，`elapsed = _stopwatch.Elapsed.TotalSeconds`
- `_lastSaveTime` 同样改用 `_stopwatch.Elapsed`（`TimeSpan` 类型）或保留 DateTime（仅用于决定 60s 保存周期，时钟回拨影响小，**保留 DateTime 不动**以缩小改动面）
- 首次采样时 `_stopwatch` 已经在跑，无需 Restart

### A3. 原子写（Save，253-269 行）

当前 `File.WriteAllText` 非原子，写入中崩溃 → 30 天历史损坏。

**改动**：临时文件 + `File.Replace` 原子替换。

```csharp
public void Save()
{
    try
    {
        List<DailyTrafficRecord> snapshot;
        lock (_lock)
            snapshot = _history.ToList();

        var json = JsonSerializer.Serialize(snapshot, JsonOpts);
        var tmpPath = _dataFilePath + ".tmp";

        // 先写临时文件（utf-8 BOM-less）
        File.WriteAllText(tmpPath, json);

        // 原子替换：目标存在用 File.Replace，不存在用 File.Move
        if (File.Exists(_dataFilePath))
            File.Replace(tmpPath, _dataFilePath, destinationBackupFileName: null);
        else
            File.Move(tmpPath, _dataFilePath);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "保存流量数据失败");
    }
}
```

需 `using System.IO;`（已有）。

### A4. 缓存读写加锁（GetActiveInterfaces，212-229 行）

`_cachedInterfaces`/`_interfaceCacheTime` 当前无锁，自动刷新（DispatcherTimer 1s）与手动 RefreshCommand 并发时可能 InvalidOperationException。

**改动**：新增 `private readonly object _interfaceCacheLock = new();`，缓存读判断与写刷新都在 `lock(_interfaceCacheLock)` 内完成。

### A5. 网卡切换基线重置（Sample 内）

切 Wi-Fi / 网卡插拔时，`GetTotalBytes()` 返回的网卡集合变化，新基线与旧基线不可比 → 负 delta 吞流量或正 delta 假峰值。

**改动**：在 `Sample` 计算 delta 前，检测当前活跃网卡集合是否与上次不同（用网卡 Id 集合签名）。不同则重置基线、跳过本次 delta：

```csharp
// 在 GetTotalBytes 返回时同时返回参与统计的网卡 Id 签名
private (long sent, long received, string signature) GetTotalBytes()
{
    // ... 累加时构建 signature = string.Join("|", ids.OrderBy(...))
}

// Sample 内：
var (bytesSent, bytesReceived, signature) = GetTotalBytes();
if (_firstSample || signature != _lastSignature)
{
    _lastBytesSent = bytesSent;
    _lastBytesReceived = bytesReceived;
    _lastSignature = signature;
    _firstSample = false;
    return; // 跳过本次 delta
}
```

新增字段 `private string _lastSignature = string.Empty;`。

---

## 变更 B：PortScanner + ServerConstants + ServerDetector（Task 9）

### B1. ServerConstants 扩展扫描范围

**文件**：[Constants/ServerConstants.cs](file:///workspace/src/McServerGuard/Constants/ServerConstants.cs)（334-339 行）

**改动**：
- `PortScanStart = 25565` 不变，`PortScanEnd` 从 `25575` 扩展到 `25590`（覆盖 BungeeCord/Velocity 原生端口段）
- 新增 `AdditionalScanPorts` 数组，覆盖 Bedrock/Geyser TCP（19132）+ 自定义高位常用端口：

```csharp
public const int PortScanEnd = 25590;

/// <summary>
/// 主区间外的补充扫描端口（Bedrock/Geyser TCP、自定义高位常用端口）。
/// 由 ServerDetector 合并到主区间后统一扫描，避免改 PortScanner.ScanRangeAsync 签名。
/// </summary>
public static readonly int[] AdditionalScanPorts = { 19132, 26000, 30000, 40000 };
```

**不新增 `BedrockPort` 常量**：Bedrock 是 UDP，PortScanner 是 TCP 探测器，19132 仅作为 Geyser（Java 装 Geyser 桥接基岩版时开的 TCP 端口）候选，无需单独命名常量，直接放数组即可（避免过度工程化）。

### B2. PortScanner 新增多端口扫描重载 + Dispose + race 修复

**文件**：[Services/ServerDetection/PortScanner.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/PortScanner.cs)

**改动 1：新增 `ScanPortsAsync` 重载**（不改原 `ScanRangeAsync` 签名，保留向后兼容）：

```csharp
/// <summary>
/// 扫描任意端口集合，返回所有开放端口（升序）。
/// </summary>
public async Task<List<int>> ScanPortsAsync(IReadOnlyCollection<int> ports)
{
    // 复用 ScanRangeAsync 的并发+SemaphoreSlim 逻辑，但遍历传入的 ports
}
```

实现与 `ScanRangeAsync` 几乎一致，仅把 `for (port = startPort; port <= endPort; port++)` 换成 `foreach (var port in ports)`。可考虑让 `ScanRangeAsync` 内部生成区间列表后委托给 `ScanPortsAsync`，消除重复（推荐，单一数据路径）。

**改动 2：SemaphoreSlim Dispose**（105 行）：

`ScanRangeAsync`/`ScanPortsAsync` 内的 `var semaphore = new SemaphoreSlim(...)` 改为 `using var semaphore = new SemaphoreSlim(...)`。async 方法中 `using var` 在方法结束时 Dispose，此时 `await Task.WhenAll(tasks)` 已完成，所有 Release 都已发生，Dispose 安全。

**改动 3：race 修复**（63-68 行）：

超时分支直接 return false 会漏报——`Task.WhenAny` 返回 timeoutTask 时，connectTask 可能也已刚好完成。先检查：

```csharp
if (completed == timeoutTask)
{
    // race 修复：timeoutTask 先到，但 connectTask 可能也刚好完成
    if (connectTask.IsCompletedSuccessfully)
    {
        Log.Debug("✅ 端口 {Port} 开放（与超时同时完成）", port);
        return true;
    }
    _ = connectTask.ContinueWith(t => { var _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
    Log.Debug("⏱️ 端口 {Port} 探测超时", port);
    return false;
}
```

### B3. ServerDetector 改用多端口扫描入口

**文件**：[Services/ServerDetection/ServerDetector.cs](file:///workspace/src/McServerGuard/Services/ServerDetection/ServerDetector.cs)（375-376 行）

**改动**：`DiscoverByPortScanAsync` 中合并主区间 + AdditionalScanPorts，去重后调用新重载：

```csharp
var portsToScan = new HashSet<int>();
for (var p = ServerConstants.PortScanStart; p <= ServerConstants.PortScanEnd; p++)
    portsToScan.Add(p);
foreach (var p in ServerConstants.AdditionalScanPorts)
    portsToScan.Add(p);

var openPorts = await _portScanner.ScanPortsAsync(portsToScan);
```

注释 363-367 行的"扫描 25565-25575 端口范围"同步更新为"扫描主区间 + AdditionalScanPorts"。

---

## 变更 C：NetworkMonitorViewModel 适配（Task 10）

**文件**：[ViewModels/NetworkMonitorViewModel.cs](file:///workspace/src/McServerGuard/ViewModels/NetworkMonitorViewModel.cs)（539 行）

**改动**：`RemoveBridge` 调用 `RemoveBridgeRule` 时传入 `rule.Protocol`（接口已在 Task 4 加了 `string protocol = "v4tov4"` 参数，默认值仅对旧调用方兼容；UI 拿到的 rule 来自 `GetAllBridgeRules`，Protocol 字段已正确填充，必须显式传递才能删非 v4tov4 规则）。

```csharp
private async Task RemoveBridge(PortBridgeRule? rule)
{
    if (rule == null) return;

    var success = await Task.Run(() =>
        _portBridgeService.RemoveBridgeRule(rule.ListenAddress, rule.ListenPort, rule.Protocol));
    // ... 其余不变
}
```

**不注入 ITcpForwarder**：原 plan 变更 9 提到"可选，UI 显示连接数/字节量"。当前 UI 无此展示位，注入会增加未使用依赖。遵循"不过度工程化"原则，**本次不注入**，留待后续 UI 扩展时再加。

**AddBridge/RefreshPorts 无需改动**：CompositePortBridgeService 已实现 AddBridgeRule 自动走 TcpForwarder→netsh，GetAllBridgeRules 合并两个引擎列表，VM 现有逻辑直接可用。

---

## 假设与决策

1. **沿用原 plan 所有决策**：两者并存、全量修复、网卡白名单+黑名单、Kill 优雅停止、范围扩展用 AdditionalScanPorts 数组
2. **PortScanEnd 扩展到 25590**：覆盖 BungeeCord/Velocity 原生端口段，主区间仍是连续区间（不改 ScanRangeAsync 签名）
3. **新增 ScanPortsAsync 重载而非改 ScanRangeAsync 签名**：保留向后兼容，ServerDetector 是唯一需要多区间的调用方
4. **不注入 ITcpForwarder 到 VM**：UI 无展示位，避免未使用依赖
5. **NetworkTrafficService 的 _lastSaveTime 保留 DateTime**：仅用于 60s 保存周期判断，时钟回拨影响小，缩小改动面
6. **沙箱无 dotnet SDK**：编译验证需用户在 Windows 机器执行 `dotnet build src/McServerGuard/McServerGuard.csproj`

---

## 验证步骤

1. **编译验证**（用户本机）：`dotnet build src/McServerGuard/McServerGuard.csproj`，`TreatWarningsAsErrors=true` 下零警告
2. **流量统计验证**：带 WSL/Hyper-V 的机器 → 流量值接近物理网卡实际值；切 Wi-Fi 不出现假峰值；kill 进程后 `traffic.json` 不损坏
3. **端口扫描验证**：启动 BungeeCord（25577）→ 应被发现；Geyser（19132 TCP）→ 应被发现
4. **桥接删除验证**：添加 v6tov6 规则 → 删除应成功（RemoveBridge 传 rule.Protocol）
5. **回归验证**：LiveCharts2 图表、仪表盘动画、DispatcherTimer 刷新不受影响

---

## 执行顺序

1. 改 NetworkTrafficService（A1–A5，单文件）
2. 改 ServerConstants（B1，加 PortScanEnd=25590 + AdditionalScanPorts）
3. 改 PortScanner（B2，ScanPortsAsync 重载 + using semaphore + race 修复）
4. 改 ServerDetector（B3，DiscoverByPortScanAsync 合并端口集）
5. 改 NetworkMonitorViewModel（C，RemoveBridge 传 rule.Protocol）
6. 编译验证（用户本机）
