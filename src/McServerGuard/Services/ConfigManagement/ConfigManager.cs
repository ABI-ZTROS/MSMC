// ⚙️ 配置管理器实现 —— 统一调度各种格式的解析/序列化
// 这个类就像一个"翻译官"：把各种奇怪的配置格式翻译成统一的键值对，
// 再把修改后的键值对翻译回原始格式写回去 🔄
namespace McServerGuard.Services.ConfigManagement;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog;

/// <summary>
/// 配置管理器的具体实现 🏗️
/// 
/// 核心设计思路：
///   - 内部使用"扁平化"的 key=value 模型，不管原始格式是嵌套的还是扁平的
///   - YAML/JSON 的嵌套结构在读入时被压平（如 "world-settings.seed"），
///     写出时再还原回嵌套结构
///   - .properties 天生就是扁平的，直接用
///   - 验证统一走 DescriptorRegistry 的约束
/// </summary>
public sealed class ConfigManager : IConfigManager
{
    private readonly ConfigDescriptorRegistry _registry;

    /// <summary>
    /// 构造函数 —— 注入注册表 🧃
    /// </summary>
    public ConfigManager(ConfigDescriptorRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        // 构造函数中记录注册表已加载的描述符数量
        Log.Information("⚙️ ConfigManager 初始化，注册表已加载 {Count} 个描述符",
            registry.GetDescriptorsForFile("server.properties").Count);
    }

    /// <summary>
    /// 读取配置文件，根据扩展名自动选择解析器 📖
    /// 返回统一的扁平化键值对字典
    /// </summary>
    public async Task<Dictionary<string, string>> ReadConfigAsync(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        // 日志：读取配置文件入口
        Log.Information("📂 读取配置文件: {Path}", filePath);

        if (!File.Exists(filePath))
        {
            Log.Warning("❌ 配置文件不存在: {Path}", filePath);
            throw new FileNotFoundException($"配置文件不存在: {filePath}", filePath);
        }

        // 异步读取文件内容（避免阻塞线程，虽然对配置文件来说影响不大 😏）
        var content = await File.ReadAllTextAsync(filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var format = ConfigFormatDetector.Resolve(content, extension);

        var result = format switch
        {
            ConfigFormat.Properties => ParseProperties(content),
            ConfigFormat.Yaml => FlattenYaml(content),
            ConfigFormat.Json => FlattenJson(content),
            _ => throw HandleUnsupportedFormat(extension)
        };

        // 日志：解析完成
        Log.Information("✅ 配置解析完成，共 {Count} 个键值对", result.Count);

        return result;
    }

    /// <summary>
    /// 将配置写回文件，根据扩展名自动选择序列化器 💾
    /// </summary>
    public async Task SaveConfigAsync(string filePath, Dictionary<string, string> config)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(config);

        // 日志：保存配置入口
        Log.Information("💾 保存配置到: {Path}", filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        // 保存时优先用扩展名判断（写文件时内容尚未生成，无法做内容检测）
        var format = ConfigFormatDetector.DetectByExtension(extension);

        string content;
        switch (format)
        {
            case ConfigFormat.Properties:
                content = PropertiesParser.Serialize(config);
                break;

            case ConfigFormat.Yaml:
                content = SerializeYaml(config);
                break;

            case ConfigFormat.Json:
                content = SerializeJson(config);
                break;

            default:
                Log.Warning("❌ 不支持的配置文件格式: {Ext}", extension);
                throw new NotSupportedException(
                    $"不支持的配置文件格式: {extension} —— 我不会写这种格式啦 🙅");
        }

        // 确保目标目录存在（不然写到空气里去了）
        var directory = Path.GetDirectoryName(filePath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, content);

        Log.Information("配置文件 {FilePath} 已保存，共 {Count} 个配置项 ✅",
            filePath, config.Count);
    }

    /// <summary>
    /// 获取配置项的中文描述符 🏷️
    /// </summary>
    public ServerConfigDescriptor? GetDescriptor(string key, string configFileName)
    {
        // 日志：查询描述符
        Log.Debug("🔍 查询描述符: Key={Key} File={File}", key, configFileName);
        return _registry.GetDescriptor(key, configFileName);
    }

    /// <summary>
    /// 验证配置值 —— 综合检查范围、枚举、正则等约束 🔍
    /// 
    /// 验证逻辑：
    ///   1. 没有描述符？放行（不认识的配置不拦）
    ///   2. IsBoolean？检查是否为 true/false
    ///   3. AllowedValues？检查是否在枚举列表里
    ///   4. RegexPattern？检查是否匹配正则
    ///   5. MinValue/MaxValue？检查数值是否在范围内
    /// </summary>
    public bool ValidateValue(string key, string configFileName, string value)
    {
        // 日志：验证值入口
        Log.Debug("🔍 验证值: Key={Key} Value={Value}", key, value);
        var descriptor = _registry.GetDescriptor(key, configFileName);
        if (descriptor is null)
        {
            // 没注册的配置项，默认放行（宽容一点 🕊️）
            return true;
        }

        // 布尔值检查
        if (descriptor.ValueType == "bool")
        {
            if (!bool.TryParse(value, out _))
            {
                Log.Debug("布尔值验证失败: {Key}={Value} —— 这看起来不像 true 或 false 🤔",
                    key, value);
                return false;
            }
        }

        // 枚举值检查
        if (descriptor.AllowedValues is { Length: > 0 })
        {
            if (!descriptor.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                Log.Debug("枚举值验证失败: {Key}={Value}，允许的值: {Allowed} —— 选错了哦 📋",
                    key, value, string.Join(", ", descriptor.AllowedValues));
                return false;
            }
        }

        // 正则表达式检查
        var regex = descriptor.GetCompiledRegex();
        if (regex is not null && !regex.IsMatch(value))
        {
            Log.Debug("正则验证失败: {Key}={Value}，模式: {Pattern} —— 格式不对 📐",
                key, value, descriptor.RegexPattern);
            return false;
        }

        // 数值范围检查
        if (descriptor.MinValue.HasValue || descriptor.MaxValue.HasValue)
        {
            if (!int.TryParse(value, out var numValue))
            {
                Log.Debug("数值范围验证失败: {Key}={Value} —— 这不是数字呀 🧮", key, value);
                return false;
            }

            if (numValue < descriptor.MinValue)
            {
                Log.Debug("数值太小: {Key}={Value}，最小值: {Min} —— 再小就没了 📉",
                    key, value, descriptor.MinValue);
                return false;
            }

            if (numValue > descriptor.MaxValue)
            {
                Log.Debug("数值太大: {Key}={Value}，最大值: {Max} —— 太贪心了吧 📈",
                    key, value, descriptor.MaxValue);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 按分类分组配置项 📂
    /// 
    /// 将描述符按 Category 分组，返回一个"分类 -> 描述符列表"的字典。
    /// 如果传入了 config 参数，还可以知道哪些值被修改了（方便 UI 高亮显示）。
    /// </summary>
    public Dictionary<string, List<ServerConfigDescriptor>> GroupByCategory(
        string configFileName,
        Dictionary<string, string>? config = null)
    {
        var descriptors = _registry.GetDescriptorsForFile(configFileName);

        return descriptors
            .GroupBy(static d => d.Category)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(static d => d.DisplayName).ToList());
    }

    /// <summary>获取翻译覆盖率报告</summary>
    public ConfigDescriptorRegistry.CoverageReport GetCoverageReport()
        => _registry.GetCoverageReport();

    /// <summary>找出没有描述符的 key 列表</summary>
    public List<string> FindUnmatchedKeys(List<string> keys, string configFileName)
        => _registry.FindUnmatchedKeys(keys, configFileName);

    // ==================== 私有辅助方法 ====================

    /// <summary>
    /// 处理不支持的配置文件格式 —— 记录警告并抛出异常
    /// </summary>
    private static NotSupportedException HandleUnsupportedFormat(string extension)
    {
        Log.Warning("❌ 无法识别的配置文件格式: 扩展名={Ext}", extension);
        return new NotSupportedException(
            $"无法识别的配置文件格式（扩展名: {extension}）。" +
            "支持的格式: .properties / .yml / .yaml / .json 🙅");
    }

    /// <summary>
    /// 解析 .properties 格式 🍋
    /// </summary>
    private static Dictionary<string, string> ParseProperties(string content)
    {
        return PropertiesParser.Parse(content);
    }

    /// <summary>
    /// 解析 YAML 并压平为 key.path 格式 📄
    /// 
    /// 比如这样的 YAML：
    ///   world-settings:
    ///     default:
    ///       seed: 123
    /// 会被压平为：
    ///   "world-settings.default.seed" = "123"
    /// </summary>
    private static Dictionary<string, string> FlattenYaml(string content)
    {
        var nested = YamlParser.Parse(content);
        return FlattenNestedDictionary(nested);
    }

    /// <summary>
    /// 解析 JSON 并压平为 key.path 格式 📋
    /// 
    /// 和 FlattenYaml 一样的思路，只是输入格式不同。
    /// 使用 JsonNode 来解析，避免 System.Text.Json 把 object? 反序列化成 JsonElement 的坑 😵‍💫
    /// </summary>
    private static Dictionary<string, string> FlattenJson(string content)
    {
        var node = JsonNode.Parse(content);
        if (node is not JsonObject obj)
            return [];

        return FlattenJsonObject(obj);
    }

    /// <summary>
    /// 将嵌套字典递归压平为 "a.b.c" = "value" 格式 🔨
    /// 
    /// 这是核心辅助方法，用于 YAML 解析后的 Dictionary。
    /// 叶子节点会被转为字符串（因为最终要统一为 string 类型的键值对）。
    /// 对于嵌套字典/列表，会先序列化为 YAML 字符串再作为叶子值。
    /// 
    /// 注意：YamlDotNet 反序列化的嵌套字典可能是 Dictionary&lt;object, object?&gt;，
    /// 需要兼容处理，不能只检查 Dictionary&lt;string, object?&gt;。
    /// </summary>
    private static Dictionary<string, string> FlattenNestedDictionary(
        Dictionary<string, object?> data,
        string prefix = "")
    {
        var result = new Dictionary<string, string>();

        foreach (var kvp in data)
        {
            var fullKey = string.IsNullOrEmpty(prefix)
                ? kvp.Key
                : $"{prefix}.{kvp.Key}";

            FlattenValue(kvp.Value, fullKey, result);
        }

        return result;
    }

    /// <summary>
    /// 压平单个值 —— 递归处理嵌套字典、列表和简单值
    /// 兼容 YamlDotNet 返回的 Dictionary&lt;object, object?&gt; 等非泛型字典
    /// </summary>
    private static void FlattenValue(object? value, string fullKey, Dictionary<string, string> result)
    {
        if (value is null)
        {
            result[fullKey] = string.Empty;
            return;
        }

        // 检查是否为字典类型（兼容 string 键和 object 键）
        if (value is System.Collections.IDictionary dict)
        {
            // 嵌套字典，递归压平 🪃
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                var entryKey = entry.Key?.ToString() ?? string.Empty;
                var nestedKey = $"{fullKey}.{entryKey}";
                FlattenValue(entry.Value, nestedKey, result);
            }
            return;
        }

        // 检查是否为列表/数组
        if (value is System.Collections.IList list)
        {
            // 判断是否为复杂对象数组（非简单类型）
            bool isComplexList = list.Count > 0 &&
                list[0] is not string &&
                list[0] is not int &&
                list[0] is not long &&
                list[0] is not double &&
                list[0] is not bool;

            if (isComplexList)
            {
                // 复杂对象数组，序列化为 YAML 字符串作为叶子值
                result[fullKey] = YamlParser.Serialize(
                    new Dictionary<string, object?> { { "value", value } });
            }
            else
            {
                // 简单数组，直接 ToString（对于空数组也走这里）
                result[fullKey] = value.ToString() ?? string.Empty;
            }
            return;
        }

        // 简单值，直接转为字符串
        result[fullKey] = value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 将 JsonObject 递归压平为 "a.b.c" = "value" 格式 🔨
    /// 
    /// 这是 JSON 专用的压平方法，因为 JsonNode 解析出来的嵌套结构是 JsonObject，
    /// 不是 Dictionary，需要单独处理 🙄
    /// </summary>
    private static Dictionary<string, string> FlattenJsonObject(
        JsonObject obj,
        string prefix = "")
    {
        var result = new Dictionary<string, string>();

        foreach (var kvp in obj)
        {
            var fullKey = string.IsNullOrEmpty(prefix)
                ? kvp.Key
                : $"{prefix}.{kvp.Key}";

            if (kvp.Value is JsonObject nestedObj)
            {
                // 嵌套对象，递归压平 🪃
                var flattened = FlattenJsonObject(nestedObj, fullKey);
                foreach (var nestedKvp in flattened)
                {
                    result[nestedKvp.Key] = nestedKvp.Value;
                }
            }
            else if (kvp.Value is JsonArray arr)
            {
                // 数组直接 ToString 序列化（简单粗暴但够用）📦
                result[fullKey] = arr.ToJsonString();
            }
            else
            {
                // 叶子节点，转为字符串
                result[fullKey] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        return result;
    }

    /// <summary>
    /// 将扁平化的键值对还原为 YAML 并序列化 📝
    /// 
    /// 需要把 "world-settings.default.seed" = "123" 还原为嵌套结构再交给 YamlDotNet。
    /// </summary>
    private static string SerializeYaml(Dictionary<string, string> config)
    {
        var nested = UnflattenDictionary(config);
        return YamlParser.Serialize(nested);
    }

    /// <summary>
    /// 将扁平化的键值对还原为 JSON 并序列化 📝
    /// </summary>
    private static string SerializeJson(Dictionary<string, string> config)
    {
        var nested = UnflattenDictionary(config);
        return JsonSerializer.Serialize(nested, new JsonSerializerOptions
        {
            WriteIndented = true, // 缩进美化，方便人类阅读 👀
        });
    }

    /// <summary>
    /// 将扁平化的 "a.b.c" = "value" 还原为嵌套字典结构 🔧
    /// 
    /// 和 FlattenNestedDictionary 正好是逆操作。
    /// 所有值都是字符串（因为输入就是 string）。
    /// 
    /// 对于非点号分隔的键（如 server.properties 的 "server-port"），
    /// 会直接作为顶层键保留。
    /// </summary>
    private static Dictionary<string, object?> UnflattenDictionary(Dictionary<string, string> config)
    {
        var root = new Dictionary<string, object?>();

        foreach (var kvp in config)
        {
            var segments = kvp.Key.Split('.');
            var current = root;

            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!current.TryGetValue(segments[i], out var next))
                {
                    var newDict = new Dictionary<string, object?>();
                    current[segments[i]] = newDict;
                    current = newDict;
                }
                else if (next is Dictionary<string, object?> nextDict)
                {
                    current = nextDict;
                }
                else
                {
                    // 已有非字典值？强行覆盖（覆盖警告：数据可能丢失 ⚠️）
                    var newDict = new Dictionary<string, object?>();
                    current[segments[i]] = newDict;
                    current = newDict;
                }
            }

            current[segments[^1]] = kvp.Value;
        }

        return root;
    }
}
