using Elevator.Application.Interfaces;
using Elevator.Application.Services;
using Elevator.Core.Interfaces;
using Elevator.Infrastructure.BackgroundJobs;
using Elevator.Infrastructure.Data;
using Elevator.Infrastructure.Repositories;
using Elevator.Infrastructure.Storage;
using Elevator.Infrastructure.BackgroundJobs;
using Elevator.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elevator.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all infrastructure and application services.
    /// All core services are Singleton.
    /// </summary>
    public static IServiceCollection AddDownloadInfrastructure(
        this IServiceCollection services,
        IConfiguration          config)
    {
        // ── Database ──────────────────────────────────────────────────────────
        // IDbContextFactory is thread-safe and Singleton-compatible.
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(
                config.GetConnectionString("Default"),
                sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        // ── HTTP client for downloading remote files ───────────────────────────
        services.AddHttpClient("downloader", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(30);
            client.DefaultRequestHeaders.Add("User-Agent", "Elevator/1.0");
        });

        // ── Singleton Repositories / Stores ───────────────────────────────────
        services.AddSingleton<IDownloadJobRepository,  DownloadJobRepository>();
        services.AddSingleton<IDownloadProgressStore,  InMemoryProgressStore>();

        // ── Singleton Messaging ───────────────────────────────────────────────
        // InProcessDownloadQueue is exposed both as IDownloadMessageQueue (interface)
        // and as InProcessDownloadQueue (concrete) so the background processor
        // can call ReadAllAsync.
        services.AddSingleton<InProcessDownloadQueue>();
        services.AddSingleton<IDownloadMessageQueue>(sp =>
            sp.GetRequiredService<InProcessDownloadQueue>());

        services.AddSingleton<IDownloadMessagePublisher, RabbitMqDownloadPublisher>();

        // ── Singleton Storage ─────────────────────────────────────────────────
        services.AddSingleton<IStorageProvider, LocalDiskStorageProvider>();

        // ── Singleton Application Services ────────────────────────────────────
        services.AddSingleton<IDownloadService,  DownloadService>();
        services.AddSingleton<IDownloadExecutor, DownloadExecutor>();

        // ── Hosted Background Services ────────────────────────────────────────
        services.AddHostedService<RabbitMqDownloadConsumer>();
        services.AddHostedService<InProcessDownloadProcessor>();

        return services;
    }
}
