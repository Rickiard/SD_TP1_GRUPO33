using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using Shared.Models;

namespace AggregatorServer.Services
{
    public class RabbitMQSubscriber : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _aggregatorId;
        private const string ExchangeName = "wavy_data_exchange";
        private const string QueueName = "aggregator_queue";

        public RabbitMQSubscriber(string hostName, int port, string user, string password, string aggregatorId)
        {
            _aggregatorId = aggregatorId;

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = user,
                Password = password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare the exchange
            _channel.ExchangeDeclare(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            // Declare the queue
            _channel.QueueDeclare(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Bind the queue to the exchange with routing patterns
            _channel.QueueBind(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: "wavy.#.hello");
            _channel.QueueBind(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: "wavy.#.datacsv");
            _channel.QueueBind(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: "wavy.#.quit");
        }

        public void StartConsuming(Action<WavyMessage, IBasicProperties, Action<string>> messageHandler)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var wavyMessage = WavyMessage.FromJson(message);

                // Create a callback to send response
                Action<string> sendResponse = (response) =>
                {
                    if (ea.BasicProperties.ReplyTo != null)
                    {
                        var responseProps = _channel.CreateBasicProperties();
                        responseProps.CorrelationId = ea.BasicProperties.CorrelationId;

                        var responseBody = Encoding.UTF8.GetBytes(response);
                        _channel.BasicPublish(
                            exchange: "",
                            routingKey: ea.BasicProperties.ReplyTo,
                            basicProperties: responseProps,
                            body: responseBody);
                    }
                };

                messageHandler(wavyMessage, ea.BasicProperties, sendResponse);
            };

            _channel.BasicConsume(
                queue: QueueName,
                autoAck: true,
                consumer: consumer);
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
} 