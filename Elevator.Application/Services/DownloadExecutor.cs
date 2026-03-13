using Elevator.Core.Enums;
using Elevator.Core.Exceptions;
using Elevator.Core.Interfaces;
using Elevator.Core.Models;
using Elevator.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Elevator.Application.Services;

/// <summary>
/// Singleton executor that carries out the actual file download for a queued job.
/// Called by the background consumer after it dequeues a job ID.
///
/// Responsibilities:
///   1. Load job from repository
///   2. Mark InProgress, write audit log
///   3. Stream the file from source via IStorageProvider
///   4. Report progress percentage to IDownloadProgressStore every chunk
///   5. Mark Completed / Failed, write audit log
/// </summary>
public sealed class DownloadExecutor : IDownloadExecutor
{
    private readonly IDownloadJobRepository   _repository;
    private readonly IDownloadProgressStore   _progressStore;
    private readonly IStorageProvider         _storage;
    private readonly IConfiguration           _config;
    private readonly ILogger<DownloadExecutor> _logger;

    public DownloadExecutor(
        IDownloadJobRepository    repository,
        IDownloadProgressStore    progressStore,
        IStorageProvider          storage,
        IConfiguration            config,
        ILogger<DownloadExecutor> logger)
    {
        _repository    = repository;
        _progressStore = progressStore;
        _storage       = storage;
        _config        = config;
        _logger        = logger;
    }

    public async Task ExecuteAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _repository.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            _logger.LogWarning("ExecuteAsync: job {Id} not found — skipping.", jobId);
            return;
        }

        if (job.Status is DownloadStatus.Completed or DownloadStatus.Cancelled)
        {
            _logger.LogWarning("ExecuteAsync: job {Id} already in terminal state '{State}' — skipping.", jobId, job.Status);
            return;
        }

        _logger.LogInformation("Starting download for job {Id} — '{File}'", jobId, job.FileName);

        // ── Mark InProgress ───────────────────────────────────────────────────
        var oldStatus   = job.Status;
        job.Status      = DownloadStatus.InProgress;
        job.StartedAt   = DateTime.UtcNow;
        job.UpdatedAt   = DateTime.UtcNow;
        await _repository.UpdateAsync(job, ct);
        await WriteAuditAsync(job.Id, oldStatus, DownloadStatus.InProgress, "Download started", ct);

        UpdateProgress(job, DownloadStatus.InProgress, 0, 0, null, null);

        try
        {
            string destinationFolder = _config["Storage:DownloadFolder"] ?? Path.GetTempPath();

            // Progress callback — called by IStorageProvider chunk-by-chunk
            var progressHandler = new Progress<(long bytesRead, long? totalBytes)>(report =>
            {
                int pct = report.totalBytes > 0
                    ? (int)(report.bytesRead * 100L / report.totalBytes!.Value)
                    : 0;

                job.BytesDownloaded  = report.bytesRead;
                job.FileSizeBytes  ??= report.totalBytes;
                job.ProgressPercent  = pct;

                UpdateProgress(job, DownloadStatus.InProgress, pct, report.bytesRead, report.totalBytes, null);

                _logger.LogDebug("Job {Id}: {Pct}% ({Bytes}/{Total} bytes)",
                    jobId, pct, report.bytesRead, report.totalBytes?.ToString() ?? "?");
            });

            // ── Stream file from source ───────────────────────────────────────
            string localPath = await _storage.DownloadToLocalAsync(
                job.FileUrl,
                destinationFolder,
                progressHandler,
                ct);

            // ── Mark Completed ────────────────────────────────────────────────
            job.Status          = DownloadStatus.Completed;
            job.LocalPath       = localPath;
            job.ProgressPercent = 100;
            job.CompletedAt     = DateTime.UtcNow;
            job.UpdatedAt       = DateTime.UtcNow;
            await _repository.UpdateAsync(job, ct);
            await WriteAuditAsync(job.Id, DownloadStatus.InProgress, DownloadStatus.Completed, "Download completed", ct);

            UpdateProgress(job, DownloadStatus.Completed, 100, job.BytesDownloaded, job.FileSizeBytes, null);
            _logger.LogInformation("Job {Id} completed. Saved to '{Path}'", jobId, localPath);
        }
        catch (OperationCanceledException)
        {
            await MarkFailedAsync(job, "Download was cancelled.", ct);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {Id} failed: {Msg}", jobId, ex.Message);
            await MarkFailedAsync(job, ex.Message, ct);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateProgress(
        DownloadJob    job,
        DownloadStatus status,
        int            pct,
        long           bytesRead,
        long?          total,
        string?        error)
    {
        _progressStore.SetProgress(job.Id, new DownloadProgress
        {
            JobId           = job.Id,
            Status          = status,
            ProgressPercent = pct,
            BytesDownloaded = bytesRead,
            TotalBytes      = total,
            ErrorMessage    = error,
            UpdatedAt       = DateTime.UtcNow
        });
    }

    private async Task MarkFailedAsync(DownloadJob job, string error, CancellationToken ct)
    {
        job.Status       = DownloadStatus.Failed;
        job.ErrorMessage = error;
        job.UpdatedAt    = DateTime.UtcNow;
        await _repository.UpdateAsync(job, ct);
        await WriteAuditAsync(job.Id, DownloadStatus.InProgress, DownloadStatus.Failed, error, ct);
        UpdateProgress(job, DownloadStatus.Failed, job.ProgressPercent, job.BytesDownloaded, job.FileSizeBytes, error);
    }

    private async Task WriteAuditAsync(
        Guid           jobId,
        DownloadStatus oldStatus,
        DownloadStatus newStatus,
        string         message,
        CancellationToken ct)
    {
        await _repository.AddAuditLogAsync(new DownloadAuditLog
        {
            JobId     = jobId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Message   = message,
            CreatedAt = DateTime.UtcNow
        }, ct);
    }
}
