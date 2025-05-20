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
        static Mutex mutex = new Mutex(); // Mutex para sincronizar acesso ao ficheiro de progresso
        static ManualResetEvent[] wavyCompletionEvents;

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Uso: Wavy <RabbitMQ Host>");
                Console.WriteLine("Usando localhost como padrão para RabbitMQ");
            }

            string rabbitMqHost = args.Length > 0 ? args[0] : RABBITMQ_HOST;

            // Criar eventos para sinalizar a conclusão de cada WAVY
            wavyCompletionEvents = new ManualResetEvent[4];
            for (int i = 0; i < wavyCompletionEvents.Length; i++)
            {
                wavyCompletionEvents[i] = new ManualResetEvent(false);
            }

            // Criar threads para executar as WAVYs simultaneamente
            Thread wavy1 = new Thread(() => ExecuteWavy("001", rabbitMqHost, "buoy - Cópia.csv", 0));
            Thread wavy2 = new Thread(() => ExecuteWavy("002", rabbitMqHost, "buoy - Cópia (2).csv", 1));
            Thread wavy3 = new Thread(() => ExecuteWavy("003", rabbitMqHost, "buoy - Cópia (3).csv", 2));
            Thread wavy4 = new Thread(() => ExecuteWavy("004", rabbitMqHost, "buoy - Cópia (4).csv", 3));

            // Iniciar as threads
            wavy1.Start();
            wavy2.Start();
            wavy3.Start();
            wavy4.Start();

            // Aguardar a conclusão de todas as WAVYs
            WaitHandle.WaitAll(wavyCompletionEvents);

            Console.WriteLine("Todas as WAVYs foram concluídas.");
        }

        static void ExecuteWavy(string id, string rabbitMqHost, string filename, int eventIndex)
        {
            bool wavySuccess = false; // Variável local para controlar o estado da WAVY
            string progressFile = $"progress_WAVY{id}.txt"; // Arquivo de progresso específico para cada WAVY

            // Tentar executar a WAVY até 3 vezes, se necessário
            for (int attempt = 1; attempt <= 3 && !wavySuccess; attempt++)
            {
                Console.WriteLine($"Iniciando WAVY{id}, tentativa {attempt}...");
                wavySuccess = StartWavy(id, rabbitMqHost, filename, progressFile);

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
            wavyCompletionEvents[eventIndex].Set();
        }

        static bool StartWavy(string id, string rabbitMqHost, string filename, string progressFile)
        {
            string wavyId = "WAVY" + id;
            RabbitMQService rabbitMqService = null;

            try
            {
                // Carregar o progresso salvo
                int lastProcessedLine = LoadProgress(progressFile);
                Console.WriteLine($"Última linha processada para {wavyId}: {lastProcessedLine}");

                // Inicializar o serviço RabbitMQ
                rabbitMqService = new RabbitMQService(rabbitMqHost, EXCHANGE_NAME, wavyId);
                
                // Configurar o handler para mensagens recebidas
                AutoResetEvent responseReceived = new AutoResetEvent(false);
                string lastResponse = null;
                
                rabbitMqService.OnMessageReceived += (message) =>
                {
                    Console.WriteLine($"AGREGADOR para {wavyId}: {message.MessageType} - {message.Data}");
                    lastResponse = message.MessageType;
                    responseReceived.Set();
                };

                // Enviar mensagem HELLO
                var helloMessage = new WavyMessage(wavyId, "HELLO", "");
                rabbitMqService.PublishMessage(helloMessage);
                
                // Aguardar resposta do agregador
                if (!responseReceived.WaitOne(10000))
                {
                    Console.WriteLine($"{wavyId}: Timeout aguardando resposta do agregador.");
                    return false;
                }

                if (lastResponse == "DENIED")
                {
                    Console.WriteLine($"{wavyId}: Conexão negada pelo agregador.");
                    return false;
                }

                if (lastResponse == "ACK")
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
                                int retryCount = 0;

                                while (!success && retryCount < MAX_RETRIES)
                                {
                                    try
                                    {
                                        // Resetar o evento para aguardar nova resposta
                                        responseReceived.Reset();
                                        lastResponse = null;
                                        
                                        // Enviar dados
                                        var dataMessage = new WavyMessage(wavyId, "DATA_CSV", line);
                                        rabbitMqService.PublishMessage(dataMessage);

                                        // Aguardar resposta
                                        if (!responseReceived.WaitOne(10000))
                                        {
                                            throw new TimeoutException("Timeout aguardando resposta do agregador.");
                                        }

                                        if (lastResponse == "ACK")
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
                                            Console.WriteLine($"{wavyId}: Resposta inesperada do agregador: {lastResponse}. Tentando novamente...");
                                            retryCount++;
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        retryCount++;
                                        Console.WriteLine($"{wavyId}: Erro durante o envio: {e.Message}. Tentativa {retryCount}/{MAX_RETRIES}");
                                        Thread.Sleep(RETRY_DELAY_MS);
                                    }
                                }

                                if (!success)
                                {
                                    Console.WriteLine($"{wavyId}: Falha ao enviar dados após {MAX_RETRIES} tentativas.");
                                    return false;
                                }

                                Thread.Sleep(2000); // Pequeno atraso entre envios (2 segundos)
                            }
                        }
                        
                        // Enviar mensagem QUIT
                        var quitMessage = new WavyMessage(wavyId, "QUIT", "");
                        rabbitMqService.PublishMessage(quitMessage);
                        
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"{wavyId}: Erro: Ficheiro não encontrado: {filename}");
                        return false;
                    }
                }
                
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine($"{wavyId}: Erro crítico: {e.Message}");
                return false;
            }
            finally
            {
                rabbitMqService?.Dispose();
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