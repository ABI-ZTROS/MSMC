using System.Text.Json;
using io.NET.ZTR_OS.Features.PlayerManager.Models;
using io.NET.ZTR_OS.Features.PlayerManager.Services;
using Xunit;

namespace io.NET.ZTR_OS.Tests.Services;

public class PlayerManagerTests : IDisposable
{
    private readonly string _testRoot;

    public PlayerManagerTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "msmc_player_tests_" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void PlayerLogParser_JoinedEvent_ReturnsPlayer()
    {
        var lines = new[] { "[15:30:00 INFO]: Notch joined the game" };
        var result = PlayerLogParser.ParseLogLines(lines);

        Assert.Single(result);
        Assert.Equal("Notch", result[0].Name);
        Assert.Equal(new TimeSpan(15, 30, 0), result[0].At);
        Assert.True(result[0].Online);
    }

    [Fact]
    public void PlayerLogParser_LeftEvent_MarksOffline()
    {
        var lines = new[]
        {
            "[15:30:00 INFO]: Notch joined the game",
            "[15:35:00 INFO]: Notch left the game",
        };
        var result = PlayerLogParser.ParseLogLines(lines);

        Assert.Single(result);
        Assert.Equal("Notch", result[0].Name);
        Assert.False(result[0].Online);
    }

    [Fact]
    public void PlayerLogParser_MultipleEvents_Accumulates()
    {
        var lines = new[]
        {
            "[10:00:00 INFO]: Alice joined the game",
            "[10:01:00 INFO]: Bob joined the game",
            "[10:02:00 INFO]: Charlie joined the game",
            "[10:03:00 INFO]: Dave joined the game",
            "[10:04:00 INFO]: Eve joined the game",
        };
        var result = PlayerLogParser.ParseLogLines(lines);

        Assert.Equal(5, result.Count);
        Assert.Equal("Alice", result[0].Name);
        Assert.Equal("Bob", result[1].Name);
        Assert.Equal("Charlie", result[2].Name);
        Assert.Equal("Dave", result[3].Name);
        Assert.Equal("Eve", result[4].Name);
        Assert.All(result, p => Assert.True(p.Online));
    }

    [Fact]
    public void JsonFileService_UpsertWhitelist_AddsEntry()
    {
        var serverDir = Path.Combine(_testRoot, "srv1");
        Directory.CreateDirectory(serverDir);
        var svc = new JsonFileService();

        svc.Upsert(serverDir, "wl", new WhitelistEntry
        {
            Name = "Notch",
            Uuid = "069a79b7-4891-4a77-acf3-4ae4e2bf088b",
        });

        var readBack = svc.ReadJson<WhitelistEntry>(serverDir, "wl");
        Assert.Single(readBack);
        Assert.Equal("Notch", readBack[0].Name);
        Assert.Equal("069a79b7-4891-4a77-acf3-4ae4e2bf088b", readBack[0].Uuid);
    }

    [Fact]
    public void JsonFileService_UpsertOp_Level4()
    {
        var serverDir = Path.Combine(_testRoot, "srv2");
        Directory.CreateDirectory(serverDir);
        var svc = new JsonFileService();

        svc.Upsert(serverDir, "ops", new OpEntry
        {
            Name = "Notch",
            Uuid = "069a79b7-4891-4a77-acf3-4ae4e2bf088b",
            Level = 4,
            BypassesPlayerLimit = true,
        });

        var readBack = svc.ReadJson<OpEntry>(serverDir, "ops");
        Assert.Single(readBack);
        Assert.Equal("Notch", readBack[0].Name);
        Assert.Equal(4, readBack[0].Level);
        Assert.True(readBack[0].BypassesPlayerLimit);
    }

    [Fact]
    public void JsonFileService_RemoveBan_RemovesByName()
    {
        var serverDir = Path.Combine(_testRoot, "srv3");
        Directory.CreateDirectory(serverDir);
        var svc = new JsonFileService();

        svc.Upsert(serverDir, "ban", new BanEntry
        {
            Name = "Notch",
            Uuid = "069a79b7-4891-4a77-acf3-4ae4e2bf088b",
            Created = "2026-01-01 00:00:00 +0800",
            Source = "Server",
            Expires = "forever",
            Reason = "Banned by admin",
        });
        svc.Upsert(serverDir, "ban", new BanEntry
        {
            Name = "Steve",
            Uuid = "8667ba71-b85a-4004-af54-457a9734eed7",
            Created = "2026-01-01 00:00:00 +0800",
            Source = "Server",
            Expires = "forever",
            Reason = "Banned by admin",
        });

        var before = svc.ReadJson<BanEntry>(serverDir, "ban");
        Assert.Equal(2, before.Length);

        svc.Remove(serverDir, "ban", "Notch");

        var after = svc.ReadJson<BanEntry>(serverDir, "ban");
        Assert.Single(after);
        Assert.Equal("Steve", after[0].Name);
    }
}
