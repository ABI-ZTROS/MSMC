// 🔍 配置文件格式探测器 —— 当扩展名不靠谱时，靠内容特征来判断
namespace McServerGuard.Services.ConfigManagement;

using System.Text.Json;

/// <summary>
/// 配置文件格式枚举
/// </summary>
public enum ConfigFormat
{
    Unknown,
    Properties,
    Yaml,
    Json
}

/// <summary>
/// 基于内容的配置文件格式探测器 🔍
/// 当扩展名无法确定格式时（如 .conf），通过分析文件内容特征来判断
/// </summary>
public static class ConfigFormatDetector
{
    /// <summary>
    /// 通过分析内容特征检测格式
    /// 判断优先级：JSON → YAML → Properties → Unknown
    /// </summary>
    public static ConfigFormat Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ConfigFormat.Unknown;

        var trimmed = TrimBomAndWhitespace(content);

        // JSON：以 { 或 [ 开头
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return ConfigFormat.Json;

        // YAML 特征：有缩进的 "key: value" 行（冒号后有空格或冒号在行尾）
        // 或含 "---" 文档分隔符
        if (HasYamlFeatures(content))
            return ConfigFormat.Yaml;

        // Properties 特征：key=value（无缩进，等号分割）
        if (HasPropertiesFeatures(content))
            return ConfigFormat.Properties;

        return ConfigFormat.Unknown;
    }

    private static string TrimBomAndWhitespace(string content)
    {
        if (content.Length == 0)
            return content;

        int start = 0;

        // 跳过 UTF-8 BOM (\uFEFF)
        if (content[0] == '\uFEFF')
            start = 1;

        // 跳过空白字符
        while (start < content.Length && char.IsWhiteSpace(content[start]))
            start++;

        return start == 0 ? content : content.Substring(start);
    }

    /// <summary>
    /// 通过文件扩展名检测格式
    /// </summary>
    public static ConfigFormat DetectByExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ConfigFormat.Unknown;

        var ext = extension.ToLowerInvariant();
        return ext switch
        {
            ".properties" => ConfigFormat.Properties,
            ".yml" or ".yaml" => ConfigFormat.Yaml,
            ".json" => ConfigFormat.Json,
            _ => ConfigFormat.Unknown
        };
    }

    /// <summary>
    /// 综合判断：先看扩展名，再看内容，内容优先级更高
    /// </summary>
    public static ConfigFormat Resolve(string content, string extension)
    {
        // 先看内容
        var contentFormat = Detect(content);
        if (contentFormat != ConfigFormat.Unknown)
            return contentFormat;

        // 内容无法判断时，回退到扩展名
        return DetectByExtension(extension);
    }

    private static bool TryParseJson(string content)
    {
        try
        {
            JsonDocument.Parse(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasYamlFeatures(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // YAML 文档分隔符
        if (lines.Any(l => l.Trim() == "---"))
            return true;

        // 检查是否有缩进的 "key: value" 行（冒号后有空格或冒号在行尾）
        int yamlLineCount = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
                continue;

            // 有缩进（行首有空格）且包含冒号
            if (line.StartsWith(' ') && trimmed.Contains(':'))
            {
                yamlLineCount++;
                continue;
            }

            // 无缩进的 "key: value" 行（但不是 key=value）
            if (!line.StartsWith(' ') && trimmed.Contains(':') && !trimmed.Contains('='))
            {
                yamlLineCount++;
            }
        }

        return yamlLineCount > 0;
    }

    private static bool HasPropertiesFeatures(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int propsLineCount = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // 跳过注释和空行
            if (trimmed.StartsWith('#') || string.IsNullOrEmpty(trimmed))
                continue;

            // 有等号且无冒号 → properties 特征
            if (trimmed.Contains('=') && !trimmed.Contains(':'))
                propsLineCount++;
        }

        return propsLineCount > 0;
    }
}
