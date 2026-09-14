using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;


namespace Diario_de_Bordo.Services
{
    public class FujiWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpClientFactory;

        public FujiWorker(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
        {
            _serviceProvider = serviceProvider;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Define as linhas que o servidor vai monitorar sozinho
            var linhasParaMonitorar = new[] { "SMD1", "SMD8", "SMD6", "SMD9", "SMD10" };

            var maquinasPorLinha = new Dictionary<string, string[]>
            {
                { "SMD1",  new[] { "L1NXT3A", "L1NXT3B" } },
                { "SMD6",  new[] { "NXTAL6_", "NXTBL6_" } },
                { "SMD8",  new[] { "L8NXTRA", "L8NXTRB" } },
                { "SMD9",  new[] { "NXTAL9", "NXTBL9" } },
                { "SMD10", new[] { "NXTAL10", "NXTBL10" } }
            };

            //var maquinasPorLinha = new Dictionary<string, string[]>
            //{
            //    { "SMD1",  new[] { "L1NXT3A"} },
            //    { "SMD6",  new[] { "NXTAL6_" } },
            //    { "SMD8",  new[] { "L8NXTRA" } },
            //    { "SMD9",  new[] { "NXTAL9" } },
            //    { "SMD10", new[] { "NXTAL10" } }
            //};

            var configRede = new Dictionary<string, (string IP, string NomeOficial)>
            {
                { "SMD1",  ("172.20.100.4",   "SMD1") },
                { "SMD6",  ("172.19.100.5", "Linha 6") },
                { "SMD8",  ("172.20.100.4",   "SMD8") },
                { "SMD9",  ("172.19.100.5", "Linha 9") },
                { "SMD10", ("172.19.100.5", "Linha 10") }
            };

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<DiarioContext>();
                    var client = _httpClientFactory.CreateClient();

                    foreach (var line in linhasParaMonitorar)
                    {
                        try
                        {
                            var settings = configRede[line];
                            int totalQty = 0;
                            string produtoAtual = "---";
                            string ladoAtual = "--";
                            string statusFinal = "OFFLINE";

                            foreach (var machine in maquinasPorLinha[line])
                            {
                                string url = $"http://{settings.IP}/fujiweb/fujimoni/ui/api/EquipInfo?Machine={machine}&Line={Uri.EscapeDataString(settings.NomeOficial)}";
                                var response = await client.GetAsync(url, stoppingToken);

                                if (response.IsSuccessStatusCode)
                                {
                                    var content = await response.Content.ReadAsStringAsync();
                                    var data = JsonSerializer.Deserialize<FujiMachineStatus>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                                    if (data?.Modules != null && data.Modules.Any())
                                    {
                                        var lastModule = data.Modules.LastOrDefault();
                                        var laneInfo = lastModule?.Lanes?.FirstOrDefault();
                                        if (laneInfo != null)
                                        {
                                            produtoAtual = laneInfo.JobName;
                                            totalQty = laneInfo.Qty;
                                            ladoAtual = laneInfo.JobSide;
                                        }
                                        statusFinal = data.Modules.Any(m => new[] { 1, 5, 101 }.Contains(m.State)) ? "ERRO" : "OK";
                                    }
                                }
                            }
                            var fuso = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time");
                            var agoraManaus = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso);
                            // Grava no Banco (Mesmo sem ninguém olhando)
                            if (produtoAtual != "---")
                            {
                                context.tbl_producaolog.Add(new ProducaoLog
                                {
                                    Linha = line,
                                    Maquina = maquinasPorLinha[line].FirstOrDefault(),
                                    Produto = produtoAtual,
                                    Quantidade = totalQty,
                                    Lado = ladoAtual ?? "--",
                                    Status = statusFinal,
                                    Timestamp = agoraManaus
                                });
                                await context.SaveChangesAsync(stoppingToken);
                            }
                        }
                        catch (Exception) { /* Ignora erros de rede temporários */ }
                    }
                }

                await Task.Delay(15000, stoppingToken); // Aguarda 5 segundos para o próximo ciclo
            }

            //var linhasDemo = new[] { "LINHA_1", "LINHA_2", "LINHA_3", "LINHA_4", "LINHA_5", "LINHA_6" };
            //var contadores = linhasDemo.ToDictionary(l => l, l => 0);
            //var rng = new Random();

            //while (!stoppingToken.IsCancellationRequested)
            //{
            //    using (var scope = _serviceProvider.CreateScope())
            //    {
            //        var context = scope.ServiceProvider.GetRequiredService<DiarioContext>();

            //        foreach (var line in linhasDemo)
            //        {
            //            try
            //            {
            //                // Lógica de Simulação: 95% de chance de OK
            //                string statusFinal = rng.Next(1, 100) <= 5 ? "ERRO" : "OK";

            //                if (statusFinal == "OK")
            //                {
            //                    contadores[line] += rng.Next(1, 3);
            //                }

            //                var fuso = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time");
            //                var agoraManaus = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso);

            //                context.tbl_producaolog.Add(new ProducaoLog
            //                {
            //                    Linha = line,
            //                    Maquina = "VIRTUAL-NXT",
            //                    Produto = $"PRODUTO-{line}",
            //                    Quantidade = contadores[line],
            //                    Status = statusFinal,
            //                    Timestamp = agoraManaus
            //                });
            //            }
            //            catch (Exception) { /* Ignora erros na simulação */ }
            //        }
            //        // Salva todas as 6 linhas de uma vez para ganhar performance
            //        await context.SaveChangesAsync(stoppingToken);
            //    }

            //    // Delay de 4 segundos entre as "batidas" de produção
            //    await Task.Delay(4000, stoppingToken);
            //}


        }
    }
}