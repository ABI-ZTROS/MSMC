# Leaves 服务器配置文件中文手册

> Leaves 是基于 Paper 的 Minecraft 服务端，旨在修复被破坏的原版特性，并对**生电（技术性生存）**玩法做特殊支持。
> 继承关系：Vanilla → Spigot → Paper → Leaves
> 官方网站：https://leavesmc.org/
> 官方文档：https://docs.leavesmc.org/
> 源码仓库：https://github.com/LeavesMC/Leaves
> 配置仓库：https://github.com/LeavesMC/Configuration
> 数据来源：Leaves 官方文档 / LeavesMC/Configuration 仓库 leaves.yml / LeavesMC/Leaves 源码（LeavesConfig.java、GlobalConfigManager.java）
> 适用版本基准：Leaves 1.21.x（config-version: 6，2025-2026 稳定版）

## 配置文件清单

| 文件名 | 格式 | 来源 | 说明 |
|---|---|---|---|
| server.properties | Properties | Vanilla 继承 | 基础服务器设置（端口、视距、难度等） |
| bukkit.yml | YAML | Bukkit 继承 | Bukkit API 层配置（生成上限、命令别名等） |
| spigot.yml | YAML | Spigot 继承 | Spigot 配置（实体激活范围、视距等） |
| paper-global.yml | YAML | Paper 继承 | Paper 全局配置（区块、网络、漏洞修复等） |
| paper-world-defaults.yml | YAML | Paper 继承 | Paper 世界默认配置（每世界可覆盖） |
| **leaves.yml** | YAML | **Leaves 专属** | **Leaves 全部独有配置（本文档重点）** |

> **⚠️ 关于 `config/leaves-global.yml` 的说明**
>
> 经 GitHub 源码核实（`LeavesConfig.java`、`GlobalConfigManager.java`、`GlobalConfigCreator.java`、`0003-Leaves-Server-Config.patch`、`LeavesServerConfigProvider.java`），**LeavesMC/Leaves 不存在独立的 `config/leaves-global.yml` 文件**。Leaves 的所有独有配置（包括服务器全局设置与世界级玩法设置）都统一写在根目录的**单一 `leaves.yml`** 文件中。
>
> 这与 Paper 的"全局 + 世界默认 + 每世界"三文件拆分模式不同。源码中虽存在 `@GlobalConfig` 注解，但该注解仅表示"由 GlobalConfigManager 统一管理的配置字段"，所有字段最终都写入 `leaves.yml`（路径前缀 `settings.`），不会拆分到独立的 global 文件。
>
> 如需区分"服务器级"与"玩法级"配置，可参照本文档的分类标题：`misc`/`region`/`fix`/`protocol` 偏服务器全局，`fakeplayer`/`minecraft-old`/`performance` 偏世界玩法。

## leaves.yml 整体结构

```yaml
config-version: 6                      # 配置版本号（内部用，勿手改）

settings:
  modify:                              # 玩法修改（Leaves 核心特色）
    fakeplayer: { ... }                # 假人（机器人）系统
    minecraft-old: { ... }             # 旧版特性回退（生电向）
    elytra-aeronautics: { ... }        # 鞘翅巡航
    block-updater / shulker-box / ...  # 各类玩法开关
  performance: { ... }                 # 性能优化
  protocol: { ... }                    # 客户端模组协议兼容
  misc: { ... }                        # 杂项（自动更新、外置登录、语言等）
  region: { ... }                      # 区块文件格式（Linear/ANVIL）
  fix: { ... }                         # 漏洞修复
```

---

## settings.modify.fakeplayer（假人 / 机器人系统）

> Leaves 内置的假人（Fakeplayer / Bot）系统，可用于挂机加载区块、测试机关等。通过 `/bot` 命令管理。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.fakeplayer.enable` | 启用假人系统 | `true` | bool | true/false | 是 | 总开关。关闭后无法创建假人，已存在的假人也会失效 |
| `settings.modify.fakeplayer.unable-fakeplayer-names` | 禁用的假人名字 | `[player-name]` | list | — | 否 | 黑名单：列表中的名字不允许用作假人名，防止冒充真人 |
| `settings.modify.fakeplayer.limit` | 假人数量上限 | `10` | int | 0+ | 否 | 单个服务器允许同时存在的假人数量上限 |
| `settings.modify.fakeplayer.prefix` | 假人名前缀 | `（空）` | string | — | 否 | 创建假人时自动加在名字前的前缀，便于识别 |
| `settings.modify.fakeplayer.suffix` | 假人名后缀 | `（空）` | string | — | 否 | 创建假人时自动加在名字后的后缀 |
| `settings.modify.fakeplayer.regen-amount` | 假人回血量 | `0.0` | double | 0+ | 否 | 假人每刻自动回复的生命值，0=不回血 |
| `settings.modify.fakeplayer.resident-fakeplayer` | 假人常驻 | `false` | bool | true/false | 否 | true 时假人重启服务器后自动重建（常驻） |
| `settings.modify.fakeplayer.open-fakeplayer-inventory` | 可打开假人背包 | `false` | bool | true/false | 否 | true 时允许玩家打开假人的物品栏 |
| `settings.modify.fakeplayer.use-action` | 假人可用动作 | `true` | bool | true/false | 否 | 假人是否能执行攻击/使用/放置等动作 |
| `settings.modify.fakeplayer.modify-config` | 假人可改配置 | `false` | bool | true/false | 否 | 假人是否能通过命令修改自身配置 |
| `settings.modify.fakeplayer.manual-save-and-load` | 手动保存/读取 | `false` | bool | true/false | 否 | true 时假人数据需手动保存/读取，不自动持久化 |
| `settings.modify.fakeplayer.cache-skin` | 缓存假人皮肤 | `false` | bool | true/false | 否 | true 时缓存假人皮肤数据，减少皮肤查询请求 |

### settings.modify.fakeplayer.in-game（游戏内假人行为）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.fakeplayer.in-game.always-send-data` | 始终发送数据 | `true` | bool | true/false | 否 | 假人是否始终向客户端发送实体数据（保证渲染稳定） |
| `settings.modify.fakeplayer.in-game.skip-sleep-check` | 跳过睡眠检查 | `false` | bool | true/false | 否 | true 时假人不参与"全员睡觉跳过夜晚"的判定 |
| `settings.modify.fakeplayer.in-game.spawn-phantom` | 假人可生成幻翼 | `false` | bool | true/false | 否 | true 时假人也会触发幻翼生成（模拟玩家不睡觉） |
| `settings.modify.fakeplayer.in-game.tick-type` | 假人 Tick 类型 | `NETWORK` | enum | `NETWORK`/`MAIN` | 否 | NETWORK=仅网络 tick（轻量）；MAIN=完整主线程 tick（真实玩家行为） |
| `settings.modify.fakeplayer.in-game.simulation-distance` | 假人模拟距离 | `-1` | int | -1 / 0+ | 否 | 假人加载区块的模拟距离，-1=跟随服务器默认值 |
| `settings.modify.fakeplayer.in-game.enable-locator-bar` | 启用定位条 | `false` | bool | true/false | 否 | 假人是否显示 1.21 的定位条（locator bar） |

---

## settings.modify.minecraft-old（旧版特性回退）

> 回退到旧版本的行为，主要用于**生电（技术性生存）**玩法还原被 Mojang 修改/修复的原版机制。⚠️ 部分选项会重新引入已被修复的漏洞，非生电服建议保持默认。

### settings.modify.minecraft-old.block-updater（方块更新器）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.minecraft-old.block-updater.instant-block-updater-reintroduced` | 恢复瞬时方块更新 | `false` | bool | true/false | 是 | 恢复 1.19 前的瞬时方块更新器行为（影响红石时序） |
| `settings.modify.minecraft-old.block-updater.cce-update-suppression` | CCE 更新抑制 | `false` | bool | true/false | 是 | 恢复基于 ClassCastException 的更新抑制（生电常用） |
| `settings.modify.minecraft-old.block-updater.sound-update-suppression` | 声音更新抑制 | `false` | bool | true/false | 是 | 恢复基于声音事件的更新抑制 |
| `settings.modify.minecraft-old.block-updater.redstone-ignore-upwards-update` | 红石忽略向上更新 | `false` | bool | true/false | 是 | 红石信号不再向上传播更新（旧版行为） |
| `settings.modify.minecraft-old.block-updater.old-block-remove-behaviour` | 旧版方块移除行为 | `false` | bool | true/false | 是 | 恢复旧版方块被移除时的行为 |

### settings.modify.minecraft-old（其他旧版特性）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.minecraft-old.shears-in-dispenser-can-zero-amount` | 剪刀可归零 | `false` | bool | true/false | 否 | 发射器中的剪刀耐久用尽后不消失（旧版行为） |
| `settings.modify.minecraft-old.villager-infinite-discounts` | 村民无限折扣 | `false` | bool | true/false | 否 | 恢复村民折扣可无限叠加的旧版行为（已被 Mojang 修复） |
| `settings.modify.minecraft-old.copper-bulb-1gt-delay` | 铜灯 1gt 延迟 | `false` | bool | true/false | 是 | 铜灯（铜泡）恢复 1gt 延迟的旧版行为 |
| `settings.modify.minecraft-old.crafter-1gt-delay` | 合成器 1gt 延迟 | `false` | bool | true/false | 是 | 合成器恢复 1gt 延迟的旧版行为 |
| `settings.modify.minecraft-old.zero-tick-plants` | 零刻催熟 | `false` | bool | true/false | 否 | 恢复零刻植物催熟（已被修复的漏洞） |
| `settings.modify.minecraft-old.rng-fishing` | 随机数钓鱼 | `false` | bool | true/false | 否 | 恢复基于随机数的旧版钓鱼机制（可预测/操纵） |
| `settings.modify.minecraft-old.allow-entity-portal-with-passenger` | 实体带乘客穿门 | `true` | bool | true/false | 否 | 带乘客的实体是否可通过传送门 |
| `settings.modify.minecraft-old.disable-gateway-portal-entity-ticking` | 禁用传送门实体 tick | `false` | bool | true/false | 否 | 末地传送门传送实体后暂停其 tick |
| `settings.modify.minecraft-old.disable-LivingEntity-ai-step-alive-check` | 禁用 AI 步进存活检查 | `false` | bool | true/false | 否 | 禁用 LivingEntity 的 aiStep 存活检查 |
| `settings.modify.minecraft-old.spawn-invulnerable-time` | 出生无敌时间 | `false` | bool | true/false | 否 | 恢复旧版生物出生无敌时间 |
| `settings.modify.minecraft-old.old-hopper-suck-in-behavior` | 旧版漏斗吸取行为 | `false` | bool | true/false | 否 | 恢复旧版漏斗吸取物品的行为 |
| `settings.modify.minecraft-old.old-zombie-piglin-drop` | 旧版僵尸猪灵掉落 | `false` | bool | true/false | 否 | 恢复旧版僵尸猪灵的掉落物 |
| `settings.modify.minecraft-old.old-raid-behavior` | 旧版袭击行为 | `false` | bool | true/false | 否 | 恢复旧版袭击（Raid）机制 |
| `settings.modify.minecraft-old.old-zombie-reinforcement` | 旧版僵尸增援 | `false` | bool | true/false | 否 | 恢复旧版僵尸召唤增援的行为 |
| `settings.modify.minecraft-old.allow-anvil-destroy-item-entities` | 铁砧可销毁掉落物 | `false` | bool | true/false | 否 | 铁砧坠落时可销毁其下方的掉落物实体 |
| `settings.modify.minecraft-old.old-throwable-projectile-tick-order` | 旧版投掷物 tick 顺序 | `false` | bool | true/false | 否 | 恢复旧版投掷物的 tick 处理顺序 |
| `settings.modify.minecraft-old.keep-leash-connect-when-use-firework` | 烟花保留拴绳连接 | `false` | bool | true/false | 否 | 使用烟花加速时保留拴绳连接 |
| `settings.modify.minecraft-old.tnt-wet-explosion-no-item-damage` | 湿 TNT 不破坏物品 | `false` | bool | true/false | 否 | 水中/TNT 爆炸不破坏掉落物 |
| `settings.modify.minecraft-old.old-projectile-explosion-behavior` | 旧版抛射物爆炸行为 | `false` | bool | true/false | 否 | 恢复旧版抛射物爆炸的行为 |
| `settings.modify.minecraft-old.ender-dragon-part-can-use-end-portal` | 末影龙部位可穿末地门 | `false` | bool | true/false | 否 | 末影龙的子部位是否可通过末地传送门 |
| `settings.modify.minecraft-old.old-minecart-motion-behavior` | 旧版矿车运动行为 | `false` | bool | true/false | 否 | 恢复旧版矿车运动逻辑 |
| `settings.modify.minecraft-old.allow-inf-nan-motion-values` | 允许无穷/NaN 运动 | `true` | bool | true/false | 否 | 允许实体的运动值出现无穷大或 NaN（生电用） |

### settings.modify.minecraft-old.tripwire-and-hook-behavior（绊线与钩子行为）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.minecraft-old.tripwire-and-hook-behavior.string-tripwire-hook-duplicate` | 线-绊线钩复制 | `false` | bool | true/false | 否 | 恢复线与绊线钩的复制行为（已被修复的漏洞） |
| `settings.modify.minecraft-old.tripwire-and-hook-behavior.tripwire-behavior` | 绊线行为 | `VANILLA_21` | enum | `VANILLA_21`/`VANILLA_20` | 否 | 绊线钩的版本行为：VANILLA_21=1.21+ 行为；VANILLA_20=1.20 行为 |

---

## settings.modify.elytra-aeronautics（鞘翅巡航）

> 模拟"鞘翅 + 烟花"高速飞行时的区块加载优化，防止超速飞行导致卡顿/穿墙。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.elytra-aeronautics.no-chunk-load` | 禁止加载新区块 | `false` | bool | true/false | 否 | true 时高速飞行不再加载前方新区块（仅已加载区块可见） |
| `settings.modify.elytra-aeronautics.no-chunk-height` | 不加载区块的高度 | `500.0` | double | 0+ | 否 | 玩家 Y 坐标高于此值时不再加载区块，配合 no-chunk-load |
| `settings.modify.elytra-aeronautics.no-chunk-speed` | 不加载区块的速度 | `-1.0` | double | -1 / 0+ | 否 | 飞行速度超过此值时不再加载新区块，-1=禁用该判定 |
| `settings.modify.elytra-aeronautics.message` | 显示提示消息 | `true` | bool | true/false | 否 | 进入/退出巡航模式时是否给玩家发消息 |
| `settings.modify.elytra-aeronautics.message-start` | 进入巡航提示 | `Flight enter cruise mode` | string | — | 否 | 进入巡航模式时发送的消息文本 |
| `settings.modify.elytra-aeronautics.message-end` | 退出巡航提示 | `Flight exit cruise mode` | string | — | 否 | 退出巡航模式时发送的消息文本 |

---

## settings.modify（玩法修改开关）

> 直接位于 `settings.modify` 下的各类玩法开关，多为生电向或原版还原。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.redstone-shears-wrench` | 剪刀红石扳手 | `false` | bool | true/false | 否 | 剪刀可作为红石扳手调整红石元件方向 |
| `settings.modify.movable-budding-amethyst` | 可移动紫水晶母岩 | `false` | bool | true/false | 否 | 紫水晶母岩可被活塞推动（生电可再生紫水晶） |
| `settings.modify.spectator-dont-get-advancement` | 旁观者不触发进度 | `false` | bool | true/false | 否 | 旁观模式玩家不触发进度/成就 |
| `settings.modify.stick-change-armorstand-arm-status` | 木棍切换盔甲架手臂 | `true` | bool | true/false | 否 | 用木棍右键盔甲架可切换其手臂显示状态 |
| `settings.modify.snowball-and-egg-can-knockback-player` | 雪球鸡蛋可击退玩家 | `true` | bool | true/false | 否 | 雪球和鸡蛋能否击退玩家 |
| `settings.modify.flatten-triangular-distribution` | 拉平三角分布 | `false` | bool | true/false | 否 | 将随机分布的三角分布拉平（影响部分随机生成） |
| `settings.modify.player-operation-limiter` | 玩家操作限速 | `false` | bool | true/false | 否 | 限制玩家单 tick 内的操作次数（防作弊/防卡服） |
| `settings.modify.renewable-elytra` | 可再生鞘翅 | `-1.0` | double | -1 / 0~1 | 否 | 末影龙被杀死掉落鞘翅的概率，-1=禁用，0~1=概率值 |
| `settings.modify.force-void-trade` | 强制虚空交易 | `false` | bool | true/false | 否 | 强制启用虚空交易（生电特性） |
| `settings.modify.mc-technical-survival-mode` | 生电技术生存模式 | `true` | bool | true/false | 是 | 启用生电技术生存相关优化与特性（总开关） |
| `settings.modify.return-nether-portal-fix` | 修复返回下界传送门 | `false` | bool | true/false | 否 | 修复从下界返回主世界的传送门定位问题 |
| `settings.modify.use-vanilla-random` | 使用原版随机数 | `false` | bool | true/false | 是 | 强制使用原版随机数生成器（保证与原版一致） |
| `settings.modify.fix-update-suppression-crash` | 修复更新抑制崩溃 | `true` | bool | true/false | 否 | 防止更新抑制导致服务器崩溃（建议开启） |
| `settings.modify.fix-stuck-zombified-piglin-anger-target` | 修复僵尸猪灵卡愤怒 | `false` | bool | true/false | 否 | 修复僵尸猪灵愤怒目标卡住的问题 |
| `settings.modify.bedrock-break-list` | 基岩破坏记录 | `false` | bool | true/false | 否 | 记录基岩被破坏的事件（生电用） |
| `settings.modify.disable-distance-check-for-use-item` | 禁用使用物品距离检查 | `false` | bool | true/false | 否 | 禁用使用物品时的距离检查（生电用） |
| `settings.modify.no-feather-falling-trample` | 精英掉落不踩坏农田 | `false` | bool | true/false | 否 | 穿鞘翅/摔落保护时不踩坏农田 |
| `settings.modify.shared-villager-discounts` | 共享村民折扣 | `false` | bool | true/false | 否 | 所有玩家共享同一村民的折扣进度 |
| `settings.modify.disable-check-out-of-order-command` | 禁用乱序命令检查 | `false` | bool | true/false | 否 | 禁用命令乱序执行检查 |
| `settings.modify.despawn-enderman-with-block` | 携带方块的末影人消失 | `false` | bool | true/false | 否 | 末影人携带方块时消失会同时移除方块 |
| `settings.modify.creative-no-clip` | 创造模式无碰撞 | `false` | bool | true/false | 否 | 创造模式玩家可穿墙（无碰撞箱） |
| `settings.modify.shave-snow-layers` | 刮雪层 | `true` | bool | true/false | 否 | 用锹右键可逐层刮除雪层 |
| `settings.modify.disable-packet-limit` | 禁用数据包限制 | `false` | bool | true/false | 否 | 禁用数据包速率限制（⚠️ 有被攻击风险） |
| `settings.modify.lava-riptide` | 岩浆激流 | `false` | bool | true/false | 否 | 激流附魔可在岩浆中使用 |
| `settings.modify.no-block-update-command` | 禁方块更新命令 | `false` | bool | true/false | 否 | 提供 `/blockupdate` 命令控制方块更新（生电用） |
| `settings.modify.no-tnt-place-update` | 放置 TNT 不更新 | `false` | bool | true/false | 否 | 放置 TNT 时不触发方块更新 |
| `settings.modify.container-passthrough` | 容器穿透 | `false` | bool | true/false | 否 | 允许实体穿过容器方块 |
| `settings.modify.avoid-anvil-too-expensive` | 铁砧避免过于昂贵 | `false` | bool | true/false | 否 | 取消铁砧"过于昂贵"的限制 |
| `settings.modify.bow-infinity-fix` | 无限弓修复 | `false` | bool | true/false | 否 | 修复无限附魔弓的箭矢消耗问题 |
| `settings.modify.spider-jockeys-drop-gapples` | 蜘蛛骑士掉金苹果 | `-1.0` | double | -1 / 0~1 | 否 | 蜘蛛骑士掉落附魔金苹果的概率，-1=禁用 |
| `settings.modify.renewable-deepslate` | 可再生深板岩 | `false` | bool | true/false | 否 | 启用可再生深板岩（生电特性） |
| `settings.modify.renewable-sponges` | 可再生海绵 | `false` | bool | true/false | 否 | 启用可再生海绵 |
| `settings.modify.renewable-coral` | 可再生珊瑚 | `FALSE` | enum | `FALSE`/`TRUE`/`EXPANDED` | 否 | FALSE=禁用；TRUE=启用；EXPANDED=扩展模式 |
| `settings.modify.disable-vault-blacklist` | 禁用宝库黑名单 | `false` | bool | true/false | 否 | 禁用 1.21 试炼宝库的领取黑名单 |
| `settings.modify.exp-orb-absorb-mode` | 经验球吸收模式 | `VANILLA` | enum | `VANILLA`/`ASYNC` | 否 | VANILLA=原版同步吸收；ASYNC=异步吸收（性能更好） |
| `settings.modify.follow-tick-sequence-merge` | 跟随 tick 序列合并 | `false` | bool | true/false | 否 | 合并 tick 序列以优化性能 |

### settings.modify.shulker-box（潜影盒）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.shulker-box.stackable-shulker-boxes` | 潜影盒可堆叠 | `false` | bool | true/false | 否 | 空潜影盒可堆叠（生电特性） |
| `settings.modify.shulker-box.same-nbt-stackable` | 相同 NBT 可堆叠 | `false` | bool | true/false | 否 | NBT 相同的潜影盒可堆叠 |

### settings.modify.hopper-counter（漏斗计数器）

> Carpet 风格的漏斗计数器，用于生电物资统计。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.modify.hopper-counter.enable` | 启用漏斗计数器 | `false` | bool | true/false | 否 | 启用漏斗计数器功能 |
| `settings.modify.hopper-counter.unlimited-speed` | 无限速度 | `false` | bool | true/false | 否 | 漏斗吸收物品无速度上限 |

---

## settings.performance（性能优化）

> Leaves 内置的各项性能优化开关。多数默认开启，关闭后会回退到原版/Paper 行为。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.performance.optimized-dragon-respawn` | 优化末影龙重生 | `false` | bool | true/false | 否 | 优化末影龙重生流程的性能 |
| `settings.performance.dont-send-useless-entity-packets` | 不发无用实体包 | `true` | bool | true/false | 否 | 跳过无意义的实体数据包发送，减少网络开销 |
| `settings.performance.enable-suffocation-optimization` | 启用窒息优化 | `true` | bool | true/false | 否 | 优化实体窒息检测性能 |
| `settings.performance.inactive-goal-selector-disable` | 禁用非活跃目标选择器 | `false` | bool | true/false | 否 | 非活跃实体禁用 AI 目标选择器 |
| `settings.performance.reduce-entity-allocations` | 减少实体内存分配 | `true` | bool | true/false | 否 | 减少实体相关的内存分配 |
| `settings.performance.cache-climb-check` | 缓存攀爬检查 | `true` | bool | true/false | 否 | 缓存实体攀爬判定结果 |
| `settings.performance.reduce-chuck-load-and-lookup` | 减少区块加载/查询 | `true` | bool | true/false | 否 | 减少区块加载与查询次数 |
| `settings.performance.cache-ignite-odds` | 缓存点燃概率 | `true` | bool | true/false | 否 | 缓存方块点燃概率计算 |
| `settings.performance.faster-chunk-serialization` | 加速区块序列化 | `true` | bool | true/false | 否 | 加快区块数据的序列化速度 |
| `settings.performance.skip-secondary-POI-sensor-if-absent` | 跳过次要 POI 感知 | `true` | bool | true/false | 否 | POI 不存在时跳过次要感知器 |
| `settings.performance.store-mob-counts-in-array` | 数组存储生物计数 | `true` | bool | true/false | 否 | 用数组存储生物数量（减少内存） |
| `settings.performance.optimize-noise-generation` | 优化噪声生成 | `false` | bool | true/false | 是 | 优化世界生成的噪声计算 |
| `settings.performance.optimize-sun-burn-tick` | 优化阳光灼烧 tick | `true` | bool | true/false | 否 | 优化僵尸在阳光下燃烧的检测 |
| `settings.performance.optimized-CubePointRange` | 优化 CubePointRange | `true` | bool | true/false | 否 | 优化 CubePointRange 计算 |
| `settings.performance.check-frozen-ticks-before-landing-block` | 落地前检查冻结 tick | `true` | bool | true/false | 否 | 落地前先检查冻结 tick，减少不必要计算 |
| `settings.performance.skip-entity-move-if-movement-is-zero` | 零移动跳过实体位移 | `true` | bool | true/false | 否 | 实体无位移时跳过移动处理 |
| `settings.performance.skip-cloning-advancement-criteria` | 跳过进度条件克隆 | `false` | bool | true/false | 否 | 跳过进度条件的克隆操作 |
| `settings.performance.skip-negligible-planar-movement-multiplication` | 跳过可忽略平面运动乘法 | `true` | bool | true/false | 否 | 跳过可忽略的平面运动乘法计算 |
| `settings.performance.sleeping-block-entity` | 休眠方块实体 | `false` | bool | true/false | 否 | 允许方块实体进入休眠状态 |
| `settings.performance.equipment-tracking` | 装备追踪优化 | `false` | bool | true/false | 否 | 优化实体装备变更追踪 |

### settings.performance.remove（移除开销）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.performance.remove.tick-guard-lambda` | 移除 tick 守卫 lambda | `true` | bool | true/false | 否 | 移除 tick 处理中的 lambda 守卫，减少对象创建 |
| `settings.performance.remove.damage-lambda` | 移除伤害 lambda | `true` | bool | true/false | 否 | 移除伤害处理中的 lambda，减少对象创建 |

---

## settings.protocol（客户端模组协议兼容）

> Leaves 对各类客户端模组/工具协议的兼容支持，让生电玩家可用 Carpet、Syncmatica、PCA、AppleSkin、Servux、Litematica、BBOR、Jade、Xaero's Map、REI 等模组。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.protocol.strict-mode` | 严格模式 | `false` | bool | true/false | 否 | 协议严格模式，true 时仅允许已配置的协议，更安全 |

### settings.protocol.bladeren（Bladeren 协议）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.protocol.bladeren.protocol` | Bladeren 协议 | `true` | bool | true/false | 是 | 启用 Bladeren 客户端协议支持 |
| `settings.protocol.bladeren.mspt-sync-protocol` | MSPT 同步协议 | `false` | bool | true/false | 是 | 启用 MSPT（每刻毫秒数）同步给客户端 |
| `settings.protocol.bladeren.mspt-sync-tick-interval` | MSPT 同步间隔 | `20` | int | 1+ | 否 | 多少 tick 同步一次 MSPT，20=1 秒 |

### settings.protocol.syncmatica（Syncmatica 投影同步）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.protocol.syncmatica.enable` | 启用 Syncmatica | `false` | bool | true/false | 是 | 启用 Syncmatica 投影同步协议（Litematica 服务端投影） |
| `settings.protocol.syncmatica.quota` | 启用配额限制 | `false` | bool | true/false | 否 | 是否对投影同步启用数据量配额 |
| `settings.protocol.syncmatica.quota-limit` | 配额上限 | `40000000` | long | 0+ | 否 | 单次投影同步的最大数据量（字节） |

### settings.protocol.pca（PCA 同步协议）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.protocol.pca.pca-sync-protocol` | PCA 同步协议 | `false` | bool | true/false | 是 | 启用 PCA（PhiClientAddons）数据同步协议 |
| `settings.protocol.pca.pca-sync-player-entity` | 同步玩家实体 | `OPS` | enum | `NONE`/`OPS`/`ALL` | 否 | 玩家实体数据同步范围：NONE=不同步；OPS=仅 OP；ALL=所有人 |

### settings.protocol.appleskin（AppleSkin 饱食度同步）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.protocol.appleskin.protocol` | AppleSkin 协议 | `false` | bool | true/false | 是 | 启用 AppleSkin 饱食度/生命同步协议 |
| `settings.protocol.appleskin.sync-tick-interval` | 同步间隔 | `20` | int | 1+ | 否 | 多少 tick 同步一次饱和度数据 |

### settings.protocol.servux（Servux 协议套件）

> Servux 是生电向的服务端工具协议集合，向客户端提供结构/实体/HUD 等数据。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.protocol.servux.structure-protocol` | 结构协议 | `false` | bool | true/false | 是 | 向客户端发送结构边界数据（MiniHUD 用） |
| `settings.protocol.servux.entity-protocol` | 实体协议 | `false` | bool | true/false | 是 | 向客户端发送实体数据 |
| `settings.protocol.servux.hud-metadata-protocol` | HUD 元数据协议 | `false` | bool | true/false | 是 | 向客户端发送 HUD 元数据 |
| `settings.protocol.servux.hud-logger-protocol` | HUD 日志协议 | `false` | bool | true/false | 是 | 向客户端发送 HUD 日志数据 |
| `settings.protocol.servux.hud-enabled-loggers` | 启用的日志类型 | `[TPS, MOB_CAPS]` | list | — | 否 | 启用的 HUD 日志类型列表（如 TPS、MOB_CAPS） |
| `settings.protocol.servux.hud-update-interval` | HUD 更新间隔 | `1` | int | 1+ | 否 | HUD 数据更新间隔（秒） |
| `settings.protocol.servux.hud-metadata-protocol-share-seed` | HUD 共享种子 | `true` | bool | true/false | 否 | 是否通过 HUD 元数据协议共享世界种子 |

#### settings.protocol.servux.litematics（投影原理图）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.protocol.servux.litematics.enable` | 启用投影原理图 | `false` | bool | true/false | 是 | 启用服务端投影原理图分发 |
| `settings.protocol.servux.litematics.max-nbt-size` | 最大 NBT 大小 | `2097152` | int | 0+ | 否 | 单个投影原理图的最大 NBT 字节数（默认 2MB） |

### settings.protocol（其他协议）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.protocol.bbor-protocol` | BBOR 协议 | `false` | bool | true/false | 是 | 启用 BBOR（边界/结构显示）协议 |
| `settings.protocol.jade-protocol` | Jade 协议 | `false` | bool | true/false | 是 | 启用 Jade（WAILA 类信息显示）协议 |
| `settings.protocol.alternative-block-placement` | 替代方块放置 | `NONE` | enum | `NONE`/`CARPET`/`CLIENT` | 否 | 替代方块放置模式：NONE=禁用；CARPET=Carpet 风格；CLIENT=客户端风格 |
| `settings.protocol.xaero-map-protocol` | Xaero 地图协议 | `false` | bool | true/false | 是 | 启用 Xaero's Minimap/WorldMap 协议 |
| `settings.protocol.xaero-map-server-id` | Xaero 服务器 ID | `0` | int | 0+ | 否 | Xaero 地图的服务器唯一标识，避免不同服地图混淆 |
| `settings.protocol.leaves-carpet-support` | Leaves Carpet 支持 | `false` | bool | true/false | 是 | 启用 Carpet 模组协议兼容（部分 Carpet 功能） |
| `settings.protocol.rei-server-protocol` | REI 服务端协议 | `false` | bool | true/false | 是 | 启用 REI（Roughly Enough Items）服务端协议 |
| `settings.protocol.chat-image-protocol` | 聊天图片协议 | `false` | bool | true/false | 是 | 启用聊天图片协议（可在聊天发送图片） |

---

## settings.misc（杂项 / 服务器全局设置）

> 服务器运行层面的杂项配置：自动更新、外置登录、语言、聊天签名等。多为服务器级（非世界级）设置。

### settings.misc.async-keepalive（异步心跳）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.misc.async-keepalive.enable` | 启用异步心跳 | `false` | bool | true/false | 是 | 异步处理玩家保活（keepalive）检测，减少主线程压力 |
| `settings.misc.async-keepalive.timeout-seconds` | 心跳超时秒数 | `20` | int | 1+ | 否 | 多少秒无响应判定玩家掉线 |

### settings.misc.auto-update（自动更新）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.misc.auto-update.enable` | 启用自动更新 | `false` | bool | true/false | 否 | 是否启用 Leaves 自动更新 |
| `settings.misc.auto-update.download-source` | 下载源 | `application` | enum | `application`/`cloud` | 否 | 更新下载来源：application=官方应用源；cloud=云端 |
| `settings.misc.auto-update.allow-experimental` | 允许实验版 | `false` | bool | true/false | 否 | 是否允许更新到实验性版本 |
| `settings.misc.auto-update.time` | 更新时间 | `[14:00, 2:00]` | list | — | 否 | 每天检查更新的时间点列表（HH:mm 格式） |

### settings.misc.extra-yggdrasil-service（外置登录 / authlib-injector）

> 兼容第三方外置登录（authlib-injector / Yggdrasil）服务，支持非正版皮肤与登录。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.misc.extra-yggdrasil-service.enable` | 启用外置登录 | `false` | bool | true/false | 是 | 启用第三方 Yggdrasil 外置登录支持 |
| `settings.misc.extra-yggdrasil-service.login-protect` | 登录保护 | `false` | bool | true/false | 否 | 启用登录保护（防止登录劫持） |
| `settings.misc.extra-yggdrasil-service.urls` | 外置登录地址 | `[https://url.with.authlib-injector-yggdrasil]` | list | — | 是 | 外置登录服务的 URL 列表（authlib-injector 兼容） |

### settings.misc（其他杂项）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.misc.disable-method-profiler` | 禁用方法分析器 | `true` | bool | true/false | 否 | 禁用原版方法性能分析器（减少开销） |
| `settings.misc.no-chat-sign` | 取消聊天签名 | `true` | bool | true/false | 是 | 取消聊天消息签名验证（1.19+ 聊天举报相关） |
| `settings.misc.dont-respond-ping-before-start-fully` | 完全启动前不响应 ping | `true` | bool | true/false | 否 | 服务器完全启动前不响应列表 ping，避免启动中显示异常 |
| `settings.misc.server-lang` | 服务器语言 | `en_us` | enum | `en_us`/`zh_cn` 等 | 是 | 服务器内置消息语言（影响 Leaves 自身提示文本） |
| `settings.misc.server-mod-name` | 服务端模组名 | `Leaves` | string | — | 是 | F3 屏幕显示的服务端名称 |
| `settings.misc.bstats-privacy-mode` | bStats 隐私模式 | `false` | bool | true/false | 是 | 启用 bStats 隐私模式（不上报详细数据） |
| `settings.misc.force-minecraft-command` | 强制 Minecraft 命令 | `false` | bool | true/false | 否 | 强制使用原版 Minecraft 命令（覆盖插件命令） |
| `settings.misc.leaves-packet-event` | Leaves 数据包事件 | `false` | bool | true/false | 是 | 启用 Leaves 自定义数据包事件（插件用） |

---

## settings.region（区块文件格式）

> 控制世界区块的存储格式。⚠️ 修改格式相关选项需重启，且切换格式会涉及世界数据迁移。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.region.format` | 区块文件格式 | `ANVIL` | enum | `ANVIL`/`LINEAR` | 是 | 区块存储格式：ANVIL=原版格式；LINEAR=线性压缩格式（更省空间） |

### settings.region.linear（Linear 格式参数）

> 仅当 `format` 为 `LINEAR` 时生效。Linear 是一种高压缩比的区块存储格式。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.region.linear.version` | Linear 版本 | `V2` | enum | `V1`/`V2` | 是 | Linear 格式版本：V1=旧版；V2=新版（推荐） |
| `settings.region.linear.flush-max-threads` | 刷新最大线程数 | `6` | int | 1+ | 是 | Linear 刷盘最大线程数 |
| `settings.region.linear.flush-delay-ms` | 刷新延迟 | `100` | int | 0+ | 否 | Linear 刷盘延迟（毫秒） |
| `settings.region.linear.region-unload-idle-ms` | 空闲卸载时间 | `600000` | long | 0+ | 否 | 区块区域空闲多久后卸载（毫秒），默认 10 分钟 |
| `settings.region.linear.region-unload-check-interval-ms` | 卸载检查间隔 | `30000` | long | 0+ | 否 | 多久检查一次空闲区块区域（毫秒），默认 30 秒 |
| `settings.region.linear.max-flush-per-run` | 单次最大刷新数 | `256` | int | 1+ | 否 | 单次刷盘最多处理的区域文件数 |
| `settings.region.linear.use-virtual-thread` | 使用虚拟线程 | `true` | bool | true/false | 是 | 是否使用 Java 虚拟线程刷盘（Java 21+） |
| `settings.region.linear.compression-level` | 压缩级别 | `1` | int | 0-9 | 是 | Linear 压缩级别，0=无压缩，9=最高压缩，越高越省空间但越慢 |

---

## settings.fix（漏洞修复）

> Leaves 对原版/Paper 行为的修复与对齐，多数默认开启以修正已知问题。

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `settings.fix.vanilla-hopper` | 原版漏斗行为 | `false` | bool | true/false | 否 | true 时漏斗完全恢复原版行为（关闭优化） |
| `settings.fix.vanilla-display-name` | 原版显示名 | `true` | bool | true/false | 否 | 使用原版显示名处理逻辑 |
| `settings.fix.vanilla-portal-handle` | 原版传送门处理 | `true` | bool | true/false | 否 | 使用原版传送门处理逻辑 |
| `settings.fix.collision-behavior` | 碰撞行为 | `PAPER` | enum | `PAPER`/`BLOCK_SHAPE_VANILLA` | 否 | 碰撞计算方式：PAPER=Paper 行为；BLOCK_SHAPE_VANILLA=原版方块形状 |
| `settings.fix.stacked-container-destroyed-drop` | 堆叠容器销毁掉落 | `true` | bool | true/false | 否 | 堆叠的容器被破坏时正确掉落物品 |

---

## config-version（配置版本号）

| 配置项（英文） | 中文翻译 | 默认值 | 值类型 | 取值范围 | 需重启 | 说明 |
|---|---|---|---|---|---|---|
| `config-version` | 配置版本号 | `6` | int | — | 是 | 内部使用，**不要手动修改**。Leaves 用它做配置自动升级与兼容性判断 |

---

## 附录：配置文件不存在性核实

经以下源码核实，LeavesMC/Leaves **不存在** `config/leaves-global.yml`：

1. **`LeavesConfig.java`** 的 `init(File file)` 方法接收单一文件参数。
2. **`0003-Leaves-Server-Config.patch`** 中启动参数 `"leaves-settings"` 默认值为 `new File("leaves.yml")`。
3. **`GlobalConfigCreator.java`** 生成默认配置时写入 `new File("leaves.yml")`（单一文件）。
4. **`LeavesServerConfigProvider.java`**（spark 上报）中 `leaves.yml` 使用 `YamlConfigParser`（单文件），而 `paper/` 才用 `SplitYamlConfigParser`（拆分多文件）。
5. **LeavesMC/Configuration 仓库**仅含 `leaves.yml`，无 global 文件。
6. GitHub 代码搜索 `leaves-global` / `leaves-global.yml` 在 LeavesMC/Leaves 仓库命中 **0 条**。

**结论**：Leaves 的所有独有配置统一存放于根目录 `leaves.yml`，无独立的 `config/leaves-global.yml`。若您的配置框架需要区分"全局"与"世界"配置，建议按本文档的分类标题归类：`misc`/`region`/`fix`/`protocol`/`config-version` 为服务器全局向，`fakeplayer`/`minecraft-old`/`performance` 及 `modify` 下的玩法开关为世界玩法向，但二者均读写同一个 `leaves.yml`。
