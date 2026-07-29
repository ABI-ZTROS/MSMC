# Quilt 服务器配置文件中文手册

> Quilt 是 Fabric 的社区驱动分支，强调开放治理与现代化 API 设计，配置与 Fabric 几乎一致。
> 继承关系：Vanilla → Fabric → Quilt（与 Fabric 高度兼容，可直接加载 Fabric 模组）
> 官方 GitHub：https://github.com/QuiltMC/quilt
> 官方网站：https://quiltmc.org/
> 官方安装页：https://quiltmc.org/en/install/
> 数据来源：Quilt 安装器源码 / 官方文档 / 社区开服教程
> 适用版本基准：Quilt Loader 0.25+（MC 1.20 ~ 1.21.x）

## 核心简介

**Quilt 与 Fabric 一样是模组加载器，不是完整的服务端实现。** 它的工作原理与 Fabric 完全一致：用一个小型「启动器 JAR」包装原版 `server.jar`，在原版服务端启动之前注入模组加载逻辑。

Quilt 于 2023 年从 Fabric 分叉而来，主要差异在**项目治理**（社区驱动而非任何公司控制）和**扩展 API**（Quilt Standard Libraries, QSL，是 Fabric API 的扩展超集）。技术上 Quilt 与 Fabric **高度兼容**，绝大多数 Fabric 模组可直接在 Quilt 上运行，无需修改。

Quilt 服的启动入口是 `quilt-server-launch.jar`（注意：是 `launch` 不是 `launcher`），它会读取同目录下的 `quilt-server-launcher.properties`（注意：是 `launcher` 不是 `launch`）找到原版 `server.jar` 的路径，然后加载 `mods/` 下的模组并启动游戏。

> ⚠️ **命名陷阱**：Quilt 的文件命名沿用 Fabric 的不一致传统——JAR 文件叫 `quilt-server-launch.jar`（无 er），而 properties 文件叫 `quilt-server-launcher.properties`（有 er）。这是历史遗留，请照抄即可，不要试图统一命名。

> 💡 **小白类比**：Quilt 就是 Fabric 的「社区改良版糖浆」，配方几乎一样，但额外加了一些「风味添加剂」（QSL）。糖浆瓶上仍贴着「白水位置」便签（即 `quilt-server-launcher.properties`），用法和 Fabric 完全相同。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| eula.txt | 文本 | Vanilla 继承 | 必须 `eula=true` 才能启动 |
| server.jar | 二进制 | Vanilla 继承 | 原版 Minecraft 服务端 JAR |
| **quilt-server-launch.jar** | 二进制 | **Quilt 专属** | **Quilt 启动入口（实际启动的就是它）** |
| **quilt-server-launcher.properties** | Properties | **Quilt 专属** | **Quilt 启动器配置（本文档重点，仅 1 个键）** |
| mods/ | 目录 | Quilt 专属 | 存放模组 JAR 文件（兼容 Fabric 模组） |
| config/ | 目录 | 各模组专属 | 各模组的配置文件（Quilt 自身无配置） |

> 说明：Quilt 与 Fabric 一样不引入任何游戏机制配置，所有「服务器怎么开」的选项都在 `server.properties` 中，请参阅 Vanilla 手册。Quilt 与 Fabric 在配置层面**几乎完全相同**，主要差异在文件名前缀（`quilt-` vs `fabric-`）和主类名（`org.quiltmc.loader.impl.launch.server.QuiltServerLauncher` vs `net.fabricmc.loader.impl.launch.server.FabricServerLauncher`）。

## quilt-server-launcher.properties（Quilt 启动器配置）

这是一个极简的 Properties 格式文件，由 Quilt 安装器在安装服务端时自动生成，与 `quilt-server-launch.jar` 放在同一目录下。文件通常**只有一行**配置。

### 阅读约定

- **键名**：保持原样不翻译（Properties 格式，等号前为键名）。
- **值类型**：`string` 字符串 / `路径` 文件路径。
- **取值范围**：任意有效的相对/绝对路径字符串。
- **需重启**：✅ 表示修改后必须重启服务器才能生效（Quilt 启动器仅在启动时读取此文件）。

---

### 启动器配置项

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `serverJar` | 原版服务端 JAR 路径 | string / 路径 | `server.jar`（任意相对/绝对路径） | ✅ | 指向**原版 Minecraft 服务端 JAR 文件**的路径。Quilt 启动器会加载这个 JAR，并在其启动前注入模组加载逻辑（Quilt Loader + QSL）。默认值 `server.jar` 表示与启动器同目录下的 `server.jar`。**何时需要修改**：1）若你把原版 JAR 重命名为 `vanilla.jar`（如某些主机面板要求启动入口必须叫 `server.jar`），则需把此值改为 `vanilla.jar`，再把 `quilt-server-launch.jar` 重命名为 `server.jar`；2）若原版 JAR 在其他目录，可填写相对路径（相对启动器 JAR 所在目录）或绝对路径。**⚠️ 路径错误会导致启动失败**，提示找不到主类 `org.quiltmc.loader.impl.launch.server.QuiltServerLauncher` 或找不到 JAR。 |

---

## 配置示例（quilt-server-launcher.properties 完整默认值）

```properties
serverJar=server.jar
```

### 重命名场景示例（主机面板要求入口为 server.jar 时）

```properties
# 步骤：1) 把原版 server.jar 重命名为 vanilla.jar
#       2) 把 quilt-server-launch.jar 重命名为 server.jar
#       3) 修改本文件如下
serverJar=vanilla.jar
```

---

## Quilt 服启动方式

理解 Quilt 配置文件后，整个启动流程与 Fabric 几乎一致：

1. **入口 JAR 是 `quilt-server-launch.jar`**，不是 `server.jar`！
2. 启动命令示例：
   ```bash
   java -Xmx4G -Xms2G -jar quilt-server-launch.jar nogui
   ```
3. Quilt 启动器读取 `quilt-server-launcher.properties` 找到原版 `server.jar`。
4. 启动器加载原版 JAR 并注入 Quilt Loader（含 QSL）。
5. Quilt Loader 扫描 `mods/` 目录加载模组（兼容 Fabric 模组）。
6. 进入正常的 Minecraft 服务端启动流程（读 `server.properties`、生成世界等）。

### 首次启动流程

1. 从 https://quiltmc.org/en/install/ 下载 Quilt 安装器。
2. 运行安装器，选择 `Server` 选项卡，选择 MC 版本和 Quilt Loader 版本，指定安装目录。
3. 安装器会生成 `quilt-server-launch.jar`、`server.jar`（原版）、`quilt-server-launcher.properties`、`libraries/` 等。
4. **额外步骤（Quilt 特有）**：下载 **Quilt Standard Libraries (QSL)** 模组 JAR（从 https://modrinth.com/mod/qsl 获取对应版本），放入 `mods/` 目录。许多 Quilt 模组依赖 QSL。
5. 运行 `java -jar quilt-server-launch.jar nogui`，首次启动会因 `eula=false` 而退出。
6. 编辑 `eula.txt` 把 `eula=false` 改为 `eula=true`。
7. 再次启动，服务器正常运行。
8. 把模组 JAR 放入 `mods/` 目录，重启服务器以加载模组。

### 推荐启动脚本（含 Aikar's Flags）

```bash
java -Xms4G -Xmx4G \
  -XX:+UseG1GC -XX:+UnlockExperimentalVMOptions \
  -XX:MaxGCPauseMillis=100 -XX:+DisableExplicitGC \
  -XX:TargetSurvivorRatio=90 -XX:G1NewSizePercent=50 \
  -XX:G1MaxNewSizePercent=80 -XX:G1MixedGCLiveThresholdPercent=35 \
  -XX:+AlwaysPreTouch -XX:+ParallelRefProcEnabled \
  -Dusing.aikars.flags=mcflags.emc.gs \
  -jar quilt-server-launch.jar nogui
```

> 调整 `-Xms` 和 `-Xmx` 为你想分配的内存大小。Quilt 服内存占用与 Fabric 接近。

---

## 优化建议（针对模组服管理员）

### 🆚 Quilt vs Fabric：何时选 Quilt？

- **Quilt 优势**：社区驱动治理、扩展 API（QSL）提供更多模组开发能力、保留 Fabric 兼容性。
- **Fabric 优势**：生态更成熟、模组数量稍多、文档更完善、加载器更轻量。
- **互通性**：Quilt 默认兼容大多数 Fabric 模组（通过 Quilt 兼容层加载 Fabric API），但少数依赖 Fabric Loader 内部实现细节的模组可能不兼容。建议测试后再大规模使用。

### ⚡ 性能优化模组推荐

Quilt 服可直接使用 Fabric 生态的性能优化模组（同样仅服务端需要）：

- **Lithium**：通用游戏逻辑优化（物理、AI、调度等），几乎零副作用。
- **FerriteCore**：大幅降低内存占用（节省 30-50%）。
- **Krypton**：网络栈优化，减少带宽和 CPU 开销。
- **ServerCore**：多项服务端优化（实体、区块等）。
- **LazyDFU**：延迟数据包注册表初始化，加快启动。

> 这些模组都标注支持 Fabric，由于 Quilt 兼容 Fabric API，可直接放入 `mods/` 使用。

### 🧹 模组管理小贴士

- **QSL 是 Quilt 的必需库**：Quilt 标准库（QSL）相当于 Fabric API 的扩展超集。Quilt 原生模组通常需要 QSL 才能运行。Fabric 模组则通过 Quilt 的 Fabric 兼容层加载，无需额外操作。
- **客户端模组不要放进服务端**：标签为 `CLIENT` 的模组（如 OptiFine/Sodium/Iris 等渲染类模组）只装在客户端。误装到服务端可能引起崩溃或行为异常。
- **`mods/` 子目录**：Quilt 与 Fabric 一样支持在 `mods/` 下建子目录组织模组。
- **混合 Fabric 与 Quilt 模组**：Quilt 设计上支持与 Fabric 模组共存，但若两个模组都修改同一游戏行为（如同时装了 Fabric 版和 Quilt 版的同一模组），会冲突。**不要同时安装同一模组的 Fabric 版和 Quilt 版**。

### 🔄 升级 Quilt

1. 用新版 Quilt 安装器重新安装（或直接替换 `quilt-server-launch.jar` 和 `libraries/`）。
2. 升级 QSL 到对应 MC 版本。
3. 如同时升级 MC 版本，需下载对应版本的原版 `server.jar` 覆盖旧文件。
4. 重新启动。

### 🌐 Java 版本要求

| MC 版本 | 最低 Java 版本 | 推荐 JDK |
|---|---|---|
| 1.17 ~ 1.17.1 | Java 16 | Temurin 17 |
| 1.18 ~ 1.20.4 | Java 17 | Temurin 17 |
| 1.20.5+ ~ 1.21.x | Java 21 | Temurin 21 |

低版本 Java 会直接启动失败。验证：`java -version`。

---

## 参考链接

- 官方网站：https://quiltmc.org/
- 官方安装页：https://quiltmc.org/en/install/
- QSL（Quilt Standard Libraries）下载：https://modrinth.com/mod/qsl
- GitHub 源码：https://github.com/QuiltMC/quilt
- QuiltMC Wiki：https://wiki.quiltmc.org/
- 社区开服教程（Bilibili）：https://www.bilibili.com/opus/825124687140880438

---

> ⚠️ **免责声明**：Quilt 处于活跃开发中，配置文件结构稳定（沿用 Fabric 模式）。本文档基于 Quilt Loader 0.25+ / MC 1.20+ 整理。如遇新版安装器生成的额外键，请以实际文件内容为准。
