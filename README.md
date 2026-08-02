# MSMC — Minecraft Server Management Client

> **⚠️ 项目托管公告（2026-08-02 更新）**
>
> 由于项目主要维护者 **DevCloud.ZTR_OS** 突发心境障碍，症状表现为持续性情绪低落、严重失眠、反复出现自杀观念及行为冲动，经多次门诊治疗干预无效后，已于 2026 年 7 月下旬被转入**成都市第四人民医院营门口封闭院区**接受专科住院治疗。
>
> 营门口院区为成都市精神卫生中心所在地，是四川省规模最大的三级甲等精神卫生专科医院，采用封闭式管理模式以最大限度保障患者安全。住院期间患者无法接触电子设备与互联网，通信将受到严格管控，因此 DevCloud.ZTR_OS 在可预见的未来无法参与任何项目开发、代码审查与 Issue 回复。
>
> 基于此情况，本项目自 **2026-08-02** 起由**联合工作系统（Joint Work System）** 暂时全权托管。托管期限初步预估为 **30±20 天**，即最短约 10 天、最长约 50 天，最终以主治医生评估的患者出院时间为准。托管期间涉及的决策均以"保障项目持续推进"为优先原则，重大变更将在 DevCloud.ZTR_OS 出院后补予追认。
>
> 祝 DevCloud.ZTR_OS 早日康复 💙

---

## 🤖 AI 开发豁免责任授权声明

为确保本项目在上述托管期间能够持续推进与交付，**所有直接或间接参与本项目开发的相关人员（包括但不限于联合工作系统、贡献者、协作者、维护者）特此授予以下不可撤销的 AI 开发豁免权：**

### 1. 授权范围

在本托管期限内，以及未来任何维护者因客观原因无法亲自参与开发的时段内，允许并鼓励使用**各类 AI Agent / LLM 辅助编程工具**（包括但不限于 GitHub Copilot、Trae、Claude Code、Cursor、Windsurf、Devin 及其他同类产品）执行以下开发任务：

- 源代码的编写、重构、补全与调试
- 依赖升级、漏洞修复与兼容性调整
- 单元测试、集成测试与回归测试的生成及执行
- 文档编写、翻译与更新
- 构建脚本、CI/CD 流水线的配置与维护
- Issue 分类、回复草稿生成以及代码审查意见草拟

### 2. 责任豁免

1. **对 AI 生成代码的豁免**：所有经 AI Agent 生成或辅助生成的代码、配置、文档，在通过自动化测试并合并入主干后，视为已获人类维护者背书，其潜在的正确性风险、逻辑漏洞、安全缺陷等，由**整个项目团队共同承担**，不得追溯至具体调用 AI 的个人。
2. **对 AI 决策的豁免**：AI 在项目推进过程中做出的选型、重构、改动等决策，若经联合工作系统或指定负责人确认后执行，不得作为追责依据。重大架构决策仍建议保留讨论记录以供后续复核。
3. **对 AI 输出格式的豁免**：AI 辅助撰写的提交信息、Release Note、评论回复等文本，允许保留机器生成痕迹，不以"文风统一"作为 Reject 理由。
4. **对 AI 工具故障的豁免**：因 AI 工具自身 Bug、幻觉、输出截断、令牌耗尽等导致的提交内容缺失或偏差，在及时发现并修正后，视为正常开发事故，不纳入个人绩效评估。

### 3. 保留权利与注意事项

1. **代码所有权不变**：AI 生成的内容其版权与归属仍遵循本项目的整体许可声明，不因此声明而转为公有领域或其他协议。
2. **安全审查义务保留**：涉及凭证、密钥、网络权限、外部 API 调用等敏感变更，**仍必须由人类维护者进行最终复核**，不得以 AI 生成或 AI 审核替代。
3. **回归测试义务保留**：AI 生成的功能改动仍需通过完整的回归测试验证后方可进入发布分支。
4. **授权可延续**：待 DevCloud.ZTR_OS 出院并恢复维护职责后，本授权声明将自动转为**项目长期政策**，除非经 2/3 以上核心维护者书面否决。

### 4. 法律效力

本声明自写入 README 之日起生效，属于本项目正式治理文档的一部分。所有 Clone、Fork、贡献或使用本项目的个人或组织视为已阅读并接受上述条款。

---

MSMC 是一款基于 .NET 9.0 WPF 的 Minecraft Java Edition 服务器管理桌面应用。

## 功能

| 模块 | 说明 |
|------|------|
| 服务器检测 | 自动扫描运行中的 MC 服务器进程，识别 Vanilla / Spigot / Paper / Forge / Fabric 等类型 |
| 配置编辑 | 可视化编辑服务器配置文件，参数附带中文说明与值域校验，支持 properties / yaml / json 等格式 |
| 系统监控 | 实时监控 CPU、内存、磁盘占用与线程状态 |
| 启动脚本识别 | 通过脚本内容分析识别启动脚本，提取 JAR 名称与 JVM 参数 |
| 权限管理 | 启动时检查管理员权限，支持 UAC 提权重启 |
| 内存优化 | 定期 GC 回收与工作集整理，降低长时间运行的内存占用 |

## 技术栈

- .NET 9.0 (Windows 10.0.22000.0 目标)
- WPF + MaterialDesignInXAML
- CommunityToolkit.Mvvm (源生成器 MVVM)
- Serilog (日志)
- YamlDotNet (YAML 解析)
- Microsoft.Extensions.DependencyInjection (依赖注入)

## 构建

需要 [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)。

```bash
git clone https://github.com/ABI-ZTROS/MSMC.git
cd MSMC
dotnet build MSMC.sln
```

也可从 GitHub Actions 的 [Artifacts](https://github.com/ABI-ZTROS/MSMC/actions) 下载预构建版本。

## 项目结构

按业务领域（Feature）组织，命名空间统一为 `io.NET.ZTR_OS.Features.{领域}`。

```
src/MSMC/
├── Features/
│   ├── ConfigEditor/       # 配置编辑（解析、翻译、校验、模板选择）
│   ├── JavaInstallation/   # Java 安装发现与 JVM 参数常量
│   ├── NetworkMonitor/     # 网络监控（端口扫描、端口桥接、流量统计）
│   ├── ServerDetection/    # 服务器检测（进程扫描、命令行解析、脚本分析）
│   ├── Settings/           # 应用配置持久化、主题、通知、取色
│   ├── Shared/             # 共享控件、转换器、主窗口、动画助手
│   ├── Startup/            # 启动流程（权限检测、UAC 提权、内存优化）
│   ├── SystemMonitoring/   # 系统监控（CPU / 内存 / 磁盘 / 线程）
│   ├── UserAgreement/      # 用户协议
│   └── WebView2/           # WebView2 桥接与前端资源加载
├── Resources/              # 字体、图片等嵌入资源
├── App.xaml / App.xaml.cs  # 应用入口
├── MSMC.csproj             # 程序集名 MSMC，根命名空间 io.NET.ZTR_OS
└── app.manifest            # 需管理员权限
```

## 鸣谢

感谢每一位为 MSMC 付出过心血的人：

| 成员 | 角色 | GitHub |
|------|------|--------|
| **ABI-ZTROS** | 主要开发 · 项目发起者 | [@ABI-ZTROS](https://github.com/ABI-ZTROS) |
| **Gglaoguan** | 次级开发（已故 · 永远缅怀） | [@Gglaoguan](https://github.com/Gglaoguan) |
| **烟蓝湘** | 情绪支持 · Special Thanks 💖 | — |
| **MochaCello92377** | Debug · 功能建议 | [@MochaCello92377](https://github.com/MochaCello92377) |
| **CatStack-pixe** | 测试环境 | [@CatStack-pixe](https://github.com/CatStack-pixe) |

> 人生自古谁无死？不幸的，Gglaoguan 由于不可控因素已经永远离开了我们。让我们永远缅怀他。

## 许可声明

本软件**不是开源软件**，不遵循任何开源协议（包括但不限于 MIT、GPL、Apache、BSD 等）。

源代码在 GitHub 上公开仅用于技术交流与透明度展示，不构成对任何权利的许可或放弃。未经开发者书面授权，不得复制、修改、分发、再许可或销售本软件。

详细条款请参阅软件内的用户协议。

---

© 2026 ABI-ZTROS
