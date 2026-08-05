using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;

namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;

public class CoreDownloadService
{
    private readonly List<ICoreDownloadSource> _sources;
    private const int MaxSourceSwitches = 2;
    private const int ProbeTimeoutMs = 1000;

    public IReadOnlyList<ICoreDownloadSource> Sources => _sources;

    public CoreDownloadService(List<ICoreDownloadSource> sources)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    public CoreDownloadService(IEnumerable<ICoreDownloadSource> sources)
    {
        _sources = sources?.ToList() ?? throw new ArgumentNullException(nameof(sources));
    }

    public async Task<List<ICoreDownloadSource>> ProbeAndRankSourcesAsync(CancellationToken ct = default)
    {
        var results = new List<(ICoreDownloadSource Source, double LatencyMs, bool Alive)>();

        var probeTasks = _sources.Select(async s =>
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(ProbeTimeoutMs);
                var alive = await s.ProbeAvailableAsync(timeoutCts.Token);
                sw.Stop();
                return (Source: s, LatencyMs: sw.Elapsed.TotalMilliseconds, Alive: alive);
            }
            catch
            {
                sw.Stop();
                return (Source: s, LatencyMs: double.MaxValue, Alive: false);
            }
        });

        var probeResults = await Task.WhenAll(probeTasks);

        foreach (var r in probeResults)
            if (r.Alive)
                results.Add(r);

        return results
            .OrderBy(r => r.LatencyMs)
            .Select(r => r.Source)
            .ToList();
    }

    public async Task<CoreDownloadResult> DownloadSmartAsync(
        string coreType,
        string version,
        string destDir,
        string? destFileName = null,
        IProgress<(long Downloaded, long Total)>? progress = null,
        CancellationToken ct = default)
    {
        var rankedSources = await ProbeAndRankSourcesAsync(ct);
        if (rankedSources.Count == 0)
            rankedSources = new List<ICoreDownloadSource>(_sources);

        var errors = new List<string>();
        int sourceSwitchCount = 0;
        CoreDownloadResult? lastResult = null;

        for (int i = 0; i < rankedSources.Count; i++)
        {
            var source = rankedSources[i];
            ct.ThrowIfCancellationRequested();

            ServerCorePackage? pkg = null;
            try
            {
                pkg = await source.ResolvePackageAsync(coreType, version, ct);
            }
            catch (Exception ex)
            {
                errors.Add($"[{source.Name}] ResolvePackage failed: {ex.Message}");
                if (sourceSwitchCount >= MaxSourceSwitches || i == rankedSources.Count - 1)
                    break;
                sourceSwitchCount++;
                continue;
            }

            if (pkg == null)
            {
                errors.Add($"[{source.Name}] ResolvePackage returned null");
                if (sourceSwitchCount >= MaxSourceSwitches || i == rankedSources.Count - 1)
                    break;
                sourceSwitchCount++;
                continue;
            }

            CoreDownloadResult dlResult;
            try
            {
                dlResult = await source.DownloadAsync(pkg, destDir, destFileName, progress, ct);
            }
            catch (HttpRequestException hrex)
            {
                errors.Add($"[{source.Name}] HttpRequestException: {hrex.Message}");
                if (sourceSwitchCount >= MaxSourceSwitches || i == rankedSources.Count - 1)
                    break;
                sourceSwitchCount++;
                continue;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add($"[{source.Name}] Download error: {ex.Message}");
                if (sourceSwitchCount >= MaxSourceSwitches || i == rankedSources.Count - 1)
                    break;
                sourceSwitchCount++;
                continue;
            }

            lastResult = dlResult;

            if (dlResult.Status != CoreDownloadStatus.Completed)
            {
                errors.Add($"[{source.Name}] Download status={dlResult.Status}: {dlResult.ErrorMessage}");
                if (sourceSwitchCount >= MaxSourceSwitches || i == rankedSources.Count - 1)
                    break;
                sourceSwitchCount++;
                continue;
            }

            if (!string.IsNullOrEmpty(dlResult.SavedFilePath) && File.Exists(dlResult.SavedFilePath))
            {
                bool hashOk = true;
                if (!string.IsNullOrEmpty(pkg.ExpectedSha1))
                {
                    var fileBytes = await File.ReadAllBytesAsync(dlResult.SavedFilePath, ct);
                    var expectedHex = pkg.ExpectedSha1.Trim().ToLowerInvariant();
                    hashOk = expectedHex.Length switch
                    {
                        64 => VerifySha256(fileBytes, expectedHex),
                        40 => VerifySha1(fileBytes, expectedHex),
                        _ => true
                    };

                    if (!hashOk)
                    {
                        errors.Add($"[{source.Name}] Hash mismatch (expected {pkg.ExpectedSha1})");
                        try { File.Delete(dlResult.SavedFilePath); } catch { }
                        if (sourceSwitchCount >= MaxSourceSwitches || i == rankedSources.Count - 1)
                            break;
                        sourceSwitchCount++;
                        continue;
                    }
                }

                return dlResult with { HashVerified = hashOk };
            }

            if (sourceSwitchCount >= MaxSourceSwitches || i == rankedSources.Count - 1)
                break;
            sourceSwitchCount++;
        }

        return new CoreDownloadResult(
            CoreDownloadStatus.Failed,
            lastResult?.SavedFilePath,
            lastResult?.DownloadedBytes ?? 0,
            lastResult?.TotalBytes ?? 0,
            ErrorMessage: string.Join(" | ", errors));
    }

    public static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static string ComputeSha1(byte[] data)
    {
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static bool VerifySha256(byte[] data, string expectedHex)
    {
        if (string.IsNullOrEmpty(expectedHex)) return true;
        var actual = ComputeSha256(data);
        return string.Equals(actual, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool VerifySha1(byte[] data, string expectedHex)
    {
        if (string.IsNullOrEmpty(expectedHex)) return true;
        var actual = ComputeSha1(data);
        return string.Equals(actual, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
