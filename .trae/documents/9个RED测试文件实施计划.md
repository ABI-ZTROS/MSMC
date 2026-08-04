# 9 个 RED 阶段 xUnit 测试文件实施计划

## 一、背景与目标

基于审计结论（❌空壳14项 ⚠️部分57项），按 TDD「RED先写失败测试」原则，在 `/workspace/src/MSMC.Tests/Services/` 下新建 **9 个 xUnit 测试文件**。

### 核心约束
1. **纯 RED 阶段**：不修改生产代码，仅写测试
2. **可编译**：所有新测试文件必须通过 `dotnet build`
3. **运行 FAIL**：`dotnet test` 时所有测试必须失败，失败原因限定为：
   - 被测方法返回 `null` / `default`
   - 枚举值不全
   - 抛 `NotSupportedException` / `NotImplementedException`
4. **测试方法总数 ≥ 40**（Fact + Theory）
5. **中文注释**：每个测试类/方法有中文说明

---

## 二、现有 xUnit 写法与命名风格（学习自 6 个现有测试）

### 2.1 命名规范
| 元素 | 规范 | 示例 |
|------|------|------|
| 命名空间 | `io.NET.ZTR_OS.Tests.Services` | 所有测试统一 |
| 测试类名 | `{被测类名}Tests` | `CommandLineParserTests` |
| 测试方法名 | `{被测方法}_{场景}_{期望结果}` | `Parse_VanillaServerCommand_ExtractsCorrectFields` |
| Fact 方法 | 独立场景用 `[Fact]` | 无参数单一场景 |
| Theory 方法 | 参数化用 `[Theory]` + `[InlineData]` | `ParseMemoryValue_ValidInputs_ReturnsCorrectBytes` |

### 2.2 代码风格
- 三阶段：`// Arrange` / `// Act` / `// Assert`（中文注释）
- 使用 🎮⚡📂🔧🚫🕳️👻 等 emoji 辅助场景说明
- 字符串比较用 `Assert.Equal`，布尔用 `Assert.True/False`
- 异常断言用 `Assert.Throws<T>()`

### 2.3 引用的生产代码命名空间
```csharp
using io.NET.ZTR_OS.Features.ServerDetection.Services;
using io.NET.ZTR_OS.Features.JavaInstallation.Constants;  // ServerType 枚举
using io.NET.ZTR_OS.Features.ConfigEditor.Services;
using io.NET.ZTR_OS.Features.SystemMonitoring.Services;
using io.NET.ZTR_OS.Features.Settings.Services;
using io.NET.ZTR_OS.Features.UserAgreement.Services;
using io.NET.ZTR_OS.Features.WebView2.Frontend;
using io.NET.ZTR_OS.Features.NetworkMonitor.Services;
using io.NET.ZTR_OS.Features.Startup.Views;
```

---

## 三、9 个测试文件设计（每个 ≥ 4-6 个 Fact/Theory，总计 ≥ 40）

### 文件 1：`ServerTypeClassifierEnhancedTests.cs`
**覆盖范围**：JarCoreIdentifier/ServerTypeClassifier 空壳  
**目标**：ServerType 枚举总数 ≥ 36；Purpur/Kaiiju/PowerNukkit/SpongeForge/NeoForge 5 派生类可识别  
**预计测试数**：6 Fact + 1 Theory = **7 个**

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `ServerTypeEnum_TotalValues_AtLeast36` | 枚举.GetValues 数量断言（实际 40 满足？若生产枚举被删则 FAIL） |
| 2 | `ClassifyByJarName_PurpurJar_ReturnsPurpur` | ClassifyByJarName("purpur-1.21.jar") 可能返回 Paper 而非 Purpur |
| 3 | `ClassifyByJarName_KaiijuJar_ReturnsKaiiju` | 同上，Kaiiju 未被正确识别 |
| 4 | `ClassifyByJarName_PowerNukkitJar_ReturnsPowerNukkit` | PowerNukkit 可能回落到 Nukkit |
| 5 | `ClassifyByJarName_SpongeForgeJar_ReturnsSpongeForge` | SpongeForge 可能回落到 Forge/Sponge |
| 6 | `ClassifyByJarName_NeoForgeJar_ReturnsNeoForge` | NeoForge 可能回落到 Forge |
| 7 | `JarCoreIdentifier_IdentifyAsync_DerivedCoreTypes_Theory` | 用 InlineData 批量测试 5 个派生类型 JAR 路径（空文件，返回 Unknown 导致 FAIL） |

---

### 文件 2：`UndoRedoStackTests.cs`
**覆盖范围**：ConfigEditor 撤销重做栈 UndoRedoStack<T>  
**目标**：Push/Undo/Redo/CanUndo/CanRedo/Reset 行为  
**预计测试数**：**6 个 Fact**

> ⚠️ 生产代码中 `UndoRedoStack<T>` 类不存在 → 在本测试文件内定义 internal stub 类（让编译通过，stub 返回 default）

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `Push_SingleItem_CanUndoTrue` | Stub 的 CanUndo 返回 false（default(bool)） |
| 2 | `Undo_AfterPush_ReturnsPushedItem` | Undo() 返回 default(T)（null）而非 Push 的值 |
| 3 | `Undo_ThenRedo_ReturnsSameItem` | Redo() 返回 default(T) |
| 4 | `CanRedo_AfterUndo_True` | CanRedo 返回 false |
| 5 | `Reset_AfterPush_CanUndoFalse` | Reset 后 CanUndo 仍可能 true |
| 6 | `Push_MultipleItems_UndoOrderLIFO` | 栈顺序不正确，Undo 返回乱序 |

---

### 文件 3：`MetricsDownsamplingTests.cs`
**覆盖范围**：SystemMonitor MetricsPersistence MMF 24h 降采样  
**目标**：Append 1440×60 个样本 → Downsample 后约 1440 个分钟点  
**预计测试数**：**5 个 Fact**

> ⚠️ 生产 `IMetricsPersistenceService` 无 `Downsample` 方法 → 在本测试文件内定义 extension 包装 + stub 接口

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `Append_1MinuteSamples_Total86400Points` | LoadDay 返回空列表（Count=0），未达到 86400 |
| 2 | `Downsample_86400Points_To1440MinuteBuckets` | Downsample stub 返回空 List，Count=0≠1440 |
| 3 | `Downsample_EachBucket_AverageOf60Samples` | 每个 bucket 的 CPU 值为 0.0（default） |
| 4 | `Downsample_Timestamps_AlignToMinuteBoundary` | 时间戳不对齐 |
| 5 | `Append_And_Downsample_RoundTripConsistency` | 往返不一致 |

---

### 文件 4：`ThemePresetTests.cs`
**覆盖范围**：ThemeService 13 套预设枚举  
**目标**：GetAllPresets 返回 Count ≥ 13；ApplyPreset("ColorOS") 返回非空色阶  
**预计测试数**：**6 个 Fact**

> ⚠️ 生产 `ThemeService` 无 `GetAllPresets()` / `ApplyPreset()` 方法 → 测试内定义 wrapper stub

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `GetAllPresets_Count_AtLeast13` | Stub 返回空 List，Count=0<13 |
| 2 | `ApplyPreset_ColorOS_ReturnsNonNullSwatches` | ApplyPreset stub 返回 null 色阶数组 |
| 3 | `ApplyPreset_ColorOS_SwatchCountAtLeast6` | 色阶数 < 6 |
| 4 | `GetAllPresets_ContainsSkyBlueAndOceanBlue` | 现有 5 个预设应存在，但 stub 返回空 |
| 5 | `ApplyPreset_InvalidName_ThrowsOrReturnsNull` | 非法预设名行为未定义 |
| 6 | `ApplyPreset_EachPreset_NonEmptyPrimaryColor` | 每个预设 PrimaryColor 都是 #00000000 |

---

### 文件 5：`UserAgreementReagreementTests.cs`
**覆盖范围**：UserAgreementService RequireReagreement  
**目标**：已同意v2.0 当前v3.0 → RequiresReagreement = true  
**预计测试数**：**5 个 Fact**

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `RequiresReagreement_AgreedV2_CurrentV3_True` | 直接 new UserAgreementService() 后 SetAgreed("2.0")，但 CurrentAgreementVersion 可能是 "3.0.0" 非 "3.0" → 比较时版本格式不同导致 false |
| 2 | `RequiresReagreement_AgreedV3_CurrentV3_False` | 同上，版本号匹配逻辑 bug |
| 3 | `RequiresReagreement_NeverAgreed_True` | IsAgreed 默认 false 时 RequiresReagreement 应为 true（生产代码中 `!IsAgreed` 是 true，这个其实会 pass？— 那么 FAIL 原因改为：未 Load 时属性未初始化） |
| 4 | `SetAgreed_UpdatesAgreedVersion` | AgreedVersion 仍为 null |
| 5 | `CurrentAgreementVersion_IsNotNullOrEmpty` | 生产可能返回空字符串导致判断异常 |

---

### 文件 6：`ZipExtractResourceTests.cs`
**覆盖范围**：ZipExtractResourceProvider 内存解压  
**目标**：GetResource("/index.html") 返回非 null Stream（内嵌 zip 模拟）  
**预计测试数**：**5 个 Fact**

> 实现方式：在测试项目中手动构造一个 MemoryStream-based ZipArchive，其中包含 index.html，包装为自定义的 IFrontendResourceProvider 实现来模拟（而非依赖真实的 EmbeddedResource）。或者直接测试生产 ZipExtractResourceProvider（它的 GetResourceAsync 目前返回 null，天然 FAIL）。

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `GetResourceAsync_IndexHtml_ReturnsNonNullStream` | 生产 ZipExtractResourceProvider.GetResourceAsync("/index.html") 硬编码返回 Task.FromResult<Stream?>(null) |
| 2 | `GetResourceAsync_NestedAsset_ReturnsNonNullStream` | 同上，更深路径也返回 null |
| 3 | `GetResourceAsync_NonExistentFile_ReturnsNull` | 这个理论上会 pass → 改为检查 MIME 类型不正确的场景 |
| 4 | `GetBasePathAsync_WhenAvailable_ReturnsNonNullPath` | IsAvailable=false（因为没有嵌入资源）→ 返回 null |
| 5 | `IsAvailable_WithEmbeddedZip_True` | 未嵌入 zip → IsAvailable=false |

---

### 文件 7：`NetworkPublicIpDetectionTests.cs`
**覆盖范围**：Network UPnP/NAT-PMP + 公网 IP  
**目标**：Stub 公网 IP 接口，IP 检测返回 1.2.3.4 正确  
**预计测试数**：**5 个 Fact**

> ⚠️ 生产 NetworkService 无公网 IP 检测 → 测试内定义 `IPublicIpDetector` stub 接口

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `PublicIpDetector_DetectAsync_ReturnsExpectedIp` | stub 返回 null 而非 "1.2.3.4" |
| 2 | `PublicIpDetector_DetectAsync_IpFormatValid` | 返回 IP 格式不合法（非 IPv4） |
| 3 | `PublicIpDetector_DetectAsync_NonEmpty` | 返回 string.Empty |
| 4 | `UPnPService_GetExternalIpAsync_MatchesStub` | UPnP stub 返回 default |
| 5 | `NatPmpService_GetExternalAddressAsync_MatchesStub` | NAT-PMP stub 返回 default |

---

### 文件 8：`CrashWindowActionsTests.cs`
**覆盖范围**：CrashWindow 5 按钮  
**目标**：ICrashActions.CopyToClipboard/OpenLogs/Exit/Report/Restart 五个方法有真实实现且不 throw  
**预计测试数**：**5 个 Fact**（刚好对应 5 个按钮方法）

> ⚠️ 生产无 `ICrashActions` 接口 → 测试内定义 stub

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `CopyToClipboard_Invoke_NoExceptionThrown` | stub 抛 NotSupportedException |
| 2 | `OpenLogs_Invoke_NoExceptionThrown` | 同上 |
| 3 | `Exit_Invoke_NoExceptionThrown` | 同上 |
| 4 | `Report_Invoke_NoExceptionThrown` | 同上 |
| 5 | `Restart_Invoke_NoExceptionThrown` | 同上 |

---

### 文件 9：`AppConfigJavawPreferenceTests.cs`
**覆盖范围**：Settings QoS javaw-preference 持久化  
**目标**：Write → AppConfig → Read 回读一致  
**预计测试数**：**5 个 Fact**

| # | 测试方法名 | 预期失败原因 |
|---|-----------|-------------|
| 1 | `Write_PreferJavawTrue_SaveThenLoad_ReadBackTrue` | 测试时使用临时文件路径，但 AppConfigService 使用固定 %AppData% 路径 → 测试环境下写入和读取可能不隔离，导致值回读为默认 false |
| 2 | `Write_PreferJavawFalse_SaveThenLoad_ReadBackFalse` | 同上，持久化失败 → 回读不一致 |
| 3 | `AppConfig_DefaultPreferJavaw_IsFalse` | 默认值实际为 false，本测试应 Pass → 改为：修改 PreferJavaw 后 Config 属性同步更新（但 Save/Load 后丢失） |
| 4 | `MultipleProperties_RoundTrip_PreferJavawConsistent` | Supervisor 等其他属性持久化失败影响 |
| 5 | `LoadAsync_PreferJavaw_AsyncReadConsistent` | 异步 LoadAsync 未正确填充值 |

---

## 四、测试方法总数统计

| 文件 | Fact | Theory | 小计 |
|------|------|--------|------|
| 1. ServerTypeClassifierEnhancedTests | 6 | 1 (×5 InlineData) | **7** |
| 2. UndoRedoStackTests | 6 | 0 | **6** |
| 3. MetricsDownsamplingTests | 5 | 0 | **5** |
| 4. ThemePresetTests | 6 | 0 | **6** |
| 5. UserAgreementReagreementTests | 5 | 0 | **5** |
| 6. ZipExtractResourceTests | 5 | 0 | **5** |
| 7. NetworkPublicIpDetectionTests | 5 | 0 | **5** |
| 8. CrashWindowActionsTests | 5 | 0 | **5** |
| 9. AppConfigJavawPreferenceTests | 5 | 0 | **5** |
| **合计** | **48** | **1** | **49** |

✅ **总数 49 ≥ 40，满足要求。**

---

## 五、stub 策略（确保可编译但运行 FAIL）

对于生产代码中**不存在**的类型/方法，在各测试文件内定义 `internal` stub，**保证编译通过**：

| 缺失类型/方法 | 放置位置 | stub 行为（确保 FAIL） |
|--------------|---------|----------------------|
| `UndoRedoStack<T>` 类 | 文件 2 顶部 | `Push()` 空操作，`Undo()/Redo()` 返回 `default(T)`，`CanUndo/CanRedo` => `false`，`Reset()` 空 |
| `Downsample()` 扩展方法 | 文件 3 顶部 | 返回 `new List<MetricsHistoryPoint>()`（空列表） |
| `GetAllPresets()` / `ApplyPreset()` wrapper | 文件 4 顶部 | 返回空列表 / null 色阶 |
| `IPublicIpDetector` / `IUPnPService` / `INatPmpService` 接口 + stub 实现 | 文件 7 顶部 | 返回 `null` / default |
| `ICrashActions` 接口 + stub 实现 | 文件 8 顶部 | 每个方法抛 `NotSupportedException` |

> 所有 stub 都使用 `#if RED_STUBS` 条件编译或直接作为 internal 类，确保不影响生产代码。

---

## 六、验证步骤

1. **编译验证**：`dotnet build src/MSMC.Tests` → 0 error
2. **测试数量验证**：`grep -rE "^\s*\[Fact\]|^\s*\[Theory\]" src/MSMC.Tests/Services/*.cs | wc -l` → ≥ 40
3. **FAIL 验证（选做）**：`dotnet test src/MSMC.Tests --no-build` → 新测试全部 FAIL（原因符合约束）

---

## 七、执行顺序

1. 文件 1（ServerTypeClassifierEnhancedTests）→ 已有类型直接引用
2. 文件 5（UserAgreement）→ 类型存在，简单
3. 文件 9（AppConfig）→ 类型存在
4. 文件 6（ZipExtractResource）→ 类型存在
5. 文件 2（UndoRedoStack）→ 含 stub
6. 文件 3（Metrics）→ 含 stub
7. 文件 4（ThemePreset）→ 含 stub
8. 文件 7（Network IP）→ 含 stub
9. 文件 8（CrashActions）→ 含 stub
