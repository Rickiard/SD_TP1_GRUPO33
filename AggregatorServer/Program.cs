using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class AgregadorManager
{
    static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("Uso correto: AgregadorManager <IP_SERVIDOR> <PORTA_SERVIDOR_1> <PORTA_SERVIDOR_2>");
            return;
        }

        string IpServer = args[0]; // IP do servidor
        int serverPort1 = Convert.ToInt32(args[1]); // Porta do servidor 1 (ex.: 5000)
        int serverPort2 = Convert.ToInt32(args[2]); // Porta do servidor 2 (ex.: 5001)

        // Iniciar 3 agregadores nas portas 4000, 4001 e 4002
        Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4000));
        Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4001));
        Task.Run(() => StartAgregador(IpServer, serverPort1, serverPort2, 4002));

        Console.WriteLine("[MANAGER] Todos os agregadores foram iniciados. Pressione ENTER para sair.");
        Console.ReadLine();
    }

    static void StartAgregador(string IpServer, int serverPort1, int serverPort2, int aggregatorPort)
    {
        try
        {
            string aggregatorId = $"Agregador_{aggregatorPort}";
            string ipAgregador = GetLocalIPAddress();

            // Conectar ao servidor menos ocupado
            int selectedServerPort = SelectLeastBusyServer(IpServer, serverPort1, serverPort2);
            TcpClient serverClient = new TcpClient(IpServer, selectedServerPort);
            NetworkStream serverStream = serverClient.GetStream();
            Console.WriteLine($"[{aggregatorId}] Conectado ao SERVIDOR na porta {selectedServerPort}!");

            // Iniciar listener para WAVYs
            IPAddress ipAddress = IPAddress.Parse(ipAgregador);
            TcpListener listener = new TcpListener(ipAddress, aggregatorPort);
            listener.Start();
            Console.WriteLine($"[{aggregatorId}] Aguardando conexões em {ipAgregador} na porta {aggregatorPort}...");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine($"[{aggregatorId}] Cliente WAVY conectado!");

                // Gerir a comunicação com o cliente de forma contínua
                Task.Run(() =>
                {
                    using (client)
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] buffer = new byte[1024];
                        while (true)
                        {
                            try
                            {
                                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                                if (bytesRead == 0)
                                {
                                    // Cliente fechou a conexão
                                    Console.WriteLine($"[{aggregatorId}] Cliente WAVY desconectado.");
                                    break;
                                }

                                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                Console.WriteLine($"[{aggregatorId}] Mensagem recebida: {receivedMessage}");
                                string response = ProcessMessage(receivedMessage, aggregatorId);
                                byte[] responseData = Encoding.UTF8.GetBytes(response);
                                stream.Write(responseData, 0, responseData.Length);
                                Console.WriteLine($"[{aggregatorId}] Resposta enviada: {response}");

                                if (receivedMessage.StartsWith("DATA_CSV"))
                                {
                                    SaveWavyDataToFile(receivedMessage, aggregatorId);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[{aggregatorId}] Erro: {ex.Message}");
                                break;
                            }
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao iniciar agregador na porta {aggregatorPort}: {ex.Message}");
        }
    }

    static int SelectLeastBusyServer(string IpServer, int port1, int port2)
    {
        try
        {
            int queueLength1 = GetServerQueueLength(IpServer, port1);
            int queueLength2 = GetServerQueueLength(IpServer, port2);

            return queueLength1 <= queueLength2 ? port1 : port2;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar fila dos servidores: {ex.Message}");
            return port1; // Retorna o primeiro servidor como fallback
        }
    }

    static int GetServerQueueLength(string IpServer, int port)
    {
        try
        {
            using (TcpClient tempClient = new TcpClient(IpServer, port))
            using (NetworkStream tempStream = tempClient.GetStream())
            {
                byte[] request = Encoding.UTF8.GetBytes("QUEUE_LENGTH");
                tempStream.Write(request, 0, request.Length);

                byte[] buffer = new byte[1024];
                int bytesRead = tempStream.Read(buffer, 0, buffer.Length);
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (int.TryParse(response, out int queueLength))
                {
                    return queueLength;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar fila do servidor na porta {port}: {ex.Message}");
        }

        return int.MaxValue; // Retorna um valor alto se falhar
    }

    static string ProcessMessage(string message, string aggregatorId)
    {
        if (message.StartsWith("HELLO:"))
        {
            string wavyId = message.Split(':')[1].Trim();
            Console.WriteLine($"[{aggregatorId}] WAVY ID recebido: {wavyId}");
            if (!IsWavyConfigured(wavyId, aggregatorId))
            {
                Console.WriteLine($"[{aggregatorId}] WAVY ID {wavyId} não está configurada.");
                return "DENIED";
            }
            UpdateWavyStatus(wavyId, "operação", aggregatorId);
            return $"ACK:{GetLocalIPAddress()}";
        }
        else if (message.StartsWith("STATUS_REQUEST:"))
        {
            string wavyId = message.Split(':')[1].Trim();
            Console.WriteLine($"[{aggregatorId}] Requisição de status para WAVY ID: {wavyId}");
            string status = GetWavyStatus(wavyId, aggregatorId);
            if (status == null)
            {
                Console.WriteLine($"[{aggregatorId}] WAVY ID {wavyId} não está configurada.");
                return "DENIED";
            }
            return $"CURRENT_STATUS:{wavyId}:{status}";
        }
        else if (message.StartsWith("DATA_CSV:"))
        {
            Console.WriteLine($"[{aggregatorId}] Dados CSV recebidos.");
            return "ACK";
        }
        else if (message.Equals("QUIT"))
        {
            Console.WriteLine($"[{aggregatorId}] Finalizando conexão.");
            return "100 OK";
        }
        else
        {
            Console.WriteLine($"[{aggregatorId}] Mensagem desconhecida recebida.");
            return "DENIED";
        }
    }

    static void SaveWavyDataToFile(string message, string aggregatorId)
    {
        try
        {
            string[] parts = message.Split(':');
            if (parts.Length < 2 || !parts[0].Equals("DATA_CSV"))
            {
                Console.WriteLine($"[{aggregatorId}] Formato de mensagem inválido para salvar dados.");
                return;
            }

            string wavyId = parts[1].Trim();
            string data = string.Join(":", parts.Skip(2));
            string fileName = $"{aggregatorId}_WAVY_{wavyId}.csv"; // Nome único para o arquivo
            File.AppendAllText(fileName, data + Environment.NewLine);
            Console.WriteLine($"[{aggregatorId}] Dados guardados no ficheiro: {fileName}");

            int? volumeToSend = GetVolumeToSend(wavyId, aggregatorId);
            if (volumeToSend.HasValue)
            {
                int currentLines = File.ReadAllLines(fileName).Length;
                if (currentLines >= volumeToSend.Value)
                {
                    string aggregatedData = AggregateData(fileName);
                    SendMessageToServer($"DATA_CSV:{wavyId}:{aggregatedData}", aggregatorId);
                    File.WriteAllText(fileName, string.Empty);
                    Console.WriteLine($"[{aggregatorId}] Dados agregados enviados e ficheiro {fileName} limpo.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{aggregatorId}] Erro ao guardar ou enviar dados: {ex.Message}");
        }
    }

    static string AggregateData(string fileName)
    {
        var lines = File.ReadAllLines(fileName);
        // Implementar lógica de agregação aqui
        // Exemplo: concatenar todas as linhas
        return string.Join(Environment.NewLine, lines);
    }

    static bool IsWavyConfigured(string wavyId, string aggregatorId)
    {
        string configFileName = $"{aggregatorId}_wavy_config.txt"; // Nome único para o arquivo de configuração
        foreach (var line in File.ReadAllLines(configFileName))
        {
            var parts = line.Split(':');
            if (parts[0].Trim() == wavyId)
                return true;
        }
        return false;
    }

    static string GetWavyStatus(string wavyId, string aggregatorId)
    {
        string configFileName = $"{aggregatorId}_wavy_config.txt"; // Nome único para o arquivo de configuração
        foreach (var line in File.ReadAllLines(configFileName))
        {
            var parts = line.Split(':');
            if (parts[0].Trim() == wavyId && parts.Length >= 2)
                return parts[1].Trim();
        }
        return null;
    }

    static void UpdateWavyStatus(string wavyId, string newStatus, string aggregatorId)
    {
        string configFileName = $"{aggregatorId}_wavy_config.txt"; // Nome único para o arquivo de configuração
        var lines = File.ReadAllLines(configFileName);
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
        File.WriteAllLines(configFileName, lines);
    }

    static int? GetVolumeToSend(string wavyId, string aggregatorId)
    {
        string preprocessingFileName = $"{aggregatorId}_preprocessing_config.txt"; // Nome único para o arquivo de pré-processamento
        foreach (var line in File.ReadAllLines(preprocessingFileName))
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

    static void SendMessageToServer(string message, string aggregatorId)
    {
        try
        {
            string IpServer = "127.0.0.1"; // Substitua pelo IP do servidor real, se necessário
            int PORT = 5000; // Substitua pela porta do servidor real, se necessário
            using (TcpClient serverClient = new TcpClient(IpServer, PORT))
            using (NetworkStream serverStream = serverClient.GetStream())
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                serverStream.Write(data, 0, data.Length);
                Console.WriteLine($"[{aggregatorId}] Mensagem encaminhada ao SERVIDOR.");
                byte[] buffer = new byte[1024];
                int bytesRead = serverStream.Read(buffer, 0, buffer.Length);
                string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"[{aggregatorId}] Resposta do SERVIDOR: {serverResponse}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{aggregatorId}] Erro ao enviar para SERVIDOR: {ex.Message}");
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