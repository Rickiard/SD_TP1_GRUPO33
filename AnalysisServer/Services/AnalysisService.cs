using Grpc.Core;
using AnalysisServer.Protos;
using System.Threading.Tasks;

namespace AnalysisServer.Services
{
    public class AnalysisServiceImpl : AnalysisService.AnalysisServiceBase
    {
        public override Task<AnalysisResponse> AnalyzeData(AnalysisRequest request, ServerCallContext context)
        {
            // Exemplo de análise: contar número de linhas recebidas
            int numLinhas = request.ProcessedData.Split('\n').Length;
            string resultado = $"Linhas recebidas: {numLinhas}";
            return Task.FromResult(new AnalysisResponse
            {
                WavyId = request.WavyId,
                AnalysisResult = resultado,
                Success = true,
                Message = "Análise concluída."
            });
        }
    }
} 