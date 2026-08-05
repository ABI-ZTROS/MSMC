namespace io.NET.ZTR_OS.Features.CoreDownloader.Models;

public record ServerCorePackage(
    string CoreType,
    string Version,
    long SizeBytes,
    string? ExpectedSha1,
    string SourceName,
    Uri DownloadUrl,
    bool IsStable = true)
{
    public bool IsValid
        => !string.IsNullOrWhiteSpace(CoreType)
        && !string.IsNullOrWhiteSpace(Version)
        && SizeBytes > 0
        && DownloadUrl != null;
}
