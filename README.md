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
dotnet build McServerGuard.sln
```

也可从 GitHub Actions 的 [Artifacts](https://github.com/ABI-ZTROS/MSMC/actions) 下载预构建版本。

## 项目结构

```
src/McServerGuard/
├── Constants/              # 常量定义（服务器类型、JVM 参数）
├── Models/                 # 数据模型（服务器实例、配置条目、系统指标）
├── Services/
│   ├── ServerDetection/    # 服务器检测（进程扫描、命令行解析、脚本分析）
│   ├── ConfigManagement/   # 配置管理（解析、翻译、校验）
│   ├── SystemMonitoring/   # 系统监控（CPU / 内存 / 磁盘 / 线程）
│   ├── HardwareInfo/       # 硬件信息识别
│   ├── Privilege/          # 权限管理（管理员检测、UAC 提权）
│   ├── AppConfigService.cs # 应用配置持久化
│   ├── MemoryOptimizerService.cs  # 内存优化
│   ├── ThemeService.cs     # 主题管理
│   └── ToastNotificationService.cs # 通知服务
├── ViewModels/             # MVVM ViewModel
├── Views/                  # WPF 界面与自定义控件
├── Converters/             # 值转换器
└── Selectors/              # 模板选择器
```

## 许可声明

本软件**不是开源软件**，不遵循任何开源协议（包括但不限于 MIT、GPL、Apache、BSD 等）。

源代码在 GitHub 上公开仅用于技术交流与透明度展示，不构成对任何权利的许可或放弃。未经开发者书面授权，不得复制、修改、分发、再许可或销售本软件。

详细条款请参阅软件内的用户协议。

---

© 2026 ABI-ZTROS
