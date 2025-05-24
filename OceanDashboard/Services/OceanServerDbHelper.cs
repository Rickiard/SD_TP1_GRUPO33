using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;
using OceanDashboard.Models;
using Microsoft.Extensions.Logging;

namespace OceanDashboard.Services
{
    /// <summary>
    /// Helper class for working with the OceanServer database format
    /// </summary>
    public class OceanServerDbHelper
    {
        private readonly ILogger _logger;
        private readonly string _connectionString;

        /// <summary>
        /// Factory method to create an OceanServerDbHelper with any ILogger
        /// </summary>
        public static OceanServerDbHelper Create(string connectionString, ILogger logger)
        {
            return new OceanServerDbHelper(connectionString, logger);
        }

        public OceanServerDbHelper(string connectionString, ILogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }        /// <summary>
        /// Checks if the database contains the dados_wavy table used by OceanServer
        /// MODIFICADO: Sempre retorna false para forçar uso da tabela ocean_data
        /// </summary>
        public bool HasOceanServerFormat()
        {
            try
            {
                _logger.LogInformation("=== VERIFICANDO FORMATO OCEANSERVER ===");
                _logger.LogInformation("String de conexão: {ConnectionString}", _connectionString);
                
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    _logger.LogInformation("Conexão aberta com sucesso");
                    
                    // Primeiro, listar todas as tabelas disponíveis
                    var listTablesCmd = connection.CreateCommand();
                    listTablesCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                    
                    var allTables = new List<string>();
                    using (var reader = listTablesCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allTables.Add(reader.GetString(0));
                        }
                    }
                    
                    _logger.LogInformation("Tabelas encontradas no banco: {Tables}", string.Join(", ", allTables));
                    
                    // Verificar se existe a tabela ocean_data
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ocean_data'";
                    var oceanDataResult = command.ExecuteScalar();
                    bool hasOceanDataTable = oceanDataResult != null && oceanDataResult.ToString() == "ocean_data";
                    
                    _logger.LogInformation("Found ocean_data table: {HasOceanDataTable}", hasOceanDataTable);
                    
                    if (hasOceanDataTable)
                    {
                        var countCmd = connection.CreateCommand();
                        countCmd.CommandText = "SELECT COUNT(*) FROM ocean_data";
                        var count = countCmd.ExecuteScalar();
                        _logger.LogInformation("Tabela ocean_data tem {Count} registros", count);
                    }
                    
                    // FORÇAR USO DA TABELA ocean_data - sempre retorna false
                    _logger.LogInformation("=== FORÇANDO USO DA TABELA ocean_data ===");
                    _logger.LogInformation("=== RESULTADO: HasOceanServerFormat = false (forçado) ===");
                    
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if database has OceanServer format");
                return false;
            }
        }
          /// <summary>
        /// Gets ocean data from the OceanServer dados_wavy table based on date range
        /// </summary>
        public List<OceanData> GetDataFromOceanServer(DateTime startDate, DateTime endDate, string stationIdFilter)
        {
            var data = new List<OceanData>();
            
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    
                    string whereClause = "WHERE datetime(data_recebida) >= datetime($startDate) AND datetime(data_recebida) <= datetime($endDate)";
                    
                    // Se tiver filtro de estação, adicionar à query
                    if (!string.IsNullOrEmpty(stationIdFilter) && stationIdFilter != "all")
                    {
                        whereClause += " AND wavy_id = $stationId";
                    }
                    
                    // Query the dados_wavy table
                    var command = connection.CreateCommand();
                    command.CommandText = $@"
                        SELECT id, wavy_id, data_linha, data_recebida 
                        FROM dados_wavy 
                        {whereClause}
                        ORDER BY data_recebida DESC";
                    
                    command.Parameters.AddWithValue("$startDate", startDate.ToString("o"));
                    command.Parameters.AddWithValue("$endDate", endDate.ToString("o"));
                    
                    if (!string.IsNullOrEmpty(stationIdFilter) && stationIdFilter != "all")
                    {
                        command.Parameters.AddWithValue("$stationId", stationIdFilter);
                    }
                    
                    _logger.LogInformation("Executing query for dados_wavy table");
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            try 
                            {
                                // Read the line data
                                string dataLinha = reader.GetString(2); // data_linha
                                string dataRecebida = reader.GetString(3); // data_recebida
                                string wavyId = reader.GetString(1); // wavy_id
                                
                                // Parse the data_linha field which contains CSV data
                                var parsedData = ParseWavyDataLine(dataLinha, dataRecebida, wavyId, stationIdFilter);
                                if (parsedData != null)
                                {
                                    data.Add(parsedData);
                                }
                            }
                            catch (Exception ex) 
                            {
                                _logger.LogError(ex, "Error reading wavy record: {0}", reader[0]);
                            }
                        }
                    }
                    
                    _logger.LogInformation("Found {0} records in dados_wavy table", data.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying OceanServer database");
            }
            
            return data;
        }        /// <summary>
        /// Parse a data_linha field from the OceanServer format into an OceanData object
        /// </summary>
        private OceanData? ParseWavyDataLine(string dataLinha, string dataRecebida, string wavyId, string locationFilter)
        {
            try 
            {
                // Example of data_linha: "2023-05-23 14:30:00 UTC;-25.123;-44.456;1013.2;210;12.3;15.6;2.5;8.7;180;3.8;22.1;18.5;19.8;78.5;0"
                // Fields: timestamp;longitude;latitude;pressure;windDir;windSpeed;gust;waveHeight;wavePeriod;waveDir;maxWaveHeight;airTemp;dewPoint;seaTemp;humidity;qcFlag
                
                string[] campos = dataLinha.Split(';');
                if (campos.Length < 15)
                {
                    _logger.LogWarning("Invalid data line (not enough fields): {0}", dataLinha);
                    return null;
                }
                
                // Verificar e limpar dados antes de criar o objeto
                DateTime timestamp;
                if (!DateTime.TryParse(campos[0], out timestamp))
                {
                    _logger.LogWarning("Invalid timestamp in data line: {0}", campos[0]);
                    timestamp = DateTime.Parse(dataRecebida); // Usar a data de recebimento como fallback
                }
                
                // Calcular valores de fallback para campos importantes
                double waveHeight = 0.0;
                double.TryParse(campos[7], NumberStyles.Any, CultureInfo.InvariantCulture, out waveHeight);
                
                double wavePeriod = 0.0;
                double.TryParse(campos[8], NumberStyles.Any, CultureInfo.InvariantCulture, out wavePeriod);
                
                double seaTemp = 0.0;
                double.TryParse(campos[13], NumberStyles.Any, CultureInfo.InvariantCulture, out seaTemp);
                
                // Criar objeto com tratamento adequado para valores inválidos
                var oceanData = new OceanData 
                {
                    Timestamp = timestamp,
                    SensorId = wavyId,
                    StationId = wavyId
                };
                
                // Campos de posição
                if (double.TryParse(campos[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double longitude))
                    oceanData.Longitude = longitude;
                
                if (double.TryParse(campos[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double latitude))
                    oceanData.Latitude = latitude;
                
                // Campos atmosféricos
                if (double.TryParse(campos[3], NumberStyles.Any, CultureInfo.InvariantCulture, out double pressure))
                    oceanData.AtmospherePressure = pressure;
                
                // Campos de vento
                if (double.TryParse(campos[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double windDir))
                    oceanData.WindDirection = windDir;
                
                if (double.TryParse(campos[5], NumberStyles.Any, CultureInfo.InvariantCulture, out double windSpeed))
                    oceanData.WindSpeed = windSpeed;
                
                if (double.TryParse(campos[6], NumberStyles.Any, CultureInfo.InvariantCulture, out double gust))
                    oceanData.Gust = gust;
                
                // Campos de ondas
                oceanData.WaveHeight = waveHeight;
                oceanData.WavePeriod = wavePeriod;
                
                if (double.TryParse(campos[9], NumberStyles.Any, CultureInfo.InvariantCulture, out double waveDir))
                    oceanData.WaveDirection = waveDir;
                
                if (double.TryParse(campos[10], NumberStyles.Any, CultureInfo.InvariantCulture, out double maxWaveHeight))
                    oceanData.MaxWaveHeight = maxWaveHeight;
                
                // Campos de temperatura e umidade
                if (double.TryParse(campos[11], NumberStyles.Any, CultureInfo.InvariantCulture, out double airTemp))
                    oceanData.AirTemperature = airTemp;
                
                if (double.TryParse(campos[12], NumberStyles.Any, CultureInfo.InvariantCulture, out double dewPoint))
                    oceanData.DewPoint = dewPoint;
                
                oceanData.SeaTemperature = seaTemp;
                
                if (double.TryParse(campos[14], NumberStyles.Any, CultureInfo.InvariantCulture, out double humidity))
                    oceanData.RelativeHumidity = humidity;
                
                // QC flag
                if (campos.Length > 15 && int.TryParse(campos[15], out int qcFlag))
                    oceanData.QcFlag = qcFlag;
                
                // Verificar se temos pelo menos alguns dados válidos
                if (oceanData.WaveHeight <= 0 && oceanData.WindSpeed <= 0 && oceanData.SeaTemperature <= 0)
                {
                    _logger.LogWarning("No valid sensor data in line: {0}", dataLinha);
                    return null;
                }
                
                return oceanData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing data line: {0}", dataLinha);
                return null;
            }
        }
    }
}
