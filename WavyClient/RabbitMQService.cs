using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace WavyClient
{
    // Simplified implementation without RabbitMQ for now
    public class RabbitMQService : IDisposable
    {
        private readonly string _hostName;
        private readonly string _exchangeName;
        private readonly string _wavyId;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private TcpClient _client;
        private NetworkStream _stream;
        private Thread _receiveThread;
        private bool _isConnected;

        public delegate void MessageReceivedHandler(WavyMessage message);
        public event MessageReceivedHandler OnMessageReceived;

        public RabbitMQService(string hostName, string exchangeName, string wavyId)
        {
            _hostName = hostName;
            _exchangeName = exchangeName;
            _wavyId = wavyId;
            _cancellationTokenSource = new CancellationTokenSource();
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                // For now, we'll use a direct TCP connection instead of RabbitMQ
                // In a real implementation, this would use RabbitMQ client
                _client = new TcpClient(_hostName, 5672); // Default RabbitMQ port
                _stream = _client.GetStream();
                _isConnected = true;

                // Start a thread to receive messages
                _receiveThread = new Thread(ReceiveMessages);
                _receiveThread.IsBackground = true;
                _receiveThread.Start();

                Console.WriteLine($"Connection initialized for WAVY {_wavyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing connection: {ex.Message}");
                _isConnected = false;
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                byte[] buffer = new byte[4096];
                while (!_cancellationTokenSource.IsCancellationRequested && _isConnected)
                {
                    if (_stream.DataAvailable)
                    {
                        int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            var message = WavyMessage.Deserialize(buffer.AsSpan(0, bytesRead).ToArray());
                            OnMessageReceived?.Invoke(message);
                        }
                    }
                    Thread.Sleep(100); // Small delay to prevent CPU spinning
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in receive thread: {ex.Message}");
                _isConnected = false;
            }
        }

        public void PublishMessage(WavyMessage message)
        {
            try
            {
                if (!_isConnected)
                {
                    Console.WriteLine("Cannot send message: not connected");
                    return;
                }

                var body = message.Serialize();
                _stream.Write(body, 0, body.Length);
                
                Console.WriteLine($"Message sent: {message.MessageType} from WAVY {message.WavyId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error publishing message: {ex.Message}");
                _isConnected = false;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _receiveThread?.Join(1000);
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}