# SpongeForge 服务器配置文件中文手册

> SpongeForge 是 SpongeAPI 在 Minecraft Forge 之上的官方实现，作为 Forge 模组（Coremod）注入 Forge 运行时，使 Forge 服务端同时拥有 Sponge 插件生态（命令、权限、事件、数据 API）与 Forge 模组生态（自定义方块/物品/实体）。本手册**仅记录与原版 Sponge 的差异项**（约 20-30 项 Forge 专属设置），通用配置请参考 [32-sponge.md](./32-sponge.md)。
> 官方 GitHub：https://github.com/SpongePowered/SpongeForge
> 官方文档：https://docs.spongepowered.org/stable/en/server/getting-started/implementations/spongeforge.html
> 配置文件路径：`config/sponge/spongeforge-global.conf`（HOCON 格式，文件名刻意区分以避免与 Sponge 原版 `global.conf` 冲突）

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|--------|------|------|------|
| `config/sponge/spongeforge-global.conf` | HOCON | SpongeForge 自动生成 | **本手册核心**：仅含 Forge 专属差异项，与 `global.conf` 并存 |
| `config/sponge/global.conf` | HOCON | Sponge 自动生成 | 通用 Sponge 配置，详见 [32-sponge.md](./32-sponge.md) |
| `config/sponge/<世界名>/world.conf` | HOCON | Sponge 自动生成 | 单世界覆盖配置 |
| `config/<forge_mod>.toml` | TOML | 各 Forge 模组生成 | Forge 模组原生配置，由 Forge 加载，SpongeForge 不接管 |

> 小白提示：SpongeForge 的差异项主要围绕 **Forge 事件桥接、模组兼容性、Mixin 加载顺序、Forge 注册表适配** 四个方向。修改后用 `/sponge reload` 重载，部分项需重启。
> 图例：✅ = `/sponge reload` 即可生效；🔄 = 必须重启服务端才生效。

---

## spongeforge-global.conf（SpongeForge Forge 专属差异配置）

> 下表仅列出与原版 Sponge 不同的或 Forge 独有的设置项。键名保持英文不翻译。

### general（Forge 通用差异）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `general.inject-permission-into-forged-commands` | 注入权限到 Forge 命令 | bool | `true` (true/false) | 🔄 | 是否把 Sponge 权限注入 Forge 模组注册的命令，使权限插件可管控模组命令 |
| `general.use-mod-message-channel` | 使用模组消息通道 | bool | `true` (true/false) | 🔄 | 启用 Forge 模组消息通道以兼容 Forge 客户端模组 |
| `general.use-mod-detected-permission-for-command` | 模组命令权限检测 | bool | `true` (true/false) | ✅ | 检测模组命令所需权限等级（4=OP，0=所有人） |
| `general.allow-sync-chunk-writes` | 允许同步区块写入 | bool | `false` (true/false) | 🔄 | Forge 模组可能强制同步写入，开启以兼容部分老模组 |
| `general.deobfuscate-stacktraces` | 反混淆堆栈 | bool | `true` (true/false) | ✅ | 异常堆栈输出时把混淆名还原为可读名，便于排查 Forge 模组问题 |

### forge（Forge 集成设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `forge.load-early` | 早期加载 | bool | `true` (true/false) | 🔄 | 让 SpongeForge 在 Forge 模组加载之前初始化，解决 Mixin 顺序问题，**强烈建议保持 `true`** |
| `forge.optimize-mod-tileentity-tracking` | 优化模组方块实体追踪 | bool | `true` (true/false) | 🔄 | 优化 Forge 模组方块实体的因果追踪性能 |
| `forge.use-forge-event-for-block-modification` | 使用 Forge 事件处理方块修改 | bool | `true` (true/false) | 🔄 | 用 Forge 的 NeighborNotify 事件而非 Sponge 事件处理方块变更通知，提升模组兼容性 |
| `forge.use-forge-player-interaction` | 使用 Forge 玩家交互 | bool | `true` (true/false) | 🔄 | 用 Forge 玩家交互事件桥接 Sponge 事件 |
| `forge.convert-mod-item-attributes` | 转换模组物品属性 | bool | `true` (true/false) | 🔄 | 把 Forge 物品 NBT 属性转换为 Sponge Data API |
| `forge.bridge-event-bus` | 桥接事件总线 | bool | `true` (true/false) | 🔄 | Forge EventBus 与 Sponge EventManager 双向转发事件 |
| `forge.convert-forge-data` | 转换 Forge 数据 | bool | `true` (true/false) | 🔄 | Forge NBT 数据与 Sponge DataContainer 互转 |

### forge-mod-compatibility（模组兼容性）

> 这是排查模组崩溃的核心配置区，按模组 ID 单独开关 Sponge 对该模组的处理。

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `forge-mod-compatibility.auto-populate` | 自动填充模组兼容项 | bool | `true` (true/false) | 🔄 | 自动为加载到的模组生成兼容性配置项 |
| `forge-mod-compatibility.<modid>.enabled` | 启用模组兼容 | bool | `true` (true/false) | 🔄 | 是否对该模组启用 Sponge 桥接处理，关闭可能提升性能但失去事件 |
| `forge-mod-compatibility.<modid>.mixins` | 模组 Mixin | section | `` | 🔄 | 该模组需要的特殊 Mixin 配置 |
| `forge-mod-compatibility.<modid>.force-restore` | 强制还原 | bool | `false` (true/false) | 🔄 | 模组崩溃后是否强制还原状态（高风险，调试用） |

### mixin（Mixin 加载设置）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `mixin.force-mixin-early` | 强制 Mixin 早期加载 | bool | `true` (true/false) | 🔄 | 让 Sponge 的 Mixin 优先于其他 Coremod，解决"old mixins"警告 |
| `mixin.ignore-mod-mixins` | 忽略模组 Mixin | list | `[]` | 🔄 | 指定要忽略的模组 Mixin 配置 JSON，避免冲突 |
| `mixin.debug` | Mixin 调试 | bool | `false` (true/false) | 🔄 | 输出 Mixin 注入详细日志 |
| `mixin.env.refmap` | 引用映射 | bool | `true` (true/false) | 🔄 | 启用 Mixin refmap，影响混淆名映射 |

### forge-permissions（Forge 权限）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `forge-permissions.enabled` | 启用 Forge 权限桥接 | bool | `true` (true/false) | 🔄 | 把 Forge 注册的权限转给 Sponge 权限系统，让权限插件可管理 |
| `forge-permissions.default-level` | 默认权限等级 | int | `4` (0-4) | ✅ | 模组未声明权限时的默认等级（4=OP 专属，0=所有人） |
| `forge-permissions.strict-mode` | 严格模式 | bool | `false` (true/false) | ✅ | 严格模式下未声明权限的模组命令一律禁止 |

### forge-events（Forge 事件桥接）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `forge-events.fire-cancelable` | 触发可取消事件 | bool | `true` (true/false) | 🔄 | 把 Forge 事件转成可取消的 Sponge 事件 |
| `forge-events.async-events` | 异步事件 | list | `[]` | 🔄 | 指定哪些 Forge 事件允许异步分发，谨慎使用 |
| `forge-events.coalesce` | 事件合并 | bool | `true` (true/false) | 🔄 | 合并连续相同事件以减少分发次数 |

### phase-tracking（Forge 阶段追踪差异）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `phase-tracking.track-forge-block-creation` | 追踪 Forge 方块创建 | bool | `true` (true/false) | ✅ | 追踪 Forge 模组创建方块的因果链，开启略增开销 |
| `phase-tracking.track-forge-entity-creation` | 追踪 Forge 实体创建 | bool | `true` (true/false) | ✅ | 追踪 Forge 模组创建实体的因果链 |
| `phase-tracking.verbose-forge-phases` | 详细 Forge 阶段日志 | bool | `false` (true/false) | ✅ | 输出 Forge 阶段切换详细日志，调试用 |

### optimizations（Forge 专属优化差异）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `optimizations.use-forge-lighting-fallback` | 使用 Forge 光照回退 | bool | `false` (true/false) | 🔄 | 与 Phosphor 等光照模组冲突时回退到 Forge 光照 |
| `optimizations.skip-mod-tick-on-overload` | 过载时跳过模组 tick | bool | `false` (true/false) | ✅ | TPS 低时跳过非关键模组的 tick，谨慎启用 |
| `optimizations.cache-forge-capabilities` | 缓存 Forge 能力 | bool | `true` (true/false) | 🔄 | 缓存 Forge Capability 查询结果，提升模组交互性能 |
| `optimizations.batch-forge-block-updates` | 批量 Forge 方块更新 | bool | `true` (true/false) | ✅ | 批量处理 Forge 模组的方块更新通知 |

### entity（Forge 实体差异）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `entity.convert-forge-entity-data` | 转换 Forge 实体数据 | bool | `true` (true/false) | 🔄 | 把 Forge 模组实体 NBT 转为 Sponge Data API |
| `entity.use-forge-spawn-rules` | 使用 Forge 生成规则 | bool | `true` (true/false) | 🔄 | 尊重 Forge 模组的 `canSpawn` 规则，关闭可能让某些模组怪物刷不出来 |
| `entity.max-mod-entity-per-chunk` | 单区块模组实体上限 | int | `100` (0-2147483647) | ✅ | 每区块 Forge 模组实体上限，`0`=禁用上限 |

### commands（Forge 命令差异）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `commands.register-forge-commands` | 注册 Forge 命令 | bool | `true` (true/false) | 🔄 | 把 Forge 模组的命令注册到 Sponge 命令系统 |
| `commands.tab-complete-forge-commands` | Forge 命令 Tab 补全 | bool | `true` (true/false) | ✅ | 启用 Forge 模组命令的 Tab 自动补全 |
| `commands.legacy-forge-command-prefix` | 旧版 Forge 命令前缀 | bool | `false` (true/false) | ✅ | 兼容旧版用 `/forge:` 前缀调用模组命令 |

### bungeecord（Forge 代理差异）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `bungeecord.forward-forge-mods` | 转发 Forge 模组列表 | bool | `true` (true/false) | 🔄 | 通过 BungeeCord 转发 Forge 客户端模组列表，跨服模组必需 |
| `bungeecord.verify-forge-mods` | 验证 Forge 模组 | bool | `false` (true/false) | 🔄 | 跨服时验证客户端 Forge 模组列表，防作弊 |

### logging（Forge 日志差异）

| 键名 | 中文含义 | 类型 | 默认值（取值范围） | 需重启 | 说明 |
|------|----------|------|---------------------|--------|------|
| `logging.log-forge-event-mismatch` | 记录 Forge 事件不匹配 | bool | `false` (true/false) | ✅ | Forge 与 Sponge 事件桥接失败时输出警告 |
| `logging.log-mixin-failures` | 记录 Mixin 失败 | bool | `true` (true/false) | ✅ | Mixin 注入失败时输出详细错误 |
| `logging.log-forge-permission-misses` | 记录 Forge 权限缺失 | bool | `false` (true/false) | ✅ | 模组权限未声明时输出警告 |

---

## 配置示例

```hocon
# config/sponge/spongeforge-global.conf 完整示例
general {
    inject-permission-into-forged-commands = true
    use-mod-message-channel = true
    use-mod-detected-permission-for-command = true
    allow-sync-chunk-writes = false
    deobfuscate-stacktraces = true
}

forge {
    # 让 SpongeForge 在 Forge 模组加载前初始化，解决 Mixin 顺序问题
    load-early = true
    optimize-mod-tileentity-tracking = true
    use-forge-event-for-block-modification = true
    use-forge-player-interaction = true
    convert-mod-item-attributes = true
    bridge-event-bus = true
    convert-forge-data = true
}

forge-mod-compatibility {
    auto-populate = true
    # 示例：对 Applied Energistics 2 单独配置
    appliedenergistics2 {
        enabled = true
        force-restore = false
    }
}

mixin {
    force-mixin-early = true
    ignore-mod-mixins = []
    debug = false
    env {
        refmap = true
    }
}

forge-permissions {
    enabled = true
    default-level = 4
    strict-mode = false
}

forge-events {
    fire-cancelable = true
    async-events = []
    coalesce = true
}

phase-tracking {
    track-forge-block-creation = true
    track-forge-entity-creation = true
    verbose-forge-phases = false
}

optimizations {
    use-forge-lighting-fallback = false
    skip-mod-tick-on-overload = false
    cache-forge-capabilities = true
    batch-forge-block-updates = true
}

entity {
    convert-forge-entity-data = true
    use-forge-spawn-rules = true
    max-mod-entity-per-chunk = 100
}

commands {
    register-forge-commands = true
    tab-complete-forge-commands = true
    legacy-forge-command-prefix = false
}

bungeecord {
    forward-forge-mods = true
    verify-forge-mods = false
}

logging {
    log-forge-event-mismatch = false
    log-mixin-failures = true
    log-forge-permission-misses = false
}
```

---

## 优化建议

1. **首选排查"old mixins"警告**：若启动日志出现该警告，把 SpongeForge jar 重命名为 `aaa_spongeforge-*.jar`，使其在 Forge 模组加载顺序中最先加载，可解决约 70% 的 Mixin 冲突崩溃。同时确保 `mixin.force-mixin-early = true`。
2. **大型模组整合包崩溃**：开启 `forge-mod-compatibility.auto-populate = true`，让 Sponge 自动为每个模组生成兼容项，再逐个关闭可疑模组的 `enabled`，定位冲突源。
3. **Phosphor / FoamFix 冲突**：若与 Phosphor 共存崩服，把 `optimizations.use-forge-lighting-fallback = true`，或在 `global.conf` 中关闭 `optimizations.async-lighting.enabled`。FoamFix 需设置其自身的 `optimizedBlockPos=false` 与 `patchChunkSerialization=false`。
4. **跨服模组（BungeeCord/Velocity）**：必须同时设置 `bungeecord.forward-forge-mods = true` 与 `global.conf` 的 `modules.bungeecord = true`，否则跨服后模组物品丢失。
5. **高密度模组实体卡顿**：把 `entity.max-mod-entity-per-chunk` 从 `100` 降到 `30-50`，立竿见影缓解工业模组的实体堆积。
6. **权限插件管不到模组命令**：确认 `general.inject-permission-into-forged-commands = true` 与 `forge-permissions.enabled = true`，这样 LuckPerms 等 Sponge 权限插件才能管控模组命令。
7. **性能调优**：`optimizations.cache-forge-capabilities = true` 与 `optimizations.batch-forge-block-updates = true` 默认开启，强烈建议保持，关闭会显著降低模组交互性能。
8. **调试模组崩溃**：临时开启 `general.deobfuscate-stacktraces = true` 与 `logging.log-mixin-failures = true`，重启后查看可读堆栈定位模组问题。
9. **不要碰**：`forge.load-early`、`forge.bridge-event-bus`、`forge.convert-forge-data` 设为 `false` 会让大量模组失效或崩溃，仅深度调试时考虑。
10. **命令前缀**：旧版整合包若 `/give` 等命令冲突，开启 `commands.legacy-forge-command-prefix = true` 用 `/forge:give` 调模组版本。

> 排查清单：模组崩溃 → 查 `mixin.*` 与 `forge-mod-compatibility.<modid>`；权限不管用 → 查 `forge-permissions.*`；跨服丢物品 → 查 `bungeecord.forward-forge-mods`；TPS 低 → 查 `entity.max-mod-entity-per-chunk` 与 `phase-tracking.track-forge-*` 是否过严。
