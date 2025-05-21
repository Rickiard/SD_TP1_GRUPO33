﻿using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WavyClient
{
    class Wavy
    {
        const int MAX_RETRIES = 5; // Número máximo de tentativas de reconexão
        const int RETRY_DELAY_MS = 5000; // Tempo de espera entre tentativas (em milissegundos)
        const string RABBITMQ_HOST = "localhost"; // Host do RabbitMQ
        const string EXCHANGE_NAME = "wavy_exchange"; // Nome da exchange
        static readonly Mutex mutex = new Mutex(); // Mutex para sincronizar acesso ao ficheiro de progresso
        static ManualResetEvent[]? wavyCompletionEvents; // Array de eventos para controle de conclusão

        static void Main(string[] args)
        {            if (args.Length < 4)
            {
                Console.WriteLine("Uso: Wavy <RabbitMQ Host> <Port1> <Port2> <Port3>");
                Console.WriteLine("Usando valores padrão: localhost 4000 4001 4002");
            }

            string rabbitMqHost = args.Length > 0 ? args[0] : RABBITMQ_HOST;
            int[] ports = args.Length >= 4 
                ? new[] { int.Parse(args[1]), int.Parse(args[2]), int.Parse(args[3]) }
                : new[] { 4000, 4001, 4002 };

            // Criar eventos para sinalizar a conclusão de cada WAVY
            wavyCompletionEvents = new ManualResetEvent[4];
            for (int i = 0; i < wavyCompletionEvents.Length; i++)
            {
                wavyCompletionEvents[i] = new ManualResetEvent(false);
            }            // Criar threads para executar as WAVYs simultaneamente
            Thread wavy1 = new Thread(() => ExecuteWavy("001", rabbitMqHost, ports, "buoy - Cópia.csv", 0));
            Thread wavy2 = new Thread(() => ExecuteWavy("002", rabbitMqHost, ports, "buoy - Cópia (2).csv", 1));
            Thread wavy3 = new Thread(() => ExecuteWavy("003", rabbitMqHost, ports, "buoy - Cópia (3).csv", 2));
            Thread wavy4 = new Thread(() => ExecuteWavy("004", rabbitMqHost, ports, "buoy - Cópia (4).csv", 3));

            // Iniciar as threads
            wavy1.Start();
            wavy2.Start();
            wavy3.Start();
            wavy4.Start();

            // Aguardar a conclusão de todas as WAVYs
            WaitHandle.WaitAll(wavyCompletionEvents);

            Console.WriteLine("Todas as WAVYs foram concluídas.");
        }

        static void ExecuteWavy(string id, string rabbitMqHost, int[] ports, string filename, int eventIndex)
        {
            bool wavySuccess = false;
            string progressFile = $"progress_WAVY{id}.txt";

            // Tentar executar a WAVY até 3 vezes, se necessário
            for (int attempt = 1; attempt <= 3 && !wavySuccess; attempt++)
            {
                Console.WriteLine($"Iniciando WAVY{id}, tentativa {attempt}...");
                wavySuccess = StartWavy(id, rabbitMqHost, ports, filename, progressFile);

                if (wavySuccess)
                {
                    Console.WriteLine($"WAVY{id} concluída com sucesso.");
                }
                else
                {
                    Console.WriteLine($"WAVY{id} falhou na tentativa {attempt}. Retentando...");
                    Thread.Sleep(RETRY_DELAY_MS);
                }
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

            // Sinalizar que esta WAVY foi concluída
            wavyCompletionEvents?[eventIndex].Set();
        }

        static bool StartWavy(string id, string rabbitMqHost, int[] ports, string filename, string progressFile)
        {
            string wavyId = "WAVY" + id;
            RabbitMQService? currentService = null;
            AutoResetEvent waitHandle = new AutoResetEvent(false);
            string? currentResponse = null;

            try
            {
                // Carregar o progresso salvo
                int lastProcessedLine = LoadProgress(progressFile);
                Console.WriteLine($"Última linha processada para {wavyId}: {lastProcessedLine}");

                // Tentar conectar em diferentes portas
                bool connected = false;

                foreach (int port in ports)
                {
                    try
                    {
                        currentService?.Dispose();
                        currentService = new RabbitMQService(rabbitMqHost, EXCHANGE_NAME, wavyId, port);
                        
                        if (currentService != null)
                        {
                            // Resetar estado para nova tentativa
                            waitHandle.Reset();
                            currentResponse = null;

                            // Configurar o handler para mensagens recebidas
                            currentService.OnMessageReceived += (message) =>
                            {
                                Console.WriteLine($"AGREGADOR para {wavyId}: {message.MessageType} - {message.Data}");
                                currentResponse = message.MessageType;
                                waitHandle.Set();
                            };

                            // Enviar mensagem HELLO
                            var initMessage = new WavyMessage(wavyId, "HELLO", "");
                            currentService.PublishMessage(initMessage);
                            
                            // Aguardar resposta do agregador
                            if (!waitHandle.WaitOne(5000)) // Reduzindo o timeout para 5 segundos
                            {
                                Console.WriteLine($"{wavyId}: Timeout aguardando resposta do agregador na porta {port}.");
                                continue; // Tentar próxima porta
                            }

                            if (currentResponse == "DENIED")
                            {
                                Console.WriteLine($"{wavyId}: Conexão negada pelo agregador na porta {port}.");
                                continue; // Tentar próxima porta
                            }

                            if (currentResponse == "ACK")
                            {
                                connected = true;
                                Console.WriteLine($"{wavyId}: Conectado com sucesso ao agregador na porta {port}");
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{wavyId}: Falha ao conectar na porta {port}: {ex.Message}");
                    }
                }

                if (!connected || currentService == null)
                {
                    Console.WriteLine($"{wavyId}: Não foi possível conectar em nenhuma porta disponível.");
                    return false;
                }

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
                                    waitHandle.Reset();
                                    currentResponse = null;

                                    var dataMessage = new WavyMessage(wavyId, "DATA_CSV", line);
                                    currentService.PublishMessage(dataMessage);

                                    if (!waitHandle.WaitOne(10000))
                                    {
                                        throw new TimeoutException("Timeout aguardando resposta do agregador.");
                                    }

                                    if (currentResponse == "ACK")
                                    {
                                        success = true;
                                        SaveProgress(progressFile, i + 1);
                                    }
                                    else
                                    {
                                        throw new Exception($"Resposta inesperada do agregador: {currentResponse}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    retryCount++;
                                    Console.WriteLine($"{wavyId}: Erro ao enviar dados (tentativa {retryCount}): {ex.Message}");
                                    Thread.Sleep(RETRY_DELAY_MS);
                                }
                            }

                            if (!success)
                            {
                                Console.WriteLine($"{wavyId}: Falha ao enviar dados após {MAX_RETRIES} tentativas.");
                                return false;
                            }
                        }
                    }
                    return true;
                }
                else
                {
                    Console.WriteLine($"{wavyId}: Arquivo não encontrado: {filename}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{wavyId}: Erro geral: {ex.Message}");
                return false;
            }
            finally
            {
                currentService?.Dispose();
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
}