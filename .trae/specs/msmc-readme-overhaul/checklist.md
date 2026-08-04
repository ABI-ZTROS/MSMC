# MSMC README 全面重写 - Verification Checklist

## AC-1 旧内容彻底移除
- [ ] Grep 全文 `营门口` → 0 命中
- [ ] Grep 全文 `病区` → 0 命中
- [ ] Grep 全文 `坐牢` → 0 命中
- [ ] Grep 全文 `豁免` → 0 命中
- [ ] Grep 全文 `AI 代码` → 0 命中
- [ ] Grep 全文 `AI 生成` → 0 命中
- [ ] Grep 全文 `AI 协作` → 0 命中
- [ ] Grep 全文 `hotspot` → 0 命中
- [ ] Grep 全文 `NAT` → 0 命中（允许出现技术术语 Network Address Translation 等；检查的是旧公告个人化内容）
- [ ] Grep 全文 `CPE` → 0 命中
- [ ] 不存在「项目托管公告」整段结构
- [ ] 不存在「AI 代码开发豁免声明」整段结构

## AC-2 视觉元素齐全
- [ ] `/workspace/assets/banner.jpg` 文件存在
- [ ] `/workspace/assets/server-detect.jpg` 文件存在
- [ ] `/workspace/assets/config-editor.jpg` 文件存在
- [ ] `/workspace/assets/system-monitor.jpg` 文件存在
- [ ] `/workspace/assets/modules-trio.jpg` 文件存在
- [ ] README.md 中 `![Banner](assets/banner.jpg)` 形式的 `![]()` 共 5 处
- [ ] 5 处引用路径与上述 5 个文件名严格对应
- [ ] ASCII 架构图存在且含框线字符的行数 ≥ 10

## AC-3 章节骨架完整（13 节）
- [ ] 顶部 Banner 图引用存在
- [ ] Hero 徽章矩阵存在（≥10 枚徽章）
- [ ] 一句话 Slogan 存在
- [ ] `## ✨ 核心亮点` 或同含义二级标题存在
- [ ] `## 🎯 三大核心功能` 或同含义二级标题存在
- [ ] `## ⚡ 四大重要功能` 或同含义二级标题存在
- [ ] `## 🧱 基础设施模块` 或同含义二级标题存在
- [ ] `## 🏗️ 架构全景` 或同含义二级标题存在
- [ ] `## 🧰 技术栈` 或同含义二级标题存在
- [ ] `## 🚀 构建指南` 或同含义二级标题存在
- [ ] `## 📁 项目结构` 或同含义二级标题存在
- [ ] `## 🗺️ 路线图` 或同含义二级标题存在
- [ ] `## 🤝 团队致谢` 或同含义二级标题存在
- [ ] `## 📜 许可声明` 或同含义二级标题存在
- [ ] Footer（版本+Release+联系渠道）存在
- [ ] L1 三个模块三级标题存在：服务器检测 / 配置编辑器 / 系统监控
- [ ] L2 四个模块三级标题存在：Java 管理 / 电源管理 / 网络监控 / 启动脚本识别

## AC-4 L1 模块详略得当
- [ ] 服务器检测：简介 2-4 句 ✓
- [ ] 服务器检测：功能要点 bullet ≥ 6 条 ✓
- [ ] 服务器检测：图片引用存在 ✓
- [ ] 服务器检测：亮点小节 2-4 句 ✓
- [ ] 配置编辑器：简介 2-4 句 ✓
- [ ] 配置编辑器：功能要点 bullet ≥ 6 条 ✓
- [ ] 配置编辑器：图片引用存在 ✓
- [ ] 配置编辑器：亮点小节 2-4 句 ✓
- [ ] 系统监控：简介 2-4 句 ✓
- [ ] 系统监控：功能要点 bullet ≥ 6 条 ✓
- [ ] 系统监控：图片引用存在 ✓
- [ ] 系统监控：亮点小节 2-4 句 ✓
- [ ] 三模块合计 bullet ≥ 20 条 ✓
- [ ] 三模块亮点描述与实际代码功能一致（无虚构未实现能力）✓

## AC-5 L2 模块卡片呈现
- [ ] L2 四模块采用 2×2 卡片布局（表格或视觉分栏）
- [ ] Java 管理：简介 2-4 句 + bullet ≥ 4 条 ✓
- [ ] 电源管理：简介 2-4 句 + bullet ≥ 4 条 ✓
- [ ] 电源管理：明确标注「实验性功能 · 启用需重启 MSMC」或近似文字 ✓
- [ ] 网络监控：简介 2-4 句 + bullet ≥ 4 条 ✓
- [ ] 启动脚本识别：简介 2-4 句 + bullet ≥ 4 条 ✓
- [ ] 四模块合计 bullet ≥ 16 条 ✓
- [ ] L2 末尾三联图 `modules-trio.jpg` 引用存在 ✓

## AC-6 L3 简表完整
- [ ] 基础设施采用 Markdown 表格呈现
- [ ] 表格列 ≥ 4：模块 / 角色 / 关键能力 / 备注 ✓
- [ ] 第 1 行：主题系统 Settings ✓
- [ ] 第 2 行：用户协议 UserAgreement ✓
- [ ] 第 3 行：崩溃恢复 CrashWindow ✓
- [ ] 第 4 行：WebView2 桥接 BridgeService ✓
- [ ] 关键能力列描述与代码真实实现一致 ✓

## AC-7 架构图 + 技术栈正确
- [ ] ASCII 架构图包含 WPF 主程序层（Host/DI/Serilog/WebView2 Core）节点
- [ ] ASCII 架构图包含 Bridge Service 层（API Handlers / EmbeddedResource Provider）节点
- [ ] ASCII 架构图包含 React App 层（Router / Stores / 7 Pages）节点
- [ ] ASCII 架构图包含底层 Native Services 或 System APIs 节点
- [ ] ASCII 架构图纯字符宽度 ≤ 110 列
- [ ] 技术栈矩阵为分层表格：5 行（UI / 渲染 / 逻辑 / 数据 / 工具链）
- [ ] 技术栈：.NET 9、Windows SDK 22000、WPF ✓
- [ ] 技术栈：React 18、Vite 5、TypeScript 5.8 ✓
- [ ] 技术栈：MaterialDesignInXamlToolkit 4.9、MahApps.IconPacks ✓
- [ ] 技术栈：Serilog、WebView2、CommunityToolkit.Mvvm ✓
- [ ] 技术栈：StyleCop、SonarAnalyzer、xUnit ✓

## AC-8 构建指南可复现
- [ ] 前置条件表格存在（.NET 9 SDK / Node 20 / npm ≥ 10 / Win10+ / WebView2）
- [ ] 步骤 1：`git clone` 命令存在且有代码块
- [ ] 步骤 2：前端 `npm install` + `npm run build` 命令存在
- [ ] 前端构建有 MSBuild `--prefix` 用法说明
- [ ] 步骤 3：`dotnet restore` 命令存在
- [ ] 步骤 4：`dotnet build -c Release src/MSMC/MSMC.csproj` 命令存在
- [ ] 步骤 4：标注 `net9.0-windows10.0.22000.0` + `win-x64` RID 说明
- [ ] 步骤 5：`dotnet run --project src/MSMC -c Debug /p:SignAssembly=false` 命令存在
- [ ] EmbeddedResource 模式说明存在（前端构建失败=MSB3073 后端失败）
- [ ] 强签名注意事项说明存在（`/p:SignAssembly=false` 临时关闭）
- [ ] 日志目录说明存在：`bin/**/win-x64/logs/`
- [ ] Win10 WebView2 Runtime 缺失处理说明存在

## AC-9 项目结构树与代码一致
- [ ] ASCII tree 风格展示 `src/`
- [ ] `MSMC/` 主目录节点存在
- [ ] 10 个 Feature 文件夹全部列出来：ConfigEditor / JavaManagement / NetworkMonitor / PowerManagement / ServerDetection / Settings / Shared / Startup / SystemMonitoring / UserAgreement / WebView2
- [ ] `App.xaml(.cs)` 启动入口注释正确
- [ ] `MSMC.Tests/` xUnit 测试目录存在
- [ ] `frontend/` + `src/pages/`（7 Pages）存在
- [ ] `components/` + `stores/` + `hooks/` + `utils/` + `types/` 目录结构存在
- [ ] 每个关键节点附一句话注释 ✓

## AC-10 路线图层次分明
- [ ] ✅ 已实现列条目 ≥ 10 条
- [ ] 🔨 进行中列条目 = 3-5 条
- [ ] 🧭 规划中列条目 = 4-6 条
- [ ] 「已实现」条目均为代码中实际存在功能，无虚报
- [ ] 「进行中」与「规划中」条目不重复、方向清晰

## AC-11 致谢与许可保留得当
- [ ] 核心团队 4 人名单正确：ABI-ZTROS / MochaCello92377 / CatStack / 烟蓝湘
- [ ] 每人附职责标注（项目发起 / 核心翻译 / 前端架构等）
- [ ] 开源项目致谢 bullet ≥ 6 条
- [ ] 开源致谢包含：MDIX / MahApps.IconPacks / Serilog / WebView2 / React / Vite
- [ ] 特别鸣谢段落存在，无健康/病区个人化内容
- [ ] Apache-2.0 主声明存在，链接至 LICENSE
- [ ] 附加条款 1：CREDITS.md 保留完整版权声明 ✓（原文搬运，无修改）
- [ ] 附加条款 2：衍生作品保留版权/免责声明 ✓（原文搬运）
- [ ] 附加条款 3：第三方宣传需书面同意 ✓（原文搬运）
- [ ] 附加条款 4：衍生项目变更需显眼位置声明 ✓（原文搬运）
- [ ] 无新增或删减附加条款 1-4

## AC-12 徽章矩阵呈现
- [ ] 徽章总数量 ≥ 10 枚
- [ ] `.NET 9` / `.NET 9.0` 徽章 ✓
- [ ] `Windows SDK 22000` 徽章 ✓
- [ ] `React 18` 徽章 ✓
- [ ] `C# 13` 或 `C# latest` 徽章 ✓
- [ ] `TypeScript 5.8` 徽章 ✓
- [ ] `TreatWarningsAsErrors` 或 `WAE ON` 徽章 ✓
- [ ] `nullable enable` 徽章 ✓
- [ ] `Windows 10+ x64` 或平台徽章 ✓
- [ ] `Apache-2.0` 或许可证徽章 ✓
- [ ] `v0.9.0-preview.17` 或版本号徽章 ✓
- [ ] `Team ZTROS` 或贡献者徽章 ✓
- [ ] 配色语义正确：蓝=信息 / 绿=通过 / 橙=警告 / 紫=构建 / 灰=元信息

## AC-13 技术参数准确性
- [ ] .NET 版本：README 标注 `.NET 9`，与 csproj `net9.0-windows10.0.22000.0` 一致 ✓
- [ ] Windows SDK 版本：README 标注 `10.0.22000.0`（或 `22000` 缩写），与 csproj 一致 ✓
- [ ] React 版本：README 标注 `18`，与 `frontend/package.json` 中 React 主版本一致 ✓
- [ ] TypeScript 版本：README 标注 `5.8`，与 package.json 一致 ✓
- [ ] Vite 版本：README 标注 `5.x`，与 package.json 一致 ✓
- [ ] 核心翻译数量：README 标注 `36+`，与配置翻译列表实际数量一致 ✓
- [ ] 服务器核心识别数量：README 标注 `36+`，与 ServerDetection 指纹清单一致 ✓
- [ ] 前端路由页数：README 标注 `7 个页面`，与 router 配置一致（Dashboard/Config/System/Network/Power/Java/Settings）✓
- [ ] 版本号：README 标注 `v0.9.0-preview.17`，与 `version.json` 一致 ✓
- [ ] 强签名状态：README 提到 `SignAssembly`，与 csproj 中 `<SignAssembly>true</SignAssembly>` 一致 ✓
