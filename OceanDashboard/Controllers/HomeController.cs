using Microsoft.AspNetCore.Mvc;
using OceanDashboard.Models;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.IO;

namespace OceanDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _connectionString;
        private readonly string _dbPath;
        private readonly Services.OceanServerDbHelper _oceanServerDbHelper;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            
            // Tentar encontrar o banco de dados em vários locais possíveis,
            // priorizando o arquivo dados_recebidos.db do OceanServer
            _dbPath = ResolveDbPath(env);
            _connectionString = $"Data Source={_dbPath}";
            
            _logger.LogInformation("Usando conexão de banco de dados: {ConnectionString}", _connectionString);
            _logger.LogInformation("Caminho do banco de dados: {DbPath}", _dbPath);
            
            // Create helper for working with OceanServer database format
            _oceanServerDbHelper = new Services.OceanServerDbHelper(_connectionString, _logger);
            
            InitializeDatabase();
        }

        private string ResolveDbPath(IWebHostEnvironment env)
        {
            // Priorizar o caminho do OceanServer para a dados_recebidos.db
            var oceanServerPaths = new List<string>
            {
                Path.Combine(env.ContentRootPath, "..", "OceanServer", "dados_recebidos.db"),
                @"c:\Users\srric\OneDrive\Ambiente de Trabalho\SD\TP1 - Repositório\OceanServer\dados_recebidos.db",
                // Adicionar o caminho absoluto para garantir
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "OceanServer", "dados_recebidos.db"))
            };
            
            // Verificar se o banco de dados existe no OceanServer
            foreach (var path in oceanServerPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    _logger.LogInformation("Banco de dados do OceanServer encontrado em: {DbPath}", path);
                    return path;
                }
                else
                {
                    _logger.LogInformation("Banco de dados do OceanServer não encontrado em: {DbPath}", path);
                }
            }
            
            // Caminhos de fallback se o banco de dados do OceanServer não for encontrado
            var fallbackPaths = new List<string>
            {
                Path.Combine(env.ContentRootPath, "..", "dados_recebidos.db"),
                Path.Combine(Directory.GetCurrentDirectory(), "dados_recebidos.db"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dados_recebidos.db"),
                @"c:\Users\srric\OneDrive\Ambiente de Trabalho\SD\TP1 - Repositório\dados_recebidos.db"
            };
            
            // Verificar cada caminho de fallback
            foreach (var path in fallbackPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    _logger.LogInformation("Banco de dados encontrado em caminho de fallback: {DbPath}", path);
                    return path;
                }
                else
                {
                    _logger.LogInformation("Banco de dados não encontrado em: {DbPath}", path);
                }
            }
            
            // Se não encontrar em nenhum lugar, criar no local do OceanServer
            _logger.LogWarning("Banco de dados não encontrado. Criando novo no caminho do OceanServer: {DbPath}", oceanServerPaths[0]);
            return oceanServerPaths[0];
        }

        private void InitializeDatabase()
        {
            try
            {
                // Verificar se o diretório existe
                var directory = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Criar conexão e tabela
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();

                    _logger.LogInformation("Conexão com banco de dados aberta com sucesso");

                    // Criar tabela se não existir
                    var command = connection.CreateCommand();                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ocean_data (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            sensor_id TEXT,
                            longitude REAL,
                            latitude REAL,
                            timestamp DATETIME NOT NULL,
                            atmosphere_mb REAL,
                            wind_direction_degrees REAL,
                            wind_speed_kn REAL,
                            gust_kn REAL,
                            wave_height_m REAL NOT NULL,
                            wave_period_s REAL NOT NULL,
                            wave_direction_degrees REAL NOT NULL,
                            hmax_m REAL,
                            air_temperature_c REAL,
                            dew_point_c REAL,
                            sea_temperature_c REAL NOT NULL,
                            relative_humidity_percent REAL,
                            qc_flag INTEGER,
                            station_id TEXT
                        )";
                    command.ExecuteNonQuery();
                    _logger.LogInformation("Tabela ocean_data verificada/criada com sucesso");

                    // Inserir dados de exemplo se a tabela estiver vazia
                    command.CommandText = "SELECT COUNT(*) FROM ocean_data";
                    var count = Convert.ToInt32(command.ExecuteScalar());
                    _logger.LogInformation("Contagem de registros no banco de dados: {Count}", count);

                    if (count == 0)
                    {
                        _logger.LogInformation("Inserindo dados de exemplo no banco de dados");
                        var random = new Random();
                        var baseTime = DateTime.Now.AddHours(-24);
                        
                        for (int i = 0; i < 100; i++)
                        {
                            command.CommandText = @"
                                INSERT INTO ocean_data (
                                    sensor_id, longitude, latitude, timestamp, atmosphere_mb, 
                                    wind_direction_degrees, wind_speed_kn, gust_kn, 
                                    wave_height_m, wave_period_s, wave_direction_degrees, hmax_m,
                                    air_temperature_c, dew_point_c, sea_temperature_c, relative_humidity_percent,
                                    qc_flag, station_id, location)
                                VALUES (
                                    $sensor_id, $longitude, $latitude, $timestamp, $atmosphere_mb,
                                    $wind_direction, $wind_speed, $gust, 
                                    $wave_height, $wave_period, $wave_direction, $hmax,
                                    $air_temp, $dew_point, $sea_temp, $humidity,
                                    $qc_flag, $station_id, $location)";

                            command.Parameters.Clear();
                            var stationId = "BUOY_" + i % 3;
                            var timestamp = baseTime.AddMinutes(i * 15);
                            var location = "Oceano Atlântico";
                            
                            command.Parameters.AddWithValue("$sensor_id", "SENSOR_" + (1000 + i));
                            command.Parameters.AddWithValue("$longitude", random.NextDouble() * 10 - 5); // -5 a 5
                            command.Parameters.AddWithValue("$latitude", random.NextDouble() * 10 + 35); // 35 a 45
                            command.Parameters.AddWithValue("$timestamp", timestamp.ToString("o"));
                            command.Parameters.AddWithValue("$atmosphere_mb", random.NextDouble() * 30 + 990); // 990-1020mb
                            command.Parameters.AddWithValue("$wind_direction", random.NextDouble() * 360); // 0-360°
                            command.Parameters.AddWithValue("$wind_speed", random.NextDouble() * 30); // 0-30 nós
                            command.Parameters.AddWithValue("$gust", random.NextDouble() * 15 + 30); // 30-45 nós
                            command.Parameters.AddWithValue("$wave_height", random.NextDouble() * 3 + 1); // 1-4m
                            command.Parameters.AddWithValue("$wave_period", random.NextDouble() * 8 + 4); // 4-12s
                            command.Parameters.AddWithValue("$wave_direction", random.NextDouble() * 360); // 0-360°
                            command.Parameters.AddWithValue("$hmax", random.NextDouble() * 5 + 2); // 2-7m
                            command.Parameters.AddWithValue("$air_temp", random.NextDouble() * 15 + 10); // 10-25°C
                            command.Parameters.AddWithValue("$dew_point", random.NextDouble() * 5 + 5); // 5-10°C
                            command.Parameters.AddWithValue("$sea_temp", random.NextDouble() * 10 + 15); // 15-25°C
                            command.Parameters.AddWithValue("$humidity", random.NextDouble() * 30 + 60); // 60-90%
                            command.Parameters.AddWithValue("$qc_flag", random.Next(0, 4)); // 0-3
                            command.Parameters.AddWithValue("$station_id", stationId);
                            command.Parameters.AddWithValue("$location", location);

                            command.ExecuteNonQuery();
                        }
                        _logger.LogInformation("Dados de exemplo inseridos com sucesso");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao inicializar banco de dados");
            }
        }

        public async Task<IActionResult> Index(string timeRange = "24h", string stationId = "all", string resolution = "hour", string analysisType = "all")
        {
            // Create and populate dashboard view model
            var viewModel = new DashboardViewModel
            {
                TimeRange = timeRange,
                StationId = stationId,
                Resolution = resolution,
                AnalysisType = analysisType
            };
            
            // Get ocean data based on filters
            viewModel.OceanData = GetLatestOceanData(timeRange, stationId, resolution);
            
            // If we don't have any data, try to create sample data
            if (viewModel.OceanData.Count == 0)
            {
                var endDate = DateTime.Now;
                var startDate = endDate.AddHours(-24);
                _logger.LogInformation("No data found for default view, inserting sample data");
                InsertDefaultData(startDate, endDate);
                viewModel.OceanData = GetLatestOceanData(timeRange, stationId, resolution);
            }
            
            // Get available stations for filter dropdown
            viewModel.AvailableStations = GetAvailableStations();
            
            // Calculate summary statistics
            viewModel.CalculateSummary();
            
            // Get pattern analysis if requested
            if (analysisType != "none")
            {
                try
                {
                    var dataAnalysisClient = HttpContext.RequestServices.GetRequiredService<Services.DataAnalysisServiceClient>();
                    string fieldName = "wave_height";
                    
                    switch (analysisType)
                    {
                        case "wave":
                            fieldName = "wave_height_m";
                            break;
                        case "wind":
                            fieldName = "wind_speed_kn";
                            break;
                        case "temperature":
                            fieldName = "sea_temperature_c";
                            break;
                    }
                    
                    viewModel.PatternAnalysisResult = await dataAnalysisClient.DetectPatternsAsync(
                        viewModel.OceanData,
                        "all",
                        fieldName,
                        24
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error connecting to Data Analysis Service");
                }
            }
            
            return View(viewModel);        }        [HttpGet]
        public IActionResult TestData()
        {
            var testData = new List<object>
            {
                new {
                    id = 1,
                    timestamp = DateTime.Now.AddHours(-1).ToString("o"),
                    waveHeight = 2.5,
                    waveDirection = 180.0,
                    seaTemperature = 18.5,
                    windSpeed = 15.2
                },
                new {
                    id = 2,
                    timestamp = DateTime.Now.AddHours(-2).ToString("o"),
                    waveHeight = 2.8,
                    waveDirection = 185.0,
                    seaTemperature = 18.3,
                    windSpeed = 14.8
                }
            };
            
            return Json(testData);
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult GetLatestData(string timeRange = "24h", string location = "all", string resolution = "hour")
        {
            _logger.LogInformation("=== INÍCIO GetLatestData ===");
            _logger.LogInformation("Parâmetros: timeRange={TimeRange}, location={Location}, resolution={Resolution}", 
                timeRange, location, resolution);
            
            // Adicionar logs detalhados sobre detecção de formato de banco
            _logger.LogInformation("Caminho do banco de dados: {DbPath}", _dbPath);
            _logger.LogInformation("Verificando formato do banco de dados...");
            
            bool hasOceanServerFormat = _oceanServerDbHelper.HasOceanServerFormat();
            _logger.LogInformation("HasOceanServerFormat retornou: {HasOceanServerFormat}", hasOceanServerFormat);
            
            if (hasOceanServerFormat)
            {
                _logger.LogInformation("USANDO FORMATO OCEANSERVER - tabela dados_wavy");
            }
            else
            {
                _logger.LogInformation("USANDO FORMATO PADRÃO - tabela ocean_data");
            }
            
            var oceanData = GetLatestOceanData(timeRange, location, resolution);
            
            _logger.LogInformation("GetLatestOceanData retornou {Count} registros", oceanData?.Count ?? 0);
            _logger.LogInformation("=== FIM GetLatestData ===");
            
            return Json(oceanData);
        }

        public List<OceanData> GetLatestOceanData(string timeRange = "24h", string location = "all", string resolution = "hour")
        {
            // Determinar o intervalo de tempo para a consulta
            DateTime startDate;
            DateTime endDate = DateTime.Now;
            
            switch (timeRange)
            {
                case "1h":
                    startDate = endDate.AddHours(-1);
                    break;
                case "6h":
                    startDate = endDate.AddHours(-6);
                    break;
                case "7d":
                    startDate = endDate.AddDays(-7);
                    break;
                case "30d":
                    startDate = endDate.AddDays(-30);
                    break;
                case "24h":
                default:
                    startDate = endDate.AddHours(-24);
                    break;
            }
            
            // Get data from database
            var data = GetDataFromDatabase(startDate, endDate, location);
              
            // Check if we have any data before applying resolution
            if (data.Count == 0)
            {
                _logger.LogWarning("No ocean data found for the specified criteria: timeRange={TimeRange}, location={Location}, timeRange from {StartDate} to {EndDate}", 
                    timeRange, location, startDate.ToString("o"), endDate.ToString("o"));
                
                // If no data is found, insert some default data for the requested time period
                InsertDefaultData(startDate, endDate, location);
                
                // Try again after inserting default data
                data = GetDataFromDatabase(startDate, endDate, location);
                
                if (data.Count == 0)
                {
                    _logger.LogError("Still no data after inserting defaults. Database may be inaccessible or corrupted.");
                    return data;
                }
                
                _logger.LogInformation("Successfully inserted and retrieved default data for display");
            }
            
            // Aplicar resolução dos dados (agrupamento)
            data = ApplyDataResolution(data, resolution);

            return data;
        }
        
        private List<OceanData> ApplyDataResolution(List<OceanData> rawData, string resolution)
        {
            // Se for dados brutos ou não tiver dados suficientes, retornar os dados originais
            if (resolution == "raw" || rawData == null || rawData.Count <= 1)
            {
                _logger.LogInformation($"Resolução 'raw' ou dados insuficientes: {rawData?.Count ?? 0} registos devolvidos.");
                return rawData ?? new List<OceanData>();
            }

            var processedData = new List<OceanData>();
            var groupedData = new Dictionary<(DateTime, string), List<OceanData>>();

            // Determinar o formato de agrupamento com base na resolução
            Func<DateTime, DateTime> getGroupKey;

            switch (resolution)
            {
                case "minute":
                    getGroupKey = (dt) => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0);
                    break;
                case "hour":
                    getGroupKey = (dt) => new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0);
                    break;
                case "day":
                    getGroupKey = (dt) => new DateTime(dt.Year, dt.Month, dt.Day);
                    break;
                default:
                    _logger.LogWarning($"Resolução desconhecida '{resolution}', a devolver dados originais.");
                    return rawData;
            }            // Agrupar dados por timestamp e estação
            foreach (var item in rawData)
            {
                var key = (getGroupKey(item.Timestamp), item.StationId);
                if (!groupedData.ContainsKey(key))
                {
                    groupedData[key] = new List<OceanData>();
                }
                groupedData[key].Add(item);
            }

            _logger.LogInformation($"Agrupamento para resolução '{resolution}': {groupedData.Count} grupos encontrados.");

            // Calcular médias para cada grupo
            foreach (var group in groupedData.OrderByDescending(g => g.Key))
            {                var groupData = group.Value;
                if (groupData.Any())
                {
                    // Criar um novo OceanData com valores agregados
                    var aggregatedData = new OceanData
                    {
                        Timestamp = group.Key.Item1,
                        Id = groupData[0].Id, // Usar o ID do primeiro registro
                        
                        // Usar médias para os valores numéricos
                        WaveHeight = groupData.Average(d => d.WaveHeight),
                        WavePeriod = groupData.Average(d => d.WavePeriod),
                        WaveDirection = groupData.Average(d => d.WaveDirection),
                        
                        // Adicionar todos os campos do formato SensorData
                        AtmospherePressure = groupData.Average(d => d.AtmospherePressure),
                        WindDirection = groupData.Average(d => d.WindDirection),
                        WindSpeed = groupData.Average(d => d.WindSpeed),
                        Gust = groupData.Average(d => d.Gust),
                        MaxWaveHeight = groupData.Average(d => d.MaxWaveHeight),
                        AirTemperature = groupData.Average(d => d.AirTemperature),
                        DewPoint = groupData.Average(d => d.DewPoint), 
                        SeaTemperature = groupData.Average(d => d.SeaTemperature),
                        RelativeHumidity = groupData.Average(d => d.RelativeHumidity),
                        
                        // Para campos não numéricos, usar o valor mais frequente ou o primeiro
                        SensorId = groupData.GroupBy(d => d.SensorId)
                            .OrderByDescending(g => g.Count())
                            .Select(g => g.Key)
                            .FirstOrDefault() ?? "",
                            
                        StationId = groupData.GroupBy(d => d.StationId)
                            .OrderByDescending(g => g.Count())
                            .Select(g => g.Key)
                            .FirstOrDefault() ?? "",
                            
                        // Para coordenadas, usar a média
                        Longitude = groupData.Average(d => d.Longitude),
                        Latitude = groupData.Average(d => d.Latitude),
                        
                        // Para a flag de qualidade, usar o modo (valor mais frequente)
                        QcFlag = groupData.GroupBy(d => d.QcFlag)
                            .OrderByDescending(g => g.Count())
                            .Select(g => g.Key)
                            .FirstOrDefault()
                    };
                    
                    // Add to processed data list
                    processedData.Add(aggregatedData);
                }
            }

            _logger.LogInformation($"Resolução '{resolution}': {processedData.Count} pontos devolvidos após agrupamento.");
            return processedData;
        }        // Helper method to get data from database with specific date range and location
        private List<OceanData> GetDataFromDatabase(DateTime startDate, DateTime endDate, string location)
        {
            var data = new List<OceanData>();
            
            try 
            {
                _logger.LogInformation("=== INÍCIO GetDataFromDatabase ===");
                _logger.LogInformation("Obtendo dados do período {StartDate} até {EndDate}, local: {Location}", 
                    startDate.ToString("o"), endDate.ToString("o"), location);
                
                if (!System.IO.File.Exists(_dbPath))
                {
                    _logger.LogWarning("Arquivo de banco de dados não encontrado em: {DbPath}", _dbPath);
                    return data;
                }
                
                // First check if we're using OceanServer format
                bool usingOceanServerFormat = _oceanServerDbHelper.HasOceanServerFormat();
                _logger.LogInformation("Verificação de formato OceanServer: {UsingOceanServerFormat}", usingOceanServerFormat);
                
                if (usingOceanServerFormat)
                {
                    _logger.LogInformation("EXECUTANDO: GetDataFromOceanServer (tabela dados_wavy)");
                    var oceanServerData = _oceanServerDbHelper.GetDataFromOceanServer(startDate, endDate, location);
                    _logger.LogInformation("GetDataFromOceanServer retornou {Count} registros", oceanServerData?.Count ?? 0);
                    _logger.LogInformation("=== FIM GetDataFromDatabase (OceanServer) ===");
                    return oceanServerData ?? new List<OceanData>();
                }
                
                // If not OceanServer format, use the ocean_data table
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    
                    // Verificar quais colunas existem na tabela
                    var columnQuery = "PRAGMA table_info(ocean_data)";
                    var columnCmd = new SqliteCommand(columnQuery, connection);
                    var columns = new List<string>();
                    using (var reader = columnCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columns.Add(reader.GetString(1)); // Column name is at index 1
                        }
                    }
                    
                    _logger.LogInformation("Colunas na tabela ocean_data: {0}", string.Join(", ", columns));
                      // Verificar se estamos usando o esquema antigo ou o novo
                    bool isLegacySchema = !columns.Contains("sea_temperature_c") && 
                                        !columns.Contains("wave_height_m") && 
                                        columns.Contains("temperature");
                      string selectFields;
                    if (isLegacySchema)
                    {
                        selectFields = "id, timestamp, wave_height, wave_period, wave_direction, temperature, location";
                    }
                    else 
                    {                        selectFields = @"id, COALESCE(sensor_id, '') as sensor_id, 
                            COALESCE(longitude, 0) as longitude, 
                            COALESCE(latitude, 0) as latitude, 
                            timestamp, 
                            COALESCE(atmosphere_mb, 0) as atmosphere_mb, 
                            COALESCE(wind_direction_degrees, 0) as wind_direction_degrees, 
                            COALESCE(wind_speed_kn, 0) as wind_speed_kn, 
                            COALESCE(gust_kn, 0) as gust_kn, 
                            COALESCE(wave_height_m, 0) as wave_height, 
                            COALESCE(wave_period_s, 0) as wave_period, 
                            COALESCE(wave_direction_degrees, 0) as wave_direction,
                            COALESCE(hmax_m, 0) as hmax_m, 
                            COALESCE(air_temperature_c, 0) as air_temperature,
                            COALESCE(dew_point_c, 0) as dew_point, 
                            COALESCE(sea_temperature_c, 0) as sea_temperature, 
                            COALESCE(relative_humidity_percent, 0) as humidity,
                            COALESCE(qc_flag, 0) as qc_flag, 
                            COALESCE(station_id, '') as station_id";
                    }
                      var sql = $"SELECT {selectFields} FROM ocean_data " +
                            "WHERE datetime(timestamp) >= datetime($startDate) AND datetime(timestamp) <= datetime($endDate)";
                    
                    if (location != "all")
                    {
                        sql += " AND station_id = $stationId";
                    }
                    
                    // Adicionar ordenação
                    sql += " ORDER BY timestamp DESC";
                    
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("$startDate", startDate.ToString("o"));
                    command.Parameters.AddWithValue("$endDate", endDate.ToString("o"));
                      if (location != "all")
                    {
                        command.Parameters.AddWithValue("$stationId", location);
                    }
                    
                    _logger.LogInformation("Executando consulta SQL: {Sql}", sql);
                    _logger.LogInformation("Parâmetros: startDate={StartDate}, endDate={EndDate}", 
                        startDate.ToString("o"), endDate.ToString("o"));

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try 
                            {                                if (isLegacySchema)
                                {
                                    // Lendo no formato antigo
                                    data.Add(new OceanData
                                    {
                                        Id = reader.GetInt32(0),
                                        Timestamp = DateTime.Parse(reader.GetString(1)),
                                        WaveHeight = reader.GetDouble(2),
                                        WavePeriod = reader.GetDouble(3),
                                        WaveDirection = reader.GetDouble(4),
                                        SeaTemperature = reader.GetDouble(5)
                                    });
                                }
                                else
                                {
                                    // Lendo no formato novo
                                    data.Add(new OceanData
                                    {
                                        Id = reader.GetInt32(0),
                                        SensorId = reader.GetString(1),
                                        Longitude = reader.GetDouble(2),
                                        Latitude = reader.GetDouble(3),
                                        Timestamp = DateTime.Parse(reader.GetString(4)),
                                        AtmospherePressure = reader.GetDouble(5),
                                        WindDirection = reader.GetDouble(6),
                                        WindSpeed = reader.GetDouble(7),
                                        Gust = reader.GetDouble(8),
                                        WaveHeight = reader.GetDouble(9),
                                        WavePeriod = reader.GetDouble(10),
                                        WaveDirection = reader.GetDouble(11),
                                        MaxWaveHeight = reader.GetDouble(12),
                                        AirTemperature = reader.GetDouble(13),
                                        DewPoint = reader.GetDouble(14),
                                        SeaTemperature = reader.GetDouble(15),
                                        RelativeHumidity = reader.GetDouble(16),
                                        QcFlag = reader.GetInt32(17),                                        StationId = reader.GetString(18)
                                    });
                                }
                            }
                            catch (Exception ex) 
                            {
                                _logger.LogError(ex, "Erro ao ler registro: [0]={V0}, [1]={V1}, [2]={V2}, [3]={V3}, [4]={V4}, [5]={V5}, [6]={V6}",
                                    reader[0], reader[1], reader[2], reader[3], reader[4], reader[5], reader[6]);
                            }
                        }
                    }
                    
                    _logger.LogInformation("Consulta finalizada. {Count} registros encontrados", data.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar dados do banco de dados");
            }
            
            return data;
        }

        // Insert default data if no data is found
        private void InsertDefaultData(DateTime startDate, DateTime endDate, string location = "Oceano Atlântico")
        {
            try
            {
                _logger.LogInformation("Inserindo dados padrão para o período {StartDate} até {EndDate}, local: {Location}", 
                    startDate.ToString("o"), endDate.ToString("o"), location);
                
                // Garantir que temos um banco de dados válido
                if (!System.IO.File.Exists(_dbPath))
                {
                    var directory = Path.GetDirectoryName(_dbPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    _logger.LogInformation("Criando novo arquivo de banco de dados em: {DbPath}", _dbPath);
                }
                
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Garantir que a tabela exista com todos os campos do SensorData
                    var createTableCommand = connection.CreateCommand();
                    createTableCommand.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ocean_data (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            sensor_id TEXT,
                            longitude REAL,
                            latitude REAL,
                            timestamp DATETIME NOT NULL,
                            atmosphere_mb REAL,
                            wind_direction_degrees REAL,
                            wind_speed_kn REAL,
                            gust_kn REAL,
                            wave_height_m REAL NOT NULL,
                            wave_period_s REAL NOT NULL,
                            wave_direction_degrees REAL NOT NULL,
                            hmax_m REAL,
                            air_temperature_c REAL,
                            dew_point_c REAL,
                            sea_temperature_c REAL NOT NULL,
                            relative_humidity_percent REAL,
                            qc_flag INTEGER,
                            station_id TEXT,
                            location TEXT NOT NULL
                        )";
                    createTableCommand.ExecuteNonQuery();
                    
                    var transaction = connection.BeginTransaction();
                    
                    try
                    {
                        // Calcular um intervalo de tempo apropriado
                        var random = new Random();
                        var totalMinutes = (endDate - startDate).TotalMinutes;
                        var desiredPoints = Math.Min(100, Math.Max(10, totalMinutes / 15)); // Entre 10 e 100 pontos
                        var interval = Math.Max(15, totalMinutes / desiredPoints); // No mínimo 15 minutos entre pontos
                        
                        _logger.LogInformation("Gerando {Count} pontos de dados com intervalo de {Interval} minutos", 
                            desiredPoints, interval);
                        
                        for (var time = startDate; time <= endDate; time = time.AddMinutes(interval))
                        {
                            // Criar dados de exemplo baseados no formato completo do SensorData
                            var sensorIdNumber = random.Next(1000, 9999);
                            var sensorId = $"SENSOR_{sensorIdNumber}";
                            var stationId = $"BUOY_{sensorIdNumber % 5}";
                            var longitude = random.NextDouble() * 10 - 5; // -5 a 5
                            var latitude = random.NextDouble() * 10 + 35; // 35 a 45
                            var atmospherePressure = random.NextDouble() * 30 + 990; // 990-1020mb
                            var windDirection = random.NextDouble() * 360; // 0-360°
                            var windSpeed = random.NextDouble() * 30; // 0-30 nós
                            var gust = random.NextDouble() * 15 + 30; // 30-45 nós
                            var waveHeight = random.NextDouble() * 3 + 1; // 1-4m
                            var wavePeriod = random.NextDouble() * 8 + 4; // 4-12s
                            var waveDirection = random.NextDouble() * 360; // 0-360°
                            var maxWaveHeight = random.NextDouble() * 5 + 2; // 2-7m
                            var airTemperature = random.NextDouble() * 15 + 10; // 10-25°C
                            var dewPoint = random.NextDouble() * 5 + 5; // 5-10°C
                            var seaTemperature = random.NextDouble() * 10 + 15; // 15-25°C
                            var humidity = random.NextDouble() * 30 + 60; // 60-90%
                            var qcFlag = random.Next(0, 4); // 0-3
                            
                            var command = connection.CreateCommand();
                            command.CommandText = @"
                                INSERT INTO ocean_data (
                                    sensor_id, longitude, latitude, timestamp, atmosphere_mb, 
                                    wind_direction_degrees, wind_speed_kn, gust_kn, 
                                    wave_height_m, wave_period_s, wave_direction_degrees, hmax_m,
                                    air_temperature_c, dew_point_c, sea_temperature_c, relative_humidity_percent,
                                    qc_flag, station_id, location)
                                VALUES (
                                    $sensor_id, $longitude, $latitude, $timestamp, $atmosphere_mb,
                                    $wind_direction, $wind_speed, $gust, 
                                    $wave_height, $wave_period, $wave_direction, $hmax,
                                    $air_temp, $dew_point, $sea_temp, $humidity,
                                    $qc_flag, $station_id, $location)";

                            command.Parameters.AddWithValue("$sensor_id", sensorId);
                            command.Parameters.AddWithValue("$longitude", longitude);
                            command.Parameters.AddWithValue("$latitude", latitude);
                            command.Parameters.AddWithValue("$timestamp", time.ToString("o"));
                            command.Parameters.AddWithValue("$atmosphere_mb", atmospherePressure);
                            command.Parameters.AddWithValue("$wind_direction", windDirection);
                            command.Parameters.AddWithValue("$wind_speed", windSpeed);
                            command.Parameters.AddWithValue("$gust", gust);
                            command.Parameters.AddWithValue("$wave_height", waveHeight);
                            command.Parameters.AddWithValue("$wave_period", wavePeriod);
                            command.Parameters.AddWithValue("$wave_direction", waveDirection);
                            command.Parameters.AddWithValue("$hmax", maxWaveHeight);
                            command.Parameters.AddWithValue("$air_temp", airTemperature);
                            command.Parameters.AddWithValue("$dew_point", dewPoint);                            command.Parameters.AddWithValue("$sea_temp", seaTemperature);
                            command.Parameters.AddWithValue("$humidity", humidity);
                            command.Parameters.AddWithValue("$qc_flag", qcFlag);
                            command.Parameters.AddWithValue("$station_id", stationId);
                            command.Parameters.AddWithValue("$location", location);

                            command.ExecuteNonQuery();
                        }
                        
                        transaction.Commit();
                        _logger.LogInformation("Default data inserted successfully");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Error inserting default data");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open database connection to insert default data");
            }
        }
        
        public IActionResult Privacy()
        {
            return View();
        }        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private List<string> GetAvailableStations()
        {
            var stations = new List<string>();
            
            try
            {
                if (_oceanServerDbHelper.HasOceanServerFormat())
                {
                    // If we have OceanServer format, get unique wavy_id values
                    using (var connection = new SqliteConnection(_connectionString))
                    {
                        connection.Open();
                        var command = connection.CreateCommand();
                        command.CommandText = "SELECT DISTINCT wavy_id FROM dados_wavy ORDER BY wavy_id";
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    string wavyId = reader.GetString(0);
                                    if (!string.IsNullOrWhiteSpace(wavyId))
                                    {
                                        stations.Add(wavyId);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // For regular ocean_data format, get unique station_id values
                    using (var connection = new SqliteConnection(_connectionString))
                    {
                        connection.Open();
                        var command = connection.CreateCommand();
                        command.CommandText = "SELECT DISTINCT station_id FROM ocean_data WHERE station_id IS NOT NULL AND station_id != '' ORDER BY station_id";
                        
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    string stationId = reader.GetString(0);
                                    if (!string.IsNullOrWhiteSpace(stationId))
                                    {
                                        stations.Add(stationId);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter estações disponíveis");
            }
            
            return stations;
        }
    }
}
