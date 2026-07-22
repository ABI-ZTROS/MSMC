// -----------------------------------------------------------------------------
// 文件名: CompositePortBridgeService.cs
// 命名空间: McServerGuard.Services.Network
// 功能描述: 桥接系统外观 —— 默认 TcpForwarder 用户态转发 + netsh 内核态兜底
//           两者并存策略：AddBridge 先 TcpForwarder，失败降级 netsh
// 依赖组件: Serilog, McServerGuard.Models
// 设计模式: 外观模式 + 策略链（先用户态转发，失败降级内核态）
// -----------------------------------------------------------------------------
namespace McServerGuard.Services.Network;

using System.Collections.Generic;
using System.Linq;
using McServerGuard.Models;
using Serilog;

/// <summary>
/// 桥接系统外观。默认走 <see cref="ITcpForwarder"/> 用户态转发（无 locale/iphlpsvc 依赖），
/// TcpForwarder 失败时降级到 <see cref="NetshPortBridgeService"/> 内核态兜底。
/// </summary>
/// <remarks>
/// <para><see cref="GetAllBridgeRules"/> 合并两个引擎的规则列表，UI 看到完整规则集。</para>
/// <para>防火墙规则仅在用户勾选时由 netsh 引擎添加（用户态转发不需要防火墙规则）。</para>
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

        // 策略 1：优先用户态 TcpForwarder
        if (_tcpForwarder.AddForward(rule))
        {
            Log.Information("✅ 桥接规则通过 TcpForwarder 启动: {Listen}:{LPort} -> {Connect}:{CPort}",
                rule.ListenAddress, rule.ListenPort, rule.ConnectAddress, rule.ConnectPort);
            return true;
        }

        var forwarderError = _tcpForwarder.LastError;
        Log.Warning("⚠️ TcpForwarder 失败，降级到 netsh 兜底: {Error}", forwarderError);

        // 策略 2：降级到内核态 netsh portproxy
        if (_netsh.AddBridgeRule(rule))
        {
            Log.Information("✅ 桥接规则通过 netsh 兜底启动: {Listen}:{LPort} -> {Connect}:{CPort}",
                rule.ListenAddress, rule.ListenPort, rule.ConnectAddress, rule.ConnectPort);
            return true;
        }

        var netshError = _netsh.LastError;
        LastError = $"TcpForwarder: {forwarderError} | netsh: {netshError}";
        Log.Error("❌ 两个引擎均失败: {Error}", LastError);
        return false;
    }

    public bool RemoveBridgeRule(string listenAddress, int listenPort, string protocol = "v4tov4")
    {
        lock (_errorLock) _lastError = string.Empty;

        // 两者都尝试删除，幂等：一个成功即整体成功
        var forwardOk = _tcpForwarder.RemoveForward(listenAddress, listenPort, protocol);
        var netshOk = _netsh.RemoveBridgeRule(listenAddress, listenPort, protocol);

        if (forwardOk || netshOk)
        {
            Log.Information("桥接规则已删除 (TcpForwarder={Fwd}, netsh={Netsh}): {Addr}:{Port}",
                forwardOk, netshOk, listenAddress, listenPort);
            return true;
        }

        LastError = $"TcpForwarder: {_tcpForwarder.LastError} | netsh: {_netsh.LastError}";
        Log.Warning("两个引擎均无对应规则可删: {Error}", LastError);
        return false;
    }

    public List<PortBridgeRule> GetAllBridgeRules()
    {
        // 合并两个引擎的规则列表，按 (ListenAddress, ListenPort, Protocol) 去重
        var forwarderRules = _tcpForwarder.GetActiveForwards();
        var netshRules = _netsh.GetAllBridgeRules();

        var merged = new List<PortBridgeRule>(forwarderRules.Count + netshRules.Count);
        merged.AddRange(forwarderRules);

        var seen = new HashSet<(string, int, string)>(
            forwarderRules.Select(r => (r.ListenAddress, r.ListenPort, r.Protocol)));

        foreach (var rule in netshRules)
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
        if (_tcpForwarder.GetActiveForwards().Any(r =>
                r.ListenAddress == listenAddress && r.ListenPort == listenPort))
            return true;

        return _netsh.BridgeRuleExists(listenAddress, listenPort);
    }

    public bool EnableFirewallRule(int listenPort, string protocol = "TCP")
    {
        // 仅 netsh 引擎实现防火墙（用户态转发不需要防火墙，但用户勾选时仍可添加）
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
