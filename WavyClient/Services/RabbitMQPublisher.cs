using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Shared.Models;

namespace WavyClient.Services
{
    public class RabbitMQPublisher : IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _wavyId;
        private readonly string _sensorType;
        private readonly string _location;
        private const string ExchangeName = "wavy_data_exchange";

        public RabbitMQPublisher(string hostName, int port, string user, string password, string wavyId, string sensorType, string location)
        {
            _wavyId = wavyId;
            _sensorType = sensorType;
            _location = location;

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
        }

        public string PublishMessage(WavyMessage message, bool waitForResponse = false, int timeoutMs = 5000)
        {
            var routingKey = $"wavy.{_wavyId}.{message.Type.ToString().ToLower()}";
            var body = Encoding.UTF8.GetBytes(message.ToJson());

            if (waitForResponse)
            {
                // Declare a temporary queue for responses
                var replyQueueName = _channel.QueueDeclare().QueueName;
                var correlationId = Guid.NewGuid().ToString();

                var props = _channel.CreateBasicProperties();
                props.CorrelationId = correlationId;
                props.ReplyTo = replyQueueName;

                // Publish the message
                _channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: routingKey,
                    basicProperties: props,
                    body: body);

                // Wait for response
                var consumer = new EventingBasicConsumer(_channel);
                var response = new TaskCompletionSource<string>();

                consumer.Received += (model, ea) =>
                {
                    if (ea.BasicProperties.CorrelationId == correlationId)
                    {
                        var responseBody = Encoding.UTF8.GetString(ea.Body.ToArray());
                        response.SetResult(responseBody);
                    }
                };

                _channel.BasicConsume(
                    consumer: consumer,
                    queue: replyQueueName,
                    autoAck: true);

                // Wait for response with timeout
                if (response.Task.Wait(timeoutMs))
                {
                    return response.Task.Result;
                }
                return null;
            }
            else
            {
                // Publish without waiting for response
                _channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: routingKey,
                    basicProperties: null,
                    body: body);
                return null;
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
} 