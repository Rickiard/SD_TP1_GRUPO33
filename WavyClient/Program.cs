using System.Net;
using System.Net.Sockets;
using System.Text;

class Wavy
{
    static void Main(string[] args)
    {
        //if (args.Length != 2)
        //{
        //    return;
        //}

        string wavyId = "001";
        //string aggregatorIp = args[0];
        //int aggregatorPort = Convert.ToInt32(args[1]);
        string aggregatorIp = GetLocalIPAddress();
        int aggregatorPort = 5000;
        string state; 

        try
        {
            TcpClient client = new TcpClient(aggregatorIp, aggregatorPort);
            NetworkStream stream = client.GetStream();

            // Enviar identificação inicial
            string helloMessage = $"HELLO:WAVY{wavyId}";
            SendMessage(stream, helloMessage);

            // Receber resposta do agregador
            string response = ReceiveMessage(stream);
            Console.WriteLine("AGREGADOR: " + response);

            if (response.StartsWith("ACK"))
            {
                // Enviar requisição de estado
                string statusRequest = $"STATUS_REQUEST:WAVY{wavyId}";
                SendMessage(stream, statusRequest);

                // Receber estado atual
                response = ReceiveMessage(stream);
                state = response.Split(':')[2];
                Console.WriteLine("AGREGADOR: " + response);

                // Enviar dados em CSV
                string csvData = "timestamp,temperature,salinity\n2025-03-13 12:00,22.5,35.1";
                string dataMessage = $"DATA_CSV:WAVY{wavyId}:{csvData}";
                SendMessage(stream, dataMessage);

                // Receber confirmação de envio de dados
                response = ReceiveMessage(stream);
                Console.WriteLine("AGREGADOR: " + response);
            }
            else
            {
                Console.WriteLine(response);
            }

            // Finalizar comunicação
            SendMessage(stream, "QUIT");
            response = ReceiveMessage(stream);
            if (response.StartsWith("100 OK"))
            {
                Console.WriteLine("AGREGADOR: " + response);
                stream.Close();
                client.Close();
            }
            else
            {
                Console.WriteLine("Erro: Desconexão forçada.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Erro: " + e.Message);
        }
    }

    static void SendMessage(NetworkStream stream, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        stream.Write(data, 0, data.Length);
    }

    static string ReceiveMessage(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead);
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

