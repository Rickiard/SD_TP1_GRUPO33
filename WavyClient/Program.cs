using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Wavy
{
    static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Uso: Wavy <IP do Agregador> <Porta>");
            return;
        }

        string wavyId = "001";
        string aggregatorIp = args[0];
        int aggregatorPort = Convert.ToInt32(args[1]);

        try
        {
            using (TcpClient client = new TcpClient(aggregatorIp, aggregatorPort))
            using (NetworkStream stream = client.GetStream())
            {
                // Enviar identificação inicial
                string helloMessage = $"HELLO:WAVY{wavyId}";
                SendMessage(stream, helloMessage);

                // Receber resposta do agregador
                string response = ReceiveMessage(stream);
                Console.WriteLine("AGREGADOR: " + response);

                if (response.StartsWith("ACK"))
                {
                    // Extrair IP do ACK, se necessário
                    string ackIp = response.Split(':')[1];
                    Console.WriteLine($"Conectado ao agregador em {ackIp}");

                    // Solicitar estado atual
                    string statusRequest = $"STATUS_REQUEST:WAVY{wavyId}";
                    SendMessage(stream, statusRequest);

                    response = ReceiveMessage(stream);
                    Console.WriteLine("AGREGADOR: " + response);

                    if (response.StartsWith("CURRENT_STATUS"))
                    {
                        string[] parts = response.Split(':');
                        string state = parts.Length > 2 ? parts[2] : "UNKNOWN";
                        Console.WriteLine($"Estado atual: {state}");
                    }

                    // Ler dados do ficheiro CSV e enviá-los aos poucos
                    if (File.Exists("buoy.csv"))
                    {
                        string[] lines = File.ReadAllLines("buoy.csv");
                        foreach (string line in lines)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                string dataMessage = $"DATA_CSV:WAVY{wavyId}:{line}";
                                SendMessage(stream, dataMessage);

                                response = ReceiveMessage(stream);
                                Console.WriteLine("AGREGADOR: " + response);

                                Thread.Sleep(2000); // Pequeno atraso entre envios (2 segundos)
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Erro: Ficheiro buoy.csv não encontrado.");
                    }
                }
                else if (response.StartsWith("DENIED"))
                {
                    Console.WriteLine("Conexão negada pelo agregador.");
                }

                // Finalizar comunicação
                SendMessage(stream, "QUIT");
                response = ReceiveMessage(stream);
                Console.WriteLine("AGREGADOR: " + response);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Erro: " + e.Message);
        }
    }

    static void SendMessage(NetworkStream stream, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n"); // Garantir quebra de linha
        stream.Write(data, 0, data.Length);
    }

    static string ReceiveMessage(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
    }
}