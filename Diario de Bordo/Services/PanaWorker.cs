using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

namespace Diario_de_Bordo.Services
{
    public class PanaWorker : BackgroundService
    {
        [StructLayout(LayoutKind.Sequential)]
        public class PanaNetResource
        {
            public int Scope;
            public int Type;
            public int DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly string _remotePath = @"\\172.20.100.140\othersystem";

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(PanaNetResource nr, string password, string username, int flags);

        public PanaWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private async Task GravarLogLocal(string mensagem)
        {
            try
            {
                string caminhoPasta = @"C:\logs";
                if (!Directory.Exists(caminhoPasta)) Directory.CreateDirectory(caminhoPasta);

                string caminhoArquivo = Path.Combine(caminhoPasta, $"log_pana_{DateTime.Now:yyyyMMdd}.txt");
                string linhaLog = $"{DateTime.Now:HH:mm:ss} => {mensagem}{Environment.NewLine}";
                await File.AppendAllTextAsync(caminhoArquivo, linhaLog);
            }
            catch { }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await GravarLogLocal(">>> SERVIÇO INICIADO - MODO FORÇADO (5s) <<<");

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<DiarioContext>();

                    try
                    {
                        var nr = new PanaNetResource { Type = 1, RemoteName = _remotePath };
                        WNetAddConnection2(nr, "P@nasonic", @".\DGS", 0);

                        string pastaDia = Path.Combine(_remotePath, "ProductManageInfoFile", DateTime.Now.ToString("yyyyMMdd"));

                        if (Directory.Exists(pastaDia))
                        {
                            var arquivos = new DirectoryInfo(pastaDia).GetFiles("*.u01")
                                .OrderByDescending(f => f.CreationTime);

                            foreach (var arquivo in arquivos)
                            {
                                // Foca apenas no último evento de produção real
                                if (arquivo.Name.Contains("02-1-1-3"))
                                {
                                    using var fs = new FileStream(arquivo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                    using var sr = new StreamReader(fs);
                                    string conteudo = await sr.ReadToEndAsync();

                                    var matchBoard = Regex.Match(conteudo, @"Board=(\d+)");
                                    var matchProduto = Regex.Match(conteudo, @"LotName=""([^""]+)""");                                    
                                    var matchLinha = Regex.Match(conteudo, @"MJSID=""([^""]+)""");

                                    if (matchBoard.Success)
                                    {
                                        int qtdAtual = int.Parse(matchBoard.Groups[1].Value);
                                        string produto = matchProduto.Success ? matchProduto.Groups[1].Value : "N/A";
                                        string linha = "SMD5";

                                        // --- LÓGICA DE IDENTIFICAÇÃO DO LADO ---
                                        string ladoDetectado = "B"; // Padrão caso não encontre nada

                                        if (produto.EndsWith("_T", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ladoDetectado = "A"; // Mapeamos o Top como Lado A
                                        }
                                        else if (produto.EndsWith("_B", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ladoDetectado = "B"; // Mapeamos o Bottom como Lado B
                                        }
                                        // ----------------------------------------

                                        var fuso = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time");
                                        var agoraManaus = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso);

                                        context.tbl_producaolog.Add(new ProducaoLog
                                        {
                                            Linha = linha,
                                            Maquina = "NPM-W",
                                            Produto = produto,
                                            Lado = ladoDetectado, // Agora gravando A ou B dinamicamente
                                            Quantidade = qtdAtual,
                                            Status = "OK",
                                            Timestamp = agoraManaus,
                                        });

                                        await context.SaveChangesAsync(stoppingToken);
                                        await GravarLogLocal($"PANA: {produto} | Lado: {ladoDetectado} | Qtd: {qtdAtual}");

                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await GravarLogLocal($"[ERRO]: {ex.Message}");
                    }
                }

                await Task.Delay(15000, stoppingToken);
            }
        }
    }
}