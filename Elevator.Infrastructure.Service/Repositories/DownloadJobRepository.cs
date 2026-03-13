using Elevator.Core.Enums;
using Elevator.Core.Interfaces;
using Elevator.Core.Models;
using Elevator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Elevator.Infrastructure.Repositories;

/// <summary>
/// Repository backed by SQL Server via EF Core.
/// Registered as Singleton — uses IDbContextFactory to create a fresh,
/// thread-safe DbContext per operation.
/// </summary>
public sealed class DownloadJobRepository : IDownloadJobRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DownloadJobRepository(IDbContextFactory<AppDbContext> factory)
        => _factory = factory;

    public async Task<DownloadJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.DownloadJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<IReadOnlyList<DownloadJob>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.DownloadJobs.AsNoTracking()
                       .OrderByDescending(j => j.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DownloadJob>> GetByStatusAsync(DownloadStatus status, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.DownloadJobs.AsNoTracking()
                       .Where(j => j.Status == status)
                       .OrderByDescending(j => j.CreatedAt).ToListAsync(ct);
    }

    public async Task<DownloadJob> AddAsync(DownloadJob job, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.DownloadJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }

    public async Task UpdateAsync(DownloadJob job, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.DownloadJobs.Update(job);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var job = await db.DownloadJobs.FindAsync([id], ct);
        if (job is not null)
        {
            db.DownloadJobs.Remove(job);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task AddAuditLogAsync(DownloadAuditLog log, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.DownloadAuditLogs.Add(log);
        await db.SaveChangesAsync(ct);
    }
}
