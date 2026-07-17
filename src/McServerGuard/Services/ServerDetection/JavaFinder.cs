// -----------------------------------------------------------------------------
// 文件名: JavaFinder.cs
// 命名空间: McServerGuard.Services.ServerDetection
// 功能描述: Java 运行时查找器，多策略扫描系统中的 Java 安装并验证版本信息
// 依赖组件: System.Diagnostics, System.IO, Microsoft.Win32, Serilog
// 设计模式: 策略模式（多源查找）、验证器模式、收集器模式
// -----------------------------------------------------------------------------
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Serilog;

namespace McServerGuard.Services.ServerDetection;

/// <summary>
/// Java 运行时查找器 —— Windows 平台专用
/// </summary>
/// <remarks>
/// <para>采用多策略扫描方案，从多种来源发现系统中的 Java 安装实例，
/// 确保在各种部署环境下均能可靠定位 Java 运行时。</para>
/// <para>查找来源（按优先级排序）：
///   1. JAVA_HOME 环境变量（用户显式配置）
///   2. Windows 注册表（安装器写入，最可靠）
///   3. PATH 环境变量
///   4. where 命令查找
///   5. 常见安装目录扫描（Program Files、用户目录等）
/// </para>
/// </remarks>
public static class JavaFinder
{
    /// <summary>
    /// Java 安装信息实体
    /// </summary>
    public class JavaInstallation
    {
        /// <summary>java.exe 可执行文件完整路径</summary>
        public string JavaPath { get; init; } = string.Empty;
        /// <summary>JAVA_HOME 根目录路径</summary>
        public string JavaHome { get; init; } = string.Empty;
        /// <summary>版本号对象</summary>
        public Version? Version { get; init; }
        /// <summary>版本字符串</summary>
        public string VersionString { get; init; } = string.Empty;
        /// <summary>是否为 64 位架构</summary>
        public bool Is64Bit { get; init; }
        /// <summary>发行厂商名称</summary>
        public string Vendor { get; init; } = string.Empty;
    }

    /// <summary>
    /// 查找系统中第一个可用的 Java 运行时
    /// </summary>
    /// <returns>java.exe 路径；未找到返回 null</returns>
    public static string? FindJava()
    {
        var all = FindAllJavaInstallations();
        return all.FirstOrDefault()?.JavaPath;
    }

    /// <summary>
    /// 查找系统中所有的 Java 安装实例
    /// </summary>
    /// <returns>Java 安装信息列表，按版本号从高到低排序</returns>
    public static List<JavaInstallation> FindAllJavaInstallations()
    {
        Log.Debug("开始在系统中查找 Java 安装...");

        var found = new Dictionary<string, JavaInstallation>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>();

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaExe = GetJavaExecutable(javaHome);
            if (javaExe != null) candidates.Add(javaExe);
            Log.Debug("JAVA_HOME: {Path}", javaHome);
        }

        var registryJava = FindJavaViaRegistry();
        candidates.AddRange(registryJava);
        Log.Debug("注册表查询完成，找到 {Count} 个", registryJava.Count);

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
            Log.Debug("PATH 环境变量已扫描");
        }

        var whereResults = FindJavaViaWhereCommand();
        candidates.AddRange(whereResults);
        Log.Debug("where 命令查找完成");

        candidates.AddRange(ScanCommonInstallPaths());
        Log.Debug("常见路径扫描完成");

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
                Log.Debug("找到 Java: {Path} (版本: {Version})", info.JavaPath, info.VersionString);
            }
        }

        var result = found.Values
            .OrderByDescending(j => j.Version ?? new Version(0, 0))
            .ThenBy(j => j.JavaPath)
            .ToList();

        Log.Information("共找到 {Count} 个 Java 安装", result.Count);
        return result;
    }

    /// <summary>
    /// 从 Java 安装目录获取 java.exe 可执行文件路径
    /// </summary>
    /// <param name="directory">Java 安装根目录</param>
    /// <returns>java.exe 完整路径；未找到返回 null</returns>
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

    /// <summary>
    /// 通过 Windows 注册表查找 Java 安装路径
    /// </summary>
    /// <returns>java.exe 路径列表</returns>
    /// <remarks>
    /// 扫描多个注册表路径，包括新版 JDK/JRE、旧版 JDK/JRE 及 32 位兼容节点。
    /// Java 安装器（Oracle、Temurin、Microsoft 等）均会写入注册表，
    /// 因此这是最可靠的查找方式。
    /// </remarks>
    private static List<string> FindJavaViaRegistry()
    {
        var results = new List<string>();

        var registryPaths = new[]
        {
            @"SOFTWARE\JavaSoft\JDK",
            @"SOFTWARE\JavaSoft\JRE",
            @"SOFTWARE\JavaSoft\Java Development Kit",
            @"SOFTWARE\JavaSoft\Java Runtime Environment",
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
                    Log.Debug("读取注册表 {Hive}\\{Key} 失败: {Msg}", hive.Name, keyPath, ex.Message);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 使用 where 命令查找 Java
    /// </summary>
    /// <returns>java.exe 路径列表</returns>
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
            Log.Debug("where 命令查找 Java 失败: {Msg}", ex.Message);
        }

        return results;
    }

    /// <summary>
    /// 扫描常见的 Java 安装目录
    /// </summary>
    /// <returns>java.exe 路径列表</returns>
    private static List<string> ScanCommonInstallPaths()
    {
        var results = new List<string>();

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var basePaths = new[]
        {
            Path.Combine(programFiles, "Java"),
            Path.Combine(programFiles, "Eclipse Adoptium"),
            Path.Combine(programFiles, "Microsoft"),
            Path.Combine(programFiles, "BellSoft"),
            Path.Combine(programFiles, "Azul"),
            Path.Combine(programFiles, "Amazon Corretto"),
            Path.Combine(programFiles, "SapMachine"),
            Path.Combine(programFiles, "OpenLogic"),
            Path.Combine(programFiles, "GraalVM"),
            Path.Combine(programFilesX86, "Java"),
            Path.Combine(programFilesX86, "Eclipse Adoptium"),
            Path.Combine(programFilesX86, "BellSoft"),
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
            }
        }

        return results;
    }

    /// <summary>
    /// 验证 Java 可执行文件的有效性，并提取版本与厂商信息
    /// </summary>
    /// <param name="javaPath">java.exe 完整路径</param>
    /// <returns>Java 安装信息对象；验证失败返回 null</returns>
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
            Log.Debug("验证 Java 失败 {Path}: {Msg}", javaPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 从 java -version 输出中解析版本号
    /// </summary>
    /// <param name="versionOutput">java -version 命令输出</param>
    /// <returns>版本号对象；解析失败返回 null</returns>
    /// <remarks>
    /// 兼容两种版本格式：
    ///   - Java 9+：major.minor.build（如 17.0.1）
    ///   - Java 8 及以前：1.major.minor_build（如 1.8.0_301 → 主版本为 8）
    /// </remarks>
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

    /// <summary>
    /// 从 java -version 输出中解析发行厂商
    /// </summary>
    /// <param name="versionOutput">java -version 命令输出</param>
    /// <returns>厂商名称字符串</returns>
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

    /// <summary>
    /// 从 java.exe 路径反推 JAVA_HOME 根目录
    /// </summary>
    /// <param name="javaPath">java.exe 完整路径</param>
    /// <returns>JAVA_HOME 路径；推导失败返回 null</returns>
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
