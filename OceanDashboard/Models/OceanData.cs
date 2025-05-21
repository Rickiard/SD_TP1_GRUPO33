using System;

namespace OceanDashboard.Models
{
    public class OceanData
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public double WaveHeight { get; set; }
        public double WavePeriod { get; set; }
        public double WaveDirection { get; set; }
        public double Temperature { get; set; }
        public string Location { get; set; }
    }
}
