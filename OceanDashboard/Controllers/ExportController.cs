using Microsoft.AspNetCore.Mvc;
using OceanDashboard.Models;
using System.Text;
using CsvHelper;
using System.Globalization;

namespace OceanDashboard.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExportController : ControllerBase
    {
        private readonly ILogger<ExportController> _logger;
        private readonly Services.OceanServerDbHelper _oceanServerDbHelper;
        private readonly string _connectionString;

        public ExportController(ILogger<ExportController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            
            // Encontrar caminho do banco de dados
            var dbPath = ResolveDbPath(env);
            _connectionString = $"Data Source={dbPath}";
            
            _logger.LogInformation("Usando conexão de banco de dados para exportação: {ConnectionString}", _connectionString);
            
            // Inicializar helper para banco de dados
            _oceanServerDbHelper = new Services.OceanServerDbHelper(_connectionString, _logger);
        }
        
        private string ResolveDbPath(IWebHostEnvironment env)
        {
            // Priorizar o caminho do OceanServer para a dados_recebidos.db
            var oceanServerPaths = new List<string>
            {
                Path.Combine(env.ContentRootPath, "..", "OceanServer", "dados_recebidos.db"),
                @"c:\Users\srric\OneDrive\Ambiente de Trabalho\SD\TP1 - Repositório\OceanServer\dados_recebidos.db",
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "OceanServer", "dados_recebidos.db"))
            };
            
            // Verificar se o banco de dados existe no OceanServer
            foreach (var path in oceanServerPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
            }
            
            // Caminhos de fallback
            var fallbackPaths = new List<string>
            {
                Path.Combine(env.ContentRootPath, "..", "dados_recebidos.db"),
                Path.Combine(Directory.GetCurrentDirectory(), "dados_recebidos.db")
            };
            
            foreach (var path in fallbackPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
            }
            
            return oceanServerPaths[0];
        }
          [HttpGet("csv")]
        [Route("ExportToCsv")]
        public async Task<IActionResult> ExportToCsv(
            [FromQuery] DateTime? startDate, 
            [FromQuery] DateTime? endDate, 
            [FromQuery] string stationId = "all")
        {
            try
            {
                _logger.LogInformation("Iniciando exportação de dados para CSV. Período: {StartDate} a {EndDate}, Estação: {StationId}",
                    startDate, endDate, stationId);
                
                // Definir período padrão se não especificado
                if (!startDate.HasValue)
                    startDate = DateTime.Now.AddDays(-7);
                    
                if (!endDate.HasValue)
                    endDate = DateTime.Now;
                
                // Buscar dados no banco
                var data = GetOceanDataFromDb(startDate.Value, endDate.Value, stationId);
                
                if (!data.Any())
                {
                    _logger.LogWarning("Nenhum dado encontrado para exportação com os filtros especificados");
                    return NotFound("Nenhum dado encontrado para o período e estação selecionados");
                }
                
                // Gerar CSV em memória
                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                
                await csv.WriteRecordsAsync(data);
                await writer.FlushAsync();
                
                memoryStream.Position = 0;
                
                _logger.LogInformation("Exportação de {Count} registros para CSV concluída com sucesso", data.Count);
                
                // Retornar arquivo para download
                var fileName = $"ocean_data_{startDate.Value:yyyyMMdd}_{endDate.Value:yyyyMMdd}_{stationId}.csv";
                return File(memoryStream.ToArray(), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao exportar dados para CSV");
                return StatusCode(500, $"Erro ao exportar dados: {ex.Message}");
            }
        }
          [HttpGet("json")]
        [Route("ExportToJson")]
        public IActionResult ExportToJson(
            [FromQuery] DateTime? startDate, 
            [FromQuery] DateTime? endDate, 
            [FromQuery] string stationId = "all")
        {
            try
            {
                _logger.LogInformation("Iniciando exportação de dados para JSON. Período: {StartDate} a {EndDate}, Estação: {StationId}",
                    startDate, endDate, stationId);
                
                // Definir período padrão se não especificado
                if (!startDate.HasValue)
                    startDate = DateTime.Now.AddDays(-7);
                    
                if (!endDate.HasValue)
                    endDate = DateTime.Now;
                
                // Buscar dados no banco
                var data = GetOceanDataFromDb(startDate.Value, endDate.Value, stationId);
                
                if (!data.Any())
                {
                    _logger.LogWarning("Nenhum dado encontrado para exportação com os filtros especificados");
                    return NotFound("Nenhum dado encontrado para o período e estação selecionados");
                }
                
                _logger.LogInformation("Exportação de {Count} registros para JSON concluída com sucesso", data.Count);
                
                // Retornar arquivo JSON para download
                var fileName = $"ocean_data_{startDate.Value:yyyyMMdd}_{endDate.Value:yyyyMMdd}_{stationId}.json";
                return new JsonResult(data) 
                { 
                    StatusCode = 200,
                    ContentType = "application/json",
                    SerializerSettings = new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao exportar dados para JSON");
                return StatusCode(500, $"Erro ao exportar dados: {ex.Message}");
            }
        }
        
        private List<OceanData> GetOceanDataFromDb(DateTime startDate, DateTime endDate, string stationId)
        {
            var result = new List<OceanData>();
            
            // Verificar formato do banco de dados e buscar dados adequadamente
            if (_oceanServerDbHelper.HasOceanServerFormat())
            {
                result = _oceanServerDbHelper.GetDataFromOceanServer(startDate, endDate, stationId);
            }
            else
            {
                // Fallback para outro formato de banco se necessário
                _logger.LogWarning("Formato do banco de dados não reconhecido como OceanServer");
            }
            
            return result;
        }
    }
}
