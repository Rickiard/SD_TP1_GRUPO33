using Microsoft.AspNetCore.Mvc;
using OceanDashboard.Models;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace OceanDashboard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly string _connectionString = "Data Source=dados_recebidos.db";

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                // Criar tabela se não existir
                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ocean_data (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        timestamp DATETIME NOT NULL,
                        wave_height REAL NOT NULL,
                        wave_period REAL NOT NULL,
                        wave_direction REAL NOT NULL,
                        temperature REAL NOT NULL,
                        location TEXT NOT NULL
                    )";
                command.ExecuteNonQuery();

                // Inserir dados de exemplo se a tabela estiver vazia
                command.CommandText = "SELECT COUNT(*) FROM ocean_data";
                var count = Convert.ToInt32(command.ExecuteScalar());

                if (count == 0)
                {
                    var random = new Random();
                    var baseTime = DateTime.Now.AddHours(-24);

                    for (int i = 0; i < 100; i++)
                    {
                        command.CommandText = @"
                            INSERT INTO ocean_data (timestamp, wave_height, wave_period, wave_direction, temperature, location)
                            VALUES ($timestamp, $wave_height, $wave_period, $wave_direction, $temperature, $location)";

                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("$timestamp", baseTime.AddMinutes(i * 15));
                        command.Parameters.AddWithValue("$wave_height", random.NextDouble() * 3 + 1); // 1-4m
                        command.Parameters.AddWithValue("$wave_period", random.NextDouble() * 8 + 4); // 4-12s
                        command.Parameters.AddWithValue("$wave_direction", random.NextDouble() * 360); // 0-360°
                        command.Parameters.AddWithValue("$temperature", random.NextDouble() * 10 + 15); // 15-25°C
                        command.Parameters.AddWithValue("$location", "Oceano Atlântico");

                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public IActionResult Index()
        {
            var oceanData = GetLatestOceanData();
            return View(oceanData);
        }

        [HttpGet]
        public IActionResult GetLatestData()
        {
            var oceanData = GetLatestOceanData();
            return Json(oceanData);
        }

        private List<OceanData> GetLatestOceanData()
        {
            var data = new List<OceanData>();
            
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT id, timestamp, wave_height, wave_period, wave_direction, temperature, location 
                    FROM ocean_data 
                    ORDER BY timestamp DESC 
                    LIMIT 100";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        data.Add(new OceanData
                        {
                            Id = reader.GetInt32(0),
                            Timestamp = DateTime.Parse(reader.GetString(1)),
                            WaveHeight = reader.GetDouble(2),
                            WavePeriod = reader.GetDouble(3),
                            WaveDirection = reader.GetDouble(4),
                            Temperature = reader.GetDouble(5),
                            Location = reader.GetString(6)
                        });
                    }
                }
            }

            return data;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
