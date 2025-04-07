using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Agregador
{
    static string ipAgregador;
    static TcpClient serverClient;
    static NetworkStream serverStream;

    static Dictionary<string, WavyConfig> WavyConfigs = new Dictionary<string, WavyConfig>();
    static Dictionary<string, PreprocessingConfig> PreprocessingConfigs = new Dictionary<string, PreprocessingConfig>();

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

            LoadWavyConfigs("wavy_config.txt");
            LoadPreprocessingConfigs("preprocessing_config.txt");

            serverClient = new TcpClient(IpServer, PORT);
            serverStream = serverClient.GetStream();
            Console.WriteLine("[AGREGADOR] Conectado ao SERVIDOR!");

            IPAddress ipAddress = IPAddress.Parse(ipAgregador);
            TcpListener listener = new TcpListener(ipAddress, PORT);
            listener.Start();
            Console.WriteLine($"[AGREGADOR] Aguardando conexões em {ipAgregador}:{PORT}");

            while (true)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                {
                    Console.WriteLine("[AGREGADOR] WAVY conectada.");
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[AGREGADOR] Mensagem recebida: {receivedMessage}");

                    string response = ProcessMessage(receivedMessage);
                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                    Console.WriteLine($"[AGREGADOR] Resposta enviada: {response}");

                    if (receivedMessage.StartsWith("DATA_CSV:"))
                    {
                        SaveWavyDataToFile(receivedMessage);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AGREGADOR] Erro: {ex.Message}");
        }
    }

    static string ProcessMessage(string message)
    {
        if (message.StartsWith("HELLO:"))
        {
            string wavyId = message.Split(':')[1].Trim();
            if (WavyConfigs.ContainsKey(wavyId))
            {
                WavyConfigs[wavyId].Status = "operação";
                WavyConfigs[wavyId].LastSync = DateTime.Now;
                return $"ACK:{ipAgregador}";
            }
            return "DENIED";
        }
        else if (message.StartsWith("STATUS_REQUEST:"))
        {
            string wavyId = message.Split(':')[1].Trim();
            if (WavyConfigs.ContainsKey(wavyId))
            {
                string status = WavyConfigs[wavyId].Status;
                return $"CURRENT_STATUS:{wavyId}:{status}";
            }
            return "DENIED";
        }
        else if (message.StartsWith("DATA_CSV:"))
        {
            return "ACK";
        }
        else if (message == "QUIT")
        {
            return "100 OK";
        }
        else
        {
            return "DENIED";
        }
    }

    static void SaveWavyDataToFile(string message)
    {
        try
        {
            string[] parts = message.Split(':');
            if (parts.Length < 3)
            {
                Console.WriteLine("[AGREGADOR] Mensagem CSV mal formatada.");
                return;
            }

            string wavyId = parts[1].Trim();
            string data = string.Join(":", parts.Skip(2));
            string fileName = $"WAVY_{wavyId}.csv";

            File.AppendAllText(fileName, data + Environment.NewLine);
            Console.WriteLine($"[AGREGADOR] Dados guardados em {fileName}");

            if (PreprocessingConfigs.ContainsKey(wavyId))
            {
                int volumeToSend = PreprocessingConfigs[wavyId].VolumeToSend;
                int currentLines = File.ReadAllLines(fileName).Length;

                if (currentLines >= volumeToSend)
                {
                    string fullData = File.ReadAllText(fileName);
                    SendMessageToServer($"DATA_CSV:{wavyId}:{fullData}");
                    File.WriteAllText(fileName, string.Empty);
                    Console.WriteLine($"[AGREGADOR] Dados enviados ao servidor e ficheiro {fileName} limpo.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AGREGADOR] Erro ao salvar dados: {ex.Message}");
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

                byte[] buffer = new byte[1024];
                int bytesRead = serverStream.Read(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"[AGREGADOR] Resposta do servidor: {response}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AGREGADOR] Erro ao comunicar com o servidor: {ex.Message}");
        }
    }

    static void LoadWavyConfigs(string filePath)
    {
        try
        {
            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split(':');
                if (parts.Length >= 4)
                {
                    string wavyId = parts[0].Trim();
                    string status = parts[1].Trim();
                    string dataTypes = parts[2].Trim('[', ']', ' ');
                    string lastSync = parts[3].Trim();

                    WavyConfigs[wavyId] = new WavyConfig
                    {
                        Status = status,
                        DataTypes = dataTypes.Split(','),
                        LastSync = DateTime.Parse(lastSync)
                    };
                }
            }
            Console.WriteLine("[CONFIG] WAVY configs carregadas.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CONFIG] Erro ao carregar WAVY configs: {ex.Message}");
        }
    }

    static void LoadPreprocessingConfigs(string filePath)
    {
        try
        {
            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split(':');
                if (parts.Length >= 4)
                {
                    string wavyId = parts[0].Trim();
                    string preprocessing = parts[1].Trim();
                    int volume = int.Parse(parts[2].Trim());
                    string serverAddress = parts[3].Trim();

                    PreprocessingConfigs[wavyId] = new PreprocessingConfig
                    {
                        PreprocessingType = preprocessing,
                        VolumeToSend = volume,
                        ServerAddress = serverAddress
                    };
                }
            }
            Console.WriteLine("[CONFIG] Preprocessing configs carregadas.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CONFIG] Erro ao carregar Preprocessing configs: {ex.Message}");
        }
    }

    static string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        throw new Exception("IP local não encontrado.");
    }
}

public class WavyConfig
{
    public string Status { get; set; }
    public string[] DataTypes { get; set; }
    public DateTime LastSync { get; set; }
}

public class PreprocessingConfig
{
    public string PreprocessingType { get; set; }
    public int VolumeToSend { get; set; }
    public string ServerAddress { get; set; }
}