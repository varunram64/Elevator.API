using Elevator.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace Elevator.Infrastructure.Messaging;

/// <summary>
/// Hosted background service that consumes job IDs from the RabbitMQ
/// "download_jobs" queue and delegates execution to IDownloadExecutor.
///
/// If RabbitMQ is unavailable at startup, logs a warning and exits gracefully.
/// </summary>
public sealed class RabbitMqDownloadConsumer : BackgroundService
{
    private readonly IConfiguration           _config;
    private readonly IDownloadExecutor        _executor;
    private readonly ILogger<RabbitMqDownloadConsumer> _logger;

    private IConnection? _connection;
    private IModel?      _channel;

    public RabbitMqDownloadConsumer(
        IConfiguration config,
        IDownloadExecutor executor,
        ILogger<RabbitMqDownloadConsumer> logger)
    {
        _config   = config;
        _executor = executor;
        _logger   = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName               = _config["RabbitMQ:Host"]     ?? "localhost",
                Port                   = int.Parse(_config["RabbitMQ:Port"] ?? "5672"),
                UserName               = _config["RabbitMQ:Username"] ?? "guest",
                Password               = _config["RabbitMQ:Password"] ?? "guest",
                DispatchConsumersAsync = true,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };

            _connection = factory.CreateConnection("download-consumer");
            _channel    = _connection.CreateModel();
            _channel.QueueDeclare(RabbitMqDownloadPublisher.QueueName,
                durable: true, exclusive: false, autoDelete: false);
            _channel.BasicQos(0, prefetchCount: 2, global: false); // process 2 concurrently

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnMessageReceivedAsync;
            _channel.BasicConsume(RabbitMqDownloadPublisher.QueueName, autoAck: false, consumer);

            _logger.LogInformation("RabbitMQ consumer started — listening on '{Q}'",
                RabbitMqDownloadPublisher.QueueName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ consumer could not connect — running without queue consumption.");
        }

        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        Guid jobId = Guid.Empty;
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var obj  = JObject.Parse(json);
            jobId    = Guid.Parse(obj["JobId"]!.ToString());

            _logger.LogInformation("Consumer received job {Id}", jobId);
            await _executor.ExecuteAsync(jobId);

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consumer failed to process job {Id}", jobId);
            // Requeue once so transient failures get a second chance
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
