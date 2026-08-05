using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using io.NET.ZTR_OS.Features.BackupManager.Models;

namespace io.NET.ZTR_OS.Features.BackupManager.Services;

public class BackupService
{
    public List<string> IncludePatterns { get; } = new()
    {
        "world",
        "world_nether",
        "world_the_end",
        "plugins",
        "mods",
        "config",
        "server.properties",
        "bukkit.yml",
        "spigot.yml",
        "paper-global.yml"
    };

    public List<string> ExcludePatterns { get; } = new()
    {
        "logs",
        "cache",
        "tmp",
        "*.tmp"
    };

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public async Task<BackupSnapshot> CreateAsync(string serverDir, string label = "")
    {
        if (!Directory.Exists(serverDir))
            throw new DirectoryNotFoundException($"Server directory not found: {serverDir}");

        var backupsDir = Path.Combine(serverDir, "backups");
        Directory.CreateDirectory(backupsDir);

        var timestamp = DateTime.Now;
        var zipName = $"backup-{timestamp:yyyyMMdd_HHmmss}.zip";
        var zipPath = Path.Combine(backupsDir, zipName);

        var worldNames = new List<string>();
        var includedEntries = CollectIncludedEntries(serverDir, worldNames);

        long originalSize = 0;
        using (var fs = File.Create(zipPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var absPath in includedEntries)
            {
                var rel = Path.GetRelativePath(serverDir, absPath);
                var entryName = rel.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
                var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                entry.LastWriteTime = File.GetLastWriteTime(absPath);
                await using (var es = entry.Open())
                await using (var src = File.OpenRead(absPath))
                {
                    await src.CopyToAsync(es);
                }
                originalSize += new FileInfo(absPath).Length;
            }
        }

        var sha1 = ComputeSha1(zipPath);

        var snapshot = new BackupSnapshot(
            timestamp,
            label ?? string.Empty,
            originalSize,
            zipPath,
            sha1,
            worldNames);

        await AppendIndexAsync(backupsDir, snapshot);
        return snapshot;
    }

    public async Task<List<BackupSnapshot>> ListAsync(string serverDir)
    {
        var indexPath = Path.Combine(serverDir, "backups", "index.json");
        if (!File.Exists(indexPath))
            return new List<BackupSnapshot>();

        var json = await File.ReadAllTextAsync(indexPath);
        var list = JsonSerializer.Deserialize<List<BackupSnapshot>>(json) ?? new List<BackupSnapshot>();
        return list.OrderByDescending(s => s.Timestamp).ToList();
    }

    private List<string> CollectIncludedEntries(string serverDir, List<string> worldNames)
    {
        var results = new List<string>();
        var excludedDirNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var excl in ExcludePatterns)
        {
            if (!excl.Contains('*'))
                excludedDirNames.Add(excl);
        }

        foreach (var entryPath in Directory.EnumerateFileSystemEntries(serverDir))
        {
            var name = Path.GetFileName(entryPath);
            var isDir = Directory.Exists(entryPath);

            if (isDir)
            {
                if (excludedDirNames.Contains(name))
                    continue;

                bool included = false;
                foreach (var inc in IncludePatterns)
                {
                    if (inc.Contains('*'))
                        continue;
                    if (name.Equals(inc, StringComparison.OrdinalIgnoreCase))
                    {
                        included = true;
                        break;
                    }
                }

                if (included)
                {
                    if (name.StartsWith("world", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!worldNames.Contains(name))
                            worldNames.Add(name);
                    }
                    AddFilesRecursive(entryPath, results, excludedDirNames);
                }
            }
            else
            {
                bool included = false;
                foreach (var inc in IncludePatterns)
                {
                    if (inc.Contains('*'))
                    {
                        if (MatchesGlob(name, inc)) { included = true; break; }
                    }
                    else if (name.Equals(inc, StringComparison.OrdinalIgnoreCase))
                    {
                        included = true;
                        break;
                    }
                }
                if (included)
                    results.Add(entryPath);
            }
        }
        return results;
    }

    private static void AddFilesRecursive(string dir, List<string> results, HashSet<string> excludedDirNames)
    {
        foreach (var file in Directory.EnumerateFiles(dir))
            results.Add(file);

        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
            var subName = Path.GetFileName(sub);
            if (excludedDirNames.Contains(subName))
                continue;
            if (MatchesAnyWildcardExclude(subName))
                continue;
            AddFilesRecursive(sub, results, excludedDirNames);
        }
    }

    private static bool MatchesAnyWildcardExclude(string name)
    {
        return name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesGlob(string name, string pattern)
    {
        if (pattern == "*.tmp")
            return name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    private static async Task AppendIndexAsync(string backupsDir, BackupSnapshot snapshot)
    {
        var indexPath = Path.Combine(backupsDir, "index.json");
        List<BackupSnapshot> list;
        if (File.Exists(indexPath))
        {
            var existing = await File.ReadAllTextAsync(indexPath);
            list = JsonSerializer.Deserialize<List<BackupSnapshot>>(existing) ?? new List<BackupSnapshot>();
        }
        else
        {
            list = new List<BackupSnapshot>();
        }
        list.Add(snapshot);
        await File.WriteAllTextAsync(indexPath, JsonSerializer.Serialize(list, JsonOpts));
    }

    private static string ComputeSha1(string path)
    {
        using var fs = File.OpenRead(path);
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(fs);
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
