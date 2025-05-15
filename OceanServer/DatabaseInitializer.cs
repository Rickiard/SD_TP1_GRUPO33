using System;
using System.Data.SQLite;
using System.IO;

public static class DatabaseInitializer
{
    public static void InitializeDatabase()
    {
        string dbPath = "dados_recebidos.db";
        bool isNewDatabase = !System.IO.File.Exists(dbPath);

        using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
        {
            conn.Open();

            if (isNewDatabase)
            {
                // Create dados_wavy table
                using (var cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = @"
                        CREATE TABLE dados_wavy (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            wavy_id TEXT NOT NULL,
                            data TEXT NOT NULL,
                            timestamp TEXT NOT NULL
                        )";
                    cmd.ExecuteNonQuery();
                }

                Console.WriteLine("Base de dados criada com sucesso.");
            }
            else
            {
                Console.WriteLine("Base de dados já existe.");
            }
        }
    }
}