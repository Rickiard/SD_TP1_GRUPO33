using System;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using WavyClient.Models;

namespace WavyClient.Services
{
    /// <summary>
    /// Service for handling RabbitMQ communication for WAVY devices.
    /// </summary>
    public class RabbitMQService : IDisposable
    {
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _userName;
        private readonly string _password;
        private readonly string _exchangeName;
        private readonly string _wavyId;
        private readonly string _sensorType;
        private readonly string _location;

        private IConnection _connection;
        private IModel _channel;
        private string _replyQueueName;
        private EventingBasicConsumer _consumer;
        private readonly ManualResetEvent _responseReceived = new ManualResetEvent(false);
        private string _responseMessage;

        /// <summary>
        /// Creates a new instance of the RabbitMQService class.
        /// </summary>
        /// <param name="hostName">The hostname of the RabbitMQ server.</param>
        /// <param name="port">The port of the RabbitMQ server.</param>
        /// <param name="userName">The username for the RabbitMQ server.</param>
        /// <param name="password">The password for the RabbitMQ server.</param>
        /// <param name="exchangeName">The name of the exchange to use.</param>
        /// <param name="wavyId">The unique identifier of the WAVY device.</param>
        /// <param name="sensorType">The type of sensor (used for routing).</param>
        /// <param name="location">The geographic location (used for routing).</param>
        public RabbitMQService(
            string hostName, 
            int port, 
            string userName, 
            string password, 
            string exchangeName,
            string wavyId,
            string sensorType,
            string location)
        {
            _hostName = hostName;
            _port = port;
            _userName = userName;
            _password = password;
            _exchangeName = exchangeName;
            _wavyId = wavyId;
            _sensorType = sensorType;
            _location = location;

            Initialize();
        }

        /// <summary>
        /// Initializes the RabbitMQ connection and channel.
        /// </summary>
        private void Initialize()
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _hostName,
                    Port = _port,
                    UserName = _userName,
                    Password = _password
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Declare a topic exchange
                _channel.ExchangeDeclare(
                    exchange: _exchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);

                // Declare a reply queue for receiving responses
                _replyQueueName = _channel.QueueDeclare().QueueName;

                _consumer = new EventingBasicConsumer(_channel);
                _consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    _responseMessage = Encoding.UTF8.GetString(body);
                    _responseReceived.Set();
                };

                _channel.BasicConsume(
                    queue: _replyQueueName,
                    autoAck: true,
                    consumer: _consumer);

                Console.WriteLine($"[WAVY{_wavyId}] Connected to RabbitMQ at {_hostName}:{_port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WAVY{_wavyId}] Failed to initialize RabbitMQ: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Publishes a message to the RabbitMQ exchange.
        /// </summary>
        /// <param name="message">The message to publish.</param>
        /// <param name="waitForResponse">Whether to wait for a response.</param>
        /// <param name="timeoutMs">The timeout in milliseconds for waiting for a response.</param>
        /// <returns>The response message, or null if no response is expected or received.</returns>
        public string PublishMessage(WavyMessage message, bool waitForResponse = false, int timeoutMs = 10000)
        {
            try
            {
                // Reset the response event
                if (waitForResponse)
                {
                    _responseReceived.Reset();
                }

                // Create a routing key based on sensor type and location
                string routingKey = $"{_sensorType}.{_location}.{message.Type.ToString().ToLower()}";

                var props = _channel.CreateBasicProperties();
                props.ContentType = "application/json";
                props.DeliveryMode = 2; // persistent

                if (waitForResponse)
                {
                    props.CorrelationId = Guid.NewGuid().ToString();
                    props.ReplyTo = _replyQueueName;
                }

                var messageBody = Encoding.UTF8.GetBytes(message.ToJson());

                _channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: routingKey,
                    basicProperties: props,
                    body: messageBody);

                Console.WriteLine($"[WAVY{_wavyId}] Published message: {message.Type} with routing key: {routingKey}");

                if (waitForResponse)
                {
                    bool received = _responseReceived.WaitOne(timeoutMs);
                    if (received)
                    {
                        return _responseMessage;
                    }
                    else
                    {
                        Console.WriteLine($"[WAVY{_wavyId}] Timeout waiting for response");
                        return null;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WAVY{_wavyId}] Failed to publish message: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Disposes the RabbitMQ connection and channel.
        /// </summary>
        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            _responseReceived?.Dispose();
        }
    }
}
