// -----------------------------------------------------------------------------
// 文件名: PluginYmlParser.cs
// 命名空间: io.NET.ZTR_OS.Features.PluginManager.Services
// 功能描述: plugin.yml 解析器（轻量逐行解析，不依赖第三方库）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.PluginManager.Services;

using System;
using System.IO;
using System.IO.Compression;
using io.NET.ZTR_OS.Features.PluginManager.Models;

/// <summary>
/// 解析 Bukkit 风格 plugin.yml
/// </summary>
public static class PluginYmlParser
{
    /// <summary>
    /// 从 jar 文件解析 PluginInfo
    /// </summary>
    public static PluginInfo ParseJar(string jarPath)
    {
        var info = new PluginInfo
        {
            FilePath = jarPath,
            IsValid = false,
        };

        try
        {
            using var fs = File.OpenRead(jarPath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var entry = zip.GetEntry("plugin.yml");
            if (entry == null)
                return info;

            using var sr = new StreamReader(entry.Open());
            var yml = sr.ReadToEnd();
            var kv = ParseSimpleYaml(yml);

            if (kv.TryGetValue("name", out var name) && !string.IsNullOrEmpty(name))
                info.Name = name;
            if (kv.TryGetValue("version", out var version) && !string.IsNullOrEmpty(version))
                info.Version = version;
            if (kv.TryGetValue("author", out var author))
                info.Author = author;
            else if (kv.TryGetValue("authors", out var authors))
                info.Author = authors;
            if (kv.TryGetValue("main", out var main))
                info.Main = main;
            if (kv.TryGetValue("description", out var desc))
                info.Description = desc;

            info.IsValid = !string.IsNullOrEmpty(info.Name) && !string.IsNullOrEmpty(info.Version);
        }
        catch
        {
            // 解析失败保持 IsValid=false
        }

        return info;
    }

    /// <summary>
    /// 从目录解析（读取目录下的 plugin.yml，测试用）
    /// </summary>
    public static PluginInfo ParseDirectory(string dirPath)
    {
        var info = new PluginInfo
        {
            FilePath = dirPath,
            IsValid = false,
        };

        try
        {
            var ymlPath = Path.Combine(dirPath, "plugin.yml");
            if (!File.Exists(ymlPath))
                return info;

            var yml = File.ReadAllText(ymlPath);
            var kv = ParseSimpleYaml(yml);

            if (kv.TryGetValue("name", out var name) && !string.IsNullOrEmpty(name))
                info.Name = name;
            if (kv.TryGetValue("version", out var version) && !string.IsNullOrEmpty(version))
                info.Version = version;
            if (kv.TryGetValue("author", out var author))
                info.Author = author;
            else if (kv.TryGetValue("authors", out var authors))
                info.Author = authors;
            if (kv.TryGetValue("main", out var main))
                info.Main = main;
            if (kv.TryGetValue("description", out var desc))
                info.Description = desc;

            info.IsValid = !string.IsNullOrEmpty(info.Name) && !string.IsNullOrEmpty(info.Version);
        }
        catch
        {
            // 解析失败保持 IsValid=false
        }

        return info;
    }

    /// <summary>
    /// 轻量 YAML 解析：只处理 key: value 单行格式
    /// - # 开头注释跳过
    /// - 冒号后空格忽略
    /// - 值首尾单引号/双引号去除
    /// </summary>
    private static Dictionary<string, string> ParseSimpleYaml(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(content))
            return result;

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.TrimStart();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;
            if (trimmed.StartsWith('#'))
                continue;

            var colonIdx = trimmed.IndexOf(':');
            if (colonIdx < 0)
                continue;

            var key = trimmed.Substring(0, colonIdx).Trim();
            if (string.IsNullOrEmpty(key))
                continue;

            var value = colonIdx + 1 < trimmed.Length
                ? trimmed.Substring(colonIdx + 1).Trim()
                : string.Empty;

            // 去首尾引号
            if (value.Length >= 2)
            {
                if ((value.StartsWith('\'') && value.EndsWith('\'')) ||
                    (value.StartsWith('"') && value.EndsWith('"')))
                {
                    value = value.Substring(1, value.Length - 2);
                }
            }

            result[key] = value;
        }

        return result;
    }
}
