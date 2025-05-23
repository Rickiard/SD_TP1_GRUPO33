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
        }

        /// <summary>
        /// Checks if the database contains the dados_wavy table used by OceanServer
        /// </summary>
        public bool HasOceanServerFormat()
        {
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Check if the dados_wavy table exists
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='dados_wavy'";
                    
                    var result = command.ExecuteScalar();
                    return result != null && result.ToString() == "dados_wavy";
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
        public List<OceanData> GetDataFromOceanServer(DateTime startDate, DateTime endDate, string location)
        {
            var data = new List<OceanData>();
            
            try
            {
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Query the dados_wavy table
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT id, wavy_id, data_linha, data_recebida 
                        FROM dados_wavy 
                        WHERE datetime(data_recebida) >= datetime($startDate) 
                        AND datetime(data_recebida) <= datetime($endDate)
                        ORDER BY data_recebida DESC";
                    
                    command.Parameters.AddWithValue("$startDate", startDate.ToString("o"));
                    command.Parameters.AddWithValue("$endDate", endDate.ToString("o"));
                    
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
                                var parsedData = ParseWavyDataLine(dataLinha, dataRecebida, wavyId, location);
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
        }
          /// <summary>
        /// Parse a data_linha field from the OceanServer format into an OceanData object
        /// </summary>
        private OceanData? ParseWavyDataLine(string dataLinha, string dataRecebida, string wavyId, string location)
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
                
                return new OceanData 
                {
                    Timestamp = DateTime.Parse(campos[0]),
                    Longitude = double.Parse(campos[1], CultureInfo.InvariantCulture),
                    Latitude = double.Parse(campos[2], CultureInfo.InvariantCulture),
                    AtmospherePressure = double.Parse(campos[3], CultureInfo.InvariantCulture),
                    WindDirection = double.Parse(campos[4], CultureInfo.InvariantCulture),
                    WindSpeed = double.Parse(campos[5], CultureInfo.InvariantCulture),
                    Gust = double.Parse(campos[6], CultureInfo.InvariantCulture),
                    WaveHeight = double.Parse(campos[7], CultureInfo.InvariantCulture),
                    WavePeriod = double.Parse(campos[8], CultureInfo.InvariantCulture),
                    WaveDirection = double.Parse(campos[9], CultureInfo.InvariantCulture),
                    MaxWaveHeight = double.Parse(campos[10], CultureInfo.InvariantCulture),
                    AirTemperature = double.Parse(campos[11], CultureInfo.InvariantCulture),
                    DewPoint = double.Parse(campos[12], CultureInfo.InvariantCulture),
                    SeaTemperature = double.Parse(campos[13], CultureInfo.InvariantCulture),
                    RelativeHumidity = double.Parse(campos[14], CultureInfo.InvariantCulture),
                    QcFlag = campos.Length > 15 ? int.Parse(campos[15]) : 0,
                    SensorId = wavyId,
                    StationId = wavyId, // Use wavyId as stationId too
                    Location = location == "all" ? "Oceano Atlântico" : location
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing data line: {0}", dataLinha);
                return null;
            }
        }
    }
}
