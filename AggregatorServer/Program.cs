using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Agregador
{
    static string ipAgregador;
    static TcpClient serverClient;
    static NetworkStream serverStream;

    // Dicionários para armazenar as configurações
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

            // Carregar ficheiros de configuração
            LoadWavyConfigs("wavy_config.txt");
            LoadPreprocessingConfigs("preprocessing_config.txt");

            // Conectar ao servidor
            serverClient = new TcpClient(IpServer, PORT); // Porta do servidor
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

                    // Encaminhar a mensagem para o servidor, se necessário
                    if (receivedMessage.StartsWith("DATA_CSV"))
                    {
                        SaveWavyDataToFile(receivedMessage);
                        SendMessageToServer(receivedMessage);
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

            // Verificar se a WAVY está configurada
            if (!WavyConfigs.ContainsKey(wavyId))
            {
                Console.WriteLine($"[AGREGADOR] WAVY ID {wavyId} não está configurada.");
                return "DENIED";
            }

            // Atualizar o estado da WAVY para "operação"
            WavyConfigs[wavyId].Status = "operação";
            WavyConfigs[wavyId].LastSync = DateTime.Now;

            return $"ACK:{ipAgregador}";
        }
        else if (message.StartsWith("STATUS_REQUEST:"))
        {
            string wavyId = message.Split(':')[1].Trim();
            Console.WriteLine($"[AGREGADOR] Requisição de status para WAVY ID: {wavyId}");

            // Verificar se a WAVY está configurada
            if (!WavyConfigs.ContainsKey(wavyId))
            {
                Console.WriteLine($"[AGREGADOR] WAVY ID {wavyId} não está configurada.");
                return "DENIED";
            }

            string status = WavyConfigs[wavyId].Status;
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

                // Ler resposta do servidor
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
            // Extrair o ID da WAVY da mensagem
            string[] parts = message.Split(':');
            if (parts.Length < 2 || !parts[0].Equals("DATA_CSV"))
            {
                Console.WriteLine("[AGREGADOR] Formato de mensagem inválido para salvar dados.");
                return;
            }

            string wavyId = parts[1].Trim();
            string data = string.Join(":", parts.Skip(2)); // Restante da mensagem é o conteúdo CSV

            // Nome do ficheiro baseado no ID da WAVY
            string fileName = $"WAVY_{wavyId}.csv";

            // Guardar os dados no ficheiro
            File.AppendAllText(fileName, data + Environment.NewLine);
            Console.WriteLine($"[AGREGADOR] Dados guardados no ficheiro: {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AGREGADOR] Erro ao guardar dados no ficheiro: {ex.Message}");
        }
    }

    static void LoadWavyConfigs(string filePath)
    {
        try
        {
            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split(':');
                if (parts.Length == 4)
                {
                    string wavyId = parts[0];
                    string status = parts[1];
                    string dataTypes = parts[2].Trim('[', ']'); // Remover colchetes
                    string lastSync = parts[3];

                    WavyConfigs[wavyId] = new WavyConfig
                    {
                        Status = status,
                        DataTypes = dataTypes.Split(','),
                        LastSync = DateTime.Parse(lastSync)
                    };
                }
            }
            Console.WriteLine("[CONFIG] Ficheiro de configuração das WAVYs carregado com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CONFIG] Erro ao carregar ficheiro de configuração das WAVYs: {ex.Message}");
        }
    }

    static void LoadPreprocessingConfigs(string filePath)
    {
        try
        {
            foreach (var line in File.ReadAllLines(filePath))
            {
                var parts = line.Split(':');
                if (parts.Length == 4)
                {
                    string wavyId = parts[0];
                    string preprocessing = parts[1];
                    int volumeToSend = int.Parse(parts[2]);
                    string serverAddress = parts[3];

                    PreprocessingConfigs[wavyId] = new PreprocessingConfig
                    {
                        PreprocessingType = preprocessing,
                        VolumeToSend = volumeToSend,
                        ServerAddress = serverAddress
                    };
                }
            }
            Console.WriteLine("[CONFIG] Ficheiro de configuração de pré-processamento carregado com sucesso.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CONFIG] Erro ao carregar ficheiro de configuração de pré-processamento: {ex.Message}");
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

// Classe para representar a configuração de uma WAVY
public class WavyConfig
{
    public string Status { get; set; }
    public string[] DataTypes { get; set; }
    public DateTime LastSync { get; set; }
}

// Classe para representar a configuração de pré-processamento
public class PreprocessingConfig
{
    public string PreprocessingType { get; set; }
    public int VolumeToSend { get; set; }
    public string ServerAddress { get; set; }
}

// Exemplo de conteúdo dos ficheiros de configuração:

//Ficheiro de Configuração das WAVYs (wavy_config.txt)
//WAVY001: operação: [acelerometro, giroscopio]:2024 - 03 - 25T14: 30:00
//WAVY002: manutenção: [hidrofone]:2024 - 03 - 20T10: 15:00
//WAVY003: desativada: [camera]:2024 - 03 - 10T08: 00:00

//Ficheiro de Pré-processamento (preprocessing_config.txt)
//WAVY001: filtragem: 1024:192.168.1.10
//WAVY002: agregacao: 2048:192.168.1.20
//WAVY003: normalizacao: 512:192.168.1.30