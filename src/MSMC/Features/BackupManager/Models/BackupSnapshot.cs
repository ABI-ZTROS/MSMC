namespace io.NET.ZTR_OS.Features.BackupManager.Models;

public record BackupSnapshot(
    DateTime Timestamp,
    string Label,
    long SizeBytes,
    string BackupFilePath,
    string Sha1,
    List<string> WorldNames);
