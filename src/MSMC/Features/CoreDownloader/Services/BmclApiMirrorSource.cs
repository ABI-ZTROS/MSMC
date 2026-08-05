using System.IO;
using System.Net.Http;
using System.Text.Json;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;

namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;

public class BmclApiMirrorSource : ICoreDownloadSource
{
    public string Name => "BMCLAPI (CN Mirror)";
    public int Priority => 3;
    public string? ForCountryHint => "CN";

    public string BaseUrl => "https://bmclapi2.bangbang93.com";

    private readonly HttpClient _http;
    private static readonly HashSet<string> SupportedCores = new() { "paper", "purpur", "vanilla", "fabric" };

    public BmclApiMirrorSource(HttpClient? http = null)
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
        var key = coreType.ToLowerInvariant();
        var url = key switch
        {
            "paper" => $"{BaseUrl}/paper/versions",
            "purpur" => $"{BaseUrl}/purpurmc/versions",
            "vanilla" => $"{BaseUrl}/mc/game/version_manifest.json",
            "fabric" => $"{BaseUrl}/fabric/game",
            _ => throw new NotSupportedException($"BMCLAPI does not support core type: {coreType}")
        };
        var result = new List<string>();
        using var doc = await JsonDocument.ParseAsync(await _http.GetStreamAsync(url, ct), cancellationToken: ct);
        if (key == "vanilla")
        {
            if (doc.RootElement.TryGetProperty("versions", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in arr.EnumerateArray().Take(15))
                    if (v.TryGetProperty("id", out var id))
                        if (id.GetString() is { } s) result.Add(s);
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in doc.RootElement.EnumerateArray().TakeLast(15).Reverse())
                if (v.GetString() is { } s) result.Add(s);
        }
        return result;
    }

    public async Task<ServerCorePackage?> ResolvePackageAsync(string coreType, string version, CancellationToken ct = default)
    {
        var key = coreType.ToLowerInvariant();
        if (!SupportedCores.Contains(key))
            throw new NotSupportedException($"BMCLAPI does not support core type: {coreType}");

        string dlUrlStr;
        long size = 0;
        string? hash = null;
        switch (key)
        {
            case "paper":
                dlUrlStr = $"{BaseUrl}/paper/{version}/download";
                break;
            case "purpur":
                dlUrlStr = $"{BaseUrl}/purpurmc/{version}/download";
                break;
            case "vanilla":
                dlUrlStr = $"{BaseUrl}/version/{version}/client";
                break;
            case "fabric":
                dlUrlStr = $"{BaseUrl}/fabric/loader/{version}/0.16.9/server/jar";
                break;
            default:
                return null;
        }

        try
        {
            using var headMsg = new HttpRequestMessage(HttpMethod.Head, dlUrlStr);
            using var headResp = await _http.SendAsync(headMsg, HttpCompletionOption.ResponseHeadersRead, ct);
            if (headResp.IsSuccessStatusCode)
            {
                size = headResp.Content.Headers.ContentLength ?? 0;
                if (headResp.Headers.TryGetValues("x-bmclapi-hash", out var hashes))
                    hash = hashes.FirstOrDefault();
                else if (headResp.Content.Headers.TryGetValues("content-md5", out var md5s))
                    hash = md5s.FirstOrDefault();
            }
        }
        catch { }

        return new ServerCorePackage(coreType, version, size, hash, Name, new Uri(dlUrlStr));
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
