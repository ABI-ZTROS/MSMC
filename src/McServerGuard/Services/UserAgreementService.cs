// -----------------------------------------------------------------------------
// 文件名: UserAgreementService.cs
// 命名空间: McServerGuard.Services
// 功能描述: 提供用户协议状态管理服务，支持协议同意状态的持久化存储
// 依赖组件: System.Text.Json, Serilog
// 设计模式: 单例模式（DI容器注册）
// -----------------------------------------------------------------------------
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace McServerGuard.Services;

/// <summary>
/// 用户协议服务实现
/// 管理用户协议的同意状态、同意时间与版本号，支持本地持久化存储
/// </summary>
public class UserAgreementService : IUserAgreementService
{
    /// <summary>
    /// 配置文件名
    /// </summary>
    private const string ConfigFileName = "user_agreement.json";

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public bool IsAgreed { get; private set; }

    /// <inheritdoc />
    public DateTime? AgreedAt { get; private set; }

    /// <inheritdoc />
    public string? AgreedVersion { get; private set; }

    /// <summary>
    /// 配置文件完整路径
    /// </summary>
    private readonly string _configPath;

    /// <summary>
    /// 初始化用户协议服务
    /// 构造时确定配置文件存储路径
    /// </summary>
    public UserAgreementService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "McServerGuard");
        _configPath = Path.Combine(appDataPath, ConfigFileName);
    }

    /// <summary>
    /// 标记用户已同意协议
    /// </summary>
    /// <param name="version">协议版本号</param>
    public void SetAgreed(string version)
    {
        IsAgreed = true;
        AgreedAt = DateTime.Now;
        AgreedVersion = version;
        Save();
    }

    /// <summary>
    /// 从本地配置文件加载用户协议状态
    /// </summary>
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

    /// <summary>
    /// 保存当前用户协议状态到本地配置文件
    /// </summary>
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

    /// <summary>
    /// 用户协议配置数据模型
    /// 用于 JSON 序列化/反序列化
    /// </summary>
    private class UserAgreementConfig
    {
        /// <summary>
        /// 是否已同意协议
        /// </summary>
        public bool IsAgreed { get; set; }

        /// <summary>
        /// 同意时间
        /// </summary>
        public DateTime? AgreedAt { get; set; }

        /// <summary>
        /// 同意的协议版本号
        /// </summary>
        public string? AgreedVersion { get; set; }
    }
}
