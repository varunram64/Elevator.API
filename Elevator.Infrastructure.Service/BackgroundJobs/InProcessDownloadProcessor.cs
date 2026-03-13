using Elevator.Application.Interfaces;
using Elevator.Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Elevator.Infrastructure.BackgroundJobs;

/// <summary>
/// Hosted background service that drains the in-process Channel queue
/// (the fallback when RabbitMQ is unavailable).
///
/// Runs continuously — blocks on ReadAsync when the queue is empty,
/// wakes immediately when a new job is enqueued.
/// </summary>
public sealed class InProcessDownloadProcessor : BackgroundService
{
    private readonly InProcessDownloadQueue  _queue;
    private readonly IDownloadExecutor       _executor;
    private readonly ILogger<InProcessDownloadProcessor> _logger;

    public InProcessDownloadProcessor(
        InProcessDownloadQueue queue,
        IDownloadExecutor executor,
        ILogger<InProcessDownloadProcessor> logger)
    {
        _queue    = queue;
        _executor = executor;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("In-process download processor started.");

        await foreach (var jobId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("In-process processor picked up job {Id}", jobId);
                await _executor.ExecuteAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "In-process processor failed for job {Id}", jobId);
            }
        }

        _logger.LogInformation("In-process download processor stopped.");
    }
}
