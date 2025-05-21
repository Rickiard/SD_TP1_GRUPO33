﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using RPC_PreprocessingServiceClient;

namespace AggregatorServer
{
    class AgregadorManager
    {
        static ConcurrentDictionary<string, List<string>> dataBuffer = new ConcurrentDictionary<string, List<string>>();
        static ConcurrentDictionary<string, object> fileLocks = new ConcurrentDictionary<string, object>();
        static ConcurrentDictionary<string, RabbitMQService> rabbitMqServices = new ConcurrentDictionary<string, RabbitMQService>();
        static ConcurrentDictionary<string, string> activeWavys = new ConcurrentDictionary<string, string>();
        
        const string RABBITMQ_HOST = "localhost";
        const string EXCHANGE_NAME = "wavy_exchange";

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

            if (args.Length < 3)
            {
                Console.WriteLine("Uso correto: AgregadorManager <IP_SERVIDOR> <PORTA_SERVIDOR_1> <PORTA_SERVIDOR_2> [RabbitMQ Host]");
                Console.WriteLine("Usando localhost como padrão para RabbitMQ");
            }

            string IpServer = args[0];
            int serverPort1 = Convert.ToInt32(args[1]);
            int serverPort2 = Convert.ToInt32(args[2]);
            string rabbitMqHost = args.Length > 3 ? args[3] : RABBITMQ_HOST;

            Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4000, rabbitMqHost));
            Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4001, rabbitMqHost));
            Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4002, rabbitMqHost));

            Console.WriteLine("[MANAGER] Todos os agregadores foram iniciados.");
            Console.ReadLine();
        }

        static void StartAgregador(string IpServer, int serverPort1, int serverPort2, int aggregatorPort, string rabbitMqHost)
        {
            try
            {
                string aggregatorId = GetLocalIPAddress() + ":" + aggregatorPort;
                int selectedServerPort = SelectLeastBusyServer(IpServer, serverPort1, serverPort2);
                TcpClient serverClient = new TcpClient(IpServer, selectedServerPort);
                Console.WriteLine($"[{aggregatorId}] Conectado ao SERVIDOR na porta {selectedServerPort}!");

                // Inicializar o serviço RabbitMQ com a porta específica do agregador
                var rabbitMqService = new RabbitMQService(rabbitMqHost, EXCHANGE_NAME, aggregatorId, aggregatorPort);
                if (rabbitMqService != null)
                {
                    rabbitMqServices.TryAdd(aggregatorId, rabbitMqService);
                    
                    // Configurar o handler para mensagens recebidas
                    rabbitMqService.OnMessageReceived += (message) => 
                    {
                        ProcessWavyMessage(message, aggregatorId, rabbitMqService);
                    };

                    Console.WriteLine($"[{aggregatorId}] Serviço RabbitMQ inicializado e aguardando mensagens na porta {aggregatorPort}...");
                    
                    // Manter o agregador em execução
                    var waitHandle = new ManualResetEvent(false);
                    waitHandle.WaitOne();
                }
                else
                {
                    throw new Exception("Falha ao inicializar o serviço RabbitMQ");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao iniciar agregador na porta {aggregatorPort}: {ex.Message}");
            }
        }

        static void ProcessWavyMessage(WavyMessage message, string aggregatorId, RabbitMQService rabbitMqService)
        {
            Console.WriteLine($"[{aggregatorId}] Mensagem recebida: {message.MessageType} de {message.WavyId}");
            
            try
            {
                switch (message.MessageType)
                {
                    case "HELLO":
                        HandleHelloMessage(message, aggregatorId, rabbitMqService);
                        break;
                    
                    case "DATA_CSV":
                        HandleDataMessage(message, aggregatorId, rabbitMqService);
                        break;
                    
                    case "QUIT":
                        HandleQuitMessage(message, aggregatorId);
                        break;
                    
                    case "STATUS_REQUEST":
                        HandleStatusRequest(message, aggregatorId, rabbitMqService);
                        break;
                    
                    default:
                        Console.WriteLine($"[{aggregatorId}] Tipo de mensagem desconhecido: {message.MessageType}");
                        SendResponse(message.WavyId, "DENIED", "", rabbitMqService);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{aggregatorId}] Erro ao processar mensagem: {ex.Message}");
                SendResponse(message.WavyId, "ERROR", ex.Message, rabbitMqService);
            }
        }

        static void HandleHelloMessage(WavyMessage message, string aggregatorId, RabbitMQService rabbitMqService)
        {
            string wavyId = message.WavyId;
            
            if (activeWavys.ContainsKey(wavyId) || !IsWavyConfigured(wavyId))
            {
                SendResponse(wavyId, "DENIED", "WAVY já está ativa ou não está configurada", rabbitMqService);
                return;
            }
            
            UpdateWavyStatus(wavyId, "operação");
            DatabaseHelper.AtualizarLastSync(wavyId);
            activeWavys.TryAdd(wavyId, aggregatorId);
            
            SendResponse(wavyId, "ACK", aggregatorId, rabbitMqService);
        }

        static void HandleDataMessage(WavyMessage message, string aggregatorId, RabbitMQService rabbitMqService)
        {
            string wavyId = message.WavyId;
            string data = message.Data;
            
            if (!activeWavys.ContainsKey(wavyId))
            {
                SendResponse(wavyId, "DENIED", "WAVY não está ativa", rabbitMqService);
                return;
            }
            
            DatabaseHelper.AtualizarLastSync(wavyId);
            SaveWavyDataToFile(wavyId, data, aggregatorId);
            
            SendResponse(wavyId, "ACK", "", rabbitMqService);
        }

        static void HandleQuitMessage(WavyMessage message, string aggregatorId)
        {
            string wavyId = message.WavyId;
            
            if (activeWavys.TryRemove(wavyId, out _))
            {
                FlushRemainingData(wavyId, aggregatorId);
                UpdateWavyStatus(wavyId, "desativada");
                Console.WriteLine($"[{aggregatorId}] WAVY {wavyId} desconectada.");
            }
        }

        static void HandleStatusRequest(WavyMessage message, string aggregatorId, RabbitMQService rabbitMqService)
        {
            string requestedWavyId = message.WavyId;
            string status = GetWavyStatus(requestedWavyId);
            
            if (status != null)
            {
                SendResponse(requestedWavyId, "CURRENT_STATUS", status, rabbitMqService);
            }
            else
            {
                SendResponse(requestedWavyId, "DENIED", "WAVY não encontrada", rabbitMqService);
            }
        }

        static void SendResponse(string wavyId, string messageType, string data, RabbitMQService rabbitMqService)
        {
            var response = new WavyMessage(wavyId, messageType, data);
            rabbitMqService.PublishMessage(response);
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

        static void SaveWavyDataToFile(string wavyId, string data, string aggregatorId)
        {
            // Adicionar ao buffer
            if (!dataBuffer.ContainsKey(wavyId))
            {
                dataBuffer.TryAdd(wavyId, new List<string>());
            }
            
            dataBuffer[wavyId].Add(data);
            
            // Garantir que temos um lock para este WAVY
            if (!fileLocks.ContainsKey(wavyId))
            {
                fileLocks.TryAdd(wavyId, new object());
            }

            // Salvar no arquivo
            string fileName = $"{aggregatorId}_WAVY_{wavyId}.csv";
            lock (fileLocks[wavyId])
            {
                File.AppendAllText(fileName, data + Environment.NewLine);
            }

            // Verificar se temos dados suficientes para enviar
            int? volumeToSend = GetVolumeToSend(wavyId);
            if (volumeToSend.HasValue)
            {
                List<string> bufferCopy;
                lock (dataBuffer[wavyId])
                {
                    bufferCopy = new List<string>(dataBuffer[wavyId]);
                }

                if (bufferCopy.Count >= volumeToSend.Value)
                {
                    string aggregatedData = string.Join(Environment.NewLine, bufferCopy);
                    
                    // Use the PreprocessingService to normalize the data - chamada síncrona
                    Task<bool> task = ProcessDataWithRPC(wavyId, aggregatedData, aggregatorId);
                    task.Wait(); // Espera a tarefa completar de forma síncrona
                    bool success = task.Result;

                    if (success)
                    {
                        lock (dataBuffer[wavyId]) dataBuffer[wavyId].Clear();
                        lock (fileLocks[wavyId]) File.WriteAllText(fileName, string.Empty);
                        Console.WriteLine($"[{aggregatorId}] Dados agregados enviados e buffer limpo para {wavyId}.");
                    }
                }
            }
        }

        static void FlushRemainingData(string wavyId, string aggregatorId)
        {
            if (!dataBuffer.ContainsKey(wavyId) || dataBuffer[wavyId].Count == 0)
            {
                return;
            }
            
            string aggregatedData;
            lock (dataBuffer[wavyId])
            {
                aggregatedData = string.Join(Environment.NewLine, dataBuffer[wavyId]);
            }
            
            if (string.IsNullOrEmpty(aggregatedData))
            {
                return;
            }
            
            // Chamamos o método de forma síncrona para simplificar
            bool success = ProcessDataWithRPC(wavyId, aggregatedData, aggregatorId).GetAwaiter().GetResult();
            
            if (success)
            {
                lock (dataBuffer[wavyId])
                {
                    dataBuffer[wavyId].Clear();
                }
                
                string fileName = $"{aggregatorId}_WAVY_{wavyId}.csv";
                lock (fileLocks[wavyId]) 
                {
                    File.WriteAllText(fileName, string.Empty);
                }
                
                Console.WriteLine($"[{aggregatorId}] Dados restantes enviados e buffer limpo para WAVY '{wavyId}'.");
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
}