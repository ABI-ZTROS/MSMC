namespace io.NET.ZTR_OS.Features.CoreDownloader.Models;

public enum CoreDownloadStatus
{
    Scheduled,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public record CoreDownloadResult(
    CoreDownloadStatus Status,
    string? SavedFilePath = null,
    long DownloadedBytes = 0,
    long TotalBytes = 0,
    string? ErrorMessage = null,
    double ElapsedMs = 0,
    bool HashVerified = false);
