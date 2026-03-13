using Elevator.Core.DTOs;
using Elevator.Core.Enums;
using Elevator.Core.Exceptions;
using Elevator.Core.Interfaces;
using Elevator.Core.Models;
using Elevator.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Elevator.Application.Services;

/// <summary>
/// Singleton application service that orchestrates the full download lifecycle.
///
/// Thread-safety notes:
/// • Uses IDownloadJobRepository (backed by IDbContextFactory) — each call
///   creates its own short-lived DbContext, so concurrency is safe.
/// • Uses IDownloadProgressStore (ConcurrentDictionary) — inherently thread-safe.
/// • Uses IDownloadMessagePublisher / IDownloadMessageQueue — both Singleton-safe.
/// </summary>
public sealed class DownloadService : IDownloadService
{
    private readonly IDownloadJobRepository     _repository;
    private readonly IDownloadProgressStore     _progressStore;
    private readonly IDownloadMessagePublisher  _publisher;
    private readonly IDownloadMessageQueue      _localQueue;
    private readonly IStorageProvider           _storage;
    private readonly ILogger<DownloadService>   _logger;

    public DownloadService(
        IDownloadJobRepository    repository,
        IDownloadProgressStore    progressStore,
        IDownloadMessagePublisher publisher,
        IDownloadMessageQueue     localQueue,
        IStorageProvider          storage,
        ILogger<DownloadService>  logger)
    {
        _repository    = repository;
        _progressStore = progressStore;
        _publisher     = publisher;
        _localQueue    = localQueue;
        _storage       = storage;
        _logger        = logger;
    }

    // =========================================================================
    // Enqueue
    // =========================================================================

    public async Task<QueuedJobDto> EnqueueAsync(CreateDownloadRequest request, CancellationToken ct = default)
    {
        var job = new DownloadJob
        {
            Id              = Guid.NewGuid(),
            FileName        = request.FileName,
            FileUrl         = request.FileUrl,
            DownloadType    = request.DownloadType,
            FileFormat      = request.FileFormat,
            StorageProvider = request.StorageProvider,
            RequestedBy     = request.RequestedBy,
            Status          = DownloadStatus.Queued,
            Metadata        = request.Metadata ?? new(),
            CreatedAt       = DateTime.UtcNow
        };

        await _repository.AddAsync(job, ct);

        // Initialize progress entry
        _progressStore.SetProgress(job.Id, new DownloadProgress
        {
            JobId           = job.Id,
            Status          = DownloadStatus.Queued,
            ProgressPercent = 0,
            BytesDownloaded = 0
        });

        // Publish to RabbitMQ (for distributed processing)
        bool publishedToRabbit = _publisher.TryPublish(job.Id);
        if (!publishedToRabbit)
        {
            // Fallback: push to in-process queue
            _localQueue.TryEnqueue(job.Id);
            _logger.LogWarning("RabbitMQ unavailable — job {Id} queued locally.", job.Id);
        }

        _logger.LogInformation("Enqueued download job {Id} for '{File}'", job.Id, job.FileName);

        return new QueuedJobDto(
            job.Id,
            $"Download job '{job.FileName}' queued successfully.",
            publishedToRabbit);
    }

    // =========================================================================
    // Queries
    // =========================================================================

    public async Task<DownloadJobDto?> GetJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(jobId, ct);
        return job is null ? null : MapToDto(job);
    }

    public async Task<IReadOnlyList<DownloadJobDto>> GetAllJobsAsync(CancellationToken ct = default)
    {
        var jobs = await _repository.GetAllAsync(ct);
        return jobs.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<DownloadJobDto>> GetJobsByStatusAsync(
        DownloadStatus status, CancellationToken ct = default)
    {
        var jobs = await _repository.GetByStatusAsync(status, ct);
        return jobs.Select(MapToDto).ToList().AsReadOnly();
    }

    public Task<DownloadProgressDto?> GetProgressAsync(Guid jobId, CancellationToken ct = default)
    {
        var progress = _progressStore.GetProgress(jobId);
        if (progress is null) return Task.FromResult<DownloadProgressDto?>(null);

        return Task.FromResult<DownloadProgressDto?>(new DownloadProgressDto(
            progress.JobId,
            progress.Status,
            progress.ProgressPercent,
            progress.BytesDownloaded,
            progress.TotalBytes,
            progress.ErrorMessage,
            progress.UpdatedAt));
    }

    // =========================================================================
    // Serve file
    // =========================================================================

    public async Task<DownloadStreamDto?> GetFileStreamAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(jobId, ct);
        if (job is null || job.Status != DownloadStatus.Completed || job.LocalPath is null)
            return null;

        var (stream, contentType, length) = await _storage.OpenReadAsync(job.LocalPath, ct);
        return new DownloadStreamDto(stream, job.FileName, contentType, length);
    }

    // =========================================================================
    // Cancel
    // =========================================================================

    public async Task<DownloadJobDto?> CancelJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(jobId, ct);
        if (job is null) return null;

        if (job.Status is DownloadStatus.Completed or DownloadStatus.Cancelled)
            throw new InvalidDownloadStateException(
                $"Cannot cancel job in '{job.Status}' state.");

        var oldStatus = job.Status;
        job.Status    = DownloadStatus.Cancelled;
        job.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(job, ct);

        await _repository.AddAuditLogAsync(new DownloadAuditLog
        {
            JobId     = job.Id,
            OldStatus = oldStatus,
            NewStatus = DownloadStatus.Cancelled,
            Message   = "Cancelled by user"
        }, ct);

        _progressStore.Remove(jobId);
        _logger.LogInformation("Job {Id} cancelled.", jobId);
        return MapToDto(job);
    }

    // =========================================================================
    // Retry
    // =========================================================================

    public async Task<DownloadJobDto?> RetryJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(jobId, ct);
        if (job is null) return null;

        if (job.Status is not (DownloadStatus.Failed or DownloadStatus.Cancelled))
            throw new InvalidDownloadStateException(
                $"Cannot retry job in '{job.Status}' state. Only Failed or Cancelled jobs can be retried.");

        var oldStatus         = job.Status;
        job.Status            = DownloadStatus.Queued;
        job.ProgressPercent   = 0;
        job.BytesDownloaded   = 0;
        job.ErrorMessage      = null;
        job.StartedAt         = null;
        job.CompletedAt       = null;
        job.UpdatedAt         = DateTime.UtcNow;
        await _repository.UpdateAsync(job, ct);

        await _repository.AddAuditLogAsync(new DownloadAuditLog
        {
            JobId     = job.Id,
            OldStatus = oldStatus,
            NewStatus = DownloadStatus.Queued,
            Message   = "Retried by user"
        }, ct);

        _progressStore.SetProgress(job.Id, new DownloadProgress
        {
            JobId  = job.Id,
            Status = DownloadStatus.Queued
        });

        bool publishedToRabbit = _publisher.TryPublish(job.Id);
        if (!publishedToRabbit) _localQueue.TryEnqueue(job.Id);

        _logger.LogInformation("Job {Id} re-queued for retry.", jobId);
        return MapToDto(job);
    }

    // =========================================================================
    // Mapping
    // =========================================================================

    private static DownloadJobDto MapToDto(DownloadJob j) => new(
        j.Id, j.FileName, j.FileUrl, j.ContentType, j.FileSizeBytes,
        j.DownloadType, j.FileFormat, j.StorageProvider, j.Status,
        j.ProgressPercent, j.BytesDownloaded, j.ErrorMessage,
        j.RequestedBy, j.CreatedAt, j.StartedAt, j.CompletedAt);
}
