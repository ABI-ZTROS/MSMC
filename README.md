# MSMC — Minecraft Server Management Client

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
