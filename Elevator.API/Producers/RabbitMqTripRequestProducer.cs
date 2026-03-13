using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Elevator.API.Models;

namespace Elevator.API.Producers
{
    public interface ITripRequestProducer
    {
        void Publish(TripRequestMessage message);
    }

    public sealed class RabbitMqTripRequestProducer : ITripRequestProducer
    {
        private readonly string _hostName;
        private readonly string _queueName;

        public RabbitMqTripRequestProducer(IConfiguration config)
        {
            _hostName = config.GetValue<string>("RabbitMQ:HostName") ?? "localhost";
            _queueName = config.GetValue<string>("RabbitMQ:TripRequestQueue") ?? "trip-requests";
        }

        public void Publish(TripRequestMessage message)
        {
            var factory = new ConnectionFactory { HostName = _hostName };

            // Use synchronous API to ensure CreateBasicProperties and BasicPublish are available.
            using var connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            using var channel = connection.CreateChannelAsync().GetAwaiter().GetResult();

            // Declare queue (synchronous)
            channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false, arguments: null).GetAwaiter().GetResult();

            // Serialize message and get bytes
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // Publish synchronously
            channel.BasicPublishAsync(exchange: "", routingKey: _queueName, body: body).GetAwaiter().GetResult();
        }
    }
}
