using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>☕ Java 查找器 —— Windows 平台专用，各种姿势挖 Java 安装路径</summary>
/// <remarks>
/// 有人装在 C:\Program Files\Java，有人装在 C:\Program Files\Eclipse Adoptium，
/// 有人用 JAVA_HOME，有人直接扔 PATH 里，还有人用 SDKMAN...
/// 我们的目标是：不管藏在哪儿，都给它挖出来 🔍
/// </remarks>
public static class JavaFinder
{
    /// <summary>☕ 找到的 Java 安装信息</summary>
    public class JavaInstallation
    {
        public string JavaPath { get; init; } = string.Empty;
        public string JavaHome { get; init; } = string.Empty;
        public Version? Version { get; init; }
        public string VersionString { get; init; } = string.Empty;
        public bool Is64Bit { get; init; }
        public string Vendor { get; init; } = string.Empty;
    }

    /// <summary>🎯 查找系统中可用的 Java —— 找到第一个能用的就返回</summary>
    public static string? FindJava()
    {
        var all = FindAllJavaInstallations();
        return all.FirstOrDefault()?.JavaPath;
    }

    /// <summary>📋 查找系统中所有的 Java 安装 —— 按版本从高到低排序</summary>
    public static List<JavaInstallation> FindAllJavaInstallations()
    {
        Log.Debug("🔍 开始在系统中查找 Java 安装...");

        var found = new Dictionary<string, JavaInstallation>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        // 1️⃣ JAVA_HOME 环境变量（最优先，用户明确配置的）
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaExe = GetJavaExecutable(javaHome);
            if (javaExe != null) candidates.Add(javaExe);
            Log.Debug("📝 JAVA_HOME: {Path}", javaHome);
        }

        // 2️⃣ Windows 注册表查询（最靠谱，安装器一定会写）
        var registryJava = FindJavaViaRegistry();
        candidates.AddRange(registryJava);
        Log.Debug("📝 注册表查询完成，找到 {Count} 个", registryJava.Count);

        // 3️⃣ PATH 环境变量
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var path in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var javaExe = GetJavaExecutable(path.Trim());
                if (javaExe != null && File.Exists(javaExe))
                {
                    candidates.Add(javaExe);
                }
            }
            Log.Debug("📝 PATH 环境变量已扫描");
        }

        // 4️⃣ where 命令查找
        var whereResults = FindJavaViaWhereCommand();
        candidates.AddRange(whereResults);
        Log.Debug("📝 where 命令查找完成");

        // 5️⃣ 常见安装路径扫描
        candidates.AddRange(ScanCommonInstallPaths());
        Log.Debug("📝 常见路径扫描完成");

        // 🔍 去重 + 验证每个候选
        foreach (var candidate in candidates)
        {
            var normalized = Path.GetFullPath(candidate);
            if (found.ContainsKey(normalized))
                continue;

            if (!File.Exists(normalized))
                continue;

            var info = VerifyJava(normalized);
            if (info != null)
            {
                found[normalized] = info;
                Log.Debug("✅ 找到 Java: {Path} (版本: {Version})", info.JavaPath, info.VersionString);
            }
        }

        var result = found.Values
            .OrderByDescending(j => j.Version ?? new Version(0, 0))
            .ThenBy(j => j.JavaPath)
            .ToList();

        Log.Information("🔍 共找到 {Count} 个 Java 安装", result.Count);
        return result;
    }

    /// <summary>📦 从 Java 安装目录获取 java.exe 路径</summary>
    private static string? GetJavaExecutable(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        var binDir = Path.Combine(directory, "bin");
        if (Directory.Exists(binDir))
        {
            var javaPath = Path.Combine(binDir, "java.exe");
            if (File.Exists(javaPath))
                return javaPath;
        }

        var directPath = Path.Combine(directory, "java.exe");
        if (File.Exists(directPath))
            return directPath;

        return null;
    }

    /// <summary>🪟 通过 Windows 注册表查找 Java 安装路径</summary>
    /// <remarks>
    /// Java 安装器（Oracle、Temurin、Microsoft 等）都会往注册表里写安装路径，
    /// 这是最靠谱的查找方式，比扫描目录快多了。
    /// </remarks>
    private static List<string> FindJavaViaRegistry()
    {
        var results = new List<string>();

        var registryPaths = new[]
        {
            // 新版 JDK/JRE
            @"SOFTWARE\JavaSoft\JDK",
            @"SOFTWARE\JavaSoft\JRE",
            // 旧版 JDK/JRE
            @"SOFTWARE\JavaSoft\Java Development Kit",
            @"SOFTWARE\JavaSoft\Java Runtime Environment",
            // 32 位兼容
            @"SOFTWARE\WOW6432Node\JavaSoft\JDK",
            @"SOFTWARE\WOW6432Node\JavaSoft\JRE",
            @"SOFTWARE\WOW6432Node\JavaSoft\Java Development Kit",
            @"SOFTWARE\WOW6432Node\JavaSoft\Java Runtime Environment",
        };

        var registryHives = new[] { Registry.LocalMachine, Registry.CurrentUser };

        foreach (var hive in registryHives)
        {
            foreach (var keyPath in registryPaths)
            {
                try
                {
                    using var baseKey = hive.OpenSubKey(keyPath);
                    if (baseKey == null) continue;

                    foreach (var subKeyName in baseKey.GetSubKeyNames())
                    {
                        using var subKey = baseKey.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var javaHomeValue = subKey.GetValue("JavaHome") as string;
                        if (!string.IsNullOrEmpty(javaHomeValue) && Directory.Exists(javaHomeValue))
                        {
                            var javaExe = GetJavaExecutable(javaHomeValue);
                            if (javaExe != null && File.Exists(javaExe))
                            {
                                results.Add(javaExe);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug("⚠️ 读取注册表 {Hive}\\{Key} 失败: {Msg}", hive.Name, keyPath, ex.Message);
                }
            }
        }

        return results;
    }

    /// <summary>💻 用 where 命令查找 Java</summary>
    private static List<string> FindJavaViaWhereCommand()
    {
        var results = new List<string>();

        try
        {
            var startInfo = new ProcessStartInfo("where.exe", "java")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return results;

            process.WaitForExit(5000);
            var output = process.StandardOutput.ReadToEnd();

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && File.Exists(trimmed))
                {
                    results.Add(trimmed);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("⚠️ where 命令查找 Java 失败: {Msg}", ex.Message);
        }

        return results;
    }

    /// <summary>📂 扫描常见的 Java 安装目录</summary>
    private static List<string> ScanCommonInstallPaths()
    {
        var results = new List<string>();

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var basePaths = new[]
        {
            // Program Files 下的各种发行版
            Path.Combine(programFiles, "Java"),
            Path.Combine(programFiles, "Eclipse Adoptium"),
            Path.Combine(programFiles, "Microsoft"),
            Path.Combine(programFiles, "BellSoft"),
            Path.Combine(programFiles, "Azul"),
            Path.Combine(programFiles, "Amazon Corretto"),
            Path.Combine(programFiles, "SapMachine"),
            Path.Combine(programFiles, "OpenLogic"),
            Path.Combine(programFiles, "GraalVM"),
            // Program Files (x86)
            Path.Combine(programFilesX86, "Java"),
            Path.Combine(programFilesX86, "Eclipse Adoptium"),
            Path.Combine(programFilesX86, "BellSoft"),
            // 用户目录
            Path.Combine(userProfile, ".jdks"),
            Path.Combine(userProfile, ".sdkman", "candidates", "java"),
            Path.Combine(localAppData, "Programs", "Eclipse Adoptium"),
        };

        foreach (var basePath in basePaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            try
            {
                var subDirs = Directory.GetDirectories(basePath);
                foreach (var dir in subDirs)
                {
                    var javaExe = GetJavaExecutable(dir);
                    if (javaExe != null)
                    {
                        results.Add(javaExe);
                    }
                }
            }
            catch
            {
                // 权限不足什么的，跳过就好
            }
        }

        return results;
    }

    /// <summary>✅ 验证 Java 是否真的能用，顺便扒出版本信息</summary>
    public static JavaInstallation? VerifyJava(string javaPath)
    {
        if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
            return null;

        try
        {
            var startInfo = new ProcessStartInfo(javaPath, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return null;

            process.WaitForExit(5000);
            if (process.ExitCode != 0)
                return null;

            var output = process.StandardError.ReadToEnd();
            if (string.IsNullOrEmpty(output))
                output = process.StandardOutput.ReadToEnd();

            var version = ParseVersion(output);
            var javaHome = GetJavaHomeFromExecutable(javaPath);

            return new JavaInstallation
            {
                JavaPath = javaPath,
                JavaHome = javaHome ?? string.Empty,
                Version = version,
                VersionString = version?.ToString() ?? string.Empty,
                Is64Bit = output.Contains("64-Bit", StringComparison.OrdinalIgnoreCase),
                Vendor = ParseVendor(output)
            };
        }
        catch (Exception ex)
        {
            Log.Debug("⚠️ 验证 Java 失败 {Path}: {Msg}", javaPath, ex.Message);
            return null;
        }
    }

    /// <summary>📝 从 java -version 输出中解析版本号</summary>
    private static Version? ParseVersion(string versionOutput)
    {
        if (string.IsNullOrWhiteSpace(versionOutput))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            versionOutput,
            @"version\s+""?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:[._](\d+))?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        try
        {
            int major = int.Parse(match.Groups[1].Value);
            int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

            // Java 8 及以前的格式: 1.8.0_301 → 主版本是 8
            if (major == 1 && match.Groups[2].Success)
            {
                major = int.Parse(match.Groups[2].Value);
                minor = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                build = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
            }

            return new Version(major, minor, build);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>🏭 从 java -version 输出中解析厂商</summary>
    private static string ParseVendor(string versionOutput)
    {
        if (string.IsNullOrWhiteSpace(versionOutput))
            return "Unknown";

        if (versionOutput.Contains("Temurin", StringComparison.OrdinalIgnoreCase) ||
            versionOutput.Contains("AdoptOpenJDK", StringComparison.OrdinalIgnoreCase))
            return "Eclipse Temurin";
        if (versionOutput.Contains("Oracle", StringComparison.OrdinalIgnoreCase) ||
            versionOutput.Contains("Java(TM)", StringComparison.OrdinalIgnoreCase))
            return "Oracle";
        if (versionOutput.Contains("Microsoft", StringComparison.OrdinalIgnoreCase))
            return "Microsoft";
        if (versionOutput.Contains("BellSoft", StringComparison.OrdinalIgnoreCase) ||
            versionOutput.Contains("Liberica", StringComparison.OrdinalIgnoreCase))
            return "BellSoft Liberica";
        if (versionOutput.Contains("Azul", StringComparison.OrdinalIgnoreCase) ||
            versionOutput.Contains("Zulu", StringComparison.OrdinalIgnoreCase))
            return "Azul Zulu";
        if (versionOutput.Contains("Corretto", StringComparison.OrdinalIgnoreCase))
            return "Amazon Corretto";
        if (versionOutput.Contains("SapMachine", StringComparison.OrdinalIgnoreCase))
            return "SAP SapMachine";
        if (versionOutput.Contains("GraalVM", StringComparison.OrdinalIgnoreCase))
            return "GraalVM";
        if (versionOutput.Contains("Semeru", StringComparison.OrdinalIgnoreCase) ||
            versionOutput.Contains("IBM", StringComparison.OrdinalIgnoreCase))
            return "IBM Semeru";
        if (versionOutput.Contains("Red Hat", StringComparison.OrdinalIgnoreCase))
            return "Red Hat";
        if (versionOutput.Contains("OpenLogic", StringComparison.OrdinalIgnoreCase))
            return "OpenLogic";
        if (versionOutput.Contains("OpenJDK", StringComparison.OrdinalIgnoreCase))
            return "OpenJDK";

        return "Unknown";
    }

    /// <summary>🏠 从 java.exe 路径反推 JAVA_HOME</summary>
    private static string? GetJavaHomeFromExecutable(string javaPath)
    {
        try
        {
            var binDir = Path.GetDirectoryName(javaPath);
            if (string.IsNullOrEmpty(binDir))
                return null;

            if (Path.GetFileName(binDir).Equals("bin", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetDirectoryName(binDir);
            }

            return binDir;
        }
        catch
        {
            return null;
        }
    }
}
