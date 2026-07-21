// -----------------------------------------------------------------------------
// 文件名: JarCoreIdentifier.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: 增强型服务器核心代号识别器 —— 基于 JAR 解包读取 MANIFEST.MF 的 Main-Class
//           辅以特征类存在性检查，作为识别链路的第三级兜底（JAR 名 → 配置文件 → JAR Manifest）
// 依赖组件: System.IO.Compression.ZipArchive（.NET 内置）, Serilog
// 设计模式: 缓存-aside 模式（5 分钟 TTL）, 管道-过滤器架构（Main-Class → 特征类消歧）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ServerDetection;

using System.IO;
using System.IO.Compression;
using McServerGuard.Constants;
using Serilog;

/// <summary>
/// 增强型服务器核心代号识别器 —— 通过解包 JAR 读取 MANIFEST.MF 识别核心类型
/// </summary>
/// <remarks>
/// 该识别器作为现有识别链路的第三级兜底：
/// 1. 第一级：JAR 文件名 glob 匹配（<see cref="ServerTypeClassifier.ClassifyByJarName"/>）
/// 2. 第二级：配置文件特征推断（<see cref="ServerTypeClassifier.InferFromConfigFiles"/>）
/// 3. 第三级（本类）：解包 JAR 读取 META-INF/MANIFEST.MF 的 Main-Class 字段
///
/// 解决场景：
/// - JAR 被重命名后无法通过文件名识别（如 myserver.jar、core.jar）
/// - Paper/Purpur/Pufferfish/Folia 互相混淆（Main-Class 相同，需特征类区分）
/// - 混合端（Mohist/Arclight/CatServer）完全无法识别
/// - 代理端（BungeeCord/Velocity）落入 Unknown
///
/// 采用缓存-aside 模式：5 分钟 TTL，JAR 内容不变则结果不变，
/// 避免每轮 3 秒检测循环都重复解包同一 JAR。
/// </remarks>
public sealed class JarCoreIdentifier
{
    /// <summary>
    /// Main-Class 字段到基础 ServerType 的映射表
    /// </summary>
    /// <remarks>
    /// 对于共享同一 Main-Class 的核心家族（Paper 系、Forge 系），
    /// 此处给出占位类型，由 <see cref="DisambiguatePaperFamily"/> /
    /// <see cref="DisambiguateForgeFamily"/> 进一步消歧。
    /// 映射数据基于网络搜索官方文档、Jenkins 构建、社区博客等确认。
    /// </remarks>
    private static readonly Dictionary<string, ServerType> MainClassMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vanilla（1.17+ 使用 bundler 打包；1.16 及以下为旧版）
        ["net.minecraft.bundler.Main"] = ServerType.Vanilla,
        ["net.minecraft.server.MinecraftServer"] = ServerType.Vanilla,
        // Spigot / CraftBukkit（CraftBukkit 是 Spigot 的底层，共用 Main-Class）
        ["org.bukkit.craftbukkit.Main"] = ServerType.Spigot,
        // Paper 系（Paperclip 包装器，Paper/Folia/Purpur/Pufferfish 共用，需特征类区分）
        ["io.papermc.paperclip.Paperclip"] = ServerType.Paper,
        ["io.papermc.paperclip.PaperClip"] = ServerType.Paper, // 大小写容错
        // Forge / NeoForge 系（1.13+ 共用 modlauncher，需特征类区分）
        ["cpw.mods.modlauncher.Launcher"] = ServerType.Forge,
        // 旧版 Forge（1.12 及以下）
        ["net.minecraftforge.fml.relauncher.ServerLaunchWrapper"] = ServerType.Forge,
        // Fabric（fabric-loader 0.12+；旧版无 impl 子包）
        ["net.fabricmc.loader.impl.launch.server.FabricServerLauncher"] = ServerType.Fabric,
        ["net.fabricmc.loader.launch.server.FabricServerLauncher"] = ServerType.Fabric,
        // BungeeCord 代理端
        ["net.md_5.bungee.Bootstrap"] = ServerType.BungeeCord,
        // Velocity 代理端
        ["com.velocitypowered.proxy.Velocity"] = ServerType.Velocity,
        // SpongeVanilla
        ["org.spongepowered.server.launch.VanillaServerLaunch"] = ServerType.Sponge,
    };

    /// <summary>
    /// 缓存 TTL —— JAR 内容不变则识别结果不变，5 分钟足够长以应对 3 秒检测循环
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// JAR 路径到（识别结果, 时间戳）的缓存字典
    /// </summary>
    private readonly Dictionary<string, (ServerType Type, DateTime Timestamp)> _cache = new();

    /// <summary>
    /// 缓存读写锁 —— 保护 <see cref="_cache"/> 的并发访问
    /// </summary>
    private readonly object _cacheLock = new();

    /// <summary>
    /// 异步识别 JAR 文件对应的服务器核心类型
    /// </summary>
    /// <param name="jarPath">JAR 文件的绝对路径</param>
    /// <returns>识别出的服务器类型；无法识别或异常时返回 <see cref="ServerType.Unknown"/></returns>
    /// <remarks>
    /// 识别流程：
    /// 1. 查缓存（命中且未过期直接返回）
    /// 2. FileStream + FileShare.ReadWrite 打开 JAR（避免与运行中 Java 进程的文件锁冲突）
    /// 3. ZipArchive 读取 META-INF/MANIFEST.MF
    /// 4. 逐行解析 Main-Class 字段
    /// 5. 按 MainClassMap 查基础类型
    /// 6. Paper 系/Forge 系用特征类检查进一步区分
    /// 7. 写缓存并返回
    ///
    /// 容错策略：任何异常返回 Unknown，不抛异常（识别失败不应影响主检测流程）
    /// </remarks>
    public async Task<ServerType> IdentifyAsync(string jarPath)
    {
        if (string.IsNullOrWhiteSpace(jarPath))
            return ServerType.Unknown;

        // 缓存命中判定
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(jarPath, out var cached)
                && (DateTime.Now - cached.Timestamp) < CacheTtl)
            {
                Log.Debug("♻️ JAR Manifest 识别命中缓存: {Jar} → {Type}", jarPath, cached.Type);
                return cached.Type;
            }
        }

        // 异步执行解包（避免阻塞调用线程）
        var result = await Task.Run(() => IdentifyCore(jarPath));

        // 写缓存
        lock (_cacheLock)
        {
            _cache[jarPath] = (result, DateTime.Now);
        }

        return result;
    }

    /// <summary>
    /// 同步核心识别实现 —— 解包 JAR、读取 MANIFEST.MF、特征类消歧
    /// </summary>
    /// <param name="jarPath">JAR 文件绝对路径</param>
    /// <returns>识别出的服务器类型；失败返回 Unknown</returns>
    private static ServerType IdentifyCore(string jarPath)
    {
        if (!File.Exists(jarPath))
        {
            Log.Debug("JAR 文件不存在，跳过 Manifest 识别: {Jar}", jarPath);
            return ServerType.Unknown;
        }

        try
        {
            // FileShare.ReadWrite 共享读取，避免与运行中 Java 进程的独占文件锁冲突
            using var fs = new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var jar = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

            var manifestEntry = jar.GetEntry("META-INF/MANIFEST.MF");
            if (manifestEntry is null)
            {
                Log.Debug("JAR 缺少 META-INF/MANIFEST.MF: {Jar}", jarPath);
                return ServerType.Unknown;
            }

            var mainClass = ReadMainClass(manifestEntry);
            if (string.IsNullOrWhiteSpace(mainClass))
            {
                Log.Debug("MANIFEST.MF 中未找到 Main-Class 字段: {Jar}", jarPath);
                return ServerType.Unknown;
            }

            Log.Debug("🔬 JAR Main-Class: {MainClass} ({Jar})", mainClass, jarPath);

            // 查 MainClassMap 获取基础类型
            if (!MainClassMap.TryGetValue(mainClass, out var baseType))
            {
                Log.Debug("Main-Class 未在映射表中: {MainClass}", mainClass);
                return ServerType.Unknown;
            }

            // Paper 系消歧（Folia/Purpur/Pufferfish/Paper）
            if (baseType == ServerType.Paper)
                return DisambiguatePaperFamily(jar);

            // Forge 系消歧（Mohist/Arclight/CatServer/NeoForge/Forge）
            if (baseType == ServerType.Forge)
                return DisambiguateForgeFamily(jar);

            return baseType;
        }
        catch (IOException ex)
        {
            Log.Debug(ex, "JAR 解包 IO 异常: {Jar}: {Message}", jarPath, ex.Message);
            return ServerType.Unknown;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Debug(ex, "JAR 解包权限不足: {Jar}: {Message}", jarPath, ex.Message);
            return ServerType.Unknown;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "JAR 解包异常: {Jar}: {Message}", jarPath, ex.Message);
            return ServerType.Unknown;
        }
    }

    /// <summary>
    /// 从 MANIFEST.MF 条目中读取 Main-Class 字段值
    /// </summary>
    /// <param name="manifestEntry">MANIFEST.MF 的 ZipArchiveEntry</param>
    /// <returns>Main-Class 的全限定类名；缺失返回 null</returns>
    /// <remarks>
    /// MANIFEST.MF 格式：每行一个 "Key: Value" 对，Main-Class 行格式为：
    /// Main-Class: io.papermc.paperclip.Paperclip
    /// </remarks>
    private static string? ReadMainClass(ZipArchiveEntry manifestEntry)
    {
        using var stream = manifestEntry.Open();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            // MANIFEST.MF 行格式："Key: Value"，去除前后空白后比较
            if (line.StartsWith("Main-Class:", StringComparison.OrdinalIgnoreCase))
            {
                return line["Main-Class:".Length..].Trim();
            }
        }
        return null;
    }

    /// <summary>
    /// 消歧 Paper 系核心（Folia/Purpur/Pufferfish/Paper）
    /// </summary>
    /// <param name="jar">已打开的 JAR ZipArchive</param>
    /// <returns>具体的核心类型</returns>
    /// <remarks>
    /// Paper 系共享 Paperclip 包装器作为 Main-Class，需通过特征类区分：
    /// - Folia：io.papermc.paper.threadedregions.RegionizedServer（多线程区域化）
    /// - Purpur：org.purpurmc.purpur.PurpurConfig（Purpur 配置类）
    /// - Pufferfish：gg.pufferfish.pufferfish.PufferfishConfig（Pufferfish 配置类）
    /// - Paper：排除以上后默认
    ///
    /// 派生类优先检测，确保 Folia/Purpur/Pufferfish 不会被误判为基类 Paper。
    /// </remarks>
    private static ServerType DisambiguatePaperFamily(ZipArchive jar)
    {
        if (jar.GetEntry("io/papermc/paper/threadedregions/RegionizedServer.class") is not null)
            return ServerType.Folia;
        if (jar.GetEntry("org/purpurmc/purpur/PurpurConfig.class") is not null)
            return ServerType.Purpur;
        if (jar.GetEntry("gg/pufferfish/pufferfish/PufferfishConfig.class") is not null)
            return ServerType.Pufferfish;
        return ServerType.Paper;
    }

    /// <summary>
    /// 消歧 Forge 系核心（Mohist/Arclight/CatServer/NeoForge/Forge）
    /// </summary>
    /// <param name="jar">已打开的 JAR ZipArchive</param>
    /// <returns>具体的核心类型</returns>
    /// <remarks>
    /// Forge/NeoForge 系共享 modlauncher.Launcher 作为 Main-Class，需通过包前缀区分：
    /// - Mohist：com/mohistmc/ 包存在（混合端，Forge + Bukkit）
    /// - Arclight：io/izzel/arclight/ 包存在（混合端，可配置 Forge/NeoForge/Fabric + Bukkit）
    /// - CatServer：catserver/ 包存在（混合端，Forge + Bukkit）
    /// - NeoForge：net/neoforged/ 包存在（Forge 的现代分支）
    /// - Forge：排除以上后默认
    ///
    /// 混合端没有单一特征类，需通过包前缀检测（遍历 Entries）。
    /// 混合端优先检测，确保 Mohist/Arclight/CatServer 不会被误判为基类 Forge。
    /// </remarks>
    private static ServerType DisambiguateForgeFamily(ZipArchive jar)
    {
        if (HasEntryPrefix(jar, "com/mohistmc/"))
            return ServerType.Mohist;
        if (HasEntryPrefix(jar, "io/izzel/arclight/"))
            return ServerType.Arclight;
        if (HasEntryPrefix(jar, "catserver/"))
            return ServerType.CatServer;
        if (HasEntryPrefix(jar, "net/neoforged/"))
            return ServerType.NeoForge;
        return ServerType.Forge;
    }

    /// <summary>
    /// 检查 JAR 是否存在指定前缀的条目（用于包级别特征检测）
    /// </summary>
    /// <param name="jar">已打开的 JAR ZipArchive</param>
    /// <param name="prefix">条目路径前缀（如 "com/mohistmc/"）</param>
    /// <returns>true 表示存在匹配前缀的条目</returns>
    /// <remarks>
    /// ZipArchive 的 GetEntry 仅支持精确匹配，包前缀检查需遍历 Entries。
    /// 命中即返回，避免全量遍历。
    /// </remarks>
    private static bool HasEntryPrefix(ZipArchive jar, string prefix)
    {
        foreach (var entry in jar.Entries)
        {
            if (entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
