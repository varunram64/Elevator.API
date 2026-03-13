using Elevator.Core.Enums;

namespace Elevator.Core.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateDownloadRequest(
    string         FileUrl,
    string         FileName,
    DownloadType   DownloadType       = DownloadType.File,
    FileFormat     FileFormat         = FileFormat.Unknown,
    StorageProvider StorageProvider   = StorageProvider.LocalDisk,
    string?        RequestedBy        = null,
    Dictionary<string, string>? Metadata = null
);

public record CancelDownloadRequest(Guid JobId, string? Reason = null);

// ── Responses ─────────────────────────────────────────────────────────────────

public record DownloadJobDto(
    Guid            Id,
    string          FileName,
    string          FileUrl,
    string          ContentType,
    long?           FileSizeBytes,
    DownloadType    DownloadType,
    FileFormat      FileFormat,
    StorageProvider StorageProvider,
    DownloadStatus  Status,
    int             ProgressPercent,
    long            BytesDownloaded,
    string?         ErrorMessage,
    string?         RequestedBy,
    DateTime        CreatedAt,
    DateTime?       StartedAt,
    DateTime?       CompletedAt
);

public record DownloadProgressDto(
    Guid            JobId,
    DownloadStatus  Status,
    int             ProgressPercent,
    long            BytesDownloaded,
    long?           TotalBytes,
    string?         ErrorMessage,
    DateTime        UpdatedAt
);

public record QueuedJobDto(
    Guid   JobId,
    string Message,
    bool   PublishedToRabbitMq
);

public record DownloadStreamDto(
    Stream  Stream,
    string  FileName,
    string  ContentType,
    long?   Length
);
