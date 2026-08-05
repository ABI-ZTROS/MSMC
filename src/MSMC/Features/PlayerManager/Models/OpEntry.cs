using System.Text.Json.Serialization;

namespace io.NET.ZTR_OS.Features.PlayerManager.Models;

public class OpEntry
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("bypassesPlayerLimit")]
    public bool BypassesPlayerLimit { get; set; }
}
