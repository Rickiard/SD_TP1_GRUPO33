using System;
using System.Data.SQLite;
using System.IO;

class DatabaseInitializer
{
    public static void InitializeDatabase()
    {
        try
        {
            CreateDataDatabase();
            Console.WriteLine("[OK] Base de dados de dados_recebidos criada/verificada com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Falha na criação da base de dados de dados_recebidos: {ex.Message}");
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
            Console.WriteLine($"[ERRO] Erro ao criar dados_recebidos.db: {ex.Message}");
        }
    }
}