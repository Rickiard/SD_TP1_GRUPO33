using System;
using System.Collections.Generic;

namespace OceanDashboard.Models
{    public class AnalyticsViewModel
    {
        // Filter parameters
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string StationId { get; set; } = "all";
        public string AnalysisType { get; set; } = "all";
        public string TimeRange { get; set; } = "24h";
        
        // Data
        public List<OceanData> OceanData { get; set; } = new List<OceanData>();
        public List<string> AvailableStations { get; set; } = new List<string>();
        
        // Analysis results
        public DataAnalysisResult? AnalysisResult { get; set; }
        public PatternAnalysisResult? PatternResult { get; set; }
        
        // Constructor to set default dates
        public AnalyticsViewModel()
        {
            EndDate = DateTime.Now;
            StartDate = EndDate.AddDays(-1);
        }
    }
}
