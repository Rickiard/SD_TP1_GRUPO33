using System;
using System.ComponentModel.DataAnnotations;

namespace OceanDashboard.Models
{
    public class AnalysisRequest
    {
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public string Location { get; set; } = "all";

        // Filtros de ondas
        public double? WaveHeightMin { get; set; }
        public double? WaveHeightMax { get; set; }
        public double? WavePeriodMin { get; set; }
        public double? WavePeriodMax { get; set; }

        // Filtros de vento
        public double? WindSpeedMin { get; set; }
        public double? WindSpeedMax { get; set; }
        public double? WindDirectionMin { get; set; }
        public double? WindDirectionMax { get; set; }

        // Filtros de temperatura
        public double? SeaTemperatureMin { get; set; }
        public double? SeaTemperatureMax { get; set; }
        public double? AirTemperatureMin { get; set; }
        public double? AirTemperatureMax { get; set; }

        // Filtros de pressão
        public double? PressureMin { get; set; }
        public double? PressureMax { get; set; }

        // Filtros de qualidade
        public int? QcFlagMax { get; set; }

        // Filtros de estações específicas
        public string? StationIds { get; set; }

        // Tipo de análise
        public string AnalysisType { get; set; } = "comprehensive";

        // Configurações de agregação
        public string AggregationLevel { get; set; } = "hourly"; // hourly, daily, weekly
        public bool IncludeTrends { get; set; } = true;
        public bool IncludePatterns { get; set; } = true;
        public bool IncludeExtremeEvents { get; set; } = true;

        // Validação básica
        public bool IsValid()
        {
            return StartDate < EndDate && 
                   EndDate <= DateTime.Now && 
                   StartDate >= DateTime.Now.AddYears(-1);
        }
    }

    public class AnalysisPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public AnalysisRequest Filters { get; set; } = new AnalysisRequest();
        public string Icon { get; set; } = "bi-graph-up";
        public string Color { get; set; } = "primary";
    }
}
