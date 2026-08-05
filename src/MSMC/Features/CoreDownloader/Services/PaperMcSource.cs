using System.Net.Http.Headers;
using System.Text.Json;
using io.NET.ZTR_OS.Features.CoreDownloader.Models;

namespace io.NET.ZTR_OS.Features.CoreDownloader.Services;

public class PaperMcSource : ICoreDownloadSource
{
    public string Name => "PaperMC (Fill API v3)";
    public int Priority => 1;
    public string? ForCountryHint => null;
    public string FillBaseUrl => "https://fill.papermc.io/v3/projects";

    private readonly HttpClient _http;

    public PaperMcSource(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MSMC/0.1.0 (+https://github.com/ABI-ZTROS/MSMC)");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<bool> ProbeAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head,
                $"{FillBaseUrl}/paper"), ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> ListVersionsAsync(string coreType, CancellationToken ct = default)
    {
        var url = $"{FillBaseUrl}/{coreType}";
        using var doc = await JsonDocument.ParseAsync(
            await _http.GetStreamAsync(url, ct), cancellationToken: ct);
        var arr = doc.RootElement.GetProperty("versions");
        var result = new List<string>(arr.GetArrayLength());
        foreach (var v in arr.EnumerateArray().TakeLast(15).Reverse())
            result.Add(v.GetString()!);
        return result;
    }

    public async Task<ServerCorePackage?> ResolvePackageAsync(string coreType, string version, CancellationToken ct = default)
    {
        var url = $"{FillBaseUrl}/{coreType}/versions/{version}/builds";
        using var doc = await JsonDocument.ParseAsync(
            await _http.GetStreamAsync(url, ct), cancellationToken: ct);
        var builds = doc.RootElement;
        JsonElement chosen = default;
        long chosenSize = 0;
        string? chosenSha256 = null;
        foreach (var b in builds.EnumerateArray().Reverse())
        {
            var dl = b.GetProperty("downloads").GetProperty("application");
            if (dl.TryGetProperty("name", out var name) && name.GetString()?.EndsWith(".jar") == true)
            {
                chosen = b;
                chosenSize = dl.GetProperty("size").GetInt64();
                chosenSha256 = dl.GetProperty("sha256").GetString();
                break;
            }
        }
        if (chosen.ValueKind == JsonValueKind.Undefined) return null;
        var buildId = chosen.GetProperty("build").GetInt32();
        var jarName = chosen.GetProperty("downloads").GetProperty("application").GetProperty("name").GetString()!;
        var dlUrl = new Uri(
            $"{FillBaseUrl}/{coreType}/versions/{version}/builds/{buildId}/downloads/{jarName}");
        var stable = chosen.GetProperty("channel").GetString() == "STABLE";
        return new ServerCorePackage(coreType, version, chosenSize, chosenSha256, Name, dlUrl, stable);
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
