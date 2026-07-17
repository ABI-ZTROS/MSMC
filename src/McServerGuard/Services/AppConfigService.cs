using System.IO;
using System.Text.Json;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services;

public class AppConfigService : IAppConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "McServerGuard");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "app-config.json");

    public AppConfig Config { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                Config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                // 🧹 历史数据迁移：早期版本把带 PID 的 DisplayName 存进了 KnownServer.Name，
                // 导致"已知服务器"列表显示了 PID（如 "Folia @ test (PID: 17044)"）。
                // 已知服务器是静态档案，PID 是运行时概念，不该混在一起。
                // 加载时统一清理一遍，避免用户看到幽灵 PID 紧张。
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
            catch { /* 备份失败就算了 */ }
            Config = new AppConfig();
        }
    }

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

    public void UpdateKnownServer(KnownServer server)
    {
        var index = Config.KnownServers.FindIndex(s => s.Id == server.Id);
        if (index >= 0)
        {
            Config.KnownServers[index] = server;
            Save();
        }
    }

    public KnownServer? FindByJarPath(string jarPath)
    {
        return Config.KnownServers.FirstOrDefault(s =>
            string.Equals(s.ServerJarPath, jarPath, StringComparison.OrdinalIgnoreCase));
    }

    public List<KnownServer> GetAllKnownServers()
    {
        return Config.KnownServers.OrderByDescending(s => s.LastSeenAt).ToList();
    }
}
