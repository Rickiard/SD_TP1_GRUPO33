using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace AggregatorServer
{
    // Simplified implementation without RabbitMQ for now
    public class RabbitMQService : IDisposable
    {
        private readonly string _hostName;
        private readonly string _exchangeName;
        private readonly string _aggregatorId;
        private readonly int _port;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentDictionary<string, string> _activeWavys = new ConcurrentDictionary<string, string>();
        private TcpListener? _listener;
        private Thread? _acceptThread;
        private readonly List<ClientHandler> _clients = new List<ClientHandler>();
        private readonly object _clientsLock = new object();

        public delegate void MessageReceivedHandler(WavyMessage message);
        public event MessageReceivedHandler? OnMessageReceived;

        public RabbitMQService(string hostName, string exchangeName, string aggregatorId, int port)
        {
            _hostName = hostName;
            _exchangeName = exchangeName;
            _aggregatorId = aggregatorId;
            _port = port;
            _cancellationTokenSource = new CancellationTokenSource();
            Initialize();
        }

        private void Initialize()
        {
            try
            {                // For now, we'll use a direct TCP server instead of RabbitMQ
                // In a real implementation, this would use RabbitMQ client
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();

                // Start a thread to accept client connections
                _acceptThread = new Thread(AcceptClients);
                _acceptThread.IsBackground = true;
                _acceptThread.Start();

                Console.WriteLine($"Server initialized for Aggregator {_aggregatorId} on port {_port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing server: {ex.Message}");
                throw;
            }
        }

        private void AcceptClients()
        {
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    var handler = new ClientHandler(client, this);
                    
                    lock (_clientsLock)
                    {
                        _clients.Add(handler);
                    }
                    
                    handler.Start();
                }
            }
            catch (Exception ex)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Console.WriteLine($"Error accepting clients: {ex.Message}");
                }
            }
        }

        public void HandleMessage(WavyMessage message, ClientHandler handler)
        {
            // Track active WAVYs
            if (message.MessageType == "HELLO")
            {
                _activeWavys.TryAdd(message.WavyId, DateTime.UtcNow.ToString());
                handler.WavyId = message.WavyId;
            }
            else if (message.MessageType == "QUIT")
            {
                _activeWavys.TryRemove(message.WavyId, out _);
            }
            
            OnMessageReceived?.Invoke(message);
        }

        public void PublishMessage(WavyMessage message)
        {
            try
            {
                var body = message.Serialize();
                
                // Find the client handler for this WAVY
                ClientHandler targetHandler = null;
                
                lock (_clientsLock)
                {
                    foreach (var handler in _clients)
                    {
                        if (handler.WavyId == message.WavyId)
                        {
                            targetHandler = handler;
                            break;
                        }
                    }
                }
                
                if (targetHandler != null)
                {
                    targetHandler.SendMessage(body);
                    Console.WriteLine($"Message sent to WAVY {message.WavyId}: {message.MessageType}");
                }
                else
                {
                    Console.WriteLine($"No active connection for WAVY {message.WavyId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing message: {ex.Message}");
            }
        }

        public bool IsWavyActive(string wavyId)
        {
            return _activeWavys.ContainsKey(wavyId);
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            
            lock (_clientsLock)
            {
                foreach (var client in _clients)
                {
                    client.Stop();
                }
                _clients.Clear();
            }
            
            _listener?.Stop();
            _acceptThread?.Join(1000);
        }
        
        // Inner class to handle client connections
        public class ClientHandler
        {
            private readonly TcpClient _client;
            private readonly NetworkStream _stream;
            private readonly RabbitMQService _service;
            private readonly Thread _receiveThread;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            
            public string? WavyId { get; set; }
            
            public ClientHandler(TcpClient client, RabbitMQService service)
            {
                _client = client;
                _stream = client.GetStream();
                _service = service;
                _receiveThread = new Thread(ReceiveMessages);
                _receiveThread.IsBackground = true;
            }
            
            public void Start()
            {
                _receiveThread.Start();
            }
            
            public void Stop()
            {
                _cts.Cancel();
                _receiveThread.Join(1000);
                _stream.Dispose();
                _client.Dispose();
            }
            
            private void ReceiveMessages()
            {
                try
                {
                    byte[] buffer = new byte[4096];
                    while (!_cts.IsCancellationRequested)
                    {
                        if (_stream.DataAvailable)
                        {
                            int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                var message = WavyMessage.Deserialize(buffer.AsSpan(0, bytesRead).ToArray());
                                _service.HandleMessage(message, this);
                            }
                        }
                        Thread.Sleep(100); // Small delay to prevent CPU spinning
                    }
                }
                catch (Exception ex)
                {
                    if (!_cts.IsCancellationRequested)
                    {
                        Console.WriteLine($"Error in client handler: {ex.Message}");
                    }
                }
            }
            
            public void SendMessage(byte[] data)
            {
                try
                {
                    _stream.Write(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message to client: {ex.Message}");
                }
            }
        }
    }
}