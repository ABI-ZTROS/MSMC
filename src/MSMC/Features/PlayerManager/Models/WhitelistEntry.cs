using System.Text.Json.Serialization;

namespace io.NET.ZTR_OS.Features.PlayerManager.Models;

public class WhitelistEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
