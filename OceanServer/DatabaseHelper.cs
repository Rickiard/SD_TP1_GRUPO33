using System;
using System.Data.SQLite;

public static class DatabaseHelper
{
    private const string DB_PATH = "dados_recebidos.db";

    public static void GuardarDadoCSV(string wavyId, string dataLinha)
    {
        try
        {
            using (var conn = new SQLiteConnection($"Data Source={DB_PATH};Version=3;"))
            {
                conn.Open();
                var cmd = new SQLiteCommand("INSERT INTO dados_wavy (wavy_id, data_linha, data_recebida) VALUES (@wavy, @data, @timestamp)", conn);
                cmd.Parameters.AddWithValue("@wavy", wavyId);
                cmd.Parameters.AddWithValue("@data", dataLinha);
                cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DATABASE] Erro ao guardar dado CSV na base de dados: {ex.Message}");
        }
    }
}