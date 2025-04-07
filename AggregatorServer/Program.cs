using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;

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
            IpServer = args[0];
            PORT = Convert.ToInt32(args[1]);
        }
        else
        {
            Console.WriteLine("Uso correto: Agregador <IP_SERVIDOR> <PORTA>");
            return;
        }

        try
        {
            ipAgregador = GetLocalIPAddress();

            // Conectar ao servidor
            serverClient = new TcpClient(IpServer, PORT);
            serverStream = serverClient.GetStream();
            Console.WriteLine("[AGREGADOR] Conectado ao SERVIDOR!");

            // Iniciar listener para WAVYs
            IPAddress ipAddress = IPAddress.Parse(ipAgregador);
            TcpListener listener = new TcpListener(ipAddress, PORT);
            listener.Start();
            Console.WriteLine($"[AGREGADOR] Aguardando conexões em {ipAgregador} na porta {PORT}...");

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

                    if (receivedMessage.StartsWith("DATA_CSV"))
                    {
                        SaveWavyDataToFile(receivedMessage);
                    }
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
        if (message.StartsWith("HELLO:"))
        {
            string wavyId = message.Split(':')[1].Trim();
            Console.WriteLine($"[AGREGADOR] WAVY ID recebido: {wavyId}");

            if (!IsWavyConfigured(wavyId))
            {
                Console.WriteLine($"[AGREGADOR] WAVY ID {wavyId} não está configurada.");
                return "DENIED";
            }

            UpdateWavyStatus(wavyId, "operação");
            return $"ACK:{ipAgregador}";
        }
        else if (message.StartsWith("STATUS_REQUEST:"))
        {
            string wavyId = message.Split(':')[1].Trim();
            Console.WriteLine($"[AGREGADOR] Requisição de status para WAVY ID: {wavyId}");

            string status = GetWavyStatus(wavyId);
            if (status == null)
            {
                Console.WriteLine($"[AGREGADOR] WAVY ID {wavyId} não está configurada.");
                return "DENIED";
            }

            return $"CURRENT_STATUS:{wavyId}:{status}";
        }
        else if (message.StartsWith("DATA_CSV:"))
        {
            Console.WriteLine("[AGREGADOR] Dados CSV recebidos.");
            return "ACK";
        }
        else if (message.Equals("QUIT"))
        {
            Console.WriteLine("[AGREGADOR] Finalizando conexão.");
            return "100 OK";
        }
        else
        {
            Console.WriteLine("[AGREGADOR] Mensagem desconhecida recebida.");
            return "DENIED";
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
                Console.WriteLine("[AGREGADOR] Mensagem encaminhada ao SERVIDOR.");

                byte[] buffer = new byte[1024];
                int bytesRead = serverStream.Read(buffer, 0, buffer.Length);
                string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"[AGREGADOR] Resposta do SERVIDOR: {serverResponse}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AGREGADOR] Erro ao enviar para SERVIDOR: {ex.Message}");
        }
    }

    static void SaveWavyDataToFile(string message)
    {
        try
        {
            string[] parts = message.Split(':');
            if (parts.Length < 2 || !parts[0].Equals("DATA_CSV"))
            {
                Console.WriteLine("[AGREGADOR] Formato de mensagem inválido para salvar dados.");
                return;
            }

            string wavyId = parts[1].Trim();
            string data = string.Join(":", parts.Skip(2));
            string fileName = $"WAVY_{wavyId}.csv";

            File.AppendAllText(fileName, data + Environment.NewLine);
            Console.WriteLine($"[AGREGADOR] Dados guardados no ficheiro: {fileName}");

            int? volumeToSend = GetVolumeToSend(wavyId);
            if (volumeToSend.HasValue)
            {
                int currentLines = File.ReadAllLines(fileName).Length;
                if (currentLines >= volumeToSend.Value)
                {
                    string aggregatedData = AggregateData(fileName);
                    SendMessageToServer($"DATA_CSV:{wavyId}:{aggregatedData}");

                    File.WriteAllText(fileName, string.Empty);
                    Console.WriteLine($"[AGREGADOR] Dados agregados enviados e ficheiro {fileName} limpo.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AGREGADOR] Erro ao guardar ou enviar dados: {ex.Message}");
        }
    }

    static string AggregateData(string fileName)
    {
        var lines = File.ReadAllLines(fileName);
        // Implementar lógica de agregação aqui
        // Exemplo: concatenar todas as linhas
        return string.Join(Environment.NewLine, lines);
    }

    static bool IsWavyConfigured(string wavyId)
    {
        foreach (var line in File.ReadAllLines("wavy_config.txt"))
        {
            var parts = line.Split(':');
            if (parts[0].Trim() == wavyId)
                return true;
        }
        return false;
    }

    static string GetWavyStatus(string wavyId)
    {
        foreach (var line in File.ReadAllLines("wavy_config.txt"))
        {
            var parts = line.Split(':');
            if (parts[0].Trim() == wavyId && parts.Length >= 2)
                return parts[1].Trim();
        }
        return null;
    }

    static void UpdateWavyStatus(string wavyId, string newStatus)
    {
        var lines = File.ReadAllLines("wavy_config.txt");
        for (int i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split(':');
            if (parts[0].Trim() == wavyId && parts.Length == 4)
            {
                parts[1] = $" {newStatus}";
                parts[3] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                lines[i] = string.Join(":", parts);
                break;
            }
        }
        File.WriteAllLines("wavy_config.txt", lines);
    }

    static int? GetVolumeToSend(string wavyId)
    {
        foreach (var line in File.ReadAllLines("preprocessing_config.txt"))
        {
            var parts = line.Split(':');
            if (parts[0].Trim() == wavyId && parts.Length >= 3)
            {
                if (int.TryParse(parts[2], out int volume))
                    return volume;
            }
        }
        return null;
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
