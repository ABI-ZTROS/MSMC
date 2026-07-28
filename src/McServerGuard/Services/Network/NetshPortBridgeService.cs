// -----------------------------------------------------------------------------
// 文件名: NetshPortBridgeService.cs
// 命名空间: McServerGuard.Services.Network
// 功能描述: netsh portproxy 内核态桥接实现 —— 桥接系统兜底引擎
//           通过 Process.Start 调用 netsh interface portproxy / advfirewall
// 依赖组件: Serilog, System.ServiceProcess.ServiceController, McServerGuard.Models
// 设计模式: 适配器模式（封装 netsh 命令行为 IPortBridgeService）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.Network;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using McServerGuard.Models;
using Serilog;

/// <summary>
/// netsh portproxy 内核态桥接实现。作为 <see cref="CompositePortBridgeService"/> 的兜底引擎。
/// </summary>
/// <remarks>
/// <para>netsh portproxy 完全依赖 IP Helper 服务（iphlpsvc），服务停止时 add/show 都会失败。</para>
/// <para>规则列表通过解析 netsh 文本输出获取，格式为分列输出（非 地址:端口 合列）。</para>
/// <para>仅在 TcpForwarder 失败时降级使用此实现；用户态转发不可用时仍可走内核态。</para>
/// </remarks>
public sealed class NetshPortBridgeService : IPortBridgeService
{
    private readonly object _errorLock = new();
    private string _lastError = string.Empty;

    public string LastError
    {
        get { lock (_errorLock) return _lastError; }
        private set { lock (_errorLock) _lastError = value; }
    }

    public bool AddBridgeRule(PortBridgeRule rule)
    {
        lock (_errorLock) _lastError = string.Empty;

        try
        {
            // netsh portproxy 完全依赖 IP Helper 服务（iphlpsvc），服务停止时 add 命令会返回非零退出码。
            if (!EnsureIpHelperServiceRunning())
                return false;

            // 幂等：规则已存在则直接成功，避免 netsh 报"对象已存在"。
            if (BridgeRuleExists(rule.ListenAddress, rule.ListenPort))
            {
                Log.Information("端口桥接规则已存在，跳过添加: {Listen}:{LPort}",
                    rule.ListenAddress, rule.ListenPort);
                return true;
            }

            var protocol = string.IsNullOrEmpty(rule.Protocol) ? "v4tov4" : rule.Protocol;
            var args = $"interface portproxy add {protocol} " +
                       $"listenaddress={rule.ListenAddress} " +
                       $"listenport={rule.ListenPort} " +
                       $"connectaddress={rule.ConnectAddress} " +
                       $"connectport={rule.ConnectPort}";

            Log.Information("执行 portproxy 添加规则: {Args}", args);

            var (success, _, error, exitCode) = RunNetsh(args, timeoutMs: 10000,
                redirectOutput: true, redirectError: true);

            if (!success)
            {
                if (IsPermissionDenied(error, exitCode))
                {
                    LastError = $"权限不足：{error}\n\n请以管理员身份重新运行程序，或使用 TcpForwarder 用户态转发模式。";
                }
                else
                {
                    LastError = error;
                }
                Log.Error("端口桥接规则添加失败 (ExitCode={ExitCode}): {Error}", exitCode, LastError);
                return false;
            }

            Log.Information("端口桥接规则添加成功: {Listen}:{LPort} -> {Connect}:{CPort}",
                rule.ListenAddress, rule.ListenPort, rule.ConnectAddress, rule.ConnectPort);

            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "添加端口桥接规则异常");
            return false;
        }
    }

    /// <summary>
    /// 确保 IP Helper 服务（iphlpsvc）处于运行状态。netsh portproxy 依赖此服务。
    /// 用 ServiceController 替代 sc.exe，避开 locale 与 START_PENDING 竞态。
    /// </summary>
    private bool EnsureIpHelperServiceRunning()
    {
        try
        {
            using var sc = new ServiceController("iphlpsvc");

            if (sc.Status == ServiceControllerStatus.Running)
                return true;

            if (!IsRunningAsAdministrator())
            {
                LastError = "需要管理员权限才能启动 IP Helper 服务";
                return false;
            }

            Log.Warning("IP Helper 服务未运行 (状态: {Status})，尝试启动", sc.Status);
            LastError = "IP Helper 服务未运行，正在尝试启动…";

            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));

            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Running)
            {
                Log.Information("IP Helper 服务已启动");
                return true;
            }

            LastError = "IP Helper 服务启动失败，端口桥接无法工作（请在 services.msc 手动启动该服务）";
            Log.Error("{Error} (启动后状态: {Status})", LastError, sc.Status);
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"检查 IP Helper 服务异常: {ex.Message}";
            Log.Error(ex, "EnsureIpHelperServiceRunning 异常，放行至 netsh");
            return true;
        }
    }

    /// <summary>
    /// 检测当前进程是否以管理员权限运行
    /// </summary>
    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "管理员权限检测失败");
            return false;
        }
    }

    /// <summary>
    /// 检测错误消息是否为权限不足导致
    /// </summary>
    private static bool IsPermissionDenied(string error, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(error))
            return false;

        var lower = error.ToLowerInvariant();
        return lower.Contains("拒绝访问")
            || lower.Contains("access denied")
            || lower.Contains("administrator")
            || lower.Contains("权限")
            || exitCode == 5; // ERROR_ACCESS_DENIED
    }

    public bool RemoveBridgeRule(string listenAddress, int listenPort, string protocol = "v4tov4")
    {
        try
        {
            var proto = string.IsNullOrEmpty(protocol) ? "v4tov4" : protocol;
            var args = $"interface portproxy delete {proto} " +
                       $"listenaddress={listenAddress} " +
                       $"listenport={listenPort}";

            var (success, _, error, exitCode) = RunNetsh(args, timeoutMs: 5000,
                redirectOutput: false, redirectError: true);

            if (!success)
            {
                LastError = error;
                Log.Warning("删除端口桥接规则失败 ({Addr}:{Port} {Proto}): {Error}",
                    listenAddress, listenPort, proto, LastError);
                return false;
            }

            Log.Information("已删除端口桥接规则: {Address}:{Port} ({Proto})", listenAddress, listenPort, proto);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "删除端口桥接规则异常");
            return false;
        }
    }

    public List<PortBridgeRule> GetAllBridgeRules()
    {
        var rules = new List<PortBridgeRule>();

        try
        {
            // 使用 RunNetshForOutput 获取 UTF8 编码的标准输出
            var output = RunNetshForOutput("interface portproxy show all", timeoutMs: 5000);

            if (string.IsNullOrEmpty(output))
                return rules;

            var lines = output.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
            bool inData = false;

            foreach (var line in lines)
            {
                // netsh portproxy show all 实际输出格式（中英文 locale 均如此）：
                //   ipv4 到 ipv4:               （分节标题，可据此推断协议，但数据行已含协议列）
                //
                //   协议  地址        端口    地址        端口
                //   v4tov4  127.0.0.1   25565   127.0.0.1   25566
                //   v6tov6  ::1         25565   ::1         25566
                //
                // 注：表头不含 "Proto"/"Listen"（那是旧版或某些 Windows 的格式），
                // 中文 locale 表头为"协议 地址 端口 地址 端口"。
                // 直接通过数据行特征（首列为 v4tov4/v6tov6/v4tov6/v6tov4）识别，不再依赖表头。
                var trimmedLine = line.Trim();

                // 跳过分隔线（全是 - 或 = 的行）
                if (trimmedLine.All(c => c == '-' || c == '=' || char.IsWhiteSpace(c)))
                    continue;

                var parts = trimmedLine.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);

                // 数据行特征：首列为协议标识（v4tov4/v6tov6/v4tov6/v6tov4），共 5 列，第3、5列为端口号
                if (parts.Length >= 5
                    && IsPortProxyProtocol(parts[0])
                    && int.TryParse(parts[2], out var listenPort)
                    && int.TryParse(parts[4], out var connectPort))
                {
                    inData = true;
                    rules.Add(new PortBridgeRule
                    {
                        Protocol = parts[0],          // v4tov4 / v6tov6 / v4tov6 / v6tov4
                        ListenAddress = parts[1],     // 127.0.0.1 或 ::1
                        ListenPort = listenPort,
                        ConnectAddress = parts[3],
                        ConnectPort = connectPort
                    });
                }
                else if (inData && parts.Length >= 5)
                {
                    // 已进入数据区但该行格式异常，记录便于排查
                    Log.Debug("netsh portproxy 行解析跳过: {Line}", trimmedLine);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取端口桥接规则失败");
        }

        return rules;
    }

    /// <summary>
    /// 判断字符串是否为 netsh portproxy 的协议标识
    /// </summary>
    private static bool IsPortProxyProtocol(string s)
    {
        return s.Equals("v4tov4", StringComparison.OrdinalIgnoreCase)
            || s.Equals("v6tov6", StringComparison.OrdinalIgnoreCase)
            || s.Equals("v4tov6", StringComparison.OrdinalIgnoreCase)
            || s.Equals("v6tov4", StringComparison.OrdinalIgnoreCase);
    }

    public bool BridgeRuleExists(string listenAddress, int listenPort)
    {
        var rules = GetAllBridgeRules();
        return rules.Any(r => r.ListenAddress == listenAddress && r.ListenPort == listenPort);
    }

    public bool EnableFirewallRule(int listenPort, string protocol = "TCP")
    {
        try
        {
            var proto = string.IsNullOrEmpty(protocol) ? "TCP" : protocol;
            var ruleName = $"MSMC Port Bridge {listenPort}";

            // 先检查规则是否已存在（幂等）
            var checkArgs = $"advfirewall firewall show rule name=\"{ruleName}\"";
            var (exists, _, _, _) = RunNetsh(checkArgs, timeoutMs: 5000,
                redirectOutput: false, redirectError: false);

            if (exists)
            {
                Log.Information("防火墙规则已存在，跳过添加: {Name}", ruleName);
                return true;
            }

            var args = $"advfirewall firewall add rule name=\"{ruleName}\"" +
                       $" dir=in action=allow protocol={proto} localport={listenPort}";

            var (success, _, error, exitCode) = RunNetsh(args, timeoutMs: 10000,
                redirectOutput: false, redirectError: true);

            if (!success)
            {
                if (IsPermissionDenied(error, exitCode))
                {
                    LastError = $"权限不足：{error}\n\n请以管理员身份重新运行程序。";
                }
                else
                {
                    LastError = error;
                }
                Log.Error("防火墙规则添加失败 (ExitCode={ExitCode}): {Error}", exitCode, LastError);
                return false;
            }

            Log.Information("已添加防火墙规则允许端口 {Port} ({Proto})", listenPort, proto);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "添加防火墙规则失败");
            return false;
        }
    }

    public bool DisableFirewallRule(int listenPort)
    {
        try
        {
            var args = $"advfirewall firewall delete rule name=\"MSMC Port Bridge {listenPort}\"";

            var (success, _, error, exitCode) = RunNetsh(args, timeoutMs: 5000,
                redirectOutput: false, redirectError: true);

            if (!success)
            {
                LastError = error;
                Log.Warning("删除防火墙规则失败: {Port} (ExitCode={ExitCode})", listenPort, exitCode);
            }

            return success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Log.Error(ex, "删除防火墙规则失败");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // netsh 进程执行辅助方法 —— 消除 5 处 Process.Start 重复，统一超时/错误处理
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 执行 netsh 命令并返回执行结果 —— 统一 Process 启动、超时处理、错误收集逻辑
    /// </summary>
    /// <param name="args">netsh 命令参数</param>
    /// <param name="timeoutMs">超时毫秒数，超时后 Kill 进程</param>
    /// <param name="redirectOutput">是否重定向标准输出</param>
    /// <param name="redirectError">是否重定向标准错误</param>
    /// <returns>（是否成功, 标准输出, 错误消息, 退出码）</returns>
    /// <remarks>
    /// 统一处理以下逻辑（原 5 处各自实现，存在不一致和僵尸进程风险）：
    /// - ProcessStartInfo 配置（FileName/UseShellExecute/CreateNoWindow/重定向）
    /// - null 检查（Process.Start 返回 null 时报错）
    /// - 超时检查（WaitForExit 返回 false 时 Kill 进程，避免僵尸进程）
    /// - 退出码检查（非零时收集 stderr/stdout 作为错误消息）
    /// </remarks>
    private (bool Success, string Output, string Error, int ExitCode) RunNetsh(
        string args, int timeoutMs, bool redirectOutput, bool redirectError)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectError
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return (false, string.Empty, "无法启动 netsh 进程", -1);
        }

        var exited = process.WaitForExit(timeoutMs);
        if (!exited)
        {
            try { process.Kill(); } catch { }
            return (false, string.Empty, $"netsh 命令执行超时（{timeoutMs}ms）", -1);
        }

        var exitCode = process.ExitCode;
        var stdout = redirectOutput ? process.StandardOutput.ReadToEnd() : string.Empty;
        var stderr = redirectError ? process.StandardError.ReadToEnd() : string.Empty;

        if (exitCode != 0)
        {
            // 优先使用 stderr，为空时降级到 stdout，两者都空时用退出码
            var error = string.IsNullOrWhiteSpace(stderr)
                ? (string.IsNullOrWhiteSpace(stdout) ? $"netsh 退出码 {exitCode}" : stdout.Trim())
                : stderr.Trim();
            return (false, stdout, error, exitCode);
        }

        return (true, stdout, string.Empty, exitCode);
    }

    /// <summary>
    /// 执行 netsh 命令并返回标准输出 —— 专用于 GetAllBridgeRules 的输出解析场景
    /// </summary>
    /// <param name="args">netsh 命令参数</param>
    /// <param name="timeoutMs">超时毫秒数</param>
    /// <returns>标准输出文本；失败返回空字符串</returns>
    /// <remarks>
    /// 编码说明：netsh 在中文 Windows 上的控制台输出默认为 GBK（代码页 936），
    /// 硬编码 UTF-8 会导致中文表头乱码。此处使用系统默认编码（Console.OutputEncoding）
    /// 确保与 netsh 实际输出编码一致。数据行均为 ASCII（v4tov4/端口数字/IP），
    /// 不受编码影响，但表头匹配需要正确编码。
    /// </remarks>
    private string RunNetshForOutput(string args, int timeoutMs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            // 使用系统控制台默认编码（中文 Windows 为 GBK/936，英文为 437/1252）
            StandardOutputEncoding = Console.OutputEncoding
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            Log.Warning("无法启动 netsh 进程（RunNetshForOutput）");
            return string.Empty;
        }

        // 修复原实现忽略超时返回值的僵尸进程风险
        var exited = process.WaitForExit(timeoutMs);
        if (!exited)
        {
            try { process.Kill(); } catch { }
            Log.Warning("netsh 命令超时（{Timeout}ms），已终止进程", timeoutMs);
            return string.Empty;
        }

        return process.StandardOutput.ReadToEnd();
    }
}
