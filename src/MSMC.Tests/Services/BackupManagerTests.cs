using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using io.NET.ZTR_OS.Features.BackupManager.Models;
using io.NET.ZTR_OS.Features.BackupManager.Services;
using Xunit;

namespace io.NET.ZTR_OS.Tests.Services;

public class BackupManagerTests : IDisposable
{
    private readonly string _testRoot;

    public BackupManagerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "msmc_backup_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            try { Directory.Delete(_testRoot, true); }
            catch { /* ignore cleanup errors */ }
        }
    }

    private static void WriteRandomBytes(string path, long size)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        using var fs = File.Create(path);
        var buf = new byte[Math.Min(size, 4096)];
        long remaining = size;
        while (remaining > 0)
        {
            RandomNumberGenerator.Fill(buf);
            var write = (int)Math.Min(buf.Length, remaining);
            fs.Write(buf, 0, write);
            remaining -= write;
        }
    }

    private static string CreateServerDirLayout(string serverDir)
    {
        Directory.CreateDirectory(Path.Combine(serverDir, "world"));
        Directory.CreateDirectory(Path.Combine(serverDir, "plugins"));
        Directory.CreateDirectory(Path.Combine(serverDir, "logs"));
        Directory.CreateDirectory(Path.Combine(serverDir, "tmp"));
        File.WriteAllText(Path.Combine(serverDir, "world", "level.dat"), "WORLD_DATA");
        File.WriteAllText(Path.Combine(serverDir, "plugins", "Essentials.jar"), "JAR_CONTENT");
        File.WriteAllText(Path.Combine(serverDir, "server.properties"), "SERVER_PROPS");
        File.WriteAllText(Path.Combine(serverDir, "logs", "latest.log"), "LOG_DATA");
        File.WriteAllText(Path.Combine(serverDir, "tmp", "cache.bin"), "CACHE_DATA");
        return serverDir;
    }

    // ─── Test 1: CreateBackup_IncludesWorldAndPlugins_ExcludesLogsTmp ───
    [Fact]
    public async Task CreateBackup_IncludesWorldAndPlugins_ExcludesLogsTmp()
    {
        var serverDir = Path.Combine(_testRoot, "srv1");
        CreateServerDirLayout(serverDir);

        var svc = new BackupService();
        var snap = await svc.CreateAsync(serverDir, "test-label");

        Assert.True(File.Exists(snap.BackupFilePath), "zip should produce zip file");
        var entries = new HashSet<string>();
        using (var fs = File.OpenRead(snap.BackupFilePath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
        {
            foreach (var e in zip.Entries) entries.Add(e.FullName);
        }

        Assert.Contains(entries, e => e.StartsWith("world/"), "world folder should be in zip");
        Assert.Contains(entries, e => e.StartsWith("plugins/"), "plugins folder should be in zip");
        Assert.Contains(entries, e => e == "server.properties" || e.EndsWith("/server.properties")
            || entries.Contains("server.properties"), "server.properties should be in zip");
        Assert.DoesNotContain(entries, e => e.StartsWith("logs/"), "logs/ should be excluded");
        Assert.DoesNotContain(entries, e => e.StartsWith("tmp/"), "tmp/ should be excluded");
    }

    // ─── Test 2: CreateBackup_ReturnsSnapshot_WithCorrectSize ───
    [Fact]
    public async Task CreateBackup_ReturnsSnapshot_WithCorrectSize()
    {
        var serverDir = Path.Combine(_testRoot, "srv2");
        Directory.CreateDirectory(Path.Combine(serverDir, "world"));
        Directory.CreateDirectory(Path.Combine(serverDir, "plugins"));
        WriteRandomBytes(Path.Combine(serverDir, "world", "level.dat"), 20 * 1024);
        WriteRandomBytes(Path.Combine(serverDir, "world", "session.lock"), 1024);
        WriteRandomBytes(Path.Combine(serverDir, "plugins", "A.jar"), 30 * 1024);
        WriteRandomBytes(Path.Combine(serverDir, "plugins", "B.jar"), 30 * 1024);
        WriteRandomBytes(Path.Combine(serverDir, "server.properties"), 19 * 1024);

        long total = 20 * 1024 + 1024 + 30 * 1024 + 30 * 1024 + 19 * 1024;
        var svc = new BackupService();
        var snap = await svc.CreateAsync(serverDir, "size-test");

        Assert.True(snap.SizeBytes >= total, $"Snapshot.SizeBytes ({snap.SizeBytes} should >= {total}");
        Assert.True(snap.SizeBytes <= total * 1.5 + 4096, $"compressed size shouldn't be absurdly larger");
    }

    // ─── Test 3: RestoreService_PreRenamesOldFolder_BeforeExtract ───
    [Fact]
    public async Task RestoreService_PreRenamesOldFolder_BeforeExtract()
    {
        var serverDir = Path.Combine(_testRoot, "srv3");
        Directory.CreateDirectory(Path.Combine(serverDir, "world"));
        File.WriteAllText(Path.Combine(serverDir, "world", "level.dat"), "OLD_LEVEL_DAT");

        var backupDir = Path.Combine(_testRoot, "bk_src");
        Directory.CreateDirectory(Path.Combine(backupDir, "world"));
        File.WriteAllText(Path.Combine(backupDir, "world", "level.dat"), "NEW_LEVEL_DAT");
        var bkpZip = Path.Combine(_testRoot, "pre.zip");
        if (File.Exists(bkpZip)) File.Delete(bkpZip);
        ZipFile.CreateFromDirectory(backupDir, bkpZip);

        var indexDir = Path.Combine(serverDir, "backups");
        Directory.CreateDirectory(indexDir);
        var snap = new BackupSnapshot(
            DateTime.Now, "t", new FileInfo(bkpZip).Length, bkpZip,
            ComputeSha1(bkpZip), new List<string> { "world" });
        var idx = new List<BackupSnapshot> { snap };
        File.WriteAllText(Path.Combine(indexDir, "index.json"),
            System.Text.Json.JsonSerializer.Serialize(idx));

        var restore = new RestoreService();
        await restore.RestoreAsync(serverDir, snap);

        var restored = File.ReadAllText(Path.Combine(serverDir, "world", "level.dat"));
        Assert.Equal("NEW_LEVEL_DAT", restored);

        var preDirs = Directory.GetDirectories(serverDir, "world.*.pre-restore");
        Assert.NotEmpty(preDirs);
        Assert.True(File.Exists(Path.Combine(preDirs[0], "level.dat")),
            "pre-restore folder should keep old level.dat");
        Assert.Equal("OLD_LEVEL_DAT", File.ReadAllText(Path.Combine(preDirs[0], "level.dat")));
    }

    // ─── Test 4: RestoreService_ValidationFails_DoesNotDeletePreRestore ───
    [Fact]
    public async Task RestoreService_ValidationFails_DoesNotDeletePreRestore()
    {
        var serverDir = Path.Combine(_testRoot, "srv4");
        Directory.CreateDirectory(Path.Combine(serverDir, "world"));
        File.WriteAllText(Path.Combine(serverDir, "world", "level.dat"), "OLD_LEVEL_DAT");

        var backupDir = Path.Combine(_testRoot, "bk_bad");
        Directory.CreateDirectory(Path.Combine(backupDir, "world"));
        File.WriteAllText(Path.Combine(backupDir, "world", "README.txt"), "NO_LEVEL_DAT_HERE");
        var bkpZip = Path.Combine(_testRoot, "bad.zip");
        if (File.Exists(bkpZip)) File.Delete(bkpZip);
        ZipFile.CreateFromDirectory(backupDir, bkpZip);

        var indexDir = Path.Combine(serverDir, "backups");
        Directory.CreateDirectory(indexDir);
        var snap = new BackupSnapshot(
            DateTime.Now, "bad", new FileInfo(bkpZip).Length, bkpZip,
            ComputeSha1(bkpZip), new List<string> { "world" });
        File.WriteAllText(Path.Combine(indexDir, "index.json"),
            System.Text.Json.JsonSerializer.Serialize(new List<BackupSnapshot> { snap }));

        var restore = new RestoreService();
        await Assert.ThrowsAnyAsync<Exception>(() => restore.RestoreAsync(serverDir, snap));

        var preDirs = Directory.GetDirectories(serverDir, "world.*.pre-restore");
        Assert.NotEmpty(preDirs);
    }

    // ─── Test 5: BackupService_DefaultIncludeList_Covers8StandardFolders ───
    [Fact]
    public void BackupService_DefaultIncludeList_Covers8StandardFolders()
    {
        var svc = new BackupService();

        var includes = new[] { "world", "world_nether", "world_the_end", "plugins", "mods", "config" };
        var includeFiles = new[] { "server.properties", "bukkit.yml", "spigot.yml", "paper-global.yml" };
        var excludes = new[] { "logs", "cache", "tmp", "*.tmp" };

        foreach (var f in includes)
            Assert.Contains(svc.IncludePatterns, p => p.Equals(f, StringComparison.OrdinalIgnoreCase));
        foreach (var f in includeFiles)
            Assert.Contains(svc.IncludePatterns, p => p.Equals(f, StringComparison.OrdinalIgnoreCase));
        foreach (var e in excludes)
            Assert.Contains(svc.ExcludePatterns, p => p.Equals(e, StringComparison.OrdinalIgnoreCase));
    }

    // ─── Test 6: ListBackups_OrdersByDescendingTimestamp ───
    [Fact]
    public async Task ListBackups_OrdersByDescendingTimestamp()
    {
        var serverDir = Path.Combine(_testRoot, "srv5");
        Directory.CreateDirectory(Path.Combine(serverDir, "world"));
        File.WriteAllText(Path.Combine(serverDir, "world", "level.dat"), "X");
        Directory.CreateDirectory(Path.Combine(serverDir, "backups"));

        var svc = new BackupService();
        await Task.Delay(1100);
        var s1 = await svc.CreateAsync(serverDir, "first");
        await Task.Delay(1100);
        var s2 = await svc.CreateAsync(serverDir, "second");
        await Task.Delay(1100);
        var s3 = await svc.CreateAsync(serverDir, "third");

        var list = await svc.ListAsync(serverDir);

        Assert.Equal(3, list.Count);
        Assert.True(list[0].Timestamp >= list[1].Timestamp, "#0 should be newest");
        Assert.True(list[1].Timestamp >= list[2].Timestamp, "#1 should be newer than #2");
        Assert.Equal("third", list[0].Label);
        Assert.Equal("second", list[1].Label);
        Assert.Equal("first", list[2].Label);
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
