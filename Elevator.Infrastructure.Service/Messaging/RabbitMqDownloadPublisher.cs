using Elevator.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;

namespace Elevator.Infrastructure.Messaging;

/// <summary>
/// Singleton RabbitMQ publisher.
/// Owns one long-lived connection + channel.
/// Gracefully degrades (logs warning, returns false) when RabbitMQ is unavailable.
/// </summary>
public sealed class RabbitMqDownloadPublisher : IDownloadMessagePublisher, IDisposable
{
    public const string QueueName = "download_jobs";

    private readonly ILogger<RabbitMqDownloadPublisher> _logger;
    private IConnection? _connection;
    private IModel?      _channel;
    private readonly bool _available;

    public RabbitMqDownloadPublisher(
        IConfiguration config,
        ILogger<RabbitMqDownloadPublisher> logger)
    {
        _logger = logger;
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = config["RabbitMQ:Host"] ?? "localhost",
                Port     = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
                UserName = config["RabbitMQ:Username"] ?? "guest",
                Password = config["RabbitMQ:Password"] ?? "guest",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };
            _connection = factory.CreateConnection("download-publisher");
            _channel    = _connection.CreateModel();
            _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);

            _available = true;
            _logger.LogInformation("RabbitMQ publisher connected — queue '{Q}'", QueueName);
        }
        catch (Exception ex)
        {
            _available = false;
            _logger.LogWarning(ex, "RabbitMQ unavailable — publisher disabled. Fallback queue active.");
        }
    }

    public bool TryPublish(Guid jobId)
    {
        if (!_available || _channel is null) return false;
        try
        {
            var body  = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { JobId = jobId }));
            var props = _channel.CreateBasicProperties();
            props.Persistent  = true;
            props.MessageId   = jobId.ToString();
            props.ContentType = "application/json";
            props.Timestamp   = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(string.Empty, QueueName, props, body);
            _logger.LogDebug("Published job {Id} to RabbitMQ", jobId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish job {Id}", jobId);
            return false;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
