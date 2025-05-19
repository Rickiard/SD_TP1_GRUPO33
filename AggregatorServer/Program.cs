﻿using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;
using RPC_PreprocessingServiceClient;

class AgregadorManager
{
    static Dictionary<string, List<string>> dataBuffer = new Dictionary<string, List<string>>();
    static object bufferLock = new object();
    static Dictionary<string, object> fileLocks = new Dictionary<string, object>();
    static HashSet<string> activeWavys = new HashSet<string>();

    static void Main(string[] args)
    {
        Console.WriteLine("Inicializando base de dados...");
        try
        {
            DatabaseInitializer.InitializeDatabase();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao criar base de dados: {ex.Message}");
        }

        if (args.Length != 3)
        {
            Console.WriteLine("Uso correto: AgregadorManager <IP_SERVIDOR> <PORTA_SERVIDOR_1> <PORTA_SERVIDOR_2>");
            return;
        }

        string IpServer = args[0];
        int serverPort1 = Convert.ToInt32(args[1]);
        int serverPort2 = Convert.ToInt32(args[2]);

        Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4000));
        Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4001));
        Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4002));

        Console.WriteLine("[MANAGER] Todos os agregadores foram iniciados.");
        Console.ReadLine();
    }

    static void StartAgregador(string IpServer, int serverPort1, int serverPort2, int aggregatorPort)
    {
        try
        {
            string aggregatorId = GetLocalIPAddress();
            int selectedServerPort = SelectLeastBusyServer(IpServer, serverPort1, serverPort2);
            TcpClient serverClient = new TcpClient(IpServer, selectedServerPort);
            NetworkStream serverStream = serverClient.GetStream();
            Console.WriteLine($"[{aggregatorId}] Conectado ao SERVIDOR na porta {selectedServerPort}!");

            IPAddress ipAddress = IPAddress.Parse(aggregatorId);
            TcpListener listener = new TcpListener(ipAddress, aggregatorPort);
            listener.Start();
            Console.WriteLine($"[{aggregatorId}] Aguardando conexões em {aggregatorId}:{aggregatorPort}...");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine($"[{aggregatorId}] Cliente WAVY conectado!");

                Task.Run(() => HandleClient(client, aggregatorId));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao iniciar agregador na porta {aggregatorPort}: {ex.Message}");
        }
    }

    static void HandleClient(TcpClient client, string aggregatorId)
    {
        string wavyId = null;
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[{aggregatorId}] Mensagem recebida: {receivedMessage}");
                    string response = ProcessMessage(receivedMessage, aggregatorId, ref wavyId);

                    if (response == "DENIED") break;

                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                    Console.WriteLine($"[{aggregatorId}] Resposta enviada: {response}");

                    if (receivedMessage.StartsWith("DATA_CSV"))
                        SaveWavyDataToFile(receivedMessage, aggregatorId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{aggregatorId}] Erro: {ex.Message}");
        }
        finally
        {
            if (wavyId != null)
            {
                FlushRemainingData(aggregatorId);
                UpdateWavyStatus(wavyId, "desativada");
                lock (bufferLock) activeWavys.Remove(wavyId);
            }
        }
    }

    static string ProcessMessage(string message, string aggregatorId, ref string wavyId)
    {
        if (message.StartsWith("HELLO:"))
        {
            wavyId = message.Split(':')[1].Trim();
            if (activeWavys.Contains(wavyId) || !IsWavyConfigured(wavyId)) return "DENIED";
            UpdateWavyStatus(wavyId, "operação");
            DatabaseHelper.AtualizarLastSync(wavyId);
            activeWavys.Add(wavyId);
            return $"ACK:{GetLocalIPAddress()}";
        }
        else if (message.StartsWith("STATUS_REQUEST:"))
        {
            string requestedWavyId = message.Split(':')[1].Trim();
            string status = GetWavyStatus(requestedWavyId);
            return status != null ? $"CURRENT_STATUS:{requestedWavyId}:{status}" : "DENIED";
        }
        else if (message.StartsWith("DATA_CSV:"))
        {
            DatabaseHelper.AtualizarLastSync(wavyId);
            return "ACK";
        }
        else if (message == "QUIT")
        {
            UpdateWavyStatus(wavyId, "desativada");
            return "100 OK";
        }
        return "DENIED";
    }

    static void UpdateWavyStatus(string wavyId, string newStatus)
    {
        using (var conn = new SQLiteConnection("Data Source=config_agregador.db;Version=3;"))
        {
            conn.Open();
            var cmd = new SQLiteCommand("UPDATE wavy_config SET status = @status, last_sync = @sync WHERE wavy_id = @id", conn);
            cmd.Parameters.AddWithValue("@status", newStatus);
            cmd.Parameters.AddWithValue("@sync", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"));
            cmd.Parameters.AddWithValue("@id", wavyId);
            cmd.ExecuteNonQuery();
        }
    }

    static bool IsWavyConfigured(string wavyId)
    {
        using (var conn = new SQLiteConnection("Data Source=config_agregador.db;Version=3;"))
        {
            conn.Open();
            var cmd = new SQLiteCommand("SELECT COUNT(*) FROM wavy_config WHERE wavy_id = @id", conn);
            cmd.Parameters.AddWithValue("@id", wavyId);
            return (long)cmd.ExecuteScalar() > 0;
        }
    }

    static string GetWavyStatus(string wavyId)
    {
        using (var conn = new SQLiteConnection("Data Source=config_agregador.db;Version=3;"))
        {
            conn.Open();
            var cmd = new SQLiteCommand("SELECT status FROM wavy_config WHERE wavy_id = @id", conn);
            cmd.Parameters.AddWithValue("@id", wavyId);
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }
    }

    static int? GetVolumeToSend(string wavyId)
    {
        using (var conn = new SQLiteConnection("Data Source=config_agregador.db;Version=3;"))
        {
            conn.Open();
            var cmd = new SQLiteCommand("SELECT volume FROM preprocessing_config WHERE wavy_id = @id", conn);
            cmd.Parameters.AddWithValue("@id", wavyId);
            var result = cmd.ExecuteScalar();
            return result != null && int.TryParse(result.ToString(), out int vol) ? vol : null;
        }
    }

    static async void SaveWavyDataToFile(string message, string aggregatorId)
    {
        string[] parts = message.Split(':');
        if (parts.Length < 3 || !parts[0].Equals("DATA_CSV")) return;

        string wavyId = parts[1].Trim();
        string data = string.Join(":", parts, 2, parts.Length - 2);

        lock (bufferLock)
        {
            if (!dataBuffer.ContainsKey(wavyId)) dataBuffer[wavyId] = new List<string>();
            dataBuffer[wavyId].Add(data);
        }

        lock (bufferLock)
        {
            if (!fileLocks.ContainsKey(wavyId)) fileLocks[wavyId] = new object();
        }

        string fileName = $"{aggregatorId}_WAVY_{wavyId}.csv";
        lock (fileLocks[wavyId])
        {
            File.AppendAllText(fileName, data + Environment.NewLine);
        }

        int? volumeToSend = GetVolumeToSend(wavyId);
        if (volumeToSend.HasValue)
        {
            List<string> bufferCopy;
            lock (bufferLock)
            {
                bufferCopy = new List<string>(dataBuffer[wavyId]);
            }

            if (bufferCopy.Count >= volumeToSend.Value)
            {
                string aggregatedData = string.Join(Environment.NewLine, bufferCopy);
                
                // Use the PreprocessingService to normalize the data
                bool success = await ProcessDataWithRPC(wavyId, aggregatedData, aggregatorId);

                if (success)
                {
                    lock (bufferLock) dataBuffer[wavyId].Clear();
                    lock (fileLocks[wavyId]) File.WriteAllText(fileName, string.Empty);
                    Console.WriteLine($"[{aggregatorId}] Dados agregados enviados e buffer limpo para {wavyId}.");
                }
            }
        }
    }

    static async void FlushRemainingData(string aggregatorId)
    {
        lock (bufferLock)
        {
            foreach (var wavyId in dataBuffer.Keys)
            {
                List<string> bufferCopy = new List<string>(dataBuffer[wavyId]);
                if (bufferCopy.Count > 0)
                {
                    string aggregatedData = string.Join(Environment.NewLine, bufferCopy);
                    bool success = await ProcessDataWithRPC(wavyId, aggregatedData, aggregatorId);

                    if (success)
                    {
                        dataBuffer[wavyId].Clear();
                        string fileName = $"{aggregatorId}_WAVY_{wavyId}.csv";
                        lock (fileLocks[wavyId]) File.WriteAllText(fileName, string.Empty);
                        Console.WriteLine($"[{aggregatorId}] Dados restantes enviados e buffer limpo para WAVY '{wavyId}'.");
                    }
                }
            }
        }
    }
    
    static async Task<bool> ProcessDataWithRPC(string wavyId, string aggregatedData, string aggregatorId)
    {
        try
        {
            // Connect to the PreprocessingService
            using var channel = GrpcChannel.ForAddress("https://localhost:7270");
            var client = new RPC_PreprocessingServiceClient.PreprocessingService.PreprocessingServiceClient(channel);
            
            // Convert the CSV data to bytes for the RPC call
            byte[] dataBytes = Encoding.UTF8.GetBytes(aggregatedData);
            
            // Create the request
            var request = new RPC_PreprocessingServiceClient.FormatConversionRequest
            {
                InputFormat = "csv",
                OutputFormat = "csv",
                Data = Google.Protobuf.ByteString.CopyFrom(dataBytes)
            };
            
            // Call the RPC service
            Console.WriteLine($"[{aggregatorId}] Enviando dados para o serviço de pré-processamento...");
            var response = await client.ConvertFormatAsync(request);
            
            if (response.Success)
            {
                // Convert the processed data back to string
                string processedData = Encoding.UTF8.GetString(response.ConvertedData.ToByteArray());
                
                // Send the processed data to the server
                bool success = SendMessageToServer($"DATA_CSV:{wavyId}:{processedData}", aggregatorId);
                return success;
            }
            else
            {
                Console.WriteLine($"[{aggregatorId}] Erro no serviço de pré-processamento: {response.ErrorMessage}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{aggregatorId}] Erro ao processar dados com RPC: {ex.Message}");
            return false;
        }
    }

    static bool SendMessageToServer(string message, string aggregatorId)
    {
        try
        {
            string IpServer = "127.0.0.1";
            int PORT = 5000;
            using (TcpClient serverClient = new TcpClient(IpServer, PORT))
            using (NetworkStream serverStream = serverClient.GetStream())
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                serverStream.Write(data, 0, data.Length);
                byte[] buffer = new byte[1024];
                int bytesRead = serverStream.Read(buffer, 0, buffer.Length);
                string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                return serverResponse.StartsWith("100 OK");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{aggregatorId}] Erro ao enviar para SERVIDOR: {ex.Message}");
            return false;
        }
    }

    static string GetLocalIPAddress()
    {
        string localIP = string.Empty;
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                localIP = ip.ToString();
                break;
            }
        }
        if (string.IsNullOrEmpty(localIP))
        {
            throw new Exception("Nenhum endereço IPv4 encontrado na máquina.");
        }
        return localIP;
    }

    static int SelectLeastBusyServer(string IpServer, int port1, int port2)
    {
        try
        {
            int queueLength1 = GetServerQueueLength(IpServer, port1);
            int queueLength2 = GetServerQueueLength(IpServer, port2);
            return queueLength1 <= queueLength2 ? port1 : port2;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar fila dos servidores: {ex.Message}");
            return port1;
        }
    }

    static int GetServerQueueLength(string IpServer, int port)
    {
        try
        {
            using (TcpClient tempClient = new TcpClient(IpServer, port))
            using (NetworkStream tempStream = tempClient.GetStream())
            {
                byte[] request = Encoding.UTF8.GetBytes("QUEUE_LENGTH");
                tempStream.Write(request, 0, request.Length);

                byte[] buffer = new byte[1024];
                int bytesRead = tempStream.Read(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                return int.TryParse(response, out int queueLength) ? queueLength : int.MaxValue;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar fila do servidor na porta {port}: {ex.Message}");
            return int.MaxValue;
        }
    }
}