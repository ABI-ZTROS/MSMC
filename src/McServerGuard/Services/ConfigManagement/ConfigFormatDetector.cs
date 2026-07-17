// -----------------------------------------------------------------------------
// 文件名: ConfigFormatDetector.cs
// 命名空间: McServerGuard.Services.ConfigManagement
// 功能描述: 配置文件格式探测器，基于内容特征与扩展名双重判定配置文件类型
// 依赖组件: System.Text.Json, System.Linq
// 设计模式: 策略模式、启发式检测、多因子判定
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.ConfigManagement;

using System.Text.Json;

/// <summary>
/// 配置文件格式枚举
/// </summary>
public enum ConfigFormat
{
    /// <summary>未知格式</summary>
    Unknown,
    /// <summary>Java Properties 格式</summary>
    Properties,
    /// <summary>YAML 格式</summary>
    Yaml,
    /// <summary>JSON 格式</summary>
    Json
}

/// <summary>
/// 基于内容特征的配置文件格式探测器
/// </summary>
/// <remarks>
/// <para>当文件扩展名无法可靠确定配置格式时（如 .conf、.cfg 等通用扩展名），
/// 通过分析文件内容的语法特征进行启发式判定。</para>
/// <para>检测优先级：JSON → YAML → Properties → Unknown
/// 内容特征判定权重高于扩展名判定。</para>
/// </remarks>
public static class ConfigFormatDetector
{
    /// <summary>
    /// 通过分析内容语法特征检测配置格式
    /// </summary>
    /// <param name="content">配置文件原始内容</param>
    /// <returns>检测到的配置格式枚举值</returns>
    /// <remarks>
    /// 判定优先级：JSON → YAML → Properties → Unknown
    /// </remarks>
    public static ConfigFormat Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ConfigFormat.Unknown;

        var trimmed = TrimBomAndWhitespace(content);

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return ConfigFormat.Json;

        if (HasYamlFeatures(content))
            return ConfigFormat.Yaml;

        if (HasPropertiesFeatures(content))
            return ConfigFormat.Properties;

        return ConfigFormat.Unknown;
    }

    /// <summary>
    /// 去除 UTF-8 BOM 头与前导空白字符
    /// </summary>
    /// <param name="content">原始文本内容</param>
    /// <returns>清理后的文本</returns>
    private static string TrimBomAndWhitespace(string content)
    {
        if (content.Length == 0)
            return content;

        int start = 0;

        if (content[0] == '\uFEFF')
            start = 1;

        while (start < content.Length && char.IsWhiteSpace(content[start]))
            start++;

        return start == 0 ? content : content.Substring(start);
    }

    /// <summary>
    /// 通过文件扩展名检测配置格式
    /// </summary>
    /// <param name="extension">文件扩展名（含前导点号）</param>
    /// <returns>检测到的配置格式枚举值</returns>
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
    /// 综合判定配置格式：内容特征优先，扩展名作为回退
    /// </summary>
    /// <param name="content">配置文件内容</param>
    /// <param name="extension">文件扩展名</param>
    /// <returns>最终判定的配置格式</returns>
    public static ConfigFormat Resolve(string content, string extension)
    {
        var contentFormat = Detect(content);
        if (contentFormat != ConfigFormat.Unknown)
            return contentFormat;

        return DetectByExtension(extension);
    }

    /// <summary>
    /// 尝试验证 JSON 格式合法性
    /// </summary>
    /// <param name="content">待检测的文本内容</param>
    /// <returns>合法 JSON 返回 <c>true</c>，否则返回 <c>false</c></returns>
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

    /// <summary>
    /// 检测文本是否包含 YAML 语法特征
    /// </summary>
    /// <param name="content">待检测的文本内容</param>
    /// <returns>命中 YAML 特征返回 <c>true</c></returns>
    /// <remarks>
    /// 判定依据：
    ///   - 包含 YAML 文档分隔符 "---"
    ///   - 包含缩进的 "key: value" 结构行
    ///   - 无缩进的 "key: value" 结构行（且不含 = 号）
    /// </remarks>
    private static bool HasYamlFeatures(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Any(l => l.Trim() == "---"))
            return true;

        int yamlLineCount = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
                continue;

            if (line.StartsWith(' ') && trimmed.Contains(':'))
            {
                yamlLineCount++;
                continue;
            }

            if (!line.StartsWith(' ') && trimmed.Contains(':') && !trimmed.Contains('='))
            {
                yamlLineCount++;
            }
        }

        return yamlLineCount > 0;
    }

    /// <summary>
    /// 检测文本是否包含 Properties 语法特征
    /// </summary>
    /// <param name="content">待检测的文本内容</param>
    /// <returns>命中 Properties 特征返回 <c>true</c></returns>
    /// <remarks>
    /// 判定依据：无缩进的 key=value 结构行，且不含冒号。
    /// </remarks>
    private static bool HasPropertiesFeatures(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        int propsLineCount = 0;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith('#') || string.IsNullOrEmpty(trimmed))
                continue;

            if (trimmed.Contains('=') && !trimmed.Contains(':'))
                propsLineCount++;
        }

        return propsLineCount > 0;
    }
}
