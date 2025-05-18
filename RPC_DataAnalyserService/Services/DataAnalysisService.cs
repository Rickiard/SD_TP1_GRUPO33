using Grpc.Core;
using RPC_DataAnalyserService;

namespace RPC_DataAnalyserService.Services
{
    public class DataAnalysisServiceImpl : DataAnalysisService.DataAnalysisServiceBase
    {
        private readonly ILogger<DataAnalysisServiceImpl> _logger;

        public DataAnalysisServiceImpl(ILogger<DataAnalysisServiceImpl> logger)
        {
            _logger = logger;
        }

        public override Task<AnalysisResponse> AnalyzeData(AnalysisRequest request, ServerCallContext context)
        {
            try
            {
                var statistics = new Dictionary<string, double>();
                
                // Simple statistical analysis
                if (request.DataPoints.Any())
                {
                    var values = request.DataPoints.Select(d => d.Value).ToList();
                    statistics["mean"] = values.Average();
                    statistics["min"] = values.Min();
                    statistics["max"] = values.Max();
                }
                
                return Task.FromResult(new AnalysisResponse
                {
                    Statistics = { statistics },
                    Success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing data");
                return Task.FromResult(new AnalysisResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<PatternDetectionResponse> DetectPatterns(PatternDetectionRequest request, ServerCallContext context)
        {
            try
            {
                var patterns = new List<Pattern>();
                
                // Simple pattern detection
                // Here you would implement actual pattern detection logic
                // For now, we'll just return an empty list
                
                return Task.FromResult(new PatternDetectionResponse
                {
                    Patterns = { patterns },
                    Success = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting patterns");
                return Task.FromResult(new PatternDetectionResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
} 