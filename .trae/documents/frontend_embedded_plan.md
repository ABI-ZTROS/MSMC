# 前端资源内嵌方案（B 模式优先 + C 模式兜底）

## 1. 现状调研

### 1.1 当前加载方式
- **开发环境**：读取 `src/frontend/dist/` 目录（开发时本地构建）
- **发布环境**：`csproj` 中使用 `<None Include="..\frontend\dist\**" CopyToOutputDirectory="PreserveNewest" Link="wwwroot\..." />` 把前端文件复制到输出目录的 `wwwroot/` 子目录
- **WebView2 加载**：通过 `SetVirtualHostNameToFolderMapping` 将 `msmc.local` 虚拟域名映射到前端文件夹，用 `https://msmc.local/index.html` 加载
- **问题**：发布后 `wwwroot/` 是一堆散文件，不够"整"；用户希望内嵌到程序集中

### 1.2 现有资源嵌入方式
- csproj 中已有 `<Resource>` 方式嵌入图片和字体（WPF 资源字典风格）
- 项目已启用 `PublishSingleFile=true` 单文件发布

## 2. 方案设计：B 模式优先 + C 模式兜底

### 2.1 B 模式（WebResourceRequested 拦截）⭐ 优先实现

**原理**：前端所有文件作为 **嵌入资源（EmbeddedResource）** 打进程序集，注册 WebView2 的 `WebResourceRequested` 事件，拦截对虚拟域名的 HTTP 请求，从程序集资源中读取内容并构造响应。

**优点**：
- 真正的零磁盘写入，纯内存提供
- 单文件发布后所有资源都在 exe 里
- 性能好（内存读取 < 磁盘读取）

**实现要点**：
1. **资源嵌入**：将 `frontend/dist/**` 全部标记为 `EmbeddedResource`，逻辑名用 `wwwroot.{相对路径}` 格式（路径分隔符用 `.`）
2. **MIME 类型映射**：根据文件扩展名返回正确的 `Content-Type`
   - `.html` → `text/html; charset=utf-8`
   - `.js` → `application/javascript; charset=utf-8`
   - `.css` → `text/css; charset=utf-8`
   - `.svg` → `image/svg+xml`
   - `.png`/`.jpg`/`.ico` → 对应 image 类型
   - `.woff`/`.woff2`/`.ttf` → 对应 font 类型
   - `.json` → `application/json; charset=utf-8`
3. **WebResourceRequested 拦截**：
   - 在 WebView2 初始化完成后，对 `https://msmc.local/*` URL 注册拦截
   - 收到请求后，从 URL 解析出文件路径
   - 从嵌入资源中读取文件流
   - 构造 `CoreWebView2WebResourceResponse` 返回（含正确状态码、Headers、Content）
4. **404 处理**：找不到资源时返回 404 响应，避免 WebView2 报网络错误

### 2.2 C 模式（嵌入 zip + 临时目录解压）🛡️ 兜底方案

**原理**：前端打包成单个 `wwwroot.zip` 作为嵌入资源，程序启动时解压到 `%TEMP%/MSMC/wwwroot_<版本号>/`，然后虚拟主机映射到这个临时目录。

**优点**：
- 实现最简单，几乎没有兼容性问题
- 与现有虚拟主机方案 100% 兼容
- 带版本号的目录名避免多版本冲突

**缺点**：
- 首次启动有解压开销（~100-200ms，可接受）
- 会在临时目录留下文件（不影响，但不"纯"）

**兜底触发条件**：
如果 B 模式在某些环境下有兼容性问题（比如 WebView2 旧版本 `WebResourceRequested` 行为异常），则自动降级到 C 模式。

## 3. 优先级策略

```
开发环境（dist 目录存在） → 直接读文件夹（当前方式，方便热重载调试）
       ↓ 不存在
B 模式（嵌入资源 + WebResourceRequested） → 优先使用
       ↓ 失败（兼容性问题/资源找不到）
C 模式（嵌入 zip + 临时目录解压） → 兜底保证
       ↓ 也失败
内置测试页面（已有的 LoadTestPage） → 最后防线
```

## 4. 要修改的文件/模块

### 4.1 项目文件
| 文件 | 修改内容 |
|------|----------|
| `McServerGuard.csproj` | 1. 添加 `EmbeddedResource` 包含 `frontend/dist/**`（资源逻辑名 `wwwroot.xxx`）<br>2. 移除现有的 `None Include="..\frontend\dist\**"` 方式<br>3. 添加生成 zip 的 Target（给 C 模式备用） |

### 4.2 新增服务
| 文件 | 职责 |
|------|------|
| `Services/Frontend/IFrontendResourceProvider.cs` | 前端资源提供器接口（抽象 B/C 两种模式） |
| `Services/Frontend/EmbeddedResourceProvider.cs` | B 模式实现：从嵌入资源读取 + WebResourceRequested 拦截 |
| `Services/Frontend/ZipExtractResourceProvider.cs` | C 模式实现：从 zip 解压到临时目录 |
| `Services/Frontend/FrontendResourceProviderFactory.cs` | 工厂类：按优先级选择合适的提供器 |

### 4.3 修改现有服务
| 文件 | 修改内容 |
|------|----------|
| `Services/WebView2/IWebView2BridgeService.cs` | 增加 `IFrontendResourceProvider` 依赖，支持资源拦截注册 |
| `Services/WebView2/WebView2BridgeService.cs` | 实现 WebResourceRequested 事件绑定（委托给资源提供器） |
| `Views/MainWindow.xaml.cs` | 初始化时用工厂获取资源提供器，替代原来直接找文件夹的逻辑 |

### 4.4 CI 工作流
| 文件 | 修改内容 |
|------|----------|
| `.github/workflows/ci.yml` | 确保发布前前端已构建（dist 存在），否则嵌入空的，会出问题 |

## 5. 详细步骤

### Step 1：定义资源提供器接口
- 创建 `IFrontendResourceProvider` 接口
- 方法：`Task<string> GetBasePathAsync()` — 返回可用于虚拟主机映射的本地文件夹路径（C 模式），或返回 `null` 表示用拦截模式（B 模式）
- 方法：`Task<Stream?> GetResourceAsync(string relativePath)` — B 模式下获取资源流
- 属性：`string ModeName { get; }` — 模式名称（用于日志）

### Step 2：实现 B 模式（EmbeddedResourceProvider）
- 读取程序集的 `GetManifestResourceNames()` 建立路径映射
- 根据 URL 相对路径查找嵌入资源
- 实现 MIME 类型映射表
- 提供 `GetResourceStream(relativePath)` 和 `GetMimeType(path)` 方法

### Step 3：实现 C 模式（ZipExtractResourceProvider）
- 从嵌入资源读取 `wwwroot.zip`
- 计算版本哈希（程序集版本 + zip CRC）
- 解压到 `%TEMP%/MSMC/wwwroot_<hash>/`
- 如果已存在且完整则跳过解压
- 返回本地路径

### Step 4：实现工厂类
- 优先级逻辑：目录文件 → B 模式 → C 模式
- 逐级尝试，找到第一个可用的

### Step 5：集成到 WebView2BridgeService
- 初始化时注册 `WebResourceRequested`（B 模式）
- 或设置虚拟主机映射（C 模式/文件夹模式）
- 统一 `Navigate` 到 `https://msmc.local/index.html`

### Step 6：修改 MainWindow
- 用工厂获取资源提供器
- 调用 `bridgeService.LoadFrontend(provider)`
- 移除原来的 `GetFrontendFolderPath()` 方法

### Step 7：调整 csproj 资源嵌入
- 把 `frontend/dist/**` 标记为 `EmbeddedResource`
- 资源逻辑名使用 `wwwroot.{RecursiveDir}.{FileName}.{Extension}` 格式（需要处理路径转换）
- 保留 `BuildFrontend` Target 在构建前触发

### Step 8：测试验证
- 开发模式验证（dist 存在，走文件夹）
- 模拟发布模式验证（dist 不存在，走 B 模式嵌入资源）
- 验证各种资源类型加载（HTML/JS/CSS/图片/字体）
- 验证路由跳转正常

## 6. 风险与注意事项

### 6.1 资源路径映射问题
- **风险**：嵌入资源的逻辑名把路径分隔符变成 `.`，`assets/index-xxx.js` 变成 `wwwroot.assets.index-xxx.js`，但文件名里本身就有 `.`（如 `index-xxx.js`），会导致歧义
- **方案**：不用默认命名，用 `CallTarget` 在构建时手动处理，或者在 C# 里建立文件名 → 资源名的映射字典（启动时扫描一次）

### 6.2 WebResourceRequested 兼容性
- **风险**：WebView2 某些版本对 `WebResourceRequested` 的处理有 bug（比如 POST 请求、Range 请求）
- **方案**：只拦截 GET 请求，其他请求放行；出现异常自动降级到 C 模式

### 6.3 构建顺序依赖
- **风险**：嵌入资源是在构建时打包的，如果前端还没构建（dist 不存在），会嵌入空资源
- **方案**：csproj 中的 `BuildFrontend` Target 要在 `BeforeResGen` 之前执行，确保嵌入资源前前端已构建

### 6.4 单文件发布兼容性
- **风险**：`PublishSingleFile=true` 时，嵌入资源是在 exe 内部的，`Assembly.GetManifestResourceStream` 应该还能工作
- **验证**：B 模式用 `Assembly.GetManifestResourceStream` 是直接读程序集元数据，单文件发布下正常工作，这是最稳妥的嵌入方式

## 7. 验收标准

1. ✅ 开发时 dist 目录存在 → 走文件夹模式（不影响开发体验）
2. ✅ 发布后 dist 目录不存在 → 走 B 模式嵌入资源，页面正常渲染
3. ✅ 所有资源类型（HTML/CSS/JS/图片/字体）加载正常
4. ✅ HashRouter 路由跳转正常
5. ✅ 前后端桥接通信正常（app:ready 事件、API 调用）
6. ✅ CI 构建通过，单文件发布后可直接运行
7. ✅ B 模式失败时自动降级到 C 模式（兜底验证）
