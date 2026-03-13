using System.Threading.Channels;
using Elevator.Core.Interfaces;

namespace Elevator.Infrastructure.Messaging;

/// <summary>
/// In-process download queue using System.Threading.Channels.
/// Used as a fallback when RabbitMQ is unavailable.
/// Singleton — Channel is thread-safe and lock-free.
/// </summary>
public sealed class InProcessDownloadQueue : IDownloadMessageQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

    public bool TryEnqueue(Guid jobId)
        => _channel.Writer.TryWrite(jobId);

    public bool TryDequeue(out Guid jobId)
        => _channel.Reader.TryRead(out jobId);

    public int Count
        => _channel.Reader.Count;

    /// <summary>
    /// Async read — awaited by the background processor when the queue is empty.
    /// </summary>
    public ValueTask<Guid> ReadAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAsync(ct);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
}
