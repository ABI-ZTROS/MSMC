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
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "interface portproxy show all",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });

            process?.WaitForExit(5000);
            var output = process?.StandardOutput.ReadToEnd();

            if (string.IsNullOrEmpty(output))
                return rules;

            var lines = output.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
            bool inData = false;

            foreach (var line in lines)
            {
                if (!inData)
                {
                    if (line.Contains("Proto") && line.Contains("Listen"))
                        inData = true;
                    continue;
                }

                var parts = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5)
                {
                    var listenAddrPort = parts[1].Split(':');
                    var connectAddrPort = parts[3].Split(':');

                    if (listenAddrPort.Length == 2 && connectAddrPort.Length == 2 &&
                        int.TryParse(listenAddrPort[1], out var listenPort) &&
                        int.TryParse(connectAddrPort[1], out var connectPort))
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