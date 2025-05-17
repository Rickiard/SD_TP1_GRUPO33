using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using WavyClient.Models;
using WavyClient.Services;

/// <summary>
/// Main class for the WAVY client application.
/// </summary>
class Wavy
{
    const int MAX_RETRIES = 5; // Número máximo de tentativas de reconexão
    const int RETRY_DELAY_MS = 5000; // Tempo de espera entre tentativas (em milissegundos)
    const int CONNECTION_TIMEOUT_MS = 10000; // Timeout para estabelecer conexão (em milissegundos)
    static Mutex mutex = new Mutex(); // Mutex para sincronizar acesso ao ficheiro de progresso

    // RabbitMQ configuration
    const string RABBITMQ_HOST = "localhost";
    const int RABBITMQ_PORT = 5672;
    const string RABBITMQ_USER = "guest";
    const string RABBITMQ_PASSWORD = "guest";
    const string RABBITMQ_EXCHANGE = "ocean_monitoring";

    /// <summary>
    /// Entry point for the application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    static void Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Uso: Wavy <IP do Agregador> <Porta> <Porta> <Porta>");
            Console.WriteLine("Nota: Os parâmetros de IP e Porta são mantidos por compatibilidade, mas não são utilizados na comunicação via RabbitMQ.");
            return;
        }

        // Criar threads para executar as WAVYs simultaneamente
        Thread wavy1 = new Thread(() => ExecuteWavy("001", "temperature", "atlantic", "buoy - Cópia.csv"));
        Thread wavy2 = new Thread(() => ExecuteWavy("002", "pressure", "atlantic", "buoy - Cópia (2).csv"));
        Thread wavy3 = new Thread(() => ExecuteWavy("003", "salinity", "pacific", "buoy - Cópia (3).csv"));
        Thread wavy4 = new Thread(() => ExecuteWavy("004", "oxygen", "pacific", "buoy - Cópia (4).csv"));

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

    /// <summary>
    /// Executes a WAVY device simulation.
    /// </summary>
    /// <param name="id">The WAVY device ID.</param>
    /// <param name="sensorType">The type of sensor.</param>
    /// <param name="location">The geographic location.</param>
    /// <param name="filename">The CSV file containing the data to send.</param>
    static void ExecuteWavy(string id, string sensorType, string location, string filename)
    {
        bool wavy = true; // Variável local para controlar o estado da WAVY
        string progressFile = $"progress_WAVY{id}.txt"; // Arquivo de progresso específico para cada WAVY

        // Tentar executar a WAVY até 3 vezes, se necessário
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            Console.WriteLine($"Iniciando WAVY{id}, tentativa {attempt}...");
            StartWavy(id, sensorType, location, filename, ref wavy, progressFile);

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

    /// <summary>
    /// Starts a WAVY device simulation using RabbitMQ for communication.
    /// </summary>
    /// <param name="id">The WAVY device ID.</param>
    /// <param name="sensorType">The type of sensor.</param>
    /// <param name="location">The geographic location.</param>
    /// <param name="filename">The CSV file containing the data to send.</param>
    /// <param name="wavy">Reference to a boolean indicating the WAVY state.</param>
    /// <param name="progressFile">The file to store progress information.</param>
    static void StartWavy(string id, string sensorType, string location, string filename, ref bool wavy, string progressFile)
    {
        string wavyId = "WAVY" + id;

        try
        {
            // Carregar o progresso salvo
            int lastProcessedLine = LoadProgress(progressFile);
            Console.WriteLine($"Última linha processada para {wavyId}: {lastProcessedLine}");

            // Inicializar o serviço RabbitMQ
            using (var rabbitMQService = new RabbitMQService(
                RABBITMQ_HOST,
                RABBITMQ_PORT,
                RABBITMQ_USER,
                RABBITMQ_PASSWORD,
                RABBITMQ_EXCHANGE,
                wavyId,
                sensorType,
                location))
            {
                // Enviar mensagem de hello
                var helloMessage = new WavyMessage(wavyId, MessageType.Hello, "");
                string response = rabbitMQService.PublishMessage(helloMessage, true, 10000);
                
                if (response == null || response == "DENIED")
                {
                    Console.WriteLine($"{wavyId}: Conexão negada ou sem resposta do agregador.");
                    wavy = false;
                    return;
                }

                Console.WriteLine($"{wavyId}: Resposta do agregador: {response}");

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
                            int retryCount = 0;

                            while (!success && retryCount < MAX_RETRIES)
                            {
                                try
                                {
                                    var dataMessage = new WavyMessage(wavyId, MessageType.DataCsv, line);
                                    response = rabbitMQService.PublishMessage(dataMessage, true, 10000);

                                    if (response != null && response.StartsWith("ACK"))
                                    {
                                        success = true;
                                        Console.WriteLine($"{wavyId}: Dados enviados com sucesso.");

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
                                        retryCount++;
                                        Console.WriteLine($"{wavyId}: Resposta inesperada do agregador. Tentando novamente ({retryCount}/{MAX_RETRIES})...");
                                    }
                                }
                                catch (Exception e)
                                {
                                    retryCount++;
                                    Console.WriteLine($"{wavyId}: Erro durante o envio: {e.Message}. Tentando novamente ({retryCount}/{MAX_RETRIES})...");
                                }
                            }

                            if (!success)
                            {
                                Console.WriteLine($"{wavyId}: Falha ao enviar dados após {MAX_RETRIES} tentativas.");
                            }

                            Thread.Sleep(2000); // Pequeno atraso entre envios (2 segundos)
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"{wavyId}: Erro: Ficheiro não encontrado.");
                }

                // Enviar mensagem de quit
                var quitMessage = new WavyMessage(wavyId, MessageType.Quit, "");
                rabbitMQService.PublishMessage(quitMessage, false);
                Console.WriteLine($"{wavyId}: Mensagem 'QUIT' enviada ao agregador.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"{wavyId}: Erro crítico: {e.Message}");
            wavy = false;
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

}
