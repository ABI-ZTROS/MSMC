# MSMC on Android 设计

> 状态：已获用户逐节确认（2026-09-05 brainstorming）。下一步转 writing-plans 制定实施计划。

**Goal:** 在 Android 上交付一个强制 root 的 Minecraft Java 服务器管理 App（MSMC on Android）：无 GUI 管理页，改为内网网页面板 + 开服成功自动调起系统浏览器访问。

**核心决策（用户逐项确认）**
- 部署形态：独立 Android App（APK）
- 技术栈：.NET for Android（`net9.0-android`）单体 —— 最大复用 `MSMC.Shared` 与 MSMC-on-Linux 的 `BridgeHost`/action 注册表
- Java runtime：强制 root；**内置完整 Termux 环境**（shell/bash/wget/ssh 全量包），root 解压即用
- **内置 JDK：17 / 21 / 25 / 26 四版本全捆绑**（覆盖全 MC 版本）
- **CI 双 flavor**：`internal`（完整 Termux + 4 JDK 捆绑，离线开箱）/ `external`（完整 Termux、不含 JDK 捆绑），各自产出 APK
- **选 JDK：自动识别 MC 版本映射 + 每实例可手动覆盖**（1.17–1.20.4→17，≥1.20.5→21+）
- **非内置版 JDK 来源：检测 Termux 已有 JDK 优先 + 引导下载兜底**（下载到 app 私有目录，源为 Termux 仓库 aarch64 .deb）
- root 用途：全部能力（性能调优 / 网络转发 / 深度系统信息 / 常驻自启）
- 服务器类型：Java 版（Paper/vanilla/Spigot 生态），支持多开
- 浏览器形态：`ACTION_VIEW` 调起系统默认浏览器（不内嵌 WebView，不注入 JS 桥）
- root 调用：topjohnwu **libsu**（core + nio + busybox，版本 6.0.0）—— 通过 .NET for Android Java Bindings 绑定为 C# API，不搓 su 轮子
- 仓库：新建独立公开仓库 `ABI-ZTROS/MSMC-on-Android`，一次导入 `MSMC.Shared`（63 cs）+ `frontend` 全量，独立演进

---

## 1. 架构

```
┌─────────────────────────────────────────────────┐
│  MSMC-on-Android（net9.0-android 单体 APK）       │
│  ┌─────────────────────────────────────────────┐ │
│  │ 极简壳：MainActivity（启停/状态）+ 前台服务     │ │
│  │   → 通知栏常驻 · 开机自启（BOOT_COMPLETED）    │ │
│  │   · 无 GUI 管理页                            │ │
│  ├─────────────────────────────────────────────┤ │
│  │ WebPanel：内嵌 HTTP 服务器（0.0.0.0:8080）    │ │
│  │   → 复用 BridgeHost 思路 + MSMC.Shared 逻辑   │ │
│  │   → 托管现有 React 前端（静态 + JSON API）     │ │
│  ├─────────────────────────────────────────────┤ │
│  │ RootService（libsu core/nio/busybox，Bindings）│ │
│  │ TermuxRuntime（内置 openjdk-21 aarch64）      │ │
│  │ ServerSupervisor（多实例 java 进程管理）        │ │
│  │ PowerManager（taskset/renice/OOM 保护）       │ │
│  │ NetworkManager（iptables 转发/防火墙）         │ │
│  └─────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────┘
       开服成功 → ACTION_VIEW 调系统浏览器
                 → http://<手机局域网IP>:8080
```

组件分解（隔离 + 单职责 + 明界面）：
1. **WebPanel**：监听 0.0.0.0:8080（可配置），复用 MSMC-on-Linux `BridgeHost` 同源 HTTP 思路（静态托管 + `/api/invoke` + `/api/poll` + `/health`）；静态不鉴权、API 携带 token 鉴权；未构建 dist 时显示占位页。
2. **RootService**：封装 libsu（Bindings）——`Shell.Su(..).Exec()`、`Shell.IsAppGrantedRoot()`、`SuFile` IO；无 root 时 App 降级为引导页（说明 + 跳转 KernelSU Manager 授权）。
3. **TermuxRuntime（完整 Termux）**：捆绑 Termux 完整 bootstrap（shell/bash/wget/ssh 全量包）+ 内置版额外捆绑 JDK 17/21/25/26；root 解压到 `/data/data/<pkg>/termux`；首次校验 `java -version`。
4. **ServerSupervisor**：多开——每实例独立目录/内存上限/CPU 亲和性/启动参数；`setsid` 进程组启动防杀；日志落盘；进程退出监测 + 可选崩溃自动重启 N 次；**JDK 自动识别（MC 版本映射）+ 手动覆盖**。
5. **PowerManager**：root 下 taskset 锁核、renice、OOM 保护（oom_score_adj）。
6. **NetworkManager**：root 下 iptables/nftables 端口转发与防火墙；监听端口占用查询（/proc/net）。

## 1.5 内置版 / 非内置版（CI 双 flavor）

同一代码库、Android Gradle 双 flavor，各自的 JDK 供给链路：

| | internal（内置版） | external（非内置版） |
|---|---|---|
| Termux 运行时 | 完整捆绑 | 完整捆绑 |
| JDK 17/21/25/26 | 全部捆绑（离线开箱） | 不捆绑 |
| JDK 来源 | 内置（Termux 仓库 aarch64 .deb 解包，CI 时打进 assets） | ① 检测 Termux 已有 JDK ② 引导从 Termux 仓库下载到 app 私有目录 |
| APK 体积 | 大（约 300–400MB） | 小（约 40–80MB） |
| 适用 | 离线机 / 开箱即用 | 体积敏感 / 已有 JDK / 允许首启联网 |

两者共享全部功能代码，差异仅限 JDK 捆绑与供给链路。

## 2. 功能面与网页 API

| 模块 | 能力 | 复用来源 |
|---|---|---|
| 监控 | CPU/内存/磁盘/线程、每核用量、Java 进程 | MSMC.Shared + /proc（Android 内核通用） |
| 服务器多开 | 实例列表、启停/重启、eula 同意、内存/启动参数 | MSMC.Shared + ServerSupervisor |
| 性能 | CPU 亲和性（taskset）、优先级（renice）、OOM 保护 | RootService(libsu) |
| 网络 | iptables 端口转发/防火墙、占用查询 | RootService(libsu) |
| 调度 | 定时启停（cron 式） | MSMC.Shared 原样复用 |
| 通知 | Discord/邮箱/Webhook + Android 通知 | MSMC.Shared + Android 平台通知 |
| 市场 | Modrinth/Hangar/Spiget 插件下载安装 | MSMC.Shared 原样复用 |
| 配置 | server.properties 等可视化编辑 | MSMC.Shared 原样复用 |

**开服 → 自动打开浏览器流**
1. 用户点"开服"（网页或通知按钮）
2. ServerSupervisor 以 root 拉起 `setsid java -Xmx.. -jar server.jar nogui`
3. 输出 `Done` → 捕获 → `ACTION_VIEW` 浏览器访问 `http://<手机IP>:8080`（本机 `http://127.0.0.1:8080`）

**网页安全**：内网 0.0.0.0 监听 + 只读启动 token（局域网访问需登录，存 localStorage）；静态资源免鉴权、API 鉴权；防内网他人乱操作。

## 3. 错误处理

- **无 root**：`Shell.isAppGrantedRoot()` 失败 → 引导页（说明 + 一键跳 KernelSU Manager 授权），拒绝开服
- **Java 缺失/损坏**：首次解压后校验 `java -version`；失败重试一次；仍失败报错并提供重置
- **开服失败**：捕获退出码 + 日志尾部 100 行回显网页；eula 未同意自动生成并 patch 为 true
- **进程被杀/崩溃**：标记异常退出；可选崩溃自动重启 N 次（默认关）
- **鉴权失败**：API 401 + 重定向登录页

## 4. 测试

- 纯逻辑（Scheduler/Market/Config/Persistence）：单元测试（CI 跑）
- root 层：真机冒烟清单（su 探测/锁核/iptables/4×JDK java -version/开服），CI 仅编译级验证
- 网页 API：MSMC-on-Linux 式 smoke（HTTP 服务器 / invoke / poll / token 鉴权），CI 无 root 可跑协议层
- 构建：GitHub Actions 出 **internal + external 双 flavor** 的 debug+release APK（arm64），附测试报告

## 5. 里程碑 M0→M4

- **M0 骨架**：新仓库 + 复制 MSMC.Shared/frontend + MSMC.Android 最小 APK（双 flavor 占位：无 root 可装，显示引导页）
- **M1 root + TermuxRuntime**：libsu Bindings + 授权流 + 完整 Termux 解压校验 + 4×JDK `java -version` 通过（internal 捆绑 / external 检测+下载）
- **M2 核心开服**：ServerSupervisor 多开（含 JDK 自动识别+手动覆盖）+ 网页面板壳（登录/仪表盘/启停）+ 开服自动开浏览器端到端
- **M3 深化**：监控/性能/网络/调度/通知/市场/配置 全接通
- **M4 打磨**：开机自启、保活、崩溃重启、真机清单全过、internal/external APK 发布产物

## 6. 风险与取舍

- libsu Bindings 一次性适配成本（已接受）
- 内置 openjdk 体积大（已接受；离线可用优先）
- Android /proc 与 Linux 差异小，监控逻辑基本复用；无 X11 / 无 systemd，桌面级路径不可用（无影响，本方案无 GUI）
- Android 上 `nohup`/`setsid`、bionic 差异由 TermuxRuntime 的完整环境吸收