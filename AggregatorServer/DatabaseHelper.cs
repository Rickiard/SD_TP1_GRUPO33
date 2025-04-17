using System;
using System.Data.SQLite;
using System.IO;

public static class DatabaseHelper
{
    private static readonly string DB_PATH = Path.Combine(Environment.CurrentDirectory, "config_agregador.db");

    public static void AdicionarOuAtualizarWavy(string wavyId, string status, string sensores)
    {
        try
        {
            using (var conn = new SQLiteConnection($"Data Source={DB_PATH};Version=3;"))
            {
                conn.Open();

                var cmd = new SQLiteCommand(@"
                    INSERT INTO wavy_config (wavy_id, status, sensores, last_sync)
                    VALUES (@wavyId, @status, @sensores, @sync)
                    ON CONFLICT(wavy_id) DO UPDATE SET
                        status = excluded.status,
                        sensores = excluded.sensores,
                        last_sync = excluded.last_sync;
                ", conn);

                cmd.Parameters.AddWithValue("@wavyId", wavyId);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@sensores", sensores);
                cmd.Parameters.AddWithValue("@sync", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine($"[DB] Wavy '{wavyId}' registado/atualizado com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Erro ao atualizar wavy_config: {ex.Message}");
        }
    }

    public static void AtualizarLastSync(string wavyId)
    {
        try
        {
            using (var conn = new SQLiteConnection($"Data Source={DB_PATH};Version=3;"))
            {
                conn.Open();

                var cmd = new SQLiteCommand(@"
                    UPDATE wavy_config 
                    SET last_sync = @sync
                    WHERE wavy_id = @id;
                ", conn);

                cmd.Parameters.AddWithValue("@id", wavyId);
                cmd.Parameters.AddWithValue("@sync", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine($"[DB] last_sync de '{wavyId}' atualizado.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Erro ao atualizar last_sync: {ex.Message}");
        }
    }

    public static void AdicionarOuAtualizarPreprocessing(string wavyId, string aggregatorId, int volume)
    {
        try
        {
            using (var conn = new SQLiteConnection($"Data Source={DB_PATH};Version=3;"))
            {
                conn.Open();

                var cmd = new SQLiteCommand(@"
                    INSERT INTO preprocessing_config (wavy_id, aggregator_id, volume)
                    VALUES (@wavyId, @aggId, @volume)
                    ON CONFLICT(wavy_id) DO UPDATE SET
                        aggregator_id = excluded.aggregator_id,
                        volume = excluded.volume;
                ", conn);

                cmd.Parameters.AddWithValue("@wavyId", wavyId);
                cmd.Parameters.AddWithValue("@aggId", aggregatorId);
                cmd.Parameters.AddWithValue("@volume", volume);
                cmd.ExecuteNonQuery();
            }

            Console.WriteLine($"[DB] Preprocessing config para '{wavyId}' atualizado com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Erro ao atualizar preprocessing_config: {ex.Message}");
        }
    }
}