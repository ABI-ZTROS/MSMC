using McServerGuard.Models;

namespace McServerGuard.Services;

public class AppConfig
{
    public List<KnownServer> KnownServers { get; set; } = [];
    public string LastActiveServerId { get; set; } = string.Empty;
}

public interface IAppConfigService
{
    AppConfig Config { get; }
    void Load();
    void Save();

    void AddKnownServer(KnownServer server);
    void RemoveKnownServer(string id);
    void UpdateKnownServer(KnownServer server);
    KnownServer? FindByJarPath(string jarPath);
    List<KnownServer> GetAllKnownServers();
}
