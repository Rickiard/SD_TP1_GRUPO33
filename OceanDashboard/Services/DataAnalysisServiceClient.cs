using Grpc.Net.Client;
using OceanDashboard.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RPC_DataAnalyserService;
using System.Linq;
using Grpc.Core;

namespace OceanDashboard.Services
{
    public class DataAnalysisServiceClient
    {
        private readonly ILogger<DataAnalysisServiceClient> _logger;
        private readonly string _rpcServiceUrl;
        private readonly HttpClient _httpClient;

        public DataAnalysisServiceClient(
            ILogger<DataAnalysisServiceClient> logger,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _logger = logger;
            _rpcServiceUrl = configuration["AppSettings:RpcServiceUrl"] ?? "http://localhost:5073";
            _httpClient = httpClient;
            
            // Permite HTTP/2 sem TLS para desenvolvimento local
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            
            _logger.LogInformation("DataAnalysisServiceClient configurado para se conectar a {RpcServiceUrl}", _rpcServiceUrl);
        }

        public async Task<PatternAnalysisResult> DetectPatternsAsync(
            List<OceanData> oceanData,
            string patternType, 
            string dataField, 
            int windowSize)
        {
            _logger.LogInformation("Chamando serviço gRPC para detectar padrões em {Count} pontos de dados", oceanData.Count);
              try
            {
                // Configure HTTP client timeout
                _httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                var channelOptions = new GrpcChannelOptions { 
                    HttpClient = _httpClient,
                    DisposeHttpClient = false // Don't dispose the HTTP client when the channel is disposed
                };
                
                using var channel = GrpcChannel.ForAddress(_rpcServiceUrl, channelOptions);
                var client = new RPC_DataAnalyserService.DataAnalysisService.DataAnalysisServiceClient(channel);
                
                // Preparar a requisição
                var request = new PatternDetectionRequest
                {
                    PatternType = patternType,
                    DataField = dataField,
                    WindowSize = windowSize
                };

                // Converter dados do oceano para o formato esperado pelo gRPC
                foreach (var data in oceanData)
                {
                    request.DataPoints.Add(ConvertToSensorData(data));
                }

                // Configurar um timeout razoável para a requisição
                var deadline = DateTime.UtcNow.AddSeconds(30);
                var options = new CallOptions(deadline: deadline);

                // Chamar o serviço
                var response = await client.DetectPatternsAsync(request, options);
                
                // Converter a resposta para um modelo do cliente
                var result = new PatternAnalysisResult
                {
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    Patterns = response.Patterns.Select(p => new PatternInfo
                    {
                        Type = p.Type,
                        Description = p.Description,
                        Confidence = p.Confidence,
                        StartTime = DateTime.Parse(p.StartTime),
                        EndTime = DateTime.Parse(p.EndTime),
                        Intensity = p.Intensity,
                        Location = p.Location
                    }).ToList(),
                    StormEvents = response.StormEvents.Select(s => new StormEventInfo
                    {
                        StartTime = DateTime.Parse(s.StartTime),
                        EndTime = DateTime.Parse(s.EndTime),
                        PeakWaveHeight = s.PeakWaveHeight,
                        PeakWindSpeed = s.PeakWindSpeed,
                        PeakGust = s.PeakGust,
                        Location = s.Location,
                        Severity = s.Severity,
                        Description = s.Description
                    }).ToList(),
                    Anomalies = response.Anomalies.Select(a => new AnomalyInfo
                    {
                        Timestamp = DateTime.Parse(a.Timestamp),
                        Parameter = a.Parameter,
                        ExpectedValue = a.ExpectedValue,
                        ActualValue = a.ActualValue,
                        DeviationPercent = a.DeviationPercent,
                        Location = a.Location,
                        Confidence = a.Confidence
                    }).ToList()
                };
                
                _logger.LogInformation("Detectados: {PatternsCount} padrões, {StormCount} tempestades, {AnomalyCount} anomalias",
                    result.Patterns.Count, result.StormEvents.Count, result.Anomalies.Count);
                
                return result;
            }            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable || ex.StatusCode == StatusCode.DeadlineExceeded)
            {
                _logger.LogError(ex, "Serviço de detecção de padrões indisponível ou timeout: {StatusCode} - {Detail}",
                    ex.StatusCode, ex.Status.Detail);
                return new PatternAnalysisResult
                {
                    Success = false,
                    ErrorMessage = "O serviço de análise está indisponível no momento. Por favor, tente novamente mais tarde."
                };
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Erro RPC ao chamar o serviço de detecção de padrões: {StatusCode} - {Detail}",
                    ex.StatusCode, ex.Status.Detail);
                return new PatternAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"Falha na comunicação com o serviço de análise: {ex.Status.Detail}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao chamar o serviço de detecção de padrões");
                return new PatternAnalysisResult
                {
                    Success = false,
                    ErrorMessage = "Ocorreu um erro ao processar a detecção de padrões. Verifique a conexão de rede e tente novamente."
                };
            }
        }

        public async Task<DataAnalysisResult> AnalyzeDataAsync(
            List<OceanData> oceanData,
            string analysisType,
            string timeRange,
            string location)
        {
            _logger.LogInformation("Chamando serviço gRPC para analisar {Count} pontos de dados", oceanData.Count);
            
            try
            {
                var channelOptions = new GrpcChannelOptions { HttpClient = _httpClient };
                using var channel = GrpcChannel.ForAddress(_rpcServiceUrl, channelOptions);
                var client = new RPC_DataAnalyserService.DataAnalysisService.DataAnalysisServiceClient(channel);
                
                // Preparar a requisição
                var request = new AnalysisRequest
                {
                    AnalysisType = analysisType,
                    TimeRange = timeRange,
                    Location = location
                };

                // Converter dados do oceano para o formato esperado pelo gRPC
                foreach (var data in oceanData)
                {
                    request.DataPoints.Add(ConvertToSensorData(data));
                }

                // Configurar um timeout razoável para a requisição
                var deadline = DateTime.UtcNow.AddSeconds(30);
                var options = new CallOptions(deadline: deadline);

                // Chamar o serviço
                var response = await client.AnalyzeDataAsync(request, options);
                
                // Converter a resposta para um modelo do cliente
                var result = new DataAnalysisResult
                {
                    Success = response.Success,
                    ErrorMessage = response.ErrorMessage,
                    Statistics = response.Statistics.ToDictionary(kv => kv.Key, kv => kv.Value)
                };
                
                // Converter as estatísticas de onda
                foreach (var stat in response.WaveStats)
                {
                    result.WaveStatistics[stat.Key] = new WaveStatisticsInfo
                    {
                        AvgHeight = stat.Value.AvgHeight,
                        MaxHeight = stat.Value.MaxHeight,
                        MinHeight = stat.Value.MinHeight,
                        AvgPeriod = stat.Value.AvgPeriod,
                        MaxPeriod = stat.Value.MaxPeriod,
                        MinPeriod = stat.Value.MinPeriod,
                        AvgDirection = stat.Value.AvgDirection,
                        PredominantDirection = stat.Value.PredominantDirection
                    };
                }
                
                // Converter as estatísticas de vento
                foreach (var stat in response.WindStats)
                {
                    result.WindStatistics[stat.Key] = new WindStatisticsInfo
                    {
                        AvgSpeed = stat.Value.AvgSpeed,
                        MaxSpeed = stat.Value.MaxSpeed,
                        MinSpeed = stat.Value.MinSpeed,
                        AvgGust = stat.Value.AvgGust,
                        MaxGust = stat.Value.MaxGust,
                        AvgDirection = stat.Value.AvgDirection,
                        PredominantDirection = stat.Value.PredominantDirection
                    };
                }
                
                // Converter as estatísticas de temperatura
                foreach (var stat in response.TempStats)
                {
                    result.TemperatureStatistics[stat.Key] = new TemperatureStatisticsInfo
                    {
                        AvgAirTemp = stat.Value.AvgAirTemp,
                        MaxAirTemp = stat.Value.MaxAirTemp,
                        MinAirTemp = stat.Value.MinAirTemp,
                        AvgSeaTemp = stat.Value.AvgSeaTemp,
                        MaxSeaTemp = stat.Value.MaxSeaTemp,
                        MinSeaTemp = stat.Value.MinSeaTemp,
                        AvgHumidity = stat.Value.AvgHumidity
                    };
                }
                
                _logger.LogInformation("Análise de dados concluída com sucesso. {StatCount} estatísticas retornadas.",
                    result.Statistics.Count);
                
                return result;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Erro RPC ao chamar o serviço de análise de dados: {StatusCode} - {Detail}",
                    ex.StatusCode, ex.Status.Detail);
                throw new Exception($"Falha na comunicação com o serviço de análise: {ex.Status.Detail}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao chamar o serviço de análise de dados");
                throw new Exception("Não foi possível processar a análise de dados", ex);
            }
        }        // Converte do modelo OceanData para o modelo SensorData do gRPC
        private SensorData ConvertToSensorData(OceanData data)
        {
            return new SensorData
            {
                StationId = string.IsNullOrEmpty(data.StationId) ? data.Location : data.StationId,
                Longitude = data.Longitude,
                Latitude = data.Latitude,
                Timestamp = data.Timestamp.ToString("o"),
                WaveHeightM = data.WaveHeight,
                WavePeriodS = data.WavePeriod,
                MeanWaveDirectionDegrees = data.WaveDirection,
                SeaTemperatureC = data.SeaTemperature,
                // Usar os campos atualizados do modelo OceanData
                WindDirectionDegrees = data.WindDirection,  
                WindSpeedKn = data.WindSpeed,
                AirTemperatureC = data.AirTemperature,
                RelativeHumidityPercent = data.RelativeHumidity
            };
        }
    }
}
