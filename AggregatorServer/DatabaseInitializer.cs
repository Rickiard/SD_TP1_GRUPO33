using System;
using System.Data.SQLite;
using System.IO;

class DatabaseInitializer
{
    public static void InitializeDatabases()
    {
        CreateConfigDatabase();
        CreateDataDatabase();
    }

    private static void CreateConfigDatabase()
    {
        string dbPath = "config_agregador.db";

        if (!File.Exists(dbPath))
            SQLiteConnection.CreateFile(dbPath);

        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
        {
            connection.Open();

            string createWavyConfigTable = @"
                CREATE TABLE IF NOT EXISTS wavy_config (
                    wavy_id TEXT PRIMARY KEY,
                    status TEXT,
                    ip TEXT,
                    last_sync TEXT
                );";

            string createPreprocessingTable = @"
                CREATE TABLE IF NOT EXISTS preprocessing_config (
                    wavy_id TEXT PRIMARY KEY,
                    aggregator_id TEXT,
                    volume INTEGER
                );";

            using (var command = new SQLiteCommand(createWavyConfigTable, connection))
                command.ExecuteNonQuery();

            using (var command = new SQLiteCommand(createPreprocessingTable, connection))
                command.ExecuteNonQuery();
        }
    }

    private static void CreateDataDatabase()
    {
        string dbPath = "dados_recebidos.db";

        if (!File.Exists(dbPath))
            SQLiteConnection.CreateFile(dbPath);

        using (var connection = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
        {
            connection.Open();

            string createDataTable = @"
                CREATE TABLE IF NOT EXISTS dados_wavy (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    wavy_id TEXT,
                    data_linha TEXT,
                    data_recebida TEXT
                );";

            using (var command = new SQLiteCommand(createDataTable, connection))
                command.ExecuteNonQuery();
        }
    }
}