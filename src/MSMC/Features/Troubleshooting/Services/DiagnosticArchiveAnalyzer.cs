// -----------------------------------------------------------------------------
// DiagnosticArchiveAnalyzer.cs — 玩家存档 + 区块分析业务逻辑
// 设计约束: P7 内存有界；Try-Catch 每个 .dat/.mca 独立；诚实返回链
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public interface IDiagnosticArchiveAnalyzer
{
    /// <summary>扫描 players/*.dat — NBT 异常 + 财富 Top10 榜单</summary>
    List<CheckResult> AnalyzePlayerDat(string playersDir);

    /// <summary>扫描 region/*.mca — 实体堆叠 / 物品堆叠 / 方块异常</summary>
    List<CheckResult> AnalyzeRegions(string regionDir, int maxRegionsToScan = 16);

    /// <summary>供 DiagnosticEngine 拿 Top10 财富榜</summary>
    List<PlayerStat> GetTopPlayers(int n = 10);
}

public class DiagnosticArchiveAnalyzer : IDiagnosticArchiveAnalyzer
{
    private readonly List<PlayerStat> _topPlayers = new();

    // ─── MC Wiki 物品价值表（钻石块 = 1 基准）───
    // 价值换算公式: 钻石 = 2 / 铁 = 2/8 ≈ 0.25 / 金 = 2/32 ≈ 0.0625 ...
    // P0 用简化值（按物品稀有度粗估）
    private static readonly Dictionary<string, double> ItemValue = new()
    {
        { "diamond_block", 81 }, { "diamond", 1 },
        { "iron_block", 8 }, { "iron_ingot", 0.125 },
        { "gold_block", 32 }, { "gold_ingot", 0.03125 },
        { "emerald_block", 81 }, { "emerald", 1 },
        { "netherite_block", 729 }, { "netherite_ingot", 9 },
        { "ancient_debris", 8 }, { "obsidian", 0 },
        { "enchanted_golden_apple", 64 },
        { "elytra", 512 }, { "trident", 256 },
        { "netherite_sword", 729 }, { "diamond_sword", 81 },
    };

    public List<CheckResult> AnalyzePlayerDat(string playersDir)
    {
        var results = new List<CheckResult>();
        if (!Directory.Exists(playersDir))
        {
            results.Add(new CheckResult("archive.player", Severity.Info, "Player",
                "未找到 players 目录", playersDir, false, null, null));
            return results;
        }

        var playerDats = Directory.GetFiles(playersDir, "*.dat");
        _topPlayers.Clear();

        foreach (var datFile in playerDats)
        {
            try
            {
                var player = AnalyzeSinglePlayer(datFile);
                if (player != null)
                {
                    _topPlayers.Add(player);

                    if (player.AnomalyCount > 0)
                    {
                        results.Add(new CheckResult("archive.player",
                            player.AnomalyCount > 5 ? Severity.Error : Severity.Warning,
                            "Player",
                            $"玩家 {player.Name} 存在 {player.AnomalyCount} 项 NBT 异常",
                            $"异常类型: {string.Join(", ", player.AnomalyTypes.Take(5))}",
                            true, null, new { player.Name, player.Uuid, anomalies = player.AnomalyTypes }));
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult("archive.player", Severity.Warning, "Player",
                    $"解析玩家存档失败", $"{Path.GetFileName(datFile)}: {ex.Message}",
                    false, null, null));
            }
        }

        _topPlayers.Sort((a, b) => b.WealthScore.CompareTo(a.WealthScore));

        if (_topPlayers.Count > 0)
        {
            var top3 = _topPlayers.Take(3).Select(p => $"{p.Name}={p.WealthScore:F1}").ToList();
            results.Add(new CheckResult("archive.player.wealth", Severity.Info, "Player",
                $"财富 Top 榜（共 {_topPlayers.Count} 名玩家）",
                $"Top 3: {string.Join(", ", top3)}", false, null, _topPlayers.Take(10)));
        }

        return results;
    }

    private PlayerStat? AnalyzeSinglePlayer(string datFile)
    {
        var root = DiagnosticNbtReader.ParseDatFile(datFile);
        if (root == null || root.Value is not Dictionary<string, NbtTag> compound) return null;

        // UUID: 文件名去掉 .dat — 但有些是 32 位无横杠，有些是带横杠
        string fileName = Path.GetFileNameWithoutExtension(datFile);
        string uuid = fileName;
        string name = "Unknown"; // .dat 里没有 Name（那在 usernamecache.json 里）

        int totalItems = 0;
        double wealth = 0;
        var anomalies = new List<string>();

        // 扫 4 个主要物品容器
        foreach (var tag in new[] { "Inventory", "EnderItems", "ArmorItems", "HandItems" })
        {
            if (!compound.TryGetValue(tag, out var list) || list.Value is not NbtList listData) continue;

            foreach (var itemTag in listData.Items)
            {
                if (itemTag.Type != NbtTagType.Compound || itemTag.Value is not Dictionary<string, NbtTag> item) continue;

                totalItems++;

                // 物品 ID
                string itemId = item.TryGetValue("id", out var idTag) && idTag.Value is string idStr ? idStr : "unknown";
                // 数量
                int count = item.TryGetValue("Count", out var countTag) && countTag.Value is int ci ? ci : 1;
                // Damage / durability
                int damage = item.TryGetValue("Damage", out var dmgTag) && dmgTag.Value is int di ? di : 0;

                // 财富累计
                if (ItemValue.TryGetValue(itemId, out double baseVal))
                    wealth += baseVal * count;

                // 异常检测
                if (count > 99) anomalies.Add($"物品堆叠 {itemId}×{count}");
                if (damage > 1561) anomalies.Add($"耐久溢出 {itemId} damage={damage}");

                // Enchantments 非法等级
                if (item.TryGetValue("tag", out var tagTag) && tagTag.Value is Dictionary<string, NbtTag> tagDict)
                {
                    CheckEnchantmentLegality(tagDict, anomalies);
                    CheckBigPayload(tagDict, itemId, anomalies);
                }
            }
        }

        return new PlayerStat(uuid, name, totalItems, wealth, anomalies.Count, anomalies);
    }

    private static void CheckEnchantmentLegality(Dictionary<string, NbtTag> tagDict, List<string> anomalies)
    {
        if (!tagDict.TryGetValue("Enchantments", out var enchTag) || enchTag.Value is not NbtList enchList) return;

        foreach (var ench in enchList.Items)
        {
            if (ench.Value is not Dictionary<string, NbtTag> e) continue;
            if (e.TryGetValue("lvl", out var lvlTag) && lvlTag.Value is int lvl)
            {
                if (lvl > 30) anomalies.Add($"非法附魔等级 lvl={lvl}");
            }
        }
    }

    private static void CheckBigPayload(Dictionary<string, NbtTag> tagDict, string itemId, List<string> anomalies)
    {
        // P0: 粗略估 — 序列化后的 tag 字符串 > 10KB
        try
        {
            string? tagStr = EstimateTagSize(tagDict);
            if (tagStr != null && tagStr.Length > 10_000)
                anomalies.Add($"大 payload 物品 {itemId} ({tagStr.Length / 1024}KB)");
        }
        catch { }
    }

    private static string? EstimateTagSize(Dictionary<string, NbtTag> tagDict)
    {
        try { return Newtonsoft.Json.JsonConvert.SerializeObject(tagDict); }
        catch { return null; }
    }

    public List<PlayerStat> GetTopPlayers(int n = 10) => _topPlayers.Take(n).ToList();

    // ═══════════════════════════════════════════════════════════
    // Region 扫描
    // ═══════════════════════════════════════════════════════════

    public List<CheckResult> AnalyzeRegions(string regionDir, int maxRegionsToScan = 16)
    {
        var results = new List<CheckResult>();
        if (!Directory.Exists(regionDir))
        {
            results.Add(new CheckResult("archive.region", Severity.Info, "Region",
                "未找到 region 目录", regionDir, false, null, null));
            return results;
        }

        var mcaFiles = Directory.GetFiles(regionDir, "*.mca").Take(maxRegionsToScan).ToList();
        int entityCriticalCount = 0;
        int itemCriticalCount = 0;
        int totalScanned = 0;

        foreach (var mca in mcaFiles)
        {
            try
            {
                var chunks = DiagnosticRegionReader.ParseRegionFile(mca);
                totalScanned += chunks.Count;

                foreach (var chunk in chunks.Where(c => c.ParseSucceeded && c.Root != null))
                {
                    if (chunk.Root!.Value is not Dictionary<string, NbtTag> rootDict) continue;

                    // 扫 Level / Entities
                    AnalyzeChunkEntities(rootDict, chunk, ref entityCriticalCount, ref itemCriticalCount, results);
                }
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult("archive.region", Severity.Warning, "Region",
                    $"解析 {Path.GetFileName(mca)} 失败", ex.Message, false, null, null));
            }
        }

        results.Add(new CheckResult("archive.region.stat", Severity.Info, "Region",
            $"已扫描 {mcaFiles.Count} 个 .mca 文件，{totalScanned} 个区块",
            $"实体 Critical={entityCriticalCount}，物品堆叠 Critical={itemCriticalCount}",
            false, null, new { McaScanned = mcaFiles.Count, ChunksScanned = totalScanned }));

        return results;
    }

    private static void AnalyzeChunkEntities(Dictionary<string, NbtTag> root, RegionChunkInfo chunk,
        ref int entityCritical, ref int itemCritical, List<CheckResult> results)
    {
        // Java 版 1.17+: entities / block_entities 在 chunk root 直接下
        // 旧版: 在 Level / Data / Level 嵌套下（P0 先处理 1.17+）

        if (root.TryGetValue("entities", out var entitiesTag) && entitiesTag.Value is NbtList entities)
        {
            var byPos = new Dictionary<(int x, int z), int>();
            string? worstType = null;
            int worstCount = 0;

            foreach (var ent in entities.Items)
            {
                if (ent.Value is not Dictionary<string, NbtTag> entDict) continue;

                string id = entDict.TryGetValue("id", out var idTag) && idTag.Value is string s ? s : "unknown";
                // Pos 是 [x, y, z] List<Double>
                if (!entDict.TryGetValue("Pos", out var posTag) || posTag.Value is not NbtList posList) continue;
                if (posList.Items.Count < 3) continue;

                double ex = posList.Items[0].Value is double dx ? dx : 0;
                double ez = posList.Items[2].Value is double dz ? dz : 0;

                var key = ((int)ex, (int)ez);
                byPos.TryGetValue(key, out int c);
                byPos[key] = c + 1;
                if (c + 1 > worstCount) { worstCount = c + 1; worstType = id; }
            }

            if (worstCount > 200)
            {
                entityCritical++;
                results.Add(new CheckResult("archive.region.entity.stack", Severity.Error, "Region",
                    $"区块 ({chunk.RegionX * 32 + chunk.ChunkX},{chunk.RegionZ * 32 + chunk.ChunkZ}) 实体堆叠严重",
                    $"{worstType} 在同坐标聚集 {worstCount} 个，可能导致服务器卡顿或崩溃",
                    true, null, new { ChunkX = chunk.ChunkX, ChunkZ = chunk.ChunkZ, Type = worstType, Count = worstCount }));
            }
            else if (worstCount > 50)
            {
                results.Add(new CheckResult("archive.region.entity.stack", Severity.Warning, "Region",
                    $"区块内同坐标实体聚集",
                    $"{worstType} × {worstCount}", false, null, new { Type = worstType, Count = worstCount }));
            }

            // item_entity 物品堆叠
            if (worstType == "minecraft:item" && worstCount > 256)
            {
                itemCritical++;
            }
        }
    }
}
