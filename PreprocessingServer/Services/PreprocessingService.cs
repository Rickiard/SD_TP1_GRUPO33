using Grpc.Core;
using Preprocessing;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace PreprocessingServer.Services
{
    public class PreprocessingServiceImpl : PreprocessingService.PreprocessingServiceBase
    {
        private readonly ILogger<PreprocessingServiceImpl> _logger;

        public PreprocessingServiceImpl(ILogger<PreprocessingServiceImpl> logger)
        {
            _logger = logger;
        }

        public override Task<PreprocessResponse> PreprocessData(PreprocessRequest request, ServerCallContext context)
        {
            var (preProcessType, volume, server) = DatabaseHelper.GetPreprocessingConfig(request.WavyId);

            string processedData = request.RawData;
            string message = $"Tipo: {preProcessType}, Volume: {volume}, Servidor: {server}";

            // Aplicar o pré-processamento de acordo com o tipo
            switch (preProcessType?.ToLower())
            {
                case "filtragem":
                    // Remove linhas vazias e caracteres especiais
                    processedData = ApplyFiltragem(processedData);
                    break;

                case "agregacao":
                    // Agrega dados por linha, calculando médias ou somas
                    processedData = ApplyAgregacao(processedData);
                    break;

                case "normalizacao":
                    // Normaliza os valores numéricos para uma escala padrão
                    processedData = ApplyNormalizacao(processedData);
                    break;

                default:
                    _logger.LogWarning("Tipo de pré-processamento desconhecido: {Tipo}", preProcessType);
                    message = $"Tipo de pré-processamento desconhecido: {preProcessType}";
                    break;
            }

            _logger.LogInformation("Processamento {Tipo} para Wavy ID: {WavyId}", preProcessType, request.WavyId);

            var response = new PreprocessResponse
            {
                Success = true,
                Message = message,
                ProcessedData = processedData
            };

            return Task.FromResult(response);
        }

        private string ApplyFiltragem(string data)
        {
            // Remove linhas vazias
            var lines = data.Split('\n')
                           .Where(line => !string.IsNullOrWhiteSpace(line))
                           .ToList();

            // Remove caracteres especiais e normaliza espaços
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i] = Regex.Replace(lines[i], @"[^\w\s.,-]", "");
                lines[i] = Regex.Replace(lines[i], @"\s+", " ").Trim();
            }

            return string.Join("\n", lines);
        }

        private string ApplyAgregacao(string data)
        {
            var lines = data.Split('\n')
                           .Where(line => !string.IsNullOrWhiteSpace(line))
                           .ToList();

            if (lines.Count == 0) return string.Empty;

            // Assume que cada linha tem valores numéricos separados por vírgula
            var aggregatedLines = new List<string>();
            var currentBatch = new List<double[]>();

            foreach (var line in lines)
            {
                try
                {
                    var values = line.Split(',')
                                   .Select(v => double.TryParse(v.Trim(), out double num) ? num : 0)
                                   .ToArray();

                    if (values.Length > 0)
                    {
                        currentBatch.Add(values);
                    }

                    // Agrega a cada 5 linhas ou no final
                    if (currentBatch.Count >= 5 || line == lines.Last())
                    {
                        if (currentBatch.Count > 0)
                        {
                            // Calcula a média para cada coluna
                            var averages = new double[currentBatch[0].Length];
                            for (int i = 0; i < averages.Length; i++)
                            {
                                averages[i] = currentBatch.Average(row => row[i]);
                            }
                            aggregatedLines.Add(string.Join(",", averages));
                            currentBatch.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao processar linha na agregação: {Erro}", ex.Message);
                }
            }

            return string.Join("\n", aggregatedLines);
        }

        private string ApplyNormalizacao(string data)
        {
            var lines = data.Split('\n')
                           .Where(line => !string.IsNullOrWhiteSpace(line))
                           .ToList();

            if (lines.Count == 0) return string.Empty;

            // Encontra os valores máximos e mínimos para cada coluna
            var maxValues = new List<double>();
            var minValues = new List<double>();
            bool firstLine = true;

            foreach (var line in lines)
            {
                try
                {
                    var values = line.Split(',')
                                   .Select(v => double.TryParse(v.Trim(), out double num) ? num : 0)
                                   .ToArray();

                    if (firstLine)
                    {
                        maxValues = values.ToList();
                        minValues = values.ToList();
                        firstLine = false;
                    }
                    else
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            maxValues[i] = Math.Max(maxValues[i], values[i]);
                            minValues[i] = Math.Min(minValues[i], values[i]);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao processar linha na normalização: {Erro}", ex.Message);
                }
            }

            // Normaliza os valores para o intervalo [0,1]
            var normalizedLines = new List<string>();
            foreach (var line in lines)
            {
                try
                {
                    var values = line.Split(',')
                                   .Select(v => double.TryParse(v.Trim(), out double num) ? num : 0)
                                   .ToArray();

                    var normalizedValues = new double[values.Length];
                    for (int i = 0; i < values.Length; i++)
                    {
                        double range = maxValues[i] - minValues[i];
                        if (range == 0)
                            normalizedValues[i] = 0;
                        else
                            normalizedValues[i] = (values[i] - minValues[i]) / range;
                    }

                    normalizedLines.Add(string.Join(",", normalizedValues));
                }
                catch (Exception ex)
                {
                    _logger.LogError("Erro ao normalizar linha: {Erro}", ex.Message);
                }
            }

            return string.Join("\n", normalizedLines);
        }
    }
} 