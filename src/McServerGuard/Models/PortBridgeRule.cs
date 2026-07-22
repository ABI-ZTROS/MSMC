namespace McServerGuard.Models;

public class PortBridgeRule : IEquatable<PortBridgeRule>
{
    public string ListenAddress { get; set; } = "127.0.0.1";
    public int ListenPort { get; set; }
    public string ConnectAddress { get; set; } = "127.0.0.1";
    public int ConnectPort { get; set; }
    public string Protocol { get; set; } = "v4tov4";

    // 值相等性：基于监听/连接地址端口比较，让 UpdateCollection 的智能跳过真正生效
    public bool Equals(PortBridgeRule? other) =>
        other is not null
        && ListenAddress == other.ListenAddress
        && ListenPort == other.ListenPort
        && ConnectAddress == other.ConnectAddress
        && ConnectPort == other.ConnectPort;

    public override bool Equals(object? obj) => Equals(obj as PortBridgeRule);

    public override int GetHashCode() => System.HashCode.Combine(ListenAddress, ListenPort, ConnectAddress, ConnectPort);
}