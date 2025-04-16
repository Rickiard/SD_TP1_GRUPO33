using System;
using System.Data.SQLite;
using System.IO;

class DatabaseInitializer
{
    public static void InitializeDatabase()
    {
        try
        {
            CreateConfigDatabase();
            Console.WriteLine("[OK] Base de dados config_agregador criada/verificada com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Falha na criação da base de dados config_agregador: {ex.Message}");
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

                Console.WriteLine("[OK] Tabelas wavy_config e preprocessing_config criadas/verificadas.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Erro ao criar config_agregador.db: {ex.Message}");
        }
    }
}