using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class Program
{
    public static void Main(string[] args)
    {
        // Iniciar servidores TCP nas portas 5000 e 5001
        Task.Run(() => StartTcpServer(5000));
        Task.Run(() => StartTcpServer(5001));

        // Manter a aplicação rodando
        Console.WriteLine("Servidores TCP iniciados. Pressione Ctrl+C para encerrar.");
        Console.ReadLine();
    }

    public static void StartTcpServer(int port)
    {
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"Servidor TCP iniciado na porta {port}...");
        while (true)
        {
            try
            {
                TcpClient client = server.AcceptTcpClient();
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[TCP {port}] Dados recebidos: {received}");
                }
                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP {port}] Erro: {ex.Message}");
            }
        }
    }
}