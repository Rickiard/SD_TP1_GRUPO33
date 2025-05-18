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
        }

        public override Task<FormatConversionResponse> ConvertFormat(FormatConversionRequest request, ServerCallContext context)
        {
            try
            {
                // Simple format conversion logic
                ByteString convertedData = request.Data;
                
                // Here you would implement actual format conversion logic
                // For now, we'll just return the same data
                
                return Task.FromResult(new FormatConversionResponse
                {
                    ConvertedData = convertedData,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting format");
                return Task.FromResult(new FormatConversionResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<SensorDataResponse> NormalizeSensorRates(SensorDataRequest request, ServerCallContext context)
        {
            try
            {
                var normalizedReadings = new List<SensorReading>();
                
                // Simple normalization logic
                // Here you would implement actual rate normalization
                // For now, we'll just return the same readings
                normalizedReadings.AddRange(request.Readings);
                
                return Task.FromResult(new SensorDataResponse
                {
                    NormalizedReadings = { normalizedReadings },
                    Success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error normalizing sensor rates");
                return Task.FromResult(new SensorDataResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
} 