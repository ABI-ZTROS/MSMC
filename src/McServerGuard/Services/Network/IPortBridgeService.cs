using System.Collections.Generic;
using McServerGuard.Models;

namespace McServerGuard.Services.Network;

public interface IPortBridgeService
{
    /// <summary>最近一次添加/删除操作失败的详细原因，供 UI 展示真实错误。</summary>
    string LastError { get; }

    bool AddBridgeRule(PortBridgeRule rule);
    bool RemoveBridgeRule(string listenAddress, int listenPort);
    List<PortBridgeRule> GetAllBridgeRules();
    bool BridgeRuleExists(string listenAddress, int listenPort);
    bool EnableFirewallRule(int listenPort);
    bool DisableFirewallRule(int listenPort);
}