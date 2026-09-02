// -----------------------------------------------------------------------------
// 文件名: HangarProvider.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: PaperMC Hangar API 提供器 —— 搜索/版本/下载
// 文档: https://hangar.papermc.io/api-docs
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
/// PaperMC Hangar API v1 客户端
/// Hangar 是 PaperMC 官方插件市场，主要收录 Paper/Purpur/Folia 生态的插件
/// </summary>
public class HangarProvider : IMarketProvider
{
    private const string BaseUrl = "https://hangar.papermc.io/api/v1";
    private readonly ILogger<HangarProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MarketSource Source => MarketSource.Hangar;

    public HangarProvider(ILogger<HangarProvider> logger)
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
        var queryString = HttpUtility.ParseQueryString(string.Empty);
        queryString["query"] = request.Query;
        queryString["limit"] = request.Limit.ToString();
        queryString["offset"] = request.Offset.ToString();

        if (!string.IsNullOrEmpty(request.Category))
            queryString["category"] = request.Category;

        var url = $"{BaseUrl}/projects?{queryString}";
        _logger.LogDebug("Hangar 搜索: {Url}", url);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var results = new List<MarketProject>();
            if (root.TryGetProperty("result", out var resultArr) && resultArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resultArr.EnumerateArray())
                {
                    results.Add(ParseProject(item));
                }
            }
            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Hangar 搜索 HTTP 错误");
            return Array.Empty<MarketProject>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hangar 搜索异常");
            return Array.Empty<MarketProject>();
        }
    }

    public async Task<IReadOnlyList<MarketVersion>> GetVersionsAsync(string projectId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/projects/{Uri.EscapeDataString(projectId)}/versions?limit=50";
        _logger.LogDebug("Hangar 版本查询: {Url}", url);

        try
        {
            var json = await _httpClient.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var versions = new List<MarketVersion>();
            if (root.TryGetProperty("result", out var resultArr) && resultArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resultArr.EnumerateArray())
                {
                    versions.Add(ParseVersion(item, projectId));
                }
            }
            return versions;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Hangar 版本查询 HTTP 错误");
            return Array.Empty<MarketVersion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hangar 版本查询异常");
            return Array.Empty<MarketVersion>();
        }
    }

    public async Task<byte[]> DownloadVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null, CancellationToken ct = default)
    {
        // Hangar 下载需要 projectSlug + versionName + downloadName，
        // 这里简化处理——直接返回空数组，让 Factory 走直链下载
        _logger.LogWarning("Hangar DownloadVersionAsync 需要完整版本信息，建议用直链下载");
        return Array.Empty<byte>();
    }

    private static MarketProject ParseProject(JsonElement item)
    {
        var project = new MarketProject
        {
            Source = MarketSource.Hangar,
            Id = item.TryGetProperty("id", out var idEl) ? idEl.GetInt32().ToString() : "",
            Name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
            Description = item.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? "" : "",
            UpdatedAt = item.TryGetProperty("lastUpdated", out var updEl) && updEl.TryGetDateTimeOffset(out var upd) ? upd : null,
        };

        // namespace.slug / namespace.owner
        if (item.TryGetProperty("namespace", out var nsEl))
        {
            if (nsEl.TryGetProperty("slug", out var slugEl))
                project.Slug = slugEl.GetString() ?? "";
            if (nsEl.TryGetProperty("owner", out var ownerEl))
                project.Author = ownerEl.GetString() ?? "";
        }

        // stats
        if (item.TryGetProperty("stats", out var statsEl))
        {
            if (statsEl.TryGetProperty("downloads", out var dlEl))
                project.Downloads = dlEl.GetInt64();
            if (statsEl.TryGetProperty("watchers", out var watchEl))
                project.Followers = watchEl.GetInt64();
        }

        // category
        if (item.TryGetProperty("category", out var catEl) && catEl.GetString() is { } cat)
            project.Categories.Add(cat);

        // supportedPlatforms (字典: PAPER -> versions[])
        if (item.TryGetProperty("supportedPlatforms", out var spEl) && spEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in spEl.EnumerateObject())
            {
                if (Enum.TryParse<ModLoader>(prop.Name, true, out var loader))
                    project.SupportedLoaders.Add(loader);

                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in prop.Value.EnumerateArray())
                    {
                        if (v.GetString() is { } gv && !project.GameVersions.Contains(gv))
                            project.GameVersions.Add(gv);
                    }
                }
            }
        }

        // iconUrl — Hangar 项目没有直接的 icon 字段，可用 namespace slug 构造头像
        if (item.TryGetProperty("namespace", out var ns) && ns.TryGetProperty("owner", out var owner))
        {
            var ownerStr = owner.GetString() ?? "";
            if (!string.IsNullOrEmpty(ownerStr))
                project.IconUrl = $"https://hangarcdn.papermc.io/avatars/{ownerStr}.png";
        }

        project.ProjectUrl = $"https://hangar.papermc.io/{project.Slug}";

        return project;
    }

    private static MarketVersion ParseVersion(JsonElement item, string projectId)
    {
        var version = new MarketVersion
        {
            ProjectId = projectId,
            Id = item.TryGetProperty("id", out var idEl) ? idEl.GetInt32().ToString() : "",
            VersionNumber = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "",
            Name = item.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "",
            Changelog = item.TryGetProperty("description", out var descEl) ? descEl.GetString() : null,
            ReleasedAt = item.TryGetProperty("createdAt", out var caEl) && caEl.TryGetDateTimeOffset(out var ca) ? ca : null,
        };

        // downloadUrl — Hangar 返回文件名为 downloadName
        if (item.TryGetProperty("downloads", out var dlEl) && dlEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in dlEl.EnumerateObject())
            {
                // 取 PAPER 的下载
                if (prop.Name.Equals("PAPER", StringComparison.OrdinalIgnoreCase)
                    && prop.Value.TryGetProperty("downloadName", out var dnEl))
                {
                    var downloadName = dnEl.GetString() ?? "";
                    if (!string.IsNullOrEmpty(downloadName))
                        version.DownloadUrl = $"{BaseUrl}/projects/{Uri.EscapeDataString(projectId)}/versions/{Uri.EscapeDataString(version.VersionNumber)}/{Uri.EscapeDataString(downloadName)}";
                }
            }
        }

        // platformDependencies (游戏版本)
        if (item.TryGetProperty("platformDependencies", out var pdEl) && pdEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in pdEl.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in prop.Value.EnumerateArray())
                    {
                        if (v.GetString() is { } gv && !version.GameVersions.Contains(gv))
                            version.GameVersions.Add(gv);
                    }
                }
            }
        }

        return version;
    }
}
