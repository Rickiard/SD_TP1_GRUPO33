﻿using System;
using System.Data.SQLite;
using System.IO;

public static class DatabaseHelper
{
    private static readonly string DB_PATH = Path.Combine(Environment.CurrentDirectory, "config_agregador.db");

    public static void AdicionarOuAtualizarWavy(string wavyId, string status, string ip)
    {
        try
        {
            using (var conn = new SQLiteConnection($"Data Source={DB_PATH};Version=3;"))
            {
                conn.Open();

                var cmd = new SQLiteCommand(@"
                    INSERT INTO wavy_config (wavy_id, status, ip, last_sync)
                    VALUES (@wavyId, @status, @ip, @sync)
                    ON CONFLICT(wavy_id) DO UPDATE SET
                        status = excluded.status,
                        ip = excluded.ip,
                        last_sync = excluded.last_sync;
                ", conn);

                cmd.Parameters.AddWithValue("@wavyId", wavyId);
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@ip", ip);
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

    // Função para ler os dados da tabela wavy_config
    public static List<dynamic> LerWavyConfig()
    {
        var resultados = new List<dynamic>();

        try
        {
            using (var conn = new SQLiteConnection($"Data Source={DB_PATH};Version=3;"))
            {
                conn.Open();

                var cmd = new SQLiteCommand("SELECT wavy_id, status, ip, last_sync FROM wavy_config;", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        resultados.Add(new
                        {
                            WavyId = reader["wavy_id"].ToString(),
                            Status = reader["status"].ToString(),
                            Ip = reader["ip"].ToString(),
                            LastSync = reader["last_sync"].ToString()
                        });
                    }
                }
            }

            Console.WriteLine("[DB] Dados da tabela wavy_config lidos com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Erro ao ler wavy_config: {ex.Message}");
        }

        return resultados;
    }

    // Função para ler os dados da tabela preprocessing_config
    public static List<dynamic> LerPreprocessingConfig()
    {
        var resultados = new List<dynamic>();

        try
        {
            using (var conn = new SQLiteConnection($"Data Source={DB_PATH};Version=3;"))
            {
                conn.Open();

                var cmd = new SQLiteCommand("SELECT wavy_id, aggregator_id, volume FROM preprocessing_config;", conn);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        resultados.Add(new
                        {
                            WavyId = reader["wavy_id"].ToString(),
                            AggregatorId = reader["aggregator_id"].ToString(),
                            Volume = Convert.ToInt32(reader["volume"])
                        });
                    }
                }
            }

            Console.WriteLine("[DB] Dados da tabela preprocessing_config lidos com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Erro ao ler preprocessing_config: {ex.Message}");
        }

        return resultados;
    }
}