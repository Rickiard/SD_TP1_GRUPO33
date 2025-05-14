using Grpc.Core;
using Preprocessing;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace PreprocessingServer.Services
{
    public class PreprocessingServiceImpl : PreprocessingService.PreprocessingServiceBase
    {
        private readonly ILogger<PreprocessingServiceImpl> _logger;

        public PreprocessingServiceImpl(ILogger<PreprocessingServiceImpl> logger)
        {
            _logger = logger;
        }

        public override Task<PreprocessResponse> PreprocessData(PreprocessRequest request, ServerCallContext context)
        {
            var (preProcessType, volume, server) = DatabaseHelper.GetPreprocessingConfig(request.WavyId);

            string processedData = request.RawData;
            string message = $"Tipo: {preProcessType}, Volume: {volume}, Servidor: {server}";

            // Exemplo de lógica de pré-processamento
            if (preProcessType == "filtragem")
                processedData = processedData.Replace("ruido", "");
            else if (preProcessType == "agregacao")
                processedData = processedData.ToUpper();
            else if (preProcessType == "normalizacao")
                processedData = processedData.ToLower();

            _logger.LogInformation("Processamento {Tipo} para Wavy ID: {WavyId}", preProcessType, request.WavyId);

            var response = new PreprocessResponse
            {
                Success = true,
                Message = message,
                ProcessedData = processedData
            };

            return Task.FromResult(response);
        }
    }
} 