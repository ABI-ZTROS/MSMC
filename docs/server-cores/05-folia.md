# Folia 服务器配置文件中文手册

> Folia 是 PaperMC 团队开发的 Paper 分支，引入了**区域化多线程（Regionised Multithreading）**架构，彻底移除主线程，将世界划分为多个独立区域并行 tick，专为 200+ 玩家且玩家分散的大型服务器（如空岛、SMP）设计。
> 继承关系：Vanilla → Spigot → Paper → **Folia**
> 官方 GitHub：https://github.com/PaperMC/Folia
> 官方文档：https://docs.papermc.io/folia
> 下载地址：https://papermc.io/downloads/folia
> 数据来源：PaperMC/Folia 源码 `folia-server/paper-patches/features/0001-Region-Threading-Base.patch`（commit `e48800d`，Folia 26.x）
> 适用版本基准：Folia 1.20.4+ / 26.x（2025–2026 稳定版）

Folia 不是 Paper 的"即插即用"替代品。它**没有独立的全局配置文件**，所有 Folia 新增的多线程区域配置（`ThreadedRegions`）直接追加到 Paper 的 `config/paper-global.yml` 中。⚠️ 由于多线程架构，绝大多数未显式声明 `folia-supported: true` 的 Paper 插件无法运行。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（含 `netty-threads` 网络线程） |
| **config/paper-global.yml** | YAML | **Paper + Folia 追加** | **Paper 全局配置；Folia 在此追加 `threaded-regions` 节（本文档重点）** |
| config/paper-world-defaults.yml | YAML | Paper 继承 | Paper 世界默认配置 |
| kaiiju.yml | — | — | （Kaiiju 才有，Folia 无独立文件） |

> **⚠️ 关于 `config/folia-global.yml` 的说明**
>
> 经 GitHub 源码核实（`0001-Region-Threading-Base.patch` 中的 `ThreadedRegions extends ConfigurationPart` 注册逻辑），**Folia 不存在独立的 `config/folia-global.yml` 文件**。Folia 唯一新增的全局配置节 `threaded-regions` 直接写入 Paper 的 `config/paper-global.yml`，与 Paper 原有的 `chunk-system`、`misc`、`proxies` 等节并列。详见文末「附录：配置文件不存在性核实」。
>
> 因此本文档仅翻译 Folia **新增**的 `threaded-regions` 节，以及 Folia 部署中**最常调优**的若干 Paper 继承节（`chunk-system`、`misc`、`proxies.velocity`）。完整的 Paper 配置请参阅 Paper 手册。

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `threaded-regions.threads`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串 / `enum` 枚举 / `double` 浮点。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载（Folia 多数项需重启）。
- **硬件建议**：Folia 官方推荐**至少 16 个物理核心**（不是线程），2 核 VPS 几乎无收益。

---

## config/paper-global.yml —— Folia 新增节

### threaded-regions（线程化区域 / Folia 多线程核心）

> 这是 Folia 唯一新增到 `paper-global.yml` 的配置节，由 `io.papermc.paper.threadedregions` 包下的 `ThreadedRegions` 配置类（继承 `ConfigurationPart`）加载。它控制**区域 tick 线程池**与区域划分粒度，是 Folia 性能调优的核心。
>
> **80% 上限原则**：Folia 官方强调，所有可配置线程（tick 线程 + 区块系统 IO 线程 + 区块系统工作线程 + Netty IO 线程 + GC 并发线程 `-XX:ConcGCThreads`）的总和**不应超过物理核心数的 80%**，需为插件与后台任务预留余量，否则可能因线程饥饿导致崩溃。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `threaded-regions.threads` | 区域 tick 线程数 | int | `-1`（-1 = 自动；≥ 1 = 手动） | ✅ | 区域 tick 循环所使用的线程池大小。**-1（自动）**：根据可用 CPU 自动计算（`max(可用核心数 − 预留, 1)`）。**手动设置**：建议设为「物理核心数 − Netty IO 线程 − 区块 IO 线程 − 区块工作线程 − GC 并发线程」后的剩余值，且总分配不超过 80%。例如 32 核 / 500 人服可设约 10。修改后必须重启。 |
| `threaded-regions.grid-exponent` | 区域网格指数 | int | `4`（≥ 0） | ✅ | 控制区域划分的网格粒度。计算公式：每个网格单元的边长 = `2^gridExponent` 个区块。默认 `4` = 16 区块边长（16×16 区块 = 256 区块为一个网格单元）。值越大，区域越大、合并越激进、线程并行度越低；值越小，区域越细碎、并行度越高但跨区域交互开销越大。**非高级用户请勿修改**，错误值会显著降低性能。 |
| `threaded-regions.scheduler` | 区域调度算法 | enum | `EDF`（`EDF`） | ✅ | 区域 tick 任务的调度策略。`EDF` = Earliest Deadline First（最早截止期优先），按区域 tick 截止时间排序优先调度最紧迫的区域。目前仅 `EDF` 一种已实现值。修改需重启。 |

---

## config/paper-global.yml —— Paper 继承节（Folia 部署高频调优）

> 以下节虽来自 Paper，但在 Folia 多线程环境下需要**单独重新分配线程预算**，是 Folia 调优必看项。Folia 不修改这些键的语义，仅改变其推荐取值。

### chunk-system（区块系统线程池）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `chunk-system.io-threads` | 区块 IO 线程数 | int | `1`（≥ 1） | ✅ | 负责从磁盘读写区块文件的线程数。Folia 官方建议**每 200–300 名玩家约 3 个**。预生成世界后可适当下调。 |
| `chunk-system.worker-threads` | 区块工作线程数 | int | `1`（≥ 1） | ✅ | 负责区块生成 / 装饰计算的线程数。Folia 官方建议**预生成后每 200–300 名玩家约 2 个**；未预生成时需大幅增加（曾测试 16 线程仍偏慢）。 |

### misc（杂项）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `misc.region-file-cache-size` | 区域文件缓存大小 | int | `256`（≥ 0） | ✅ | 缓存的 Region 文件（.mca）句柄数。大型世界 / 大量玩家分散时调大（如 512）可减少磁盘 IO，但占用更多内存。 |

### proxies.velocity（Velocity 代理）

> 当 Folia 前置 Velocity 代理时启用。代理层负责压缩，Folia 侧可禁用网络压缩以节省 CPU。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `proxies.velocity.enabled` | 启用 Velocity 转发 | bool | `false`（`true`/`false`） | ✅ | 是否启用 Velocity 现代转发（modern forwarding）。启用后玩家信息（IP、UUID、皮肤）由 Velocity 转发，Folia 侧 `server.properties` 的 `online-mode` 应设为 `false`。 |
| `proxies.velocity.secret` | Velocity 共享密钥 | string | ` `（空 = 禁用） | ✅ | 与 Velocity `forwarding.secret` 一致的密钥，用于验证代理身份。**生产环境必须设置强密钥**，留空则任何人都可伪造玩家身份。 |
| `proxies.velocity.online-mode` | 在线模式（Velocity 侧） | bool | `false`（`true`/`false`） | ✅ | 表示 Velocity 是否已做 Mojang 正版验证。设为 `true` 时 Folia 信任 Velocity 转发的正版身份。 |

---

## spigot.yml —— 网络线程（Folia 调优相关）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.netty-threads` | Netty IO 线程数 | int | `4`（≥ 1） | ✅ | 处理玩家网络数据包的 Netty 线程数。Folia 官方建议**每 200–300 名玩家约 4 个**。500 人服可设 8。需计入 80% 总预算。 |

---

## 配置示例（config/paper-global.yml，Folia 多线程部分）

> 以下为 96 核 EPYC / 500 人测试服的真实调优示例（参考 Cubxity 2023 测试），数值仅为起点，需根据实际负载调整。

```yaml
# ========== Folia 新增：区域化多线程核心 ==========
threaded-regions:
  threads: 70          # 区域 tick 线程数（96 核机器，扣除其他线程后剩余）
  # grid-exponent: 4   # 默认 4，非高级用户不要改
  # scheduler: EDF     # 默认 EDF，目前仅此一种

# ========== Paper 继承：Folia 需重新分配预算 ==========
chunk-system:
  io-threads: 30       # 区块 IO 线程（每 200-300 人约 3 个）
  worker-threads: 10   # 区块工作线程（预生成后每 200-300 人约 2 个）

misc:
  region-file-cache-size: 512   # 大型世界调大可减磁盘 IO

proxies:
  velocity:
    enabled: true                # 前置 Velocity 时启用
    online-mode: true            # Velocity 已做正版验证
    secret: "<your-strong-secret>"

# chunk-loading-basic（继承自 Paper，Folia 可限流以保护区块系统）
chunk-loading-basic:
  player-max-chunk-generate-rate: 40.0   # 每玩家每 tick 生成区块上限
  player-max-chunk-load-rate: 40.0       # 每玩家每 tick 加载区块上限
  player-max-chunk-send-rate: 40.0       # 每玩家每 tick 发送区块上限
```

对应 `spigot.yml`：

```yaml
settings:
  netty-threads: 50     # Netty IO 线程（每 200-300 人约 4 个，500 人可设 8-16）
```

---

## 优化建议（针对大型多线程服务器）

1. **预生成世界**：上线前务必用 Chunky 等插件预生成世界。预生成后区块工作线程需求大幅下降（从 16+ 降到 2–4 / 300 人），是 Folia 性能的第一杠杆。
2. **遵守 80% 上限**：tick 线程 + 区块 IO + 区块工作 + Netty + GC 并发线程（`-XX:ConcGCThreads`）总和 **< 物理核心数 × 80%**，为插件与后台任务留出余量。
3. **线程分配起点**（每 200–300 人）：Netty IO 4 个、区块 IO 3 个、区块工作 2 个（预生成后），剩余核心（至 80%）给 `threaded-regions.threads`。
4. **`threads` 留 -1 还是手填**：小白建议保持 `-1`（自动）；进阶用户在压测后手动填精确值，通常比自动更优。
5. **`grid-exponent` 慎改**：默认 `4`（16 区块边长）对绝大多数场景最优。仅在玩家高度密集或极度分散的极端场景下尝试 `3` 或 `5`，并压测对比。
6. **代理 + 禁用压缩**：前置 Velocity 时启用 `proxies.velocity`，并在 Folia 侧将 `server.properties` 的 `network-compression-threshold` 设为 `-1`（禁用），由 Velocity 统一压缩，节省 Folia CPU。
7. **GC 线程计入预算**：使用 `-XX:ConcGCThreads=n`（注意不是 `ParallelGCThreads`）设置 GC 并发线程，并把这些线程也算进 80% 总预算。
8. **插件兼容性**：仅加载在 `plugin.yml` 中显式声明 `folia-supported: true` 的插件。Folia 官方明确「兼容性预期为 0」，所有不假设主线程的旧插件都需重写。
9. **硬件门槛**：低于 16 物理核心的机器不建议上 Folia，Paper 单线程反而更快。Folia 收益在 200+ 玩家且玩家分散时才显著。

---

## 附录：配置文件不存在性核实

经以下源码核实，PaperMC/Folia **不存在**独立的 `config/folia-global.yml` 文件：

1. **`folia-server/paper-patches/features/0001-Region-Threading-Base.patch`** 中定义了 Folia 唯一新增的全局配置类：
   ```java
   public class ThreadedRegions extends ConfigurationPart {
       public int threads = -1;
       public int gridExponent = 4;
       public io.papermc.paper.threadedregions.TickRegionScheduler.SchedulerType scheduler
           = io.papermc.paper.threadedregions.TickRegionScheduler.SchedulerType.EDF;
       @PostProcess
       public void postProcess() { ... }
   }
   ```
   该类通过 Paper 的 `ConfigurationPart` 机制注册到 **`config/paper-global.yml`** 的 `threaded-regions` 节下，与 Paper 原有的 `chunk-system`、`misc`、`proxies` 节并列，**不会**生成独立的 `folia-global.yml`。

2. **Folia README** 与官方文档均只提及在「全局配置（global config）」即 `paper-global.yml` 中调整 `threaded-regions.threads`，从未引用 `folia-global.yml`。

3. **社区部署示例**（如 Cubxity 2023 的 96 核测试、PaperMC 官方 FAQ）均直接修改 `config/paper-global.yml`，不存在 `config/folia-global.yml` 路径。

> **命名提示**：早期 Folia 版本（2023 年中前后）的社区文档中曾将此节误记为 `thread-regions`，当前源码（Folia 26.x）的规范键名为 `threaded-regions`（与 `ThreadedRegions` 类名一一对应）。若你的配置文件中是 `thread-regions`，请重命名为 `threaded-regions`。

> 参考来源：PaperMC/Folia 源码 [`0001-Region-Threading-Base.patch`](https://github.com/PaperMC/Folia/blob/master/folia-server/paper-patches/features/0001-Region-Threading-Base.patch)、[Folia 官方 FAQ](https://github.com/PaperMC/Folia#faq)、[Cubxity 96 核测试报告](https://cubxity.dev/blog/folia-test-july-2023)。
