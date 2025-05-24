using Microsoft.AspNetCore.Mvc;
using OceanDashboard.Models;
using OceanDashboard.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OceanDashboard.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly ILogger<AnalyticsController> _logger;
        private readonly HomeController _homeController;
        private readonly DataAnalysisServiceClient _dataAnalysisClient;

        public AnalyticsController(
            ILogger<AnalyticsController> logger, 
            HomeController homeController,
            DataAnalysisServiceClient dataAnalysisClient)
        {
            _logger = logger;
            _homeController = homeController;
            _dataAnalysisClient = dataAnalysisClient;
        }        [HttpGet("detect-patterns")]
        public async Task<IActionResult> DetectPatterns(
            string patternType = "all", 
            string dataField = "wave_height", 
            string timeRange = "24h", 
            string stationId = "all",
            int windowSize = 10)
        {
            try
            {
                _logger.LogInformation("Iniciando detecção de padrões: tipo={PatternType}, campo={DataField}, " +
                    "período={TimeRange}, estação={StationId}, janela={WindowSize}",
                    patternType, dataField, timeRange, stationId, windowSize);

                // Buscar os dados do oceano para o período especificado
                var oceanData = _homeController.GetLatestOceanData(timeRange, stationId, "raw");
                
                if (oceanData == null || oceanData.Count == 0)
                {
                    _logger.LogWarning("Nenhum dado encontrado para análise de padrões");
                    return BadRequest("Nenhum dado disponível para o período e localização especificados");
                }

                // Chamar o serviço de detecção de padrões
                var result = await _dataAnalysisClient.DetectPatternsAsync(
                    oceanData, 
                    patternType, 
                    dataField, 
                    windowSize
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao detectar padrões");
                return StatusCode(500, new { error = "Erro ao processar a detecção de padrões", details = ex.Message });
            }
        }

        [HttpGet("analyze")]
        public async Task<IActionResult> AnalyzeData(
            string analysisType = "all",
            string timeRange = "24h",
            string location = "all")
        {
            try
            {
                _logger.LogInformation("Iniciando análise de dados: tipo={AnalysisType}, período={TimeRange}, local={Location}",
                    analysisType, timeRange, location);

                // Buscar os dados do oceano para o período especificado
                var oceanData = _homeController.GetLatestOceanData(timeRange, location, "raw");
                
                if (oceanData == null || oceanData.Count == 0)
                {
                    _logger.LogWarning("Nenhum dado encontrado para análise");
                    return BadRequest("Nenhum dado disponível para o período e localização especificados");
                }

                // Chamar o serviço de análise de dados
                var result = await _dataAnalysisClient.AnalyzeDataAsync(
                    oceanData,
                    analysisType,
                    timeRange,
                    location
                );

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao analisar dados");
                return StatusCode(500, new { error = "Erro ao processar a análise de dados", details = ex.Message });
            }
        }
    }
}
