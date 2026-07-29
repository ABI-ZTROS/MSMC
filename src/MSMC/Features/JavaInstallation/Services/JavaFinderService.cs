// -----------------------------------------------------------------------------
// 文件名: JavaFinderService.cs
// 命名空间: io.NET.ZTR_OS.Features.JavaInstallation.Services
// 功能描述: Java 运行时查找服务，多策略扫描系统中的 Java 安装并验证版本信息
// 依赖组件: System.Diagnostics, System.IO, Microsoft.Win32, Serilog, IAppConfigService
// 设计模式: 策略模式（多源查找）、验证器模式、收集器模式、服务模式（DI注入）
// -----------------------------------------------------------------------------
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using io.NET.ZTR_OS.Features.Settings.Services;
using Serilog;

namespace io.NET.ZTR_OS.Features.JavaInstallation.Services;

/// <summary>
/// Java 运行时查找服务 —— Windows 平台专用
/// </summary>
/// <remarks>
/// <para>采用多策略扫描方案，从多种来源发现系统中的 Java 安装实例，
/// 确保在各种部署环境下均能可靠定位 Java 运行时。</para>
/// <para>查找来源（按优先级排序）：
///   1. 用户自定义路径（最高优先级）
///   2. JAVA_HOME / JDK_HOME / JRE_HOME 环境变量
///   3. Windows 注册表（安装器写入，最可靠）
///   4. PATH 环境变量
///   5. where 命令查找
///   6. 常见安装目录扫描（Program Files、用户目录等）
/// </para>
/// </remarks>
public class JavaFinderService : IJavaFinderService
{
    private readonly IAppConfigService _appConfigService;

    /// <summary>
    /// 默认 Java 路径（用户指定）
    /// </summary>
    public string? DefaultJavaPath
    {
        get => _appConfigService.Config.DefaultJavaPath;
        set
        {
            _appConfigService.Config.DefaultJavaPath = value ?? string.Empty;
            _appConfigService.Save();
        }
    }

    /// <summary>
    /// 是否优先使用 javaw.exe（无控制台窗口）
    /// </summary>
    public bool PreferJavaw
    {
        get => _appConfigService.Config.PreferJavaw;
        set
        {
            _appConfigService.Config.PreferJavaw = value;
            _appConfigService.Save();
        }
    }

    /// <summary>
    /// 初始化 Java 查找服务
    /// </summary>
    /// <param name="appConfigService">应用配置服务</param>
    public JavaFinderService(IAppConfigService appConfigService)
    {
        _appConfigService = appConfigService;
    }

    /// <summary>
    /// 查找默认的 Java 运行时
    /// </summary>
    /// <returns>默认 Java 安装信息；未找到返回 null</returns>
    public JavaInstallation? FindDefault()
    {
        var all = FindAll();
        if (all.Count == 0)
            return null;

        if (!string.IsNullOrEmpty(DefaultJavaPath))
        {
            var defaultInstall = all.FirstOrDefault(j =>
                string.Equals(j.JavaPath, DefaultJavaPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(j.JavawPath, DefaultJavaPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(j.JavaHome, DefaultJavaPath, StringComparison.OrdinalIgnoreCase));

            if (defaultInstall != null)
                return defaultInstall;
        }

        return all.FirstOrDefault(j => j.Is64Bit) ?? all.FirstOrDefault();
    }

    /// <summary>
    /// 查找系统中所有的 Java 安装实例
    /// </summary>
    /// <returns>Java 安装信息列表，按版本号从高到低排序</returns>
    public List<JavaInstallation> FindAll()
    {
        Log.Debug("开始在系统中查找 Java 安装...");

        var found = new Dictionary<string, JavaInstallation>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<(string Path, bool IsCustom)>();

        var customPaths = GetCustomPaths();
        foreach (var customPath in customPaths)
        {
            var javaExe = GetJavaExecutable(customPath);
            if (javaExe != null)
                candidates.Add((javaExe, true));
        }
        Log.Debug("自定义路径: {Count} 个", customPaths.Count);

        var envVars = new[] { "JAVA_HOME", "JDK_HOME", "JRE_HOME" };
        foreach (var envVar in envVars)
        {
            var javaHome = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(javaHome))
            {
                var javaExe = GetJavaExecutable(javaHome);
                if (javaExe != null)
                    candidates.Add((javaExe, false));
                Log.Debug("{EnvVar}: {Path}", envVar, javaHome);
            }
        }

        var registryJava = FindJavaViaRegistry();
        foreach (var regJava in registryJava)
            candidates.Add((regJava, false));
        Log.Debug("注册表查询完成，找到 {Count} 个", registryJava.Count);

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var path in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var javaExe = GetJavaExecutable(path.Trim());
                if (javaExe != null && File.Exists(javaExe))
                {
                    candidates.Add((javaExe, false));
                }
            }
            Log.Debug("PATH 环境变量已扫描");
        }

        var whereResults = FindJavaViaWhereCommand();
        foreach (var whereJava in whereResults)
            candidates.Add((whereJava, false));
        Log.Debug("where 命令查找完成");

        var commonPaths = ScanCommonInstallPaths();
        foreach (var commonJava in commonPaths)
            candidates.Add((commonJava, false));
        Log.Debug("常见路径扫描完成");

        foreach (var (candidate, isCustom) in candidates)
        {
            var normalized = Path.GetFullPath(candidate);
            if (found.ContainsKey(normalized))
                continue;

            if (!File.Exists(normalized))
                continue;

            // 跳过已知的 Java 路径垫片（如 Oracle javapath），这些不是真正的 Java 安装
            if (IsKnownShimPath(normalized))
            {
                Log.Debug("跳过 Java 路径垫片: {Path}", normalized);
                continue;
            }

            var info = VerifyJava(normalized, isCustom);
            if (info != null)
            {
                // 额外安全检查：验证推导出的 JAVA_HOME 是否有可用的 bin 目录
                if (!IsUsableJavaHome(info.JavaHome))
                {
                    Log.Debug("跳过无法作为 JAVA_HOME 的路径: {Path} (JavaHome: {JavaHome})",
                        normalized, info.JavaHome);
                    continue;
                }

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
    /// 验证 Java 可执行文件的有效性
    /// </summary>
    /// <param name="javaPath">java.exe 或 javaw.exe 完整路径</param>
    /// <returns>Java 安装信息对象；验证失败返回 null</returns>
    public JavaInstallation? Verify(string javaPath)
    {
        if (string.IsNullOrEmpty(javaPath))
            return null;

        // 拒绝已知的路径垫片（如 Oracle javapath）
        if (IsKnownShimPath(javaPath))
        {
            Log.Warning("拒绝验证 Java 路径垫片: {Path}", javaPath);
            return null;
        }

        return VerifyJava(javaPath, false);
    }

    /// <summary>
    /// 添加用户自定义的 Java 路径
    /// </summary>
    /// <param name="javaHomePath">JAVA_HOME 根目录路径</param>
    public void AddCustomPath(string javaHomePath)
    {
        if (string.IsNullOrEmpty(javaHomePath))
            return;

        var normalized = Path.GetFullPath(javaHomePath);
        var customPaths = _appConfigService.Config.CustomJavaPaths;

        if (!customPaths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            customPaths.Add(normalized);
            _appConfigService.Save();
            Log.Information("➕ 已添加自定义 Java 路径: {Path}", normalized);
        }
    }

    /// <summary>
    /// 移除用户自定义的 Java 路径
    /// </summary>
    /// <param name="javaHomePath">JAVA_HOME 根目录路径</param>
    public void RemoveCustomPath(string javaHomePath)
    {
        if (string.IsNullOrEmpty(javaHomePath))
            return;

        var normalized = Path.GetFullPath(javaHomePath);
        var customPaths = _appConfigService.Config.CustomJavaPaths;

        var removed = customPaths.RemoveAll(p =>
            string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));

        if (removed > 0)
        {
            _appConfigService.Save();
            Log.Information("🗑️ 已移除自定义 Java 路径: {Path}", normalized);
        }
    }

    /// <summary>
    /// 获取所有用户自定义路径
    /// </summary>
    /// <returns>自定义路径列表</returns>
    public List<string> GetCustomPaths()
    {
        return new List<string>(_appConfigService.Config.CustomJavaPaths);
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
    /// 从 Java 安装目录获取 javaw.exe 可执行文件路径
    /// </summary>
    /// <param name="directory">Java 安装根目录</param>
    /// <returns>javaw.exe 完整路径；未找到返回 null</returns>
    private static string? GetJavawExecutable(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        var binDir = Path.Combine(directory, "bin");
        if (Directory.Exists(binDir))
        {
            var javawPath = Path.Combine(binDir, "javaw.exe");
            if (File.Exists(javawPath))
                return javawPath;
        }

        var directPath = Path.Combine(directory, "javaw.exe");
        if (File.Exists(directPath))
            return directPath;

        return null;
    }

    /// <summary>
    /// 通过 Windows 注册表查找 Java 安装路径
    /// </summary>
    /// <returns>java.exe 路径列表</returns>
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
            catch (Exception ex)
            {
                Log.Debug(ex, "Java 路径检查失败");
            }
        }

        return results;
    }

    /// <summary>
    /// 验证 Java 可执行文件的有效性，并提取版本与厂商信息
    /// </summary>
    /// <param name="javaPath">java.exe 或 javaw.exe 完整路径</param>
    /// <param name="isCustom">是否为自定义路径</param>
    /// <returns>Java 安装信息对象；验证失败返回 null</returns>
    /// <remarks>
    /// 如果传入 javaw.exe 路径，自动定位同目录下的 java.exe 进行验证，
    /// 因为 javaw.exe 属于 GUI 子系统，在命令行重定向下可能无 stdout/stderr 输出。
    /// 验证方式：执行 java -version，解析版本、厂商、架构信息。
    /// </remarks>
    private static JavaInstallation? VerifyJava(string javaPath, bool isCustom)
    {
        if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
            return null;

        // 跳过已知的 Java 路径垫片
        if (IsKnownShimPath(javaPath))
            return null;

        // 如果传入的是 javaw.exe，自动转换为同目录的 java.exe
        // javaw.exe 属于 GUI 子系统，-version 输出行为不稳定
        var actualJavaPath = javaPath;
        var fileName = Path.GetFileName(javaPath);
        if (fileName.Equals("javaw.exe", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(javaPath);
            if (dir != null)
            {
                var javaExe = Path.Combine(dir, "java.exe");
                if (File.Exists(javaExe))
                    actualJavaPath = javaExe;
                else
                {
                    Log.Warning("javaw.exe 旁未找到 java.exe，跳过验证: {Path}", javaPath);
                    return null;
                }
            }
        }

        try
        {
            var startInfo = new ProcessStartInfo(actualJavaPath, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Log.Warning("Process.Start 返回 null: {Path}", actualJavaPath);
                return null;
            }

            // 先异步读取输出再等待退出，避免缓冲区满导致死锁
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();

            if (!process.WaitForExit(8000))
            {
                Log.Warning("java -version 执行超时（8s），尝试终止进程: {Path}", actualJavaPath);
                try { process.Kill(); } catch { /* 忽略 */ }
                return null;
            }

            var output = stderrTask.Result;
            if (string.IsNullOrEmpty(output))
                output = stdoutTask.Result;

            if (string.IsNullOrEmpty(output))
            {
                Log.Warning("java -version 无输出: {Path}", actualJavaPath);
                return null;
            }

            // 某些 Java 实现的 -version 退出码可能非 0（如旧版 Oracle Java），
            // 只要有有效输出就视为验证通过
            if (process.ExitCode != 0)
                Log.Debug("java -version 退出码非 0 ({Code})，但有输出，继续验证: {Path}", process.ExitCode, actualJavaPath);

            var version = ParseVersion(output);
            var javaHome = GetJavaHomeFromExecutable(actualJavaPath);
            var javawPath = javaHome != null ? GetJavawExecutable(javaHome) ?? string.Empty : string.Empty;

            Log.Information("✅ Java 验证通过: {Path} | 版本: {Version} | 厂商: {Vendor} | 64位: {Is64Bit}",
                actualJavaPath, version?.ToString() ?? "未知", ParseVendor(output),
                output.Contains("64-Bit", StringComparison.OrdinalIgnoreCase));

            return new JavaInstallation
            {
                JavaPath = actualJavaPath,
                JavawPath = javawPath,
                JavaHome = javaHome ?? string.Empty,
                Version = version,
                VersionString = version?.ToString() ?? string.Empty,
                Is64Bit = output.Contains("64-Bit", StringComparison.OrdinalIgnoreCase),
                Vendor = ParseVendor(output),
                IsCustom = isCustom
            };
        }
        catch (Exception ex)
        {
            Log.Warning("验证 Java 失败 {Path}: {Msg}", actualJavaPath, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 从 java -version 输出中解析版本号
    /// </summary>
    /// <param name="versionOutput">java -version 命令输出</param>
    /// <returns>版本号对象；解析失败返回 null</returns>
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

    /// <summary>
    /// 检测是否为已知的 Java 路径垫片（Shim）目录。
    /// Oracle Java 安装程序会在 Common Files\Oracle\javapath 或
    /// Program Files\Java\javapath 下创建 java.exe 跳转垫片，
    /// 这些不是真正的 Java 安装，无法作为 JAVA_HOME 使用。
    /// </summary>
    /// <param name="javaPath">java.exe 完整路径</param>
    /// <returns>是否为垫片路径</returns>
    private static bool IsKnownShimPath(string javaPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(javaPath);
            if (string.IsNullOrEmpty(dir))
                return false;

            // Oracle javapath shim — 最常见的垫片路径
            if (Path.GetFileName(dir).Equals("javapath", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证推导出的 JAVA_HOME 是否为可用的 Java 安装根目录。
    /// 可用的 JAVA_HOME 必须包含 bin\java.exe 和 bin\javaw.exe。
    /// </summary>
    /// <param name="javaHome">JAVA_HOME 路径</param>
    /// <returns>是否为可用的 Java 安装根目录</returns>
    private static bool IsUsableJavaHome(string? javaHome)
    {
        if (string.IsNullOrEmpty(javaHome) || !Directory.Exists(javaHome))
            return false;

        var binDir = Path.Combine(javaHome, "bin");
        if (!Directory.Exists(binDir))
            return false;

        var javaExe = Path.Combine(binDir, "java.exe");
        if (!File.Exists(javaExe))
            return false;

        return true;
    }
}
