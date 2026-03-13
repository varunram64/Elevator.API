using Elevator.API.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Elevator.API.Services
{
    public sealed class ElevatorPositionUpdate
    {
        public int ElevatorId { get; set; }
        public int CurrentFloor { get; set; }
        public Direction? Direction { get; set; }
        public bool IsBusy { get; set; }
    }

    public sealed class ElevatorStateConsumer : BackgroundService
    {
        private readonly IElevatorStateCache _stateCache;
        private readonly ILogger<ElevatorStateConsumer> _logger;
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly string _queueName;

        public ElevatorStateConsumer(
            IElevatorStateCache stateCache,
            IConfiguration configuration,
            ILogger<ElevatorStateConsumer> logger)
        {
            _stateCache = stateCache;
            _logger = logger;

            var factory = new ConnectionFactory
            {
                HostName = configuration.GetValue<string>("RabbitMQ:HostName") ?? "localhost"
            };

            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            _queueName = configuration.GetValue<string>("RabbitMQ:PositionQueue") ?? "ElevatorPositions";

            _channel.QueueDeclareAsync(
                queue: _queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null).GetAwaiter().GetResult();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, args) =>
            {
                try
                {
                    var body = args.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    var update = JsonSerializer.Deserialize<ElevatorPositionUpdate>(json);

                    if (update is null)
                        return;

                    _stateCache.Upsert(new ElevatorRuntimeState
                    {
                        ElevatorId = update.ElevatorId,
                        CurrentFloor = update.CurrentFloor,
                        CurrentDirection = update.Direction,
                        IsBusy = update.IsBusy,
                        LastUpdatedUtc = DateTime.UtcNow
                    });

                    await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing elevator position message");
                    await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumer: consumer).GetAwaiter().GetResult();

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel.CloseAsync();
            _connection.CloseAsync();
            base.Dispose();
        }
    }

}
