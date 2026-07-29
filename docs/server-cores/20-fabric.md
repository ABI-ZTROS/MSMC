# Fabric 服务器配置文件中文手册

> Fabric 是 Minecraft 的轻量级模组加载器，启动快、占用低，配置极简。
> 继承关系：Vanilla → Fabric（在原版服务端基础上加载 mods/ 下的模组 JAR）
> 官方 GitHub：https://github.com/FabricMC/fabric
> 官方文档：https://docs.fabricmc.net/
> 官方 Wiki：https://wiki.fabricmc.net/
> 数据来源：Fabric 安装器源码 / 官方 Wiki / 社区开服教程
> 适用版本基准：Fabric Loader 0.15+（MC 1.20 ~ 1.21.x）

## 核心简介

**Fabric 是模组加载器，不是完整的服务端实现。** 它的工作原理是：用一个小型「启动器 JAR」包装原版 `server.jar`，在原版服务端启动之前注入模组加载逻辑，然后正常加载 Minecraft 服务端。因此 Fabric 几乎**不引入任何额外配置文件**，所有「服务器行为」相关的配置仍然走原版 `server.properties`，Fabric 自己只关心一件事：**「原版 server.jar 在哪里？」**

Fabric 服的启动入口是 `fabric-server-launch.jar`（不是 `server.jar`），它会读取同目录下的 `fabric-server-launcher.properties` 找到原版 `server.jar` 的路径，然后加载 `mods/` 下的模组并启动游戏。

> 💡 **小白类比**：把原版 `server.jar` 想象成一杯白水，Fabric 就是糖浆。糖浆自己不能喝，必须先告诉它「白水在哪」才能调出一杯糖水。`fabric-server-launcher.properties` 就是糖浆瓶上贴的那张「白水位置」便签。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| eula.txt | 文本 | Vanilla 继承 | 必须 `eula=true` 才能启动 |
| server.jar | 二进制 | Vanilla 继承 | 原版 Minecraft 服务端 JAR |
| **fabric-server-launch.jar** | 二进制 | **Fabric 专属** | **Fabric 启动入口（实际启动的就是它）** |
| **fabric-server-launcher.properties** | Properties | **Fabric 专属** | **Fabric 启动器配置（本文档重点，仅 1 个键）** |
| .fabric/ | 目录 | Fabric 专属 | Fabric 内部缓存（缓存库文件、版本信息等） |
| mods/ | 目录 | Fabric 专属 | 存放模组 JAR 文件 |
| config/ | 目录 | 各模组专属 | 各模组的配置文件（Fabric 自身无配置） |

> 说明：Fabric 自身不引入任何游戏机制配置，所有「服务器怎么开」的选项（端口、视距、白名单、难度、游戏模式等）都在 `server.properties` 中，请参阅 Vanilla 手册。

## fabric-server-launcher.properties（Fabric 启动器配置）

这是一个极简的 Properties 格式文件，由 Fabric 安装器在安装服务端时自动生成，与 `fabric-server-launch.jar` 放在同一目录下。文件通常**只有一行**配置。

### 阅读约定

- **键名**：保持原样不翻译（Properties 格式，等号前为键名）。
- **值类型**：`string` 字符串 / `路径` 文件路径。
- **取值范围**：任意有效的相对/绝对路径字符串。
- **需重启**：✅ 表示修改后必须重启服务器才能生效（Fabric 启动器仅在启动时读取此文件）。

---

### 启动器配置项

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `serverJar` | 原版服务端 JAR 路径 | string / 路径 | `server.jar`（任意相对/绝对路径） | ✅ | 指向**原版 Minecraft 服务端 JAR 文件**的路径。Fabric 启动器会加载这个 JAR，并在其启动前注入模组加载逻辑。默认值 `server.jar` 表示与启动器同目录下的 `server.jar`。**何时需要修改**：1）若你把原版 JAR 重命名为 `vanilla.jar`（如某些主机面板要求启动入口必须叫 `server.jar`），则需把此值改为 `vanilla.jar`，再把 `fabric-server-launch.jar` 重命名为 `server.jar`；2）若原版 JAR 在其他目录，可填写相对路径（相对启动器 JAR 所在目录）或绝对路径。**⚠️ 路径错误会导致启动失败**，提示找不到主类或找不到 JAR。 |

---

## 配置示例（fabric-server-launcher.properties 完整默认值）

```properties
serverJar=server.jar
```

### 重命名场景示例（主机面板要求入口为 server.jar 时）

```properties
# 步骤：1) 把原版 server.jar 重命名为 vanilla.jar
#       2) 把 fabric-server-launch.jar 重命名为 server.jar
#       3) 修改本文件如下
serverJar=vanilla.jar
```

---

## Fabric 服启动方式

理解 Fabric 配置文件后，整个启动流程就清晰了：

1. **入口 JAR 是 `fabric-server-launch.jar`**，不是 `server.jar`！
2. 启动命令示例：
   ```bash
   java -Xmx4G -Xms2G -jar fabric-server-launch.jar nogui
   ```
3. Fabric 启动器读取 `fabric-server-launcher.properties` 找到原版 `server.jar`。
4. 启动器加载原版 JAR 并注入 Fabric Loader。
5. Fabric Loader 扫描 `mods/` 目录加载模组。
6. 进入正常的 Minecraft 服务端启动流程（读 `server.properties`、生成世界等）。

### 首次启动流程

1. 用 Fabric 安装器（GUI 或命令行）安装服务端到目标目录。
2. 安装器会生成 `fabric-server-launch.jar`、`server.jar`（原版）、`fabric-server-launcher.properties`、`libraries/` 等。
3. 运行 `java -jar fabric-server-launch.jar nogui`，首次启动会因 `eula=false` 而退出。
4. 编辑 `eula.txt` 把 `eula=false` 改为 `eula=true`。
5. 再次启动，服务器正常运行。
6. 把模组 JAR 放入 `mods/` 目录，重启服务器以加载模组。

### 推荐启动脚本（含 Aikar's Flags）

```bash
java -Xms4G -Xmx4G \
  -XX:+UseG1GC -XX:+UnlockExperimentalVMOptions \
  -XX:MaxGCPauseMillis=100 -XX:+DisableExplicitGC \
  -XX:TargetSurvivorRatio=90 -XX:G1NewSizePercent=50 \
  -XX:G1MaxNewSizePercent=80 -XX:G1MixedGCLiveThresholdPercent=35 \
  -XX:+AlwaysPreTouch -XX:+ParallelRefProcEnabled \
  -Dusing.aikars.flags=mcflags.emc.gs \
  -jar fabric-server-launch.jar nogui
```

> 调整 `-Xms` 和 `-Xmx` 为你想分配的内存大小。Fabric 服通常比 Forge 服省内存，4GB 起步即可，大型整合包建议 6-8GB。

---

## 优化建议（针对模组服管理员）

### ⚡ Fabric 服的真正「调参」在哪里？

Fabric 自身几乎没有配置项，但模组的配置项非常丰富。开服调优主要在以下几处：

1. **`server.properties`**：所有原版服务器行为（端口、视距、难度、白名单、OP 等）。详见 Vanilla 手册。
2. **`config/` 目录下各模组的配置文件**：每个模组自己的配置（命名通常为 `<modid>.json` 或 `fabric-<modid>.toml` 等，由模组作者决定）。
3. **`start.sh` / `start.bat` 启动脚本中的 JVM 参数**：内存分配、GC 策略等。

### 🚀 性能优化模组推荐

Fabric 生态以性能优化模组闻名，建议大型服默认安装以下模组（仅服务端需要）：

- **Lithium**：通用游戏逻辑优化（物理、AI、调度等），几乎零副作用。
- **FerriteCore**：大幅降低内存占用（节省 30-50%）。
- **Krypton**：网络栈优化，减少带宽和 CPU 开销。
- **ServerCore**：多项服务端优化（实体、区块等）。
- **LazyDFU**：延迟数据包注册表初始化，加快启动。
- **Parallel World Submission**（仅 1.21+）：并行世界提交，提升 TPS。

### 🧹 模组管理小贴士

- **客户端模组不要放进服务端**：标签为 `CLIENT` 的模组（如 OptiFine、shader、minimap HUD 等）只装在客户端。误装到服务端可能引起崩溃或行为异常。
- **`mods/` 子目录**：Fabric 1.17+ 支持在 `mods/` 下建子目录（如 `mods/disabled/`），可临时禁用某些模组。
- **`.fabric/` 缓存**：升级 Fabric Loader 后如果出现奇怪的类加载错误，删除 `.fabric/` 目录让其重新生成。
- **依赖检查**：使用 Mod Menu 或 Modrinth 检查模组依赖关系，避免缺依赖导致启动失败。

### 🔄 升级 Fabric

升级 Fabric Loader 时：
1. 删除旧的 `.fabric/` 文件夹（避免旧缓存冲突）。
2. 用新版 Fabric 安装器重新安装（或直接替换 `fabric-server-launch.jar` 和 `libraries/`）。
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

- 官方文档：https://docs.fabricmc.net/
- 官方 Wiki（服务端安装）：https://wiki.fabricmc.net/player:tutorials:server:windows
- 官方 Wiki（无 GUI 安装）：https://wiki.fabricmc.net/zh_cn:player:tutorials:install_server
- 官方下载页：https://fabricmc.net/use/
- GitHub 源码：https://github.com/FabricMC/fabric
- 中文社区教程（MCDR Fabric 服）：https://aimerny.github.io/2023/09/26/mcdr/mcdr-tutor-2-fabric/

---

> ⚠️ **免责声明**：Fabric 启动器配置在不同版本间保持高度稳定（自 Loader 0.4.x 起 `serverJar` 键未变）。本文档基于 Fabric Loader 0.15+ / MC 1.20+ 整理。如遇新版安装器生成的额外键，请以实际文件内容为准。
