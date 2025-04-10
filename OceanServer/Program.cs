using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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

    static void ProcessCSVData(string message)
    {
        string[] parts = message.Split(':', 3); // Dividir apenas em duas partes
        if (parts.Length < 2) return;

        string csvData = parts[2]; // O CSV completo está na segunda parte

        if (string.IsNullOrWhiteSpace(csvData))
        {
            Console.WriteLine("Linha em branco ignorada.");
            return;
        }

        string filePath = Path.Combine(DataDirectory, $"{parts[1]}.csv");
        Directory.CreateDirectory(DataDirectory);

        bool hasMutex = false;
        try
        {
            mutex.WaitOne();
            hasMutex = true;

            File.AppendAllText(filePath, csvData + "\n");
            Console.WriteLine($"Dados agregados armazenados em {filePath}");

            // Limpar linhas em branco do ficheiro CSV
            CleanEmptyLinesFromCSV(filePath);
        }
        finally
        {
            if (hasMutex)
            {
                mutex.ReleaseMutex();
            }
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

    static void Main()
    {
        DeleteDirectory("ReceivedData");

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