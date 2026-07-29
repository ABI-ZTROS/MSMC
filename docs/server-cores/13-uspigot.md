# USpigot 服务器配置文件中文手册

> ⚠️ **核心警告**：经核实，**USpigot 没有可访问的官方 GitHub 仓库**，也无可验证的官方源码与配置文件。该项目仅在 MineBBS 等国内社区以二进制 jar 形式分发，缺乏公开源码、文档与维护承诺。**强烈不建议在生产环境使用**，请优先选择 Paper / Purpur / Folia 等有公开源码与活跃维护的核心。
>
> 继承关系（推断）：Vanilla → Spigot → Paper → **USpigot**（基于 Spigot/Paper 的国内分支，具体上游不可考）
> 官方 GitHub：**无**（不存在可访问的官方仓库）
> 社区分发：MineBBS 等国内 Minecraft 社区
> 数据来源：⚠️ 本文档所有配置项均为**基于 Spigot/Paper 分支惯例的可推断项**，未经官方源码核实。**请勿作为权威依据**。
> 适用版本基准：未知（社区分发的版本不一，无统一版本号）

> **致开服者**：如果你正在选型，**请直接关闭本页**，改用 Paper / Purpur / Folia / Pufferfish 等有公开源码与活跃维护的核心。USpigot 仅在以下情况下才考虑：你已确认社区分发版本可信任、明确知晓其上游来源、且愿意承担无源码审计的风险。
>
> 如果你确实需要使用 USpigot，请通过 `/version` 命令在控制台确认其实际核心类型与版本，并参考其上游（Spigot / Paper）的配置手册。下文列出的配置项仅为基于分支惯例的**推断默认值**，可能与实际不符。

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置 |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（实体激活范围、视距等） |
| paper.yml / config/paper-global.yml | YAML | Paper 继承（推断） | Paper 配置（若上游为 Paper） |
| **uspigot.yml** | YAML | **USpigot 专属（推断）** | **USpigot 独有配置（本文档；⚠️ 配置项为推断，未经源码核实）** |

> ⚠️ 「USpigot 专属」一栏为基于 Spigot/Paper 分支命名惯例的**推断**。实际核心可能使用其他文件名（如 `u-spigot.yml`、`core.yml`），或根本无独立配置文件（所有自定义项混入 `spigot.yml`）。请以你下载的 jar 启动后生成的实际文件为准。

---

## 阅读约定

- **键名**：保持原样不翻译，采用点号扁平化路径（如 `settings.brand-name`）。
- **值类型**：`bool` 布尔 / `int` 整数 / `string` 字符串。
- **取值范围**：标注在「默认值」一列括号内。
- **需重启**：✅ 表示修改后必须重启服务器才能生效；🔄 表示支持热重载。
- ⚠️ **可信度**：本文档所有配置项的默认值与说明均为**推断**，可能与实际核心行为不符。修改前请务必备份原配置文件。

---

## uspigot.yml（USpigot 专属配置 / 推断）

> ⚠️ 以下配置项为基于 Spigot/Paper 分支惯例（参考 NachoSpigot、Pufferfish 等同类分支）的**可推断项**。USpigot 实际配置项数量与命名未知，本表仅列出最可能存在的 3 项基础项，用于开服者快速识别与定位。

### 1. settings（基础设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|---|---|---|---|---|---|
| `settings.brand-name` | 服务端品牌名 | string | `USpigot`（推断） | 🔄 | 发送给客户端的服务端品牌名（F3 界面 "Mod" 字段）。⚠️ 推断项，实际默认值以核心启动后生成的配置为准。可用 § 颜色码定制。建议改为通用名以隐藏核心类型。 |
| `settings.commands.enable-version-command` | 启用 /version 命令 | bool | `false`（`true`/`false`，推断） | 🔄 | 是否允许玩家使用 `/version`（`/ver`）查看服务端版本。⚠️ 推断项。公网服建议关闭以防信息泄露。 |
| `settings.commands.enable-plugins-command` | 启用 /plugins 命令 | bool | `false`（`true`/`false`，推断） | 🔄 | 是否允许玩家使用 `/plugins`（`/pl`）查看插件列表。⚠️ 推断项。公网服建议关闭以防泄露插件信息。 |

---

## 配置示例（uspigot.yml 推断默认值）

```yaml
# ⚠️ 警告：以下配置为基于 Spigot/Paper 分支惯例的推断默认值，未经官方源码核实！
# 实际配置项数量、命名、默认值可能与本示例不同。请以核心启动后生成的真实文件为准。
settings:
  brand-name: USpigot                # 推断值
  commands:
    enable-version-command: false     # 推断值
    enable-plugins-command: false     # 推断值
  # ... 可能存在更多配置项，但无法在无源码情况下推断
```

---

## 优化建议

1. **首要建议：换核心**。USpigot 无公开源码、无活跃维护、无官方文档，存在不可审计的安全风险。请迁移到 Paper / Purpur / Folia / Pufferfish 等有公开源码的核心。
2. **若必须使用**：先在隔离环境（虚拟机 / 沙箱）运行该 jar，观察其行为与网络流量，确认无恶意行为后再考虑部署。
3. **确认上游**：用 `/version` 命令查看其报告的上游核心与版本，然后参考对应上游（Spigot / Paper）的配置手册进行调优。
4. **信息泄露**：将 `brand-name` 改为通用名（如 `Paper`），关闭所有 `commands.enable-*-command` 类选项。
5. **以实际文件为准**：本文档列出的 3 项为推断项，实际核心启动后请直接阅读生成的配置文件注释（如有），并以注释为准。

---

## 附录：USpigot 资料缺失核实

经以下渠道核实，**USpigot 不存在可访问的官方 GitHub 仓库与公开源码**：

1. **GitHub 搜索**：在 GitHub 上搜索 `USpigot` / `u-spigot` 等关键词，未找到任何标记为 USpigot 官方仓库的活跃项目，仅有同名无关项目或个人 fork。
2. **社区分发**：USpigot 仅以二进制 jar 形式在 MineBBS 等国内社区分发，未提供源码链接、构建脚本或补丁文件。
3. **文档缺失**：无官方文档、Wiki 或 README，配置项含义只能从同名分支惯例推断。
4. **维护状态**：无公开的 issue tracker、commit 历史或版本路线图，无法判断是否仍在维护。

> **结论**：本手册仅作为开服者识别 USpigot 的参考，**不构成对该核心的背书**。所有配置项均为基于分支惯例的推断，使用前请务必以核心实际生成的配置文件为准，并优先考虑迁移到有公开源码的核心。

> 参考来源：MineBBS 等国内社区（仅二进制分发，无源码）、GitHub 搜索（无官方仓库）。
