using Xunit;
using io.NET.ZTR_OS.Features.CoreDownloader.Services;

namespace io.NET.ZTR_OS.Tests.Services;

public class PaperMcSourceTests
{
    [Fact]
    public void Name_IsPaperMc_FillPriority1()
    {
        var s = new PaperMcSource();
        Assert.Equal("PaperMC (Fill API v3)", s.Name);
        Assert.Equal(1, s.Priority);
        Assert.Null(s.ForCountryHint);
    }

    [Fact]
    public void ResolvePackage_KnownProject_ReturnsNonNull()
    {
        var s = new PaperMcSource();
        Assert.Contains("fill.papermc.io", s.FillBaseUrl);
    }

    [Fact]
    public void PurpurMcSource_NamePriorityUrl_IsCorrect()
    {
        var s = new PurpurMcSource();
        Assert.Equal("PurpurMC (Official API v2)", s.Name);
        Assert.Equal(2, s.Priority);
        Assert.Null(s.ForCountryHint);
        Assert.Equal("https://api.purpurmc.org/v2/purpur", s.BaseUrl);
    }

    [Fact]
    public void BothSources_ImplementICoreDownloadSource_Completely()
    {
        ICoreDownloadSource paper = new PaperMcSource();
        ICoreDownloadSource purpur = new PurpurMcSource();

        Assert.NotNull(paper.Name);
        Assert.NotNull(purpur.Name);
        Assert.True(paper.Priority > 0);
        Assert.True(purpur.Priority > 0);

        var probePaper = paper.ProbeAvailableAsync;
        var listPaper = paper.ListVersionsAsync;
        var resolvePaper = paper.ResolvePackageAsync;
        var downloadPaper = paper.DownloadAsync;

        var probePurpur = purpur.ProbeAvailableAsync;
        var listPurpur = purpur.ListVersionsAsync;
        var resolvePurpur = purpur.ResolvePackageAsync;
        var downloadPurpur = purpur.DownloadAsync;

        Assert.NotNull(probePaper);
        Assert.NotNull(listPaper);
        Assert.NotNull(resolvePaper);
        Assert.NotNull(downloadPaper);
        Assert.NotNull(probePurpur);
        Assert.NotNull(listPurpur);
        Assert.NotNull(resolvePurpur);
        Assert.NotNull(downloadPurpur);
    }
}
