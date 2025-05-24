using Grpc.Core;
using RPC_DataAnalyserService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.Collections;

namespace RPC_DataAnalyserService.Services
{
    public class DataAnalysisServiceImpl : DataAnalysisService.DataAnalysisServiceBase
    {
        private readonly ILogger<DataAnalysisServiceImpl> _logger;

        public DataAnalysisServiceImpl(ILogger<DataAnalysisServiceImpl> logger)
        {
            _logger = logger;
        }
          public override async Task<AnalysisResponse> AnalyzeData(AnalysisRequest request, ServerCallContext context)
        {
            try
            {
                var response = new AnalysisResponse { Success = true };
                
                _logger.LogInformation("Iniciando análise de dados oceânicos...");
                _logger.LogInformation($"Tipo de análise: {request.AnalysisType}, Intervalo: {request.TimeRange}");
                
                if (request.DataPoints.Count == 0)
                {
                    _logger.LogWarning("Nenhum dado fornecido para análise");
                    return new AnalysisResponse { Success = false, ErrorMessage = "Nenhum dado fornecido para análise" };
                }

                // Estatísticas gerais
                response.Statistics.Add("total_records", request.DataPoints.Count);
                  // Filtrar por ID da estação se especificada
                var dataPoints = request.DataPoints.ToList();
                if (!string.IsNullOrEmpty(request.Location) && request.Location != "all")
                {
                    string stationId = request.Location;
                    dataPoints = dataPoints.Where(d => d.StationId == stationId || d.SensorId == stationId).ToList();
                    
                    if (dataPoints.Count == 0)
                    {
                        _logger.LogWarning($"Nenhum dado encontrado para a estação: {stationId}");
                        return new AnalysisResponse 
                        { 
                            Success = false, 
                            ErrorMessage = $"Nenhum dado encontrado para a estação: {stationId}" 
                        };
                    }
                    
                    response.Statistics.Add("filtered_records", dataPoints.Count);
                }
                
                // Calcular estatísticas básicas
                await CalculateBasicStatistics(dataPoints, response);
                
                // Calcular estatísticas específicas com base no tipo de análise
                switch (request.AnalysisType.ToLower())
                {
                    case "all":
                        await CalculateWaveStatistics(dataPoints, response);
                        await CalculateWindStatistics(dataPoints, response);
                        await CalculateTemperatureStatistics(dataPoints, response);
                        break;
                        
                    case "wave":
                        await CalculateWaveStatistics(dataPoints, response);
                        break;
                        
                    case "wind":
                        await CalculateWindStatistics(dataPoints, response);
                        break;
                        
                    case "temperature":
                        await CalculateTemperatureStatistics(dataPoints, response);
                        break;
                }
                
                _logger.LogInformation("Análise de dados oceânicos concluída. Retornando {0} estatísticas, {1} estatísticas de onda, {2} de vento, {3} de temperatura.",
                    response.Statistics.Count, response.WaveStats.Count, response.WindStats.Count, response.TempStats.Count);
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar dados oceânicos");
                return new AnalysisResponse
                {
                    Success = false,
                    ErrorMessage = $"Erro ao processar dados: {ex.Message}"
                };
            }        }

        private Task CalculateBasicStatistics(List<SensorData> dataPoints, AnalysisResponse response)
        {
            // Log estatísticas básicas
            _logger.LogInformation("Calculando estatísticas básicas para {Count} pontos de dados", dataPoints.Count);
            
            // Contagem por estação
            var stationsCount = dataPoints
                .GroupBy(d => d.StationId)
                .Select(g => new { StationId = g.Key, Count = g.Count() })
                .ToDictionary(x => x.StationId, x => x.Count);
                
            foreach (var station in stationsCount)
            {
                response.Statistics.Add($"station_{station.Key}_count", station.Value);
            }
            
            // Altura das ondas
            if (dataPoints.Any(d => !double.IsNaN(d.WaveHeightM) && d.WaveHeightM > 0))
            {
                var validWaveData = dataPoints.Where(d => !double.IsNaN(d.WaveHeightM) && d.WaveHeightM > 0).ToList();
                response.Statistics.Add("avg_wave_height", validWaveData.Average(d => d.WaveHeightM));
                response.Statistics.Add("max_wave_height", validWaveData.Max(d => d.WaveHeightM));
                response.Statistics.Add("min_wave_height", validWaveData.Min(d => d.WaveHeightM));
                response.Statistics.Add("median_wave_height", CalculateMedian(validWaveData.Select(d => d.WaveHeightM)));
                response.Statistics.Add("wave_height_std_dev", CalculateStandardDeviation(validWaveData.Select(d => d.WaveHeightM)));
                response.Statistics.Add("wave_data_count", validWaveData.Count);
            }
            
            // Velocidade do vento
            if (dataPoints.Any(d => !double.IsNaN(d.WindSpeedKn) && d.WindSpeedKn > 0))
            {
                var validWindData = dataPoints.Where(d => !double.IsNaN(d.WindSpeedKn) && d.WindSpeedKn > 0).ToList();
                response.Statistics.Add("avg_wind_speed", validWindData.Average(d => d.WindSpeedKn));
                response.Statistics.Add("max_wind_speed", validWindData.Max(d => d.WindSpeedKn));
                response.Statistics.Add("min_wind_speed", validWindData.Min(d => d.WindSpeedKn));
                response.Statistics.Add("median_wind_speed", CalculateMedian(validWindData.Select(d => d.WindSpeedKn)));
                response.Statistics.Add("wind_speed_std_dev", CalculateStandardDeviation(validWindData.Select(d => d.WindSpeedKn)));
                response.Statistics.Add("wind_data_count", validWindData.Count);
                
                // Calcular distribuição de velocidades de vento em faixas de 5 nós
                var windSpeedDistribution = CalculateDistribution(validWindData.Select(d => d.WindSpeedKn), 0, 50, 5);
                foreach (var kvp in windSpeedDistribution)
                {
                    response.Statistics.Add($"wind_speed_{kvp.Key.Item1}_{kvp.Key.Item2}", kvp.Value);
                }
            }
            
            // Temperatura do mar
            if (dataPoints.Any(d => !double.IsNaN(d.SeaTemperatureC)))
            {
                var validTempData = dataPoints.Where(d => !double.IsNaN(d.SeaTemperatureC)).ToList();
                response.Statistics.Add("avg_sea_temp", validTempData.Average(d => d.SeaTemperatureC));
                response.Statistics.Add("max_sea_temp", validTempData.Max(d => d.SeaTemperatureC));
                response.Statistics.Add("min_sea_temp", validTempData.Min(d => d.SeaTemperatureC));
                response.Statistics.Add("median_sea_temp", CalculateMedian(validTempData.Select(d => d.SeaTemperatureC)));
                response.Statistics.Add("sea_temp_std_dev", CalculateStandardDeviation(validTempData.Select(d => d.SeaTemperatureC)));
                response.Statistics.Add("sea_temp_data_count", validTempData.Count);
            }
            
            // Temperatura do ar
            if (dataPoints.Any(d => !double.IsNaN(d.AirTemperatureC)))
            {
                var validAirTempData = dataPoints.Where(d => !double.IsNaN(d.AirTemperatureC)).ToList();
                response.Statistics.Add("avg_air_temp", validAirTempData.Average(d => d.AirTemperatureC));
                response.Statistics.Add("max_air_temp", validAirTempData.Max(d => d.AirTemperatureC));
                response.Statistics.Add("min_air_temp", validAirTempData.Min(d => d.AirTemperatureC));
                response.Statistics.Add("median_air_temp", CalculateMedian(validAirTempData.Select(d => d.AirTemperatureC)));
                response.Statistics.Add("air_temp_std_dev", CalculateStandardDeviation(validAirTempData.Select(d => d.AirTemperatureC)));
                response.Statistics.Add("air_temp_data_count", validAirTempData.Count);
            }
            
            // Adicionar dados de qualidade dos dados
            var qcFlags = dataPoints
                .Where(d => d.QcFlag > 0)
                .GroupBy(d => d.QcFlag)
                .Select(g => new { QcFlag = g.Key, Count = g.Count() })
                .ToDictionary(x => x.QcFlag, x => x.Count);
                
            foreach (var qc in qcFlags)
            {
                response.Statistics.Add($"qc_flag_{qc.Key}", qc.Value);
            }
            
            _logger.LogInformation("Estatísticas básicas calculadas: {StatsCount} estatísticas geradas", response.Statistics.Count);
            return Task.CompletedTask;
        }
        
        private double CalculateMedian(IEnumerable<double> values)
        {
            var sortedValues = values.OrderBy(n => n).ToList();
            int count = sortedValues.Count;
            
            if (count == 0)
                return 0;
                
            if (count % 2 == 0)
                return (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2;
            
            return sortedValues[count / 2];        }

        private async Task CalculateWaveStatistics(List<SensorData> data, AnalysisResponse response)
        {
            try
            {
                _logger.LogInformation("Iniciando cálculo de estatísticas de ondas para {Count} registros", data.Count);
                
                // Ignorar registros com valores NaN
                var validWaveHeightData = data.Where(d => !double.IsNaN(d.WaveHeightM) && d.WaveHeightM > 0).ToList();
                var validWavePeriodData = data.Where(d => !double.IsNaN(d.WavePeriodS) && d.WavePeriodS > 0).ToList();
                
                if (!validWaveHeightData.Any())
                {
                    _logger.LogWarning("Nenhum dado válido de altura de ondas encontrado");
                    return;
                }
                
                // Inicializar dicionário de estatísticas de ondas
                response.WaveStats["overall"] = new WaveStatistics();
                
                // Processar estatísticas gerais em paralelo para melhor performance
                await Task.Run(() => {
                    var overallStats = new WaveStatistics
                    {
                        AvgHeight = validWaveHeightData.Average(d => d.WaveHeightM),
                        MaxHeight = validWaveHeightData.Max(d => d.WaveHeightM),
                        MinHeight = validWaveHeightData.Min(d => d.WaveHeightM),
                        MedianHeight = CalculateMedian(validWaveHeightData.Select(d => d.WaveHeightM))
                    };
                    
                    // Adicionar estatísticas de período se disponíveis
                    if (validWavePeriodData.Any())
                    {
                        overallStats.AvgPeriod = validWavePeriodData.Average(d => d.WavePeriodS);
                        overallStats.MaxPeriod = validWavePeriodData.Max(d => d.WavePeriodS);
                        overallStats.MinPeriod = validWavePeriodData.Min(d => d.WavePeriodS);
                        overallStats.MedianPeriod = CalculateMedian(validWavePeriodData.Select(d => d.WavePeriodS));
                    }
                    
                    // Calcular direção média das ondas usando método circular
                    var validDirectionData = data.Where(d => !double.IsNaN(d.MeanWaveDirectionDegrees)).ToList();
                    if (validDirectionData.Any())
                    {
                        overallStats.AvgDirection = CalculateCircularMean(validDirectionData.Select(d => d.MeanWaveDirectionDegrees));
                        overallStats.PredominantDirection = GetPredominantDirection(validDirectionData.Select(d => d.MeanWaveDirectionDegrees).ToList());
                    }
                    
                    // Calcular altura significativa das ondas (média do terço superior das alturas)
                    var sortedHeights = validWaveHeightData.Select(d => d.WaveHeightM).OrderByDescending(h => h).ToList();
                    int thirdCount = Math.Max(1, sortedHeights.Count / 3);
                    overallStats.SignificantWaveHeight = sortedHeights.Take(thirdCount).Average();
                    
                    // Atribuir estatísticas gerais
                    response.WaveStats["overall"] = overallStats;
                    
                    // Adicionar algumas estatísticas no dicionário geral também
                    response.Statistics.Add("significant_wave_height", overallStats.SignificantWaveHeight);
                });
                
                // Calcular estatísticas por estação
                var stationIds = data
                    .Select(d => d.StationId)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();
                
                _logger.LogInformation("Calculando estatísticas de ondas para {Count} estações: {Stations}", 
                    stationIds.Count, string.Join(", ", stationIds));
                
                foreach (var stationId in stationIds)
                {
                    var stationData = data.Where(d => d.StationId == stationId).ToList();
                    var validStationWaveHeightData = stationData.Where(d => !double.IsNaN(d.WaveHeightM) && d.WaveHeightM > 0).ToList();
                    var validStationWavePeriodData = stationData.Where(d => !double.IsNaN(d.WavePeriodS) && d.WavePeriodS > 0).ToList();
                    
                    if (validStationWaveHeightData.Any())
                    {
                        var stationWaveStats = new WaveStatistics
                        {
                            AvgHeight = validStationWaveHeightData.Average(d => d.WaveHeightM),
                            MaxHeight = validStationWaveHeightData.Max(d => d.WaveHeightM),
                            MinHeight = validStationWaveHeightData.Min(d => d.WaveHeightM),
                            MedianHeight = CalculateMedian(validStationWaveHeightData.Select(d => d.WaveHeightM))
                        };
                        
                        // Adicionar estatísticas de período se disponíveis
                        if (validStationWavePeriodData.Any())
                        {
                            stationWaveStats.AvgPeriod = validStationWavePeriodData.Average(d => d.WavePeriodS);
                            stationWaveStats.MaxPeriod = validStationWavePeriodData.Max(d => d.WavePeriodS);
                            stationWaveStats.MinPeriod = validStationWavePeriodData.Min(d => d.WavePeriodS);
                            stationWaveStats.MedianPeriod = CalculateMedian(validStationWavePeriodData.Select(d => d.WavePeriodS));
                        }                          // Calcular direção média
                        var validStationDirectionData = stationData.Where(d => !double.IsNaN(d.MeanWaveDirectionDegrees)).ToList();
                        if (validStationDirectionData.Any())
                        {
                            stationWaveStats.AvgDirection = CalculateCircularMean(validStationDirectionData.Select(d => d.MeanWaveDirectionDegrees));
                            stationWaveStats.PredominantDirection = GetPredominantDirection(validStationDirectionData.Select(d => d.MeanWaveDirectionDegrees).ToList());
                        }
                        
                        // Calcular altura significativa das ondas (média do terço superior das alturas)
                        var sortedHeights = validStationWaveHeightData.Select(d => d.WaveHeightM).OrderByDescending(h => h).ToList();
                        int thirdCount = Math.Max(1, sortedHeights.Count / 3);
                        stationWaveStats.SignificantWaveHeight = sortedHeights.Take(thirdCount).Average();
                        
                        // Adicionar ao dicionário de estatísticas
                        response.WaveStats[stationId] = stationWaveStats;
                        
                        // Adicionar estatísticas importantes dessa estação ao dicionário geral também
                        response.Statistics.Add($"significant_wave_height_{stationId}", stationWaveStats.SignificantWaveHeight);
                        response.Statistics.Add($"max_wave_height_{stationId}", stationWaveStats.MaxHeight);
                    }
                }
                  _logger.LogInformation("Estatísticas de ondas calculadas para {Count} estações", response.WaveStats.Count - 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular estatísticas de ondas");
            }
        }

        private async Task CalculateWindStatistics(List<SensorData> data, AnalysisResponse response)
        {
            try
            {
                _logger.LogInformation("Iniciando cálculo de estatísticas de vento para {Count} registros", data.Count);
                
                // Ignorar registros com valores NaN
                var validWindSpeedData = data.Where(d => !double.IsNaN(d.WindSpeedKn) && d.WindSpeedKn >= 0).ToList();
                var validWindDirData = data.Where(d => !double.IsNaN(d.WindDirectionDegrees)).ToList();
                
                if (!validWindSpeedData.Any())
                {
                    _logger.LogWarning("Nenhum dado válido de velocidade do vento encontrado");
                    return;
                }
                
                // Inicializar dicionário de estatísticas de vento
                response.WindStats["overall"] = new WindStatistics();
                
                // Processar estatísticas gerais em paralelo para melhor performance
                await Task.Run(() => {
                    var overallStats = new WindStatistics
                    {
                        AvgSpeed = validWindSpeedData.Average(d => d.WindSpeedKn),
                        MaxSpeed = validWindSpeedData.Max(d => d.WindSpeedKn),
                        MinSpeed = validWindSpeedData.Min(d => d.WindSpeedKn),
                        MedianSpeed = CalculateMedian(validWindSpeedData.Select(d => d.WindSpeedKn)),
                        SpeedStdDev = CalculateStandardDeviation(validWindSpeedData.Select(d => d.WindSpeedKn))
                    };
                    
                    // Calcular direção média e predominante
                    if (validWindDirData.Any())
                    {
                        overallStats.AvgDirection = CalculateCircularMean(validWindDirData.Select(d => d.WindDirectionDegrees));
                        overallStats.PredominantDirection = GetPredominantDirection(validWindDirData.Select(d => d.WindDirectionDegrees).ToList());
                    }
                    
                    // Calcular estatísticas de rajadas
                    var validGustData = data.Where(d => !double.IsNaN(d.GustKn) && d.GustKn > 0).ToList();
                    if (validGustData.Any())
                    {
                        overallStats.AvgGust = validGustData.Average(d => d.GustKn);
                        overallStats.MaxGust = validGustData.Max(d => d.GustKn);
                        overallStats.MedianGust = CalculateMedian(validGustData.Select(d => d.GustKn));
                        
                        // Calcular razão rajada/velocidade média (Gust factor)
                        if (overallStats.AvgSpeed > 0)
                        {
                            overallStats.GustFactor = overallStats.MaxGust / overallStats.AvgSpeed;
                        }
                    }
                      // Calcular distribuição de Beaufort
                    var beaufortDistribution = CalculateBeaufortDistribution(validWindSpeedData.Select(d => d.WindSpeedKn));
                    foreach (var kv in beaufortDistribution)
                    {
                        overallStats.BeaufortDistribution.Add(kv.Key.ToString(), kv.Value);
                    }
                    
                    // Atribuir estatísticas gerais
                    response.WindStats["overall"] = overallStats;
                    
                    // Adicionar algumas estatísticas no dicionário geral também
                    response.Statistics.Add("max_gust", overallStats.MaxGust);
                    if (overallStats.GustFactor > 0)
                    {
                        response.Statistics.Add("gust_factor", overallStats.GustFactor);
                    }
                });
                
                // Calcular estatísticas por estação
                var stationIds = data
                    .Select(d => d.StationId)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();
                
                _logger.LogInformation("Calculando estatísticas de vento para {Count} estações: {Stations}", 
                    stationIds.Count, string.Join(", ", stationIds));
                
                foreach (var stationId in stationIds)
                {
                    var stationData = data.Where(d => d.StationId == stationId).ToList();
                    var stationWindSpeedData = stationData.Where(d => !double.IsNaN(d.WindSpeedKn) && d.WindSpeedKn >= 0).ToList();
                    var stationWindDirData = stationData.Where(d => !double.IsNaN(d.WindDirectionDegrees)).ToList();
                    
                    if (stationWindSpeedData.Any())
                    {
                        var stationWindStats = new WindStatistics
                        {
                            AvgSpeed = stationWindSpeedData.Average(d => d.WindSpeedKn),
                            MaxSpeed = stationWindSpeedData.Max(d => d.WindSpeedKn),
                            MinSpeed = stationWindSpeedData.Min(d => d.WindSpeedKn),
                            MedianSpeed = CalculateMedian(stationWindSpeedData.Select(d => d.WindSpeedKn)),
                            SpeedStdDev = CalculateStandardDeviation(stationWindSpeedData.Select(d => d.WindSpeedKn))
                        };
                        
                        // Calcular direção média e predominante
                        if (stationWindDirData.Any())
                        {
                            stationWindStats.AvgDirection = CalculateCircularMean(stationWindDirData.Select(d => d.WindDirectionDegrees));
                            stationWindStats.PredominantDirection = GetPredominantDirection(stationWindDirData.Select(d => d.WindDirectionDegrees).ToList());
                        }
                        
                        // Calcular estatísticas de rajadas
                        var stationGustData = stationData.Where(d => !double.IsNaN(d.GustKn) && d.GustKn > 0).ToList();
                        if (stationGustData.Any())
                        {
                            stationWindStats.AvgGust = stationGustData.Average(d => d.GustKn);
                            stationWindStats.MaxGust = stationGustData.Max(d => d.GustKn);
                            stationWindStats.MedianGust = CalculateMedian(stationGustData.Select(d => d.GustKn));
                            
                            // Calcular razão rajada/velocidade média (Gust factor)
                            if (stationWindStats.AvgSpeed > 0)
                            {
                                stationWindStats.GustFactor = stationWindStats.MaxGust / stationWindStats.AvgSpeed;
                            }
                        }
                          // Calcular distribuição de Beaufort
                        var beaufortDistribution = CalculateBeaufortDistribution(stationWindSpeedData.Select(d => d.WindSpeedKn));
                        foreach (var kv in beaufortDistribution)
                        {
                            stationWindStats.BeaufortDistribution.Add(kv.Key.ToString(), kv.Value);
                        }
                        
                        // Adicionar ao dicionário de estatísticas
                        response.WindStats[stationId] = stationWindStats;
                        
                        // Adicionar estatísticas importantes dessa estação ao dicionário geral também
                        response.Statistics.Add($"max_wind_speed_{stationId}", stationWindStats.MaxSpeed);
                        response.Statistics.Add($"max_gust_{stationId}", stationWindStats.MaxGust);
                    }
                }
                
                _logger.LogInformation("Estatísticas de vento calculadas para {Count} estações", response.WindStats.Count - 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular estatísticas de vento: {Message}", ex.Message);
            }
        }
        
        // Calcular distribuição de velocidades do vento na escala de Beaufort
        private Dictionary<int, double> CalculateBeaufortDistribution(IEnumerable<double> windSpeedsKn)
        {
            var result = new Dictionary<int, double>();
            var windSpeedsList = windSpeedsKn.ToList();
            
            if (!windSpeedsList.Any())
                return result;
            
            int totalCount = windSpeedsList.Count;
            
            // Definir limites de velocidade para cada categoria de Beaufort (em nós)
            var beaufortLimits = new[]
            {
                0, 1, 3, 7, 10, 16, 21, 27, 33, 40, 47, 55, 63
            };
            
            // Calcular percentual de registros em cada categoria
            for (int i = 0; i <= 12; i++)
            {
                double lowerLimit = i < beaufortLimits.Length ? beaufortLimits[i] : beaufortLimits.Last();
                double upperLimit = i + 1 < beaufortLimits.Length ? beaufortLimits[i + 1] : double.MaxValue;
                
                int count;
                if (i == 12) // Último nível de Beaufort (Furacão)
                {
                    count = windSpeedsList.Count(v => v >= lowerLimit);
                }
                else
                {
                    count = windSpeedsList.Count(v => v >= lowerLimit && v < upperLimit);
                }
                
                double percentage = (double)count / totalCount * 100;
                result[i] = percentage;
            }
            
            return result;        }

        private async Task CalculateTemperatureStatistics(List<SensorData> data, AnalysisResponse response)
        {
            try
            {
                _logger.LogInformation("Iniciando cálculo de estatísticas de temperatura para {Count} registros", data.Count);
                
                // Obter dados válidos
                var validSeaTempData = data.Where(d => !double.IsNaN(d.SeaTemperatureC)).ToList();
                var validAirTempData = data.Where(d => !double.IsNaN(d.AirTemperatureC)).ToList();
                var validHumidityData = data.Where(d => !double.IsNaN(d.RelativeHumidityPercent) && d.RelativeHumidityPercent >= 0 && d.RelativeHumidityPercent <= 100).ToList();
                
                if (!validSeaTempData.Any() && !validAirTempData.Any())
                {
                    _logger.LogWarning("Nenhum dado válido de temperatura encontrado");
                    return;
                }
                
                // Inicializar dicionário de estatísticas de temperatura
                response.TempStats["overall"] = new TemperatureStatistics();
                
                // Processar estatísticas gerais em paralelo para melhor performance
                await Task.Run(() => {
                    var overallStats = new TemperatureStatistics();
                    
                    // Temperatura do mar
                    if (validSeaTempData.Any())
                    {
                        overallStats.AvgSeaTemp = validSeaTempData.Average(d => d.SeaTemperatureC);
                        overallStats.MaxSeaTemp = validSeaTempData.Max(d => d.SeaTemperatureC);
                        overallStats.MinSeaTemp = validSeaTempData.Min(d => d.SeaTemperatureC);
                        overallStats.MedianSeaTemp = CalculateMedian(validSeaTempData.Select(d => d.SeaTemperatureC));
                        overallStats.SeaTempStdDev = CalculateStandardDeviation(validSeaTempData.Select(d => d.SeaTemperatureC));
                    }
                    
                    // Temperatura do ar
                    if (validAirTempData.Any())
                    {
                        overallStats.AvgAirTemp = validAirTempData.Average(d => d.AirTemperatureC);
                        overallStats.MaxAirTemp = validAirTempData.Max(d => d.AirTemperatureC);
                        overallStats.MinAirTemp = validAirTempData.Min(d => d.AirTemperatureC);
                        overallStats.MedianAirTemp = CalculateMedian(validAirTempData.Select(d => d.AirTemperatureC));
                        overallStats.AirTempStdDev = CalculateStandardDeviation(validAirTempData.Select(d => d.AirTemperatureC));
                    }
                    
                    // Umidade relativa
                    if (validHumidityData.Any())
                    {
                        overallStats.AvgHumidity = validHumidityData.Average(d => d.RelativeHumidityPercent);
                        overallStats.MaxHumidity = validHumidityData.Max(d => d.RelativeHumidityPercent);
                        overallStats.MinHumidity = validHumidityData.Min(d => d.RelativeHumidityPercent);
                    }
                    
                    // Calcular diferenças médias entre temperatura do ar e do mar
                    if (validSeaTempData.Any() && validAirTempData.Any())
                    {
                        // Para comparações precisas, precisamos de registros com ambas as temperaturas no mesmo timestamp
                        var recordsWithBothTemps = data.Where(d => !double.IsNaN(d.SeaTemperatureC) && !double.IsNaN(d.AirTemperatureC)).ToList();
                        if (recordsWithBothTemps.Any())
                        {
                            overallStats.AvgTempDifference = recordsWithBothTemps.Average(d => d.AirTemperatureC - d.SeaTemperatureC);
                            overallStats.MaxTempDifference = recordsWithBothTemps.Max(d => d.AirTemperatureC - d.SeaTemperatureC);
                            overallStats.MinTempDifference = recordsWithBothTemps.Min(d => d.AirTemperatureC - d.SeaTemperatureC);
                        }
                    }
                    
                    // Atribuir estatísticas gerais
                    response.TempStats["overall"] = overallStats;
                    
                    // Adicionar algumas estatísticas no dicionário geral também
                    if (validSeaTempData.Any())
                    {
                        response.Statistics.Add("temp_sea_range", overallStats.MaxSeaTemp - overallStats.MinSeaTemp);
                    }
                    
                    if (validAirTempData.Any())
                    {
                        response.Statistics.Add("temp_air_range", overallStats.MaxAirTemp - overallStats.MinAirTemp);
                    }
                });
                
                // Calcular estatísticas por estação
                var stationIds = data
                    .Select(d => d.StationId)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();
                
                _logger.LogInformation("Calculando estatísticas de temperatura para {Count} estações: {Stations}", 
                    stationIds.Count, string.Join(", ", stationIds));
                
                foreach (var stationId in stationIds)
                {
                    var stationData = data.Where(d => d.StationId == stationId).ToList();
                    var stationSeaTempData = stationData.Where(d => !double.IsNaN(d.SeaTemperatureC)).ToList();
                    var stationAirTempData = stationData.Where(d => !double.IsNaN(d.AirTemperatureC)).ToList();
                    var stationHumidityData = stationData.Where(d => !double.IsNaN(d.RelativeHumidityPercent) && 
                                                                     d.RelativeHumidityPercent >= 0 && 
                                                                     d.RelativeHumidityPercent <= 100).ToList();
                    
                    if (stationSeaTempData.Any() || stationAirTempData.Any())
                    {
                        var stationTempStats = new TemperatureStatistics();
                        
                        // Temperatura do mar
                        if (stationSeaTempData.Any())
                        {
                            stationTempStats.AvgSeaTemp = stationSeaTempData.Average(d => d.SeaTemperatureC);
                            stationTempStats.MaxSeaTemp = stationSeaTempData.Max(d => d.SeaTemperatureC);
                            stationTempStats.MinSeaTemp = stationSeaTempData.Min(d => d.SeaTemperatureC);
                            stationTempStats.MedianSeaTemp = CalculateMedian(stationSeaTempData.Select(d => d.SeaTemperatureC));
                            stationTempStats.SeaTempStdDev = CalculateStandardDeviation(stationSeaTempData.Select(d => d.SeaTemperatureC));
                        }
                        
                        // Temperatura do ar
                        if (stationAirTempData.Any())
                        {
                            stationTempStats.AvgAirTemp = stationAirTempData.Average(d => d.AirTemperatureC);
                            stationTempStats.MaxAirTemp = stationAirTempData.Max(d => d.AirTemperatureC);
                            stationTempStats.MinAirTemp = stationAirTempData.Min(d => d.AirTemperatureC);
                            stationTempStats.MedianAirTemp = CalculateMedian(stationAirTempData.Select(d => d.AirTemperatureC));
                            stationTempStats.AirTempStdDev = CalculateStandardDeviation(stationAirTempData.Select(d => d.AirTemperatureC));
                        }
                        
                        // Umidade relativa
                        if (stationHumidityData.Any())
                        {
                            stationTempStats.AvgHumidity = stationHumidityData.Average(d => d.RelativeHumidityPercent);
                            stationTempStats.MaxHumidity = stationHumidityData.Max(d => d.RelativeHumidityPercent);
                            stationTempStats.MinHumidity = stationHumidityData.Min(d => d.RelativeHumidityPercent);
                        }
                        
                        // Calcular diferenças entre temperatura do ar e do mar
                        if (stationSeaTempData.Any() && stationAirTempData.Any())
                        {
                            // Para comparações precisas, precisamos de registros com ambas as temperaturas no mesmo timestamp
                            var recordsWithBothTemps = stationData.Where(d => !double.IsNaN(d.SeaTemperatureC) && !double.IsNaN(d.AirTemperatureC)).ToList();
                            if (recordsWithBothTemps.Any())
                            {
                                stationTempStats.AvgTempDifference = recordsWithBothTemps.Average(d => d.AirTemperatureC - d.SeaTemperatureC);
                                stationTempStats.MaxTempDifference = recordsWithBothTemps.Max(d => d.AirTemperatureC - d.SeaTemperatureC);
                                stationTempStats.MinTempDifference = recordsWithBothTemps.Min(d => d.AirTemperatureC - d.SeaTemperatureC);
                            }
                        }
                        
                        // Adicionar ao dicionário de estatísticas
                        response.TempStats[stationId] = stationTempStats;
                        
                        // Adicionar estatísticas importantes dessa estação ao dicionário geral também
                        if (stationSeaTempData.Any())
                        {
                            response.Statistics.Add($"max_sea_temp_{stationId}", stationTempStats.MaxSeaTemp);
                        }
                        
                        if (stationAirTempData.Any())
                        {
                            response.Statistics.Add($"max_air_temp_{stationId}", stationTempStats.MaxAirTemp);
                        }
                    }
                }
                
                _logger.LogInformation("Estatísticas de temperatura calculadas para {Count} estações", response.TempStats.Count - 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular estatísticas de temperatura: {Message}", ex.Message);
            }
        }
        
        // Método para determinar a direção predominante a partir de graus
        private string GetPredominantDirection(List<double> directions)
        {
            if (!directions.Any()) 
                return "Desconhecida";
                
            var directionCounts = new Dictionary<string, int>();
            foreach (var dir in directions)
            {
                string windDirection = DegreesToCardinalDirection(dir);
                if (!directionCounts.ContainsKey(windDirection))
                    directionCounts[windDirection] = 0;
                directionCounts[windDirection]++;
            }
            
            return directionCounts.OrderByDescending(kv => kv.Value).First().Key;
        }
        
        // Converter graus para direção cardinal (N, NE, E, SE, S, SW, W, NW)
        private string DegreesToCardinalDirection(double degrees)
        {
            string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW", "N" };
            return directions[(int)Math.Round(((degrees % 360) / 45)) % 8];
        }public override async Task<PatternDetectionResponse> DetectPatterns(PatternDetectionRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("Iniciando detecção de padrões para o campo {DataField} e tipo {PatternType}...", 
                    request.DataField, request.PatternType);
                
                if (request.DataPoints == null || request.DataPoints.Count == 0)
                {
                    return new PatternDetectionResponse
                    {
                        Success = false,
                        ErrorMessage = "Nenhum dado fornecido para análise de padrões"
                    };
                }
                
                var patterns = new List<Pattern>();
                var stormEvents = new List<StormEvent>();
                var anomalies = new List<AnomalyEvent>();
                
                // Para processamento mais intensivo, usamos um Task para não bloquear a thread
                await Task.Run(() =>
                {
                    switch (request.PatternType.ToLower())
                    {
                        case "trend":
                            DetectTrends(request.DataPoints, request.DataField, patterns);
                            break;
                            
                        case "anomaly":
                            DetectAnomalies(request.DataPoints, request.DataField, anomalies);
                            break;
                            
                        case "cycle":
                            DetectCycles(request.DataPoints, request.DataField, patterns);
                            break;
                            
                        case "storm":
                            DetectStorms(request.DataPoints, stormEvents);
                            break;
                            
                        case "all":
                            DetectTrends(request.DataPoints, request.DataField, patterns);
                            DetectAnomalies(request.DataPoints, request.DataField, anomalies);
                            DetectCycles(request.DataPoints, request.DataField, patterns);
                            DetectStorms(request.DataPoints, stormEvents);
                            break;
                            
                        default:
                            _logger.LogWarning("Tipo de padrão desconhecido: {PatternType}", request.PatternType);
                            break;
                    }
                });
                
                _logger.LogInformation("Detecção de padrões concluída. Encontrados: {PatternsCount} padrões, {StormEventsCount} eventos de tempestade, {AnomaliesCount} anomalias",
                    patterns.Count, stormEvents.Count, anomalies.Count);
                
                return new PatternDetectionResponse
                {
                    Patterns = { patterns },
                    StormEvents = { stormEvents },
                    Anomalies = { anomalies },
                    Success = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao detectar padrões");
                return new PatternDetectionResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        // Detecta tendências crescentes ou decrescentes nos dados
        private void DetectTrends(RepeatedField<SensorData> dataPoints, string dataField, List<Pattern> patterns)
        {
            _logger.LogInformation("Analisando tendências para o campo {DataField}", dataField);
            
            // Ordenar os dados por timestamp para análise temporal
            var orderedData = dataPoints
                .OrderBy(d => DateTime.Parse(d.Timestamp))
                .ToList();
                
            if (orderedData.Count < 3)
            {
                _logger.LogWarning("Dados insuficientes para detectar tendências (mínimo 3 pontos)");
                return;
            }
            
            // Extrair os valores do campo especificado
            var values = ExtractFieldValues(orderedData, dataField);
            if (values.Count == 0) return;
            
            // Análise de tendência por regressão linear
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            int n = values.Count;
            
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += values[i];
                sumXY += i * values[i];
                sumX2 += i * i;
            }
            
            // Coeficiente de inclinação (slope)
            double slope = 0;
            if ((n * sumX2 - sumX * sumX) != 0)
            {
                slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            }
            
            // Confiança baseada em r-squared
            double meanY = sumY / n;
            double totalSS = 0, regressionSS = 0;
            
            for (int i = 0; i < n; i++)
            {
                double predictedY = slope * i + (sumY - slope * sumX) / n;
                totalSS += Math.Pow(values[i] - meanY, 2);
                regressionSS += Math.Pow(predictedY - meanY, 2);
            }
            
            double rSquared = (totalSS > 0) ? regressionSS / totalSS : 0;
            double confidenceLevel = Math.Min(Math.Max(rSquared, 0), 1);
            
            // Detectar se há uma tendência significativa
            if (Math.Abs(slope) > 0.05 && confidenceLevel > 0.5)
            {
                string trendType = slope > 0 ? "crescente" : "decrescente";
                string intensity = Math.Abs(slope) > 0.2 ? "forte" : "moderada";
                
                patterns.Add(new Pattern
                {
                    Type = $"tendência {trendType}",
                    Description = $"Tendência {trendType} {intensity} detectada em {dataField}",
                    Confidence = confidenceLevel,
                    StartTime = orderedData.First().Timestamp,
                    EndTime = orderedData.Last().Timestamp,
                    Intensity = Math.Abs(slope),
                    Location = GetMostFrequentLocation(orderedData)
                });
                
                _logger.LogInformation("Tendência {TrendType} detectada com confiança {Confidence:P2}", trendType, confidenceLevel);
            }
        }
        
        // Detecta anomalias (outliers) nos dados
        private void DetectAnomalies(RepeatedField<SensorData> dataPoints, string dataField, List<AnomalyEvent> anomalies)
        {
            _logger.LogInformation("Analisando anomalias para o campo {DataField}", dataField);
            
            var values = ExtractFieldValues(dataPoints.ToList(), dataField);
            if (values.Count < 5) return;
            
            // Cálculo de limites para detecção de outliers usando o método IQR (Intervalo Interquartil)
            var sortedValues = values.OrderBy(v => v).ToList();
            int n = sortedValues.Count;
            
            double q1 = sortedValues[(int)(n * 0.25)];
            double q3 = sortedValues[(int)(n * 0.75)];
            double iqr = q3 - q1;
            
            // Limites para considerar um valor como outlier
            double lowerBound = q1 - 1.5 * iqr;
            double upperBound = q3 + 1.5 * iqr;
            
            // Média para calcular o desvio percentual
            double mean = values.Average();
            
            for (int i = 0; i < dataPoints.Count; i++)
            {
                double value = GetFieldValue(dataPoints[i], dataField);
                if (double.IsNaN(value)) continue;
                
                if (value < lowerBound || value > upperBound)
                {
                    // Calcular o desvio em relação à média
                    double deviationPercent = 100 * Math.Abs(value - mean) / mean;
                    
                    // Calcular o nível de confiança baseado no desvio
                    // Quanto mais distante dos limites, maior a confiança
                    double distanceFromBound = Math.Min(
                        Math.Abs(value - lowerBound),
                        Math.Abs(value - upperBound)
                    );
                    int confidence = (int)Math.Min(Math.Max(distanceFromBound / (0.1 * mean) * 100, 50), 100);
                    
                    anomalies.Add(new AnomalyEvent
                    {
                        Timestamp = dataPoints[i].Timestamp,
                        Parameter = dataField,
                        ExpectedValue = mean,
                        ActualValue = value,
                        DeviationPercent = deviationPercent,
                        Location = dataPoints[i].StationId,
                        Confidence = confidence
                    });
                    
                    _logger.LogInformation("Anomalia detectada em {Parameter}: valor {Value} (desvio de {Deviation:P2})",
                        dataField, value, deviationPercent / 100);
                }
            }
        }
        
        // Detecta padrões cíclicos nos dados
        private void DetectCycles(RepeatedField<SensorData> dataPoints, string dataField, List<Pattern> patterns)
        {
            _logger.LogInformation("Analisando padrões cíclicos para o campo {DataField}", dataField);
            
            var orderedData = dataPoints
                .OrderBy(d => DateTime.Parse(d.Timestamp))
                .ToList();
                
            var values = ExtractFieldValues(orderedData, dataField);
            if (values.Count < 10)
            {
                _logger.LogWarning("Dados insuficientes para análise cíclica (mínimo 10 pontos)");
                return;
            }
            
            // Análise de autocorrelação para detectar padrões cíclicos
            // Testamos diferentes períodos para encontrar o melhor
            int bestLag = 0;
            double bestCorrelation = 0;
            
            // Testar períodos de 2 a metade do tamanho da série
            for (int lag = 2; lag <= values.Count / 2; lag++)
            {
                // Calcular autocorrelação para o lag atual
                double correlation = 0;
                int count = 0;
                
                for (int i = 0; i < values.Count - lag; i++)
                {
                    correlation += values[i] * values[i + lag];
                    count++;
                }
                
                if (count > 0)
                {
                    correlation /= count;
                    
                    // Normalizar a correlação
                    double meanX = values.Take(values.Count - lag).Average();
                    double meanY = values.Skip(lag).Average();
                    double stdX = Math.Sqrt(values.Take(values.Count - lag).Select(x => Math.Pow(x - meanX, 2)).Average());
                    double stdY = Math.Sqrt(values.Skip(lag).Select(y => Math.Pow(y - meanY, 2)).Average());
                    
                    if (stdX > 0 && stdY > 0)
                    {
                        correlation = (correlation - meanX * meanY) / (stdX * stdY);
                        
                        if (correlation > 0.5 && correlation > bestCorrelation)
                        {
                            bestCorrelation = correlation;
                            bestLag = lag;
                        }
                    }
                }
            }
            
            // Se encontramos um ciclo com correlação suficientemente alta
            if (bestLag > 0 && bestCorrelation > 0.5)
            {
                // Analisar o período em termos de tempo
                var timeStart = DateTime.Parse(orderedData.First().Timestamp);
                var timeEnd = DateTime.Parse(orderedData[bestLag].Timestamp);
                var cyclePeriod = timeEnd - timeStart;
                
                patterns.Add(new Pattern
                {
                    Type = "padrão cíclico",
                    Description = $"Ciclo de {cyclePeriod.TotalHours:F1} horas detectado em {dataField}",
                    Confidence = bestCorrelation,
                    StartTime = orderedData.First().Timestamp,
                    EndTime = orderedData.Last().Timestamp,
                    Intensity = bestCorrelation,
                    Location = GetMostFrequentLocation(orderedData)
                });
                
                _logger.LogInformation("Ciclo detectado: período de {Period:F1} horas com confiança {Confidence:P2}",
                    cyclePeriod.TotalHours, bestCorrelation);
            }
        }
        
        // Detecta eventos de tempestade nos dados
        private void DetectStorms(RepeatedField<SensorData> dataPoints, List<StormEvent> stormEvents)
        {
            _logger.LogInformation("Analisando eventos de tempestade nos dados oceânicos");
            
            var orderedData = dataPoints
                .OrderBy(d => DateTime.Parse(d.Timestamp))
                .ToList();
            
            if (orderedData.Count < 3)
            {
                _logger.LogWarning("Dados insuficientes para detecção de tempestades");
                return;
            }
            
            // Definir limiares para considerar uma condição de tempestade
            const double stormWaveHeightThreshold = 2.5;  // em metros
            const double stormWindSpeedThreshold = 20.0;  // em nós
            const double stormGustThreshold = 25.0;       // em nós
            
            bool inStormCondition = false;
            DateTime? stormStart = null;
            double maxWaveHeight = 0;
            double maxWindSpeed = 0;
            double maxGust = 0;
            string stormLocation = "";
            
            // Analisar a série temporal para detectar condições de tempestade sustentadas
            foreach (var data in orderedData)
            {
                bool isStormCondition = 
                    (!double.IsNaN(data.WaveHeightM) && data.WaveHeightM >= stormWaveHeightThreshold) ||
                    (!double.IsNaN(data.WindSpeedKn) && data.WindSpeedKn >= stormWindSpeedThreshold) ||
                    (!double.IsNaN(data.GustKn) && data.GustKn >= stormGustThreshold);
                
                if (isStormCondition && !inStormCondition)
                {
                    // Início de uma tempestade
                    inStormCondition = true;
                    stormStart = DateTime.Parse(data.Timestamp);
                    maxWaveHeight = data.WaveHeightM;
                    maxWindSpeed = data.WindSpeedKn;
                    maxGust = data.GustKn;
                    stormLocation = data.StationId;
                }
                else if (isStormCondition && inStormCondition)
                {
                    // Continuação de uma tempestade
                    if (!double.IsNaN(data.WaveHeightM)) maxWaveHeight = Math.Max(maxWaveHeight, data.WaveHeightM);
                    if (!double.IsNaN(data.WindSpeedKn)) maxWindSpeed = Math.Max(maxWindSpeed, data.WindSpeedKn);
                    if (!double.IsNaN(data.GustKn)) maxGust = Math.Max(maxGust, data.GustKn);
                }
                else if (!isStormCondition && inStormCondition)
                {
                    // Fim de uma tempestade
                    inStormCondition = false;
                    
                    // Calcular a severidade da tempestade (1-5)
                    int severity = CalculateStormSeverity(maxWaveHeight, maxWindSpeed, maxGust);
                    
                    stormEvents.Add(new StormEvent
                    {
                        StartTime = stormStart?.ToString("o") ?? data.Timestamp,
                        EndTime = data.Timestamp,
                        PeakWaveHeight = maxWaveHeight,
                        PeakWindSpeed = maxWindSpeed,
                        PeakGust = maxGust,
                        Location = stormLocation,
                        Severity = severity,
                        Description = $"Tempestade de intensidade {severity}/5 com ondas de {maxWaveHeight:F1}m e ventos de {maxWindSpeed:F1} nós"
                    });
                    
                    _logger.LogInformation("Tempestade detectada de {StartTime} a {EndTime}, severidade {Severity}/5",
                        stormStart, data.Timestamp, severity);
                    
                    // Resetar para o próximo evento
                    stormStart = null;
                    maxWaveHeight = 0;
                    maxWindSpeed = 0;
                    maxGust = 0;
                }
            }
            
            // Verificar se ainda há uma tempestade em andamento no final da série
            if (inStormCondition && stormStart.HasValue)
            {
                var lastTime = orderedData.Last().Timestamp;
                int severity = CalculateStormSeverity(maxWaveHeight, maxWindSpeed, maxGust);
                
                stormEvents.Add(new StormEvent
                {
                    StartTime = stormStart.Value.ToString("o"),
                    EndTime = lastTime,
                    PeakWaveHeight = maxWaveHeight,
                    PeakWindSpeed = maxWindSpeed,
                    PeakGust = maxGust,
                    Location = stormLocation,
                    Severity = severity,
                    Description = $"Tempestade em andamento de intensidade {severity}/5 com ondas de {maxWaveHeight:F1}m e ventos de {maxWindSpeed:F1} nós"
                });
                
                _logger.LogInformation("Tempestade em andamento desde {StartTime}, severidade {Severity}/5",
                    stormStart, severity);
            }
        }
        
        // Calcula a severidade de uma tempestade (1-5) com base nos parâmetros
        private int CalculateStormSeverity(double waveHeight, double windSpeed, double gust)
        {
            // Normalizar os valores para uma escala de 0-1
            double waveScore = Math.Min(Math.Max(0, (waveHeight - 2.0) / 8.0), 1.0);  // 2-10m
            double windScore = Math.Min(Math.Max(0, (windSpeed - 15.0) / 45.0), 1.0); // 15-60 nós
            double gustScore = Math.Min(Math.Max(0, (gust - 20.0) / 60.0), 1.0);      // 20-80 nós
            
            // Peso maior para ondas altas
            double combinedScore = (waveScore * 0.5) + (windScore * 0.3) + (gustScore * 0.2);
            
            // Converter para escala 1-5
            return (int)Math.Round(1 + combinedScore * 4);
        }
        
        // Extrai os valores de um campo específico da lista de pontos de dados
        private List<double> ExtractFieldValues(List<SensorData> dataPoints, string dataField)
        {
            var values = new List<double>();
            
            foreach (var data in dataPoints)
            {
                double value = GetFieldValue(data, dataField);
                if (!double.IsNaN(value))
                {
                    values.Add(value);
                }
            }
            
            if (values.Count == 0)
            {
                _logger.LogWarning("Nenhum valor válido encontrado para o campo {DataField}", dataField);
            }
            
            return values;
        }
        
        // Obtém o valor de um campo específico de um ponto de dados
        private double GetFieldValue(SensorData data, string dataField)
        {
            switch (dataField.ToLower())
            {
                case "wave_height":
                    return data.WaveHeightM;
                case "wave_period":
                    return data.WavePeriodS;
                case "wave_direction":
                    return data.MeanWaveDirectionDegrees;
                case "wind_speed":
                    return data.WindSpeedKn;
                case "wind_direction":
                    return data.WindDirectionDegrees;
                case "gust":
                    return data.GustKn;
                case "temperature":
                case "air_temperature":
                    return data.AirTemperatureC;
                case "sea_temperature":
                    return data.SeaTemperatureC;
                case "humidity":
                    return data.RelativeHumidityPercent;
                case "pressure":
                    return data.AtmosphereMb;
                default:
                    _logger.LogWarning("Campo desconhecido: {DataField}", dataField);
                    return double.NaN;
            }
        }
        
        // Obtém a localização mais frequente nos dados
        private string GetMostFrequentLocation(List<SensorData> dataPoints)
        {
            return dataPoints
                .GroupBy(d => d.StationId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "desconhecido";
        }
        
        // Calcular desvio padrão para uma sequência de valores
        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var valuesList = values.ToList();
            if (!valuesList.Any())
                return 0;
                
            var avg = valuesList.Average();
            var sum = valuesList.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sum / valuesList.Count);
        }
        
        // Calcular distribuição de valores em faixas
        private Dictionary<(double, double), double> CalculateDistribution(
            IEnumerable<double> values,
            double min,
            double max,
            double binSize)
        {
            var result = new Dictionary<(double, double), double>();
            var valuesList = values.ToList();
            
            if (!valuesList.Any())
                return result;
                
            int totalCount = valuesList.Count;
            double binStart = min;
            
            while (binStart < max)
            {
                double binEnd = binStart + binSize;
                int count = valuesList.Count(v => v >= binStart && v < binEnd);
                double percentage = (double)count / totalCount * 100;
                
                result.Add((binStart, binEnd), percentage);
                binStart = binEnd;
            }
            
            return result;
        }
        
        // Calcular direção média circular para valores de direção em graus
        private double CalculateCircularMean(IEnumerable<double> directions)
        {
            var directionsList = directions.ToList();
            if (!directionsList.Any())
                return 0;
                
            // Converter graus para radianos
            var radiansValues = directionsList.Select(d => d * Math.PI / 180.0);
            
            // Calcular os componentes x e y
            double sumSin = radiansValues.Sum(r => Math.Sin(r));
            double sumCos = radiansValues.Sum(r => Math.Cos(r));
            
            // Calcular o ângulo médio em radianos
            double meanRadians = Math.Atan2(sumSin, sumCos);
            
            // Converter de volta para graus no intervalo [0, 360)
            double meanDegrees = meanRadians * 180.0 / Math.PI;
            if (meanDegrees < 0)
                meanDegrees += 360.0;
                
            return meanDegrees;
        }
    }
}