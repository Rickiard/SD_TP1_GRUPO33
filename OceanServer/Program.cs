using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class TCPServer
{
    private static Mutex mutex = new Mutex();

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
            mutex.ReleaseMutex();

            if (message == "QUIT")
            {
                byte[] byeResponse = Encoding.UTF8.GetBytes("400 BYE\n");
                stream.Write(byeResponse, 0, byeResponse.Length);
                break;
            }
        }

        client.Close();
    }

    static void Main()
    {
        TcpListener server = new TcpListener(IPAddress.Any, 5000);
        server.Start();
        Console.WriteLine("Servidor TCP iniciado na porta 5000...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Thread clientThread = new Thread(HandleClient);
            clientThread.Start(client);
        }
    }
}
