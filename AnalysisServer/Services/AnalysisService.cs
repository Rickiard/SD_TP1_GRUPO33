using Grpc.Core;
using AnalysisServer.Protos;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace AnalysisServer.Services
{
    public class AnalysisServiceImpl : AnalysisService.AnalysisServiceBase
    {
        private readonly ILogger<AnalysisServiceImpl> _logger;

        public AnalysisServiceImpl(ILogger<AnalysisServiceImpl> logger)
        {
            _logger = logger;
        }

        public override Task<AnalysisResponse> AnalyzeData(AnalysisRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Analisando dados do Wavy ID: {WavyId}", request.WavyId);

            bool isValid = ValidateData(request.Data);
            string message = isValid ? "Dados validados com sucesso" : "Dados inválidos";

            var response = new AnalysisResponse
            {
                Success = isValid,
                Message = message
            };

            return Task.FromResult(response);
        }

        private bool ValidateData(string data)
        {
            try
            {
                // Split data into lines
                var lines = data.Split('\n')
                               .Where(line => !string.IsNullOrWhiteSpace(line))
                               .ToList();

                if (lines.Count == 0) return false;

                // Validate each line
                foreach (var line in lines)
                {
                    // Check if line contains valid numeric values
                    var values = line.Split(',')
                                   .Select(v => v.Trim())
                                   .ToList();

                    if (values.Count == 0) return false;

                    // Validate each value
                    foreach (var value in values)
                    {
                        if (!double.TryParse(value, out double num))
                        {
                            _logger.LogWarning("Valor inválido encontrado: {Value}", value);
                            return false;
                        }

                        // Check for reasonable range (adjust as needed)
                        if (num < -1000 || num > 1000)
                        {
                            _logger.LogWarning("Valor fora do intervalo aceitável: {Value}", num);
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro ao validar dados: {Error}", ex.Message);
                return false;
            }
        }
    }
} 