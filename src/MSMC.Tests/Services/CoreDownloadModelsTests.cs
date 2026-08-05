using Xunit;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;
using io.NET.ZTR_OS.Features.CoreDownloader.Services;
using io.NET.ZTR_OS.Features.StartupDiagnostics.Models;
using io.NET.ZTR_OS.Features.BackupManager.Models;

namespace io.NET.ZTR_OS.Tests.Services;

public class CoreDownloadModelsTests
{
    [Fact]
    public void ServerCorePackage_SetsPropertiesCorrectly()
    {
        var p = new ServerCorePackage("paper", "1.21.1", 42_000_000, "abc123", "PaperMC",
            new Uri("https://fill.papermc.io/v3/projects/paper/versions/1.21.1/builds/1/downloads/paper-1.21.1-1.jar"));
        Assert.Equal("paper", p.CoreType);
        Assert.Equal("1.21.1", p.Version);
        Assert.Equal(42_000_000, p.SizeBytes);
        Assert.True(p.IsValid);
    }

    [Fact]
    public void ServerCorePackage_InvalidWhenEmptyCoreType()
    {
        var p = new ServerCorePackage("", "1.21.1", 42_000_000, "abc123", "PaperMC",
            new Uri("https://fill.papermc.io/v3/projects/paper/versions/1.21.1/builds/1/downloads/paper-1.21.1-1.jar"));
        Assert.False(p.IsValid);
    }

    [Fact]
    public void CoreDownloadStatus_EnumHasFiveValues()
    {
        var values = Enum.GetValues(typeof(CoreDownloadStatus)).Cast<CoreDownloadStatus>().ToList();
        Assert.Equal(5, values.Count);
        Assert.Contains(CoreDownloadStatus.Scheduled, values);
        Assert.Contains(CoreDownloadStatus.InProgress, values);
        Assert.Contains(CoreDownloadStatus.Completed, values);
        Assert.Contains(CoreDownloadStatus.Failed, values);
        Assert.Contains(CoreDownloadStatus.Cancelled, values);
    }

    [Fact]
    public void CoreDownloadResult_DefaultValuesAreSane()
    {
        var r = new CoreDownloadResult(CoreDownloadStatus.Completed, "/tmp/paper.jar", 42_000_000, 42_000_000,
            ElapsedMs: 1234.5, HashVerified: true);
        Assert.Equal(CoreDownloadStatus.Completed, r.Status);
        Assert.Equal("/tmp/paper.jar", r.SavedFilePath);
        Assert.Equal(42_000_000, r.DownloadedBytes);
        Assert.True(r.HashVerified);
    }

    [Fact]
    public void ICoreDownloadSource_InterfaceHasExpectedMembers()
    {
        var t = typeof(ICoreDownloadSource);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetProperty("Name"));
        Assert.NotNull(t.GetProperty("Priority"));
        Assert.NotNull(t.GetProperty("ForCountryHint"));
        Assert.NotNull(t.GetMethod("ProbeAvailableAsync"));
        Assert.NotNull(t.GetMethod("ListVersionsAsync"));
        Assert.NotNull(t.GetMethod("ResolvePackageAsync"));
        Assert.NotNull(t.GetMethod("DownloadAsync"));
    }

    [Fact]
    public void StartupDiagnosis_SeverityEnumHasThreeLevels()
    {
        var values = Enum.GetValues(typeof(DiagnosisSeverity)).Cast<DiagnosisSeverity>().ToList();
        Assert.Equal(3, values.Count);
        Assert.Contains(DiagnosisSeverity.Info, values);
        Assert.Contains(DiagnosisSeverity.Warning, values);
        Assert.Contains(DiagnosisSeverity.Critical, values);
    }

    [Fact]
    public void StartupDiagnosis_PropertiesSetCorrectly()
    {
        var d = new StartupDiagnosis(
            Severity: DiagnosisSeverity.Critical,
            Description: "端口 25565 被占用",
            SuggestedAction: "关闭占用端口的进程",
            OneClickFixCommandId: "fix:kill-port-25565");
        Assert.Equal(DiagnosisSeverity.Critical, d.Severity);
        Assert.Equal("端口 25565 被占用", d.Description);
        Assert.Equal("fix:kill-port-25565", d.OneClickFixCommandId);
    }

    [Fact]
    public void BackupSnapshot_HasExpectedWorldList()
    {
        var snap = new BackupSnapshot(
            Timestamp: new DateTime(2026, 8, 4, 15, 30, 0, DateTimeKind.Utc),
            Label: "每日备份",
            SizeBytes: 123_456_789,
            BackupFilePath: "/backups/20260804_1530.zip",
            Sha1: "a1b2c3d4e5",
            WorldNames: ["world", "world_nether", "world_the_end"]);
        Assert.Equal("每日备份", snap.Label);
        Assert.Equal(3, snap.WorldNames.Count);
        Assert.Contains("world_nether", snap.WorldNames);
    }
}
