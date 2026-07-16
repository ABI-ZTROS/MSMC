// 📜 server.properties 格式解析器
// Minecraft 的配置文件格式简单得令人发指——key=value，连 JSON 都不如
// 但偏偏就是这种"朴素"的格式最容易出幺蛾子（比如值里带=号、中文乱码之类的）
namespace McServerGuard.Services.ConfigManagement;

using System.IO;
using System.Text;
using Serilog;

/// <summary>
/// Minecraft server.properties 文件解析器 🍋
/// 
/// 别看 server.properties 格式简单，它可是 Minecraft 服务器配置的"门面"。
/// 格式规则：
///   - 每行一个 key=value
///   - 以 # 开头的是注释（Minecraft 自己生成的注释还挺多的）
///   - 空行直接无视
///   - 键名不区分大小写（但我们保留原始大小写输出，做个体面的解析器）
/// </summary>
public static class PropertiesParser
{
    /// <summary>
    /// 解析 server.properties 格式的文本内容 📖
    /// </summary>
    /// <param name="content">配置文件的原始文本内容</param>
    /// <returns>键值对字典，不包含注释和空行</returns>
    /// <exception cref="ArgumentNullException">content 为 null 时抛出</exception>
    /// <exception cref="FormatException">遇到没有 = 的非空非注释行时抛出</exception>
    public static Dictionary<string, string> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        // 日志：解析入口
        Log.Debug("📄 PropertiesParser.Parse: 解析 {Len} 字符", content.Length);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(content);
        var line = reader.ReadLine();

        while (line is not null)
        {
            // 去掉首尾空白 —— 毕竟有些编辑器喜欢在行末加个空格什么的 😒
            var trimmed = line.Trim();

            // 空行？拜拜拜拜 👋
            if (trimmed.Length == 0)
            {
                line = reader.ReadLine();
                continue;
            }

            // 注释行？以 # 开头的统统忽略（包括那些 Mojang 写的"贴心"注释）
            if (trimmed[0] == '#')
            {
                line = reader.ReadLine();
                continue;
            }

            // 日志：每行解析
            Log.Debug("⏩ 解析行: {Line}", trimmed);

            // 找到第一个 = 号作为分隔符
            // 注意：value 里面可能包含 = 号（比如 motd 里可能有 = 符号）
            // 所以我们只认第一个 = 哦 ✂️
            var equalIndex = trimmed.IndexOf('=');
            if (equalIndex < 0)
            {
                // 日志：格式错误
                Log.Warning("⚠️ 格式错误: {Line}", trimmed);
                throw new FormatException(
                    $"无法解析的配置行（缺少 = 号）: \"{trimmed}\" —— 这行到底想表达什么？🤔");
            }

            var key = trimmed[..equalIndex].Trim();
            var value = trimmed[(equalIndex + 1)..].Trim();

            // key 为空那也太离谱了吧
            if (key.Length == 0)
            {
                // 日志：空键名
                Log.Warning("⚠️ 空键名: {Line}", trimmed);
                throw new FormatException(
                    "配置行的键名为空 —— = 号前面啥也没有，你是在写等号练习册吗？✏️");
            }

            result[key] = value;
            line = reader.ReadLine();
        }

        // 日志：解析完成
        Log.Debug("✅ 解析完成，共 {Count} 个键值对", result.Count);
        return result;
    }

    /// <summary>
    /// 将键值对字典序列化为 server.properties 格式 📝
    /// 
    /// 输出格式：每行 key=value，按字典序排列（强迫症友好）
    /// 不包含注释（因为注释不好保持，而且 Mojang 的注释每次启动都会重新生成）
    /// </summary>
    /// <param name="config">配置键值对</param>
    /// <returns>序列化后的文本</returns>
    /// <exception cref="ArgumentNullException">config 为 null 时抛出</exception>
    public static string Serialize(Dictionary<string, string> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // 日志：序列化入口
        Log.Debug("📝 PropertiesParser.Serialize: {Count} 个键值对", config.Count);

        var sb = new StringBuilder();

        // 按字母顺序排列，看着舒服 😌
        foreach (var kvp in config.OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"{kvp.Key}={kvp.Value}");
        }

        return sb.ToString();
    }
}
