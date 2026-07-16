// 📋 配置管理器接口 —— 统一管理各种格式的配置文件
// 不管你是 .properties 还是 .yml 还是 .json，都归我管 💪
namespace McServerGuard.Services.ConfigManagement;

/// <summary>
/// 配置管理器接口 —— 提供统一的方式来读写不同格式的 Minecraft 配置文件 🎛️
/// 
/// 支持的格式：
///   - .properties (server.properties)
///   - .yml / .yaml (spigot.yml, paper-global.yml 等)
///   - .json (某些模组的配置文件)
/// 
/// 核心能力：
///   1. 根据文件扩展名自动选择合适的解析器
///   2. 验证配置值是否符合约束（范围、枚举、正则等）
///   3. 按分类组织配置项（方便 UI 展示）
///   4. 将嵌套配置"压平"为 key.path 格式
/// </summary>
public interface IConfigManager
{
    /// <summary>
    /// 读取配置文件并返回扁平化的键值对 📖
    /// 
    /// 自动根据文件扩展名选择解析器：
    ///   - .properties → PropertiesParser
    ///   - .yml/.yaml  → YamlParser（嵌套结构会被压平）
    ///   - .json       → JSON 解析器（嵌套结构会被压平）
    /// </summary>
    /// <param name="filePath">配置文件的完整路径</param>
    /// <returns>扁平化的键值对字典</returns>
    public Task<Dictionary<string, string>> ReadConfigAsync(string filePath);

    /// <summary>
    /// 将配置写回文件 💾
    /// 
    /// 自动根据文件扩展名选择序列化器。
    /// 对于 YAML/JSON，会将扁平化的键值对还原为嵌套结构后再写入。
    /// </summary>
    /// <param name="filePath">配置文件的完整路径</param>
    /// <param name="config">扁平化的键值对字典</param>
    public Task SaveConfigAsync(string filePath, Dictionary<string, string> config);

    /// <summary>
    /// 获取配置项的描述符（包含中文翻译和约束信息）🏷️
    /// </summary>
    /// <param name="key">配置项键名</param>
    /// <param name="configFileName">配置文件名（如 "server.properties"）</param>
    /// <returns>描述符，未注册的配置项返回 null</returns>
    public ServerConfigDescriptor? GetDescriptor(string key, string configFileName);

    /// <summary>
    /// 验证配置值是否符合约束 ✅
    /// 
    /// 根据 ServerConfigDescriptor 中定义的规则验证：
    ///   - MinValue/MaxValue（数值范围）
    ///   - AllowedValues（枚举值列表）
    ///   - RegexPattern（正则表达式）
    ///   - IsBoolean（布尔值验证）
    /// 
    /// 如果没有对应的描述符，默认通过验证（不认识的配置项不拦着）。
    /// </summary>
    /// <param name="key">配置项键名</param>
    /// <param name="configFileName">配置文件名</param>
    /// <param name="value">要验证的值</param>
    /// <returns>验证通过返回 true，否则返回 false</returns>
    public bool ValidateValue(string key, string configFileName, string value);

    /// <summary>
    /// 按分类分组配置项 📁
    /// 
    /// 返回按 Category 分组的字典，方便在 UI 中按类别展示配置项。
    /// 比如"网络"、"世界"、"玩家"等等。
    /// </summary>
    /// <param name="configFileName">配置文件名</param>
    /// <param name="config">当前配置的键值对（可选，用于标记已修改项）</param>
    /// <returns>按分类分组的描述符字典</returns>
    public Dictionary<string, List<ServerConfigDescriptor>> GroupByCategory(string configFileName, Dictionary<string, string>? config = null);

    /// <summary>获取翻译覆盖率报告</summary>
    ConfigDescriptorRegistry.CoverageReport GetCoverageReport();

    /// <summary>找出没有描述符的 key 列表</summary>
    List<string> FindUnmatchedKeys(List<string> keys, string configFileName);
}
