using System;
using System.Text.Json;

namespace WavyClient.Models
{
    /// <summary>
    /// Represents a message sent from a WAVY device to an Aggregator.
    /// </summary>
    public class WavyMessage
    {
        /// <summary>
        /// The unique identifier of the WAVY device.
        /// </summary>
        public string WavyId { get; set; }

        /// <summary>
        /// The type of message being sent.
        /// </summary>
        public MessageType Type { get; set; }

        /// <summary>
        /// The content of the message, which varies depending on the message type.
        /// </summary>
        public string Data { get; set; }

        /// <summary>
        /// The type of sensor associated with the message.
        /// </summary>
        public string SensorType { get; set; }

        /// <summary>
        /// The location associated with the message.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// The timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Creates a new instance of the WavyMessage class.
        /// </summary>
        /// <param name="wavyId">The unique identifier of the WAVY device.</param>
        /// <param name="type">The type of message being sent.</param>
        /// <param name="data">The content of the message.</param>
        /// <param name="sensorType">The type of sensor associated with the message.</param>
        /// <param name="location">The location associated with the message.</param>
        public WavyMessage(string wavyId, MessageType type, string data, string sensorType = "", string location = "")
        {
            WavyId = wavyId;
            Type = type;
            Data = data;
            SensorType = sensorType;
            Location = location;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Serializes the message to a JSON string.
        /// </summary>
        /// <returns>A JSON string representation of the message.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Deserializes a JSON string to a WavyMessage object.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A WavyMessage object.</returns>
        public static WavyMessage FromJson(string json)
        {
            return JsonSerializer.Deserialize<WavyMessage>(json);
        }
    }

    /// <summary>
    /// Represents the type of message being sent.
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// A hello message sent when a WAVY device connects to the system.
        /// </summary>
        Hello,

        /// <summary>
        /// A data message containing CSV data from the WAVY device.
        /// </summary>
        DataCsv,

        /// <summary>
        /// A quit message sent when a WAVY device disconnects from the system.
        /// </summary>
        Quit
    }
}
