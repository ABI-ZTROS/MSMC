// -----------------------------------------------------------------------------
// CheckRunner.cs — P0 12 个系统/配置检查点实现
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
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace io.NET.ZTR_OS.Features.Troubleshooting.Services;

public class CheckRunner : ICheckRunner
{
    private static readonly HashSet<string> _knownCheckIds = new()
    {
        "java.version", "java.heap.size", "process.priority",
        "process.t1.qos", "process.t3.tuning", "port.availability",
        "port.firewall", "config.syntax", "storage.world.size",
        "log.startup.failure", "log.outmemory", "log.chunk.generation"
    };

    public IReadOnlyCollection<string> KnownCheckIds => _knownCheckIds;

    private readonly ILogger _log = Serilog.Log.ForContext<CheckRunner>();

    public List<CheckResult> RunAll(string serverJarPath, string? worldPath, ServerInfo server)
    {
        var results = new List<CheckResult>();

        // 12 个检查点，每个独立 try-catch，失败也返回 CheckResult
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
        results.Add(SafeRun("log.outmemory", () => CheckLogOutOfMemory(worldPath)));
        results.Add(SafeRun("log.chunk.generation", () => CheckLogChunkGeneration(worldPath)));

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
        // 1.20.5+ → Java 21；1.17-1.20.4 → Java 17；1.12-1.16 → Java 8；1.8-1.11 → Java 8
        var parts = mcVer.Split('.');
        if (parts.Length < 2) return null;
        if (!int.TryParse(parts[1], out int minor)) return null;

        if (minor > 20) return "21";
        if (minor == 20)
        {
            // 1.20.0–1.20.4 只需要 Java 17，1.20.5 起才要求 Java 21
            if (parts.Length >= 3 && int.TryParse(parts[2], out int patch) && patch >= 5) return "21";
            return "17";
        }
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
        if (physicalMb <= 0) physicalMb = 8192; // 防御：拿不到物理内存时按 8GB 估

        if (heapXmx == null)
            return new CheckResult("java.heap.size", Severity.Info, "Java",
                "未检测到 -Xmx 参数", "建议在启动脚本中显式设置 -Xmx2G~-Xmx4G", false, null, null);

        // heapXmx 是字节；先换算成 MB 再算占比，避免单位错乱（此前 /1024 当 MB 实为 KB）
        double heapMb = heapXmx.Value / (1024.0 * 1024.0);
        double heapPct = heapMb / physicalMb * 100;
        if (heapPct > 80)
            return new CheckResult("java.heap.size", Severity.Warning, "Java",
                "Java 堆占物理内存比例过高",
                $"-Xmx={heapMb:F0}MB ({heapPct:F0}%)，建议 ≤ 70% 避免 OOM",
                false, null, new { HeapMb = (long)heapMb, PhysicalMb = physicalMb });

        if (heapPct < 25)
            return new CheckResult("java.heap.size", Severity.Warning, "Java",
                "Java 堆可能过小",
                $"-Xmx={heapMb:F0}MB ({heapPct:F0}%)，建议 ≥ 物理内存 25%",
                false, null, new { HeapMb = (long)heapMb, PhysicalMb = physicalMb });

        return new CheckResult("java.heap.size", Severity.Ok, "Java",
            "Java 堆大小合理",
            $"-Xmx={heapMb:F0}MB ({heapPct:F0}% 物理内存)", false, null, new { HeapMb = (long)heapMb, PhysicalMb = physicalMb });
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
                        string unit = xmx.Groups[2].Value.ToLower(System.Globalization.CultureInfo.InvariantCulture);
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
    // 真实实现：读取进程的电源节流状态（GetProcessInformation）与
    // I/O / 内存优先级，判断调度是否被系统限制。
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckT1Qos(ServerInfo server)
    {
        if (server.ProcessId == null)
            return new CheckResult("process.t1.qos", Severity.Info, "Process",
                "服务器未运行", "无法检查 T1 QoS 调度", false, null, null);

        try
        {
            using var proc = Process.GetProcessById(server.ProcessId.Value);

            // 查询电源节流（PROCESS_POWER_THROTTLING_STATE, class=8）
            bool throttled = false;
            try
            {
                var state = new ProcessPowerThrottlingState();
                if (GetProcessInformation(proc.Handle, ProcessInfoClassPowerThrottling,
                        ref state, Marshal.SizeOf<ProcessPowerThrottlingState>()))
                {
                    // ControlMask 位0=执行节流开关；StateMask 位0=节流已开启
                    throttled = (state.ControlMask & 1) != 0 && (state.StateMask & 1) != 0;
                }
            }
            catch { /* 非 Windows 或权限不足，忽略 */ }

            var priority = proc.PriorityClass;

            if (throttled)
                return new CheckResult("process.t1.qos", Severity.Warning, "Process",
                    "进程被电源节流调度",
                    "系统对服务器进程开启了执行节流（Power Throttling），tick 稳定性可能受影响",
                    true, null, new { Throttled = true, Priority = priority.ToString() });

            if (priority >= ProcessPriorityClass.AboveNormal)
                return new CheckResult("process.t1.qos", Severity.Ok, "Process",
                    "T1 QoS 调度正常",
                    $"优先级 {priority}，未受电源节流", false, null, new { Throttled = false, Priority = priority.ToString() });

            return new CheckResult("process.t1.qos", Severity.Warning, "Process",
                "进程优先级偏低",
                $"当前优先级 {priority}，建议 AboveNormal 或 High",
                false, null, new { Throttled = false, Priority = priority.ToString() });
        }
        catch (Exception ex)
        {
            return new CheckResult("process.t1.qos", Severity.Warning, "Process",
                "无法检查 T1 QoS", ex.Message, false, null, null);
        }
    }

    private CheckResult CheckT3Tuning(ServerInfo server)
    {
        if (server.ProcessId == null)
            return new CheckResult("process.t3.tuning", Severity.Info, "Process",
                "服务器未运行", "无法检查 T3 最大权限调度", false, null, null);

        try
        {
            using var proc = Process.GetProcessById(server.ProcessId.Value);

            // I/O 优先级（0=VeryLow,1=Low,2=Normal,3=High,4=Critical）
            int ioPriority = -1;
            try { ioPriority = (int)GetProcessIoPriority(proc.Handle); }
            catch { }

            // 内存优先级（PROCESS_MEMORY_PRIORITY, class=10，0~5，默认 5=Normal）
            uint memPriority = 5;
            try
            {
                var mem = new ProcessMemoryPriority();
                if (GetProcessInformation(proc.Handle, ProcessInfoClassMemoryPriority,
                        ref mem, Marshal.SizeOf<ProcessMemoryPriority>()))
                {
                    memPriority = mem.Priority;
                }
            }
            catch { }

            bool ioOk = ioPriority >= 2;   // Normal 及以上
            bool memOk = memPriority >= 4; // 高于 VeryLow 的默认即可，5=Normal 最佳

            if (!ioOk || !memOk)
                return new CheckResult("process.t3.tuning", Severity.Warning, "Process",
                    "进程 I/O / 内存优先级偏低",
                    $"I/O 优先级 {(ioPriority < 0 ? "未知" : ioPriority)}，内存优先级 {memPriority}（建议 I/O ≥ Normal，内存 ≥ 4）",
                    false, null, new { IoPriority = ioPriority, MemoryPriority = (int)memPriority });

            return new CheckResult("process.t3.tuning", Severity.Ok, "Process",
                "T3 最大权限调度正常",
                $"I/O 优先级 {ioPriority}，内存优先级 {memPriority}", false, null,
                new { IoPriority = ioPriority, MemoryPriority = (int)memPriority });
        }
        catch (Exception ex)
        {
            return new CheckResult("process.t3.tuning", Severity.Warning, "Process",
                "无法检查 T3 调度", ex.Message, false, null, null);
        }
    }

    // ── Windows P/Invoke：进程电源节流 / 内存优先级 / I/O 优先级查询 ──

    private const int ProcessInfoClassPowerThrottling = 8;
    private const int ProcessInfoClassMemoryPriority = 10;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryPriority
    {
        public uint Priority;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessInformation(
        IntPtr hProcess, int processInformationClass,
        ref ProcessPowerThrottlingState processInformation, int processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessInformation(
        IntPtr hProcess, int processInformationClass,
        ref ProcessMemoryPriority processInformation, int processInformationSize);

    [DllImport("kernel32.dll")]
    private static extern uint GetProcessIoPriority(IntPtr hProcess);

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
            // 查询入站规则全量（只读查询不需要管理员，去掉之前的 Verb=runas 必败路径）
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall show rule name=all dir=in",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            // netsh 输出按「----」分隔成规则块，每块包含端口/本地端口 + 已启用/Enabled 字段。
            // 同时兼容中英文系统输出。
            bool hasRule = false;
            bool enabledRule = false;
            foreach (var block in output.Split(new[] { "-------------------" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string? portLine = block.Split('\n', '\r')
                    .FirstOrDefault(l => l.Contains("LocalPort", StringComparison.OrdinalIgnoreCase)
                                      || l.Contains("本地端口", StringComparison.Ordinal));
                if (portLine == null) continue;

                // 提取端口值：LocalPort:  25565 或 本地端口: 25565（可能带范围 25565-25566）
                var value = portLine.Split(':', 2).Skip(1).FirstOrDefault()?.Trim();
                if (string.IsNullOrEmpty(value)) continue;

                var portParts = value.Split('-');
                if (!int.TryParse(portParts[0].Trim(), out int lo)) continue;
                int hi = portParts.Length > 1 && int.TryParse(portParts[1].Trim(), out var h) ? h : lo;
                if (port.Value < lo || port.Value > hi) continue;

                hasRule = true;
                // 该规则块里「已启用」行
                string enableLine = block.Split('\n', '\r')
                    .FirstOrDefault(l => l.Contains("Enabled", StringComparison.OrdinalIgnoreCase)
                                      || l.Contains("已启用", StringComparison.Ordinal)) ?? string.Empty;
                if (enableLine.Contains("Yes", StringComparison.OrdinalIgnoreCase)
                    || enableLine.Contains("是", StringComparison.Ordinal))
                {
                    enabledRule = true;
                    break;
                }
            }

            if (hasRule && enabledRule)
                return new CheckResult("port.firewall", Severity.Ok, "Network",
                    "防火墙规则已配置", $"端口 {port} 有入站放行规则", false, null, port);

            if (hasRule)
                return new CheckResult("port.firewall", Severity.Warning, "Network",
                    "端口规则存在但未启用",
                    $"端口 {port} 的入站规则未启用，外部玩家可能无法连接",
                    true, null, port);

            return new CheckResult("port.firewall", Severity.Warning, "Network",
                "防火墙可能拦截连接",
                $"未检测到端口 {port} 的入站放行规则，外部玩家可能无法连接",
                true, null, port);
        }
        catch (Exception ex)
        {
            return new CheckResult("port.firewall", Severity.Info, "Network",
                "防火墙检查不可用", ex.Message, false, null, null);
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

                string ext = Path.GetExtension(file).ToLower(System.Globalization.CultureInfo.InvariantCulture);
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

            // 读末尾 2000 行（P7 内存有界 — 流式倒读）
            // 注：OOM / CHUNK_GENERATION 由 log.outmemory / log.chunk.generation
            // 专门检查负责，这里只做通用启动失败信号检测，避免两处重复判断给出矛盾结论。
            var lines = ReadTailLines(latest, 2000);
            var errorCount = lines.Count(l => l.Contains("ERROR") || l.Contains("Exception"));

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

    /// <summary>流式倒读文件最后 N 行（P7 内存有界：每次只读固定大小的尾部窗口）</summary>
    private static List<string> ReadTailLines(string path, int n)
    {
        var result = new List<string>();
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            const int chunkSize = 32 * 1024;
            long pos = fs.Length;
            var buf = new byte[chunkSize];
            var tail = new StringBuilder();
            int newlines = 0;

            while (pos > 0 && newlines <= n)
            {
                long start = Math.Max(0, pos - chunkSize);
                int len = (int)(pos - start);
                fs.Position = start;
                fs.ReadExactly(buf, 0, len);
                pos = start;

                for (int i = 0; i < len; i++)
                {
                    if (buf[i] == (byte)'\n') newlines++;
                }
                tail.Insert(0, Encoding.UTF8.GetString(buf, 0, len));
                if (newlines > n) break;
            }

            var text = tail.ToString().TrimEnd('\r', '\n');
            if (text.Length == 0) return result;

            var parts = text.Split('\n');
            int skip = Math.Max(0, parts.Length - n);
            for (int i = skip; i < parts.Length; i++)
                result.Add(parts[i].TrimEnd('\r'));
        }
        catch { /* 读失败返回空列表 */ }
        return result;
    }

    // ═══════════════════════════════════════════════════════════
    // Check 11: log.outmemory — 扫描 OutOfMemoryError
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckLogOutOfMemory(string? worldPath)
    {
        var logDir = ResolveLogDir(worldPath);
        if (logDir == null)
            return new CheckResult("log.outmemory", Severity.Ok, "Log",
                "未检测到 OOM", "未找到日志目录", false, null, null);

        int oomCount = CountKeywordInLogs(logDir, "java.lang.OutOfMemoryError");
        if (oomCount == 0)
            return new CheckResult("log.outmemory", Severity.Ok, "Log",
                "未检测到 OOM", $"扫描完成，无 OutOfMemoryError", false, null, new { Count = 0 });

        return new CheckResult("log.outmemory", Severity.Critical, "Log",
            "检测到 OutOfMemoryError",
            $"日志中出现 {oomCount} 次 OutOfMemoryError — -Xmx 堆内存配置不足或存在泄漏",
            false,
            new FixAction("backup.world", "建议增加堆内存后重启", false, null, 0.9,
                "OOM 说明 -Xmx 不够或内存泄漏", new List<FixStep>()),
            new { oomCount });
    }

    // ═══════════════════════════════════════════════════════════
    // Check 12: log.chunk.generation — 扫描 CHUNK_GENERATION 事件
    // ═══════════════════════════════════════════════════════════

    private CheckResult CheckLogChunkGeneration(string? worldPath)
    {
        var logDir = ResolveLogDir(worldPath);
        if (logDir == null)
            return new CheckResult("log.chunk.generation", Severity.Ok, "Log",
                "未检测到区块生成异常", "未找到日志目录", false, null, null);

        int count = CountKeywordInLogs(logDir, "Could not pass event CHUNK_GENERATION");
        if (count == 0)
            return new CheckResult("log.chunk.generation", Severity.Ok, "Log",
                "区块生成正常", "无异常事件", false, null, new { Count = 0 });

        if (count < 5)
            return new CheckResult("log.chunk.generation", Severity.Info, "Log",
                "少量区块生成异常", $"{count} 次 CHUNK_GENERATION 异常（< 5）", false, null, new { count });

        return new CheckResult("log.chunk.generation", Severity.Warning, "Log",
            "频繁区块生成异常",
            $"{count} 次 CHUNK_GENERATION 事件异常，可能是插件冲突或区块损坏",
            false, null, new { count });
    }

    // ── 日志扫描辅助方法 ──

    /// <summary>在 latest.log 和 logs/*.log 中统计关键字出现次数</summary>
    private static int CountKeywordInLogs(string logDir, string keyword)
    {
        int count = 0;
        try
        {
            var latest = Path.Combine(logDir, "latest.log");
            if (File.Exists(latest))
                count += CountKeywordInFile(latest, keyword);

            foreach (var f in Directory.GetFiles(logDir, "*.log", SearchOption.TopDirectoryOnly))
            {
                if (f.EndsWith("latest.log")) continue;
                count += CountKeywordInFile(f, keyword);
            }
        }
        catch { }
        return count;
    }

    private static int CountKeywordInFile(string path, string keyword)
    {
        try
        {
            // 只扫文件尾部 2MB，性能友好
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(-Math.Min(fs.Length, 2 * 1024 * 1024), SeekOrigin.End);
            using var sr = new StreamReader(fs);
            var text = sr.ReadToEnd();
            return System.Text.RegularExpressions.Regex.Matches(
                text,
                System.Text.RegularExpressions.Regex.Escape(keyword)).Count;
        }
        catch { return 0; }
    }

    /// <summary>从 worldPath 推导出 server jar 同级 logs 目录</summary>
    private static string? ResolveLogDir(string? worldPath)
    {
        if (string.IsNullOrEmpty(worldPath)) return null;
        var parent = Directory.GetParent(worldPath);
        if (parent == null) return null;
        var p = Path.Combine(parent.FullName, "logs");
        return Directory.Exists(p) ? p : null;
    }
}
