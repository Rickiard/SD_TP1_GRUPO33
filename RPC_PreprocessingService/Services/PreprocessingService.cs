using Grpc.Core;
using RPC_PreprocessingService;
using Google.Protobuf;

namespace RPC_PreprocessingService.Services
{
    public class PreprocessingServiceImpl : PreprocessingService.PreprocessingServiceBase
    {
        private readonly ILogger<PreprocessingServiceImpl> _logger;

        public PreprocessingServiceImpl(ILogger<PreprocessingServiceImpl> logger)
        {
            _logger = logger;
        }        public override async Task<FormatConversionResponse> ConvertFormat(FormatConversionRequest request, ServerCallContext context)
        {
            try
            {
                // Simple format conversion logic
                ByteString convertedData = request.Data;
                
                // Simulando processamento mais demorado
                _logger.LogInformation("Iniciando processamento dos dados...");
                await Task.Delay(1000); // Aguarda 1 segundo para simular processamento
                _logger.LogInformation("Processamento concluído.");
                
                // Here you would implement actual format conversion logic
                // For now, we'll just return the same data
                
                return new FormatConversionResponse
                {
                    ConvertedData = convertedData,
                    Success = true
                };
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting format");
                return new FormatConversionResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }        public override async Task<SensorDataResponse> NormalizeSensorRates(SensorDataRequest request, ServerCallContext context)
        {
            try
            {
                var normalizedReadings = new List<SensorReading>();
                
                // Simulando processamento mais demorado
                _logger.LogInformation("Iniciando normalização das taxas de sensores...");
                await Task.Delay(1000); // Aguarda 1 segundo para simular processamento
                _logger.LogInformation("Normalização concluída.");
                
                // Simple normalization logic
                // Here you would implement actual rate normalization
                // For now, we'll just return the same readings                normalizedReadings.AddRange(request.Readings);
                
                return new SensorDataResponse
                {
                    NormalizedReadings = { normalizedReadings },
                    Success = true
                };
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Error normalizing sensor rates");
                return new SensorDataResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
} 