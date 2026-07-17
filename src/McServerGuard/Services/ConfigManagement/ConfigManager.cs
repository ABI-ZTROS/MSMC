// -----------------------------------------------------------------------------
// 文件名: ConfigManager.cs
// 命名空间: McServerGuard.Services.ConfigManagement
// 功能描述: 多格式配置管理编排器，实现Properties/YAML/JSON配置文件的统一读写、扁平化键值对转换、描述符验证及分类分组，对外提供格式无关的配置操作契约
// 依赖组件: System.IO, System.Text.Json, Serilog, McServerGuard.Models.Config
// 设计模式: 策略模式（多格式解析/序列化）、适配器模式（格式统一适配）、注册表模式（描述符管理）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ConfigManagement;

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Serilog;

/// <summary>
/// 配置管理器 —— 多格式配置文件的统一读写与验证编排器
/// </summary>
/// <remarks>
/// 核心架构设计：
/// <list type="number">
/// <item>内部采用扁平化键值对模型（key=value），屏蔽原始格式的结构差异</item>
/// <item>YAML/JSON嵌套结构在读入时压平为"a.b.c"路径式键，写出时还原为嵌套结构</item>
/// <item>.properties格式原生扁平，直接映射</item>
/// <item>配置项验证统一通过<see cref="ConfigDescriptorRegistry"/>的约束描述符执行</item>
/// </list>
/// </remarks>
public sealed class ConfigManager : IConfigManager
{
    /// <summary>
    /// 配置描述符注册表 —— 存储各配置项的元数据与验证规则
    /// </summary>
    private readonly ConfigDescriptorRegistry _registry;

    /// <summary>
    /// 初始化配置管理器实例
    /// </summary>
    /// <param name="registry">配置描述符注册表实例</param>
    /// <exception cref="ArgumentNullException">当registry为<c>null</c>时抛出</exception>
    public ConfigManager(ConfigDescriptorRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Log.Information("⚙️ ConfigManager 初始化，注册表已加载 {Count} 个描述符",
            registry.GetDescriptorsForFile("server.properties").Count);
    }

    /// <summary>
    /// 读取配置文件，根据扩展名与内容检测自动选择解析器，返回扁平化键值对字典
    /// </summary>
    /// <param name="filePath">配置文件路径</param>
    /// <returns>扁平化的键值对字典</returns>
    /// <exception cref="ArgumentNullException">filePath为<c>null</c>时抛出</exception>
    /// <exception cref="FileNotFoundException">配置文件不存在时抛出</exception>
    /// <exception cref="NotSupportedException">不支持的文件格式时抛出</exception>
    public async Task<Dictionary<string, string>> ReadConfigAsync(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        Log.Information("📂 读取配置文件: {Path}", filePath);

        if (!File.Exists(filePath))
        {
            Log.Warning("❌ 配置文件不存在: {Path}", filePath);
            throw new FileNotFoundException($"配置文件不存在: {filePath}", filePath);
        }

        // 异步读取文件内容
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

        Log.Information("✅ 配置解析完成，共 {Count} 个键值对", result.Count);

        return result;
    }

    /// <summary>
    /// 将配置写回文件，根据扩展名自动选择序列化器
    /// </summary>
    /// <param name="filePath">目标文件路径</param>
    /// <param name="config">扁平化键值对配置数据</param>
    /// <returns>异步任务</returns>
    /// <exception cref="ArgumentNullException">参数为<c>null</c>时抛出</exception>
    /// <exception cref="NotSupportedException">不支持的文件格式时抛出</exception>
    public async Task SaveConfigAsync(string filePath, Dictionary<string, string> config)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(config);

        Log.Information("💾 保存配置到: {Path}", filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        // 保存时优先使用扩展名判断（写文件时内容尚未生成，无法进行内容检测）
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

        // 确保目标目录存在
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
    /// 获取配置项的中文描述符
    /// </summary>
    /// <param name="key">配置项键名</param>
    /// <param name="configFileName">所属配置文件名</param>
    /// <returns>配置描述符；未找到返回<c>null</c></returns>
    public ServerConfigDescriptor? GetDescriptor(string key, string configFileName)
    {
        Log.Debug("🔍 查询描述符: Key={Key} File={File}", key, configFileName);
        return _registry.GetDescriptor(key, configFileName);
    }

    /// <summary>
    /// 验证配置值的合法性 —— 综合检查布尔类型、枚举值、正则表达式及数值范围等约束
    /// </summary>
    /// <param name="key">配置项键名</param>
    /// <param name="configFileName">所属配置文件名</param>
    /// <param name="value">待验证的配置值</param>
    /// <returns>验证通过返回<c>true</c>，失败返回<c>false</c></returns>
    /// <remarks>
    /// 验证执行顺序：
    /// <list type="number">
    /// <item>无描述符 → 放行（未注册的配置项不拦截）</item>
    /// <item>布尔类型 → 检查是否为true/false</item>
    /// <item>枚举值 → 检查是否在允许值列表中</item>
    /// <item>正则表达式 → 检查是否匹配模式</item>
    /// <item>数值范围 → 检查是否在MinValue/MaxValue区间内</item>
    /// </list>
    /// </remarks>
    public bool ValidateValue(string key, string configFileName, string value)
    {
        Log.Debug("🔍 验证值: Key={Key} Value={Value}", key, value);
        var descriptor = _registry.GetDescriptor(key, configFileName);
        if (descriptor is null)
        {
            // 未注册的配置项，默认放行
            return true;
        }

        // 布尔值验证
        if (descriptor.ValueType == "bool")
        {
            if (!bool.TryParse(value, out _))
            {
                Log.Debug("布尔值验证失败: {Key}={Value} —— 这看起来不像 true 或 false 🤔",
                    key, value);
                return false;
            }
        }

        // 枚举值验证
        if (descriptor.AllowedValues is { Length: > 0 })
        {
            if (!descriptor.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                Log.Debug("枚举值验证失败: {Key}={Value}，允许的值: {Allowed} —— 选错了哦 📋",
                    key, value, string.Join(", ", descriptor.AllowedValues));
                return false;
            }
        }

        // 正则表达式验证
        var regex = descriptor.GetCompiledRegex();
        if (regex is not null && !regex.IsMatch(value))
        {
            Log.Debug("正则验证失败: {Key}={Value}，模式: {Pattern} —— 格式不对 📐",
                key, value, descriptor.RegexPattern);
            return false;
        }

        // 数值范围验证
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
    /// 按分类分组配置项描述符
    /// </summary>
    /// <param name="configFileName">配置文件名</param>
    /// <param name="config">可选的当前配置字典（预留参数，用于UI高亮已修改项）</param>
    /// <returns>按分类分组的描述符字典</returns>
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

    /// <summary>
    /// 获取描述符覆盖率报告
    /// </summary>
    /// <returns>覆盖率报告对象</returns>
    public ConfigDescriptorRegistry.CoverageReport GetCoverageReport()
        => _registry.GetCoverageReport();

    /// <summary>
    /// 找出没有对应描述符的配置键列表
    /// </summary>
    /// <param name="keys">配置键集合</param>
    /// <param name="configFileName">配置文件名</param>
    /// <returns>未匹配的键列表</returns>
    public List<string> FindUnmatchedKeys(List<string> keys, string configFileName)
        => _registry.FindUnmatchedKeys(keys, configFileName);

    // ==================== 私有辅助方法 ====================

    /// <summary>
    /// 处理不支持的配置文件格式 —— 记录警告并抛出异常
    /// </summary>
    /// <param name="extension">文件扩展名</param>
    /// <returns>不支持的格式异常对象</returns>
    private static NotSupportedException HandleUnsupportedFormat(string extension)
    {
        Log.Warning("❌ 无法识别的配置文件格式: 扩展名={Ext}", extension);
        return new NotSupportedException(
            $"无法识别的配置文件格式（扩展名: {extension}）。" +
            "支持的格式: .properties / .yml / .yaml / .json 🙅");
    }

    /// <summary>
    /// 解析.properties格式配置文件
    /// </summary>
    /// <param name="content">文件内容</param>
    /// <returns>扁平化键值对字典</returns>
    private static Dictionary<string, string> ParseProperties(string content)
    {
        return PropertiesParser.Parse(content);
    }

    /// <summary>
    /// 解析YAML并压平为点分隔的扁平化键值对格式
    /// </summary>
    /// <param name="content">YAML内容</param>
    /// <returns>扁平化键值对字典</returns>
    /// <example>
    /// 输入YAML：
    /// <code>
    /// world-settings:
    ///   default:
    ///     seed: 123
    /// </code>
    /// 输出：["world-settings.default.seed" = "123"]
    /// </example>
    private static Dictionary<string, string> FlattenYaml(string content)
    {
        var nested = YamlParser.Parse(content);
        return FlattenNestedDictionary(nested);
    }

    /// <summary>
    /// 解析JSON并压平为点分隔的扁平化键值对格式
    /// </summary>
    /// <param name="content">JSON内容</param>
    /// <returns>扁平化键值对字典</returns>
    /// <remarks>
    /// 采用<see cref="JsonNode"/>进行解析，避免System.Text.Json将object?反序列化为JsonElement的类型转换问题。
    /// </remarks>
    private static Dictionary<string, string> FlattenJson(string content)
    {
        var node = JsonNode.Parse(content);
        if (node is not JsonObject obj)
            return [];

        return FlattenJsonObject(obj);
    }

    /// <summary>
    /// 将嵌套字典递归压平为"a.b.c" = "value"的扁平化格式
    /// </summary>
    /// <param name="data">嵌套字典数据</param>
    /// <param name="prefix">当前键前缀（用于递归拼接）</param>
    /// <returns>扁平化键值对字典</returns>
    /// <remarks>
    /// 核心压平算法：
    /// <list type="bullet">
    /// <item>叶子节点直接转为字符串</item>
    /// <item>嵌套字典递归压平，键名用点号连接</item>
    /// <item>复杂对象数组序列化为YAML字符串后作为叶子值</item>
    /// <item>简单数组直接ToString</item>
    /// </list>
    /// 注意：兼容YamlDotNet反序列化的Dictionary&lt;object, object?&gt;非泛型字典。
    /// </remarks>
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
    /// </summary>
    /// <param name="value">待压平的值</param>
    /// <param name="fullKey">完整键名</param>
    /// <param name="result">结果字典（引用传递，直接写入）</param>
    /// <remarks>
    /// 兼容YamlDotNet返回的非泛型字典（<see cref="System.Collections.IDictionary"/>）。
    /// </remarks>
    private static void FlattenValue(object? value, string fullKey, Dictionary<string, string> result)
    {
        if (value is null)
        {
            result[fullKey] = string.Empty;
            return;
        }

        // 字典类型（兼容string键和object键）
        if (value is System.Collections.IDictionary dict)
        {
            // 嵌套字典，递归压平
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                var entryKey = entry.Key?.ToString() ?? string.Empty;
                var nestedKey = $"{fullKey}.{entryKey}";
                FlattenValue(entry.Value, nestedKey, result);
            }
            return;
        }

        // 列表/数组类型
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
                // 复杂对象数组，序列化为YAML字符串作为叶子值
                result[fullKey] = YamlParser.Serialize(
                    new Dictionary<string, object?> { { "value", value } });
            }
            else
            {
                // 简单数组，直接ToString（空数组也走此分支）
                result[fullKey] = value.ToString() ?? string.Empty;
            }
            return;
        }

        // 简单值，直接转为字符串
        result[fullKey] = value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 将JsonObject递归压平为"a.b.c" = "value"的扁平化格式
    /// </summary>
    /// <param name="obj">JSON对象节点</param>
    /// <param name="prefix">当前键前缀</param>
    /// <returns>扁平化键值对字典</returns>
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
                // 嵌套对象，递归压平
                var flattened = FlattenJsonObject(nestedObj, fullKey);
                foreach (var nestedKvp in flattened)
                {
                    result[nestedKvp.Key] = nestedKvp.Value;
                }
            }
            else if (kvp.Value is JsonArray arr)
            {
                // 数组直接序列化为JSON字符串
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
    /// 将扁平化键值对还原为YAML嵌套结构并序列化
    /// </summary>
    /// <param name="config">扁平化键值对配置</param>
    /// <returns>YAML格式字符串</returns>
    private static string SerializeYaml(Dictionary<string, string> config)
    {
        var nested = UnflattenDictionary(config);
        return YamlParser.Serialize(nested);
    }

    /// <summary>
    /// 将扁平化键值对还原为JSON嵌套结构并序列化
    /// </summary>
    /// <param name="config">扁平化键值对配置</param>
    /// <returns>JSON格式字符串</returns>
    private static string SerializeJson(Dictionary<string, string> config)
    {
        var nested = UnflattenDictionary(config);
        return JsonSerializer.Serialize(nested, new JsonSerializerOptions
        {
            WriteIndented = true, // 缩进美化，便于人工阅读
        });
    }

    /// <summary>
    /// 将扁平化的"a.b.c" = "value"还原为嵌套字典结构
    /// </summary>
    /// <param name="config">扁平化键值对配置</param>
    /// <returns>嵌套字典结构</returns>
    /// <remarks>
    /// 与<see cref="FlattenNestedDictionary"/>互为逆操作。
    /// 对于非点号分隔的键（如server.properties的"server-port"），直接作为顶层键保留。
    /// </remarks>
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
                    // 已有非字典值，强制覆盖为字典（数据可能丢失）
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
