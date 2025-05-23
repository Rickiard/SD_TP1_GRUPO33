using System;

namespace OceanDashboard.Models
{
    /// <summary>
    /// OceanData model class aligned with the SensorData message from the analysis.proto file.
    /// Used to store and represent sensor data from ocean buoys.
    /// </summary>
    public class OceanData
    {
        // Database ID
        public int Id { get; set; }
        
        // Original fields from SensorData message
        public string SensorId { get; set; } = string.Empty;
        public double Longitude { get; set; }
        public double Latitude { get; set; }
        public DateTime Timestamp { get; set; }
        
        // Atmospheric and wind data
        public double AtmospherePressure { get; set; }  // atmosphere_mb in proto
        public double WindDirection { get; set; }       // wind_direction_degrees in proto
        public double WindSpeed { get; set; }           // wind_speed_kn in proto
        public double Gust { get; set; }                // gust_kn in proto
        
        // Wave data
        public double WaveHeight { get; set; }         // wave_height_m in proto
        public double WavePeriod { get; set; }         // wave_period_s in proto
        public double WaveDirection { get; set; }      // mean_wave_direction_degrees in proto
        public double MaxWaveHeight { get; set; }      // hmax_m in proto
        
        // Temperature and humidity data
        public double AirTemperature { get; set; }     // air_temperature_c in proto
        public double DewPoint { get; set; }           // dew_point_c in proto
        public double SeaTemperature { get; set; }     // sea_temperature_c in proto
        public double RelativeHumidity { get; set; }   // relative_humidity_percent in proto
        
        // Quality control and station information
        public int QcFlag { get; set; }                // qc_flag in proto
        public string StationId { get; set; } = string.Empty;
        public string Location { get; set; } = "Oceano Atlântico";
        
        // Additional fields (optional in proto)
        public string Value { get; set; } = string.Empty;  // For any other data values
        public string Unit { get; set; } = string.Empty;   // Unit of measurement
        
        /// <summary>
        /// Constructor that sets default values to 0
        /// </summary>
        public OceanData()
        {
            // Initialize numeric values to 0
            Longitude = 0;
            Latitude = 0;
            AtmospherePressure = 0;
            WindDirection = 0;
            WindSpeed = 0;
            Gust = 0;
            WaveHeight = 0;
            WavePeriod = 0;
            WaveDirection = 0;
            MaxWaveHeight = 0;
            AirTemperature = 0;
            DewPoint = 0;
            SeaTemperature = 0;
            RelativeHumidity = 0;
            QcFlag = 0;
        }
    }
}
