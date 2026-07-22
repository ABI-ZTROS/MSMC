namespace McServerGuard.Models;

public class PortBridgeRule
{
    public string ListenAddress { get; set; } = "127.0.0.1";
    public int ListenPort { get; set; }
    public string ConnectAddress { get; set; } = "127.0.0.1";
    public int ConnectPort { get; set; }
    public string Protocol { get; set; } = "v4tov4";
}