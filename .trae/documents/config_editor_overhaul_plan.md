# 配置文件编辑功能全面整改计划（Config Editor Overhaul）

## 1. 调研结论

用户当前反馈两类严重问题：

> ① 从始至终**列不出配置文件**（文件树永远空，下拉选了服务器也没反应，或选中后 UI 一直显示"请先选择服务器"）
> ② **配置解析系统完全没工作**：点了文件也加载不出条目，用户无法编辑、保存任何配置。

经过全链路（前端 → 桥接 → ViewModel → 配置管理服务层）的代码走查，确认**不是单一 bug**，而是 **5 层设计缺陷叠加**。每层各修一点的做法会导致"修了 A 又暴露 B"，因此建议一次性根治。

---

### 1.1 缺陷分层清单（从用户操作链路正向追查）

| 层 | 组件 | 严重缺陷 | 说明 | 影响 |
|---|---|---|---|---|
| **L1 前端** | ConfigEditorPage.tsx `handleSelectServer` → `loadFileTree` | **状态回滚竞态**：`selectConfigServer` 成功后前端 `setSelectedServerName(name)`，但**下一行**立刻调 `loadFileTree()` → 读 `selectedServerName = cfg.SelectedServerName`。如果这个时刻后端任何一条赋值链路有延迟 / fire-and-forget，返回的 selectedServerName 是 null → **立刻把刚才 set 的值覆盖为 null**。尽管已经加了 120ms 二次拉取，但这只是"在文件树层面补偿"，**没有补偿 selectedServerName 被回滚这件事**，导致下拉框视觉上仍然空着 / 没选中 → 用户："选不了"。| 下拉选中后立刻闪回空 |
| **L2 桥接** | MainWindow.xaml.cs `config:selectDefaultServer` | **只调 `SelectServerByContext`，不同步文件扫描**：`SelectServerByContext` 只设 `cfg.Server = best` → 触发 `OnServerChanged` → `_ = ScanDirectoryAfterServerChangedAsync()` **fire-and-forget**。handler 立刻 return `success=true` → 前端以为已经完成了，立刻 `loadFileTree()` → `ConfigFiles / ConfigFileTree` 还是空数组 → 空列表。| 联动 Dashboard 选中服务器后文件树永远空 |
| **L2 桥接** | MainWindow.xaml.cs `config:selectServer` L2118-L2129 目录不存在分支 | **死分支 + 潜在 IO 异常**：`direct.ConfigFiles.Where(...).Select(f => Path.GetRelativePath(direct.WorkingDirectory, f))` 此时 `direct.WorkingDirectory` 可能就是空字符串 → `Path.GetRelativePath("","...")` 在某些 .NET 运行时会抛 `ArgumentException`；而且注释说"ConfigFiles/ConfigFileTree 必须通过 setter 赋值"又说"走 cfg.Server = direct 链路"，前后矛盾；这里其实完全没赋值 `ConfigFiles / ConfigFileTree`，`Server = direct` 会触发 `OnServerChanged` 又重置一次所有状态（`ConfigFiles = []`）→ **连回退的 ConfigFiles 也被清空**。| 未运行服务器 + WorkingDirectory 不存在 → 100% 空列表 |
| **L3 ViewModel** | ConfigEditorViewModel `OnServerChanged` + `ScanDirectoryAfterServerChangedAsync` | **异步调度双重触发**：`OnServerChanged` 同步完成 `ServerWorkingDirectory` 赋值后 `_ = ScanDirectoryAfterServerChangedAsync(value)`（fire-and-forget 1），同时 L2 桥接层又自己 `await cfg.ScanDirectoryForConfigFilesAsync(direct.WorkingDirectory)`（await 2）。两次同时扫同一个目录时 `ConfigFileTree = treeRoot` 可能交错，最后写的那次如果恰好扫空（旧快照），列表会被空值覆盖。| 偶发的"扫到一半又没了" |
| **L3 ViewModel** | ConfigEditorViewModel `BuildConfigFileTree` L942-L949 目录过滤 | **根目录过滤规则错误**：`if (dirName.Equals("mods", StringComparison.OrdinalIgnoreCase) && depth > 0) continue;` → `depth > 0` 判定 `&& depth > 0` **写错了**。想表达的是"如果是 mods 目录，只有当 depth>0（不是根）才跳过"，或者意思是"mods 只要 depth > 0 才跳过"。但问题是：根的 depth 是 0，depth > 0 的子目录才会被跳过 → 如果用户服务器目录里**有一个叫 `world` / `logs` / `cache` 的子目录（这三个没加 depth 保护）直接跳过**，而根目录（depth 0）也被跳过（逻辑不对）。更严重的是 `dirName.StartsWith('.')` → 连 `plugins/.data` 下可能的 `.yml` 配置文件也被跳过了，而某些模组配置就在以 `.` 开头的目录里。 | 漏掉真实配置文件；plugins 目录下 .data 等隐藏配置永远扫描不到 |
| **L4 服务层** | ConfigFormatDetector.cs `HasYamlFeatures` L150-L177、`HasPropertiesFeatures` L187-L203 | **YAML vs Properties 冲突误判**：<br>1. 内容含 `server-port=25565:tcp` 这种带冒号的值 → 行内有 `:` 但也有 `=`，HasPropertiesFeatures 检查 `trimmed.Contains('=') && !trimmed.Contains(':')` → **失败**，不计数；<br>2. HasYamlFeatures 检查 `!trimmed.Contains('=')` 只对"无冒号+带空格且有冒号"那行成立；但大部分 server.properties 行都是 `key=value:port` 这种混合 → **两个计数器都 0** → `Detect(content) = ConfigFormat.Unknown` → `DetectByExtension(extension)` 才回到 `.properties`。但如果用户文件扩展名是 `.conf` 或 `.cfg`（确实有的模组会用），那么 **Resolve 最终返回 Unknown** → `ConfigManager.ReadConfigAsync` → `format switch { _ => throw HandleUnsupportedFormat }` → **异常！整个解析链路直接炸**。| `.conf`/`.cfg` 扩展名 + 内容含冒号 → 100% 解析失败，前端点文件条目永远不显示 |
| **L4 服务层** | ConfigManager.cs `ReadConfigAsync` L54-L84 `NotSupportedException` 没被上层 LoadConfigAsync catch 干净 | `LoadConfigAsync` catch `Exception ex` 打日志 `ConfigEntries.Clear()` 后确实不会让进程崩，但前端 `getEntries` 拿到 `groups=[]` → 用户看不到错误详情，只觉得"这个文件加载不出来"。| 错误被静默吞掉，用户不知道为啥解析失败 |
| **L4 服务层** | PropertiesParser.cs `Parse` L66-82 | **`:` 合法分隔符未支持** + **`!trimmed.Contains(':')` 脆弱反模式**：<br>Java Properties 规范允许 `:` 作为分隔符（`key: value` 与 `key=value` 等价）。Minecraft 的 server.properties 也有历史版本或自定义配置用 `:`。现在 PropertiesParser 只按 `=` 切，并且 L67 找不到 `=` 就 `throw FormatException`。结果是：如果检测层（L4.1）把它误判成 YAML 就丢了，就算检测对了，只要有一行用 `:` 分隔就**整文件解析失败 FormatException** → 又回 groups=[]。| server.properties 含 `key: value` 行 → 整文件 0 条 |
| **L4 服务层** | PropertiesParser.cs `Serialize` L102-L116 | **丢失注释 + 重排顺序**：`foreach (kvp in config.OrderBy(...))` 把所有行按键名排序，且不保留任何注释行。Mojang 每次启动时**不会按字母顺序重建**，而是保留注释和行顺序。结果：<br>- 用户保存一次 → 注释全丢（虽然设计文档说"Mojang 会重生成注释"，但真实世界里服主写的 `# BungeeCord IP` 这种注释是有价值的）<br>- 更严重：如果 properties 文件里出现**相同 key 的重复定义**（这很常见，因为某些插件在文件末尾追加覆盖），Parse 时**后者覆盖前者**，但序列化时只会输出一行，可能导致用户的最后一次覆盖值被前面的默认值覆盖回原顺序 → **保存一次就把覆盖值丢了**！| 保存配置导致隐藏值丢失 |
| **L5 模型层** | ConfigEditorViewModel `GroupedConfigEntries` 分组通知漏发 | `_groupUpdateTimer.Elapsed += OnGroupUpdateTimerElapsed` 调用 `UpdateGroupedEntries()`，但当 `ConfigEntries.Clear()`（切换服务器、切换文件）时只重置条目，**不立刻调 `UpdateGroupedEntries()`**，要等 20ms Timer 触发。20ms 内前端就来调 `config:getEntries`（`handleSelectFile` → `await loadEntries()`）→ 此时 `GroupedConfigEntries` 仍是旧文件的分组 → **前端短暂显示上一个文件的分组内容，然后被新的覆盖**。用户："点了 A 文件之后先看到 B 文件闪了一下才变对"。| 条目闪烁 / 错位；对某些慢速前端设备（低配机）来说窗口可能直接冻结 |

---

### 1.2 已确认的"用户操作 → 坏结果"映射表

| 用户操作 | 实际经过的链路 | 现象 | 对应缺陷 |
|---|---|---|---|
| 从 Dashboard 进入 ConfigEditor（服务器未运行） | init → getAvailableServers（有服务器）→ getSelectedServer → config:selectDefaultServer → return success → loadFileTree | 下拉显示空，文件列表"未找到配置文件" | L2（只调 SelectServerByContext，不等扫目录） |
| 在 ConfigEditor 里手动从下拉选一台服务器 | handleSelectServer → selectConfigServer → handler 扫完 return → setSelectedServerName → loadFileTree（立刻）→ 返回 selectedServerName=null → **覆盖成 null** | 下拉选中闪一下又空了 | L1（状态回滚竞态） |
| 选中一台服务器后稍等 1s → 文件列表出来了 → 点击 `paper-global.yml` | handleSelectFile → selectConfigFile → OnSelectedConfigFileChanged → LoadConfigAsync → ConfigManager.ReadConfigAsync → FlattenYaml | 条目区域永远"请先选择文件"或空的 | L4.2 NotSupportedException 吞 / L4.3 ParseProperties FormatException |
| 点 `server.properties` 编辑了 `server-port` → 保存 | saveConfig → config:save → SaveConfigCommand → SaveConfigAsync → PropertiesParser.Serialize | 保存后配置文件里的所有自定义注释都没了；最后一行的覆盖值消失 | L4.4 Properties 序列化反模式 |
| 运行了一台模组服，plugins/Essentials/config.yml 应该出现 | BuildConfigFileTree 扫描时 `dirName.StartsWith('.') continue` | plugins 目录下 `.data/` 或其他隐藏目录内的配置文件永远不出现在列表里 | L3.2 目录过滤规则过严 |

---

## 2. 整改目标

1. **用户从 Dashboard 进入 ConfigEditor → 立刻看到：下拉框已选中当前服务器、左侧文件树有配置文件**。整个链路不需要用户手动点任何"刷新/扫描"。
2. **在下拉里选任何一台服务器 → 下拉立即选中不回滚、文件树 100% 同步显示**。对"服务器未运行但 WorkingDirectory 存在"的情况也必须能扫描出所有配置文件。
3. **点击任何配置文件（扩展名 .properties/.yml/.yaml/.json/.cfg/.conf/.toml/.ini）→ 条目区域 1s 内显示分组内容**；解析失败的文件必须显示**明确原因**（例如："无法识别文件格式，请检查是否为标准 .conf 文本"），而不是静默空列表。
4. **保存配置必须是无损的**：
   - `.properties`：保留原始行顺序、保留注释、保留重复键覆盖语义（最后一个生效）。
   - `.yml/.yaml/.json`：保持原有缩进风格，不改变语义。
5. **目录过滤**：保留 world/logs/cache/libraries 的跳过规则（避免扫到 10 万文件卡死），但必须：
   - `mods` 目录的判定只对**真正的 mods 目录名**生效（任何 depth 都跳过，但子目录里如果直接包含 `.properties` 这种单独例外就不考虑 —— 只要求根目录）。
   - `plugins` 目录应该允许进入，但对 `plugins/<name>/lib/`、`plugins/<name>/data/` 的二进制 jar 不扫（靠扩展名过滤，已经做了）。
   - `.` 开头的目录只在 depth=0 跳过（避免把 `.git` 扫进去）。

---

## 3. 实现方案（按依赖顺序）

### 3.1 修复 L1 前端状态回滚 + 文件树二次拉取

**文件**：`src/frontend/src/pages/ConfigEditorPage.tsx`

**改造点**：

1. **`handleSelectServer` 不再信任 `loadFileTree` 的 selectedServerName**：
   把"同步 selectedServerName"从 loadFileTree 中解耦。后端返回的 selectedServerName 只作为兜底，**绝不覆盖当前本地设置的 name**。
   ```
   selectConfigServer(name)
   → selectedServerNameRef.current = name
   → setSelectedServerName(name)  ← 本地先确定
   → getFileTree()
   → resp.selectedServerName 作为兜底（仅当本地 name==null 时才 set）
   ```
2. **`loadFileTree` 拆为"获取文件树"+"可选地同步 selectedServerName"两个纯函数**，selectedServerName 同步加"不覆盖已有值"开关。
3. **二次拉取 120ms 后也要补偿 selectedServerName**：如果二次拉取返回 selectedServerName 但本地 name 仍是 null，才应用。
4. **handleSelectFile 同样**：先 `setSelectedConfigFile(path)`，再 loadEntries，绝不信任 resp.selectedConfigFile 覆盖当前选择（除非当前是 null）。

---

### 3.2 修复 L2 桥接层两个 selectServer 接口的同步语义

**文件**：`src/McServerGuard/Views/MainWindow.xaml.cs`

**改造点**：

1. **`config:selectDefaultServer` 重写**：
   - 先 `SelectServerByContext(...)` 找匹配 → `matched` 非空：
     - 走**完全同步赋值**链路（和 config:selectServer 一样）：
       1. `SelectedServerName / ConfigEntries.Clear / SelectedConfigFile = null / ServerWorkingDirectory = matched.WorkingDirectory`
       2. 目录存在 → `await cfg.ScanDirectoryForConfigFilesAsync(...)`
       3. `cfg.Server = matched` 触发最后的属性通知（但 OnServerChanged 会因 DisplayName 相等不重复赋值）
     - return `{ success: true, selected: true, appliedDisplayName: matched.DisplayName }`
   - 未匹配 → return `{ success: false, selected: false, error: ... }`

2. **`config:selectServer` 死分支修复**（L2118-L2129）：
   - WorkingDirectory 不存在时，**直接构造 flat list + tree**，不走 `cfg.Server = direct`：
     - `cfg.ConfigFiles = fallbackFiles;`
     - `cfg.ConfigFileTree = BuildFlatFileTree(fallbackFiles);`（新增一个静态方法，把扁平相对路径渲染成文件树）
     - 不再触发 OnServerChanged 的 Reset 流程。
   - 如果 `direct.WorkingDirectory == string.Empty`，`Path.GetRelativePath("", f)` 提前短路直接返回 `f`，不调用框架方法。

---

### 3.3 修复 L3 ViewModel：OnServerChanged 双重扫描 + 目录过滤规则

**文件**：`src/McServerGuard/ViewModels/ConfigEditorViewModel.cs`

**改造点**：

1. **OnServerChanged 不再 fire-and-forget 自己扫目录**：把 `_ = ScanDirectoryAfterServerChangedAsync(value);` 移除。改为一个 `bool _autoScanOnServerChange` 开关，由桥接层设置。
   - 如果桥接层已经 `await ScanDirectoryForConfigFilesAsync` 完再设 Server，OnServerChanged 就不要再扫一次。
   - 如果桥接层没扫（例如 WPF 侧 code-behind 直接赋值 Server），OnServerChanged 里扫目录改为同步（或者用 `await` 的异步但在属性通知前完成）。
   - 更简单做法：`_ = ScanDirectory...` 之前先检查 `!_scanning && ConfigFiles.Count == 0`，避免并发两次。
   - 新增 `private int _scanVersion;`，每次 `ScanDirectoryForConfigFilesAsync` 开始时 `_scanVersion++` 并缓存 localVersion，结束前只有 `localVersion == _scanVersion` 才把 `ConfigFileTree = treeRoot` 赋值，**防止旧的长扫描覆盖新结果**。

2. **目录过滤规则重写**（BuildConfigFileTree）：
   - 枚举黑名单目录常量：`SkipDirNames = { "mods", "world", "world_nether", "world_the_end", "logs", "cache", "libraries", "versions", "assets", "crash-reports" }`（任何 depth 都跳过）。
   - `.` 开头的目录只在 depth=0 时跳过（避免把 `.git/` 扫进去，但 plugins/.data 这种真实配置目录不拦）。
   - 新增 `MaxFilesPerServer` 保护（例如 500）：超过时中断并打一条 Warning 日志，不把 UI 卡死。

3. **LoadConfigAsync 错误 UX 改进**：
   - catch 块里除了 `ConfigEntries.Clear()`，新增**错误条目**：一条 `ServerConfigEntry` 标记 `IsValid=false, ErrorMessage="解析失败：xxx", Key="__ERROR__", Value=原始异常描述, DisplayName="⚠️ 文件解析失败"`，前端显示为醒目黄条；前端 ConfigEditorPage 在渲染 groups 时如果遇到 `key == "__ERROR__"`，渲染成 Alert 样式而不是表单控件。

---

### 3.4 修复 L4 解析层：格式检测 + Properties 序列化

#### 3.4.1 ConfigFormatDetector 误判修复

**文件**：`src/McServerGuard/Services/ConfigManagement/ConfigFormatDetector.cs`

**改造点**：

1. **`HasPropertiesFeatures` 重写**：
   - 用行正则：`^\s*[A-Za-z0-9_.\-]+\s*[=:]\s*`（允许 `=` 或 `:` 作分隔符）
   - 命中行计数，只要 ≥ 1 且 ≥ yamlLineCount * 0.5 就判 Properties 优先。
   - 对**注释行 / 空行**正确跳过。
   - 对值里含冒号的情况：在分隔符处先 split 出 key，而不是用 `Contains(':')` 直接否决整行。

2. **`HasYamlFeatures` 增强**：
   - 必须满足"缩进块层级 ≥ 2"或"存在 `---` 文档分隔符"或"存在列表项 `- ` 前缀"三选一。
   - 不允许把 `key=value:port`（Properties 行里有冒号）判成 YAML。

3. **DetectByExtension 扩展**：
   - 新增 `.conf` → Properties（先试 Properties，再试 YAML 回退）
   - 新增 `.cfg` → Properties
   - 新增 `.toml` → 保留位（暂时先 Unknown 但后续可接入 TOML 解析）
   - 新增 `.ini` → Properties

4. **`Resolve` 三级回退**：
   ```
   内容特征 → 扩展名 → 尝试逐解析器探测（先 ParseProperties，成功返回，再 FlattenYaml，成功返回，再 FlattenJson）→ 最后才 Unknown throw
   ```

#### 3.4.2 PropertiesParser 双向无损修复

**文件**：`src/McServerGuard/Services/ConfigManagement/PropertiesParser.cs`

**改造点**：

1. **Parse 支持 `:` 分隔符**：
   ```csharp
   // 找 key 结束位置：第一个未被反斜杠转义的 `=` 或 `:`
   int SplitIndex(ReadOnlySpan<char> line) { ... }
   ```
   优先第一个 `=`，如果没有再用 `:`。如果都没有按原来的处理（warning 或跳过改为 skip 单行 + Log.Debug，不再 throw FormatException 整文件挂）。
   - 遇到空键名：跳过（而不是 throw）。
   - 重复键：保留最后一个值（与 Java Properties 语义一致），但**在内部记录所有原始行的位置信息**，供序列化还原顺序用。

2. **Parse 保留原始行结构**：
   ```csharp
   public sealed class PropertiesDocument
   {
       public List<IPropertiesLine> Lines { get; } = [];
       public Dictionary<string, string> EffectiveValues { get; }
   }
   public sealed record PropertiesComment(string Text, int OriginalIndex);
   public sealed record PropertiesBlankLine(int OriginalIndex);
   public sealed record PropertiesEntry(string Key, string Value, int OriginalIndex, char Separator /* = or : */, bool IsDuplicate);
   ```
   对外的 `Parse(content)` 仍然返回 `Dictionary<string,string>`（保持 IConfigManager 契约不变），**但内部挂一个 `ConditionalWeakTable<string, PropertiesDocument>` 缓存**——以 content 哈希为 key，缓存原始文档结构；Save 时先用同路径 + 上次修改时间 / content 长度命中缓存拿原始结构，找不到时再把上次读取的原始文件读一遍重建结构。

3. **Serialize 按原始结构回写**：
   - 用 `PropertiesDocument.Lines` 顺序输出：Comment → 原注释，Blank → 空行，Entry → 用 `Key=Value`（或原 Separator 是 `:` 时用 `Key: Value`），只替换 Entry 的 Value 为用户修改后的新值。
   - **新增键**（用户通过 UI 加的不在原文件里）：追加到末尾。
   - **删除键**（把 Value 改成什么魔法表示删除？本项目目前没有删除条目的 UI，所以不用考虑；但若后续加删除，把对应 Entry 行改成注释 `# DELETED: key=oldValue` 并打 Warning）。
   - **重复键**：只改最后一条 Occurrence 的 Value，前面的保留原值（与 last-wins 语义一致，且用户下次打开 Parse 时仍然是最后一条生效）。

4. **异常安全**：
   - `Parse` 遇到单行坏数据不再 throw，而是 `Log.Debug` 该行并当 Comment 保留（回写时原样输出，不会破坏原文件）。
   - `Serialize` 前先 `File.ReadAllText` 比较 MD5（如果能拿到原内容），判断中间是否被 Minecraft 进程修改过；若已修改 → 打 Warning 并提示用户"文件已被服务器进程修改，将合并写入"或返回冲突错误（由 `SaveConfigAsync` 上层决定）。

#### 3.4.3 ConfigManager.ReadConfigAsync 的回退链 + 错误暴露

**文件**：`src/McServerGuard/Services/ConfigManagement/ConfigManager.cs`

**改造点**：

1. **`ReadConfigAsync` 解析失败多格式回退**：
   ```
   var formatsToTry = new[] { format, ConfigFormat.Properties, ConfigFormat.Yaml, ConfigFormat.Json }
       .Distinct()
       .Where(f => f != ConfigFormat.Unknown);
   Exception? lastEx = null;
   foreach (var fmt in formatsToTry)
   {
       try
       {
           // 按 fmt 解析
           return Parse(fmt, content);
       }
       catch (Exception ex) { lastEx = ex; }
   }
   // 全失败 → 抛带诊断信息的异常
   throw new ConfigParseException(
       $"所有支持的解析器都解析失败 ({filePath})。扩展名={extension}, 内容长度={content.Length}",
       lastEx);
   ```

2. **新增 `ConfigParseException : Exception`**（在 Models 或 ConfigManagement 命名空间内），包含 `HintTryFormat` 等诊断字段。

3. **`SaveConfigAsync` 写前比较**：保存前读原 content，做"脏检查"——若服务器进程已改了文件，先合并冲突（若只是同一行不同位置则最后一次写入覆盖；若 Minecraft 新增加了条目则新条目保留追加在末尾；若用户新增键冲突则打 Warning 并按用户值覆盖）。

---

### 3.5 L5 分组通知时序修复

**文件**：`src/McServerGuard/ViewModels/ConfigEditorViewModel.cs`

**改造点**：

1. **`ConfigEntries.Clear()` 后立刻同步调用 `UpdateGroupedEntries()`**，再把 `_groupUpdateTimer` 停掉（如果正启动中）。
2. **分批加载条目时（batchSize=10）**：每批结束后不立刻发分组更新，全部 batch 处理完最后 `UpdateGroupedEntries()` 一次。中间如果前端 `getEntries` 进来，读到的 `GroupedConfigEntries` 可能是 0 个分组，但至少不会"先显示旧文件再闪新文件"——新文件前 2 秒显示 0 条是可接受的（有 Loading 进度条）。
3. **Loading 标志位**：`LoadConfigAsync` 开始时 `IsLoading=true`，**所有 batch 完成后**才 `IsLoading=false`，前端可以在 `isLoading` 时显示骨架屏而不是空分组。
4. `loadEntries` 前端拿到 `resp.isLoading=true` 时，**不替换 `configGroups` 为空**，保留上一次内容直到 loading 完成。避免"闪一下空列表"。

---

## 4. 涉及修改的文件清单

| 文件 | 改动量 | 主要改动 |
|---|---|---|
| `src/frontend/src/pages/ConfigEditorPage.tsx` | 中 | handleSelectServer/loadFileTree 状态回滚修复；解析错误条目渲染为 Alert；loading 不覆盖旧内容 |
| `src/McServerGuard/Views/MainWindow.xaml.cs` | 中 | config:selectDefaultServer 同步语义；config:selectServer 死分支修复；新增 BuildFlatFileTree；ConfigParseException 透传 |
| `src/McServerGuard/ViewModels/ConfigEditorViewModel.cs` | 大 | ScanDirectoryForConfigFilesAsync 并发保护（scanVersion）；BuildConfigFileTree 目录过滤重写；LoadConfigAsync 错误条目注入；GroupedConfigEntries 立刻刷新；Loading 标志修正；ConfigEntries.Clear 后同步分组 |
| `src/McServerGuard/Services/ConfigManagement/ConfigFormatDetector.cs` | 大 | Properties/YAML 判定重写；扩展名映射扩展；Resolve 多格式回退；TryParse 顺序 |
| `src/McServerGuard/Services/ConfigManagement/PropertiesParser.cs` | 大 | Parse 支持 `:` 分隔符；PropertiesDocument 行结构保存；Serialize 无损回写；单行坏数据降级为注释 |
| `src/McServerGuard/Services/ConfigManagement/ConfigManager.cs` | 中 | ReadConfigAsync 多格式回退链；ConfigParseException；SaveConfigAsync 脏检查 + 冲突合并策略；UnflattenDictionary 对重复键的兼容 |
| 新增 `ConfigParseException.cs`（可嵌套到 ConfigManager 命名空间） | 小 | 诊断异常类型 |
| 新增 `PropertiesDocument.cs`（可嵌套到 PropertiesParser.cs 同文件） | 小 | Lines / EffectiveValues 模型 |

---

## 5. 风险与回退

| 风险 | 说明 | 应对 |
|---|---|---|
| Properties 序列化重写破坏用户文件 | 用户已有 server.properties 在保存后被改坏（最严重风险） | 写前自动备份：`File.Copy(path, path + ".bak", overwrite: true)`；序列化后再 Read 回来比较 EffectiveValues，不一致则拒绝写入并恢复 bak |
| 目录过滤放宽后 UI 卡死 | plugins 下有 2000 个 jar，虽然扩展名不匹配，但枚举文件本身仍耗时 | MaxFilesPerServer=500 硬上限；用 `Directory.EnumerateFiles(..., SearchOption.TopDirectoryOnly)` 加 depth 限制，每一层再递归，而不是一次性 SearchAll |
| 前端状态机改动导致"不加载条目" | 把"loading 时不覆盖旧内容"改成"新文件没加载完就显示旧条目"会混淆 | 加一个 `loadingForFile` 状态，当 `loadingForFile != null && selectedConfigFile != loadingForFile` 时才用旧内容，否则显示骨架屏 |
| ConfigParseException 暴露后 UI 溢出 | 堆栈文本太长撑破布局 | Alert 固定 max-height + overflow-y:auto，只显示 Message 一行，详情可展开 |
| 桥接层改动破坏 WebView2 <-> WPF 的 JSON 契约 | 前端改了 `getFileTree()` 的字段顺序 / 新字段 | 所有新增字段全部 nullable，旧前端可以忽略；后端不删任何原字段 |

**回退策略**：任何一个模块改动冒烟不通过时，分别回退到修改前版本——PropertiesParser 改回纯 Dictionary + 字母顺序（至少能工作），ConfigFormatDetector 回退原逻辑，前端 `handleSelectServer` 回退原"先拉取再 set"方案。

---

## 6. 验收用例（必跑）

按用户实际场景逐条验收：

### T1：从 Dashboard 直接进 ConfigEditor（服务器未运行，是 KnownServer）
- **期望**：下拉立即显示该服务器 DisplayName；左侧文件树列出 server.properties / spigot.yml 等。

### T2：下拉手动选一台服务器
- **期望**：下拉选中后保持不动（不闪不回滚）；文件树 200ms 内刷新。

### T3：点击 `.properties` 文件（含 `key: value` 形式一行）
- **期望**：Parse 正确，分组显示全部项；不抛 FormatException。

### T4：点击扩展名 `.conf` 的模组配置（内容是 Properties 风格）
- **期望**：Resolve 最终能按 Properties 解析成功，不抛 NotSupportedException；条目正常显示。

### T5：修改 server.properties 保存
- **期望**：重新打开 server.properties，原注释、原行顺序、重复键的覆盖语义都保持不变；用户修改的项值被正确写回。

### T6：点击解析失败的文件（例如手工把 YAML 写坏）
- **期望**：条目区显示红色 Alert，说明"解析失败：YAML 语法错误在第 N 行"，而不是空列表。

### T7：保存后与 Minecraft 进程的文件修改冲突
- **期望**：保存时检测到文件已被外部修改，打 Warning 并合并（Minecraft 新增行保留，用户修改的项正确覆盖）。

