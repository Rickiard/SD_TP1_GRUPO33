using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Agregador
{
    static string ipAgregador;
    static TcpClient serverClient;
    static NetworkStream serverStream;

    static void Main(string[] args)
    {
        int PORT;
        string IpServer;

        if (args.Length == 2)
        {
            PORT = Convert.ToInt32(args[1]);
            IpServer = args[0];
        }
        else
        {
            Console.WriteLine("Uso correto: Agregador <IP> <PORT>");
            return;
        }

        try
        {
            ipAgregador = GetLocalIPAddress();

            // Conectar ao servidor
            serverClient = new TcpClient(IpServer, 6000); // Porta do servidor
            serverStream = serverClient.GetStream();
            Console.WriteLine("[AGREGADOR] Conectado ao SERVIDOR!");

            // Iniciar listener para WAVYs
            IPAddress ipAddress = IPAddress.Parse(IpServer);
            TcpListener listener = new TcpListener(ipAddress, PORT);
            listener.Start();
            Console.WriteLine($"[AGREGADOR] Aguardando conexões na porta {PORT}...");

            while (true)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    Console.WriteLine("[AGREGADOR] Cliente WAVY conectado!");

                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[AGREGADOR] Mensagem recebida: {receivedMessage}");

                    string response = ProcessMessage(receivedMessage);
                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                    Console.WriteLine($"[AGREGADOR] Resposta enviada: {response}");

                    // Encaminhar a mensagem para o servidor
                    SendMessageToServer(receivedMessage);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AGREGADOR] Erro inesperado: {ex.Message}");
        }
    }

    static string ProcessMessage(string message)
    {
        if (message.StartsWith("HELLO, I AM WAVY"))
        {
            return $"ACK,'{ipAgregador}'";
        }
        else if (message.StartsWith("STATUS_REQUEST"))
        {
            return "CURRENT_STATUS:'WAVY_ID':'STATUS'";
        }
        else if (message.StartsWith("DATA_CSV"))
        {
            return "ACK";
        }
        else if (message.Equals("QUIT"))
        {
            return "100 OK";
        }
        else
        {
            return "YOU ARE NOT MY WAVY Or I DON’T KNOW YOU!!!";
        }
    }

    static void SendMessageToServer(string message)
    {
        try
        {
            if (serverClient.Connected)
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                serverStream.Write(data, 0, data.Length);
                Console.WriteLine("[AGREGADOR] Mensagem enviada ao SERVIDOR.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AGREGADOR] Erro ao enviar para SERVIDOR: {ex.Message}");
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
}


