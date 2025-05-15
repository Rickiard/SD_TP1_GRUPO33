using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Data.SQLite;
using Grpc.Net.Client;
using AnalysisServer.Protos;

public class Program
{
    private static readonly string DbPath = "dados_recebidos.db";
    private static readonly string AnalysisServerAddress = "http://localhost:7020";

    public static void Main(string[] args)
    {
        // Initialize database
        DatabaseInitializer.InitializeDatabase();

        // Start TCP servers on ports 5000 and 5001
        Task.Run(() => StartTcpServer(5000));
        Task.Run(() => StartTcpServer(5001));

        Console.WriteLine("Ocean Servers iniciados. Pressione Ctrl+C para encerrar.");
        Console.ReadLine();
    }

    public static async Task StartTcpServer(int port)
    {
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"Servidor TCP iniciado na porta {port}...");

        // Create gRPC channel for AnalysisServer
        using var channel = GrpcChannel.ForAddress(AnalysisServerAddress);
        var analysisClient = new AnalysisService.AnalysisServiceClient(channel);

        while (true)
        {
            try
            {
                TcpClient client = server.AcceptTcpClient();
                _ = HandleClientAsync(client, analysisClient);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP {port}] Erro: {ex.Message}");
            }
        }
    }

    private static async Task HandleClientAsync(TcpClient client, AnalysisService.AnalysisServiceClient analysisClient)
    {
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[4096];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"[TCP] Dados recebidos: {received}");

                // Parse the received data
                string[] parts = received.Split(':');
                if (parts.Length >= 3 && parts[0] == "DATA")
                {
                    string wavyId = parts[1];
                    string data = string.Join(":", parts, 2, parts.Length - 2);

                    // Send to AnalysisServer for confirmation
                    var analysisRequest = new AnalysisRequest
                    {
                        WavyId = wavyId,
                        Data = data
                    };

                    var analysisResponse = await analysisClient.AnalyzeDataAsync(analysisRequest);

                    if (analysisResponse.Success)
                    {
                        // Store confirmed data in database
                        StoreDataInDatabase(wavyId, data);
                        
                        // Send acknowledgment back to client
                        byte[] response = Encoding.UTF8.GetBytes("ACK");
                        stream.Write(response, 0, response.Length);
                    }
                    else
                    {
                        // Send rejection back to client
                        byte[] response = Encoding.UTF8.GetBytes("REJECTED");
                        stream.Write(response, 0, response.Length);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TCP] Erro ao processar cliente: {ex.Message}");
        }
    }

    private static void StoreDataInDatabase(string wavyId, string data)
    {
        using (var conn = new SQLiteConnection($"Data Source={DbPath};Version=3;"))
        {
            conn.Open();
            var cmd = new SQLiteCommand(
                "INSERT INTO dados_wavy (wavy_id, data, timestamp) VALUES (@wavyId, @data, @timestamp)",
                conn);
            
            cmd.Parameters.AddWithValue("@wavyId", wavyId);
            cmd.Parameters.AddWithValue("@data", data);
            cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));
            
            cmd.ExecuteNonQuery();
        }
    }
}