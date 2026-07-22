using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using McServerGuard.Models;
using Serilog;

namespace McServerGuard.Services.Network;

public class PortBridgeService : IPortBridgeService
{
    public bool AddBridgeRule(PortBridgeRule rule)
    {
        try
        {
            var args = $"interface portproxy add {rule.Protocol} " +
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

            process?.WaitForExit(5000);

            if (process?.ExitCode == 0)
            {
                Log.Information("端口桥接规则添加成功: {Listen}:{LPort} -> {Connect}:{CPort}",
                    rule.ListenAddress, rule.ListenPort, rule.ConnectAddress, rule.ConnectPort);
                return true;
            }

            var error = process?.StandardError.ReadToEnd();
            Log.Error("端口桥接规则添加失败: {Error}", error);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加端口桥接规则异常");
            return false;
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
                CreateNoWindow = true
            });

            process?.WaitForExit(5000);
            var success = process?.ExitCode == 0;

            if (success)
                Log.Information("已添加防火墙规则允许端口 {Port}", listenPort);

            return success;
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