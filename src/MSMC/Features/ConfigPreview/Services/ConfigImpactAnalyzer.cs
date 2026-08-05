using io.NET.ZTR_OS.Features.ConfigPreview.Models;

namespace io.NET.ZTR_OS.Features.ConfigPreview.Services;

public class ConfigImpactAnalyzer
{
    private class Rule
    {
        public Func<string, string?, string?, bool> Match { get; init; } =
            (_, _, _) => false;
        public ImpactSeverity Severity { get; init; }
        public Func<string?, string?, string> Description { get; init; } = (_, _) => string.Empty;
        public string? Recommendation { get; init; }
        public string Icon { get; init; } = string.Empty;
    }

    private static bool Eq(string? a, string b) =>
        string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);

    private static bool TryParseInt(string? s, out int v)
    {
        v = 0;
        return int.TryParse(s?.Trim(), out v);
    }

    private readonly List<Rule> _rules = new()
    {
        // ======== 1. online-mode (High: true→false, Info: false→true) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/online-mode", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "true") && Eq(after, "false"),
            Severity = ImpactSeverity.High,
            Icon = "🔴",
            Description = (_, _) => "关闭正版验证→任何玩家都能以任意昵称进入服务器，存在被冒名顶替和 grief 风险",
            Recommendation = "若非开离线服的明确需求，建议保持 online-mode=true。若需配合 BungeeCord 使用，请同时启用 proxy-settings 并配置防火墙白名单。",
        },
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/online-mode", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "false") && Eq(after, "true"),
            Severity = ImpactSeverity.Info,
            Icon = "🔵",
            Description = (_, _) => "开启正版验证，仅 Minecraft 正版账号可进入",
            Recommendation = "建议在公开服务器上保持开启，防止冒名进入。",
        },

        // ======== 2. pvp (Medium: true→false) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/pvp", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "true") && Eq(after, "false"),
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (_, _) => "全服玩家互相 PvP 被禁用，玩家间无法直接攻击",
            Recommendation = "适合纯建造/养老服。若为竞技场/PVP 服务器请恢复为 true。",
        },

        // ======== 3. white-list (Medium: false→true) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/white-list", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "false") && Eq(after, "true"),
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (_, _) => "启用白名单：请确认你自己在白名单内，否则保存后连管理员也无法进入",
            Recommendation = "在 /whitelist add <你的ID> 之后再开启，或确保 ops.json 中的管理员同时在 white-list 中。",
        },

        // ======== 4. difficulty (Medium: 向更难升级, Info: 向更容易) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/difficulty", StringComparison.OrdinalIgnoreCase) &&
                (Eq(after, "hard") || (Eq(before, "peaceful") && !Eq(after, "peaceful"))),
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (b, a) => Eq(a, "hard")
                ? "全服刷怪难度提升到 hard：怪物伤害更高、AI 更激进，低装备玩家易死亡"
                : $"全服难度由 {b} 提升至 {a}，生存体验明显变化",
            Recommendation = "提前告知玩家难度变更，必要时为新手区设置 difficulty=peaceful 的多世界插件。",
        },
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/difficulty", StringComparison.OrdinalIgnoreCase) &&
                (Eq(before, "hard") || Eq(before, "normal")) &&
                (Eq(after, "peaceful") || Eq(after, "easy")),
            Severity = ImpactSeverity.Info,
            Icon = "🔵",
            Description = (b, a) => $"难度降低（{b} → {a}），怪物生成和伤害减少",
            Recommendation = "适合新手开荒期，玩家群体稳定后可逐步提高。",
        },

        // ======== 5. view-distance (High: 大, Info: 小) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/view-distance", StringComparison.OrdinalIgnoreCase) &&
                TryParseInt(before, out var b) && TryParseInt(after, out var a) && a - b >= 4,
            Severity = ImpactSeverity.High,
            Icon = "🔴",
            Description = (b, a) => $"视距由 {b} 加大到 {a}：内存占用和 CPU 会显著上涨，低配置机器可能导致 TPS 骤降",
            Recommendation = "建议 4G 内存以下服务器保持 8-10，Paper/Purpur 可配合 chunk-loading 异步加载微调。",
        },
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/view-distance", StringComparison.OrdinalIgnoreCase) &&
                TryParseInt(before, out var b2) && TryParseInt(after, out var a2) && a2 < b2,
            Severity = ImpactSeverity.Info,
            Icon = "🔵",
            Description = (b, a) => $"视距减小（{b} → {a}），有助于释放内存和降低 CPU 占用",
            Recommendation = "适合资源紧张或玩家数较多时使用，配合 entity-broadcast-range-percentage 调节。",
        },

        // ======== 6. paper-global max-autosave-chunks (Medium) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("paper-global.yml/chunk-loading/max-autosave-chunks", StringComparison.OrdinalIgnoreCase) &&
                TryParseInt(before, out var pb) && TryParseInt(after, out var pa) && pa > pb,
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (b, a) => $"更大的自动保存批次（{b} → {a}），每次保存写入更多区块，可能造成瞬时卡顿峰值",
            Recommendation = "若 SSD 写入性能较好可接受；建议结合 autosave-interval 综合调节，或启用异步 I/O 选项。",
        },

        // ======== 7. bukkit spawn-limits/monsters (Medium: 调大) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("bukkit.yml/spawn-limits/monsters", StringComparison.OrdinalIgnoreCase) &&
                TryParseInt(before, out var mb) && TryParseInt(after, out var ma) && ma > mb,
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (b, a) => $"世界刷怪上限提高（{b} → {a}），单位体积内怪物数量上限增加",
            Recommendation = "留意 TPS 和实体数量，高怪物上限对低配置主机压力明显。必要时配合 MobStacking 插件。",
        },

        // ======== 9. gamemode (Medium: 变 creative) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/gamemode", StringComparison.OrdinalIgnoreCase) &&
                Eq(after, "creative"),
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (_, _) => $"默认游戏模式切换为 creative：新玩家进入即获得创造权限，可能破坏生存经济平衡",
            Recommendation = "生存服务器推荐 survival；公共服除非明确做创造分区，否则避免使用 creative 作为默认。",
        },

        // ======== 10. level-seed (Info) ========
        new()
        {
            Match = (key, _, _) =>
                key.StartsWith("server.properties/level-seed", StringComparison.OrdinalIgnoreCase),
            Severity = ImpactSeverity.Info,
            Icon = "🔵",
            Description = (_, _) => "地图种子已变更：该改动仅对尚未生成的新区块生效，已生成的世界不会重新生成",
            Recommendation = "若希望重新生成地图，请备份并删除 world/ 目录，或使用多世界插件创建新世界。",
        },

        // ======== 11. max-players (Medium: 大幅增加) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/max-players", StringComparison.OrdinalIgnoreCase) &&
                TryParseInt(before, out var mpb) && TryParseInt(after, out var mpa) && mpa > mpb,
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (b, a) => $"最大玩家数由 {b} 提升到 {a}，同时在线人数上限增加",
            Recommendation = "请根据带宽（建议每位玩家 1-2 Mbps 上行）、内存和 CPU 核数评估容量。",
        },

        // ======== 12. server-port (Medium) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/server-port", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(before?.Trim(), after?.Trim(), StringComparison.Ordinal),
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (b, a) => $"服务器端口变更（{b} → {a}），玩家需用新端口连接",
            Recommendation = "同步更新内网穿透/端口映射规则、防火墙放行以及服务器列表对外公告的地址。",
        },

        // ======== 13. motd (Info) ========
        new()
        {
            Match = (key, _, _) =>
                key.StartsWith("server.properties/motd", StringComparison.OrdinalIgnoreCase),
            Severity = ImpactSeverity.Info,
            Icon = "🔵",
            Description = (_, _) => "服务器标语（MOTD）已更新，玩家在多人游戏列表中看到的介绍文字会变化",
            Recommendation = "建议 MOTD 控制在两行内，避免使用特殊字符导致部分客户端显示乱码。",
        },

        // ======== 14. allow-flight (Info) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/allow-flight", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "false") && Eq(after, "true"),
            Severity = ImpactSeverity.Info,
            Icon = "🔵",
            Description = (_, _) => "允许飞行：原版飞行类 Mod/客户端移动不再被自动踢出",
            Recommendation = "若需防止作弊飞行，请配合 NoCheatPlus/Matrix 等反作弊插件使用。",
        },

        // ======== 15. enable-command-block (Medium) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/enable-command-block", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "false") && Eq(after, "true"),
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (_, _) => "命令方块已启用：OP 可放置并执行指令，错误的连锁指令可能造成服内异常",
            Recommendation = "仅授权给可信任的管理员使用，并定期审计功能服务器中的命令方块逻辑。",
        },

        // ======== 16. resource-pack (Info) ========
        new()
        {
            Match = (key, _, _) =>
                key.StartsWith("server.properties/resource-pack", StringComparison.OrdinalIgnoreCase),
            Severity = ImpactSeverity.Info,
            Icon = "🔵",
            Description = (_, _) => "资源包地址已更新，玩家进入服务器时会被提示下载新资源包",
            Recommendation = "确保资源包 URL 可直连（HTTPS 优先），并配置 resource-pack-sha1 以利用客户端缓存。",
        },

        // ======== 17. force-gamemode (Medium) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/force-gamemode", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "false") && Eq(after, "true"),
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (_, _) => "强制游戏模式：所有玩家进入时会被强制切换到 server.properties/gamemode 指定的模式",
            Recommendation = "多模式服务器需谨慎开启，避免玩家个人 gamemode 被覆盖导致存档体验丢失。",
        },

        // ======== 18. spawn-protection (Medium: 调大) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/spawn-protection", StringComparison.OrdinalIgnoreCase) &&
                TryParseInt(before, out var spb) && TryParseInt(after, out var spa) && spa > spb,
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (b, a) => $"出生点保护范围扩大（{b} → {a}），非 OP 在出生点方圆内无法破坏或放置方块",
            Recommendation = "设置为 0 可完全禁用出生点保护；社区服建议保留适度保护以防止 grief。",
        },

        // ======== 19. player-idle-timeout (Medium: 非 0 设置) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/player-idle-timeout", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "0") && TryParseInt(after, out var pit) && pit > 0,
            Severity = ImpactSeverity.Medium,
            Icon = "🟡",
            Description = (_, a) => $"玩家挂机超时已设置为 {a} 分钟，长时间 AFK 玩家会被自动踢出",
            Recommendation = "挂机收益类服（如刷怪塔/农场）需谨慎使用，避免玩家投诉被误踢。",
        },

        // ======== 20. hardcore (High: false→true) ========
        new()
        {
            Match = (key, before, after) =>
                key.StartsWith("server.properties/hardcore", StringComparison.OrdinalIgnoreCase) &&
                Eq(before, "false") && Eq(after, "true"),
            Severity = ImpactSeverity.High,
            Icon = "🔴",
            Description = (_, _) => "极限模式已开启：玩家死亡后无法复活，自动切换到 spectator 视角，且无法通过 /gamemode 解除",
            Recommendation = "建议在测试服充分体验后再在正式服开启，开启前务必备份世界存档。",
        },
    };

    public List<ConfigImpactSummary> Analyze(List<(string key, string? before, string? after)> changedKVs)
    {
        var results = new List<ConfigImpactSummary>(changedKVs.Count);

        foreach (var (key, before, after) in changedKVs)
        {
            Rule? matched = null;
            foreach (var rule in _rules)
            {
                if (rule.Match(key, before, after))
                {
                    matched = rule;
                    break;
                }
            }

            if (matched != null)
            {
                results.Add(new ConfigImpactSummary
                {
                    Key = key,
                    BeforeValue = before,
                    AfterValue = after,
                    ImpactSeverity = matched.Severity,
                    Icon = matched.Icon,
                    Description = matched.Description(before, after),
                    Recommendation = matched.Recommendation,
                });
            }
            else
            {
                results.Add(new ConfigImpactSummary
                {
                    Key = key,
                    BeforeValue = before,
                    AfterValue = after,
                    ImpactSeverity = ImpactSeverity.Info,
                    Icon = "⚪",
                    Description = $"未找到该键的预定义规则，建议自行核对文档（key={key}）",
                    Recommendation = "可查阅对应核心官方 Wiki：server.properties 参照 Minecraft Wiki，Paper/Purpur Yaml 参照各自官方文档。",
                });
            }
        }

        return results
            .OrderBy(r => r.ImpactSeverity)
            .ToList();
    }
}
