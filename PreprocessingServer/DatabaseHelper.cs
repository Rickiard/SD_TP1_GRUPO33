using System.Data.SQLite;

public static class DatabaseHelper
{
    public static (string PreProcessType, int Volume, string Server) GetPreprocessingConfig(string wavyId)
    {
        using var conn = new SQLiteConnection("Data Source=../config_agregador.db");
        conn.Open();
        using var cmd = new SQLiteCommand("SELECT [pré_processamento], volume, servidor_associado FROM preprocessing_config WHERE wavy_id = @wavyId", conn);
        cmd.Parameters.AddWithValue("@wavyId", wavyId);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2)
            );
        }
        return (null, 0, null);
    }
} 