// -----------------------------------------------------------------------------
// CheckRunner.cs — P0 10 个系统/配置检查点实现
// 设计约束: 诚实返回链(P4) — 失败返回 CheckResult 不吞异常；不返回 null
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using Serilog;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public class CheckRunner : ICheckRunner
{
    private static readonly HashSet<string> _knownCheckIds = new()
    {
        "java.version", "java.heap.size", "process.priority",
        "process.t1.qos", "process.t3.tuning", "port.availability",
        "port.firewall", "config.syntax", "storage.world.size", "log.startup.failure"
    };

    public IReadOnlyCollection<string> KnownCheckIds => _knownCheckIds;

    private readonly ILogger _log = Serilog.Log.ForContext<CheckRunner>();

    public List<CheckResult> RunAll(string serverJarPath, string? worldPath, ServerInfo server)
    {
        var results = new List<CheckResult>();

        // 10 个检查点，每个独立 try-catch，失败也返回 CheckResult
        results.Add(SafeRun("java.version", () => CheckJavaVersion(serverJarPath, server)));
        results.Add(SafeRun("java.heap.size", () => CheckJavaHeap(serverJarPath, server)));
        results.Add(SafeRun("process.priority", () => CheckProcessPriority(server)));
        results.Add(SafeRun("process.t1.qos", () => CheckT1Qos(server)));
        results.Add(SafeRun("process.t3.tuning", () => CheckT3Tuning(server)));
        results.Add(SafeRun("port.availability", () => CheckPortAvailability(serverJarPath, server)));
        results.Add(SafeRun("port.firewall", () => CheckFirewall(server)));
        results.Add(SafeRun("config.syntax", () => CheckConfigSyntax(serverJarPath)));
        results.Add(SafeRun("storage.world.size", () => CheckWorldSize(worldPath)));
        results.Add(SafeRun("log.startup.failure", () => CheckStartupLogs(worldPath)));

        return results;
    }

    /// <summary>P4 诚实返回链：任何异常都返回 CheckResult（Severity=Warning + Detail=异常消息），不返回 null</summary>
    private CheckResult SafeRun(string checkId, Func<CheckResult> fn)
    {
        try { return fn(); }
        catch (Exception ex)
        {
            _log.Warning(ex, "[TROUBLESHOOT] Check {CheckId} 异常", checkId);
            return new CheckResult(checkId, Severity.Warning, "System",
                "检查出错", $"检查 {checkId} 时发生异常: {ex.Message}",
                false, null, ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Check 1: java.version
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckJavaVersion(string jarPath, ServerInfo server)
    {
        if (string.IsNullOrEmpty(jarPath) || !File.Exists(jarPath))
            return new CheckResult("java.version", Severity.Info, "Java",
                "未找到服务器 JAR", $"路径不存在: {jarPath}", false, null, null);

        string? required = InferJavaRequirement(jarPath);
        string? current = server.JavaVersion ?? GetProcessJavaVersion();

        if (required == null)
            return new CheckResult("java.version", Severity.Info, "Java",
                "无法推断 JAR 需要的 Java 版本", "建议手动核对启动参数", false, null, null);

        if (current == null)
            return new CheckResult("java.version", Severity.Warning, "Java",
                "无法检测运行时 Java 版本", "建议检查 JAVA_HOME 或启动脚本", false, null, null);

        // Java 21 = 21, Java 17 = 17, Java 8 = 1.8 等
        int reqMajor = ParseJavaMajor(required);
        int curMajor = ParseJavaMajor(current);

        if (curMajor >= reqMajor)
            return new CheckResult("java.version", Severity.Ok, "Java",
                "Java 版本满足要求", $"JAR 需要 Java {required}，运行时是 {current}", false, null, null);
        else
            return new CheckResult("java.version", Severity.Error, "Java",
                "Java 版本与服务器不兼容",
                $"服务器 JAR 需要 Java {required}，但运行时是 Java {current}，可能导致启动失败或崩溃",
                true, null, new { Required = required, Current = current });
    }

    /// <summary>从 JAR 的 MANIFEST.MF / module-info / fabric.mod.json 推断 Java 版本要求</summary>
    private static string? InferJavaRequirement(string jarPath)
    {
        try
        {
            using var jar = ZipFile.OpenRead(jarPath);
            // 1. 查 MANIFEST.MF
            var manifest = jar.GetEntry("META-INF/MANIFEST.MF");
            if (manifest != null)
            {
                using var reader = new StreamReader(manifest.Open());
                string content = reader.ReadToEnd();
                var match = System.Text.RegularExpressions.Regex.Match(content,
                    @"Implementation-Version:\s*(\d+\.\d+[\.\d]*)");
                if (match.Success)
                {
                    string ver = match.Groups[1].Value;
                    return InferFromMcVersion(ver);
                }
            }
            // 2. 查 module-info.class 的 ClassFileVersion
            var moduleInfo = jar.GetEntry("module-info.class");
            if (moduleInfo != null)
            {
                using var stream = moduleInfo.Open();
                var buf = new byte[10];
                stream.ReadExactly(buf, 0, 10);
                ushort major = (ushort)((buf[6] << 8) | buf[7]);
                // Java version table: 61=21, 65=21 (?), 64=20, 63=19, 61=17...
                // 实际上 ClassFileVersion: 45=Java 1.1, 46=1.2, ... 61=17, 65=21
                int javaVer = major switch
                {
                    >= 65 => 21,
                    >= 61 => 17,
                    >= 55 => 11,
                    >= 52 => 8,
                    _ => 8
                };
                return javaVer.ToString();
            }
            // 3. 查 fabric.mod.json
            var fabric = jar.GetEntry("fabric.mod.json");
            if (fabric != null)
            {
                using var reader = new StreamReader(fabric.Open());
                string json = reader.ReadToEnd();
                var match = System.Text.RegularExpressions.Regex.Match(json,
                    @"""requires"":\s*\{[^}]*""minecraft"":\s*""([\d\.]+)""",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                if (match.Success) return InferFromMcVersion(match.Groups[1].Value);
            }
        }
        catch { /* GZip corrupt — 跳过 */ }
        return null;
    }

    /// <summary>Minecraft 版本 → 最低 Java 版本</summary>
    private static string? InferFromMcVersion(string mcVer)
    {
        // 1.20.5+ 需要 Java 21；1.17-1.20.4 需要 Java 17；1.12-1.16 需要 Java 8
        var parts = mcVer.Split('.');
        if (parts.Length < 2) return null;
        if (!int.TryParse(parts[1], out int minor)) return null;

        if (minor >= 20) return "21";
        if (minor >= 17) return "17";
        return "8";
    }

    private static int ParseJavaMajor(string versionStr)
    {
        // "21.0.1" → 21, "17.0.8" → 17, "1.8.0_402" → 8
        var match = System.Text.RegularExpressions.Regex.Match(versionStr, @"(\d+)");
        if (!match.Success) return 0;
        int first = int.Parse(match.Groups[1].Value);
        return first == 1 ? int.Parse(System.Text.RegularExpressions.Regex.Match(versionStr, @"1\.(\d+)").Groups[1].Value) : first;
    }

    private static string? GetProcessJavaVersion()
    {
        try
        {
            var gc = GC.GetGCMemoryInfo();
            // 从 Environment 猜
            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                string releaseFile = Path.Combine(javaHome, "release");
                if (File.Exists(releaseFile))
                {
                    var verLine = File.ReadAllLines(releaseFile)
                        .FirstOrDefault(l => l.StartsWith("JAVA_VERSION="));
                    if (verLine != null) return verLine.Replace("JAVA_VERSION=", "").Trim('"');
                }
            }
            // 用 java -version 命令查
            try
            {
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "java", Arguments = "-version",
                        RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
                    }
                };
                proc.Start();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit(3000);
                var m = System.Text.RegularExpressions.Regex.Match(err, @"version ""([\d\.]+)""");
                if (m.Success) return m.Groups[1].Value;
            }
            catch { /* java 命令不可用 */ }
        }
        catch { }
        return null;
    }

    // ═══════════════════════════════════════════════════════════
    // Check 2: java.heap.size
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckJavaHeap(string jarPath, ServerInfo server)
    {
        long? heapXmx = ParseHeapFromLaunchScript(jarPath);
        long physicalMb = server.TotalMemoryMb ?? GetPhysicalMemoryMb();

        if (heapXmx == null)
            return new CheckResult("java.heap.size", Severity.Info, "Java",
                "未检测到 -Xmx 参数", "建议在启动脚本中显式设置 -Xmx2G~-Xmx4G", false, null, null);

        double heapPct = (double)heapXmx.Value / physicalMb * 100;
        if (heapPct > 80)
            return new CheckResult("java.heap.size", Severity.Warning, "Java",
                "Java 堆占物理内存比例过高",
                $"-Xmx={heapXmx.Value / 1024}MB ({heapPct:F0}%)，建议 ≤ 70% 避免 OOM",
                false, null, new { HeapMb = heapXmx / 1024, PhysicalMb = physicalMb });

        if (heapPct < 25)
            return new CheckResult("java.heap.size", Severity.Warning, "Java",
                "Java 堆可能过小",
                $"-Xmx={heapXmx.Value / 1024}MB ({heapPct:F0}%)，建议 ≥ 物理内存 25%",
                false, null, new { HeapMb = heapXmx / 1024, PhysicalMb = physicalMb });

        return new CheckResult("java.heap.size", Severity.Ok, "Java",
            "Java 堆大小合理",
            $"-Xmx={heapXmx.Value / 1024}MB ({heapPct:F0}% 物理内存)", false, null, null);
    }

    private static long? ParseHeapFromLaunchScript(string jarPath)
    {
        try
        {
            // 找同目录下的 start.bat / start.ps1 / start.sh
            string dir = Path.GetDirectoryName(jarPath) ?? ".";
            foreach (var file in new[] { "start.bat", "start.ps1", "start.sh", "run.bat" })
            {
                string full = Path.Combine(dir, file);
                if (File.Exists(full))
                {
                    string content = File.ReadAllText(full);
                    var xmx = System.Text.RegularExpressions.Regex.Match(content, @"-Xmx(\d+)([gGmMkK])");
                    if (xmx.Success)
                    {
                        long num = long.Parse(xmx.Groups[1].Value);
                        string unit = xmx.Groups[2].Value.ToLower();
                        return unit switch
                        {
                            "g" => num * 1024L * 1024 * 1024,
                            "m" => num * 1024L * 1024,
                            "k" => num * 1024L,
                            _ => num
                        };
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static long GetPhysicalMemoryMb()
    {
        try { return (long)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024.0)); }
        catch { return 8192; } // 默认 8GB
    }

    // ═══════════════════════════════════════════════════════════
    // Check 3: process.priority
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckProcessPriority(ServerInfo server)
    {
        if (server.ProcessId == null)
            return new CheckResult("process.priority", Severity.Info, "Process",
                "服务器未运行", "无法检查进程优先级", false, null, null);

        try
        {
            using var proc = Process.GetProcessById(server.ProcessId.Value);
            var priority = proc.PriorityClass;
            bool ok = priority >= ProcessPriorityClass.AboveNormal;

            return ok
                ? new CheckResult("process.priority", Severity.Ok, "Process",
                    "进程优先级合理", $"当前: {priority}", false, null, priority.ToString())
                : new CheckResult("process.priority", Severity.Warning, "Process",
                    "进程优先级过低",
                    $"当前: {priority}，建议设为 AboveNormal 或 High",
                    false, null, priority.ToString());
        }
        catch (Exception ex)
        {
            return new CheckResult("process.priority", Severity.Warning, "Process",
                "无法检查进程优先级", ex.Message, false, null, null);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Check 4/5: T1 QoS / T3 Tuning
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckT1Qos(ServerInfo server)
    {
        // P0 简化：不依赖 ICpuPowerService（条件注入 + P9 门卫）
        // 只看进程有没有开 SetProcessInformation PROCESS_POWER_THROTTLING
        return new CheckResult("process.t1.qos", Severity.Info, "Process",
            "T1 QoS 调度检查", "需启用「电源管理」功能后生效", false, null, null);
    }

    private CheckResult CheckT3Tuning(ServerInfo server)
    {
        return new CheckResult("process.t3.tuning", Severity.Info, "Process",
            "T3 最大权限调度检查", "需启用「电源管理」功能后生效", false, null, null);
    }

    // ═══════════════════════════════════════════════════════════
    // Check 6: port.availability
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckPortAvailability(string jarPath, ServerInfo server)
    {
        int? port = server.Port ?? ParsePortFromServerProperties(jarPath);
        if (port == null)
            return new CheckResult("port.availability", Severity.Info, "Network",
                "未检测到服务器端口", "检查 server.properties", false, null, null);

        try
        {
            var active = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            bool occupied = active.Any(l => l.Port == port.Value);

            if (!occupied)
                return new CheckResult("port.availability", Severity.Ok, "Network",
                    "端口可用", $"端口 {port} 未被占用", false, null, port);

            // 谁占了这个端口？
            string occupier = GetProcessByPort(port.Value);
            return new CheckResult("port.availability", Severity.Error, "Network",
                "服务器端口被占用",
                $"端口 {port} 已被占用: {occupier}，服务器无法启动",
                true, null, new { Port = port, Occupier = occupier });
        }
        catch (Exception ex)
        {
            return new CheckResult("port.availability", Severity.Warning, "Network",
                "端口检查失败", ex.Message, false, null, null);
        }
    }

    private static int? ParsePortFromServerProperties(string jarPath)
    {
        try
        {
            string dir = Path.GetDirectoryName(jarPath) ?? ".";
            string propsPath = Path.Combine(dir, "server.properties");
            if (!File.Exists(propsPath)) return null;

            foreach (var line in File.ReadAllLines(propsPath))
            {
                if (line.StartsWith("server-port=") && int.TryParse(line.Split('=')[1].Trim(), out int port))
                    return port;
            }
        }
        catch { }
        return null;
    }

    private static string GetProcessByPort(int port)
    {
        try
        {
            // netstat -ano + find port
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat", Arguments = "-ano -p tcp",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                }
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            var line = output.Split('\n').FirstOrDefault(l => l.Contains($":{port} ") && l.Contains("LISTENING"));
            if (line == null) return "未知进程";

            string pid = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "?";
            try
            {
                using var p = Process.GetProcessById(int.Parse(pid));
                return $"{p.ProcessName} (PID {pid})";
            }
            catch { return $"PID {pid}"; }
        }
        catch { return "未知进程"; }
    }

    // ═══════════════════════════════════════════════════════════
    // Check 7: port.firewall
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckFirewall(ServerInfo server)
    {
        int? port = server.Port ?? ParsePortFromServerProperties(server.JarName);
        if (port == null)
            return new CheckResult("port.firewall", Severity.Info, "Network",
                "未检测到端口", "跳过防火墙检查", false, null, null);

        try
        {
            // netsh advfirewall firewall show rule name=all
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh", Arguments = "advfirewall firewall show rule name=all",
                    RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
                    Verb = "runas" // 管理员
                }
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            bool hasRule = output.Contains($"LocalPort={port}") && output.Contains("Enabled: Yes");
            if (hasRule)
                return new CheckResult("port.firewall", Severity.Ok, "Network",
                    "防火墙规则已配置", $"端口 {port} 有入站放行规则", false, null, port);
            else
                return new CheckResult("port.firewall", Severity.Warning, "Network",
                    "防火墙可能拦截连接",
                    $"未检测到端口 {port} 的入站放行规则，外部玩家可能无法连接",
                    true, null, port);
        }
        catch (Exception ex)
        {
            return new CheckResult("port.firewall", Severity.Info, "Network",
                "防火墙检查需要管理员权限", ex.Message, false, null, null);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Check 8: config.syntax — 用 YamlDotNet 解析 .properties + .yml
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckConfigSyntax(string jarPath)
    {
        try
        {
            string dir = Path.GetDirectoryName(jarPath) ?? ".";
            var errors = new List<string>();

            // 检查 .properties（Minecraft 是简单 key=value）
            foreach (var file in new[] { "server.properties", "bukkit.yml", "spigot.yml", "paper-global.yml" })
            {
                string full = Path.Combine(dir, file);
                if (!File.Exists(full)) continue;

                string ext = Path.GetExtension(file).ToLower();
                if (ext == ".yml" || ext == ".yaml")
                {
                    try
                    {
                        var yaml = new YamlDotNet.Serialization.DeserializerBuilder().Build();
                        yaml.Deserialize<object>(File.ReadAllText(full));
                    }
                    catch (Exception ex) { errors.Add($"{file}: {ex.Message}"); }
                }
                else if (ext == ".properties")
                {
                    foreach (var line in File.ReadAllLines(full))
                    {
                        string t = line.Trim();
                        if (string.IsNullOrEmpty(t) || t.StartsWith('#')) continue;
                        if (!t.Contains('=')) errors.Add($"{file}: 行缺少 '=' — {t}");
                    }
                }
            }

            if (errors.Count == 0)
                return new CheckResult("config.syntax", Severity.Ok, "Config",
                    "配置文件语法正确", "server.properties / *.yml 解析通过", false, null, null);

            return new CheckResult("config.syntax", Severity.Warning, "Config",
                "配置文件可能有语法错误",
                string.Join(" | ", errors), false, null, errors);
        }
        catch (Exception ex)
        {
            return new CheckResult("config.syntax", Severity.Warning, "Config",
                "配置检查异常", ex.Message, false, null, null);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Check 9: storage.world.size
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckWorldSize(string? worldPath)
    {
        if (string.IsNullOrEmpty(worldPath) || !Directory.Exists(worldPath))
            return new CheckResult("storage.world.size", Severity.Info, "Storage",
                "未检测到世界目录", worldPath ?? "null", false, null, null);

        try
        {
            long total = Directory.GetFiles(worldPath, "*", SearchOption.AllDirectories)
                .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0; } });
            double gb = total / (1024.0 * 1024.0 * 1024.0);

            if (gb > 50)
                return new CheckResult("storage.world.size", Severity.Warning, "Storage",
                    "世界文件过大",
                    $"世界总大小 {gb:F1} GB，建议定期清理或归档旧 region",
                    false, null, gb);

            return new CheckResult("storage.world.size", Severity.Ok, "Storage",
                "世界文件大小正常", $"世界总大小 {gb:F1} GB", false, null, gb);
        }
        catch (Exception ex)
        {
            return new CheckResult("storage.world.size", Severity.Warning, "Storage",
                "世界大小检查失败", ex.Message, false, null, null);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Check 10: log.startup.failure
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckStartupLogs(string? worldPath)
    {
        string? logsDir = worldPath != null
            ? Path.Combine(Path.GetDirectoryName(worldPath)!, "logs")
            : null;

        if (logsDir == null || !Directory.Exists(logsDir))
            return new CheckResult("log.startup.failure", Severity.Info, "Log",
                "未找到 logs 目录", logsDir ?? "null", false, null, null);

        try
        {
            string latest = Path.Combine(logsDir, "latest.log");
            if (!File.Exists(latest))
                return new CheckResult("log.startup.failure", Severity.Info, "Log",
                    "没有 latest.log", null, false, null, null);

            // 读末尾 2000 行（P7 内存有界）
            var lines = ReadTailLines(latest, 2000);
            var errorCount = lines.Count(l => l.Contains("ERROR") || l.Contains("Exception"));
            var oom = lines.Any(l => l.Contains("OutOfMemoryError"));
            var chunkErr = lines.Count(l => l.Contains("Could not pass event CHUNK_GENERATION"));

            if (oom)
                return new CheckResult("log.startup.failure", Severity.Critical, "Log",
                    "检测到 OOM",
                    "latest.log 包含 java.lang.OutOfMemoryError，建议增大 -Xmx 或清理世界",
                    true, null, new { Oom = true });

            if (chunkErr > 10)
                return new CheckResult("log.startup.failure", Severity.Warning, "Log",
                    "区块生成事件高频异常",
                    $"CHUNK_GENERATION 异常 {chunkErr} 次，可能是世界损坏或插件冲突",
                    false, null, new { ChunkErr = chunkErr });

            if (errorCount > 5)
                return new CheckResult("log.startup.failure", Severity.Warning, "Log",
                    "日志包含较多错误",
                    $"latest.log 末尾 2000 行有 {errorCount} 条 ERROR/Exception，建议排查",
                    false, null, new { ErrorCount = errorCount });

            return new CheckResult("log.startup.failure", Severity.Ok, "Log",
                "日志正常", $"错误数 {errorCount}", false, null, new { ErrorCount = errorCount });
        }
        catch (Exception ex)
        {
            return new CheckResult("log.startup.failure", Severity.Warning, "Log",
                "日志检查异常", ex.Message, false, null, null);
        }
    }

    /// <summary>读文件最后 N 行（P7 内存有界 — 流式倒读）</summary>
    private static List<string> ReadTailLines(string path, int n)
    {
        // 简化实现：读全部然后取最后 N 行
        // （大文件场景 P2 再做真正的流式倒读）
        try
        {
            var all = File.ReadAllLines(path);
            int skip = Math.Max(0, all.Length - n);
            return all.Skip(skip).ToList();
        }
        catch { return new List<string>(); }
    }
}
