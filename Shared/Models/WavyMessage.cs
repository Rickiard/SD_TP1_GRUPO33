using System.Text.Json;

namespace Shared.Models
{
    public class WavyMessage
    {
        public string WavyId { get; set; }
        public MessageType Type { get; set; }
        public string Data { get; set; }
        public string SensorType { get; set; }
        public string Location { get; set; }
        public DateTime Timestamp { get; set; }

        public WavyMessage(string wavyId, MessageType type, string data, string sensorType = "", string location = "")
        {
            WavyId = wavyId;
            Type = type;
            Data = data;
            SensorType = sensorType;
            Location = location;
            Timestamp = DateTime.UtcNow;
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        public static WavyMessage FromJson(string json)
        {
            return JsonSerializer.Deserialize<WavyMessage>(json);
        }
    }

    public enum MessageType
    {
        Hello,
        DataCsv,
        Quit
    }
} 