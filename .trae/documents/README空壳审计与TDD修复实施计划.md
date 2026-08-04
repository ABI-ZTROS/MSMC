# README 声明 vs 实际代码 空壳审计与 TDD 修复实施计划

## 一、任务目标（用户原话）

> "readme写的很牛逼，你把readme里面的内容再套回去，看看哪些东西没实现或者是空壳子呢？
> 老规矩：因果链，执行链，响应链，UI模拟以及还原。有必要时在网上调研，善用并发子代理，
> 写完代码后要看看是不是空壳子。前端受到改动后确认是否影响已有的UI布局。
> 完成之后在本地核查后推送（commit写清楚干了些啥）。"

**核心原则（TDD 铁律）**：NO PRODUCTION CODE WITHOUT A FAILING TEST FIRST。任何补全的空壳功能必须先写 xUnit/Vitest 测试 → 确认因缺失实现而 FAIL（正确原因） → 再写最小代码使测试 PASS → 回归 GREEN。

---

## 二、初步仓库审计结论（Repo Research）

### 2.1 初步发现（grep 正则扫关键词）

第一轮扫 `NotImplementedException/throw null/return null/TODO/未实现` 6 大典型空壳关键词，命中 **20 个 C# 源文件**：

| 命中文件（共 20 个） | 所属模块 |
|---|---|
| `ServerDetection/ViewModels/ServerDetectionViewModel.cs` | L1·服务器检测（VM层） |
| `ServerDetection/Services/JarCoreIdentifier.cs` | L1·服务器检测（核心指纹识别） |
| `ServerDetection/Services/WorkingDirectoryResolver.cs` | L1·服务器检测（工作目录解析） |
| `ServerDetection/Services/JavaFinder.cs` | L1·服务器检测（进程级 Java/JAR 扫描） |
| `ServerDetection/Services/ServerImporterService.cs` | L1·服务器检测（导入服务） |
| `ServerDetection/Services/ServerManagerService.cs` | L1·服务器检测（实例管理：启动/停止/重启） |
| `ServerDetection/Services/StartupScriptDetector.cs` | L2·启动脚本解析器 |
| `ServerDetection/Services/ServerDetector.cs` | L1·服务器检测主入口 |
| `ConfigEditor/ViewModels/ConfigEditorViewModel.cs` | L1·配置编辑器（VM层） |
| `ConfigEditor/Services/ConfigDescriptorRegistry.cs` | L1·配置编辑器（36+核心中文翻译矩阵） |
| `ConfigEditor/Services/YamlParser.cs` | L1·配置编辑器（YAML 6 格式之一解析） |
| `JavaInstallation/Services/JavaFinderService.cs` | L2·Java 运行环境管理（正式版，与上面 SD 内 JavaFinder 疑似重复） |
| `SystemMonitoring/Services/ProcessSupervisorService.cs` | L3·进程监管 / CPU 电源实验性 |
| `SystemMonitoring/Services/ProcessManagerService.cs` | L1·系统监控（进程亲和/终结等） |
| `NetworkMonitor/Services/*`（需详查 6 个服务） | L2·端口监控与桥接 |
| `Settings/Services/ThemeService.cs` | L3·主题系统 13 套品牌预设 |
| `Settings/Services/AnimationSettings.cs` | L3·主题系统·动画配置 |
| `Settings/Services/ToastNotificationService.cs` | L3·设置·Windows 原生 Toast |
| `WebView2/Frontend/ZipExtractResourceProvider.cs` | L3·EmbeddedResource wwwroot.zip 解压器 |
| `WebView2/Frontend/FrontendResourceProviderFactory.cs` | L3·EmbeddedResource 工厂分发 |
| `Shared/Helpers/AnimationHelper.cs` | 基础工具·动画辅助 |
| `UserAgreement/Views/UserAgreementWindow.xaml.cs` | L3·用户协议窗口逻辑 |

⚠ 注意：grep 只抓到了「明显写了 NotImplemented/TODO」的文件，**还有一批文件是「接口有方法、实现类返回 default/null/空 List、但没有显式抛异常」的隐性空壳**（例如 PropertiesParser/TomlParser/HoconParser/XmlParser 等 6 格式完整度、NetworkService 端口扫描与 UPnP、NetshPortBridgeService 实际调用 netsh.exe、MetricsPersistenceService 24h 持久化等），这部分必须深度读源码排查，不能仅靠关键词。

### 2.2 前端 React 层空壳嫌疑区（已读 L1~L3 全部页面骨架）

根据已读 `DashboardPage / ConfigEditorPage / SystemMonitorPage / NetworkMonitorPage / PowerPage / JavaPage / SettingsPage` 7 个页面源码的初步结构：
- 大量页面使用 `const [items, setItems] = useState([])` 初始化，数据来源硬编码或 mock，没有走 `useBridgeInit() + bridge.invoke(...)` 真实 RPC。
- `DashboardPage` 服务器列表、`ConfigEditorPage` 当前配置项、`SystemMonitorPage` 每核 CPU 条形与趋势图、`PowerPage` 睿频档位列表、`JavaPage` Java 安装列表，存在**硬编码静态数据**或「加载中永远不 resolve」的情况。
- Sidebar 的 7 个导航图标与路由已存在，路由入口不为空壳，但**路由页面内部的渲染结果可能是空壳 UI**（骨架完整但数据没动）。

### 2.3 WebView2 Bridge API 层空壳嫌疑区

在 `WebView2BridgeService.cs` 中应当注册 50+ 个 API handler，但已读代码仅覆盖主题相关（getSettings / setPrimaryColor / applyThemePreset）、设置相关、少量服务器检测相关。需逐项对照 README 声明的功能点做「前端要调用 X API → 后端有没有 RegisterApiHandler("x", ...)」的 1:1 审计。

---

## 三、审计方法：三链审计法（因果链 · 执行链 · 响应链）

对 README 中**每一条 bullet 功能声明**，逐项执行以下三链分析：

### 3.1 因果链（为什么能/不能工作）

```
README 声明能力 A
  └─因果：依赖 B（后端服务方法）+ C（数据结构）+ D（持久化字段）同时存在
       ├─B 存在？→ 方法体有真实逻辑？→ 返回类型与入参匹配？→ 无 throw？
       ├─C 存在？→ 字段齐全？→ 有 JSON 映射？→ 不为 default 全零？
       └─D 存在？→ AppConfigService 有对应 Key？→ 有读写？→ 有默认值？
结果：✅ 完整 / ⚠️ 部分（B/C/D 有缺） / ❌ 空壳（三项任意一项 throw 或 null/default 恒返回）
```

### 3.2 执行链（从用户点击到结果返回的完整步骤）

以「用户点击服务器检测 → 刷新按钮」为例：
```
[UI] DashboardPage → onRefreshClick()
  → React utils/bridge.ts → bridge.invoke("server:detect")
    → WebView2BridgeService.cs 注册的 handler["server:detect"] 存在？
      → handler 内部 DI 拿到 IServerDetector
        → ServerDetector.DetectAsync()
          ├─ProcessScanner（枚举 javaw.exe）
          ├─CommandLineParser（解析 JAR 路径、JVM 参数）
          ├─JarCoreIdentifier（Manifest + 哈希）
          ├─PortScanner（枚举 TCP 0-65535）
          ├─StartupScriptDetector（父进程 + bat/ps1/sh）
          └─ 返回 DetectionResult 序列
      → 序列化为 JSON
    → bridge.invoke Promise resolve（不为永远 pending）
  → React 代码 setServers()
→ [UI] md-server-card 出现卡片
```
任何一环缺实现 → 整链断裂 → 空壳。

### 3.3 响应链（用户可见结果是否符合预期语义）

```
用户预期（README）："36+ 核心识别" → 实际代码：
  返回 DetectionResult 里 ServerType 枚举值是否覆盖 36 个？
  不覆盖 → ServerTypeClassifier.Classify() 分类数 < 30 → 部分。
  仅 10 种 → 空壳（写了 36+ 但实际只能识别 Bukkit/Vanilla 等 10 种常见）。
```

三链**同时成立**才判定为"完整"。任意一条断裂即判定为部分/空壳。

---

## 四、要审计与修复的模块清单（分 L1/L2/L3，按严重优先级排序）

### 🔴 P0：L1 三大核心（详写模块，用户最能直接感知到）

| # | 模块 | README 功能声明要点 | 预期审查与修复范围 |
|---|---|---|---|
| L1-1 | 🖥️ 服务器检测与核心指纹识别 | ① 进程级多实例扫描 ② 36+ 核心指纹（Bukkit→Mod端→代理→混合端全表） ③ JVM 参数解析（-Xmx/-XX GC/-D 属性三分类） ④ JAR Manifest 深读取（Git-Commit/Build-Time 等签名字段） ⑤ 启动脚本自动关联（父进程 + bat/ps1/sh 4 格式） ⑥ 端口监听探测 25565/19132 ⑦ 6 维资源占用（Private Bytes/线程/句柄数/GDI 对象等） ⑧ 崩溃/僵死判断（线程 0 + CPU 0% + 句柄上涨） ⑨ 一键停止三阶段（Ctrl+C→30s→TaskKill） ⑩ server-icon.png 64×64 自动预览 | **JavaFinder + JarCoreIdentifier + ServerTypeClassifier + ProcessScanner + PortScanner + WorkingDirectoryResolver + StartupScriptDetector + ServerManagerService** 8 个服务 + 对应 Bridge API 8+ handlers + DashboardPage 渲染 |
| L1-2 | ⚙️ 全核心中文配置编辑器 | ① 36+核心翻译映射 ② 6 种格式原生读写（Properties/YAML/TOML/JSON/HOCON/XML + YAML 1.1 布尔/时间戳陷阱修正） ③ 完整撤销/重做栈（Ctrl+Z 30 秒脏快照） ④ 实时搜索过滤（中英文双向搜索 + 4 类标签筛选） ⑤ 差异化控件（Spinner/Toggle/下拉/输入框/可增删列表） ⑥ 原子保存（tmp→ReplaceFile）+ 脏标记 `●` ⑦ Properties 原版顺序+注释保留 ⑧ 大文件 5000+ 行虚拟化 ⑨ 原版英文+中文注释长按双面板 | **ConfigDescriptorRegistry（36+核心翻译矩阵完整性）+ 6 格式 Parser（Properties/YamlParser + TOML/JSON/HOCON/XML 缺失 Parser）+ ConfigManager + ConfigEditorViewModel 撤销重做栈 + Bridge API 7+ handlers + 前端 ConfigEditorPage 的 5 类控件渲染** |
| L1-3 | 📊 异构 CPU 拓扑监控仪表盘 | ① 真实异构 CPU 拓扑树（P-core/E-core/SMT/NUMA/多级 Cache 徽章） ② 每核实时利用率 1s 刷新（精确 0.1%） ③ 内存/页文件/GC 压力估算（Gen2 数/CPU 时间阈值报警） ④ 磁盘 IOPS + 容量 5 项（物理盘 + 吞吐 + IOPS） ⑤ 线程数/句柄数/GDI 对象 24h 三折线趋势 ⑥ CPU Set 亲和性绑定（Win32 SetProcessDefaultCpuSets，拖拽 UI） ⑦ NUMA 跨节点调度 ≥30% 警告徽章 ⑧ 24h 历史指标持久化 MMF+降采样 ⑨ 进程级 Top N 3 种排序 ⑩ 可选温度/电压（LibreHardwareMonitor） | **CpuIdentifier（真实 GetLogicalProcessorInformationEx 封装）+ SystemMonitor 9 个采样方法 + MetricsPersistenceService（MMF/JSON 文件持久化）+ ProcessManagerService + 前端 SystemMonitorPage 的 CPU 拓扑/环形/折线图渲染 + Bridge API 10+ handlers** |

### 🟠 P1：L2 四大重要功能

| # | 模块 | README 功能声明要点 | 修复范围 |
|---|---|---|---|
| L2-1 | ☕ Java 运行环境管理（正式版，重复 JavaFinder 需合并） | ① 注册表 HKLM\SOFTWARE\JavaSoft\JDK 扫描 ② JAVA_HOME / PATH 解析 ③ 5+ 常见目录枚举 ④ 自定义路径「浏览/手动输入」 ⑤ 默认版本金色徽章 + 新增实例时 pin ⑥ javaw.exe/java.exe 偏好切换 ⑦ vendor+arch+major 三维标注 ⑧ 导入导出 JSON 快照 | **JavaFinderService（正式版，与 SD/JavaFinder 去重合并）+ AppConfigService 存默认版本 + 路径列表持久化 + Bridge API（getJavaList / rescanJava / setDefault / addPath / removePath / importExport）+ 前端 JavaPage** |
| L2-2 | ⚡ CPU 电源与调度优化（🧪 实验性，总开关关闭=不加载） | ① 总开关：关闭=CpuPowerService 全不注册/API 全不挂/前端功能不渲染 ② CPU 睿频档位 4 档（节能/均衡/性能/狂飙） ③ CPU QoS：SetProcessInformation 系统调用 ④ CPU Set 亲和性批量分配（与 L1-3-⑥ 共享底层） ⑤ 多媒体定时器精度 1ms/15ms（timeBeginPeriod） ⑥ Power Request 防睡眠（阻止系统休眠 + 阻止显示器关闭） ⑦ 管理员权限前置校验：未提升 → 提示 + 一键 restart 提权 | **CpuPowerService（之前 DI 已修）+ 4 档位 Win32 真实 powercfg / SetThreadExecutionState / timeBeginPeriod 调用链 + AppConfigService `EnableExperimentalPowerManagement` 开关 + DI 条件注册（IServiceCollection 仅当 true 时 AddSingleton CpuPower）+ Bridge API 条件注册 + 前端 PowerPage 总开关 + "启用后需重启 MSMC" 提示条** |
| L2-3 | 🛰️ 端口监控与桥接 | ① 实时端口扫描 0-65535 TCP/UDP 监听 ② netsh interface portproxy add v4tov4 真实执行 ③ UPnP IGDv2 + NAT-PMP 双协议端口映射 ④ 入站/出站 Mbps/Tick ETW 追踪 ⑤ 公网 IP 检测 + IP 变化检测通知（stun 服务器列表） ⑥ IPv4/IPv6 双栈 ⑦ 防火墙一键加白 netsh advfirewall | **NetworkService 端口扫描 + NetshPortBridgeService（真实 Process.Start("netsh.exe")）+ TcpForwarderService + CompositePortBridgeService 调度 netsh/UPnP 两策略 + NetworkTrafficService ETW + UPnP NAT-PMP 封装 + 公网 IP HTTP + STUN 检测 + Bridge API 7+ handlers + 前端 NetworkMonitorPage** |
| L2-4 | 📜 启动脚本解析器 4 语言 | ① 4 格式：.bat/.cmd（CMD）/ .ps1（PowerShell）/ .sh（Bash/WSL） ② JVM 参数完整抽取 -Xmx/-Xms/-XX GC/-D/-agentpath ③ JAR 路径 + 工作目录定位（cd /d pushd 相对路径处理） ④ 多脚本冲突同一 JAR ≥2 时黄色警告 ⑤ 参数模板 JSON 导出 ⑥ 参数合法性校验（-Xmx 超出物理内存预警） | **StartupScriptDetector（之前命中 TODO）+ 内部 Parser 分 4 语种子类 + 桥 L2-1 共享 JVM 参数抽取逻辑（避免重复实现）+ 导入时冲突检测 + Bridge API 3+ handlers** |

### 🟡 P2：L3 基础设施简表 4 模块

| # | 模块 | 修复范围 |
|---|---|---|
| L3-1 | 🎨 主题系统 Settings：13 套预设（ColorOS/Aquario/极光/日落/薄荷…）+ 主色/强调色取色器 + 圆角 0-16px 滑块 + 动画开关 + 深浅色跟随系统 + Toast 测试按钮 + 进程监管策略 4 项 + 崩溃次数 + 防睡眠开关 + CPU 优先级 + 内存上限 | **ThemeService（13 套预设枚举值写进代码里，不能只有接口）+ ToastNotificationService（真实 Win10/11 Toast 平台 API）+ AppConfig 所有 Key 字段存在并能读能写 + AnimationSettings（全局动画开关）→ 对应 SettingsPage UI 渲染（每一项都要有真实取值，不是死数据）** |
| L3-2 | 📝 用户协议：首次启动弹 / 版本号 v3.0.0 / 已同意后重启动不再弹 / 版本升级强制重新同意（RequiresReagreement 逻辑） | **UserAgreementWindow.xaml.cs：Countdown 2 分钟倒计时 + ScrollViewer 滚动到底部才能点击「同意」（这两个逻辑目前很可能 TODO，需验证补全）+ UserAgreementService 的 Load/Save 完整 |
| L3-3 | 💥 崩溃恢复 CrashWindow：三道异常防线（DispatcherUnhandledException / AppDomain UnhandledException / TaskScheduler UnobservedTaskException）→ 友好崩溃窗 + 异常消息 + StackTrace + 一键复制 + 一键打开 logs/ + 尝试重启 MSMC 按钮 + Serilog 强制 flush | **App.xaml.cs 三道挂接 + CrashWindow.xaml.cs 5 个按钮 Click Handler（目前可能只有 XAML 布局，没有真实复制/开目录/重启实现）** |
| L3-4 | 🌉 WebView2 Bridge：① 50+ 个真实 handlers 全部挂到 AddHostObjectToScript ② SSE 广播（启动进度/主题变更/监管心跳）3 类主题事件 ③ EmbeddedResource wwwroot.zip 解压拦截分发 ZipExtractResourceProvider（之前命中 TODO，需补全真实解压） ④ 503/404 资源兜底页 | **WebView2BridgeService.cs handler 清单（对照 README 全部 API 数一遍）+ 三道 SSE broadcast 触发点 + ZipExtractResourceProvider 解压到 MemoryMappedFile 或 TempDirectory（不要真写到磁盘）+ FrontendResourceProviderFactory 根据 basePath 选择 Folder/Zip/EmbeddedResource** |

---

## 五、按模块的 TDD 测试设计（RED → GREEN → REFACTOR 三阶段每功能必走）

### 5.1 后端 C# xUnit 测试（放 `src/MSMC.Tests/Services/` 下新文件）

每个待测服务生成一个独立的 `*Tests.cs`，遵循「一条测试一个行为，明名带 When_Given_Should」规范：

| 新建测试文件（建议 14 个） | 覆盖模块 | 示例核心测试名（仅典型，需扩展到 50+） |
|---|---|---|
| `JarCoreIdentifierTests.cs` | L1-1 核心指纹 | `IdentifyAsync_WhenPaperJarManifestContainsPaperweightVersion_ShouldReturnServerTypePaper` |
| `ServerTypeClassifierTests.cs` | L1-1 分类 | `ClassifyCore_36KnownJarHashSamples_ShouldMatchExpectedEnum （Parameterized 36 InlineData）` |
| `CommandLineParserTests.cs`（已有，需扩容 L2-1 JVM） | L1-1 + L2-4 | 已有；扩容：`ParseJvmArguments_WhenAgentpathAndMultipleGcFlags_ShouldExtractAll8Flags` |
| `StartupScriptDetectorTests.cs` | L2-4 4 语言 | `Detect_WhenCmdBatWithCdDPushd_ShouldResolveWorkingDirectory` + PowerShell + Bash 3 组 |
| `YamlParserTests.cs` | L1-2 格式 | `Parse_WhenYaml11BooleanValueOn_ShouldParseAsTrueNot1230AmString` |
| `ConfigFormatDetectorTests.cs` | L1-2 格式 | 已有；保持 GREEN |
| `PropertiesParserTests.cs` | L1-2 格式 | 已有；保持 GREEN；追加 `WriteBack_ShouldPreserveOriginalKeyOrderAndComments` |
| `ConfigDescriptorRegistryTests.cs` | L1-2 翻译 | `Registry_ShouldContainDescriptorsForAll36Cores_DescriptorsCountGreaterThan1000` |
| `ConfigManagerTests.cs` | L1-2 原子保存 | `SaveAsync_WhenWriteInterrupted_ShouldNotCorruptOriginalFile_TmpOnly` |
| `CpuIdentifierTests.cs` | L1-3 拓扑 | `GetCpuTopology_OnNonZeroCpu_ShouldReturnAtLeastOnePackageNode` |
| `SystemMonitorSamplingTests.cs` | L1-3 采样 | `SampleOnce_ShouldReturnNonNullMetrics_WithAllFieldsInitialized` |
| `MetricsPersistenceTests.cs` | L1-3 24h | `AppendSample_24HoursOf1sSamples_ShouldDownsampleTo1440MinutePoints` |
| `JavaFinderServiceTests.cs` | L2-1 Java | `ScanRegistry_WhenStubRegHas5Jdks_ShouldReturnAll5WithCorrectVersionVendorArch` |
| `CpuPowerServiceTests.cs` | L2-2 实验 | `ApplyPowerProfile_WhenCalledAsAdmin_ShouldInvokePowercfgWithCorrectGuid` + 权限不足时抛 UnauthorizedAccessException |
| `NetshPortBridgeServiceTests.cs` | L2-3 转发 | `AddForwardingRule_ShouldExecuteNetshWithExpectedArgs_WhenValidPortAndAddress` |
| `UserAgreementServiceTests.cs` | L3-2 | `RequiresReagreement_WhenAgreedVersionV200CurrentV300_ShouldReturnTrue` |
| `ZipExtractResourceProviderTests.cs` | L3-4 桥 | `GetEmbeddedZip_WhenWebAssetPathRequested_ShouldUnzipAndReturnCorrectStreamWithoutDiskWrite` |

### 5.2 前端 TS vitest 测试（如无前端测试框架则以「手动 UI 对照清单 + 浏览器 DevTools Network 截获 RPC 真实请求」等效替代）

如果前端还没有 vitest 配置（package.json 中 scripts 通常没有 test），则用**手动 UI 模拟还原清单**代替自动化测试：
- 7 个页面的每个按钮/输入框/滑块/开关点击 → 验证触发了 `bridge.invoke("xxx", payload)`，payload 字段符合类型定义 `src/frontend/src/types/bridge.ts`；
- 响应侧伪造 backend 返回 payload → 验证 UI 是否真实渲染数据（不是空骨架）；
- 前端布局回归：Sidebar 7 项 + 移动端折叠 + 3 张卡片 / 1 表格页面都在不超出滚动，`globals.css` 样式改动不影响原布局。

### 5.3 三链联合自动化：End-to-End 走读清单（最后跑）

对 L1/L2 的 7 大功能，走一遍「按钮点击 → 网络 → 响应 → UI 渲染」完整链路：
1. 检测页：点刷新 → `server:detect` → 假 javaw.exe 进程 → 返回 2 张卡片 → 卡片标题正确为 Paper + Velocity
2. 配置页：点打开 `test-server/server.properties` → 28 个 Properties 键 → 改 `view-distance` → 点保存 → 文件实际写入（读回确认）
3. 监控页：打开 1 秒后 → 环形图 CPU% > 0 → 每核条至少 4 条 → 趋势图有数据点
4. 电源页：总开关从 OFF→ON → 弹提示"需重启 MSMC 生效" → 关闭开关后 API 404（确认不注册）
5. Java 页：点重新扫描 → `java:list` 返回 3 项 → 设为默认 → 列表金色徽章
6. 网络页：添加端口转发 25565→25565 → 命令行 `netsh interface portproxy show all` 实际看到这条规则（有权限的环境）
7. 设置页：改主色滑块 → 立即全局色阶变化（所有页面 md-primary CSS 变量实时变）

---

## 六、执行步骤（顺序 + 并发子代理分工）

### Phase A：审计阶段（先不动代码，纯读+测试）

| 步骤 | 动作 | 执行者（可并发） |
|---|---|---|
| A1 | 逐文件通读 §4 中列出的 ~35 个 C# 实现类 + 7 个 React 页面，对 README 每条功能声明打三链判定：✅/⚠️/❌，输出最终审计清单（≥50 行结构化条目） | 并发子代理 A（C# 后端服务 3 个 Feature）+ 子代理 B（前端页面 + Bridge API 30+ handlers 对照） |
| A2 | 根据最终审计清单，写所有 ❌/⚠️ 对应单元测试 → 本地 `dotnet test src/MSMC.Tests` → 记录 FAIL 测试（正确原因，不是编译错误） | 子代理 C（TDD RED 阶段） |

### Phase B：修复阶段（GREEN，按严重级 P0→P1→P2）

| 步骤 | 动作 |
|---|---|
| B1 | **P0·L1 服务器检测**：补全 8 个空壳服务 → 重新测试 → GREEN |
| B2 | **P0·L1 配置编辑器**：补 TOML/HOCON/XML 三个 Parser（引用 NuGet Tomlyn/Hocon/配置库，保持与现有 YamlDotNet/Newtonsoft.Json 版本兼容） + ConfigDescriptorRegistry 36+ 核心翻译条目 ≥ 1000 + 撤销/重做栈 → GREEN |
| B3 | **P0·L1 系统监控**：补 CpuIdentifier P/E/NLAA/Cache 4 级结构 + MetricsPersistenceService 24h + 10 handlers → GREEN |
| B4 | **P1·L2 Java + 电源（实验开关）+ 网络 + 脚本解析**：逐项补全 → GREEN |
| B5 | **P2·L3 主题/用户协议/崩溃恢复/Bridge**：补 13 套主题值 + 用户协议倒计时+滚底判定 + 崩溃按钮 5 项真实逻辑 + 50+ API handlers 对照清单 → GREEN |
| B6 | 前端 7 页面对应的 bridge 调用：把静态 mock 数据改为真实 API，保留 loading skeleton + empty + error 三态 |

### Phase C：验证与交付阶段

| 步骤 | 动作 |
|---|---|
| C1 | 跑 **所有 xUnit 测试**：`dotnet test src/MSMC.Tests -c Release` → 全部 GREEN（0 FAIL） |
| C2 | **前端 TS 类型检查 + 构建**：`cd src/frontend && npm run build` → 无 TS2339 等错误；打开 dist/index.html 做 §5.2 UI 还原清单 7 页验证；确认 Sidebar/Settings/ConfigEditor 等布局无偏移 |
| C3 | **完整三链验证**：§5.3 7 条 E2E 清单逐项过 |
| C4 | **Git 提交（写清楚干了啥）**：使用 git-commit 技能，Commit 信息结构：<br> `feat: 补全 [模块名] README 声明空壳功能 (#编号)` <br> 每块一个独立 commit（不要 squash 全部丢一个 commit，无法回溯），commit body 详细列出补了哪些服务/API/方法，TDD 跑了哪些测试 |
| C5 | **推送**：`git push origin main`（若有 CI/CD，需等待 GitHub Actions `.github/workflows/ci.yml` 也 GREEN；若挂了立即查看日志修复） |

---

## 七、潜在风险与处理策略

| 风险 | 概率 | 影响 | 处理策略 |
|---|---|---|---|
| NuGet 版本冲突：引入 Tomlyn/Hocon/XML 序列化与现有 Serilog/MaterialDesign 依赖打架 | 中 | 构建失败 MSB3277 | 先 `dotnet list package` 看当前版本再引；用最低兼容版本而非最新；有冲突时退而求其次用内嵌解析（手写简化 TOML/HOCON 子集解析，先过测试） |
| Win32 API 签名错误（SetProcessInformation/GetLogicalProcessorInformationEx 等）导致 P/Invoke SEHException | 高 | 运行时炸 | 所有 DllImport 方法先用单元测试在 x64 下单独跑，签名不正确立即调 StructLayout/Pack 对齐；跑不通就先降级为「抛 PlatformNotSupportedException + 前端降级提示不支持」，不影响整体启动 |
| 前端 React 页面调用真实 bridge 后，因 bridge 没初始化而白屏（之前 LazyPageErrorBoundary 已经修过 chunk 失败，但现在可能是 bridge.invoke timeout 未处理） | 中 | UI 白屏/卡死 | 所有 bridge.invoke 调用统一包 5s timeout + catch 弹 `<Toast type="error">` 友好报错；永远不 promise pending |
| 13 套品牌色阶颜色值取值不专业，取色丑 | 低 | 美观差 | 取色参考 Material Design 色盘 500-900 系列 + OkLch 颜色空间（代码里已有 OkLchColor.cs 工具类），用它生成 16 级色阶，避免硬编码死颜色 |
| 补 Network UPnP 时，家里路由器没开 UPnP 导致测试 100% 挂 | 高 | CI 永远 FAIL | 测试里用 `public interface IUpnpClient` + Moq/手工假对象模拟成功响应；真正的网络调用仅在 Integration Test（单独标记，不跑在默认 CI）里执行 |
| 用户协议倒计时 + 滚到底部才能同意 逻辑在部分机器上 ScrollViewer ScrollChanged 事件不触发 | 中 | 按钮永远灰 | 兜底：倒数 2 分钟强制置为可点（不管滚没滚到底），并在旁边打勾「我已手动阅读完毕」；双重保险保证可继续 |

---

## 八、验收标准（用户交付物）

- ✅ **README 声明功能点对照清单**：一份 CSV 或 Markdown 表格，列出 README 中 ≥50 个 bullet 功能，每个都有「实际状态 + 代码文件位置 + 三链验证结果」；
- ✅ **代码改动**：以上 Phase B 所有补全（P0/P1/P2）全部通过自己写的测试；`dotnet test` GREEN + `npm run build` 成功；
- ✅ **前端布局回归报告**：7 个页面 + Sidebar + 设置页每项控件的截图/对照说明，明确标注「改动 X 不影响 Y 布局」；
- ✅ **提交记录**：按模块独立 8~12 个 commit，每一个 commit message 都写清楚改了哪个模块、补了哪些空壳、跑了哪些测试；
- ✅ **push 到 origin/main 成功**：`git rev-list --left-right --count origin/main...HEAD = 0 0` 或 ahead 数等于本计划产生的 commit 数。
