// -----------------------------------------------------------------------------
// 文件名: PropertiesParser.cs
// 命名空间: McServerGuard.Services.ConfigManagement
// 功能描述: Java Properties 格式解析器，实现 server.properties 的双向序列化
// 依赖组件: System.IO, System.Text, System.Collections.Generic, Serilog
// 设计模式: 解析器模式、静态工具类模式
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ConfigManagement;

using System.IO;
using System.Text;
using Serilog;

/// <summary>
/// Minecraft server.properties 格式解析器
/// </summary>
/// <remarks>
/// <para>实现 Java Properties 文件格式的解析与序列化。server.properties 作为
/// Minecraft 服务器的核心配置文件，采用简单的 key=value 行式结构。</para>
/// <para>格式规则：
///   - 每行一个 key=value 配置项
///   - 以 # 开头的行为注释行（解析时忽略）
///   - 空白行直接跳过
///   - 键名大小写不敏感（解析输出保留原始大小写）
///   - 分隔符取首个 = 号，值中允许包含 = 字符（如 motd 字段）
/// </para>
/// </remarks>
public static class PropertiesParser
{
    /// <summary>
    /// 解析 server.properties 格式的文本内容
    /// </summary>
    /// <param name="content">配置文件的原始文本内容</param>
    /// <returns>键值对字典，不包含注释行与空白行</returns>
    /// <exception cref="ArgumentNullException">当 content 为 null 时抛出</exception>
    /// <exception cref="FormatException">当遇到缺少 = 号的非空非注释行时抛出</exception>
    public static Dictionary<string, string> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Log.Debug("PropertiesParser.Parse: 解析 {Len} 字符", content.Length);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(content);
        var line = reader.ReadLine();

        while (line is not null)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                line = reader.ReadLine();
                continue;
            }

            if (trimmed[0] == '#')
            {
                line = reader.ReadLine();
                continue;
            }

            Log.Debug("解析行: {Line}", trimmed);

            var equalIndex = trimmed.IndexOf('=');
            if (equalIndex < 0)
            {
                Log.Warning("格式错误: {Line}", trimmed);
                throw new FormatException(
                    $"无法解析的配置行（缺少 = 号）: \"{trimmed}\" —— 这行到底想表达什么？");
            }

            var key = trimmed[..equalIndex].Trim();
            var value = trimmed[(equalIndex + 1)..].Trim();

            if (key.Length == 0)
            {
                Log.Warning("空键名: {Line}", trimmed);
                throw new FormatException(
                    "配置行的键名为空 —— = 号前面啥也没有，你是在写等号练习册吗？");
            }

            result[key] = value;
            line = reader.ReadLine();
        }

        Log.Debug("解析完成，共 {Count} 个键值对", result.Count);
        return result;
    }

    /// <summary>
    /// 将键值对字典序列化为 server.properties 格式文本
    /// </summary>
    /// <param name="config">配置键值对字典</param>
    /// <returns>序列化后的 Properties 格式文本</returns>
    /// <exception cref="ArgumentNullException">当 config 为 null 时抛出</exception>
    /// <remarks>
    /// 输出格式：每行 key=value，按键名字母顺序（不区分大小写）排序。
    /// 不输出注释行，因为 Mojang 在服务器启动时会重新生成默认注释。
    /// </remarks>
    public static string Serialize(Dictionary<string, string> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Log.Debug("PropertiesParser.Serialize: {Count} 个键值对", config.Count);

        var sb = new StringBuilder();

        foreach (var kvp in config.OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"{kvp.Key}={kvp.Value}");
        }

        return sb.ToString();
    }
}
