using System;
using System.Text;
using System.Text.Json;

namespace AggregatorServer
{
    public class WavyMessage
    {
        public string WavyId { get; set; }
        public string MessageType { get; set; } // HELLO, DATA_CSV, QUIT, etc.
        public string Data { get; set; }
        public DateTime Timestamp { get; set; }

        public WavyMessage()
        {
            Timestamp = DateTime.UtcNow;
        }

        public WavyMessage(string wavyId, string messageType, string data)
        {
            WavyId = wavyId;
            MessageType = messageType;
            Data = data;
            Timestamp = DateTime.UtcNow;
        }

        public byte[] Serialize()
        {
            string jsonString = JsonSerializer.Serialize(this);
            return Encoding.UTF8.GetBytes(jsonString);
        }

        public static WavyMessage Deserialize(byte[] messageBytes)
        {
            string jsonString = Encoding.UTF8.GetString(messageBytes);
            return JsonSerializer.Deserialize<WavyMessage>(jsonString);
        }
    }
}