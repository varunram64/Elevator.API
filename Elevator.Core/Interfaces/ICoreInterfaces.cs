using Elevator.Core.Enums;
using Elevator.Core.Models;

namespace Elevator.Core.Interfaces;

// ── Repository ────────────────────────────────────────────────────────────────

public interface IDownloadJobRepository
{
    Task<DownloadJob?>              GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DownloadJob>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DownloadJob>> GetByStatusAsync(DownloadStatus status, CancellationToken ct = default);
    Task<DownloadJob>               AddAsync(DownloadJob job, CancellationToken ct = default);
    Task                            UpdateAsync(DownloadJob job, CancellationToken ct = default);
    Task                            DeleteAsync(Guid id, CancellationToken ct = default);
    Task                            AddAuditLogAsync(DownloadAuditLog log, CancellationToken ct = default);
}

// ── Progress store (in-memory, fast reads) ────────────────────────────────────

public interface IDownloadProgressStore
{
    void              SetProgress(Guid jobId, DownloadProgress progress);
    DownloadProgress? GetProgress(Guid jobId);
    void              Remove(Guid jobId);
    IReadOnlyList<DownloadProgress> GetAll();
}

// ── Storage provider (file I/O abstraction) ───────────────────────────────────

public interface IStorageProvider
{
    /// <summary>
    /// Streams content from the source URL / key and writes it to a local
    /// temporary path, reporting progress via the callback.
    /// </summary>
    Task<string> DownloadToLocalAsync(
        string              sourceUrl,
        string              destinationFolder,
        IProgress<(long bytesRead, long? totalBytes)> progress,
        CancellationToken   ct = default);

    /// <summary>Returns a read stream for serving a file to the HTTP client.</summary>
    Task<(Stream stream, string contentType, long? length)> OpenReadAsync(
        string              path,
        CancellationToken   ct = default);

    Task<bool>   ExistsAsync(string path, CancellationToken ct = default);
    Task         DeleteAsync(string path, CancellationToken ct = default);
}

// ── Message queue ─────────────────────────────────────────────────────────────

public interface IDownloadMessageQueue
{
    bool TryEnqueue(Guid jobId);
    bool TryDequeue(out Guid jobId);
    int  Count { get; }
}

// ── RabbitMQ publisher ────────────────────────────────────────────────────────

public interface IDownloadMessagePublisher
{
    bool TryPublish(Guid jobId);
}
