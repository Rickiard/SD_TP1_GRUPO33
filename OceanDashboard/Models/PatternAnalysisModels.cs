using System;
using System.Collections.Generic;

namespace OceanDashboard.Models
{
    public class PatternAnalysisResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<PatternInfo> Patterns { get; set; } = new List<PatternInfo>();
        public List<StormEventInfo> StormEvents { get; set; } = new List<StormEventInfo>();
        public List<AnomalyInfo> Anomalies { get; set; } = new List<AnomalyInfo>();
    }

    public class PatternInfo
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public double Confidence { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Intensity { get; set; }
        public string Location { get; set; }

        // Helper properties for display
        public string ConfidenceDisplay => $"{Confidence:P0}";
        public string DurationDisplay => (EndTime - StartTime).ToString(@"d\d\ h\h\ m\m");
        public string IntensityBar => GetIntensityBarHtml();

        private string GetIntensityBarHtml()
        {
            // Normaliza a intensidade para uma escala de 1-5 
            int barCount = Math.Max(1, Math.Min(5, (int)Math.Ceiling(Intensity * 5)));
            return string.Join("", Enumerable.Repeat("<span class=\"intensity-bar\"></span>", barCount));
        }
    }

    public class StormEventInfo
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double PeakWaveHeight { get; set; }
        public double PeakWindSpeed { get; set; }
        public double PeakGust { get; set; }
        public string Location { get; set; }
        public int Severity { get; set; }
        public string Description { get; set; }

        // Helper properties for display
        public string DurationDisplay => (EndTime - StartTime).ToString(@"d\d\ h\h\ m\m");
        public string SeverityDisplay => GetSeverityDisplay();

        private string GetSeverityDisplay()
        {
            string severityClass = Severity switch
            {
                1 => "low",
                2 => "moderate-low",
                3 => "moderate",
                4 => "moderate-high",
                5 => "high",
                _ => "unknown"
            };

            return $"<span class=\"severity-indicator {severityClass}\">{Severity}/5</span>";
        }
    }

    public class AnomalyInfo
    {
        public DateTime Timestamp { get; set; }
        public string Parameter { get; set; }
        public double ExpectedValue { get; set; }
        public double ActualValue { get; set; }
        public double DeviationPercent { get; set; }
        public string Location { get; set; }
        public int Confidence { get; set; }

        // Helper properties for display
        public string DeviationDisplay => $"{DeviationPercent:F1}%";
        public string ConfidenceDisplay => $"{Confidence}%";
        public bool IsSignificantAnomaly => DeviationPercent > 30 && Confidence > 70;
    }

    public class DataAnalysisResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, double> Statistics { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, WaveStatisticsInfo> WaveStatistics { get; set; } = new Dictionary<string, WaveStatisticsInfo>();
        public Dictionary<string, WindStatisticsInfo> WindStatistics { get; set; } = new Dictionary<string, WindStatisticsInfo>();
        public Dictionary<string, TemperatureStatisticsInfo> TemperatureStatistics { get; set; } = new Dictionary<string, TemperatureStatisticsInfo>();
    }    public class WaveStatisticsInfo
    {
        public double AvgHeight { get; set; }
        public double MaxHeight { get; set; }
        public double MinHeight { get; set; }
        public double AvgPeriod { get; set; }
        public double MaxPeriod { get; set; }
        public double MinPeriod { get; set; }
        public double AvgDirection { get; set; }
        public string PredominantDirection { get; set; }
        // Novos campos
        public double MedianHeight { get; set; }
        public double MedianPeriod { get; set; }
        public double SignificantWaveHeight { get; set; }  // H1/3 - média do terço superior das alturas
    }

    public class WindStatisticsInfo
    {
        public double AvgSpeed { get; set; }
        public double MaxSpeed { get; set; }
        public double MinSpeed { get; set; }
        public double AvgGust { get; set; }
        public double MaxGust { get; set; }
        public double AvgDirection { get; set; }
        public string PredominantDirection { get; set; }
        // Novos campos
        public double MedianSpeed { get; set; }
        public double SpeedStdDev { get; set; }
        public double MedianGust { get; set; }
        public double GustFactor { get; set; }  // Razão entre rajada máxima e velocidade média
        public Dictionary<string, double> BeaufortDistribution { get; set; } = new Dictionary<string, double>();
    }

    public class TemperatureStatisticsInfo
    {
        public double AvgAirTemp { get; set; }
        public double MaxAirTemp { get; set; }
        public double MinAirTemp { get; set; }
        public double AvgSeaTemp { get; set; }
        public double MaxSeaTemp { get; set; }
        public double MinSeaTemp { get; set; }
        public double AvgHumidity { get; set; }
        // Novos campos
        public double MedianAirTemp { get; set; }
        public double AirTempStdDev { get; set; }
        public double MedianSeaTemp { get; set; }
        public double SeaTempStdDev { get; set; }
        public double MaxHumidity { get; set; }
        public double MinHumidity { get; set; }
        public double AvgTempDifference { get; set; }  // Diferença média entre temperatura do ar e do mar
        public double MaxTempDifference { get; set; }  // Maior diferença de temperatura ar-mar
        public double MinTempDifference { get; set; }  // Menor diferença de temperatura ar-mar
    }
}
