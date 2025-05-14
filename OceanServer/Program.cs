using System;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Preprocessing;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class PreprocessingServiceImpl : PreprocessingService.PreprocessingServiceBase
{
    public override Task<PreprocessResponse> PreprocessData(PreprocessRequest request, ServerCallContext context)
    {
        // Exemplo de pré-processamento: converter para maiúsculas
        string processed = request.RawData.ToUpperInvariant();
        return Task.FromResult(new PreprocessResponse
        {
            WavyId = request.WavyId,
            ProcessedData = processed,
            Success = true,
            Message = "Pré-processamento concluído."
        });
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        // Iniciar servidores TCP nas portas 5000 e 5001
        Task.Run(() => StartTcpServer(5000));
        Task.Run(() => StartTcpServer(5001));

        // Iniciar gRPC
        CreateHostBuilder(args).Build().Run();
    }

    public static void StartTcpServer(int port)
    {
        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"Servidor TCP iniciado na porta {port}...");
        while (true)
        {
            try
            {
                TcpClient client = server.AcceptTcpClient();
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"[TCP {port}] Dados recebidos: {received}");
                }
                client.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCP {port}] Erro: {ex.Message}");
            }
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel(options =>
                {
                    options.ListenAnyIP(7000, listenOptions =>
                    {
                        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
                    });
                });
                webBuilder.UseStartup<Startup>();
            });
}

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<PreprocessingServiceImpl>();
        });
    }
}