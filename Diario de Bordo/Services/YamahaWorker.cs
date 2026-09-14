using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using System.Runtime.InteropServices;
using System.Text;

namespace Diario_de_Bordo.Services
{
    public class YamahaWorker : BackgroundService
    {
        [StructLayout(LayoutKind.Sequential)]
        public class NetResource
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

        private readonly Dictionary<string, string> _mapLinhas = new Dictionary<string, string>
{
    { "line2", "SMD2" },
    { "line3", "SMD3" },
    { "line4", "SMD4" },
    //{ "line5", "SMD5" },
    { "line7", "SMD7" }
    // Adicione as outras conforme necessário
};

        private readonly Dictionary<string, string> _mapMaquinas = new Dictionary<string, string>
{
    { "Mch01", "ASSEMBLEON-1" },
    { "Mch02", "ASSEMBLEON-2" },
    { "Mch03", "ASSEMBLEON-3" },
    { "Mch04", "ASSEMBLEON-4" },
    { "Mch05", "ASSEMBLEON-5" }
    // Adicione as outras conforme necessário
};

        [DllImport("mpr.dll")]
        private static extern int WNetAddConnection2(NetResource nr, string password, string username, int flags);

        private readonly IServiceProvider _serviceProvider;
        private readonly string _remotePath = @"\\GBR-PC\Machineslineshistoty";

        public YamahaWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private async Task GravarLogLocal(string mensagem)
        {
            try
            {
                string caminhoPasta = @"C:\logs";
                if (!Directory.Exists(caminhoPasta)) Directory.CreateDirectory(caminhoPasta);

                string caminhoArquivo = Path.Combine(caminhoPasta, $"log_yamaha_{DateTime.Now:yyyyMMdd}.txt");
                string linhaLog = $"{DateTime.Now:HH:mm:ss} => {mensagem}{Environment.NewLine}";
                await File.AppendAllTextAsync(caminhoArquivo, linhaLog);
            }
            catch { }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await GravarLogLocal(">>> SERVIÇO YAMAHA INICIADO <<<");

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<DiarioContext>();

                    try
                    {
                        // 1. Tenta conexão de rede (Ajuste usuário/senha se necessário)
                        var nr = new NetResource { Type = 1, RemoteName = _remotePath };
                        WNetAddConnection2(nr, "gbr", "GBR", 0);

                        string dataHoje = DateTime.Now.ToString("yyyyMMdd");

                        // 2. Varre as pastas de Linhas (line2, line3...)
                        var pastasLinhas = Directory.GetDirectories(_remotePath, "line*");

                        foreach (var caminhoLinha in pastasLinhas)
                        {
                            string nomeLinha = Path.GetFileName(caminhoLinha).ToUpper();

                            // 3. Varre as máquinas dentro de cada linha (Mch01, Mch02...)
                            var pastasMaquinas = Directory.GetDirectories(caminhoLinha, "Mch*");

                            foreach (var caminhoMch in pastasMaquinas)
                            {
                                string nomeMch = Path.GetFileName(caminhoMch);
                                string arquivoLog = Path.Combine(caminhoMch, $"PcbLog{dataHoje}.csv");

                                if (File.Exists(arquivoLog))
                                {
                                    await ProcessarArquivoYamaha(arquivoLog, nomeLinha, nomeMch, context);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await GravarLogLocal($"[ERRO GERAL]: {ex.Message}");
                    }
                }

                await Task.Delay(15000, stoppingToken); // Intervalo de 15 segundos entre varreduras
            }
        }
        private string TraduzirNome(string original, Dictionary<string, string> dicionario)
        {
            // Tenta buscar no dicionário (case-insensitive para garantir)
            var chave = dicionario.Keys.FirstOrDefault(k => k.Equals(original, StringComparison.OrdinalIgnoreCase));
            return chave != null ? dicionario[chave] : original;
        }

        private async Task ProcessarArquivoYamaha(string caminho, string linhaOriginal, string maquinaOriginal, DiarioContext context)
        {
            try
            {
                // 1. Aplica a Máscara nos nomes (Tradução)
                string linhaMascarada = _mapLinhas.ContainsKey(linhaOriginal.ToLower()) ? _mapLinhas[linhaOriginal.ToLower()] : linhaOriginal;
                string maquinaMascarada = _mapMaquinas.ContainsKey(maquinaOriginal) ? _mapMaquinas[maquinaOriginal] : maquinaOriginal;

                using var fs = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs, Encoding.Default);

                await sr.ReadLineAsync(); // Pula cabeçalho

                string ultimaLinha = "";
                string linhaAtual;

                // Lê até o final para pegar o status mais recente do arquivo
                while ((linhaAtual = await sr.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(linhaAtual))
                        ultimaLinha = linhaAtual;
                }

                if (!string.IsNullOrWhiteSpace(ultimaLinha))
                {
                    var cols = ultimaLinha.Split(',');

                    if (cols.Length >= 6)
                    {
                        // [A] + [B] = Modelo
                        string modelo = $"{cols[0].Trim()} {cols[1].Trim()}".Replace("\"", "");

                        // [C] = Timestamp e [F] = Quantidade
                        string dataRaw = cols[2].Trim().Replace("\"", "");
                        int.TryParse(cols[5].Trim().Replace("\"", ""), out int qtdLida);

                        if (DateTime.TryParse(dataRaw, out DateTime timestamp))
                        {
                            // 2. REGRA: Busca se já existe este modelo nesta máquina HOJE
                            var registroExistente = context.tbl_producaolog
                                .FirstOrDefault(x => x.Linha == linhaMascarada
                                                  && x.Maquina == maquinaMascarada
                                                  && x.Produto == modelo
                                                  && x.Timestamp.Date == DateTime.Now.Date);

                            if (registroExistente == null)
                            {
                                // Se é um modelo novo ou o primeiro do dia, insere
                                context.tbl_producaolog.Add(new ProducaoLog
                                {
                                    Linha = linhaMascarada,
                                    Maquina = maquinaMascarada,
                                    Produto = modelo,
                                    Quantidade = qtdLida,
                                    Status = "OK",
                                    Timestamp = timestamp
                                });

                                await context.SaveChangesAsync();
                                await GravarLogLocal($"[NOVO] {linhaMascarada}-{maquinaMascarada}: {modelo} ({qtdLida})");
                            }
                            else if (qtdLida > registroExistente.Quantidade)
                            {
                                // 3. REGRA: Salva apenas a quantidade MAIOR (Pico de produção)
                                registroExistente.Quantidade = qtdLida;
                                registroExistente.Timestamp = timestamp; // Atualiza para o horário da última placa

                                await context.SaveChangesAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await GravarLogLocal($"[ERRO {maquinaOriginal}]: {ex.Message}");
            }
        }
    }
}