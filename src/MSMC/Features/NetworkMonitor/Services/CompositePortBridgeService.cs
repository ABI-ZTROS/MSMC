// -----------------------------------------------------------------------------
// 文件名: CompositePortBridgeService.cs
// 命名空间: io.NET.ZTR_OS.Features.NetworkMonitor.Services
// 功能描述: 桥接系统外观 —— netsh 内核态转发优先 + TcpForwarder 用户态降级
//           两者并存策略：AddBridge 先 netsh，失败降级 TcpForwarder
// 依赖组件: Serilog, io.NET.ZTR_OS.Models
// 设计模式: 外观模式 + 策略链（先内核态转发，失败降级用户态）
// -----------------------------------------------------------------------------
namespace io.NET.ZTR_OS.Features.NetworkMonitor.Services;

using System.Collections.Generic;
using System.Linq;
using io.NET.ZTR_OS.Features.NetworkMonitor.Models;
using Serilog;

/// <summary>
/// 桥接系统外观。默认走 <see cref="NetshPortBridgeService"/> 内核态转发（高性能、持久化），
/// netsh 失败时降级到 <see cref="ITcpForwarder"/> 用户态转发兜底。
/// </summary>
/// <remarks>
/// <para><see cref="GetAllBridgeRules"/> 合并两个引擎的规则列表，UI 看到完整规则集。</para>
/// <para>防火墙规则随 netsh 引擎自动管理（用户勾选时添加）。</para>
/// </remarks>
public sealed class CompositePortBridgeService : IPortBridgeService
{
    private readonly ITcpForwarder _tcpForwarder;
    private readonly NetshPortBridgeService _netsh;
    private readonly object _errorLock = new();
    private string _lastError = string.Empty;

    public CompositePortBridgeService(ITcpForwarder tcpForwarder, NetshPortBridgeService netsh)
    {
        _tcpForwarder = tcpForwarder;
        _netsh = netsh;
    }

    public string LastError
    {
        get { lock (_errorLock) return _lastError; }
        private set { lock (_errorLock) _lastError = value; }
    }

    public bool AddBridgeRule(PortBridgeRule rule)
    {
        lock (_errorLock) _lastError = string.Empty;

        // 策略 1：优先内核态 netsh portproxy（高性能、持久化、系统级）
        if (_netsh.AddBridgeRule(rule))
        {
            rule.Engine = "netsh";
            Log.Information("[OK] 桥接规则通过 netsh 内核态启动: {Listen}:{LPort} -> {Connect}:{CPort}",
                rule.ListenAddress, rule.ListenPort, rule.ConnectAddress, rule.ConnectPort);
            return true;
        }

        var netshError = _netsh.LastError;
        Log.Warning("[WARN] netsh 失败，降级到 TcpForwarder 用户态转发: {Error}", netshError);

        // 策略 2：降级到用户态 TcpForwarder
        if (_tcpForwarder.AddForward(rule))
        {
            rule.Engine = "TcpForwarder";
            Log.Information("[OK] 桥接规则通过 TcpForwarder 用户态启动: {Listen}:{LPort} -> {Connect}:{CPort}",
                rule.ListenAddress, rule.ListenPort, rule.ConnectAddress, rule.ConnectPort);
            return true;
        }

        var forwarderError = _tcpForwarder.LastError;
        LastError = $"netsh: {netshError} | TcpForwarder: {forwarderError}";
        Log.Error("[ERR] 两个引擎均失败: {Error}", LastError);
        return false;
    }

    public bool RemoveBridgeRule(string listenAddress, int listenPort, string protocol = "v4tov4")
    {
        lock (_errorLock) _lastError = string.Empty;

        // 两者都尝试删除，幂等：一个成功即整体成功
        var netshOk = _netsh.RemoveBridgeRule(listenAddress, listenPort, protocol);
        var forwardOk = _tcpForwarder.RemoveForward(listenAddress, listenPort, protocol);

        if (netshOk || forwardOk)
        {
            Log.Information("桥接规则已删除 (netsh={Netsh}, TcpForwarder={Fwd}): {Addr}:{Port}",
                netshOk, forwardOk, listenAddress, listenPort);
            return true;
        }

        LastError = $"netsh: {_netsh.LastError} | TcpForwarder: {_tcpForwarder.LastError}";
        Log.Warning("两个引擎均无对应规则可删: {Error}", LastError);
        return false;
    }

    public List<PortBridgeRule> GetAllBridgeRules()
    {
        // 合并两个引擎的规则列表，按 (ListenAddress, ListenPort, Protocol) 去重
        var netshRules = _netsh.GetAllBridgeRules();
        var forwarderRules = _tcpForwarder.GetActiveForwards();

        foreach (var rule in netshRules)
            rule.Engine = "netsh";
        foreach (var rule in forwarderRules)
            rule.Engine = "TcpForwarder";

        var merged = new List<PortBridgeRule>(netshRules.Count + forwarderRules.Count);
        merged.AddRange(netshRules);

        var seen = new HashSet<(string, int, string)>(
            netshRules.Select(r => (r.ListenAddress, r.ListenPort, r.Protocol)));

        foreach (var rule in forwarderRules)
        {
            var key = (rule.ListenAddress, rule.ListenPort, rule.Protocol);
            if (seen.Add(key))
                merged.Add(rule);
        }

        return merged;
    }

    public bool BridgeRuleExists(string listenAddress, int listenPort)
    {
        // 任一引擎存在即认为存在
        if (_netsh.BridgeRuleExists(listenAddress, listenPort))
            return true;

        return _tcpForwarder.GetActiveForwards().Any(r =>
            r.ListenAddress == listenAddress && r.ListenPort == listenPort);
    }

    public bool EnableFirewallRule(int listenPort, string protocol = "TCP")
    {
        var ok = _netsh.EnableFirewallRule(listenPort, protocol);
        if (!ok)
        {
            lock (_errorLock) _lastError = _netsh.LastError;
        }
        return ok;
    }

    public bool DisableFirewallRule(int listenPort)
    {
        var ok = _netsh.DisableFirewallRule(listenPort);
        if (!ok)
        {
            lock (_errorLock) _lastError = _netsh.LastError;
        }
        return ok;
    }
}
