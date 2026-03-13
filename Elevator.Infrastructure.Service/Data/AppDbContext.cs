using Elevator.Core.Enums;
using Elevator.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Elevator.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DownloadJob>      DownloadJobs      => Set<DownloadJob>();
    public DbSet<DownloadAuditLog> DownloadAuditLogs => Set<DownloadAuditLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── DownloadJob ───────────────────────────────────────────────────────
        mb.Entity<DownloadJob>(e =>
        {
            e.HasKey(j => j.Id);
            e.Property(j => j.FileName).IsRequired().HasMaxLength(500);
            e.Property(j => j.FileUrl).IsRequired().HasMaxLength(2000);
            e.Property(j => j.ContentType).HasMaxLength(200);
            e.Property(j => j.RequestedBy).HasMaxLength(200);
            e.Property(j => j.LocalPath).HasMaxLength(1000);
            e.Property(j => j.ErrorMessage).HasMaxLength(2000);

            e.Property(j => j.Status)
             .HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(j => j.DownloadType)
             .HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(j => j.FileFormat)
             .HasConversion<string>().HasMaxLength(30).IsRequired();
            e.Property(j => j.StorageProvider)
             .HasConversion<string>().HasMaxLength(30).IsRequired();

            // Store metadata dict as JSON column
            e.Property(j => j.Metadata)
             .HasConversion(
                 v => Newtonsoft.Json.JsonConvert.SerializeObject(v),
                 v => Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(v)
                      ?? new Dictionary<string, string>())
             .HasMaxLength(4000);

            e.HasIndex(j => j.Status);
            e.HasIndex(j => j.CreatedAt);
            e.HasIndex(j => j.RequestedBy);
        });

        // ── DownloadAuditLog ──────────────────────────────────────────────────
        mb.Entity<DownloadAuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Message).HasMaxLength(1000);
            e.Property(a => a.OldStatus).HasConversion<string>().HasMaxLength(30);
            e.Property(a => a.NewStatus).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(a => a.JobId);
        });
    }
}
