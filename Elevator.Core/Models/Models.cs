using Elevator.Core.Enums;

namespace Elevator.Core.Models;

/// <summary>
/// Represents a single download job tracked through its full lifecycle.
/// </summary>
public class DownloadJob
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string FileName    { get; set; } = string.Empty;
    public string FileUrl     { get; set; } = string.Empty;   // source URL or storage key
    public string ContentType { get; set; } = "application/octet-stream";
    public long?  FileSizeBytes { get; set; }

    public DownloadType     DownloadType     { get; set; } = DownloadType.File;
    public FileFormat       FileFormat       { get; set; } = FileFormat.Unknown;
    public StorageProvider  StorageProvider  { get; set; } = StorageProvider.LocalDisk;
    public DownloadStatus   Status           { get; set; } = DownloadStatus.Queued;

    public int    ProgressPercent { get; set; } = 0;
    public long   BytesDownloaded { get; set; } = 0;

    public string? ErrorMessage  { get; set; }
    public string? LocalPath     { get; set; }   // final local path after download
    public string? RequestedBy   { get; set; }

    public DateTime  CreatedAt   { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt   { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? UpdatedAt   { get; set; }

    // Metadata bag for extensibility
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Progress snapshot for a single download job — written by the background job,
/// read by the progress-tracking endpoint.
/// </summary>
public class DownloadProgress
{
    public Guid           JobId           { get; set; }
    public DownloadStatus Status          { get; set; }
    public int            ProgressPercent { get; set; }
    public long           BytesDownloaded { get; set; }
    public long?          TotalBytes      { get; set; }
    public string?        ErrorMessage    { get; set; }
    public DateTime       UpdatedAt       { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audit log entry for every status change on a DownloadJob.
/// </summary>
public class DownloadAuditLog
{
    public int           Id        { get; set; }
    public Guid          JobId     { get; set; }
    public DownloadStatus OldStatus { get; set; }
    public DownloadStatus NewStatus { get; set; }
    public string?       Message   { get; set; }
    public DateTime      CreatedAt { get; set; } = DateTime.UtcNow;
}
