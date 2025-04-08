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
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];
        int bytesRead;

        // Enviar resposta inicial "100 OK"
        byte[] okResponse = Encoding.UTF8.GetBytes("100 OK\n");
        stream.Write(okResponse, 0, okResponse.Length);

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            mutex.WaitOne();
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
            mutex.ReleaseMutex();
        }

        client.Close();
    }

    static void ProcessCSVData(string message)
    {
        string[] parts = message.Split(':');
        if (parts.Length < 3) return;

        string wavyID = parts[1];
        string csvData = parts[2];

        string filePath = Path.Combine(DataDirectory, $"WAVY_{wavyID}.csv");
        Directory.CreateDirectory(DataDirectory);

        mutex.WaitOne();
        File.AppendAllText(filePath, csvData + "\n");
        Console.WriteLine($"Dados de {wavyID} armazenados em {filePath}");
        mutex.ReleaseMutex();
    }

    static void StartServer(int port)
    {
        string localIP = GetLocalIPAddress();

        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"Servidor TCP iniciado em {localIP}:{port}...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Thread clientThread = new Thread(HandleClient);
            clientThread.Start(client);
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

    static void Main()
    {
        // Iniciar dois servidores em threads separadas
        Thread server1 = new Thread(() => StartServer(5000));
        Thread server2 = new Thread(() => StartServer(5001));

        server1.Start();
        server2.Start();

        Console.WriteLine("Dois servidores TCP estão em execução...");
    }
}
