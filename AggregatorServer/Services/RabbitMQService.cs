using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AggregatorServer.Models;

namespace AggregatorServer.Services
{
    /// <summary>
    /// Service for handling RabbitMQ communication for Aggregator servers.
    /// </summary>
    public class RabbitMQService : IDisposable
    {
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _userName;
        private readonly string _password;
        private readonly string _exchangeName;
        private readonly string _aggregatorId;
        private readonly List<string> _sensorTypes;
        private readonly List<string> _locations;

        private IConnection _connection;
        private IModel _channel;
        private List<string> _queueNames = new List<string>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        // Delegate for message handling
        public delegate Task MessageHandlerDelegate(WavyMessage message, IModel channel, BasicDeliverEventArgs eventArgs);
        private readonly MessageHandlerDelegate _messageHandler;

        /// <summary>
        /// Creates a new instance of the RabbitMQService class.
        /// </summary>
        /// <param name="hostName">The hostname of the RabbitMQ server.</param>
        /// <param name="port">The port of the RabbitMQ server.</param>
        /// <param name="userName">The username for the RabbitMQ server.</param>
        /// <param name="password">The password for the RabbitMQ server.</param>
        /// <param name="exchangeName">The name of the exchange to use.</param>
        /// <param name="aggregatorId">The unique identifier of the Aggregator server.</param>
        /// <param name="sensorTypes">The types of sensors to subscribe to.</param>
        /// <param name="locations">The geographic locations to subscribe to.</param>
        /// <param name="messageHandler">The delegate to handle received messages.</param>
        public RabbitMQService(
            string hostName,
            int port,
            string userName,
            string password,
            string exchangeName,
            string aggregatorId,
            List<string> sensorTypes,
            List<string> locations,
            MessageHandlerDelegate messageHandler)
        {
            _hostName = hostName;
            _port = port;
            _userName = userName;
            _password = password;
            _exchangeName = exchangeName;
            _aggregatorId = aggregatorId;
            _sensorTypes = sensorTypes;
            _locations = locations;
            _messageHandler = messageHandler;

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

                // Create a queue for each combination of sensor type and location
                foreach (var sensorType in _sensorTypes)
                {
                    foreach (var location in _locations)
                    {
                        // Create a queue name based on aggregator ID, sensor type, and location
                        string queueName = $"aggregator.{_aggregatorId}.{sensorType}.{location}";
                        
                        // Declare the queue
                        _channel.QueueDeclare(
                            queue: queueName,
                            durable: true,
                            exclusive: false,
                            autoDelete: false);

                        // Bind the queue to the exchange with routing keys for all message types
                        foreach (var messageType in Enum.GetNames(typeof(MessageType)))
                        {
                            string routingKey = $"{sensorType}.{location}.{messageType.ToLower()}";
                            _channel.QueueBind(
                                queue: queueName,
                                exchange: _exchangeName,
                                routingKey: routingKey);
                        }

                        _queueNames.Add(queueName);
                        Console.WriteLine($"[{_aggregatorId}] Subscribed to queue: {queueName}");
                    }
                }

                // Start consuming messages from all queues
                StartConsuming();

                Console.WriteLine($"[{_aggregatorId}] Connected to RabbitMQ at {_hostName}:{_port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_aggregatorId}] Failed to initialize RabbitMQ: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Starts consuming messages from all queues.
        /// </summary>
        private void StartConsuming()
        {
            foreach (var queueName in _queueNames)
            {
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        
                        Console.WriteLine($"[{_aggregatorId}] Received message from queue: {queueName}");
                        
                        // Parse the message
                        var wavyMessage = WavyMessage.FromJson(message);
                        
                        // Handle the message
                        await _messageHandler(wavyMessage, _channel, ea);
                        
                        // Acknowledge the message
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{_aggregatorId}] Error processing message: {ex.Message}");
                        // Negative acknowledge the message to requeue it
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                // Start consuming with manual acknowledgment
                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);
            }
        }

        /// <summary>
        /// Sends a response to a WAVY device.
        /// </summary>
        /// <param name="response">The response message.</param>
        /// <param name="properties">The properties of the original message.</param>
        public void SendResponse(string response, IBasicProperties properties)
        {
            if (string.IsNullOrEmpty(properties.ReplyTo))
            {
                Console.WriteLine($"[{_aggregatorId}] Cannot send response: ReplyTo is not set");
                return;
            }

            var responseProps = _channel.CreateBasicProperties();
            responseProps.CorrelationId = properties.CorrelationId;

            var responseBytes = Encoding.UTF8.GetBytes(response);
            _channel.BasicPublish(
                exchange: "",
                routingKey: properties.ReplyTo,
                basicProperties: responseProps,
                body: responseBytes);

            Console.WriteLine($"[{_aggregatorId}] Sent response to {properties.ReplyTo}");
        }

        /// <summary>
        /// Disposes the RabbitMQ connection and channel.
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _channel?.Close();
            _connection?.Close();
            _cancellationTokenSource.Dispose();
        }
    }
}
