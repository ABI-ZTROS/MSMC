using System.IO;
using System.IO.Compression;
using io.NET.ZTR_OS.Features.BackupManager.Models;

namespace io.NET.ZTR_OS.Features.BackupManager.Services;

public class RestoreService
{
    private static readonly string[] OverwritableNames = new[]
    {
        "world", "world_nether", "world_the_end",
        "plugins", "mods", "config"
    };

    private static readonly string[] RequiredCriticalFiles = new[]
    {
        Path.Combine("world", "level.dat")
    };

    public async Task RestoreAsync(string serverDir, BackupSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        await RestoreAsync(serverDir, snapshot.BackupFilePath);
    }

    public async Task RestoreAsync(string serverDir, string backupPath)
    {
        if (!Directory.Exists(serverDir))
            throw new DirectoryNotFoundException($"Server directory not found: {serverDir}");
        if (!File.Exists(backupPath))
            throw new FileNotFoundException($"Backup zip not found: {backupPath}");

        var timestamp = DateTime.Now;
        var tsSuffix = timestamp.ToString("yyyyMMdd_HHmmss");
        var renamedMap = new Dictionary<string, string>();

        foreach (var name in OverwritableNames)
        {
            var targetPath = Path.Combine(serverDir, name);
            if (Directory.Exists(targetPath))
            {
                var backupName = $"{name}.{tsSuffix}.pre-restore";
                var backupDir = Path.Combine(serverDir, backupName);
                Directory.Move(targetPath, backupDir);
                renamedMap[name] = backupDir;
            }
            else if (File.Exists(targetPath))
            {
                var backupName = $"{name}.{tsSuffix}.pre-restore";
                var backupFile = Path.Combine(serverDir, backupName);
                File.Move(targetPath, backupFile);
                renamedMap[name] = backupFile;
            }
        }

        try
        {
            ZipFile.ExtractToDirectory(backupPath, serverDir, overwriteFiles: true);
        }
        catch
        {
            CleanupPartialExtraction(serverDir, renamedMap, tsSuffix);
            throw;
        }

        bool valid = ValidateExtracted(serverDir);
        if (!valid)
        {
            CleanupPartialExtraction(serverDir, renamedMap, tsSuffix);
            throw new InvalidOperationException(
                "Restore validation failed: critical file(s) missing (e.g. world/level.dat). " +
                "Pre-restore folders preserved.");
        }

        await Task.CompletedTask;
    }

    private static bool ValidateExtracted(string serverDir)
    {
        foreach (var rel in RequiredCriticalFiles)
        {
            var full = Path.Combine(serverDir, rel);
            if (!File.Exists(full))
                return false;
        }
        return true;
    }

    private static void CleanupPartialExtraction(string serverDir, Dictionary<string, string> renamedMap, string tsSuffix)
    {
        foreach (var kv in renamedMap)
        {
            var originalName = kv.Key;
            var extractedPath = Path.Combine(serverDir, originalName);

            try
            {
                if (Directory.Exists(extractedPath))
                {
                    var badPath = Path.Combine(serverDir, $"{originalName}.{tsSuffix}.bad-extract");
                    if (Directory.Exists(badPath)) Directory.Delete(badPath, true);
                    try { Directory.Move(extractedPath, badPath); }
                    catch { Directory.Delete(extractedPath, true); }
                }
                else if (File.Exists(extractedPath))
                {
                    var badPath = Path.Combine(serverDir, $"{originalName}.{tsSuffix}.bad-extract");
                    if (File.Exists(badPath)) File.Delete(badPath);
                    try { File.Move(extractedPath, badPath); }
                    catch { File.Delete(extractedPath); }
                }
            }
            catch
            {
                /* best effort cleanup — do NOT touch renamedMap entries (.pre-restore stays) */
            }
        }
    }
}
