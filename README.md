# 🔮 MSMC — Minecraft Server Management Client

> *"吾等守护者，以代码为刃，以算法为盾，誓要在这片由方块构成的世界里，守护每一台服务器的安宁。"*

---MSMC 是一款 .NET 10.0 WPF 桌面应用，为 Minecraft Java Edition 服务器而生。
它不是普通的控制面板——它是你服务器的 **守护灵**。
它能感知进程的呼吸，解析配置的灵魂，预知崩溃的命运。

## ✨ 守护之力

| 领域 | 能力 |
|------|------|
| **服务器感知** | 自动探测运行中的 MC 服务器 —— Vanilla / Spigot / Paper / Forge / Fabric，无一遁形 |
| **配置编辑** | 可视化管理服务器配置，所有参数翻译为自然中文，附带值域约束与校验 |
| **系统监控** | CPU / 内存 / 磁盘 / 线程 —— 实时感知服务器硬件的每一次脉动 |
| **AI 预知** | MLP 神经网络引擎，分析日志异常、预测崩溃风险、优化配置参数 |
| **启动脚本洞察** | 不看文件名，只看灵魂 —— 通过内容架构分析识别启动脚本 |

## 🏗️ 铸造之术

```
.NET 10.0  WPF  MaterialDesignInXAML (暗夜紫翠主题)
ML.NET  +  ONNX Runtime  (MLP 神经网络推理引擎)
CommunityToolkit.Mvvm  (源生成器驱动的 MVVM)
Serilog  (日志即命运)
YamlDotNet  (万物皆可解析)
```

## 🚀 启动仪式

**前置要求：** 需要安装 [.NET 10.0 SDK/Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

从 GitHub Actions 的 [Artifacts](https://github.com/ABI-ZTROS/MSMC/actions) 下载 `MSMC-win-x64`，解压后双击 `McServerGuard.exe` 即可。

或者从源码编译：

```bash
git clone https://github.com/ABI-ZTROS/MSMC.git
cd MSMC
dotnet build McServerGuard.sln
```

## 📂 封印之书（项目结构）

```
src/McServerGuard/
├── Constants/          # 常量封印 —— 服务器类型、JVM 参数
├── Models/             # 数据模型 —— 服务器实体、配置条目、系统指标
├── Services/
│   ├── ServerDetection/    # 探知之眼 —— 进程扫描、命令行解析、脚本分析
│   ├── ConfigManagement/   # 配置之书 —— 解析、翻译、校验
│   ├── SystemMonitoring/   # 监控之塔 —— CPU/内存/磁盘/线程
│   └── AIService/          # 预知之脑 —— 日志分析、崩溃预测、配置优化
├── ViewModels/         # 意识中枢 —— MVVM 绑定
├── Views/              # 显化之窗 —— WPF 界面
└── Converters/         # 转化之术
```

## ⚖️ 守护者契约

本项目采用 MIT 许可证 —— 自由即正义。

---

<p align="center">
  <sub>Forged by <strong>Wis'adel</strong> with code and conviction</sub><br/>
  <sub>"每一行代码，都是对服务器的誓言。"</sub>
</p>
