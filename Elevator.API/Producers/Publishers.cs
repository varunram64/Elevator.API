using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Elevator.API.Producers
{
    public interface IAssignedEventPublisher
    {
        void Publish(AssignedElevatorEvent evt);
    }

    public sealed class RabbitMqAssignedEventPublisher : IAssignedEventPublisher
    {
        private readonly string _hostName;
        private readonly string _queueName;

        public RabbitMqAssignedEventPublisher(IConfiguration config)
        {
            _hostName = config.GetValue<string>("RabbitMQ:HostName") ?? "localhost";
            _queueName = config.GetValue<string>("RabbitMQ:AssignedQueue") ?? "assigned-elevators";
        }

        public void Publish(AssignedElevatorEvent evt)
        {
            var factory = new ConnectionFactory { HostName = _hostName };
            using var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            using var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();

            channel.QueueDeclareAsync(_queueName, durable: true, exclusive: false, autoDelete: false, arguments: null).GetAwaiter().GetResult();

            var json = JsonSerializer.Serialize(evt);
            var body = Encoding.UTF8.GetBytes(json);

            channel.BasicPublishAsync("", _queueName, body: body).GetAwaiter().GetResult();
        }
    }
}
