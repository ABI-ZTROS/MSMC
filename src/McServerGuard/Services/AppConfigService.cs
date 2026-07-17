// -----------------------------------------------------------------------------
// 文件名: AppConfigService.cs
// 命名空间: McServerGuard.Services
// 功能描述: 应用程序全局配置持久化服务，管理已知服务器档案、用户偏好设置的加载/保存，支持历史数据迁移与损坏文件备份恢复
// 依赖组件: System.IO, System.Text.Json, Serilog, McServerGuard.Models
// 设计模式: 单例（DI托管生命周期）、仓储模式（KnownServers CRUD）、备忘录模式（配置备份）
// -----------------------------------------------------------------------------
using System.IO;
using System.Text.Json;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services;

/// <summary>
/// 应用配置服务 —— 全局配置的持久化管理与已知服务器档案的CRUD编排器
/// </summary>
/// <remarks>
/// 核心职责：
/// <list type="bullet">
/// <item>配置加载：从%AppData%/McServerGuard/app-config.json读取并反序列化全局配置</item>
/// <item>配置保存：序列化并写入配置文件，自动创建目录</item>
/// <item>数据迁移：加载时自动清理历史版本遗留的PID后缀等脏数据</item>
/// <item>容错处理：配置损坏时自动备份并降级为默认配置</item>
/// <item>档案管理：已知服务器的增删改查与按JAR路径去重</item>
/// </list>
/// </remarks>
public class AppConfigService : IAppConfigService
{
    /// <summary>
    /// 配置文件所在目录路径（%AppData%/McServerGuard）
    /// </summary>
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "McServerGuard");

    /// <summary>
    /// 全局配置文件完整路径
    /// </summary>
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "app-config.json");

    /// <summary>
    /// 当前内存中的全局配置实例
    /// </summary>
    public AppConfig Config { get; private set; } = new();

    /// <summary>
    /// 加载全局配置 —— 从磁盘读取配置文件并执行数据迁移与容错处理
    /// </summary>
    /// <remarks>
    /// 加载流程：
    /// 1. 配置文件存在 → 反序列化 → 历史数据迁移 → 完成
    /// 2. 配置文件不存在 → 使用默认配置
    /// 3. 反序列化失败 → 备份损坏文件 → 使用默认配置
    /// </remarks>
    public void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                // 历史数据迁移：清理KnownServer.Name中遗留的PID后缀
                // 早期版本将运行时PID写入已知服务器名称（如"Folia @ test (PID: 17044)"），
                // 已知服务器是静态档案，PID属于运行时概念，不应持久化存储。
                foreach (var ks in Config.KnownServers)
                {
                    if (!string.IsNullOrEmpty(ks.Name) && ks.Name.Contains("(PID:"))
                    {
                        ks.Name = System.Text.RegularExpressions.Regex.Replace(
                            ks.Name,
                            @"\s*\(PID:\s*\d+\)\s*$",
                            string.Empty).Trim();
                        Log.Information("🧹 已清理已知服务器名称中的 PID 后缀: {Name}", ks.Name);
                    }
                }

                Log.Information("📂 全局配置已加载，已知服务器 {Count} 个", Config.KnownServers.Count);
            }
            else
            {
                Config = new AppConfig();
                Log.Information("📂 未找到全局配置文件，使用默认配置");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 加载全局配置失败，使用默认配置");
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var bakPath = ConfigPath + ".corrupt.bak";
                    File.Copy(ConfigPath, bakPath, true);
                    Log.Warning("📦 已备份损坏的全局配置到: {BakPath}", bakPath);
                }
            }
            catch { /* 备份失败不影响主流程 */ }
            Config = new AppConfig();
        }
    }

    /// <summary>
    /// 保存全局配置到磁盘
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
            Log.Information("💾 全局配置已保存");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ 保存全局配置失败");
        }
    }

    /// <summary>
    /// 添加或更新已知服务器档案 —— 基于JAR路径去重
    /// </summary>
    /// <param name="server">服务器档案信息</param>
    /// <remarks>
    /// 幂等操作：若JAR路径已存在则更新现有记录，否则新增。
    /// 操作完成后自动持久化到磁盘。
    /// </remarks>
    public void AddKnownServer(KnownServer server)
    {
        var existing = FindByJarPath(server.ServerJarPath);
        if (existing != null)
        {
            existing.Name = server.Name;
            existing.WorkingDirectory = server.WorkingDirectory;
            existing.JavaPath = server.JavaPath;
            existing.MaxHeapMemoryBytes = server.MaxHeapMemoryBytes;
            existing.Port = server.Port;
            existing.LastSeenAt = DateTime.Now;
            Log.Information("🔄 已知服务器已更新: {Name}", server.Name);
        }
        else
        {
            Config.KnownServers.Add(server);
            Log.Information("➕ 新增已知服务器: {Name}", server.Name);
        }
        Save();
    }

    /// <summary>
    /// 移除指定ID的已知服务器档案
    /// </summary>
    /// <param name="id">服务器档案唯一标识</param>
    public void RemoveKnownServer(string id)
    {
        var server = Config.KnownServers.FirstOrDefault(s => s.Id == id);
        if (server != null)
        {
            Config.KnownServers.Remove(server);
            Log.Information("🗑️ 已移除已知服务器: {Name}", server.Name);
            Save();
        }
    }

    /// <summary>
    /// 更新已知服务器档案
    /// </summary>
    /// <param name="server">更新后的服务器档案</param>
    public void UpdateKnownServer(KnownServer server)
    {
        var index = Config.KnownServers.FindIndex(s => s.Id == server.Id);
        if (index >= 0)
        {
            Config.KnownServers[index] = server;
            Save();
        }
    }

    /// <summary>
    /// 根据JAR文件路径查找已知服务器档案
    /// </summary>
    /// <param name="jarPath">JAR文件完整路径</param>
    /// <returns>匹配的服务器档案；未找到返回<c>null</c></returns>
    public KnownServer? FindByJarPath(string jarPath)
    {
        return Config.KnownServers.FirstOrDefault(s =>
            string.Equals(s.ServerJarPath, jarPath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取所有已知服务器档案，按最后发现时间倒序排列
    /// </summary>
    /// <returns>按LastSeenAt降序排列的服务器档案列表</returns>
    public List<KnownServer> GetAllKnownServers()
    {
        return Config.KnownServers.OrderByDescending(s => s.LastSeenAt).ToList();
    }
}
