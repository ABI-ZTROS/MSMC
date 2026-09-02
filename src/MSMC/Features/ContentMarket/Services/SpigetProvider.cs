// -----------------------------------------------------------------------------
// 文件名: SpigetProvider.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: SpigotMC Spiget API 提供器 —— 搜索/版本/下载
// 文档: https://spiget.org/
// -----------------------------------------------------------------------------

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

/// <summary>
/// SpigotMC Spiget API v2 客户端
/// Spiget 是 SpigotMC 官方资源 API，覆盖 Spigot/Paper/Purpur 插件生态
/// </summary>
public class SpigetProvider : IMarketProvider
{
    private const string BaseUrl = "https://api.spiget.org/v2";
    private readonly ILogger<SpigetProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MarketSource Source => MarketSource.Spiget;

    public SpigetProvider(ILogger<SpigetProvider> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MSMC", "1.0"));
    }

    public async Task<IReadOnlyList<MarketProject>> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        // Spiget 没有专用搜索端点，用 list + 名称过滤 + sort by downloads
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString["size"] = request.Limit.ToString();
        queryString["page"] = (request.Offset / Math.Max(request.Limit, 1)).ToString();
        queryString["sort"] = "-downloads";
        queryString["field"] = "name";
        queryString["q"] = request.Query;

        var url = $"{BaseUrl}/resources?{queryString}";
        _logger.LogDebug("Spiget 搜索: {Url}", url);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Spiget 搜索返回数组，或者 /v2/resources 直接返回数组
            var results = new List<MarketProject>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    // 跳过 external 资源（Spiget 不托管的外部链接）
                    if (item.TryGetProperty("external", out var extEl) && extEl.GetBoolean())
                        continue;
                    results.Add(ParseProject(item));
                }
            }
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Spiget 搜索 HTTP 错误");
            return Array.Empty<MarketProject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Spiget 搜索异常");
            return Array.Empty<MarketProject>();
        }
    }

    public async Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(string projectId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/resources/{Uri.EscapeDataString(projectId)}/versions?size=50";
        _logger.LogDebug("Spiget 版本查询: {Url}", url);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var versions = new List<MarketVersion>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    versions.Add(ParseVersion(item, projectId));
                }
            }
            return versions;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Spiget 版本查询 HTTP 错误");
            return Array.Empty<MarketVersion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Spiget 版本查询异常");
            return Array.Empty<MarketVersion>();
        }
    }

    public async Task<byte[]> DownloadVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        _logger.LogWarning("Spiget DownloadVersionAsync 需要 resourceId + versionId，建议用直链下载");
        return Array.Empty<byte>();
    }

    private static MarketProject ParseProject(JsonElement item)
    {
        var project = new MarketProject
        {
            Source = MarketSource.Spiget,
            Id = item.TryGetProperty("id", out var idEl) ? idEl.GetInt64().ToString() : "",
            Name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
            Slug = item.TryGetProperty("name", out var slugEl) ? slugEl.GetString() ?? "" : "",
            Description = item.TryGetProperty("tag", out var tagEl) ? tagEl.GetString() ?? "" : "",
            Downloads = item.TryGetProperty("downloads", out var dlEl) ? dlEl.GetInt64() : 0,
        };

        // author 信息 Spiget 返回对象 { id, name }
        if (item.TryGetProperty("author", out var authorEl))
        {
            if (authorEl.ValueKind == JsonValueKind.Object && authorEl.TryGetProperty("name", out var anEl))
                project.Author = anEl.GetString() ?? "";
        }

        // likes / followers
        if (item.TryGetProperty("likes", out var likesEl))
            project.Followers = likesEl.GetInt64();

        // icon - Spiget 提供 iconUrl
        if (item.TryGetProperty("icon", out var iconEl) && iconEl.GetString() is { } iconUrl)
            project.IconUrl = iconUrl;

        // testedVersions → GameVersions
        if (item.TryGetProperty("testedVersions", out var tvEl) && tvEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in tvEl.EnumerateArray())
            {
                if (v.GetString() is { } gv)
                    project.GameVersions.Add(gv);
            }
        }

        // Spiget 主要是 Spigot/Paper/Purpur 生态
        project.SupportedLoaders.AddRange(new[] { ModLoader.Spigot, ModLoader.Paper, ModLoader.Purpur });

        // category
        if (item.TryGetProperty("category", out var catEl))
        {
            var catStr = catEl.ValueKind switch
            {
                JsonValueKind.String => catEl.GetString(),
                JsonValueKind.Object when catEl.TryGetProperty("name", out var cn) => cn.GetString(),
                _ => null
            };
            if (!string.IsNullOrEmpty(catStr))
                project.Categories.Add(catStr);
        }

        project.ProjectUrl = $"https://www.spigotmc.org/resources/{project.Id}";

        return project;
    }

    private static MarketVersion ParseVersion(JsonElement item, string projectId)
    {
        var version = new MarketVersion
        {
            ProjectId = projectId,
            Id = item.TryGetProperty("id", out var idEl) ? idEl.GetInt64().ToString() : "",
            VersionNumber = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
            Name = item.TryGetProperty("name", out var n2El) ? n2El.GetString() ?? "" : "",
            ReleasedAt = item.TryGetProperty("createdAt", out var caEl) && caEl.TryGetDateTimeOffset(out var ca) ? ca : null,
        };

        // releaseType: "release" / "beta" / "alpha"
        if (item.TryGetProperty("releaseType", out var rtEl) && rtEl.GetString() is { } rt)
            version.IsPreRelease = !rt.Equals("release", StringComparison.OrdinalIgnoreCase);

        // downloadUrl — Spiget 下载端点需要 resourceId + versionId
        // 格式: /resources/{resourceId}/versions/{versionId}/download
        if (!string.IsNullOrEmpty(version.Id))
        {
            version.DownloadUrl = $"{BaseUrl}/resources/{projectId}/versions/{version.Id}/download";
        }

        return version;
    }
}
