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
    static Mutex mutex = new Mutex(); // Mutex para sincronizar acesso ao ficheiro de progresso

    static void Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Uso: Wavy <IP do Agregador> <Porta>");
            return;
        }

        // Criar threads para executar as WAVYs simultaneamente
        Thread wavy1 = new Thread(() => ExecuteWavy("001", args[0], args[1], "buoy - Cópia.csv"));
        Thread wavy2 = new Thread(() => ExecuteWavy("002", args[0], args[2], "buoy - Cópia (2).csv"));
        Thread wavy3 = new Thread(() => ExecuteWavy("003", args[0], args[3], "buoy - Cópia (3).csv"));
        Thread wavy4 = new Thread(() => ExecuteWavy("004", args[0], args[3], "buoy - Cópia (4).csv"));

        // Iniciar as threads
        wavy1.Start();
        wavy2.Start();
        wavy3.Start();
        wavy4.Start();

        // Aguardar a conclusão das threads
        wavy1.Join();
        wavy2.Join();
        wavy3.Join();
        wavy4.Join();

        Console.WriteLine("Todas as WAVYs foram concluídas.");
    }

    static void ExecuteWavy(string id, string ip, string port, string filename)
    {
        bool wavy = true; // Variável local para controlar o estado da WAVY
        string progressFile = $"progress_WAVY{id}.txt"; // Arquivo de progresso específico para cada WAVY

        // Tentar executar a WAVY até 3 vezes, se necessário
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            Console.WriteLine($"Iniciando WAVY{id}, tentativa {attempt}...");
            StartWavy(id, ip, port, filename, ref wavy, progressFile);

            if (wavy)
            {
                Console.WriteLine($"WAVY{id} concluída com sucesso.");
                break;
            }

            Console.WriteLine($"WAVY{id} falhou na tentativa {attempt}. Retentando...");
        }

        // Reiniciar o estado para a próxima execução
        mutex.WaitOne();
        try
        {
            if (File.Exists(progressFile))
            {
                File.Delete(progressFile);
            }
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    static void StartWavy(string id, string ip, string port, string filename, ref bool wavy, string progressFile)
    {
        string wavyId = id;
        string aggregatorIp = ip;
        int aggregatorPort = Convert.ToInt32(port);

        TcpClient client = null;
        NetworkStream stream = null;

        try
        {
            // Carregar o progresso salvo
            int lastProcessedLine = LoadProgress(progressFile);
            Console.WriteLine($"Última linha processada para WAVY{id}: {lastProcessedLine}");

            client = ConnectToAggregator(aggregatorIp, aggregatorPort);
            stream = client.GetStream();

            // Enviar identificação inicial
            string helloMessage = $"HELLO:WAVY{wavyId}";
            SendMessage(stream, helloMessage);

            // Receber resposta do agregador
            string response = ReceiveMessageWithRetry(stream);
            Console.WriteLine("AGREGADOR: " + response);
            if (response == "DENIED")
            {
                wavy = false;
                return;
            }

            if (response.StartsWith("ACK"))
            {
                // Ler dados do ficheiro CSV e enviá-los aos poucos
                if (File.Exists(filename))
                {
                    string[] lines = File.ReadAllLines(filename);
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

                                        // Guardar progresso após envio bem-sucedido
                                        mutex.WaitOne();
                                        try
                                        {
                                            SaveProgress(progressFile, i);
                                        }
                                        finally
                                        {
                                            mutex.ReleaseMutex();
                                        }
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
                    Console.WriteLine("Erro: Ficheiro não encontrado.");
                }
            }
            else if (response.StartsWith("DENIED"))
            {
                Console.WriteLine("Conexão negada pelo agregador.");
            }
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

    static int LoadProgress(string progressFile)
    {
        mutex.WaitOne();
        try
        {
            if (File.Exists(progressFile))
            {
                string content = File.ReadAllText(progressFile);
                if (int.TryParse(content, out int progress))
                {
                    return progress;
                }
            }
        }
        finally
        {
            mutex.ReleaseMutex();
        }

        return 0; // Se não houver progresso guardado, começar do início
    }

    static void SaveProgress(string progressFile, int lineNumber)
    {
        mutex.WaitOne();
        try
        {
            File.WriteAllText(progressFile, lineNumber.ToString());
        }
        finally
        {
            mutex.ReleaseMutex();
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

        return null;
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
}