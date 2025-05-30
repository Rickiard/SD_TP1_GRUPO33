using System;
using System.Collections.Generic;
using System.Linq;

namespace OceanDashboard.Models
{
    public class DashboardViewModel
    {        // Filter parameters
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string StationId { get; set; } = "all";
        public string TimeRange { get; set; } = "24h";
        
        // Data
        public List<OceanData> OceanData { get; set; } = new List<OceanData>();
        public List<string> AvailableStations { get; set; } = new List<string>();
        
        // Analysis results
        public DataAnalysisResult? AnalysisResult { get; set; }
        public PatternAnalysisResult? PatternAnalysisResult { get; set; }
        
        // Statistics summary for cards
        public OceanDataSummary Summary { get; set; } = new OceanDataSummary();
        
        // Chart data
        public ChartData ChartData { get; set; } = new ChartData();
        
        // Constructor to set default dates
        public DashboardViewModel()
        {
            EndDate = DateTime.Now;
            StartDate = EndDate.AddDays(-1);
        }
        
        // Calculate summary statistics from ocean data
        public void CalculateSummary()
        {
            if (OceanData == null || !OceanData.Any())
            {
                Summary = new OceanDataSummary();
                return;
            }
            
            Summary = new OceanDataSummary
            {
                AverageWaveHeight = OceanData.Average(d => d.WaveHeight),
                MaxWaveHeight = OceanData.Max(d => d.WaveHeight),
                MedianWaveHeight = GetMedian(OceanData.Select(d => d.WaveHeight).ToList()),
                
                AverageSeaTemperature = OceanData.Average(d => d.SeaTemperature),
                MinSeaTemperature = OceanData.Min(d => d.SeaTemperature),
                MaxSeaTemperature = OceanData.Max(d => d.SeaTemperature),
                
                AverageWindSpeed = OceanData.Average(d => d.WindSpeed),
                MaxWindSpeed = OceanData.Max(d => d.WindSpeed),
                WindSpeedStdDev = CalculateStandardDeviation(OceanData.Select(d => d.WindSpeed).ToList()),
                
                MostCommonWindDirection = GetMostCommonDirection(OceanData.Select(d => d.WindDirection).ToList()),
                
                DataPointsCount = OceanData.Count,
                StationsCount = OceanData.Select(d => d.StationId).Distinct().Count()
            };
            
            // Prepare chart data
            PrepareChartData();
        }
        
        private void PrepareChartData()
        {
            if (OceanData == null || !OceanData.Any())
            {
                return;
            }
            
            // Prepare time labels (x-axis)
            ChartData.TimeLabels = OceanData.OrderBy(d => d.Timestamp)
                                          .Select(d => d.Timestamp.ToString("dd/MM HH:mm"))
                                          .ToList();
            
            // Prepare data series
            ChartData.WaveHeightData = OceanData.OrderBy(d => d.Timestamp)
                                             .Select(d => d.WaveHeight)
                                             .ToList();
                                             
            ChartData.SeaTemperatureData = OceanData.OrderBy(d => d.Timestamp)
                                               .Select(d => d.SeaTemperature)
                                               .ToList();
                                               
            ChartData.WindSpeedData = OceanData.OrderBy(d => d.Timestamp)
                                          .Select(d => d.WindSpeed)
                                          .ToList();
                                          
            ChartData.WindDirectionData = OceanData.OrderBy(d => d.Timestamp)
                                             .Select(d => (int)d.WindDirection)
                                             .ToList();
                                             
            ChartData.WavePeriodData = OceanData.OrderBy(d => d.Timestamp)
                                           .Select(d => d.WavePeriod)
                                           .ToList();
        }
        
        private double GetMedian(List<double> values)
        {
            if (values == null || values.Count == 0)
                return 0;
                
            var sortedValues = values.OrderBy(v => v).ToList();
            int count = sortedValues.Count;
            
            if (count % 2 == 0)
            {
                return (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2;
            }
            else
            {
                return sortedValues[count / 2];
            }
        }
        
        private double CalculateStandardDeviation(List<double> values)
        {
            if (values == null || values.Count <= 1)
                return 0;
                
            double avg = values.Average();
            double sum = values.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sum / (values.Count - 1));
        }
        
        private string GetMostCommonDirection(List<double> directions)
        {
            if (directions == null || directions.Count == 0)
                return "N/A";
                
            // Convert degrees to cardinal directions
            var cardinalDirections = directions.Select(d => DegreesToCardinal(d)).ToList();
            
            // Find most common
            return cardinalDirections
                .GroupBy(d => d)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
        }
        
        private string DegreesToCardinal(double degrees)
        {
            // Normalize degrees to 0-360 range
            degrees = ((degrees % 360) + 360) % 360;
            
            string[] cardinals = { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
            return cardinals[(int)Math.Round(degrees / 45)];
        }
    }
    
    public class OceanDataSummary
    {
        public double AverageWaveHeight { get; set; }
        public double MaxWaveHeight { get; set; }
        public double MedianWaveHeight { get; set; }
        
        public double AverageSeaTemperature { get; set; }
        public double MinSeaTemperature { get; set; }
        public double MaxSeaTemperature { get; set; }
        
        public double AverageWindSpeed { get; set; }
        public double MaxWindSpeed { get; set; }
        public double WindSpeedStdDev { get; set; }
        
        public string MostCommonWindDirection { get; set; } = "N/A";
        
        public int DataPointsCount { get; set; }
        public int StationsCount { get; set; }
    }
    
    public class ChartData
    {
        public List<string> TimeLabels { get; set; } = new List<string>();
        public List<double> WaveHeightData { get; set; } = new List<double>();
        public List<double> SeaTemperatureData { get; set; } = new List<double>();
        public List<double> WindSpeedData { get; set; } = new List<double>();
        public List<int> WindDirectionData { get; set; } = new List<int>();
        public List<double> WavePeriodData { get; set; } = new List<double>();
    }
}
