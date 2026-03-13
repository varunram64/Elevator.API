using Elevator.Core.DTOs;
using Elevator.Core.Enums;

namespace Elevator.Application.Interfaces;

/// <summary>
/// Orchestrates the full download lifecycle:
/// enqueue → background download → serve file.
/// Registered as Singleton — uses IDbContextFactory internally.
/// </summary>
public interface IDownloadService
{
    Task<QueuedJobDto>          EnqueueAsync(CreateDownloadRequest request, CancellationToken ct = default);
    Task<DownloadJobDto?>       GetJobAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<DownloadJobDto>> GetAllJobsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<DownloadJobDto>> GetJobsByStatusAsync(DownloadStatus status, CancellationToken ct = default);
    Task<DownloadProgressDto?>  GetProgressAsync(Guid jobId, CancellationToken ct = default);
    Task<DownloadStreamDto?>    GetFileStreamAsync(Guid jobId, CancellationToken ct = default);
    Task<DownloadJobDto?>       CancelJobAsync(Guid jobId, CancellationToken ct = default);
    Task<DownloadJobDto?>       RetryJobAsync(Guid jobId, CancellationToken ct = default);
}

/// <summary>
/// Executes a single download job: download from source → track progress → persist.
/// Called by the background consumer.
/// </summary>
public interface IDownloadExecutor
{
    Task ExecuteAsync(Guid jobId, CancellationToken ct = default);
}
