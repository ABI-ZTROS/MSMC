# 统一时间标准改造计划

## 一、问题根因

当前项目时间系统存在以下问题：

1. **时区不统一**：混合使用 `DateTime.Now`（本地时间）和 `DateTime.UtcNow`（UTC 时间），且持久化数据读写时存在时区转换 bug（已部分修复）
2. **缺乏权威授时校验**：完全依赖系统本地时间，无 NTP 权威时间源校验，用户修改系统时间会导致数据错乱
3. **时间格式混乱**：有的用 `yyyy-MM-dd HH:mm:ss` 字符串，有的用 Unix 毫秒时间戳，有的用 ISO 8601 格式
4. **前端时间解析不可靠**：`new Date("2026-07-28 01:54:04")` 不带时区信息，不同浏览器解析行为不一致

## 二、设计原则

1. **全软件统一使用 UTC+8 北京时间**作为唯一时间标准（所有业务时间戳、持久化数据、UI 显示均用北京时间）
2. **启动时强制 NTP 授时校验**：从国家授时中心等权威服务器获取标准时间，计算本地时钟偏差
3. **所有时间通过统一时间服务获取**：禁止直接使用 `DateTime.Now` / `DateTime.UtcNow`
4. **持久化数据使用 Unix 毫秒时间戳（UTC+8 基准）**：避免时区转换误差
5. **前后端时间统一用 Unix 毫秒时间戳传递**：避免字符串解析歧义

## 三、新增模块

### 3.1 TimeService（统一时间服务）

**文件**：`Services/TimeService.cs`

**功能**：
- `Now` 属性：返回校正后的北京时间（DateTime，Kind=Unspecified，代表 UTC+8）
- `NowUnixMilliseconds` 属性：返回校正后的 Unix 毫秒时间戳（以 UTC+8 1970-01-01 00:00:00 为基准）
- `IsSynchronized` 属性：NTP 同步是否完成
- `ClockOffset` 属性：本地时钟与 NTP 标准时间的偏差（毫秒）
- `SynchronizeAsync(CancellationToken)`：执行 NTP 授时校验
- `ToBeijingTime(DateTime)`：将任意 DateTime 转换为北京时间
- `FromUnixTimeMilliseconds(long)`：将 Unix 毫秒时间戳转为北京时间 DateTime
- `ToUnixTimeMilliseconds(DateTime)`：将北京时间 DateTime 转为 Unix 毫秒时间戳

**NTP 服务器列表**（按优先级，全部是权威授时源）：
1. `ntp.ntsc.ac.cn` - 国家授时中心 NTP 服务器
2. `cn.ntp.org.cn` - 中国 NTP 快速授时服务
3. `ntp.aliyun.com` - 阿里云 NTP 服务器
4. `time.windows.com` - Windows 时间服务（兜底）

**授时策略**：
- 启动时异步执行 NTP 同步（超时 5 秒）
- 至少成功查询 2 个服务器取平均值，确保准确性
- 如果所有 NTP 服务器都不可达，降级使用系统本地时间，标记 `IsSynchronized = false`
- 每 1 小时后台重新同步一次
- 时钟偏差超过 5 秒时记录警告日志

**实现方式**：
- 使用 `System.Net.Sockets.UdpClient` 发送 NTP v4 协议包（RFC 5905）
- 不依赖第三方 NTP 库，原生实现（避免引入新依赖）

### 3.2 注册到 DI 容器

在 `App.xaml.cs` 的 `ConfigureServices` 中注册为单例：
```csharp
services.AddSingleton<TimeService>();
```

## 四、修改范围

### 4.1 后端 C# 代码

#### 4.1.1 系统监控模块（高优先级）

- **SystemMonitor.cs**：`DateTime.Now` → `_timeService.Now`
- **MetricsPersistenceService.cs**：
  - 写入：`new DateTimeOffset(timestamp).ToUnixTimeMilliseconds()` → `_timeService.ToUnixTimeMilliseconds(timestamp)`
  - 读取：`epoch.AddMilliseconds(timestampMs).ToLocalTime().DateTime` → `_timeService.FromUnixTimeMilliseconds(timestampMs)`
  - 文件切割日期、清理日期：`DateTime.Now` → `_timeService.Now`
- **MemoryMonitor.cs**：缓存 TTL 检查用 `_timeService.Now`

#### 4.1.2 网络监控模块（中优先级）

- **NetworkService.cs**：缓存 TTL 用 `_timeService.Now`
- **NetworkTrafficService.cs**：采样时间、缓存 TTL 用 `_timeService.Now`

#### 4.1.3 服务器检测模块（中优先级）

- **ServerDetector.cs**：检测缓存 TTL、端口扫描缓存 TTL、DetectedAt 用 `_timeService.Now`
- **ServerDetectionViewModel.cs**：AddedAt、LastSeenAt 用 `_timeService.Now`

#### 4.1.4 桥接 API 时间格式（高优先级）

**MainWindow.xaml.cs 中所有时间戳返回改为 Unix 毫秒时间戳**：

- `app:getTime`：返回 `{ time: number, synchronized: boolean }`
- `systemMonitor:getMetrics`：`timestamp` 字段从字符串改为 Unix 毫秒
- `systemMonitor:getHistory`：`timestamp` 字段从字符串改为 Unix 毫秒
- `systemMonitor:getHistoryRange`：`timestamp` 字段从字符串改为 Unix 毫秒

**桥接消息时间戳保持不变**（BridgeMessage 已经是 Unix 毫秒）

#### 4.1.5 其他模块（低优先级）

- **MainViewModel.cs**：状态栏时钟用 `_timeService.Now`
- **App.xaml.cs**：日志文件名、崩溃报告时间用 `_timeService.Now`
- **AppConfigService.cs**：LastSeenAt 更新用 `_timeService.Now`
- **UserAgreementService.cs**：AgreedAt 用 `_timeService.Now`
- **MemoryOptimizerService.cs**：回收间隔判断用 `_timeService.Now`
- **JarCoreIdentifier.cs**：缓存 TTL 用 `_timeService.Now`
- **ZipExtractResourceProvider.cs**：解压标记时间改为北京时间
- **KnownServer.cs / ServerInstance.cs / SystemMetrics.cs**：默认值 `DateTime.Now` 改为可注入（或在构造时赋值）

### 4.2 前端 TypeScript 代码

#### 4.2.1 时间工具函数

**新增文件**：`src/utils/time.ts`

```typescript
// 将 Unix 毫秒时间戳（UTC+8 基准）格式化为显示字符串
export function formatBeijingTime(tsMs: number, format: string = 'YYYY-MM-DD HH:mm:ss'): string

// 将 Unix 毫秒时间戳（UTC+8 基准）转为 Date 对象（本地时区，但值对应北京时间）
export function beijingTimeToDate(tsMs: number): Date

// 获取当前北京时间的 Unix 毫秒时间戳
export function getBeijingTimeNow(): number
```

**注意**：前端的 Date 对象是本地时区的，我们只把 Unix 时间戳当作"北京时间的毫秒数"来做格式化和计算，不做时区转换。

#### 4.2.2 系统监控页面

- **SystemMonitorPage.tsx**：所有 `new Date(item.timestamp)` 改为 `beijingTimeToDate(item.timestamp)`
- **DualLineChart.tsx**：时间格式化、tooltip 时间显示改为使用北京时间工具函数
- 历史数据 X 轴时间刻度统一用北京时间

#### 4.2.3 网络监控页面

- 流量统计时间、当前小时等使用北京时间

#### 4.2.4 其他页面

- 服务器列表的 AddedAt / LastSeenAt 显示
- 状态栏时钟

### 4.3 启动流程集成

在 `App.xaml.cs` 的启动流程中：

1. 主题初始化后，**立即初始化 TimeService**（轻量构造，不阻塞）
2. 后台异步执行 NTP 授时同步（不阻塞 UI 显示）
3. 启动窗口日志输出时间同步状态：
   - `⏰ 正在同步权威授时中心时间...`
   - `✅ 时间同步完成，偏差 ±XX 毫秒` 或 `⚠️ 时间同步失败，使用本地时间`
4. NTP 同步完成后通过事件通知前端更新时间显示

## 五、修改步骤

### 阶段一：核心时间服务（后端）
1. 新建 `TimeService.cs`，实现 NTP 协议和统一时间 API
2. 在 DI 容器中注册 TimeService
3. 编写单元测试验证时间转换正确性

### 阶段二：系统监控模块改造（最高优先级，解决当前 bug）
1. 修改 `MetricsPersistenceService` 读写逻辑使用 TimeService
2. 修改 `SystemMonitor` 采集时间用 TimeService
3. 修改桥接 API 返回 Unix 毫秒时间戳
4. 前端 DualLineChart 和 SystemMonitorPage 适配新时间格式

### 阶段三：网络监控 + 服务器检测模块
1. NetworkService / NetworkTrafficService 改造
2. ServerDetector / ServerDetectionViewModel 改造

### 阶段四：其他模块 + 前端全量适配
1. 剩余模块改造
2. 前端时间工具函数 + 全页面适配
3. 启动流程集成 NTP 同步状态显示

## 六、风险与注意事项

1. **NTP 网络失败**：必须有降级方案，不能因网络不通导致软件无法启动
2. **时钟回拨**：NTP 同步后如果发现时钟偏差很大（>1分钟），不立即调整已采集的数据，只对新数据生效
3. **历史数据兼容性**：已有的 .msmcd 文件中的时间戳是按旧逻辑写入的（本地时间→Unix转换有bug），需要提供数据迁移工具或自动修复逻辑
4. **缓存 TTL 准确性**：NTP 同步导致的时钟跳变可能影响缓存过期判断，缓存 TTL 建议改用 Stopwatch 测量（仅业务时间戳用 TimeService）
5. **前端 Date 对象陷阱**：JavaScript 的 `Date` 总是本地时区的，必须明确区分"时间戳数值"和"显示字符串"，不要混用

## 七、验证方式

1. **单元测试**：验证 NTP 包解析、时间转换、时区计算正确性
2. **手动测试**：
   - 修改系统时间，确认软件显示的是正确的北京时间（NTP 同步后）
   - 断开网络，确认软件降级使用本地时间且正常运行
   - 对比系统监控图表时间和实际北京时间是否一致
3. **持久化验证**：
   - 采集数据后重启软件，确认历史数据时间正确
   - 跨天切割逻辑验证（23:59 → 00:00 切换）
4. **边界测试**：
   - 夏令时切换（中国不使用夏令时，风险较低）
   - 闰年、闰秒
