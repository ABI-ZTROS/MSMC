using System.Collections.Generic;
using McServerGuard.Models;

namespace McServerGuard.Services.Network;

public interface IPortBridgeService
{
    bool AddBridgeRule(PortBridgeRule rule);
    bool RemoveBridgeRule(string listenAddress, int listenPort);
    List<PortBridgeRule> GetAllBridgeRules();
    bool BridgeRuleExists(string listenAddress, int listenPort);
    bool EnableFirewallRule(int listenPort);
    bool DisableFirewallRule(int listenPort);
}