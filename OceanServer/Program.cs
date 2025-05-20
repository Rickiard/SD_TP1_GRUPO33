﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client;
using RPC_DataAnalyserServiceClient;
using System.Net.Http;
using Grpc.Core;

class TCPServer
{
    private static Mutex mutex = new Mutex();
    private const string DataDirectory = "ReceivedData";

    static void HandleClient(object obj)
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = null;

        try
        {
            stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            // Enviar resposta inicial "100 OK"
            byte[] okResponse = Encoding.UTF8.GetBytes("100 OK\n");
            stream.Write(okResponse, 0, okResponse.Length);

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                bool hasMutex = false; // Acompanhar se o mutex foi adquirido
                try
                {
                    mutex.WaitOne();
                    hasMutex = true;

                    Console.WriteLine($"Recebido de {((IPEndPoint)client.Client.RemoteEndPoint).Address}: {message}");

                    if (message.StartsWith("DATA_CSV"))
                    {
                        ProcessCSVData(message);
                        stream.Write(okResponse, 0, okResponse.Length); // Confirmação de receção
                    }
                    else if (message == "QUIT")
                    {
                        byte[] byeResponse = Encoding.UTF8.GetBytes("400 BYE\n");
                        stream.Write(byeResponse, 0, byeResponse.Length);
                        break;
                    }
                }
                finally
                {
                    if (hasMutex)
                    {
                        mutex.ReleaseMutex(); // Soltar mutex apenas se já foi adquirido
                    }
                }
            }
        }
        catch (IOException ex)
        {
            //Console.WriteLine($"Erro de E/S: {ex.Message}");
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Erro inesperado: {ex.Message}");
        }
        finally
        {
            client?.Close();
            stream?.Close();
        }
    }

    static async void ProcessCSVData(string message)
    {
        string[] parts = message.Split(':', 3); // Dividir apenas em três partes
        if (parts.Length < 3) 
        {
            Console.WriteLine($"Formato de mensagem inválido: {message}");
            return;
        }

        string wavyId = parts[1];
        string csvData = parts[2]; // O CSV completo está na terceira parte

        if (string.IsNullOrWhiteSpace(csvData))
        {
            Console.WriteLine("Linha em branco ignorada.");
            return;
        }

        Console.WriteLine($"Processando dados para WAVY {wavyId}");
        Console.WriteLine($"Dados recebidos: {csvData.Substring(0, Math.Min(50, csvData.Length))}...");

        string filePath = Path.Combine(DataDirectory, $"{wavyId}.csv");
        Directory.CreateDirectory(DataDirectory);

        bool hasMutex = false;
        try
        {
            mutex.WaitOne();
            hasMutex = true;

            // Save the raw data
            File.AppendAllText(filePath, csvData + "\n");
            Console.WriteLine($"Dados agregados armazenados em {filePath}");
            
            // Save to database first to ensure data is preserved
            DatabaseHelper.GuardarDadoCSV(wavyId, csvData);
            
            // Process the data with the DataAnalyserService
            try
            {
                Console.WriteLine($"Iniciando análise de dados para WAVY {wavyId}...");
                bool analysisSuccess = await AnalyzeDataWithRPC(wavyId, csvData);
                
                if (analysisSuccess)
                {
                    Console.WriteLine($"Análise de dados concluída com sucesso para WAVY {wavyId}");
                    
                    // After successful analysis, we could send a confirmation message
                    // to another service if needed, similar to how AggregatorServer
                    // sends data to OceanServer after preprocessing
                }
                else
                {
                    Console.WriteLine($"Falha na análise de dados para WAVY {wavyId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao tentar analisar dados: {ex.Message}");
                Console.WriteLine("Os dados foram salvos, mas a análise não pôde ser realizada.");
            }

            // Limpar linhas em branco do ficheiro CSV
            CleanEmptyLinesFromCSV(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao processar dados CSV: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            if (hasMutex)
            {
                mutex.ReleaseMutex();
            }
        }
    }
    
    static async Task<bool> AnalyzeDataWithRPC(string wavyId, string csvData)
    {
        try
        {
            // Configure the HttpClient to ignore certificate validation for development
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = 
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
            // Set a timeout to avoid hanging
            var httpClient = new HttpClient(httpClientHandler)
            {
                Timeout = TimeSpan.FromSeconds(10)
            };
            
            // Connect to the DataAnalyserService
            // Make sure this URL matches the actual URL where RPC_DataAnalyserService is running
            var channelOptions = new GrpcChannelOptions { HttpClient = httpClient };
            
            Console.WriteLine("Tentando conectar ao serviço RPC_DataAnalyserService...");
            using var channel = GrpcChannel.ForAddress("https://localhost:7038", channelOptions);
            var client = new RPC_DataAnalyserServiceClient.DataAnalysisService.DataAnalysisServiceClient(channel);
            
            // Parse the CSV data to create sensor data points
            var dataPoints = new List<RPC_DataAnalyserServiceClient.SensorData>();
            
            string[] lines = csvData.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string[] values = line.Split(',');
                if (values.Length >= 4)
                {
                    try
                    {
                        var sensorData = new RPC_DataAnalyserServiceClient.SensorData
                        {
                            SensorId = values[0],
                            Value = double.Parse(values[1]),
                            Timestamp = values[2],
                            Unit = values[3]
                        };
                        dataPoints.Add(sensorData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro ao processar linha CSV: {ex.Message}");
                    }
                }
            }
            
            if (dataPoints.Count > 0)
            {
                // Create the analysis request
                var request = new RPC_DataAnalyserServiceClient.AnalysisRequest
                {
                    AnalysisType = "mean",
                    TimeRange = "1h"
                };
                request.DataPoints.AddRange(dataPoints);
                
                // Call the RPC service
                Console.WriteLine($"Enviando {dataPoints.Count} pontos de dados para análise...");
                
                try
                {
                    Console.WriteLine("Chamando serviço RPC AnalyzeDataAsync...");
                    
                    // Set a deadline for the RPC call
                    var deadline = DateTime.UtcNow.AddSeconds(10); // Shorter timeout
                    var callOptions = new CallOptions(deadline: deadline);
                    
                    // Create a task that will complete after the timeout
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
                    
                    // Create the RPC call task
                    var rpcTask = client.AnalyzeDataAsync(request, callOptions);
                    
                    // Wait for either the RPC call to complete or the timeout to occur
                    var completedTask = await Task.WhenAny(rpcTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        throw new TimeoutException("A chamada RPC excedeu o tempo limite.");
                    }
                    
                    var response = await rpcTask;
                    
                    Console.WriteLine("Resposta do serviço RPC recebida.");
                    
                    if (response.Success)
                    {
                        Console.WriteLine($"Análise concluída para WAVY {wavyId}:");
                        foreach (var stat in response.Statistics)
                        {
                            Console.WriteLine($"  {stat.Key}: {stat.Value}");
                        }
                        
                        // Store the analysis results in the database
                        StoreAnalysisResults(wavyId, response.Statistics);
                        
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Erro na análise: {response.ErrorMessage}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro na chamada RPC: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Nenhum ponto de dados válido para analisar.");
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao analisar dados com RPC: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return false;
        }
    }
    
    static void StoreAnalysisResults(string wavyId, IDictionary<string, double> statistics)
    {
        try
        {
            // Here you would store the analysis results in the database
            // For now, we'll just log them
            Console.WriteLine($"Armazenando resultados de análise para WAVY {wavyId}:");
            foreach (var stat in statistics)
            {
                Console.WriteLine($"  {stat.Key}: {stat.Value}");
            }
            
            // You could implement database storage similar to GuardarDadoCSV
            // DatabaseHelper.GuardarResultadosAnalise(wavyId, statistics);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao armazenar resultados de análise: {ex.Message}");
        }
    }

    static void StartServer(int port)
    {
        string localIP = GetLocalIPAddress();

        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"Servidor TCP iniciado em {localIP}:{port}...");

        while (true)
        {
            try
            {
                TcpClient client = server.AcceptTcpClient();
                Thread clientThread = new Thread(HandleClient);
                clientThread.Start(client);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Erro ao aceitar conexão: {ex.Message}");
            }
        }
    }

    static string GetLocalIPAddress()
    {
        string localIP = "127.0.0.1";
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                localIP = ip.ToString();
                break;
            }
        }
        return localIP;
    }

    static void CleanEmptyLinesFromCSV(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Arquivo {filePath} não encontrado.");
                return;
            }

            // Ler todas as linhas do ficheiro, ignorando as linhas em branco
            var nonEmptyLines = File.ReadAllLines(filePath)
                                    .Where(line => !string.IsNullOrWhiteSpace(line))
                                    .ToList();

            // Reescrever o ficheiro apenas com as linhas não vazias
            File.WriteAllLines(filePath, nonEmptyLines);

            Console.WriteLine($"Linhas em branco removidas do ficheiro {filePath}.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao limpar linhas em branco do ficheiro {filePath}: {ex.Message}");
        }
    }

    static async Task<bool> VerifyRpcServiceAvailability()
    {
        try
        {
            // Configure the HttpClient to ignore certificate validation for development
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = 
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
            // Set a short timeout to avoid hanging
            var httpClient = new HttpClient(httpClientHandler)
            {
                Timeout = TimeSpan.FromSeconds(3)
            };
            
            // Try to connect to the RPC service
            Console.WriteLine("Verificando disponibilidade do serviço RPC_DataAnalyserService...");
            var response = await httpClient.GetAsync("https://localhost:7038");
            Console.WriteLine($"RPC service status: {response.StatusCode}");
            
            return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar disponibilidade do serviço RPC: {ex.Message}");
            return false;
        }
    }
    
    static void Main()
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

        DeleteDirectory("ReceivedData");
        
        try
        {
            // Verificar se o serviço RPC está disponível
            bool rpcAvailable = VerifyRpcServiceAvailability().GetAwaiter().GetResult();
            if (!rpcAvailable)
            {
                Console.WriteLine("AVISO: O serviço RPC_DataAnalyserService não parece estar disponível.");
                Console.WriteLine("Certifique-se de que o serviço está em execução na porta 7038.");
                Console.WriteLine("O OceanServer continuará funcionando, mas a análise de dados não será realizada.");
            }
            else
            {
                Console.WriteLine("Serviço RPC_DataAnalyserService está disponível.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar disponibilidade do serviço RPC: {ex.Message}");
            Console.WriteLine("O OceanServer continuará funcionando, mas a análise de dados pode não funcionar corretamente.");
        }

        // Iniciar dois servidores em threads separadas
        Thread server1 = new Thread(() => StartServer(5000));
        Thread server2 = new Thread(() => StartServer(5001));

        server1.Start();
        server2.Start();

        Console.WriteLine("Dois servidores TCP estão em execução...");
    }

    static void DeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                // Apagar a pasta e todo o seu conteúdo
                Directory.Delete(directoryPath, true);
                Console.WriteLine($"Pasta '{directoryPath}' apagada com sucesso.");
            }
            else
            {
                Console.WriteLine($"A pasta '{directoryPath}' não existe.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao apagar a pasta '{directoryPath}': {ex.Message}");
        }
    }
}