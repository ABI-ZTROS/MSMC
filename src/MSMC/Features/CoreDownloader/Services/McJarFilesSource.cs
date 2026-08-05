using System.Text.Json;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;

namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;

public class McJarFilesSource : ICoreDownloadSource
{
    public string Name => "mcjarfiles.xyz (Mirror)";
    public int Priority => 5;
    public string? ForCountryHint => null;

    public string BaseUrl => "https://mcjarfiles.xyz/api/v1";

    public string Endpoint_ListVersions => $"{BaseUrl}/listVersions/{{0}}";
    public string Endpoint_GetJar => $"{BaseUrl}/get-jar/{{0}}/{{1}}";
    public string Endpoint_GetLatestJar => $"{BaseUrl}/get-latest-jar/{{0}}";
    public string Endpoint_GetVersionInfo => $"{BaseUrl}/get-version-info/{{0}}/{{1}}";

    private readonly HttpClient _http;

    public McJarFilesSource(HttpClient? http = null)
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
        var url = string.Format(Endpoint_ListVersions, coreType);
        using var doc = await JsonDocument.ParseAsync(await _http.GetStreamAsync(url, ct), cancellationToken: ct);
        var result = new List<string>();
        if (doc.RootElement.TryGetProperty("versions", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in arr.EnumerateArray().TakeLast(15).Reverse())
                if (v.GetString() is { } s) result.Add(s);
        }
        return result;
    }

    public async Task<ServerCorePackage?> ResolvePackageAsync(string coreType, string version, CancellationToken ct = default)
    {
        var infoUrl = string.Format(Endpoint_GetVersionInfo, coreType, version);
        try
        {
            using var doc = await JsonDocument.ParseAsync(await _http.GetStreamAsync(infoUrl, ct), cancellationToken: ct);
            var root = doc.RootElement;
            long size = 0;
            string? sha1 = null;
            string? sha256 = null;
            if (root.TryGetProperty("size", out var sp)) size = sp.GetInt64();
            if (root.TryGetProperty("sha1", out var s1p)) sha1 = s1p.GetString();
            if (root.TryGetProperty("sha256", out var s256p)) sha256 = s256p.GetString();
            string? stableStr = null;
            if (root.TryGetProperty("stable", out var stab)) stableStr = stab.GetString();
            var dlUrl = new Uri(string.Format(Endpoint_GetJar, coreType, version));
            return new ServerCorePackage(coreType, version, size, sha256 ?? sha1, Name, dlUrl, stableStr != "false");
        }
        catch
        {
            var dlUrl = new Uri(string.Format(Endpoint_GetJar, coreType, version));
            return new ServerCorePackage(coreType, version, 0, null, Name, dlUrl);
        }
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
        long already = 0;
        if (File.Exists(fullPath))
            already = new FileInfo(fullPath).Length;
        try
        {
            using var reqMsg = new HttpRequestMessage(HttpMethod.Get, pkg.DownloadUrl);
            if (already > 0)
                reqMsg.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(already, null);
            using var resp = await _http.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            total = resp.Content.Headers.ContentRange?.Length ?? resp.Content.Headers.ContentLength ?? total;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(fullPath,
                already > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);
            var buf = new byte[128 * 1024];
            downloaded = already;
            int n;
            while ((n = await src.ReadAsync(buf, 0, buf.Length, ct)) > 0)
            {
                await dst.WriteAsync(buf, 0, n, ct);
                downloaded += n;
                progress?.Report((downloaded, total));
            }
            sw.Stop();
            return new CoreDownloadResult(CoreDownloadStatus.Completed, fullPath, downloaded,
                total, ElapsedMs: sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new CoreDownloadResult(CoreDownloadStatus.Failed, ErrorMessage: ex.Message,
                DownloadedBytes: downloaded, TotalBytes: total);
        }
    }
}
