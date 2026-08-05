// -----------------------------------------------------------------------------
// 文件名: SafeModeBootstrapper.cs
// 命名空间: io.NET.ZTR_OS.Features.SafeModeKeeper.Services
// 功能描述: 安全模式引导器：应用 L1/L2/L3 三级降级策略 + ExitSafeMode 还原
// 降级策略：
//   L1 轻度: plugins/*.jar → *.jar.disabled
//   L2 中度: server.properties view-distance=2, simulation-distance=2, online-mode=false
//   L3 重度: 写 jvm.args (-XX:+UseSerialGC -Xmx1G)
// 所有动作备份到 {serverDir}/.msmc/safemode_manifest.json，ExitSafeMode 按此还原
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.SafeModeKeeper.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using io.NET.ZTR_OS.Features.SafeModeKeeper.Models;

/// <summary>
/// 安全模式引导器：应用降级策略与还原
/// </summary>
public class SafeModeBootstrapper
{
    private const string ManifestFileName = "safemode_manifest.json";
    private const string MsmcSubdir = ".msmc";

    // L2 安全值
    private const string SafeViewDistance = "2";
    private const string SafeSimulationDistance = "2";
    private const string SafeOnlineMode = "false";

    // L3 保守 JVM 参数
    private const string SafeJvmArgs = "-XX:+UseSerialGC -Xmx1G";

    // 需要在 L2 中改值的 3 个键
    private static readonly string[] L2Keys =
    {
        "view-distance",
        "simulation-distance",
        "online-mode",
    };

    /// <summary>
    /// 应用指定等级的降级策略（会自动补全低于它的等级）
    /// </summary>
    /// <param name="level">目标等级（None 不做任何事）</param>
    /// <param name="serverDir">服务器根目录</param>
    public void ApplyLevel(SafeModeLevel level, string serverDir)
    {
        if (string.IsNullOrWhiteSpace(serverDir))
            throw new ArgumentException("服务器目录不能为空", nameof(serverDir));

        if (level == SafeModeLevel.None) return;

        // 1) 读取或创建 manifest
        var manifest = LoadManifest(serverDir);

        // 2) 按等级顺序应用（L1 → L2 → L3）
        if (level >= SafeModeLevel.L1 && manifest.AppliedLevel < SafeModeLevel.L1)
            ApplyL1(serverDir, manifest);

        if (level >= SafeModeLevel.L2 && manifest.AppliedLevel < SafeModeLevel.L2)
            ApplyL2(serverDir, manifest);

        if (level >= SafeModeLevel.L3 && manifest.AppliedLevel < SafeModeLevel.L3)
            ApplyL3(serverDir, manifest);

        // 3) 更新等级 & 保存 manifest
        manifest.AppliedLevel = (SafeModeLevel)Math.Max((int)manifest.AppliedLevel, (int)level);
        SaveManifest(serverDir, manifest);
    }

    /// <summary>
    /// 退出安全模式：按 manifest 逆向还原所有改动
    /// </summary>
    /// <param name="serverDir">服务器根目录</param>
    public void ExitSafeMode(string serverDir)
    {
        if (string.IsNullOrWhiteSpace(serverDir))
            throw new ArgumentException("服务器目录不能为空", nameof(serverDir));

        var manifest = LoadManifest(serverDir);
        if (manifest.AppliedLevel == SafeModeLevel.None) return;

        // 按 L3 → L2 → L1 逆序还原
        if (manifest.AppliedLevel >= SafeModeLevel.L3)
            RestoreL3(serverDir, manifest);

        if (manifest.AppliedLevel >= SafeModeLevel.L2)
            RestoreL2(serverDir, manifest);

        if (manifest.AppliedLevel >= SafeModeLevel.L1)
            RestoreL1(serverDir, manifest);

        // 清空 manifest，标记 None
        manifest.RenamedPlugins.Clear();
        manifest.OriginalProperties.Clear();
        manifest.HadJvmArgsFile = false;
        manifest.OriginalJvmArgsContent = null;
        manifest.AppliedLevel = SafeModeLevel.None;
        SaveManifest(serverDir, manifest);
    }

    // ═══════════════════════════════════════════════════════
    // L1: 禁用 plugins (jar → jar.disabled)
    // ═══════════════════════════════════════════════════════

    private static void ApplyL1(string serverDir, SafeModeManifest manifest)
    {
        var pluginsDir = Path.Combine(serverDir, "plugins");
        if (!Directory.Exists(pluginsDir)) return;

        var jarFiles = Directory.GetFiles(pluginsDir, "*.jar", SearchOption.TopDirectoryOnly);
        foreach (var original in jarFiles)
        {
            var renamed = original + ".disabled";
            try
            {
                if (File.Exists(renamed)) continue;
                File.Move(original, renamed);
                manifest.RenamedPlugins[original] = renamed;
            }
            catch
            {
                // 单个文件失败不中断整个流程
            }
        }
    }

    private static void RestoreL1(string serverDir, SafeModeManifest manifest)
    {
        foreach (var kvp in manifest.RenamedPlugins.ToList())
        {
            var (original, renamed) = (kvp.Key, kvp.Value);
            try
            {
                if (File.Exists(renamed))
                {
                    if (File.Exists(original)) File.Delete(original);
                    File.Move(renamed, original);
                }
            }
            catch
            {
                // 单个失败不中断
            }
        }
        manifest.RenamedPlugins.Clear();
    }

    // ═══════════════════════════════════════════════════════
    // L2: 修改 server.properties 3 键
    // ═══════════════════════════════════════════════════════

    private static void ApplyL2(string serverDir, SafeModeManifest manifest)
    {
        var propsPath = Path.Combine(serverDir, "server.properties");
        if (!File.Exists(propsPath)) return;

        var parsed = ParsePropertiesFile(propsPath);

        // 先保存原值（只保存那些我们打算覆盖的键）
        foreach (var key in L2Keys)
        {
            if (parsed.TryGetValue(key, out var originalVal) &&
                !manifest.OriginalProperties.ContainsKey(key))
            {
                manifest.OriginalProperties[key] = originalVal;
            }
        }

        // 覆盖为安全值
        parsed["view-distance"] = SafeViewDistance;
        parsed["simulation-distance"] = SafeSimulationDistance;
        parsed["online-mode"] = SafeOnlineMode;

        // 回写文件（简单实现：保留顺序，替换目标行；其他行原样输出）
        WritePropertiesBack(propsPath, parsed);
    }

    private static void RestoreL2(string serverDir, SafeModeManifest manifest)
    {
        var propsPath = Path.Combine(serverDir, "server.properties");
        if (!File.Exists(propsPath) || manifest.OriginalProperties.Count == 0) return;

        var parsed = ParsePropertiesFile(propsPath);

        // 恢复原值
        foreach (var kvp in manifest.OriginalProperties)
        {
            parsed[kvp.Key] = kvp.Value;
        }

        WritePropertiesBack(propsPath, parsed);
        manifest.OriginalProperties.Clear();
    }

    // ═══════════════════════════════════════════════════════
    // L3: jvm.args 保守参数
    // ═══════════════════════════════════════════════════════

    private static void ApplyL3(string serverDir, SafeModeManifest manifest)
    {
        var jvmArgsPath = Path.Combine(serverDir, "jvm.args");
        manifest.HadJvmArgsFile = File.Exists(jvmArgsPath);
        if (manifest.HadJvmArgsFile)
        {
            manifest.OriginalJvmArgsContent = File.ReadAllText(jvmArgsPath);
        }
        File.WriteAllText(jvmArgsPath, SafeJvmArgs, new UTF8Encoding(false));
    }

    private static void RestoreL3(string serverDir, SafeModeManifest manifest)
    {
        var jvmArgsPath = Path.Combine(serverDir, "jvm.args");
        if (manifest.HadJvmArgsFile)
        {
            if (!string.IsNullOrEmpty(manifest.OriginalJvmArgsContent))
                File.WriteAllText(jvmArgsPath, manifest.OriginalJvmArgsContent, new UTF8Encoding(false));
            else if (File.Exists(jvmArgsPath))
                File.Delete(jvmArgsPath);
        }
        else
        {
            if (File.Exists(jvmArgsPath))
                File.Delete(jvmArgsPath);
        }
        manifest.HadJvmArgsFile = false;
        manifest.OriginalJvmArgsContent = null;
    }

    // ═══════════════════════════════════════════════════════
    // server.properties 简易解析/回写（保持行顺序）
    // ═══════════════════════════════════════════════════════

    private static Dictionary<string, string> ParsePropertiesFile(string path)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('!')) continue;
            var eq = line.IndexOf('=');
            if (eq < 0)
            {
                eq = line.IndexOf(':');
                if (eq < 0) continue;
            }
            var k = line[..eq].Trim();
            var v = line[(eq + 1)..].Trim();
            if (k.Length > 0) dict[k] = v;
        }
        return dict;
    }

    private static void WritePropertiesBack(string path, Dictionary<string, string> newValues)
    {
        var lines = File.ReadAllLines(path);
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<string>(lines.Length);

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith('!'))
            {
                output.Add(rawLine);
                continue;
            }

            var eq = trimmed.IndexOf('=');
            char sep = '=';
            if (eq < 0)
            {
                eq = trimmed.IndexOf(':');
                if (eq < 0) { output.Add(rawLine); continue; }
                sep = ':';
            }

            var leadingWs = rawLine[..^rawLine.TrimStart().Length];
            var k = trimmed[..eq].Trim();

            if (k.Length > 0 && newValues.TryGetValue(k, out var newVal) && !processed.Contains(k))
            {
                output.Add($"{leadingWs}{k}{sep}{newVal}");
                processed.Add(k);
            }
            else
            {
                output.Add(rawLine);
            }
        }

        // 追加不存在于文件但在 newValues 中的键（通常 L2 不会触发，但稳妥）
        foreach (var kvp in newValues)
        {
            if (!processed.Contains(kvp.Key))
            {
                output.Add($"{kvp.Key}={kvp.Value}");
            }
        }

        File.WriteAllLines(path, output, new UTF8Encoding(false));
    }

    // ═══════════════════════════════════════════════════════
    // Manifest 读写
    // ═══════════════════════════════════════════════════════

    private static SafeModeManifest LoadManifest(string serverDir)
    {
        var msmcDir = Path.Combine(serverDir, MsmcSubdir);
        var manifestPath = Path.Combine(msmcDir, ManifestFileName);
        try
        {
            if (!File.Exists(manifestPath)) return new SafeModeManifest();
            var json = File.ReadAllText(manifestPath);
            return JsonSerializer.Deserialize<SafeModeManifest>(json) ?? new SafeModeManifest();
        }
        catch
        {
            return new SafeModeManifest();
        }
    }

    private static void SaveManifest(string serverDir, SafeModeManifest manifest)
    {
        var msmcDir = Path.Combine(serverDir, MsmcSubdir);
        if (!Directory.Exists(msmcDir)) Directory.CreateDirectory(msmcDir);
        var manifestPath = Path.Combine(msmcDir, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(manifestPath, json);
    }
}
