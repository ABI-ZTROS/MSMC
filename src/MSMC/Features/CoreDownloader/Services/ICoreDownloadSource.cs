using io.NET.ZTR_OS.Features.CoreDownloader.Models;

namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;

public interface ICoreDownloadSource
{
    string Name { get; }
    int Priority { get; }
    string? ForCountryHint { get; }
    Task<bool> ProbeAvailableAsync(CancellationToken ct = default);
    Task<List<string>> ListVersionsAsync(string coreType, CancellationToken ct = default);
    Task<ServerCorePackage?> ResolvePackageAsync(string coreType, string version, CancellationToken ct = default);
    Task<CoreDownloadResult> DownloadAsync(ServerCorePackage pkg, string destDir,
        string? destFileName = null,
        IProgress<(long Downloaded, long Total)>? progress = null,
        CancellationToken ct = default);
}
