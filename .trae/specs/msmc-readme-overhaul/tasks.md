# MSMC README 全面重写 - The Implementation Plan (Decomposed and Prioritized Task List)

## [x] Task 1: 生成视觉素材（品牌 Banner + 4 张 UI 渲染图）
- **Priority**: high
- **Depends On**: None
- **Description**: 
  - 使用 Seedream 插件生成 5 张图片并保存到 `/workspace/assets/` 目录
  - 图片 1：品牌 Banner（1280x400 landscape_16_9）— 科幻+服务器运维控制台配色，深蓝+水蓝/ColorOS 主色，中央有 "MSMC · MC Server Guard Console" 文字，背景服务器机柜、波形数据流、量子粒子元素
  - 图片 2：服务器检测面板 UI 渲染（1280x720 landscape_16_9）— ColorOS Aquario 风格，圆角卡片+粒子背景+品牌蓝主色，展示多实例服务器列表卡片（Bukkit/Paper/Velocity 等核心指纹徽章、PID、版本号、内存占用条形图、状态灯）
  - 图片 3：配置编辑器 UI 渲染（1280x720 landscape_16_9）— 侧边配置键列表+主区中文翻译说明+值输入控件+顶部搜索过滤栏+保存/撤销按钮
  - 图片 4：系统监控仪表盘 UI 渲染（1280x720 landscape_16_9）— CPU 拓扑树（P核/E核/超线程）、CPU 每核使用率条形图、内存/磁盘环形仪表、趋势曲线图、彩色品牌主题
  - 图片 5：L2 模块三联卡片（1280x720 landscape_16_9）— 左 Java 管理（Java 版本列表卡片）、中 电源管理（档位滑块/QoS 开关）、右 网络监控（端口桥接表+流量图），三联卡片并排品牌蓝主色
- **Acceptance Criteria Addressed**: FR-2, AC-2
- **Test Requirements**:
  - `programmatic` TR-1.1: `/workspace/assets/` 目录下存在 5 个 PNG 文件：`banner.png`、`server-detect.png`、`config-editor.png`、`system-monitor.png`、`modules-trio.png`
  - `programmatic` TR-1.2: 每张图片分辨率符合规格（banner≥1200×380, UI 图≥1200×700）
  - `human-judgement` TR-1.3: 图片风格统一（深蓝+水蓝主色、圆角卡片、科技感粒子/波形背景），无反常识元素（如按钮上是英文字符而非乱码）
- **Notes**: Seedream 可能会有调用上限，必要时分批次生成；若文字渲染不理想，可在 prompt 中弱化文字要求由后续人工处理

## [x] Task 2: 撰写 README.md 骨架（章节结构 + 徽章 + ASCII 架构图 + 结构树 + 路线图）
- **Priority**: high
- **Depends On**: None
- **Description**: 
  - 完全重写 `/workspace/README.md`（覆盖旧内容），先搭建 13 个章节的骨架结构与纯文本部分，暂不填充 L1/L2/L3 的详细介绍
  - 第 1 区：Banner 图占位引用 `![MSMC Banner](assets/banner.png)` + Hero 徽章矩阵（≥10 枚，纯 Markdown 文本模拟 Shield.io，蓝绿橙紫灰语义色） + Slogan 一句话
  - 第 2 区：✨ 核心亮点（6-8 条一句话卖点，emoji 开头）
  - 第 6 区：🏗️ 架构全景 ASCII 图（≤110 列宽，三层 WPF/WebView2 Bridge/React 前端 + Feature 模块节点，≥10 行框线）
  - 第 7 区：🧰 技术栈矩阵（5 行：UI 层/渲染层/逻辑层/数据层/工具链）
  - 第 9 区：📁 项目结构树（ASCII tree，与 FR-9 一致，10 个 Features 文件夹 + frontend/src/pages + stores/hooks/utils/types）
  - 第 10 区：🗺️ 路线图（三列表格：✅ 已实现≥10 条 / 🔨 进行中 3-5 条 / 🧭 规划中 4-6 条）
  - 第 3/4/5/8/11/12/13 区：仅写标题占位，后续任务填充
- **Acceptance Criteria Addressed**: FR-1 (部分), FR-3, FR-7 (部分), FR-9, FR-10, AC-3 (部分), AC-7 (部分), AC-9, AC-10, AC-12
- **Test Requirements**:
  - `programmatic` TR-2.1: `README.md` 中 `## ` 二级节标题数量 ≥ 13；包含以下关键词匹配：「核心亮点」「三大核心功能」「四大重要功能」「基础设施」「架构全景」「技术栈」「构建指南」「项目结构」「路线图」「团队致谢」「许可声明」「Footer」
  - `programmatic` TR-2.2: ASCII 架构图中出现以下符号至少各 3 次：`╔═╗` 或 `║` 或 `╚╝`，或 `┌─┐` 或 `│` 或 `└┘`；总行数 ≥ 10
  - `programmatic` TR-2.3: 项目结构树中出现 10 个 Features 文件夹名全部存在（ConfigEditor / JavaManagement / NetworkMonitor / PowerManagement / ServerDetection / Settings / Shared / Startup / SystemMonitoring / UserAgreement / WebView2）
  - `programmatic` TR-2.4: 徽章区 `\`.{3,30}\`` 内联代码形式徽章 ≥ 10 枚；包含关键词：.NET / Windows SDK / React / C# / TypeScript / TreatWarningsAsErrors 或 WAE / nullable / Windows 或 x64 / Apache 或 License / v0.9 / Team 或 ZTROS
  - `human-judgement` TR-2.5: 徽章配色遵循语义色（蓝=信息/绿=通过/橙=警告/紫=构建/灰=元信息），布局整齐不混乱
  - `human-judgement` TR-2.6: 路线图三列条目数符合要求：✅ ≥10，🔨 3-5，🧭 4-6；无编造的已实现功能
- **Notes**: 徽章用 `<kbd style="background:#color;...">text</kbd>` 纯 Markdown inline code 模拟即可，GitHub 会对 inline code 有轻微背景色区分即可

## [x] Task 3: 填充 L1 三大核心功能（服务器检测 + 配置编辑器 + 系统监控）详细介绍
- **Priority**: high
- **Depends On**: Task 1（图片路径依赖）, Task 2（骨架依赖）
- **Description**: 
  - 在 README.md 第 3 区 `## 🎯 三大核心功能` 下依次填充三个 L1 模块：
  - 模块 A：服务器检测（对应路由 `/`，前端 `Dashboard.tsx`，后端 `Features/ServerDetection`）— 三级标题 `### 🖥️ 服务器检测与核心指纹识别` + 2-4 句简介 + **功能要点** 8-10 条 bullet（覆盖：多实例进程扫描、36+核心指纹识别列表、JVM 参数解析、JAR Manifest 提取、启动脚本扫描解析、端口号监听检测、线程/句柄数统计、崩溃状态判断、服务器进程一键停止） + 图片引用 `![服务器检测面板渲染示意图](assets/server-detect.png)` + **亮点** 2-4 句
  - 模块 B：配置编辑器（对应路由 `/config`，前端 `ConfigEditorPage.tsx`，后端 `Features/ConfigEditor`）— 三级标题 `### ⚙️ 全核心中文配置编辑器` + 2-4 句简介 + **功能要点** 8-10 条 bullet（覆盖：36+ 核心中文翻译清单、Properties/YAML/TOML/JSON/HOCON/XML 多格式、撤销栈 redo/undo、搜索过滤键名、数值/布尔/枚举/字符串/列表差异化编辑控件、实时保存与脏状态、服务器.properties 读写回写、权限节点注释保留、大文件分页懒加载、长按查看原版注释对照） + 图片引用 `![配置编辑器渲染示意图](assets/config-editor.png)` + **亮点** 2-4 句
  - 模块 C：系统监控仪表盘（对应路由 `/system`，前端 `SystemMonitorPage.tsx`，后端 `Features/SystemMonitoring`）— 三级标题 `### 📊 异构 CPU 拓扑监控仪表盘` + 2-4 句简介 + **功能要点** 8-10 条 bullet（覆盖：CPU 异构拓扑树 P/E/超线程、CPU 整体+每核实时利用率、内存/页文件/GC 压力估算、磁盘 IOPS+吞吐+容量、线程数/句柄数趋势、CPU Set 亲和性绑定+掩码计算、核心分组与 NUMA 感知、历史指标 24h 持久化还原、进程级资源占用 Top N、核心电压/温度采集（若可用）） + 图片引用 `![系统监控仪表盘渲染示意图](assets/system-monitor.png)` + **亮点** 2-4 句
- **Acceptance Criteria Addressed**: FR-4, AC-4
- **Test Requirements**:
  - `programmatic` TR-3.1: 三个 L1 模块三级标题 `### ` 全部存在；三个图片引用 `![]()` 全部出现且路径为 `assets/server-detect.png` / `assets/config-editor.png` / `assets/system-monitor.png`
  - `programmatic` TR-3.2: 三个模块合计 bullet `- ` 数量 ≥ 20 条（单模块 ≥ 6 条）
  - `programmatic` TR-3.3: 每个模块有 `功能要点` 小标题 + `亮点` 小标题（或同含义变体）
  - `human-judgement` TR-3.4: 每个 bullet 的描述与实际代码功能一致，无虚构未实现能力；亮点总结真正体现差异化（如 36+ 核心翻译独有、异构 P/E 核拓扑独有、EmbeddedResource 打包等）
- **Notes**: 功能要点 bullet 请参考 spec.md「功能模块全景」L1 条目的能力描述细化展开

## [x] Task 4: 填充 L2 四大重要功能 + L3 基础设施简表
- **Priority**: high
- **Depends On**: Task 1, Task 2, Task 3
- **Description**: 
  - 第 4 区 `## ⚡ 四大重要功能` 下以 2×2 卡片表格形式填充 4 个 L2 模块：
  - 卡片 A（左上）：Java 管理 — ☕ Java 运行环境管理 + 2-4 句简介 + 4-6 bullet（自动扫描注册表/环境变量/常见路径、自定义路径添加删除、默认版本设置徽章、javaw/java 无控制台模式偏好、版本指纹识别 vendor+arch+major、批量导入导出 Java 列表）
  - 卡片 B（右上）：电源管理 — ⚡ CPU 电源与调度优化 + 「🧪 实验性功能 · 启用需重启 MSMC」徽章 + 2-4 句简介 + 4-6 bullet（总开关控制启用/禁用、CPU 睿频档位切换、QoS 等级控制、CPU Set 进程亲和调度、多媒体定时器精度 1ms/15ms 切换、Power Request 防睡眠阻止系统休眠、管理员权限前置校验）
  - 卡片 C（左下）：网络监控 — 🛰️ 端口监控与桥接 + 2-4 句简介 + 4-6 bullet（端口实时扫描 TCP/UDP 状态、netsh portproxy 本地端口转发、UPnP IGDv2 路由器端口映射、入站/出站流量统计、公网 IP 检测+变化通知、IPv4/IPv6 双栈支持、防火墙规则一键加白）
  - 卡片 D（右下）：启动脚本识别 — 📜 启动脚本解析器 + 2-4 句简介 + 4-6 bullet（.bat/.cmd/.sh/.ps1 多格式识别、JVM 参数完整解析（-Xmx/-Xms/-XX:+UseG1GC 等）、JAR 工作目录自动定位、多脚本冲突检测、参数模板导出、参数合法性校验（如堆内存超过物理内存预警））
  - L2 末尾插入三联图 `![Java+电源+网络三联面板渲染示意图](assets/modules-trio.png)`
  - 第 5 区 `## 🧱 基础设施模块` 下填充 4×4 表格（列：模块 / 角色 / 关键能力 / 备注；行：主题系统 Settings / 用户协议 UserAgreement / 崩溃恢复 CrashWindow / WebView2 桥接 BridgeService）
- **Acceptance Criteria Addressed**: FR-5, FR-6, AC-5, AC-6
- **Test Requirements**:
  - `programmatic` TR-4.1: L2 四个模块各自有三级标题 `### ` + 图标 emoji；4 个模块 bullet 合计 ≥ 16 条（单模块 ≥ 4 条）
  - `programmatic` TR-4.2: 电源管理模块中出现「实验性功能」和「重启」关键词各 ≥ 1 次
  - `programmatic` TR-4.3: `modules-trio.png` 图片引用出现 1 次
  - `programmatic` TR-4.4: L3 简表为 Markdown 表格，4 行，列数 ≥ 4；行名包含：主题系统 / 用户协议 / 崩溃 或 Crash / WebView2 或 Bridge
  - `human-judgement` TR-4.5: L2 2×2 卡片视觉整齐（Markdown 表格呈现），标题带 emoji 卡片感强
  - `human-judgement` TR-4.6: L3 表格关键能力列描述准确（主题系统=13预设+圆角+动画+进程监管；崩溃窗口=异常捕获+复制+重启+打开日志；WebView2=50+API+EmbeddedResource+SSE）
- **Notes**: L2 用 Markdown 表格 `| ... | ... |` 做 2×2 卡片布局，单元格内放模块标题+emoji+简介+bullet

## [x] Task 5: 填充构建指南 + 致谢 + 许可声明 + Footer 尾部链接
- **Priority**: high
- **Depends On**: Task 2, Task 3, Task 4
- **Description**: 
  - 第 8 区 `## 🚀 构建指南` 填充：
    - 前置条件表格（组件 / 最低版本 / 说明）：.NET 9 SDK x64 / Node.js 20 LTS / npm ≥ 10 / Windows 10 1809+ / WebView2 Evergreen Runtime
    - 步骤 1：克隆仓库 `git clone <repo-url> && cd MSMC`
    - 步骤 2：前端构建 `cd src/frontend && npm install && npm run build`（含 MSBuild 中 `--prefix` 说明）
    - 步骤 3：后端还原 `cd ../.. && dotnet restore`
    - 步骤 4：后端构建 `dotnet build -c Release src/MSMC/MSMC.csproj`（含 net9.0-windows10.0.22000.0 TFM + win-x64 说明）
    - 步骤 5：运行调试 `dotnet run --project src/MSMC -c Debug /p:SignAssembly=false`
    - 注意事项段落：EmbeddedResource 模式前端构建失败=后端 MSB3073；强签名公钥文件缺失需临时关闭 `/p:SignAssembly=false`；日志输出位置 `bin/**/win-x64/logs/`；Win10 缺失 WebView2 Runtime 的下载地址
  - 第 11 区 `## 🤝 团队致谢` 填充：
    - 核心团队表格（代号 / 角色 / 贡献）：ABI-ZTROS（项目发起+架构+后端/算法）/ MochaCello92377（核心翻译+QA）/ CatStack（核心翻译+QA）/ 烟蓝湘（前端架构+UI/动效设计）
    - 开源项目致谢 bullet ≥ 6：MaterialDesignInXamlToolkit / MahApps.Metro.IconPacks / Serilog / Microsoft.Web.WebView2 / CommunityToolkit.Mvvm / React + Vite / TypeScript / xUnit
    - 特别鸣谢段落：感谢所有测试人员与用户反馈
  - 第 12 区 `## 📜 许可声明` 填充：
    - 主声明：本项目基于 [Apache License 2.0](LICENSE) 开源，并附加以下条款：
    - 附加条款 1-4 原文保留（CREDITS.md 保留完整版权声明；衍生作品保留版权/免责声明；第三方宣传需书面同意；衍生项目变更需在显眼位置声明）
  - 第 13 区 Footer 填充：版本 v0.9.0-preview.17 · Paper 分支 / Release 链接占位 / 联系渠道占位（用户后续自行补充）
  - **关键：删除旧 README 中全部「营门口病区/坐牢/AI豁免/5G CPE/hotspot/NAT」等违规内容**，如因骨架写入不小心残留，在此任务彻底清理
- **Acceptance Criteria Addressed**: FR-8, FR-11, AC-1, AC-8, AC-11
- **Test Requirements**:
  - `programmatic` TR-5.1: 全文 Grep 关键词「营门口 / 病区 / 坐牢 / 豁免 / AI 代码 / AI 生成 / AI 协作 / hotspot / NAT / CPE」全部 0 命中
  - `programmatic` TR-5.2: 构建指南中出现代码块命令≥5 条：`git clone` / `npm install` / `npm run build` / `dotnet restore` / `dotnet build -c Release` / `dotnet run`
  - `programmatic` TR-5.3: 核心团队表格 4 行：ABI-ZTROS / MochaCello92377 / CatStack / 烟蓝湘
  - `programmatic` TR-5.4: 开源致谢 bullet ≥ 6 条，含 MDIX / MahApps / Serilog / WebView2 / React / Vite
  - `programmatic` TR-5.5: 附加条款 1-4 原文出现（CREDITS.md / 保留版权 / 书面同意 / 衍生声明）
  - `human-judgement` TR-5.6: 构建步骤可依、无遗漏关键坑（强签名/EmbeddedResource/WebView2/日志路径注意事项均有）
- **Notes**: 旧 README 中 Apache 附加条款原文直接搬运即可（1-4 条原封不动），严禁修改

## [x] Task 6: 一致性审查与规格自检（最终过一遍所有 AC）
- **Priority**: medium
- **Depends On**: Task 1-5
- **Description**: 
  - 执行 AC-13 技术参数准确性检查：逐项对照 csproj/package.json/version.json 确认 .NET 9/Windows SDK 22000/React 18/TS 5.8/核心翻译 36+/服务器核心识别 36+/7 路由等参数
  - 执行 AC-2 视觉元素齐全检查：5 张 PNG + 5 处 `![]()` 引用 + ASCII 架构图 ≥ 10 行
  - 执行 NFR-5 无外链依赖检查：README 无 shield.io / img.shields.io / 外部图片 CDN 链接（Grep `http.*\.(svg|png|jpg)`）
  - 执行 NFR-1 可读性检查：人工浏览 375px 窄屏下 ASCII 架构图和表格是否严重横溢（架构图宽≤110 列即通过）
  - 执行 NFR-6 语气克制检查：无「fuck / 坐牢 / shit」等粗口或个人化攻击
  - 修正所有发现的不一致问题
- **Acceptance Criteria Addressed**: AC-2, AC-13, NFR-1, NFR-5, NFR-6
- **Test Requirements**:
  - `programmatic` TR-6.1: Grep README 中外链图片 `http[s]?://[^ )]*\.(svg\|png\|jpg\|jpeg\|webp)` 数量 = 0
  - `programmatic` TR-6.2: ASCII 架构图中每行字符数（中文按 2 宽）最大 ≤ 120 列（纯 ASCII 字符≤110）
  - `programmatic` TR-6.3: 全文 Grep 「fuck/坐牢/shit/操/狗屎」= 0 命中
  - `programmatic` TR-6.4: 对照 csproj `TargetFramework` / `package.json versions` / `version.json` 后 README 中标注版本无夸大错误
  - `human-judgement` TR-6.5: 整体排版视觉舒适，表格/架构图在窄屏下基本可读
- **Notes**: 该任务是最终守门员，必须严格执行所有验证并修正问题
