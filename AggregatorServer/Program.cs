using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Agregador
{

    static string ipAgregador;

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

            IPAddress ipAddress = IPAddress.Parse(IpServer);
            TcpListener listener = new TcpListener(ipAddress, PORT);
            listener.Start();
            Console.WriteLine($"AGREGADOR aguardando conexões na porta {PORT}...");

            while (true)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    Console.WriteLine("Cliente conectado!");

                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Mensagem recebida: {receivedMessage}");

                    string response = ProcessMessage(receivedMessage);
                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                    Console.WriteLine($"Resposta enviada: {response}");
                }
            }
        }
        catch (FormatException)
        {
            Console.WriteLine("Erro: O endereço IP fornecido não é válido.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro inesperado");
        }
    }
     static string ProcessMessage(string message)
    {
        if (message.StartsWith("HELLO, I AM WAVY"))
        {
            return $"ACK,'{ipAgregador}'"; // Confirmação da conexão
        }
        else if (message.StartsWith("STATUS_REQUEST"))
        {
            return "CURRENT_STATUS:'WAVY_ID':'STATUS'"; // Envio de status atual
        }
        else if (message.StartsWith("DATA_CSV"))
        {
            return "ACK"; // Confirmação da recepção de dados CSV
        }
        else if (message.Equals("QUIT"))
        {
            return "100 OK"; // Finaliza a comunicação
        }
        else
        {
            return "YOU ARE NOT MY WAVY Or I DON’T KNOW YOU!!!"; // Rejeição de conexão desconhecida
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


