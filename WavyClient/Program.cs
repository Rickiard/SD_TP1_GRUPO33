using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Wavy
{
    const int MAX_RETRIES = 5; // Número máximo de tentativas de reconexão
    const int RETRY_DELAY_MS = 5000; // Tempo de espera entre tentativas (em milissegundos)
    const int CONNECTION_TIMEOUT_MS = 10000; // Timeout para estabelecer conexão (em milissegundos)
    const string PROGRESS_FILE = "progress.txt"; // Ficheiro para guardar o progresso

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

        TcpClient client = null;
        NetworkStream stream = null;

        try
        {
            // Carregar o progresso salvo
            int lastProcessedLine = LoadProgress();
            Console.WriteLine($"Última linha processada: {lastProcessedLine}");

            client = ConnectToAggregator(aggregatorIp, aggregatorPort);
            stream = client.GetStream();

            // Enviar identificação inicial
            string helloMessage = $"HELLO:WAVY{wavyId}";
            SendMessage(stream, helloMessage);

            // Receber resposta do agregador
            string response = ReceiveMessageWithRetry(stream);
            Console.WriteLine("AGREGADOR: " + response);

            if (response.StartsWith("ACK"))
            {
                // Extrair IP do ACK, se necessário
                string ackIp = response.Split(':')[1];
                Console.WriteLine($"Conectado ao agregador em {ackIp}");

                // Solicitar estado atual
                string statusRequest = $"STATUS_REQUEST:WAVY{wavyId}";
                SendMessage(stream, statusRequest);

                response = ReceiveMessageWithRetry(stream);
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
                    for (int i = lastProcessedLine; i < lines.Length; i++)
                    {
                        string line = lines[i];

                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            bool success = false;

                            while (!success)
                            {
                                try
                                {
                                    string dataMessage = $"DATA_CSV:WAVY{wavyId}:{line}";
                                    SendMessage(stream, dataMessage);

                                    response = ReceiveMessageWithRetry(stream);
                                    Console.WriteLine("AGREGADOR: " + response);

                                    if (response.StartsWith("ACK"))
                                    {
                                        success = true;
                                        SaveProgress(i); // Guardar progresso após envio bem-sucedido
                                    }
                                    else
                                    {
                                        Console.WriteLine("Resposta inesperada do agregador. Tentando novamente...");
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"Erro durante o envio: {e.Message}. Tentando reconectar...");
                                    Reconnect(ref client, ref stream, aggregatorIp, aggregatorPort);
                                }
                            }

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
            response = ReceiveMessageWithRetry(stream);
            Console.WriteLine("AGREGADOR: " + response);
        }
        catch (Exception e)
        {
            Console.WriteLine("Erro crítico: " + e.Message);
        }
        finally
        {
            stream?.Close();
            client?.Close();
        }
    }

    static TcpClient ConnectToAggregator(string ip, int port)
    {
        TcpClient client = null;
        int retryCount = 0;

        while (retryCount < MAX_RETRIES)
        {
            try
            {
                client = new TcpClient();
                var result = client.BeginConnect(ip, port, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(CONNECTION_TIMEOUT_MS, true);

                if (success && client.Connected)
                {
                    Console.WriteLine("Conexão estabelecida com sucesso.");
                    return client;
                }
                else
                {
                    client.Close();
                    throw new SocketException((int)SocketError.TimedOut);
                }
            }
            catch (Exception e)
            {
                retryCount++;
                Console.WriteLine($"Falha ao conectar ({retryCount}/{MAX_RETRIES}): {e.Message}");
                if (retryCount >= MAX_RETRIES)
                {
                    throw new Exception("Número máximo de tentativas de conexão excedido.");
                }
                Thread.Sleep(RETRY_DELAY_MS);
            }
        }

        throw new Exception("Não foi possível conectar ao agregador após várias tentativas.");
    }

    static void Reconnect(ref TcpClient client, ref NetworkStream stream, string ip, int port)
    {
        Console.WriteLine("Tentando reconectar...");

        if (stream != null)
        {
            stream.Close();
            stream = null;
        }

        if (client != null)
        {
            client.Close();
            client = null;
        }

        client = ConnectToAggregator(ip, port);
        stream = client.GetStream();

        Console.WriteLine("Reconexão bem-sucedida.");
    }

    static void SendMessage(NetworkStream stream, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n"); // Garantir quebra de linha
        stream.Write(data, 0, data.Length);
    }

    static string ReceiveMessageWithRetry(NetworkStream stream)
    {
        int retryCount = 0;

        while (retryCount < MAX_RETRIES)
        {
            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    throw new IOException("Conexão fechada pelo servidor.");
                }

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                if (string.IsNullOrEmpty(message))
                {
                    throw new IOException("Mensagem recebida inválida.");
                }

                return message;
            }
            catch (Exception e)
            {
                retryCount++;
                Console.WriteLine($"Falha ao receber mensagem ({retryCount}/{MAX_RETRIES}): {e.Message}");
                if (retryCount >= MAX_RETRIES)
                {
                    throw new Exception("Número máximo de tentativas de recepção excedido.");
                }
                Thread.Sleep(RETRY_DELAY_MS);
            }
        }

        throw new Exception("Não foi possível receber mensagem após várias tentativas.");
    }

    static int LoadProgress()
    {
        if (File.Exists(PROGRESS_FILE))
        {
            string content = File.ReadAllText(PROGRESS_FILE);
            if (int.TryParse(content, out int progress))
            {
                return progress;
            }
        }

        return 0; // Se não houver progresso guardado, começar do início
    }

    static void SaveProgress(int lineNumber)
    {
        File.WriteAllText(PROGRESS_FILE, lineNumber.ToString());
    }
}