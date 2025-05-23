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
                
                // Simulando processamento assíncrono
                await Task.Delay(500);
                
                _logger.LogInformation("Iniciando análise de dados oceânicos...");
                _logger.LogInformation($"Tipo de análise: {request.AnalysisType}, Intervalo: {request.TimeRange}");
                
                if (request.DataPoints.Count == 0)
                {
                    _logger.LogWarning("Nenhum dado fornecido para análise");
                    return new AnalysisResponse { Success = false, ErrorMessage = "Nenhum dado fornecido para análise" };
                }

                // Estatísticas gerais
                response.Statistics.Add("total_records", request.DataPoints.Count);
                
                // Filtrar por localização se especificada
                var dataPoints = request.DataPoints.ToList();
                if (!string.IsNullOrEmpty(request.Location) && request.Location != "all")
                {
                    dataPoints = dataPoints.Where(d => d.StationId == request.Location).ToList();
                    
                    if (dataPoints.Count == 0)
                    {
                        _logger.LogWarning($"Nenhum dado encontrado para a localização: {request.Location}");
                        return new AnalysisResponse 
                        { 
                            Success = false, 
                            ErrorMessage = $"Nenhum dado encontrado para a localização: {request.Location}" 
                        };
                    }
                    
                    response.Statistics.Add("filtered_records", dataPoints.Count);
                }
                
                // Calcular estatísticas básicas
                if (dataPoints.Any(d => !double.IsNaN(d.WaveHeightM)))
                {
                    var validWaveData = dataPoints.Where(d => !double.IsNaN(d.WaveHeightM)).ToList();
                    response.Statistics.Add("avg_wave_height", validWaveData.Average(d => d.WaveHeightM));
                    response.Statistics.Add("max_wave_height", validWaveData.Max(d => d.WaveHeightM));
                    response.Statistics.Add("min_wave_height", validWaveData.Min(d => d.WaveHeightM));
                }
                
                if (dataPoints.Any(d => !double.IsNaN(d.WindSpeedKn)))
                {
                    var validWindData = dataPoints.Where(d => !double.IsNaN(d.WindSpeedKn)).ToList();
                    response.Statistics.Add("avg_wind_speed", validWindData.Average(d => d.WindSpeedKn));
                    response.Statistics.Add("max_wind_speed", validWindData.Max(d => d.WindSpeedKn));
                }
                
                if (dataPoints.Any(d => !double.IsNaN(d.SeaTemperatureC)))
                {
                    var validTempData = dataPoints.Where(d => !double.IsNaN(d.SeaTemperatureC)).ToList();
                    response.Statistics.Add("avg_sea_temp", validTempData.Average(d => d.SeaTemperatureC));
                    response.Statistics.Add("max_sea_temp", validTempData.Max(d => d.SeaTemperatureC));
                }
                
                _logger.LogInformation("Análise de dados oceânicos concluída.");
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar dados oceânicos");
                return new AnalysisResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        private void CalculateWaveStatistics(IEnumerable<SensorData> data, Dictionary<string, WaveStatistics> results)
        {
            try
            {
                // Ignorar registros com valores NaN
                var validWaveData = data.Where(d => !double.IsNaN(d.WaveHeightM) && !double.IsNaN(d.WavePeriodS)).ToList();
                
                if (!validWaveData.Any())
                {
                    _logger.LogWarning("Nenhum dado válido de ondas encontrado");
                    return;
                }
                
                var waveStats = new WaveStatistics
                {
                    AvgHeight = validWaveData.Average(d => d.WaveHeightM),
                    MaxHeight = validWaveData.Max(d => d.WaveHeightM),
                    MinHeight = validWaveData.Min(d => d.WaveHeightM),
                    AvgPeriod = validWaveData.Average(d => d.WavePeriodS),
                    MaxPeriod = validWaveData.Max(d => d.WavePeriodS),
                    MinPeriod = validWaveData.Min(d => d.WavePeriodS)
                };
                
                // Calcular direção predominante (em graus)
                var validDirectionData = data.Where(d => !double.IsNaN(d.MeanWaveDirectionDegrees)).ToList();
                if (validDirectionData.Any())
                {
                    waveStats.AvgDirection = validDirectionData.Average(d => d.MeanWaveDirectionDegrees);
                    waveStats.PredominantDirection = GetPredominantDirection(validDirectionData.Select(d => d.MeanWaveDirectionDegrees).ToList());
                }
                
                results["overall"] = waveStats;
                
                // Se houver múltiplas estações, calcular estatísticas por estação
                var stations = data.Select(d => d.StationId).Distinct();
                foreach (var station in stations)
                {
                    var stationData = data.Where(d => d.StationId == station).ToList();
                    var validStationWaveData = stationData.Where(d => !double.IsNaN(d.WaveHeightM) && !double.IsNaN(d.WavePeriodS)).ToList();
                    
                    if (validStationWaveData.Any())
                    {
                        var stationWaveStats = new WaveStatistics
                        {
                            AvgHeight = validStationWaveData.Average(d => d.WaveHeightM),
                            MaxHeight = validStationWaveData.Max(d => d.WaveHeightM),
                            MinHeight = validStationWaveData.Min(d => d.WaveHeightM),
                            AvgPeriod = validStationWaveData.Average(d => d.WavePeriodS),
                            MaxPeriod = validStationWaveData.Max(d => d.WavePeriodS),
                            MinPeriod = validStationWaveData.Min(d => d.WavePeriodS)
                        };
                        
                        var validStationDirectionData = stationData.Where(d => !double.IsNaN(d.MeanWaveDirectionDegrees)).ToList();
                        if (validStationDirectionData.Any())
                        {
                            stationWaveStats.AvgDirection = validStationDirectionData.Average(d => d.MeanWaveDirectionDegrees);
                            stationWaveStats.PredominantDirection = GetPredominantDirection(validStationDirectionData.Select(d => d.MeanWaveDirectionDegrees).ToList());
                        }
                        
                        results[station] = stationWaveStats;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular estatísticas de ondas");
            }
        }
        
        private void CalculateWindStatistics(IEnumerable<SensorData> data, Dictionary<string, WindStatistics> results)
        {
            try
            {
                // Ignorar registros com valores NaN
                var validWindData = data.Where(d => !double.IsNaN(d.WindSpeedKn) && !double.IsNaN(d.WindDirectionDegrees)).ToList();
                
                if (!validWindData.Any())
                {
                    _logger.LogWarning("Nenhum dado válido de vento encontrado");
                    return;
                }
                
                var windStats = new WindStatistics
                {
                    AvgSpeed = validWindData.Average(d => d.WindSpeedKn),
                    MaxSpeed = validWindData.Max(d => d.WindSpeedKn),
                    MinSpeed = validWindData.Min(d => d.WindSpeedKn),
                    AvgDirection = validWindData.Average(d => d.WindDirectionDegrees),
                    PredominantDirection = GetPredominantDirection(validWindData.Select(d => d.WindDirectionDegrees).ToList())
                };
                
                // Calcular estatísticas de rajadas
                var validGustData = data.Where(d => !double.IsNaN(d.GustKn)).ToList();
                if (validGustData.Any())
                {
                    windStats.AvgGust = validGustData.Average(d => d.GustKn);
                    windStats.MaxGust = validGustData.Max(d => d.GustKn);
                }
                
                results["overall"] = windStats;
                
                // Se houver múltiplas estações, calcular estatísticas por estação
                var stations = data.Select(d => d.StationId).Distinct();
                foreach (var station in stations)
                {
                    var stationData = data.Where(d => d.StationId == station).ToList();
                    var validStationWindData = stationData.Where(d => !double.IsNaN(d.WindSpeedKn) && !double.IsNaN(d.WindDirectionDegrees)).ToList();
                    
                    if (validStationWindData.Any())
                    {
                        var stationWindStats = new WindStatistics
                        {
                            AvgSpeed = validStationWindData.Average(d => d.WindSpeedKn),
                            MaxSpeed = validStationWindData.Max(d => d.WindSpeedKn),
                            MinSpeed = validStationWindData.Min(d => d.WindSpeedKn),
                            AvgDirection = validStationWindData.Average(d => d.WindDirectionDegrees),
                            PredominantDirection = GetPredominantDirection(validStationWindData.Select(d => d.WindDirectionDegrees).ToList())
                        };
                        
                        var validStationGustData = stationData.Where(d => !double.IsNaN(d.GustKn)).ToList();
                        if (validStationGustData.Any())
                        {
                            stationWindStats.AvgGust = validStationGustData.Average(d => d.GustKn);
                            stationWindStats.MaxGust = validStationGustData.Max(d => d.GustKn);
                        }
                        
                        results[station] = stationWindStats;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular estatísticas de vento");
            }
        }
        
        private void CalculateTemperatureStatistics(IEnumerable<SensorData> data, Dictionary<string, TemperatureStatistics> results)
        {
            try
            {
                // Air temperature statistics
                var validAirTempData = data.Where(d => !double.IsNaN(d.AirTemperatureC)).ToList();
                var validSeaTempData = data.Where(d => !double.IsNaN(d.SeaTemperatureC)).ToList();
                var validHumidityData = data.Where(d => !double.IsNaN(d.RelativeHumidityPercent)).ToList();
                
                if (!validAirTempData.Any() && !validSeaTempData.Any())
                {
                    _logger.LogWarning("Nenhum dado válido de temperatura encontrado");
                    return;
                }
                
                var tempStats = new TemperatureStatistics();
                
                if (validAirTempData.Any())
                {
                    tempStats.AvgAirTemp = validAirTempData.Average(d => d.AirTemperatureC);
                    tempStats.MaxAirTemp = validAirTempData.Max(d => d.AirTemperatureC);
                    tempStats.MinAirTemp = validAirTempData.Min(d => d.AirTemperatureC);
                }
                
                if (validSeaTempData.Any())
                {
                    tempStats.AvgSeaTemp = validSeaTempData.Average(d => d.SeaTemperatureC);
                    tempStats.MaxSeaTemp = validSeaTempData.Max(d => d.SeaTemperatureC);
                    tempStats.MinSeaTemp = validSeaTempData.Min(d => d.SeaTemperatureC);
                }
                
                if (validHumidityData.Any())
                {
                    tempStats.AvgHumidity = validHumidityData.Average(d => d.RelativeHumidityPercent);
                }
                
                results["overall"] = tempStats;
                
                // Se houver múltiplas estações, calcular estatísticas por estação
                var stations = data.Select(d => d.StationId).Distinct();
                foreach (var station in stations)
                {
                    var stationData = data.Where(d => d.StationId == station).ToList();
                    var validStationAirTempData = stationData.Where(d => !double.IsNaN(d.AirTemperatureC)).ToList();
                    var validStationSeaTempData = stationData.Where(d => !double.IsNaN(d.SeaTemperatureC)).ToList();
                    var validStationHumidityData = stationData.Where(d => !double.IsNaN(d.RelativeHumidityPercent)).ToList();
                    
                    if (validStationAirTempData.Any() || validStationSeaTempData.Any())
                    {
                        var stationTempStats = new TemperatureStatistics();
                        
                        if (validStationAirTempData.Any())
                        {
                            stationTempStats.AvgAirTemp = validStationAirTempData.Average(d => d.AirTemperatureC);
                            stationTempStats.MaxAirTemp = validStationAirTempData.Max(d => d.AirTemperatureC);
                            stationTempStats.MinAirTemp = validStationAirTempData.Min(d => d.AirTemperatureC);
                        }
                        
                        if (validStationSeaTempData.Any())
                        {
                            stationTempStats.AvgSeaTemp = validStationSeaTempData.Average(d => d.SeaTemperatureC);
                            stationTempStats.MaxSeaTemp = validStationSeaTempData.Max(d => d.SeaTemperatureC);
                            stationTempStats.MinSeaTemp = validStationSeaTempData.Min(d => d.SeaTemperatureC);
                        }
                        
                        if (validStationHumidityData.Any())
                        {
                            stationTempStats.AvgHumidity = validStationHumidityData.Average(d => d.RelativeHumidityPercent);
                        }
                        
                        results[station] = stationTempStats;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular estatísticas de temperatura");
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
    }
} 