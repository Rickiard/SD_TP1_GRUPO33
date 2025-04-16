using System;
using System.Data.SQLite;
using System.IO;

class DatabaseInitializer
{
    public static void InitializeDatabases()
    {
        try
        {
            CreateConfigDatabase();
            CreateDataDatabase();
            Console.WriteLine("[OK] Bases de dados criadas/verificadas com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Falha na criação das bases de dados: {ex.Message}");
        }
    }

    private static void CreateConfigDatabase()
    {
        string dbPath = Path.Combine(Environment.CurrentDirectory, "config_agregador.db");
        Console.WriteLine($"[DEBUG] Caminho da config DB: {dbPath}");

        try
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
                Console.WriteLine("[INFO] Ficheiro config_agregador.db criado.");
            }

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

                Console.WriteLine("[OK] Tabelas da config DB criadas/verificadas.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Erro ao criar config DB: {ex.Message}");
        }
    }

    private static void CreateDataDatabase()
    {
        string dbPath = Path.Combine(Environment.CurrentDirectory, "dados_recebidos.db");
        Console.WriteLine($"[DEBUG] Caminho da dados DB: {dbPath}");

        try
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
                Console.WriteLine("[INFO] Ficheiro dados_recebidos.db criado.");
            }

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

                Console.WriteLine("[OK] Tabela dados_wavy criada/verificada.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Erro ao criar dados DB: {ex.Message}");
        }
    }
}