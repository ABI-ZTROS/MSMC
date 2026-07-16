// 📄 YAML 解析器 —— 基于 YamlDotNet
// YAML 格式"号称"人类可读，但缩进搞错了能让人抓狂到原地升天 💀
// 不过 Minecraft 生态里 Spigot/Paper/Bukkit 全是 YAML，躲不掉的
namespace McServerGuard.Services.ConfigManagement;

using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// YAML 配置解析器，基于 YamlDotNet 库 🧶
/// 
/// 专门对付 Minecraft 服务器生态里满天飞的 YAML 配置文件：
///   - spigot.yml
///   - bukkit.yml
///   - paper-global.yml / paper-world-defaults.yml
///   - config/paper-global.yml
///   ...还有一堆叫不出名字的 YAML
/// 
/// 支持 dot-path 风格的嵌套路径访问，比如 "world-settings.default.seed"
/// </summary>
public static class YamlParser
{
    // 反序列化器 —— 读 YAML 用
    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance) // YAML 里喜欢用下划线命名
        .IgnoreUnmatchedProperties()                                 // 多余的字段不要报错，Minecraft 配置文件经常有陌生字段
        .Build();

    // 序列化器 —— 写 YAML 用
    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults) // 默认值不写，保持文件简洁
        .Build();

    /// <summary>
    /// 解析 YAML 字符串为字典 📖
    /// 
    /// YAML 的层次结构会被映射为嵌套的 Dictionary，叶子节点是 string/int/bool 等基础类型。
    /// </summary>
    /// <param name="content">YAML 格式的文本内容</param>
    /// <returns>嵌套字典结构</returns>
    /// <exception cref="ArgumentNullException">content 为 null 时抛出</exception>
    /// <exception cref="YamlDotNet.Core.YamlException">YAML 格式有误时抛出</exception>
    public static Dictionary<string, object?> Parse(string content)
    {
        // 日志：解析入口
        Log.Debug("📄 YamlParser.Parse: 解析 {Len} 字符", content.Length);
        ArgumentNullException.ThrowIfNull(content);

        return _deserializer.Deserialize<Dictionary<string, object?>>(content)
               ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// 将字典序列化为 YAML 字符串 📝
    /// 
    /// 输出会比较"标准"（YAML 的标准可太多了……），但保证 YamlDotNet 能正确读回来。
    /// </summary>
    /// <param name="data">嵌套字典数据</param>
    /// <returns>YAML 格式文本</returns>
    /// <exception cref="ArgumentNullException">data 为 null 时抛出</exception>
    public static string Serialize(Dictionary<string, object?> data)
    {
        // 日志：序列化入口
        Log.Debug("📝 YamlParser.Serialize: {Count} 个顶级键", data.Count);
        ArgumentNullException.ThrowIfNull(data);

        return _serializer.Serialize(data);
    }

    /// <summary>
    /// 通过 dot-path 获取嵌套值 🔍
    /// 
    /// 比如GetValue(dict, "world-settings.default.seed") 会沿着字典一层层找下去。
    /// 路径不存在就返回 null，不会炸给你看（温和派）🕊️
    /// </summary>
    /// <param name="data">YAML 解析后的嵌套字典</param>
    /// <param name="path">dot 分隔的路径，如 "a.b.c"</param>
    /// <returns>找到的值，没找到返回 null</returns>
    /// <exception cref="ArgumentNullException">data 或 path 为 null 时抛出</exception>
    public static object? GetValue(Dictionary<string, object?> data, string path)
    {
        // 日志：获取值入口
        Log.Debug("🔍 YamlParser.GetValue: 路径={Path}", path);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length == 0)
            return null;

        var segments = path.Split('.');
        object? current = data;

        foreach (var segment in segments)
        {
            if (current is not Dictionary<string, object?> dict)
                return null; // 走到非字典节点了，此路不通 🚫

            if (!dict.TryGetValue(segment, out current))
                return null; // 这个 key 不存在，放弃 🏳️
        }

        return current;
    }

    /// <summary>
    /// 通过 dot-path 设置嵌套值 ✏️
    /// 
    /// 如果中间的字典节点不存在会自动创建（贴心吧？💖）
    /// 如果中间遇到了非字典节点……那对不起了，直接覆盖成新字典
    /// </summary>
    /// <param name="data">YAML 解析后的嵌套字典</param>
    /// <param name="path">dot 分隔的路径，如 "a.b.c"</param>
    /// <param name="value">要设置的值</param>
    /// <exception cref="ArgumentNullException">data 或 path 为 null 时抛出</exception>
    public static void SetValue(Dictionary<string, object?> data, string path, object value)
    {
        // 日志：设置值入口
        Log.Debug("✏️ YamlParser.SetValue: 路径={Path} 值={Value}", path, value);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(path);

        if (path.Length == 0)
        {
            Log.Warning("⚠️ 路径为空: {Path}", path);
            throw new ArgumentException("路径不能为空字符串 —— 你想设置什么呢？空气吗？🌬️", nameof(path));
        }

        var segments = path.Split('.');
        var current = data;

        // 沿着路径走到底，中间不存在的字典自动创建
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];

            if (!current.TryGetValue(segment, out var next))
            {
                // 不存在？那就造一个！🔧
                var newDict = new Dictionary<string, object?>();
                current[segment] = newDict;
                current = newDict;
            }
            else if (next is Dictionary<string, object?> nextDict)
            {
                current = nextDict;
            }
            else
            {
                // 中间节点不是字典？强行覆盖成字典（简单粗暴）
                var newDict = new Dictionary<string, object?>();
                current[segment] = newDict;
                current = newDict;
            }
        }

        // 最后一个节点设置为值
        current[segments[^1]] = value;
    }
}
