using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services.Network;

public class PortBridgeService : IPortBridgeService
{
    /// <summary>
    /// 最近一次操作的错误描述（供 UI 显示真实失败原因，而非硬编码提示）。
    /// </summary>
    public string LastError { get; private set; } = string.Empty;

    public bool AddBridgeRule(PortBridgeRule rule)
    {
        LastError = string.Empty;

        try
        {
            // netsh portproxy 完全依赖 IP Helper 服务（iphlpsvc），服务停止时 add 命令
            // 会返回非零退出码——这正是"管理员身份运行仍桥接失败"的最常见原因。
            if (!EnsureIpHelperServiceRunning())
                return false;

            // 幂等：规则已存在则直接成功，避免 netsh 报"对象已存在"。
            // GetAllBridgeRules 解析失败时会返回空列表，此处安全放行到 netsh。
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
    /// 确保 IP Helper 服务（iphlpsvc）处于运行状态。netsh portproxy 依赖此服务，
    /// 服务停止时 add/show 都会失败。未运行则尝试启动（需管理员权限）。
    /// </summary>
    private bool EnsureIpHelperServiceRunning()
    {
        try
        {
            const string serviceName = "iphlpsvc";
            var state = QueryServiceState(serviceName);

            if (state.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
                return true;

            Log.Warning("IP Helper 服务未运行 (状态: {State})，尝试启动", state.Trim());
            LastError = "IP Helper 服务未运行，正在尝试启动…";

            using var startProc = Process.Start(new ProcessStartInfo
            {
                FileName = "sc",
                Arguments = $"start {serviceName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            startProc?.WaitForExit(5000);

            var stateAfter = QueryServiceState(serviceName);
            if (stateAfter.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("IP Helper 服务已启动");
                return true;
            }

            LastError = "IP Helper 服务启动失败，端口桥接无法工作（请在 services.msc 手动启动该服务）";
            Log.Error("{Error} (启动后状态: {State})", LastError, stateAfter.Trim());
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查/启动 IP Helper 服务异常，放行至 netsh 自行报错");
            return true;
        }
    }

    private static string QueryServiceState(string serviceName)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "sc",
                Arguments = $"query {serviceName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            });
            proc?.WaitForExit(3000);
            return proc?.StandardOutput.ReadToEnd() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public bool RemoveBridgeRule(string listenAddress, int listenPort)
    {
        try
        {
            var args = $"interface portproxy delete v4tov4 " +
                       $"listenaddress={listenAddress} " +
                       $"listenport={listenPort}";

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            process?.WaitForExit(5000);
            var success = process?.ExitCode == 0;

            if (success)
                Log.Information("已删除端口桥接规则: {Address}:{Port}", listenAddress, listenPort);
            else
                Log.Warning("删除端口桥接规则失败: {Address}:{Port}", listenAddress, listenPort);

            return success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除端口桥接规则异常");
            return false;
        }
    }

    public List<PortBridgeRule> GetAllBridgeRules()
    {
        var rules = new List<PortBridgeRule>();

        try
        {
            // 强制 netsh 使用英文输出，避免中文 locale 下表头为"协议/侦听地址"导致解析失败
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
                if (line.Trim().All(c => c == '-' || c == '='))
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    // 格式：v4tov4  127.0.0.1:25565  127.0.0.1:25566
                    // parts[0]=协议, parts[1]=侦听地址:端口, parts[2]可能是"->"或"--", parts[3]/parts[4]=连接地址:端口
                    // 兼容不同 netsh 版本的列布局
                    var listenAddrPort = parts[1].Split(':');
                    string? connectAddrPortStr = null;

                    // 尝试从 parts[3] 或 parts[4] 找连接地址（跳过可能的 "->" 或 "--" 列）
                    for (int i = 2; i < parts.Length && connectAddrPortStr == null; i++)
                    {
                        if (parts[i].Contains(':') && parts[i] != parts[1])
                            connectAddrPortStr = parts[i];
                    }

                    if (listenAddrPort.Length == 2
                        && connectAddrPortStr != null
                        && int.TryParse(listenAddrPort[1], out var listenPort)
                        && connectAddrPortStr.Split(':') is { Length: 2 } connectAddrPort
                        && int.TryParse(connectAddrPort[1], out var connectPort))
                    {
                        rules.Add(new PortBridgeRule
                        {
                            Protocol = parts[0],
                            ListenAddress = listenAddrPort[0],
                            ListenPort = listenPort,
                            ConnectAddress = connectAddrPort[0],
                            ConnectPort = connectPort
                        });
                    }
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

    public bool EnableFirewallRule(int listenPort)
    {
        try
        {
            var args = $"advfirewall firewall add rule name=\"MSMC Port Bridge {listenPort}\"" +
                       $" dir=in action=allow protocol=TCP localport={listenPort}";

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
                Log.Error("无法启动 netsh 进程添加防火墙规则");
                return false;
            }

            var exited = process.WaitForExit(10000);
            if (!exited)
            {
                Log.Error("防火墙规则添加超时");
                process.Kill();
                return false;
            }

            var exitCode = process.ExitCode;
            var error = process.StandardError.ReadToEnd();

            if (exitCode != 0)
            {
                Log.Error("防火墙规则添加失败 (ExitCode={ExitCode}): {Error}", exitCode, error);
                return false;
            }

            Log.Information("已添加防火墙规则允许端口 {Port}", listenPort);

            return true;
        }
        catch (Exception ex)
        {
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
                CreateNoWindow = true
            });

            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除防火墙规则失败");
            return false;
        }
    }
}