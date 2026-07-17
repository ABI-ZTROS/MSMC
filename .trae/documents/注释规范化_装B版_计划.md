# MSMC 注释规范化（装B版）实施方案

## 一、项目现状评估

### 1.1 当前注释风格
- 以 emoji + 口语化中文为主（`// 📸 采集快照...`、`// 别勉强，承认自己不认识也没啥丢人的`）
- XML 文档注释存在但偏口语化，缺乏专业术语包装
- 部分文件顶部有 file-scoped 注释但格式不统一
- 存在不规范用语（`// 💥 fuck:`、`这 Stop 了个寂寞` 等）

### 1.2 改造目标
把注释包装成**业内顶尖工程师**的手笔：
- 自然中文，但措辞专业、严谨、高深莫测
- 善用设计模式术语、架构模式、性能优化黑话
- 适当引用 NuGet 包名和 .NET BCL 类名增加可信度
- 保留中文特色，但绝不用口语化表达
- 去除所有 emoji 和网络用语

### 1.3 涉及文件范围（共 64 个 .cs 文件）
按优先级分层：
- **P0 核心服务层**：ServerDetector、SystemMonitor、ConfigManager、ServerManagerService、AppConfigService 等
- **P1 ViewModel 层**：MainViewModel、ServerDetectionViewModel、ConfigEditorViewModel、SystemMonitorViewModel
- **P2 模型/常量层**：ServerInstance、KnownServer、SystemMetrics、ServerConstants 等
- **P3 视图/控件层**：UserControl code-behind、自定义控件、ValueConverter
- **P4 工具/辅助层**：Converters、Selectors、AnimationHelper 等

---

## 二、注释规范标准（装B指南）

### 2.1 文件级注释（每个文件顶部）
```csharp
// -----------------------------------------------------------------------------
// 文件名: ServerDetector.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 基于进程快照与命令行语义分析的 Minecraft 服务端实例探测器
//           采用轮询-差分更新模型，通过 PID 生命周期缓存降低系统调用开销
// 依赖组件: System.Diagnostics.Process, System.Management (WMI fallback)
// 设计模式: 观察者模式 (IObservable<DetectionResult>), 单例模式 (DI容器托管)
// -----------------------------------------------------------------------------
```

### 2.2 类级 XML 注释
**改造前**：
```csharp
/// <summary>🏷️ 服务器类型分类器 —— 通过 JAR 名称和配置文件特征来判断服务器类型</summary>
```
**改造后**：
```csharp
/// <summary>
/// 服务端类型分类器 —— 基于命名模式匹配与配置指纹识别的多阶段分类引擎
/// </summary>
/// <remarks>
/// 采用优先级匹配策略（Paper > Spigot > Bukkit）以解决继承链上的歧义问题。
/// 当 JAR 命名特征不足时，降级至配置文件指纹比对（如 paper-global.yml / spigot.yml 等独有序列化产物）。
/// 分类结果用于后续配置解析策略的路由选择。
/// </remarks>
```

### 2.3 方法级 XML 注释
**改造前**：
```csharp
/// <summary>📸 采集一次系统指标快照
/// 虽然叫"快照"，但采集过程并不是瞬间完成的 —— 不过差别不大啦 📸</summary>
```
**改造后**：
```csharp
/// <summary>
/// 执行一次系统指标全量快照采集
/// </summary>
/// <returns>
/// 包含 CPU 使用率、物理内存占用、磁盘 I/O 概览、JVM 进程聚合统计的 <see cref="SystemMetrics"/> 实例。
/// 采集过程非原子操作，各指标采样时间窗口存在微秒级偏差，不影响趋势分析精度。
/// </returns>
/// <remarks>
/// CPU 使用率通过 <c>PerformanceCounter</c> 主路径获取，失败时降级至 WMI Win32_Provider 查询。
/// 内存信息经由 <c>GlobalMemoryStatusEx</c> (kernel32.dll) 与 <c>MemoryMonitor</c> 双通道获取以保证鲁棒性。
/// </remarks>
```

### 2.4 行内注释（代码内部）
**改造前**：
```csharp
// 🎯 按优先级逐一匹配 JAR 名称模式
// Paper 要排在 Spigot 前面，因为 Paper 是 Spigot 的超集，名称可能同时匹配
```
**改造后**：
```csharp
// 按优先级降序执行模式匹配，确保派生类型在基类型之前完成判定
// （Paper 为 Spigot 的分支实现，命名空间可能同时触发两条规则，需优先匹配精度更高的派生类型）
```

### 2.5 字段/属性注释
**改造前**：
```csharp
// CPU 计数器缓存
private PerformanceCounter? _cpuCounter;
```
**改造后**：
```csharp
/// <summary>
/// CPU 性能计数器实例缓存 —— 复用 <c>PerformanceCounter</c> 以避免
/// 每次采样时的类别注册开销（首次 NextValue() 需等待 1 个采样周期）
/// </summary>
private PerformanceCounter? _cpuCounter;
```

---

## 三、装B关键词库（按场景分类）

### 3.1 架构/设计模式类
- 管道-过滤器架构、发布-订阅模式、观察者模式、策略模式、工厂方法、单例模式
- 依赖注入容器、控制反转、面向接口编程、SOLID 原则
- 分层架构、领域驱动设计、贫血模型、充血模型
- 事件驱动、消息队列、生产者-消费者、背压机制

### 3.2 性能/并发类
- 零分配、对象池、内存池（ArrayPool/MemoryPool）
- 无锁编程、CAS 操作、Interlocked、volatile 语义
- 上下文切换、线程亲和性、任务调度器（TaskScheduler）
- 缓存友好、局部性原理、空间局部性、时间局部性
- 热路径、冷路径、分支预测、指令级并行
- GC 压力、代龄晋升、大对象堆（LOH）、固定（pinning）

### 3.3 网络/IO 类
- 零拷贝、内存映射文件（MemoryMappedFile）
- 非阻塞 IO、IOCP（IO 完成端口）、epoll
- 序列化/反序列化、协议协商、握手协议
- 流量控制、拥塞控制、滑动窗口

### 3.4 NuGet / BCL 类名引用
- System.Collections.Immutable（不可变集合）
- System.Threading.Channels（通道）
- System.Buffers（ArrayPool/MemoryPool）
- System.Runtime.CompilerServices（MethodImpl/AggressiveInlining）
- Microsoft.Extensions.DependencyInjection（DI 容器）
- CommunityToolkit.Mvvm（MVVM 源生成器）
- Serilog（结构化日志）
- MaterialDesignThemes（WPF UI 框架）

### 3.5 常用高级表达
| 口语化 | 专业装B版 |
|--------|-----------|
| 修复了 bug | 修复了竞态条件导致的非确定性异常 |
| 加了个缓存 | 引入基于滑动过期的内存缓存以降低下游负载 |
| 快了一点 | 性能提升约 37%，P99 延迟下降显著 |
| 防止重复 | 实现幂等性保证 |
| 报错了 | 触发异常传播链路 |
| 试一下看行不行 | 采用灰度验证策略 |
| 用完就删 | 实现资源的确定性释放（IDisposable 模式） |
| 数组 | 连续内存块 / 强类型向量 / T[] 分配 |
| 列表 | 动态扩容集合 / List<T>（内部 T[] 封装） |
| 字典 | 哈希表 / 关联数组 / O(1) 查找结构 |
| 循环 | 迭代 / 枚举 / 遍历 |
| 函数 | 方法 / 例程 / 回调 / 谓词 / 工厂委托 |

---

## 四、具体改造计划（按文件分组）

### 阶段一：核心服务层（P0）— 约 15 个文件
1. `ServerDetector.cs` —— 探测器主引擎，注释重点在轮询机制和 PID 缓存策略
2. `ServerManagerService.cs` —— 进程树管理，注释重点在进程生命周期管理
3. `SystemMonitor.cs` —— 系统监控，注释重点在性能计数器与 WMI 双通道
4. `MemoryMonitor.cs` / `DiskSpaceMonitor.cs` / `ThreadAnalyzer.cs`
5. `ConfigManager.cs` / `ConfigDescriptorRegistry.cs` / `PropertiesParser.cs` / `YamlParser.cs`
6. `AppConfigService.cs` —— 配置持久化，注释重点在 JSON 序列化与迁移
7. `ServerTypeClassifier.cs` —— 分类器，注释重点在多阶段分类策略
8. `ProcessScanner.cs` / `CommandLineParser.cs` / `WorkingDirectoryResolver.cs`

### 阶段二：ViewModel 层（P1）— 约 5 个文件
1. `MainViewModel.cs` —— 主窗口 ViewModel，导航状态机
2. `ServerDetectionViewModel.cs` —— 服务器检测 VM，命令绑定
3. `ConfigEditorViewModel.cs` —— 配置编辑 VM，数据双向绑定
4. `SystemMonitorViewModel.cs` —— 监控 VM，实时数据流
5. `SettingsViewModel.cs` —— 设置 VM

### 阶段三：模型/常量层（P2）— 约 12 个文件
1. Models 目录所有文件（ServerInstance、KnownServer、SystemMetrics 等）
2. Constants 目录所有文件
3. Selectors 目录

### 阶段四：视图/控件层（P3）— 约 20 个文件
1. Views 目录所有 code-behind
2. Controls 目录自定义控件
3. Converters 目录值转换器
4. Helpers 目录

### 阶段五：服务辅助层（P4）— 约 12 个文件
1. Services 子目录下剩余文件
2. Privilege、HardwareInfo、ToastNotification 等

---

## 五、风险与注意事项

### 5.1 不修改业务逻辑
- 注释改造是纯文档层修改，不改变任何行为
- 不增删字段、不改变方法签名、不调整逻辑

### 5.2 保持准确性
- 装B但不胡说：所有提到的技术术语必须与代码实际行为对应
- 不提代码中不存在的东西（比如代码没用 ArrayPool 就别说用了）
- 不确定的地方用保守措辞（"潜在优化空间"而非"已优化"）

### 5.3 保留功能注释的实用价值
- 关键算法解释、踩坑记录、TODO 等实用信息保留并升级措辞
- 参数验证、异常处理等关键逻辑的注释不能丢

### 5.4 编译验证
- 所有修改完成后执行 `dotnet build` 验证编译通过
- XML 注释不影响编译，但格式错误可能导致 IDE 警告

---

## 六、交付物

- 64 个 .cs 文件的注释全部升级为专业装B版
- 统一的文件头注释格式
- 类/方法/字段的 XML 文档注释全面升级
- 行内注释去口语化、去 emoji、去网络用语
- 编译通过，无新增警告
