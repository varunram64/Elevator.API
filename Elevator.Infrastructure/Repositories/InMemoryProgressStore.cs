using System.Collections.Concurrent;
using Elevator.Core.Interfaces;
using Elevator.Core.Models;

namespace Elevator.Infrastructure.Repositories;

/// <summary>
/// In-memory thread-safe progress store.
/// Singleton — ConcurrentDictionary handles all concurrency.
/// Provides O(1) reads so the /progress endpoint is always fast.
/// </summary>
public sealed class InMemoryProgressStore : IDownloadProgressStore
{
    private readonly ConcurrentDictionary<Guid, DownloadProgress> _store = new();

    public void SetProgress(Guid jobId, DownloadProgress progress)
        => _store[jobId] = progress with { UpdatedAt = DateTime.UtcNow };

    public DownloadProgress? GetProgress(Guid jobId)
        => _store.TryGetValue(jobId, out var p) ? p : null;

    public void Remove(Guid jobId)
        => _store.TryRemove(jobId, out _);

    public IReadOnlyList<DownloadProgress> GetAll()
        => _store.Values.ToList().AsReadOnly();
}
