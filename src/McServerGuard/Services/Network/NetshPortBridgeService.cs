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
using System.ServiceProcess;
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

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process == null)
            {
                LastError = "无法启动 netsh 进程";
                Log.Error("{Error}", LastError);
                return false;
            }

            var exited = process.WaitForExit(10000);
            if (!exited)
            {
                LastError = "netsh 命令执行超时";
                Log.Error("{Error}", LastError);
                process.Kill();
                return false;
            }

            var exitCode = process.ExitCode;
            var stdout = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (exitCode != 0)
            {
                // 暴露 netsh 真实错误（如"请求的操作需要提升""对象已存在"等）给 UI
                LastError = string.IsNullOrWhiteSpace(error)
                    ? (string.IsNullOrWhiteSpace(stdout) ? $"netsh 退出码 {exitCode}" : stdout.Trim())
                    : error.Trim();
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

            Log.Warning("IP Helper 服务未运行 (状态: {Status})，尝试启动", sc.Status);
            LastError = "IP Helper 服务未运行，正在尝试启动…";

            sc.Start();
            // 正确等待状态迁移：START_PENDING → RUNNING，避免立即查询的竞态
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
            // ServiceController 不可用时（如精简 Server Core），放行至 netsh 自行报错并暴露真实错误
            LastError = $"检查 IP Helper 服务异常: {ex.Message}";
            Log.Error(ex, "EnsureIpHelperServiceRunning 异常，放行至 netsh");
            return true;
        }
    }

    public bool RemoveBridgeRule(string listenAddress, int listenPort, string protocol = "v4tov4")
    {
        try
        {
            var proto = string.IsNullOrEmpty(protocol) ? "v4tov4" : protocol;
            var args = $"interface portproxy delete {proto} " +
                       $"listenaddress={listenAddress} " +
                       $"listenport={listenPort}";

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            if (process == null)
            {
                LastError = "无法启动 netsh 进程";
                return false;
            }

            var exited = process.WaitForExit(5000);
            if (!exited)
            {
                LastError = "netsh 删除命令超时";
                process.Kill();
                return false;
            }

            var exitCode = process.ExitCode;
            var error = process.StandardError.ReadToEnd();

            if (exitCode != 0)
            {
                LastError = string.IsNullOrWhiteSpace(error)
                    ? $"netsh 删除退出码 {exitCode}"
                    : error.Trim();
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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "interface portproxy show all",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            });

            process?.WaitForExit(5000);
            var output = process?.StandardOutput.ReadToEnd();

            if (string.IsNullOrEmpty(output))
                return rules;

            var lines = output.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
            bool inData = false;

            foreach (var line in lines)
            {
                // 兼容中英文 locale：英文表头含 "Proto"/"Listen"，中文表头含"协议"/"侦听"
                if (!inData)
                {
                    if ((line.Contains("Proto") && line.Contains("Listen"))
                        || (line.Contains("协议") && (line.Contains("侦听") || line.Contains("监听"))))
                        inData = true;
                    continue;
                }

                // 跳过分隔线（全是 - 或 = 的行）
                var trimmedLine = line.Trim();
                if (trimmedLine.All(c => c == '-' || c == '=' || char.IsWhiteSpace(c)))
                    continue;

                // netsh portproxy show all 实际输出格式（分列，非 地址:端口 合列）：
                //   Protocol  Address         Port    Address         Port
                //   v4tov4    127.0.0.1       25565   127.0.0.1       25566
                //   v6tov6    ::1             25565   ::1             25566
                var parts = trimmedLine.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5
                    && int.TryParse(parts[2], out var listenPort)
                    && int.TryParse(parts[4], out var connectPort))
                {
                    rules.Add(new PortBridgeRule
                    {
                        Protocol = parts[0],          // v4tov4 / v6tov6 / v4tov6 / v6tov4
                        ListenAddress = parts[1],     // 127.0.0.1 或 ::1（IPv6 单列无冒号分割问题）
                        ListenPort = listenPort,
                        ConnectAddress = parts[3],
                        ConnectPort = connectPort
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取端口桥接规则失败");
        }

        return rules;
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
            var args = $"advfirewall firewall add rule name=\"MSMC Port Bridge {listenPort}\"" +
                       $" dir=in action=allow protocol={proto} localport={listenPort}";

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            if (process == null)
            {
                LastError = "无法启动 netsh 进程添加防火墙规则";
                Log.Error("{Error}", LastError);
                return false;
            }

            var exited = process.WaitForExit(10000);
            if (!exited)
            {
                LastError = "防火墙规则添加超时";
                Log.Error("{Error}", LastError);
                process.Kill();
                return false;
            }

            var exitCode = process.ExitCode;
            var error = process.StandardError.ReadToEnd();

            if (exitCode != 0)
            {
                LastError = string.IsNullOrWhiteSpace(error)
                    ? $"防火墙规则添加退出码 {exitCode}"
                    : error.Trim();
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

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });

            if (process == null)
            {
                LastError = "无法启动 netsh 进程删除防火墙规则";
                return false;
            }

            process.WaitForExit(5000);
            var success = process.ExitCode == 0;

            if (!success)
            {
                LastError = $"防火墙规则删除退出码 {process.ExitCode}";
                Log.Warning("删除防火墙规则失败: {Port}", listenPort);
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
}
