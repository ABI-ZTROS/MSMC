using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace McServerGuard.Services;

public class UserAgreementService : IUserAgreementService
{
    private const string ConfigFileName = "user_agreement.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool IsAgreed { get; private set; }
    public DateTime? AgreedAt { get; private set; }
    public string? AgreedVersion { get; private set; }

    private readonly string _configPath;

    public UserAgreementService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "McServerGuard");
        _configPath = Path.Combine(appDataPath, ConfigFileName);
    }

    public void SetAgreed(string version)
    {
        IsAgreed = true;
        AgreedAt = DateTime.Now;
        AgreedVersion = version;
        Save();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<UserAgreementConfig>(json, JsonOptions);
                if (config != null)
                {
                    IsAgreed = config.IsAgreed;
                    AgreedAt = config.AgreedAt;
                    AgreedVersion = config.AgreedVersion;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载用户协议配置失败，使用默认值");
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var config = new UserAgreementConfig
            {
                IsAgreed = IsAgreed,
                AgreedAt = AgreedAt,
                AgreedVersion = AgreedVersion
            };

            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存用户协议配置失败");
        }
    }

    private class UserAgreementConfig
    {
        public bool IsAgreed { get; set; }
        public DateTime? AgreedAt { get; set; }
        public string? AgreedVersion { get; set; }
    }
}
