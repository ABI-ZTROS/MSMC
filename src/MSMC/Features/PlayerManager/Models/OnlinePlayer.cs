namespace io.NET.ZTR_OS.Features.PlayerManager.Models;

public class OnlinePlayer
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan At { get; set; }
    public bool Online { get; set; }
}
