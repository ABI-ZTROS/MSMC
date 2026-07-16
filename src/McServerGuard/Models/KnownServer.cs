namespace McServerGuard.Models;

public class KnownServer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string ServerJarPath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string JavaPath { get; set; } = string.Empty;
    public long InitialHeapMemoryBytes { get; set; }
    public long MaxHeapMemoryBytes { get; set; }
    public int Port { get; set; } = 25565;
    public List<string> JvmArguments { get; set; } = [];
    public string? Notes { get; set; }
    public string Group { get; set; } = "默认";
    public DateTime AddedAt { get; set; } = DateTime.Now;
    public DateTime LastSeenAt { get; set; } = DateTime.Now;
    public bool IsFavorite { get; set; }
}
