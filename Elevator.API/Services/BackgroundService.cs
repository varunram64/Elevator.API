using Elevator.API.Interfaces;
using Elevator.API.Models;
using Elevator.API.Producers;
using Elevator.API.Repository;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Elevator.API.Services
{
    public sealed class TripRequestConsumer : BackgroundService
    {
        private readonly ILogger<TripRequestConsumer> _logger;
        private readonly string _hostName;
        private readonly string _queueName;
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly IElevatorRepository _repo;
        private readonly IElevatorStateCache _stateCache;
        private readonly IElevatorDispatchService _dispatchService;
        private readonly IAssignedEventPublisher _assignedPublisher;

        public TripRequestConsumer(
            IConfiguration config,
            ILogger<TripRequestConsumer> logger,
            IElevatorRepository repo,
            IElevatorStateCache stateCache,
            IElevatorDispatchService dispatchService,
            IAssignedEventPublisher assignedPublisher)
        {
            _logger = logger;
            _repo = repo;
            _stateCache = stateCache;
            _dispatchService = dispatchService;
            _assignedPublisher = assignedPublisher;

            _hostName = config.GetValue<string>("RabbitMQ:HostName") ?? "localhost";
            _queueName = config.GetValue<string>("RabbitMQ:TripRequestQueue") ?? "trip-requests";

            var factory = new ConnectionFactory { HostName = _hostName };
            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            _channel.QueueDeclareAsync(_queueName, durable: true, exclusive: false, autoDelete: false, arguments: null).GetAwaiter().GetResult();
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (ch, ea) =>
            {
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var request = JsonSerializer.Deserialize<TripRequestMessage>(json);

                    if (request is null)
                    {
                        await _channel.BasicAckAsync(ea.DeliveryTag, false);
                        return;
                    }

                    var elevators = _repo.GetElevators();
                    var states = _stateCache.GetAll();

                    var assigned = _dispatchService.Assign(request, elevators, states);

                    if (assigned is not null)
                    {
                        _assignedPublisher.Publish(assigned);
                        // here you can also write a ServiceLog record to DB
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling trip request message");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
                }
            };

            _channel.BasicConsumeAsync(_queueName, autoAck: false, consumer: consumer).GetAwaiter().GetResult();
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
