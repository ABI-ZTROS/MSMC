# MLP神经网络优化与系统增强实现计划

## 一、现状分析

### 1.1 当前架构

项目当前AI服务模块采用**规则引擎+模式匹配**架构，主要组成：

| 模块 | 位置 | 功能 | 当前实现方式 |
|------|------|------|-------------|
| 日志异常检测 | `LogAnomalyDetector.cs` | 日志分类与异常识别 | 关键词+正则匹配 |
| 自学习引擎 | `AiGuardOrchestrator.cs` | 模式发现与权重调整 | 聚类+频率统计 |
| 崩溃预测 | `CrashPredictor.cs` | 系统崩溃风险评估 | 滑动窗口阈值统计 |
| 配置优化 | `ConfigOptimizer.cs` | 服务器配置建议 | 硬编码规则 |
| 系统监控 | `SystemMonitor.cs` | CPU/内存/磁盘/线程采集 | PerformanceCounter + P/Invoke |

### 1.2 存在的问题

1. **无真正的MLP神经网络**：当前"AI"全是规则引擎，没有神经网络推理
2. **硬件识别不足**：只有CPU核心数和总内存大小，缺少CPU型号、内存频率等关键信息
3. **优化建议依据不透明**：虽然有Reason字段，但缺乏量化的判断依据和置信度
4. **无自纠正机制**：自学习只能发现新模式，不能自动纠正错误判断
5. **无主动内存压缩**：只有监控，没有干预能力
6. **无进程清理功能**：只有进程检测，没有进程管理能力

## 二、总体设计

### 2.1 技术选型

| 功能模块 | 技术方案 | 理由 |
|---------|---------|------|
| MLP神经网络 | 纯C#手写（无外部依赖） | 轻量级、可移植、无需引入ML.NET重依赖 |
| CPU识别 | WMI Win32_Processor + 本地CPU数据库 | 覆盖Intel/AMD全系列，离线可用 |
| 内存识别 | WMI Win32_PhysicalMemory | 获取容量、频率、时序等信息 |
| 内存压缩 | Win32 API EmptyWorkingSet + SetProcessWorkingSetSize | Windows原生支持，无第三方依赖 |
| 进程管理 | System.Diagnostics.Process + WMI | 进程枚举、分类、安全终止 |

### 2.2 架构图

```
┌─────────────────────────────────────────────────────────────┐
│                      AI 服务层                                │
├──────────────┬──────────────┬──────────────────────────────┤
│  MLP 网络    │  规则引擎    │  自学习 / 自纠正              │
│  (新增)      │  (现有)      │  (增强)                       │
└──────────────┴──────────────┴──────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   系统信息采集层                              │
├──────────┬──────────┬────────────┬──────────┬──────────────┤
│ CPU识别  │ 内存识别 │ 进程管理   │ 性能监控 │ 内存压缩     │
│ (新增)   │ (增强)   │ (新增)     │ (现有)   │ (新增)       │
└──────────┴──────────┴────────────┴──────────┴──────────────┘
```

## 三、详细实现计划

### 模块一：MLP神经网络实现

#### 3.1.1 核心功能

- **输入层**：12个特征（CPU使用率、内存使用率、磁盘使用率、每核线程数、Java堆使用率、TPS估算等）
- **隐藏层**：2层，每层16个神经元（ReLU激活）
- **输出层**：3个输出（崩溃概率、性能评分、优化优先级）
- **训练方式**：在线学习 + 反向传播
- **持久化**：JSON格式保存权重

#### 3.1.2 神经网络用途

1. **崩溃预测增强**：结合规则引擎的阈值检测，MLP提供非线性的综合评分
2. **性能瓶颈定位**：分析哪个指标对性能影响最大
3. **优化建议排序**：根据预测收益对建议进行优先级排序
4. **自纠正反馈**：用户接受/拒绝建议后调整网络权重

#### 3.1.3 新增文件

| 文件路径 | 说明 |
|---------|------|
| `Services/AIService/NeuralNetwork/MlpNetwork.cs` | MLP神经网络核心实现 |
| `Services/AIService/NeuralNetwork/TrainingData.cs` | 训练数据结构与样本生成 |
| `Services/AIService/PerformancePredictor.cs` | 性能预测器（MLP封装） |

#### 3.1.4 修改文件

| 文件路径 | 修改内容 |
|---------|---------|
| `CrashPredictor.cs` | 集成MLP预测，保留规则引擎作为兜底 |
| `ConfigOptimizer.cs` | 每条建议增加Confidence字段和Basis判断依据 |
| `AiGuardOrchestrator.cs` | 整合MLP预测结果，增加自纠正逻辑 |
| `IAiGuardService.cs` | 扩展接口，增加性能预测方法 |
| `SystemMetrics.cs` | 增加MLP输入所需的特征字段 |

---

### 模块二：CPU型号识别

#### 3.2.1 识别范围

- **Intel全系列**：Core i3/i5/i7/i9（第1代到第15代）、Xeon、Pentium、Celeron、Atom
- **AMD全系列**：Ryzen 3/5/7/9（1000-9000系列）、Threadripper、EPYC、Athlon
- **其他**：ARM（Windows on ARM）、老旧型号兼容处理

#### 3.2.2 识别信息

| 信息项 | 来源 | 用途 |
|-------|------|------|
| CPU型号名称 | WMI Win32_Processor.Name | 展示给用户 |
| 核心数/线程数 | WMI + Environment.ProcessorCount | 线程数优化建议 |
| 基础频率/睿频 | WMI Win32_Processor.MaxClockSpeed | 性能评估 |
| 架构 | 通过型号字符串解析 | 优化建议针对性 |
| 代际 | 通过型号字符串解析 | 判断是否老旧需升级 |
| 性能评分 | 本地基准数据库 | 相对性能对比 |

#### 3.2.3 CPU性能数据库设计

使用嵌入式JSON数据库，包含常见CPU的性能跑分（Cinebench R23单核/多核），用于相对性能评估。数据库结构：

```csharp
public record CpuInfo
{
    public string ModelName { get; init; }
    public string Manufacturer { get; init; }      // Intel / AMD
    public string Architecture { get; init; }       // Zen3 / Golden Cove 等
    public int Generation { get; init; }            // 代际
    public int Cores { get; init; }
    public int Threads { get; init; }
    public double BaseClockGHz { get; init; }
    public double BoostClockGHz { get; init; }
    public int CinebenchR23Single { get; init; }    // 单核跑分
    public int CinebenchR23Multi { get; init; }     // 多核跑分
    public DateTime ReleaseDate { get; init; }
    public string Tier { get; init; }               // 入门/主流/高端/旗舰
}
```

#### 3.2.4 新增文件

| 文件路径 | 说明 |
|---------|------|
| `Services/HardwareInfo/CpuIdentifier.cs` | CPU识别核心逻辑 |
| `Services/HardwareInfo/CpuDatabase.cs` | CPU性能数据库（嵌入资源） |
| `Models/Hardware/CpuInfo.cs` | CPU信息数据模型 |
| `Resources/cpu_database.json` | CPU数据库JSON文件 |

---

### 模块三：内存识别增强

#### 3.3.1 识别信息

| 信息项 | 来源 | 用途 |
|-------|------|------|
| 总容量 | GlobalMemoryStatusEx（已有） | 内存分配建议 |
| 可用容量 | GlobalMemoryStatusEx（已有） | 实时监控 |
| 内存频率 | WMI Win32_PhysicalMemory.Speed | 性能评估 |
| 内存类型 | WMI Win32_PhysicalMemory.MemoryType | DDR3/DDR4/DDR5 判断 |
| 插槽数量 | WMI Win32_PhysicalMemory 计数 | 升级建议 |
| 单条容量 | WMI Win32_PhysicalMemory.Capacity | 升级建议 |

#### 3.3.2 修改文件

| 文件路径 | 修改内容 |
|---------|---------|
| `MemoryMonitor.cs` | 增加内存频率、类型、插槽信息获取 |
| `SystemMetrics.cs` | 增加内存频率、类型等字段 |

---

### 模块四：自学习与自纠正增强

#### 3.4.1 自学习机制（增强）

现有自学习仅针对日志模式，扩展为：

1. **日志模式学习**（现有）：从日志中发现新异常模式
2. **优化效果学习**（新增）：记录用户采纳的建议及其后续效果
3. **阈值自适应**（新增）：根据历史数据动态调整告警阈值
4. **用户反馈学习**（新增）：用户标记误报/漏报后调整权重

#### 3.4.2 自纠正机制（新增）

1. **误报纠正**：用户标记"这不是异常" → 降低该模式权重或加入白名单
2. **漏报纠正**：用户标记"这应该是异常" → 提升为学习模式
3. **建议有效性反馈**：用户采纳建议后 → 观察后续指标变化 → 调整建议置信度
4. **模型自校正**：定期用历史数据重新训练MLP网络

#### 3.4.3 优化建议判断依据

每条建议必须包含：

```csharp
public record OptimizationBasis
{
    public string MetricName { get; init; }       // 依据的指标名称
    public double CurrentValue { get; init; }     // 当前值
    public double ThresholdValue { get; init; }   // 阈值
    public double DeviationPercent { get; init; } // 偏离程度百分比
    public string DataSource { get; init; }       // 数据来源（WMI/规则/MLP）
    public double Confidence { get; init; }       // 置信度 (0-1)
    public List<string> EvidenceChain { get; init; } // 证据链
}
```

---

### 模块五：主动内存压缩

#### 3.5.1 功能设计

1. **进程工作集清理**：调用 `EmptyWorkingSet` 将进程内存换出到页面文件
2. **智能触发条件**：
   - 系统内存使用率 > 85% 时自动触发
   - 服务器空闲时（CPU < 20%）定期清理
   - 用户手动触发
3. **安全保护**：
   - 只清理非系统进程
   - 不对当前前台进程操作
   - 操作前后记录内存变化

#### 3.5.2 内存压缩策略

| 策略 | 触发条件 | 操作对象 | 说明 |
|-----|---------|---------|------|
| 轻度压缩 | 内存 > 80% | 用户进程（非MC服务器） | 只清理普通应用 |
| 中度压缩 | 内存 > 90% | 所有非关键进程 | 包含后台服务 |
| 紧急压缩 | 内存 > 95% | 所有可安全终止进程 | 包括部分可重启服务 |

#### 3.5.3 新增文件

| 文件路径 | 说明 |
|---------|------|
| `Services/SystemOptimization/MemoryCompressor.cs` | 内存压缩核心逻辑 |

---

### 模块六：进程清理与管理

#### 3.6.1 进程分类体系

将系统进程分为五类：

| 类别 | 说明 | 是否可杀 | 示例 |
|-----|------|---------|------|
| 🔴 系统核心进程 | Windows关键系统进程 | ❌ 绝对不能杀 | System, smss, csrss, wininit, services, lsass, winlogon |
| 🟡 系统服务进程 | Windows服务进程 | ⚠️ 谨慎，需确认 | svchost(部分), dllhost, taskhost |
| 🟢 MC服务器进程 | Minecraft服务器Java进程 | ❌ 不能杀（这是保护对象） | java/javaw（服务器） |
| 🔵 用户软件进程 | 用户安装的普通软件 | ✅ 可以杀 | chrome, wechat, qq, discord 等 |
| ⚪ 未知进程 | 无法分类的进程 | ⚠️ 需要用户确认 | 其他 |

#### 3.6.2 Windows系统进程白名单

内置一份完整的Windows系统核心进程白名单（Windows 10/11 常见进程）：

**绝对不能杀的系统进程：**
- System (PID 4)
- smss.exe (会话管理器)
- csrss.exe (客户端服务器运行时)
- wininit.exe (Windows启动应用)
- services.exe (服务控制管理器)
- lsass.exe (本地安全机构)
- winlogon.exe (Windows登录)
- svchost.exe - 关键服务组（需要进一步区分）
- explorer.exe - 可重启但不建议杀
- 等等...

#### 3.6.3 常见用户软件库

内置常见用户软件进程名列表，用于分类和建议：

**浏览器类：** chrome, msedge, firefox, brave, opera...
**通讯类：** wechat, qq, tim, discord, telegram, skype...
**办公类：** wps, winword, excel, powerpnt, onenote...
**开发类：** code, devenv, idea64, webstorm, goland...
**娱乐类：** steam, epicgameslauncher, spotify, cloudmusic...
**下载类：** thunder, qbittorrent, motrix...
**其他：** 等等...

#### 3.6.4 进程清理策略

| 清理级别 | 触发条件 | 操作范围 |
|---------|---------|---------|
| 推荐清理 | 内存 > 80% | 用户软件中长时间未活动的进程 |
| 深度清理 | 内存 > 90% | 所有非必要用户软件 + 可重启服务 |
| 紧急清理 | 内存 > 95% | 除MC服务器和系统核心外的所有进程 |

#### 3.6.5 新增文件

| 文件路径 | 说明 |
|---------|------|
| `Services/SystemOptimization/ProcessManager.cs` | 进程管理核心逻辑 |
| `Services/SystemOptimization/ProcessClassifier.cs` | 进程分类器 |
| `Resources/process_whitelist.json` | 系统进程白名单 |
| `Resources/user_software_db.json` | 用户软件数据库 |
| `Models/SystemOptimization/ProcessInfo.cs` | 进程信息模型 |

---

### 模块七：UI层集成

#### 3.7.1 AI守护页面增强

在现有 `AIGuardPage.xaml` 基础上增加：

1. **硬件信息展示区**：CPU型号、内存规格（频率/类型）
2. **MLP性能仪表盘**：性能评分、瓶颈分析雷达图（可选）
3. **建议详情面板**：点击建议展开查看判断依据和置信度
4. **反馈按钮**：每条建议旁增加"有帮助/没帮助"反馈按钮
5. **自学习状态**：显示已学习模式数、模型训练进度

#### 3.7.2 系统优化页面（新增）

新建一个系统优化页面（或者集成到监控页）：

1. **内存优化卡片**：当前内存状态、一键优化按钮、优化效果预估
2. **进程管理列表**：分类展示所有进程，可选择终止
3. **优化历史记录**：记录每次优化操作和效果

#### 3.7.3 修改文件

| 文件路径 | 修改内容 |
|---------|---------|
| `ViewModels/AIGuardViewModel.cs` | 增加硬件信息、MLP预测、反馈命令 |
| `Views/AIGuardPage.xaml` | UI增强，硬件信息展示、建议详情、反馈按钮 |
| `McServerGuard.csproj` | 增加资源文件引用 |
| `App.xaml.cs` / DI注册 | 注册新增服务 |

---

## 四、实施步骤（分阶段）

### 阶段一：基础架构搭建（核心文件创建）

1. 创建 MLP 神经网络核心类
2. 创建 CPU 识别模块
3. 创建内存识别增强模块
4. 扩展 SystemMetrics 数据模型

### 阶段二：AI 服务增强

1. 集成 MLP 到崩溃预测器
2. 增强配置优化器（增加判断依据）
3. 实现自纠正反馈机制
4. 扩展 AiGuardOrchestrator

### 阶段三：系统优化功能

1. 实现内存压缩服务
2. 实现进程分类器（白名单+用户软件库）
3. 实现进程管理服务
4. 进程清理安全策略

### 阶段四：UI 集成

1. AI 守护页面 UI 增强
2. 硬件信息展示
3. 建议详情与反馈
4. 系统优化页面（可选）

### 阶段五：测试与调优

1. 单元测试（MLP训练/预测）
2. 集成测试（完整流程）
3. 边界情况处理（WMI失败降级、异常CPU型号兼容）
4. 性能优化

## 五、风险与注意事项

### 5.1 技术风险

| 风险 | 影响 | 应对策略 |
|-----|------|---------|
| WMI 在某些系统上调用慢/失败 | 硬件识别不可用 | 降级方案：Environment.ProcessorCount + 注册表读取 |
| MLP 训练过拟合 | 预测不准 | 正则化 + 规则引擎兜底 + 样本多样化 |
| 进程分类误判 | 杀错进程 | 多层校验 + 用户确认 + 白名单严格模式 |
| 内存压缩导致卡顿 | 用户体验差 | 低峰期执行 + 异步操作 + 可配置开关 |

### 5.2 兼容性

- **目标平台**：Windows 10/11 x64/arm64
- **.NET版本**：.NET 10
- **WPF限制**：UI线程安全，所有WMI调用放后台线程
- **权限要求**：普通用户权限即可（部分进程操作可能需要管理员）

### 5.3 性能影响

- MLP推理：单次 < 1ms（12输入、2隐层16神经元）
- CPU识别：首次 < 50ms（WMI调用），之后缓存
- 内存压缩：视进程数量而定，异步执行不阻塞UI
- 进程扫描：< 200ms（全进程枚举）

## 六、新增服务依赖注入注册

在 DI 容器中注册以下新服务：

```csharp
// 硬件信息服务
services.AddSingleton<CpuIdentifier>();
services.AddSingleton<CpuDatabase>();

// 神经网络服务
services.AddSingleton<MlpNetwork>();
services.AddSingleton<PerformancePredictor>();
services.AddSingleton<TrainingDataGenerator>();

// 系统优化服务
services.AddSingleton<MemoryCompressor>();
services.AddSingleton<ProcessManager>();
services.AddSingleton<ProcessClassifier>();
```

## 七、总结

本计划实现以下核心目标：

1. ✅ **真正的MLP神经网络**：用于崩溃预测和性能评估，非线性建模
2. ✅ **自学习与自纠正**：用户反馈驱动模型迭代，阈值自适应
3. ✅ **判断依据透明化**：每条建议附带量化指标、证据链、置信度
4. ✅ **CPU型号全面识别**：覆盖Intel/AMD全系列，附带性能评分
5. ✅ **内存信息增强**：频率、类型、插槽数完整识别
6. ✅ **主动内存压缩**：多级策略，智能触发
7. ✅ **进程清理管理**：系统进程白名单保护，智能分类，安全终止
