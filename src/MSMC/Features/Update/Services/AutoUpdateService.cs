// -----------------------------------------------------------------------------
// 文件名: AutoUpdateService.cs
// 命名空间: io.NET.ZTR_OS.Features.Update.Services
// 功能描述: 自动更新服务 —— 检查版本、下载更新、哈希校验
// 设计模式: 三链原则 - 因果链：版本差异 → 更新流程；执行链：下载→校验→原子替换；返回链：全链路日志
// -----------------------------------------------------------------------------

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.Update.Services;

/// <summary>
/// 自动更新服务
/// </summary>
public class AutoUpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/your-org/msmc/releases/latest";
    private readonly ILogger<AutoUpdateService> _logger;
    private readonly string _currentVersion;
    private readonly string _updateDirectory;
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AutoUpdateService(ILogger<AutoUpdateService> logger, string currentVersion, string updateDirectory)
    {
        _logger = logger;
        _currentVersion = currentVersion;
        _updateDirectory = updateDirectory;
        
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders =
            {
                UserAgent = { new ProductInfoHeaderValue("MSMC", currentVersion) },
                Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
            }
        };
    }

    /// <summary>
    /// 检查更新
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[AutoUpdate] Checking for updates...");
        
        try
        {
            var response = await _httpClient.GetAsync(GitHubApiUrl, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[AutoUpdate] Failed to check for updates: HTTP {StatusCode}", 
                    response.StatusCode);
                return new UpdateCheckResult { Success = false, Error = $"HTTP {response.StatusCode}" };
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json, _jsonOptions);
            
            if (release == null)
            {
                _logger.LogWarning("[AutoUpdate] Invalid release response");
                return new UpdateCheckResult { Success = false, Error = "Invalid response" };
            }

            var latestVersion = release.TagName?.TrimStart('v') ?? string.Empty;
            var isNewer = IsNewerVersion(latestVersion, _currentVersion);
            
            _logger.LogInformation("[AutoUpdate] Current: {Current}, Latest: {Latest}, IsNewer: {IsNewer}",
                _currentVersion, latestVersion, isNewer);

            return new UpdateCheckResult
            {
                Success = true,
                CurrentVersion = _currentVersion,
                LatestVersion = latestVersion,
                IsUpdateAvailable = isNewer,
                ReleaseNotes = release.Body ?? string.Empty,
                PublishedAt = release.PublishedAt,
                DownloadUrl = release.Assets?
                    .FirstOrDefault(a => a.Name?.EndsWith(".zip") == true)?.BrowserDownloadUrl
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[AutoUpdate] Update check cancelled");
            return new UpdateCheckResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoUpdate] Failed to check for updates");
            return new UpdateCheckResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// 下载更新
    /// </summary>
    public async Task<UpdateDownloadResult> DownloadUpdateAsync(string downloadUrl, string expectedHash, 
        IProgress<(long Downloaded, long Total)>? progress = null, CancellationToken ct = default)
    {
        _logger.LogInformation("[AutoUpdate] Downloading update from {Url}", downloadUrl);
        
        try
        {
            Directory.CreateDirectory(_updateDirectory);
            var tempPath = Path.Combine(_updateDirectory, "update.zip.tmp");
            
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[AutoUpdate] Download failed: HTTP {StatusCode}", response.StatusCode);
                return new UpdateDownloadResult { Success = false, Error = $"HTTP {response.StatusCode}" };
            }

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var contentStream = await response.Content.ReadAsStreamAsync(ct);
            
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;
                    progress?.Report((totalRead, totalBytes));
                }
            }

            // 哈希校验
            if (!string.IsNullOrEmpty(expectedHash))
            {
                var actualHash = ComputeSha256Hash(tempPath);
                if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("[AutoUpdate] Hash mismatch. Expected: {Expected}, Actual: {Actual}",
                        expectedHash, actualHash);
                    File.Delete(tempPath);
                    return new UpdateDownloadResult 
                    { 
                        Success = false, 
                        Error = $"Hash mismatch: expected {expectedHash}, got {actualHash}" 
                    };
                }
                _logger.LogInformation("[AutoUpdate] Hash verified: {Hash}", actualHash);
            }

            // 重命名为正式文件
            var finalPath = Path.Combine(_updateDirectory, "update.zip");
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
            
            _logger.LogInformation("[AutoUpdate] Download completed: {Path}", finalPath);
            
            return new UpdateDownloadResult
            {
                Success = true,
                DownloadedPath = finalPath,
                FileSize = totalBytes,
                FileHash = ComputeSha256Hash(finalPath)
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[AutoUpdate] Download cancelled");
            return new UpdateDownloadResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AutoUpdate] Download failed");
            return new UpdateDownloadResult { Success = false, Error = ex.Message };
        }
    }

    private static bool IsNewerVersion(string latest, string current)
    {
        if (string.IsNullOrEmpty(latest)) return false;
        if (string.IsNullOrEmpty(current)) return true;
        
        return Version.TryParse(latest, out var latestVer) 
            && Version.TryParse(current, out var currentVer)
            && latestVer > currentVer;
    }

    private static string ComputeSha256Hash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

/// <summary>
/// 更新检查结果
/// </summary>
public class UpdateCheckResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? CurrentVersion { get; set; }
    public string? LatestVersion { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// 下载结果
/// </summary>
public class UpdateDownloadResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? DownloadedPath { get; set; }
    public long FileSize { get; set; }
    public string? FileHash { get; set; }
}

/// <summary>
/// GitHub Release 模型
/// </summary>
public class GitHubRelease
{
    public string TagName { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<GitHubAsset>? Assets { get; set; }
}

/// <summary>
/// GitHub Asset 模型
/// </summary>
public class GitHubAsset
{
    public string? Name { get; set; }
    public string? BrowserDownloadUrl { get; set; }
    public long Size { get; set; }
}
