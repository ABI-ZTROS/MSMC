// -----------------------------------------------------------------------------
// 文件名: PluginManagerService.cs
// 命名空间: io.NET.ZTR_OS.Features.ContentMarket.Services
// 功能描述: 插件管理服务 —— 安装/更新/卸载 + SHA1 校验 + 安全备份
// 设计模式: 三链原则 - 执行链：文件备份 + Hash 校验；返回链：安装审计日志
// -----------------------------------------------------------------------------

using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using io.NET.ZTR_OS.Features.ContentMarket.Models;
using Microsoft.Extensions.Logging;

namespace io.NET.ZTR_OS.Features.ContentMarket.Services;

/// <summary>
/// 插件管理服务 —— 负责下载、安装、更新、卸载
/// </summary>
public class PluginManagerService
{
    private readonly ILogger<PluginManagerService> _logger;
    private readonly IMarketProvider _provider;
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public PluginManagerService(ILogger<PluginManagerService> logger, IMarketProvider provider)
    {
        _logger = logger;
        _provider = provider;
    }

    /// <summary>
    /// 安装插件到服务器目录
    /// </summary>
    public async Task<InstallResult> InstallAsync(MarketVersion version, string serverPath, CancellationToken ct = default)
    {
        _logger.LogInformation("[PluginMgr] Starting installation: {VersionName} (Id={VersionId})",
            version.Name, version.Id);

        // 1. 校验输入
        if (string.IsNullOrEmpty(serverPath))
            return InstallResult.Failed("Server path cannot be empty");
        if (string.IsNullOrEmpty(version.DownloadUrl))
            return InstallResult.Failed("No download URL available for this version");

        var pluginsDir = Path.Combine(serverPath, "plugins");
        try
        {
            Directory.CreateDirectory(pluginsDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Failed to create plugins directory: {Path}", pluginsDir);
            return InstallResult.Failed($"Cannot create plugins directory: {ex.Message}");
        }

        // 2. 计算目标文件名
        string safeName = SanitizeFileName(version.Name);
        string destPath = Path.Combine(pluginsDir, $"{safeName}.jar");

        // 3. 安全备份
        string? backupPath = null;
        if (File.Exists(destPath))
        {
            try
            {
                string backupDir = Path.Combine(pluginsDir, ".msmc_backups");
                Directory.CreateDirectory(backupDir);
                backupPath = Path.Combine(backupDir, $"{safeName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.jar.bak");
                File.Copy(destPath, backupPath, overwrite: true);
                _logger.LogInformation("[PluginMgr] Backup created: {BackupPath}", backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PluginMgr] Backup failed, proceeding without backup");
                backupPath = null;
            }
        }

        // 4. 下载文件
        byte[] fileBytes;
        try
        {
            fileBytes = await _provider.DownloadVersionAsync(version.Id, progress: null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Download failed for version {VersionId}", version.Id);
            RestoreFromBackup(backupPath, destPath);
            return InstallResult.Failed($"Download failed: {ex.Message}");
        }

        // 5. SHA1 校验
        if (!string.IsNullOrEmpty(version.Sha1Hash))
        {
            var actualHash = ComputeSha1Hash(fileBytes);
            if (!actualHash.Equals(version.Sha1Hash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("[PluginMgr] SHA1 hash mismatch. Expected={Expected}, Actual={Actual}",
                    version.Sha1Hash, actualHash);
                RestoreFromBackup(backupPath, destPath);
                return InstallResult.Failed($"SHA1 hash mismatch: expected {version.Sha1Hash}, got {actualHash}");
            }
            _logger.LogInformation("[PluginMgr] SHA1 hash verified: {Hash}", actualHash);
        }

        // 6. 写入目标文件
        try
        {
            // 写入临时文件，成功后再原子替换
            string tempPath = destPath + ".tmp";
            await File.WriteAllBytesAsync(tempPath, fileBytes, ct);
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(tempPath, destPath);
            _logger.LogInformation("[PluginMgr] Plugin installed: {DestPath}", destPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Failed to write plugin file");
            RestoreFromBackup(backupPath, destPath);
            return InstallResult.Failed($"File write failed: {ex.Message}");
        }

        // 7. 记录安装信息
        await SaveInstallRecordAsync(serverPath, new InstalledPlugin
        {
            ProjectId = version.ProjectId,
            ProjectName = version.Name,
            VersionId = version.Id,
            VersionNumber = version.VersionNumber,
            FileName = $"{safeName}.jar",
            Sha1Hash = version.Sha1Hash ?? ComputeSha1Hash(fileBytes),
            Source = _provider.Source,
            InstalledAt = DateTimeOffset.UtcNow,
            ServerId = serverPath
        });

        _logger.LogInformation("[PluginMgr] Installation complete: {Name} v{Version}", version.Name, version.VersionNumber);
        return new InstallResult
        {
            Success = true,
            InstalledPath = destPath,
            BackupPath = backupPath
        };
    }

    /// <summary>
    /// 卸载插件
    /// </summary>
    public async Task<bool> UninstallAsync(string serverPath, string fileName)
    {
        string pluginsDir = Path.Combine(serverPath, "plugins");
        string destPath = Path.Combine(pluginsDir, fileName);

        if (!File.Exists(destPath))
        {
            _logger.LogWarning("[PluginMgr] Plugin file not found: {Path}", destPath);
            return false;
        }

        try
        {
            // 先备份再删除
            string backupDir = Path.Combine(pluginsDir, ".msmc_backups");
            Directory.CreateDirectory(backupDir);
            string backupPath = Path.Combine(backupDir, $"{fileName}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.jar.bak");
            File.Copy(destPath, backupPath, overwrite: true);

            File.Delete(destPath);
            _logger.LogInformation("[PluginMgr] Plugin uninstalled: {FileName} (backup at {Backup})", fileName, backupPath);

            await RemoveInstallRecordAsync(serverPath, fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Uninstall failed for: {FileName}", fileName);
            return false;
        }
    }

    /// <summary>
    /// 获取已安装插件列表
    /// </summary>
    public IReadOnlyList<InstalledPlugin> GetInstalledPlugins(string serverPath)
    {
        string metaPath = GetInstalledPluginsPath(serverPath);
        if (!File.Exists(metaPath))
            return new List<InstalledPlugin>();

        try
        {
            var json = File.ReadAllText(metaPath);
            return JsonSerializer.Deserialize<List<InstalledPlugin>>(json, _jsonOptions) ?? new List<InstalledPlugin>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PluginMgr] Failed to read installed plugins list");
            return new List<InstalledPlugin>();
        }
    }

    private static string GetInstalledPluginsPath(string serverPath)
    {
        return Path.Combine(serverPath, "plugins", ".msmc", "installed-plugins.json");
    }

    private async Task SaveInstallRecordAsync(string serverPath, InstalledPlugin record)
    {
        string metaDir = Path.Combine(serverPath, "plugins", ".msmc");
        Directory.CreateDirectory(metaDir);

        string metaPath = GetInstalledPluginsPath(serverPath);
        var list = GetInstalledPlugins(serverPath).ToList();

        // 更新或添加
        var existing = list.FirstOrDefault(p => p.FileName == record.FileName);
        if (existing != null)
        {
            list.Remove(existing);
        }
        list.Add(record);

        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(list, _jsonOptions));
        _logger.LogDebug("[PluginMgr] Install record saved: {File}", record.FileName);
    }

    private async Task RemoveInstallRecordAsync(string serverPath, string fileName)
    {
        string metaPath = GetInstalledPluginsPath(serverPath);
        if (!File.Exists(metaPath)) return;

        var list = GetInstalledPlugins(serverPath).ToList();
        list.RemoveAll(p => p.FileName == fileName);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(list, _jsonOptions));
    }

    private static void RestoreFromBackup(string? backupPath, string destPath)
    {
        if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath)) return;

        try
        {
            File.Copy(backupPath, destPath, overwrite: true);
        }
        catch
        {
            // 恢复失败不阻塞流程
        }
    }

    private static string ComputeSha1Hash(byte[] data)
    {
        using var sha1 = SHA1.Create();
        byte[] hashBytes = sha1.ComputeHash(data);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return safe.Length > 60 ? safe[..60] : safe;
    }
}
