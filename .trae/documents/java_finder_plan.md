# Java 版本管理器 实施计划

## 一、现状分析

### 1.1 已有 JavaFinder
项目中已存在 `JavaFinder.cs`（`Services/ServerDetection/JavaFinder.cs`），是一个 **static class**，支持 5 种查找策略：
1. **JAVA_HOME** 环境变量
2. **Windows 注册表**（HKLM/HKCU 的 JavaSoft/JDK/JRE 等 8 个路径）
3. **PATH** 环境变量
4. **where.exe 命令**
5. **常见安装目录扫描**（Program Files 下的 Java/Eclipse Adoptium/Microsoft/BellSoft/Azul/Amazon Corretto 等）

并提供了 `VerifyJava()` 方法，能提取版本号、厂商、架构等信息。

### 1.2 存在的问题
- **静态类无法 DI 注入**，单元测试困难
- **只找 java.exe**，不支持 javaw.exe，也没有优先级配置
- **安装路径不在 C:\\Program Files 时可能漏查**（如用户装到 D 盘、E 盘等）
- **无用户手动配置机制**，找不到时只能干瞪眼
- **无 Java 版本管理 UI**，用户看不到系统里有哪些 Java
- **ServerManagerService 已在使用** `JavaFinder.FindJava()`，但仅作 fallback

### 1.3 受影响的模块
- `ServerManagerService.StartServer()` —— 启动服务器时需要 Java 路径
- `ServerDetectionViewModel` —— 启动已知/当前服务器
- `SettingsViewModel` —— 设置页面（新增 Java 管理面板）
- `AppConfig` —— 需要新增 Java 配置项

---

## 二、目标与范围

### 2.1 核心目标
1. **JavaFinder 服务化**：从 static class 改为 DI 注入的接口 + 实现
2. **增强查找策略**：覆盖用户自定义安装路径（非 C 盘）
3. **支持 javaw.exe**：优先 javaw.exe，也能切换
4. **完整 Java 版本管理器**：设置页面展示所有已发现的 Java，支持手动添加、设为默认、删除手动添加项
5. **持久化配置**：用户选择的默认 Java 路径、手动添加的路径保存到配置文件

### 2.2 不在范围
- 自动下载/安装 Java（仅查找和管理已安装的）
- 服务器级别的 Java 选择（先只做全局默认，服务器级别后续再说）

---

## 三、详细设计

### 3.1 IJavaFinder 接口 + JavaFinder 服务化

**新文件**：`Services/IJavaFinderService.cs`

```csharp
public interface IJavaFinderService
{
    JavaInstallation? FindDefault();
    List<JavaInstallation> FindAll();
    JavaInstallation? Verify(string javaPath);
    void AddCustomPath(string javaHomePath);
    void RemoveCustomPath(string javaHomePath);
    List<string> GetCustomPaths();
    string? DefaultJavaPath { get; set; }
}
```

**改造**：`Services/ServerDetection/JavaFinder.cs`
- 从 `public static class JavaFinder` 改为 `public class JavaFinderService : IJavaFinderService`
- 构造函数注入 `IAppConfigService`（读取/保存自定义路径和默认 Java）
- 所有 static 方法改为实例方法
- `FindJava()` → `FindDefault()`（先查用户指定的默认，再自动扫描）
- `FindAllJavaInstallations()` → `FindAll()`
- `JavaInstallation` 类保留为嵌套或移到 Models

### 3.2 增强查找策略

在现有 5 种策略基础上新增：

| 策略 | 说明 |
|------|------|
| **用户自定义路径** | 用户在设置页面手动添加的 Java 安装目录，优先级最高 |
| **环境变量扩展** | 除 JAVA_HOME 外，还查 `JDK_HOME`、`JRE_HOME` |

**说明**：不做全磁盘扫描（可能卡 IO），主要靠自动扫描（注册表、PATH、环境变量、常见安装目录）+ 用户手动添加覆盖剩余场景。

### 3.3 javaw.exe 支持

- `GetJavaExecutable()` 方法增加参数 `preferJavaw`（默认 true）
- `FindAll()` 返回的每个 `JavaInstallation` 同时记录 `JavaPath`（java.exe）和 `JavawPath`（javaw.exe）
- `FindDefault()` 根据配置返回 java.exe 或 javaw.exe 路径
- 新增配置项 `PreferJavaw`（默认 true）

### 3.4 AppConfig 扩展

在 `AppConfig` 类中新增：

```csharp
/// <summary>用户手动添加的 Java 安装目录列表</summary>
public List<string> CustomJavaPaths { get; set; } = [];

/// <summary>用户选择的默认 Java 可执行文件完整路径</summary>
public string DefaultJavaPath { get; set; } = string.Empty;

/// <summary>是否优先使用 javaw.exe（无控制台窗口）</summary>
public bool PreferJavaw { get; set; } = true;
```

### 3.5 SettingsViewModel 扩展

新增 Java 管理相关的属性和命令：

| 属性/命令 | 类型 | 说明 |
|----------|------|------|
| `JavaInstallations` | `ObservableCollection<JavaInstallationViewModel>` | 所有 Java 列表 |
| `SelectedJava` | `JavaInstallationViewModel?` | 当前选中的 Java |
| `IsScanningJava` | `bool` | 是否正在扫描 |
| `ScanJavaCommand` | `IRelayCommand` | 重新扫描系统中的 Java |
| `SetDefaultJavaCommand` | `IRelayCommand` | 将选中的 Java 设为默认 |
| `AddJavaPathCommand` | `IRelayCommand` | 打开文件夹对话框手动添加 Java 路径 |
| `RemoveJavaPathCommand` | `IRelayCommand` | 删除手动添加的 Java 路径 |
| `PreferJavaw` | `bool` | 是否优先使用 javaw.exe |
| `DefaultJavaDisplayText` | `string` | 当前默认 Java 的显示文本 |

### 3.6 SettingsPage.xaml 新增 Java 管理区域

在外观设置卡片下方新增「Java 运行环境」卡片，包含：
- 顶部：当前默认 Java 显示 + "重新扫描" 按钮
- 中部：Java 列表（ListBox/ItemsControl），每项显示版本号、厂商、路径、架构、是否默认
- 底部："添加 Java 路径" 按钮 + "优先使用 javaw.exe" 开关
- 选中项右侧有「设为默认」按钮（手动添加的还有「删除」按钮）

### 3.7 ServerManagerService 集成改造

```csharp
// 改造前
var javaExe = string.IsNullOrEmpty(server.JavaPath) 
    ? JavaFinder.FindJava() 
    : server.JavaPath;

// 改造后
var javaExe = string.IsNullOrEmpty(server.JavaPath)
    ? _javaFinderService.FindDefault()?.JavaPath
    : server.JavaPath;
```

构造函数注入 `IJavaFinderService`。

---

## 四、文件变更清单

### 新增文件
| 文件 | 说明 |
|------|------|
| `Services/IJavaFinderService.cs` | Java 查找服务接口契约 + JavaInstallation 模型 |
| `ViewModels/JavaInstallationViewModel.cs` | Java 安装项的 ViewModel（用于列表绑定） |
| `Services/JavaFinderService.cs` | Java 查找服务实现（从原 ServerDetection/JavaFinder.cs 迁移并服务化） |

### 修改文件
| 文件 | 改动 |
|------|------|
| `Services/ServerDetection/JavaFinder.cs` | 删除（迁移到 Services 根命名空间） |
| `Services/IAppConfigService.cs` | AppConfig 新增 CustomJavaPaths / DefaultJavaPath / PreferJavaw |
| `ViewModels/SettingsViewModel.cs` | 新增 Java 管理相关属性和命令 |
| `Views/SettingsPage.xaml` | 新增 Java 运行环境设置卡片（ListBox + 自定义项模板） |
| `Views/SettingsPage.xaml.cs` | 绑定 Java 管理区域事件（文件夹对话框等） |
| `Services/ServerDetection/ServerManagerService.cs` | 注入 IJavaFinderService，替换静态调用 |
| `App.xaml.cs` | 注册 IJavaFinderService / JavaFinderService 为 Singleton |

---

## 五、实施步骤

### 步骤 1：服务化改造 + 接口定义
- 新增 `IJavaFinderService.cs` 接口
- 将 `JavaFinder` 从 static 改为实例类，实现接口
- 构造函数注入 `IAppConfigService`
- 注册到 DI 容器

### 步骤 2：增强查找策略 + javaw.exe 支持
- 新增 JDK_HOME / JRE_HOME 环境变量查找
- 修改 `GetJavaExecutable` 支持 preferJavaw 参数
- `JavaInstallation` 新增 `JavawPath` 属性
- `FindDefault()` 逻辑：用户指定默认 → 自定义路径 → 自动扫描（选版本最高的 64 位）
- `FindAll()` 结果按版本降序 + 64 位优先排序

### 步骤 3：AppConfig 扩展 + 持久化
- `AppConfig` 新增 3 个配置项
- 确保 JSON 序列化兼容（新增字段有默认值）
- 配置加载/保存流程无需改动（AppConfigService 已处理整个对象）

### 步骤 4：SettingsViewModel Java 管理逻辑
- 新增 `JavaInstallationViewModel`
- SettingsViewModel 新增 Java 列表、选中、扫描、设为默认、添加、删除等命令
- 加载时自动扫描一次并填充列表

### 步骤 5：SettingsPage UI
- XAML 新增 Java 运行环境卡片
- Code-behind 处理文件夹对话框、列表项命令等
- 动画统一用 A6 已有的 PlayPageEntrance

### 步骤 6：ServerManagerService 集成
- 构造函数注入 `IJavaFinderService`
- 替换静态 `JavaFinder.FindJava()` 调用
- 替换静态 `JavaFinder.VerifyJava()` 调用

### 步骤 7：CI 编译验证
- 提交代码触发 CI
- 修复编译错误（如果有）

---

## 六、风险与注意事项

### 6.1 扫描性能
- 不做全磁盘扫描，避免 IO 卡顿
- 注册表、PATH、环境变量、常见目录扫描均为快速操作
- 首次扫描放后台线程，不阻塞 UI

### 6.2 JavaFinder 静态调用迁移
- 全局搜索 `JavaFinder.` 确认所有调用点都已替换
- 编译期就能发现遗漏（static 改实例后调用会报错）

### 6.3 配置兼容性
- 新增字段有默认值，旧配置文件加载不会出错
- `CustomJavaPaths` 默认为空列表，`DefaultJavaPath` 默认为空，`PreferJavaw` 默认为 true

### 6.4 Windows 注册表权限
- 现有代码已处理了权限异常（try-catch），继续保留

---

## 七、验证方式

1. **编译通过**：GitHub Actions CI build 成功
2. **功能验证**：
   - 安装 Java 到非 C 盘目录（如 D:\Java\jdk-21），重新扫描应能找到
   - 设置页面手动添加一个 Java 路径，重启后配置应保留
   - 设为默认后，启动服务器应使用选中的 Java
   - 切换 PreferJavaw 开关，启动的进程应该对应变化
3. **回退兼容**：删除新增配置项后，旧配置仍能正常加载
