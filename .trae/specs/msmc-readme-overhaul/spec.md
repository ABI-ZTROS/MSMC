# MSMC README 全面重写 - Product Requirement Document

## Overview
- **Summary**: 对 MSMC 根目录 README.md 进行一次专业化、技术感、内容充实的全面重写。采用现代 GitHub 项目的「专业产品风」版式，配合 Seedream 生成的品牌 Banner 图、ASCII 架构全景图、4 张 UI 渲染截图、功能矩阵表，完整呈现 MSMC 的 10 大功能模块、技术栈、构建方式、项目结构、路线图与团队致谢，打造一份既硬核又专业的项目门面。
- **Purpose**: 解决现有 README 内容零散、无结构、包含不适宜对外展示的个人化/豁免声明、无视觉元素的问题，让项目在 GitHub 上有正式的技术门面，同时让新贡献者能够一眼看懂项目价值、模块边界与上手方式。
- **Target Users**: Minecraft 服务器运维人员、C#/.NET 桌面开发者、想要参与贡献的开源开发者、项目的用户与朋友。

## Goals
- 打造一份结构清晰、排版紧凑、视觉饱满的专业产品级 GitHub README
- 完整呈现 MSMC 的全部功能模块，区分 L1/L2/L3 三层的介绍深度
- 移除旧 README 中「营门口病区坐牢」托管公告与「AI 开发豁免」相关内容
- 加入 Seedream 生成的品牌 Banner 图 + 4 张 UI 渲染截图，配合 ASCII 架构图、徽章矩阵
- 提供可操作的构建指南（含前置条件、构建命令、部署注意事项）与项目结构树
- 保留团队致谢、许可声明与项目价值观，延续项目的人文温度

## Non-Goals (Out of Scope)
- **不**修改项目代码或功能实现，仅撰写文档与生成图片素材
- **不**拆分多语言 README（中英双语、i18n README），本次仅输出中文单版本
- **不**创建 docs/ 子文档体系，所有核心介绍内容仍收敛在单文件 README.md 中
- **不**伪造未实现功能的截图；UI 渲染图的设计必须贴合当前代码已实现的真实界面
- **不**加入任何个人化政治声明、健康状态陈述或豁免条款

## Background & Context
### 当前 MSMC 的技术画像（来自代码库真实状态）
- **项目全名**：MC Server Guard Console（MC 服务器守护控制台）
- **技术栈**：WPF (.NET 9 / Windows Desktop SDK 10.0.22000.0) + React 18 + Vite 5 + TypeScript 5.8 + WebView2（前后端桥接）
- **打包前端**：Vite 构建产物经 zip 压缩后作为 C# 嵌入资源（EmbeddedResource），运行时由 EmbeddedResourceProvider 解压并通过 WebResourceRequested 拦截提供，无磁盘落盘
- **DI 容器**：CommunityToolkit.Mvvm + Microsoft.Extensions.DependencyInjection（StrongName 签名版）
- **日志**：Serilog（主日志 Warning+ / 调试日志 Debug+，MB×份滚动）
- **主题系统**：MaterialDesignInXamlToolkit 4.9 + MahApps.Metro.IconPacks + 自研 ColorOS Aquario 量子动画引擎（CSS 变量驱动的响应式配色，16 级品牌色阶）
- **开发规范**：`/warnaserror`（警告视为错误）+ `<AnalysisLevel>latest-all</AnalysisLevel>` + StyleCop + SonarAnalyzer + 所有 nullable 启用
- **版本号**：v0.9.0-preview.17（Paper 分支）

### 功能模块全景（来自 Features 目录结构 + 前端路由）
| 层级 | 模块 | 路由 | 关键能力 |
|------|------|------|----------|
| L1 | 服务器检测 ServerDetection | / | 进程级多实例扫描、36+ 服务器核心指纹识别（Bukkit/Spigot/Paper/Purpur/Folia/Velocity/Waterfall/BungeeCord/Sponge/Nukkit/Mohist/CatServer/Arclight/Akarin/Tuinity/Yatopia/Airplane/Pufferfish/Kaiiju/Leaves/Mod端 等）、JVM 参数解析、JAR 清单提取 |
| L1 | 配置编辑器 ConfigEditor | /config | 36 种核心的中文翻译（服务器.properties / Bukkit / Spigot / Paper / Purpur / Folia / Velocity / Waterfall / BungeeCord / Sponge Global / Nukkit / PowerNukkit / CatServer / Mohist / Arclight / Forge server / Fabric server / Glowstone 等）、Properties/YAML/TOML/JSON/HOCON/XML 多格式支持、撤销栈、实时保存、搜索过滤 |
| L1 | 系统监控 SystemMonitor | /system | CPU 拓扑（异构 P 核 / E 核 / 超线程拓扑树）、CPU 整体+每核使用率、内存/页文件/GC 压力估算、磁盘 IO+容量、线程数/句柄数、CPU Set 亲和性绑定、核心拓扑+掩码计算、历史趋势持久化 24h |
| L2 | Java 管理 Java | /java | 独立页面、注册表/环境变量/常见路径自动扫描、自定义路径添加/删除、默认版本设置、javaw/java 选择偏好 |
| L2 | 电源管理 Power | /power | 实验性功能开关（启用后重启 MSMC）、睿频档位、CPU QoS、CPU Set 绑定、定时器精度（1ms/15ms）、Power Request 防睡眠 |
| L2 | 网络监控 NetworkMonitor | /network | 端口实时扫描、端口桥接（netsh portproxy + UPnP IGDv2）、TCP/UDP 流量统计、公网 IP 检测 |
| L2 | 启动脚本识别 |（嵌入服务器检测）| .bat/.cmd/.sh/.ps1 启动脚本解析、JVM 参数提取、JAR 路径定位 |
| L3 | 主题系统 Settings | /settings | 主色/强调色自定义、13 套品牌预设、圆角滑块、动画开关、深浅色、通知测试、进程监管策略（崩溃重启/防睡眠/优先级/内存上限） |
| L3 | 用户协议 UserAgreement |（启动弹窗）| 首次启动协议确认、复选框勾选项 |
| L3 | 崩溃恢复 CrashWindow |（异常兜底）| 未处理异常捕获、崩溃日志显示、一键复制/重启/打开日志文件夹 |
| L3 | WebView2 桥接 WebView2BridgeService |（基础设施）| 前后端 50+ 个 API、资源请求拦截、EmbeddedResource 解压、启动进度事件、主题变更广播、进程监管 SSE 消息 |

### 必须移除的旧 README 内容清单
1. 「项目托管公告」整段（含营门口病区、手机 hotspot、5G CPE、电信 NAT 等个人化内容）
2. 「AI 代码开发豁免声明」整段（含 AI 生成、人工 review 承诺、免责等）
3. 「技术栈」中「AI 协作规范」相关条目

## Functional Requirements

### FR-1: 专业产品风排版骨架
README.md 必须遵循以下章节顺序（从顶到底）：
1. **品牌 Banner 图 + Hero 徽章矩阵 + Slogan**：顶部一张 Seedream 生成的品牌 Banner，下方一排 Shield.io 风格徽章，再下方一行项目一句话 Slogan
2. **✨ 核心亮点（Feature Highlights）**：6-8 条一句话卖点，每行前加 emoji 或 Unicode 符号
3. **🎯 三大核心功能（L1，详细 + 配图）**：服务器检测、配置编辑器、系统监控，每个都要有：标题徽章、能力列表、功能要点小节、一张 UI 渲染截图（共 3 张）、"为什么值得选"的亮点总结
4. **⚡ 四大重要功能（L2，简述 + 亮点）**：Java 管理、电源管理、网络监控、启动脚本识别，每个以卡片式 3-5 句介绍 + 亮点 bullet
5. **🧱 基础设施模块（L3，简表）**：主题系统、用户协议、崩溃恢复、WebView2 桥接，用表格呈现模块名/作用/亮点
6. **🏗️ 架构全景（ASCII）**：一张 ASCII 风格的模块架构图，展示 WPF ↔ WebView2 Bridge ↔ React 前端 三层关系，以及各 Feature 模块与底层服务依赖
7. **🧰 技术栈矩阵**：分层表格展示 UI 层/渲染层/逻辑层/数据层/工具链的具体依赖
8. **🚀 构建指南**：前置条件（.NET 9 SDK / Node.js ≥20 / npm ≥10 / Win10+）、仓库克隆、还原依赖、前端构建、后端构建、运行调试、EmbeddedResource 模式说明、强签名注意事项
9. **📁 项目结构树**：ASCII tree 风格展示 src/ 目录，每个关键文件夹/文件附一句话注释
10. **🗺️ 路线图（Roadmap）**：分三列展示 ✅ 已实现 / 🔨 进行中 / 🧭 规划中
11. **🤝 团队致谢（Credits）**：保留原 README 的贡献者、开源项目致谢、特别鸣谢、版权声明，更新语气去除豁免相关，并增加前端/后端/产品等角色标注
12. **📜 许可声明**：保留 Apache-2.0 + 附加条款（CREDITS.md、保留版权、宣传提及）
13. **🏁 最终 Footer**：项目版本 + Release 链接 + 联系渠道占位

### FR-2: 视觉元素完整要求
- **品牌 Banner 图**：1 张，使用 Seedream 生成，风格为科幻+服务器运维控制台配色（深蓝+水蓝/ColorOS 主色），中央有 "MSMC · MC Server Guard Console" 文字，背景有服务器机柜、波形数据流、量子粒子等元素，1280x400 landscape_16_9
- **L1 模块 UI 渲染图**：3 张，分别为服务器检测面板、配置编辑器、系统监控仪表盘，风格严格对齐项目真实 UI（ColorOS Aquario 风格，圆角卡片+粒子背景+品牌蓝主色），每张 1280x720 landscape_16_9
- **L2 模块合成图**：1 张，展示 Java 管理 + 电源管理 + 网络监控三联卡片构图，1280x720 landscape_16_9
- **ASCII 架构全景图**：1 张，纯 ASCII 字符绘制（╔═╗║╚╝┌─┐│└┘╭╮╰╯▄▀█▓▒░等），展示三层架构与模块依赖，宽度≤110 列以适配移动端

### FR-3: 徽章矩阵要求
Hero 徽章区至少包含以下 10 枚（用纯 Markdown 表格或 inline 排列，徽章颜色遵循 Shield.io 语义色：蓝=信息，绿=通过，橙=警告，紫=构建，灰=元信息）：
- .NET 版本：`.NET 9.0`
- Windows SDK：`Windows SDK 22000`
- 前端框架：`React 18`
- 语言版本：`C# 13` / `TypeScript 5.8`
- 代码质量：`TreatWarningsAsErrors ON`
- 可空类型：`#nullable enable`
- 平台：`Windows 10+ x64`
- 许可证：`Apache-2.0 + Attached`
- 状态：`v0.9.0-preview.17`
- 贡献者：`Team ZTROS`

### FR-4: L1 模块详细展开要求
每个 L1 模块必须包含：
- 一个二级标题 + 主题图标 emoji
- 2-4 句模块使命/定位
- 一个 **功能要点** 小节（至少 6-10 条 bullet，覆盖该模块全部能力）
- 一张 UI 渲染图（`![alt text](assets/xxx.png)` 格式）
- 一个 **亮点/差异点** 小节（2-4 句描述该模块相较同类工具的独特优势）

### FR-5: L2 模块简述要求
每个 L2 模块（Java 管理 / 电源管理 / 网络监控 / 启动脚本识别）必须包含：
- 三级标题 + 图标 emoji
- 2-4 句模块简介
- 4-6 条核心亮点 bullet
- 4 个模块排版为 2×2 卡片式布局（利用 Markdown 表格 + emoji 头部装饰实现视觉卡片）

### FR-6: L3 简表要求
4 个 L3 模块用表格呈现：列 = 模块 | 角色 | 关键能力 | 备注

### FR-7: 架构图 + 技术栈矩阵要求
- ASCII 架构图宽度 ≤110 列，至少包含：WPF Host/DI/Serilog/WebView2 Core、Bridge Service/API Handlers/EmbeddedResource Provider、React App/Router/Stores/7 Pages、底层 Native Services
- 技术栈矩阵分为 5 行：UI 层 | 渲染层 | 逻辑层 | 数据层 | 工具链，每行列出具体依赖名称

### FR-8: 构建指南完整性
必须覆盖以下步骤（每步提供确切命令）：
1. 前置条件清单（含版本号下限）
2. `git clone` 命令
3. 前端构建：`npm install` / `npm run build`（含 `--prefix` 说明）
4. 后端构建：`dotnet restore` / `dotnet build -c Release`（含 Win10/11 TFM、x64 说明）
5. 运行调试注意事项：`dotnet run --project src/MSMC` + 强签名临时关闭方案（`/p:SignAssembly=false`）
6. EmbeddedResource 模式说明：前端构建失败会导致 C# 构建失败（MSB3073 退出码 2），必须先 `cd frontend && npm run build`
7. 日志输出目录说明：`bin/<config>/<tfm>/win-x64/logs/`
8. WebView2 Runtime 依赖说明（Win10 可能需手动安装 WebView2 Evergreen Runtime）

### FR-9: 项目结构树要求
使用 ASCII tree 风格展示 `src/` 目录，至少列出：
```
src/
├── MSMC/                          # WPF 主程序
│   ├── App.xaml(.cs)              # 启动入口 + 全局异常 + DI 容器
│   ├── Features/                  # 功能模块（每个文件夹=一个 Feature 垂直切片）
│   │   ├── ConfigEditor/          # 配置编辑器（C# ViewModel + 前端 ConfigEditorPage）
│   │   ├── JavaManagement/        # Java 管理服务
│   │   ├── NetworkMonitor/        # 网络监控+端口桥接
│   │   ├── PowerManagement/       # 电源管理（条件注册服务）
│   │   ├── ServerDetection/       # 服务器检测+JAR指纹识别+启动脚本解析
│   │   ├── Settings/              # 主题设置+进程监管策略
│   │   ├── Shared/                # 共享控件/主题/服务/窗口效果
│   │   ├── Startup/               # 启动窗口+崩溃窗口+用户协议+NTP时钟
│   │   ├── SystemMonitoring/      # CPU拓扑+系统指标采集+历史持久化
│   │   ├── UserAgreement/         # 用户协议窗口
│   │   └── WebView2/              # WebView2 桥接 + EmbeddedResourceProvider
│   └── MSMC.csproj                # 项目文件（warnaserror + TreatWarningsAsErrors）
├── MSMC.Tests/                    # 单元测试（xUnit）
└── frontend/                      # React 前端
    ├── src/
    │   ├── pages/                 # 7 个页面组件（Dashboard/Config/System/Network/Power/Java/Settings）
    │   ├── components/            # UI 组件（Sidebar/AppLayout/LazyPageErrorBoundary/ColorPicker 等）
    │   ├── stores/                # Zustand 状态管理器
    │   ├── hooks/                 # useBridgeInit / useMetricsHistory 等
    │   ├── utils/                 # bridge API 封装、主题工具函数
    │   └── types/                 # bridge.ts 返回类型定义
    ├── package.json
    └── vite.config.ts
```

### FR-10: 路线图（Roadmap）
分三列表格：
- ✅ 已实现：列出当前代码已落地的全部大项（至少 10+ 条）
- 🔨 进行中：列出正在开发的 3-5 项（崩溃恢复完善、服务器管理进程监管实装、实验功能稳定性等）
- 🧭 规划中：列出未来想做的 4-6 项（多语言、远程 Web 面板、Linux 适配、多实例统一面板、插件系统等）

### FR-11: 团队致谢与版权
- 保留原 README 中的项目牵头人（ABI-ZTROS）、核心贡献者（MochaCello92377、CatStack）、前端架构（烟蓝湘）、技术顾问等角色，并标注各自职责
- 保留开源项目致谢列表（MaterialDesignInXamlToolkit、MahApps.Metro.IconPacks、Serilog、WebView2、React、Vite 等）
- 保留特别鸣谢段落，去掉健康相关个人化内容
- 保留 Apache-2.0 + 附加条款 1-4（CREDITS.md、保留版权、书面同意宣传、衍生声明）

## Non-Functional Requirements

- **NFR-1 可读性**：README 在 GitHub 桌面端渲染下一屏可看到 Banner+徽章+Slogan+前两条亮点，移动端（375px）下表格不溢屏，ASCII 架构图不横向溢出
- **NFR-2 排版密度**：全文约 800-1500 行 Markdown（含空行与图片行），不短于 500 行；避免单段超过 5 行，多用 bullet 和表格
- **NFR-3 准确性**：所有技术指标（依赖版本号、模块名称、路由路径、构建命令）必须与代码库实际状态一致，不得编造未实现功能
- **NFR-4 技术感**：合理使用代码块（`inline code`）、徽章、ASCII 框线、Unicode 分隔符（════ ▀▄─ ═══），但不过度滥用影响阅读
- **NFR-5 无外链依赖**：徽章使用 Markdown inline 文本模拟（Shield.io 在国内不稳定），不引用外部 badge 服务 SVG 图片；所有图片均为本地 `assets/` 目录下的 Seedream 产物
- **NFR-6 语气克制**：技术感≠装 B 过度，去掉「坐牢」「fuck」等攻击性词汇，转为专业但保留温度的语气；装 B 感通过技术密度体现，不通过脏话体现

## Constraints
- **Technical**: 仅使用 Markdown + ASCII + PNG 图片，禁止 HTML/CSS 自定义样式；图片必须通过 Seedream 生成并保存在 `/workspace/assets/` 目录；徽章用 Markdown 内嵌代码块背景色方案（纯文本模拟，无外链）
- **Business**: 必须移除豁免声明和病区托管公告；致谢中保留现有团队成员名单与关系；附加许可条款 1-4 必须保留不得修改
- **Dependencies**: Seedream 插件生成图片（最多 5 张 Banner+UI 渲染图），其余无需外部依赖

## Assumptions
- 项目仓库根目录存在或可创建 `assets/` 文件夹，README 可以相对路径引用其中的图片
- Seedream 插件能够稳定生成 5 张所需图片（Banner×1 + UI 渲染×4）
- 用户后续会自行补齐或替换 UI 渲染图（如需真实截图），Seedream 生成图作为占位+美化
- 构建命令在 x64 Windows 10/11 + 中文简体环境下测试通过，构建指南可参考已有 CI/本地开发实践

## Acceptance Criteria

### AC-1: 旧内容彻底移除
- **Given**: 重写完成后的 README.md
- **When**: 在全文中搜索关键词「营门口」「病区」「坐牢」「豁免」「AI 代码」「AI 生成」「AI 协作」「hotspot」「NAT」「CPE」
- **Then**: 0 条命中；原「项目托管公告」段与「AI 代码开发豁免声明」段完全不存在
- **Verification**: `programmatic`（Grep 搜索关键词）

### AC-2: 视觉元素齐全
- **Given**: 重写完成后的 README.md + `/workspace/assets/` 目录
- **When**: 检查 `assets/` 下 PNG 文件数量与 README.md 中 `![]()` 引用次数
- **Then**: `assets/` 下至少 5 张 PNG（banner.png、server-detect.png、config-editor.png、system-monitor.png、modules-trio.png）；README 中 `![]()` 出现 5 次且路径匹配；ASCII 架构图至少 1 段（≥10 行 ASCII 框线字符）
- **Verification**: `programmatic`（Glob + Grep 计数）

### AC-3: 章节骨架完整（13 节）
- **Given**: 重写完成后的 README.md
- **When**: 提取所有 `## ` 和 `### ` 二级/三级标题
- **Then**: 必须存在以下二级节：Banner+徽章+Slogan（隐含节）、核心亮点、三大核心功能、四大重要功能、基础设施、架构全景、技术栈、构建指南、项目结构、路线图、团队致谢、许可声明、Footer；L1 的 3 个模块有三级标题，L2 的 4 个模块有三级标题
- **Verification**: `programmatic`（Grep 标题行计数+名称验证）+ `human-judgment`（阅读体验）

### AC-4: L1 模块详略得当
- **Given**: 三大核心功能章节
- **When**: 人工审阅每个 L1 模块
- **Then**: 每个模块包含：二级/三级标题、2-4 句简介、≥6 条功能要点 bullet、1 张 UI 图、2-4 句亮点总结；合计 bullet ≥20 条；3 张截图均被引用
- **Verification**: `human-judgment`

### AC-5: L2 模块卡片呈现
- **Given**: 四大重要功能章节
- **When**: 人工审阅
- **Then**: 4 个模块为 2×2 卡片布局（表格或视觉分栏），每个模块 ≥4 条 bullet，合计 bullet ≥16 条；电源管理部分明确标注「实验性功能 · 启用需重启」
- **Verification**: `human-judgment`

### AC-6: L3 简表完整
- **Given**: 基础设施章节
- **When**: 人工审阅表格
- **Then**: 表格 4 行，列=模块/角色/关键能力/备注；包含主题系统、用户协议、崩溃恢复、WebView2 桥接四项
- **Verification**: `human-judgment` + `programmatic`（Grep 表格行计数）

### AC-7: 架构图+技术栈正确
- **Given**: 架构全景 + 技术栈矩阵
- **When**: 人工审阅并与代码结构对照
- **Then**: ASCII 架构图包含 WPF/WebView2/React 三层 + 各 Feature 模块节点；技术栈表格 5 行，依赖名称/版本与 csproj/package.json 一致（React 18、Vite 5、.NET 9、Serilog、MDIX 4.9、MahApps.IconPacks 等）
- **Verification**: `human-judgment`（一致性核对）

### AC-8: 构建指南可复现
- **Given**: 构建指南章节
- **When**: 在全新 Win11 环境（仅安装 .NET 9 SDK + Node 20 + npm）按步骤执行
- **Then**: 每步命令都有明确代码块，顺序可依；前端 `npm run build` 成功，后端 `dotnet build -c Release src/MSMC/MSMC.csproj` 成功；有 EmbeddedResource 模式、强签名、日志路径、WebView2 Runtime 等注意事项
- **Verification**: `programmatic`（命令语法正确 + 路径存在验证）+ `human-judgment`（可理解度）

### AC-9: 项目结构树与代码一致
- **Given**: 项目结构树章节
- **When**: 与实际目录 `src/` 用 Glob 对照
- **Then**: 树中列出的全部 10 个 Features 文件夹（ConfigEditor/JavaManagement/NetworkMonitor/PowerManagement/ServerDetection/Settings/Shared/Startup/SystemMonitoring/UserAgreement/WebView2）均实际存在；`frontend/src/pages` 与路由一致
- **Verification**: `programmatic`（Glob 验证目录存在性）

### AC-10: 路线图层次分明
- **Given**: 路线图章节
- **When**: 人工审阅三列表格
- **Then**: ✅ 已实现 ≥10 条；🔨 进行中 3-5 条；🧭 规划中 4-6 条；无编造的已完成功能
- **Verification**: `human-judgment`

### AC-11: 致谢与许可保留得当
- **Given**: 致谢 + 许可章节
- **When**: 人工对照旧 README
- **Then**: 致谢中保留 ABI-ZTROS、MochaCello92377、CatStack、烟蓝湘 4 位核心成员，并附加职责标注；开源项目致谢 ≥6 项；Apache-2.0 + 附加条款 1-4 原文保留；无新增或删减条款
- **Verification**: `human-judgment`

### AC-12: 徽章矩阵呈现
- **Given**: README 顶部徽章区
- **When**: 检查徽章数量与类别
- **Then**: 徽章 ≥10 枚；覆盖 .NET 版本、Windows SDK、React 版本、C# 版本、TypeScript 版本、TreatWarningsAsErrors、nullable、平台、许可证、版本号、贡献者；颜色遵循语义色（蓝/绿/橙/紫/灰）
- **Verification**: `human-judgment`

### AC-13: 技术参数准确性
- **Given**: 全文技术参数
- **When**: 对照 csproj/package.json/version.json/代码实际实现
- **Then**: .NET 版本号=9.0.100-9.0.2xx 区间标注为 `.NET 9`；Windows SDK=10.0.22000.0；React=18；TS=5.8；核心翻译数量=36+；服务器核心识别=36+；路由数量=7；以上参数与代码一致，无夸大
- **Verification**: `programmatic`（Read 实际文件并比对）

## Open Questions
- [ ] UI 渲染图最终是希望保留 Seedream 生成图占位，还是之后由用户自行替换为真实运行截图？（当前默认：Seedream 生成，README 中以「UI 渲染示意图」标注）
- [ ] 徽章区是否想要加入真实 CI 状态徽章（如 GitHub Actions workflow badge）？若仓库尚未配置 Actions，则继续用文本模拟徽章
- [ ] Footer 联系渠道要不要放具体的 QQ 群/邮箱，还是留占位符由用户后续填入？
