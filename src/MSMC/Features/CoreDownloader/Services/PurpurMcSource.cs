using System.Net.Http;
using System.Text.Json;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;

namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;

public class PurpurMcSource : ICoreDownloadSource
{
    public string Name => "PurpurMC (Official API v2)";
    public int Priority => 2;
    public string? ForCountryHint => null;
    public string BaseUrl => "https://api.purpurmc.org/v2/purpur";

    private readonly HttpClient _http;

    public PurpurMcSource(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MSMC/0.1.0 (+https://github.com/ABI-ZTROS/MSMC)");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<bool> ProbeAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, BaseUrl), ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> ListVersionsAsync(string coreType, CancellationToken ct = default)
    {
        using var doc = await JsonDocument.ParseAsync(
            await _http.GetStreamAsync(BaseUrl, ct), cancellationToken: ct);
        var arr = doc.RootElement.GetProperty("versions");
        var result = new List<string>(arr.GetArrayLength());
        foreach (var v in arr.EnumerateArray().TakeLast(15).Reverse())
            result.Add(v.GetString()!);
        return result;
    }

    public async Task<ServerCorePackage?> ResolvePackageAsync(string coreType, string version, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/{version}";
        using var doc = await JsonDocument.ParseAsync(
            await _http.GetStreamAsync(url, ct), cancellationToken: ct);
        var root = doc.RootElement;

        if (!root.TryGetProperty("builds", out var buildsProp))
            return null;

        var latestBuild = buildsProp.GetProperty("latest").GetString();
        if (latestBuild == null)
            return null;

        var allBuilds = buildsProp.GetProperty("all");
        JsonElement buildInfo = default;
        long sizeBytes = 0;
        string? md5 = null;
        string? jarName = null;

        foreach (var b in allBuilds.EnumerateArray().Reverse())
        {
            var buildNum = b.GetString();
            if (buildNum == latestBuild)
            {
                var detailUrl = $"{BaseUrl}/{version}/{buildNum}";
                using var detailDoc = await JsonDocument.ParseAsync(
                    await _http.GetStreamAsync(detailUrl, ct), cancellationToken: ct);
                var detailRoot = detailDoc.RootElement;

                if (detailRoot.TryGetProperty("size", out var sizeProp))
                    sizeBytes = sizeProp.GetInt64();

                if (detailRoot.TryGetProperty("md5", out var md5Prop))
                    md5 = md5Prop.GetString();

                jarName = $"purpur-{version}-{buildNum}.jar";
                buildInfo = detailRoot;
                break;
            }
        }

        if (buildInfo.ValueKind == JsonValueKind.Undefined)
        {
            jarName ??= $"purpur-{version}-{latestBuild}.jar";
        }

        var dlUrl = new Uri($"{BaseUrl}/{version}/latest/download");
        return new ServerCorePackage("purpur", version, sizeBytes, md5, Name, dlUrl);
    }

    public async Task<CoreDownloadResult> DownloadAsync(ServerCorePackage pkg, string destDir,
        string? destFileName = null, IProgress<(long Downloaded, long Total)>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fileName = destFileName ?? $"{pkg.CoreType}-{pkg.Version}.jar";
        var fullPath = Path.Combine(destDir, fileName);
        Directory.CreateDirectory(destDir);
        long total = pkg.SizeBytes;
        long downloaded = 0;
        try
        {
            using var resp = await _http.GetAsync(pkg.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            total = resp.Content.Headers.ContentLength ?? total;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buf = new byte[128 * 1024];
            int n;
            while ((n = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
            {
                await dst.WriteAsync(buf, 0, n, ct);
                downloaded += n;
                progress?.Report((downloaded, total));
            }
            sw.Stop();
            bool hashOk = string.IsNullOrEmpty(pkg.ExpectedSha1);
            return new CoreDownloadResult(CoreDownloadStatus.Completed, fullPath, downloaded,
                total, ElapsedMs: sw.Elapsed.TotalMilliseconds, HashVerified: hashOk);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new CoreDownloadResult(CoreDownloadStatus.Failed, ErrorMessage: ex.Message,
                DownloadedBytes: downloaded, TotalBytes: total);
        }
    }
}
