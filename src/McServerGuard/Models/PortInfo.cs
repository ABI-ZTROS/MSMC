using System;

namespace McServerGuard.Models;

public class PortInfo
{
    public int Port { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public bool IsOpen { get; set; }
    public PortRangeType PortRange { get; set; }
    public DateTime LastUpdated { get; set; }
}

public enum PortRangeType
{
    System,
    Registered,
    Dynamic
}