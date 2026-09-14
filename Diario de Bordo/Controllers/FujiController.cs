using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using Diario_de_Bordo.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using QRCoder;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;

public class FujiController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFujiService _fujiService;
    private readonly DiarioContext _context; // Adicionado para o Postgres
    private readonly ILogger<FujiController> _logger;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly PickupReportExtractor _pickupExtractor;
    private readonly PickupReportExtractorFuji _fujiExtractor;
    private readonly PickupReportExtractorFujiNexim _neximExtractor;

    // Construtor atualizado para receber o PickupReportExtractorFuji
    public FujiController(
        IFujiService fujiService,
        IHttpClientFactory httpClientFactory,
        DiarioContext context,
        ILogger<FujiController> logger,
        IWebHostEnvironment webHostEnvironment,
        PickupReportExtractor pickupExtractor,
        PickupReportExtractorFujiNexim neximExtractor,
        PickupReportExtractorFuji fujiExtractor) // Injeção do novo serviço
    {
        _logger = logger;
        _webHostEnvironment = webHostEnvironment;
        _pickupExtractor = pickupExtractor;
        _fujiExtractor = fujiExtractor; // Atribuição
        _neximExtractor = neximExtractor;
        _fujiService = fujiService;
        _httpClientFactory = httpClientFactory;
        _context = context;
    }

    public IActionResult Index() => View();
    public IActionResult Monitoramento() => View();


    [HttpGet]
    public async Task<IActionResult> ObterStatusAtual(string line = "SMD1")
    {
        var fuso = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time");
        //var agoraManaus = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fuso);
        var agoraManaus = AppTime.Now;
        var lineNormalized = line.Trim();

        // Definição de Turnos (Manaus)
        DateTime inicioTurno = (agoraManaus.Hour >= 6 && agoraManaus.Hour < 14) ? agoraManaus.Date.AddHours(6) :
                               (agoraManaus.Hour >= 14 && agoraManaus.Hour < 22) ? agoraManaus.Date.AddHours(14) :
                               (agoraManaus.Hour < 6 ? agoraManaus.Date.AddDays(-1).AddHours(22) : agoraManaus.Date.AddHours(22));

        // 1. Busca o último log (Incluindo a nova propriedade Lado/Side)
        var ultimoLog = await _context.tbl_producaolog.AsNoTracking()
            .Where(l => l.Linha == lineNormalized)
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync();

        var config = await _context.tbl_configuracao_linhas
                       .FirstOrDefaultAsync(x => x.Linha == line);


        if (ultimoLog == null) return Json(new { success = false, message = $"Sem dados para {line}" });

        // 2. Busca Gatilhos e Mudança Real
        var primeiroLogDoModeloNoTurno = await _context.tbl_producaolog.AsNoTracking()
            .Where(l => l.Linha == lineNormalized &&
                        l.Timestamp >= inicioTurno &&
                        l.Produto == ultimoLog.Produto &&
                        l.Lado == ultimoLog.Lado) // Adicionado filtro de Lado
            .OrderBy(l => l.Timestamp)
            .FirstOrDefaultAsync();

        var logMudancaReal = await _context.tbl_producaolog.AsNoTracking()
            .Where(l => l.Linha == lineNormalized &&
                        l.Quantidade == ultimoLog.Quantidade &&
                        l.Timestamp >= inicioTurno)
            .OrderBy(l => l.Timestamp)
            .FirstOrDefaultAsync();

        var timestampReferencia = logMudancaReal?.Timestamp ?? ultimoLog.Timestamp;

        // 3. Metas e Paradas (IMPORTANTE: Busca meta por Produto E Lado)
        var meta = await _context.tbl_metasproducao.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Produto == ultimoLog.Produto &&
                                     m.Linha == lineNormalized &&
                                     m.Lado == ultimoLog.Lado); // Busca meta específica para o Side


        // --- LÓGICA DE HORAS DISPONÍVEIS E METAS DINÂMICAS ---
        // Total de minutos que o turno TEM (ex: 480 - 60 de almoço = 420)
        double metaHora = meta?.MetaHora ?? 0;

        var paradasPlanejadasRaw = await _context.tbl_paradas_planejadas.AsNoTracking()
        .Where(p => p.Linha == lineNormalized && p.Ativo)
        .ToListAsync();

        var janela = ObterHorariosTurno(config, agoraManaus);

        double minutosUteisTurnoTotal = CalcularMinutosUteis(janela, paradasPlanejadasRaw, agoraManaus);

        // Minutos produtivos que JÁ SE PASSARAM (para o cálculo da eficiência agora)
        double minutosProduzidosAteAgora = CalcularMinutosProduzidosAteAgora(janela, paradasPlanejadasRaw, agoraManaus);

        int metaTurnoTotal = (int)Math.Round((metaHora / 60.0) * minutosUteisTurnoTotal);
        int metaProporcional = (int)Math.Round((metaHora / 60.0) * minutosProduzidosAteAgora);
        // ------------------------------------------------------



        // Altere para buscar a parada aberta específica deste lado/produto que está rodando agora
        //var paradaAtiva = await _context.tbl_paradas_log.AsNoTracking()
        //    .FirstOrDefaultAsync(p => p.Linha == lineNormalized &&
        //                               p.Lado == ultimoLog.Lado && // Filtro por lado
        //                               p.Status == "ABERTA");

        var paradaAtiva = await _context.tbl_paradas_log.AsNoTracking()
    .Where(p => p.Linha == lineNormalized && p.Status == "ABERTA")
    .OrderByDescending(p => p.InicioParada)
    .FirstOrDefaultAsync();

        var paradasPlanejadas = await _context.tbl_paradas_planejadas
        .Where(p => p.Linha == line && p.Ativo)
        .Select(p => new {
            p.Descricao,
            Inicio = p.HoraInicio.ToString(@"hh\:mm"), // Formata para facilitar no JS
            Fim = p.HoraFim.ToString(@"hh\:mm")
        })
        .ToListAsync();


        int pan = meta?.Panelizacao ?? 1;
        int pecasProduzidasNoModelo = 0;

        if (primeiroLogDoModeloNoTurno != null)
        {
            int deltaContador = ultimoLog.Quantidade - primeiroLogDoModeloNoTurno.Quantidade;
            // Se houve reset ou troca de modelo, o delta é a própria quantidade atual
            pecasProduzidasNoModelo = (deltaContador < 0 ? ultimoLog.Quantidade : deltaContador) * pan;
        }

        // 4. Cálculo de Produção por Turno (Ajustado para considerar a panelização do contexto)
        async Task<int> CalcularProducaoPeriodoOtimizada(DateTime inicio, DateTime fim)
        {
            var valores = await _context.tbl_producaolog.AsNoTracking()
                .Where(l => l.Linha == lineNormalized && l.Timestamp >= inicio && l.Timestamp < fim)
                .OrderBy(l => l.Timestamp)
                .Select(l => new { l.Quantidade, l.Produto, l.Lado })
                .ToListAsync();

            if (valores.Count < 2) return 0;

            int acumulado = 0;
            for (int i = 1; i < valores.Count; i++)
            {
                // Busca panelização específica para cada mudança de linha no loop (Opcional, mas preciso)
                int pLocal = pan;

                if (valores[i].Quantidade >= valores[i - 1].Quantidade)
                    acumulado += (valores[i].Quantidade - valores[i - 1].Quantidade) * pLocal;
                else
                    acumulado += valores[i].Quantidade * pLocal;
            }
            return acumulado;
        }

        var hoje = agoraManaus.Date;
        var p1T = await CalcularProducaoPeriodoOtimizada(hoje.AddHours(6), hoje.AddHours(14));
        var p2T = await CalcularProducaoPeriodoOtimizada(hoje.AddHours(14), hoje.AddHours(22));
        var p3T = await CalcularProducaoPeriodoOtimizada(hoje.AddDays(-1).AddHours(22), hoje.AddHours(6));

        return Json(new
        {
            success = true,
            timestampUltimaPlaca = timestampReferencia.ToString("yyyy-MM-ddTHH:mm:ss"),
            line = line,
            produto = ultimoLog.Produto,
            lado = ultimoLog.Lado, // Retorno para o Front-end saber se é A ou B
            quantidadeAbsolutaFuji = ultimoLog.Quantidade,
            statusLinha = (paradaAtiva != null) ? "PARADA" : "OK",
            produzidoHora = Math.Max(0, pecasProduzidasNoModelo),
            temMeta = (meta != null && meta.MetaHora > 0),
            meta = meta?.MetaHora ?? 0,
            metaDia = metaTurnoTotal,         // Meta real do turno (ex: 700)
            metaAteAgora = metaProporcional,   // Meta proporcional (emoji)
            panelizacao = pan,
            toleranciaTakt = config?.ToleranciaTakt ?? 3,
            t1Inicio = config?.T1Inicio?.ToString(@"hh\:mm") ?? "06:00",
            t1Fim = config?.T1Fim?.ToString(@"hh\:mm") ?? "13:59",
            t2Inicio = config?.T2Inicio?.ToString(@"hh\:mm") ?? "14:00",
            t2Fim = config?.T2Fim?.ToString(@"hh\:mm") ?? "21:59",
            t3Inicio = config?.T3Inicio?.ToString(@"hh\:mm") ?? "22:00",
            t3Fim = config?.T3Fim?.ToString(@"hh\:mm") ?? "05:59",
            paradasProgramadas = paradasPlanejadas,
            // --- LÓGICA DE PARADA ---
            paradaEmAberto = (paradaAtiva != null),
            idParada = paradaAtiva?.Id,
            motivoParada = paradaAtiva?.Motivo,
            timestampInicioReal = paradaAtiva?.InicioParada?.ToString("yyyy-MM-ddTHH:mm:ss.ffffff"),

            // ESTA É A CHAVE: A quantidade que foi gravada quando a parada ABRIU
            quantidadeMomentoParada = paradaAtiva?.UltimaQuantidade ?? 0,
            usuarioSessao = "op@ad.gbrsmtserver.local",
            producidoLote = Math.Max(0, pecasProduzidasNoModelo),
            producao1T = p1T,
            producao2T = p2T,
            producao3T = p3T,
            totalDia = p1T + p2T + p3T,
        });
    }

    [HttpPost]
    public async Task<IActionResult> ProcessarBipeUniversal([FromBody] BipeSetupRequest request)
    {
        if (string.IsNullOrEmpty(request.MotivoBipado))
            return BadRequest(new { success = false, message = "Motivo vazio." });

        var motivo = request.MotivoBipado.ToUpper().Trim();
        var linha = request.Linha;
        var agora = DateTime.Now;

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Busca a parada aberta atual
            var paradaAtual = await _context.tbl_paradas_log
                .Where(p => p.Linha == linha && p.Status == "ABERTA")
                .OrderByDescending(p => p.InicioParada)
                .FirstOrDefaultAsync();

            // CASO A: ESTRATIFICAÇÃO (Prefixado com SETUP-)
            if (motivo.StartsWith("SETUP-"))
            {
                // Fecha a atual se existir
                if (paradaAtual != null)
                {
                    paradaAtual.FimParada = agora;
                    paradaAtual.Status = "FECHADA";
                    if (paradaAtual.InicioParada.HasValue)
                        paradaAtual.TempoTotalSegundos = (int)(agora - paradaAtual.InicioParada.Value).TotalSeconds;
                }

                // Abre a nova etapa
                var novaEtapa = new ParadaLog
                {
                    Linha = linha,
                    Produto = request.Produto ?? "N/A",
                    Lado = request.Lado ?? "T",
                    InicioParada = agora,
                    Motivo = motivo,
                    Status = "ABERTA",
                    Usuario = "OPERADOR (BIPE)"
                };
                _context.tbl_paradas_log.Add(novaEtapa);
            }
            // CASO B: APENAS EDITAR JUSTIFICATIVA (Demais motivos)
            else
            {
                if (paradaAtual != null)
                {
                    paradaAtual.Motivo = motivo;
                    paradaAtual.Usuario = "OPERADOR (BIPE)";
                    // Não altera o Status nem o InicioParada
                }
                else
                {
                    // Opcional: Se não houver parada aberta, você pode optar por abrir uma 
                    // ou apenas ignorar se a linha estiver operando.
                    return Json(new { success = false, message = "Não há parada aberta para justificar." });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Json(new { success = true, novoMotivo = motivo, acao = motivo.StartsWith("SETUP-") ? "ESTRATIFICOU" : "EDITOU" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }


    [HttpPost]
    public async Task<IActionResult> ProcessarBipeSetup([FromBody] BipeSetupRequest request)
    {
        if (string.IsNullOrEmpty(request.MotivoBipado))
            return BadRequest(new { success = false, message = "Motivo não informado." });

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var agora = DateTime.Now;
            var linha = request.Linha;

            // 1. CRIAMOS A NOVA ETAPA (Prioridade de Abertura)
            var novaEtapa = new ParadaLog
            {
                Linha = linha,
                Produto = request.Produto ?? "N/A",
                Lado = request.Lado ?? "T",
                InicioParada = agora,
                Motivo = request.MotivoBipado.ToUpper().Trim(),
                Status = "ABERTA",
                Usuario = "OPERADOR (BIPE)",
                TaktAlvoMomento = 0
            };

            _context.tbl_paradas_log.Add(novaEtapa);
            // Precisamos salvar aqui para gerar o ID da novaEtapa e não fechá-la no loop abaixo
            await _context.SaveChangesAsync();

            // 2. BUSCAMOS TODAS AS ANTERIORES ABERTAS DA LINHA (O "Limpa Casa")
            // Note que filtramos p.Id != novaEtapa.Id para não fechar a que acabamos de abrir
            var paradasPenduradas = await _context.tbl_paradas_log
                .Where(p => p.Linha == linha && p.Status == "ABERTA" && p.Id != novaEtapa.Id)
                .ToListAsync();

            foreach (var pOld in paradasPenduradas)
            {
                pOld.FimParada = agora;
                pOld.Status = "FECHADA";

                if (pOld.InicioParada.HasValue)
                {
                    var diff = agora - pOld.InicioParada.Value;
                    pOld.TempoTotalSegundos = (int)Math.Max(0, diff.TotalSeconds);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Json(new
            {
                success = true,
                novoMotivo = novaEtapa.Motivo,
                idNovo = novaEtapa.Id,
                quantidadeFechada = paradasPenduradas.Count,
                timestampInicio = novaEtapa.InicioParada?.ToString("yyyy-MM-ddTHH:mm:ss.ffffff")
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { success = false, message = "Erro: " + ex.Message });
        }
    }
    public class BipeSetupRequest
    {
        public string Linha { get; set; }
        public string MotivoBipado { get; set; }
        public string Produto { get; set; }
        public string Lado { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> ObterDadosGrafico(string line)
    {
        var agoraManaus = AppTime.Now; //
        var inicioRelatorio = agoraManaus.AddHours(-24);

        // 1. Busca os logs
        var logs = await _context.tbl_producaolog.AsNoTracking()
            .Where(l => l.Linha == line && l.Timestamp >= inicioRelatorio)
            .OrderBy(l => l.Timestamp)
            .Select(l => new { t = l.Timestamp, q = l.Quantidade, p = l.Produto })
            .ToListAsync();

        // 2. Descobre qual o produto MAIS RECENTE que passou na linha
        // Isso evita ter que passar o produto via JavaScript
        var produtoAtual = logs.LastOrDefault()?.p;

        // 3. Busca as paradas
        var paradas = await _context.tbl_paradas_log.AsNoTracking()
            .Where(p => p.Linha == line && p.InicioParada >= inicioRelatorio)
            .Select(p => new {
                inicio = p.InicioParada,
                fim = p.FimParada,
                motivo = p.Motivo
            })
            .ToListAsync();

        // 4. Busca a meta baseada na linha E no produto que acabamos de descobrir
        var metaObj = await _context.tbl_metasproducao.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Linha == line && m.Produto == produtoAtual);

        return Json(new
        {
            success = true,
            logs = logs,
            paradas = paradas,
            agora = agoraManaus.ToString("yyyy-MM-ddTHH:mm:ss"),
            meta = metaObj?.MetaHora ?? 0,
            pan = metaObj?.Panelizacao ?? 1
        });
    }



    [HttpPost]
    public async Task<IActionResult> ExecutarRoboLWS(string inicio, string fim)
    {
        string idExec = DateTime.Now.ToString("HHmmss");
        // Endereço base para o WNetAddConnection2
        string servidorRede = @"\\192.168.1.8\publica";
        // Pasta específica da Linha 5
        string destinoDiretorio = @"\\192.168.1.8\publica\Sistema SMD\Relatorio Rejeito SMT\SMD5";

        EscreverLogTxt($"[ROBÔ LWS] {idExec} - Início da solicitação via Web (Panasonic). Período solicitado: {inicio} até {fim}");

        try
        {
            // Validação básica para evitar que strings vazias quebrem o Parse no Service
            if (string.IsNullOrEmpty(inicio) || string.IsNullOrEmpty(fim))
            {
                return Json(new { success = false, message = "As datas de início e fim são obrigatórias." });
            }

            var credentials = new NetworkCredential("sistema.smd", "Gbr@2025", "ad.gbrcomponentes.com.br");

            // 1. Abre a conexão de rede usando o seu serviço NetworkShareConnection
            using (new NetworkShareConnection(servidorRede, credentials))
            {
                EscreverLogTxt($"[ROBÔ LWS] {idExec} - Conexão estabelecida com 192.168.1.8.");

                // Garante que a pasta existe no servidor remoto
                if (!Directory.Exists(destinoDiretorio))
                {
                    Directory.CreateDirectory(destinoDiretorio);
                    EscreverLogTxt($"[ROBÔ LWS] {idExec} - Pasta criada ou verificada: {destinoDiretorio}");
                }

                // 2. Dispara o serviço (Playwright) passando o caminho da rede e as strings de data
                // O tratamento de DateTime.Parse deve ser feito dentro do ExtrairParaExcelAsync
                var caminhoArquivo = await _pickupExtractor.ExtrairParaExcelAsync(destinoDiretorio, inicio, fim);

                if (!string.IsNullOrEmpty(caminhoArquivo) && System.IO.File.Exists(caminhoArquivo))
                {
                    string nomeArq = Path.GetFileName(caminhoArquivo);
                    EscreverLogTxt($"[ROBÔ LWS] {idExec} - SUCESSO: {nomeArq} salvo em {destinoDiretorio}.");

                    return Json(new
                    {
                        success = true,
                        message = "Relatório Panasonic gerado com sucesso na rede!",
                        arquivo = nomeArq
                    });
                }

                EscreverLogTxt($"[ROBÔ LWS] {idExec} - AVISO: O processo terminou, mas o arquivo não foi localizado.");
                return Json(new { success = false, message = "O robô não encontrou dados para extrair no período selecionado." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro LWS: {ex.Message}");
            EscreverLogTxt($"[ROBÔ LWS] {idExec} - EXCEÇÃO: {ex.Message}");
            return Json(new { success = false, message = "Erro de rede ou técnico: " + ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ExecutarRoboFuji(string inicio, string fim)
    {
        string idExec = DateTime.Now.ToString("HHmmss");
        // O WNetAddConnection2 geralmente prefere o caminho base do servidor
        string servidorRede = @"\\192.168.1.8\publica";
        string caminhoFinal = @"\\192.168.1.8\publica\Sistema SMD\Relatorio Rejeito SMT\SMD6-9-10";

        EscreverLogTxt($"[ROBÔ FUJI] {idExec} - Iniciando processamento. Período: {inicio} até {fim}");

        try
        {
            // Validação básica
            if (string.IsNullOrEmpty(inicio))
            {
                return Json(new { success = false, message = "A data de início é obrigatória para localizar o arquivo XML." });
            }

            var credentials = new NetworkCredential("sistema.smd", "Gbr@2025", "ad.gbrcomponentes.com.br");

            // 1. Abre a conexão usando o seu serviço
            using (new NetworkShareConnection(servidorRede, credentials))
            {
                EscreverLogTxt($"[ROBÔ FUJI] {idExec} - Conexão estabelecida com 192.168.1.8.");

                if (!Directory.Exists(caminhoFinal))
                {
                    Directory.CreateDirectory(caminhoFinal);
                }

                string[] linhas = { "SMD6", "SMD9", "SMD10" };
                List<string> gerados = new List<string>();

                foreach (var linha in linhas)
                {
                    // PASSO CHAVE: Passamos o 'inicio' para o Service buscar o arquivo PUS2026... correspondente
                    var arquivo = await _fujiExtractor.ExtrairParaExcelAsync(caminhoFinal, linha, inicio);

                    if (!string.IsNullOrEmpty(arquivo))
                        gerados.Add(Path.GetFileName(arquivo));
                }

                if (gerados.Count == 0)
                {
                    return Json(new { success = false, message = "Nenhum arquivo XML de rejeitos foi encontrado para a data selecionada." });
                }

                return Json(new
                {
                    success = true,
                    message = $"Sucesso! Arquivos gerados para {gerados.Count} linha(s).",
                    detalhes = string.Join(", ", gerados)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro Fuji Rede: {ex.Message}");
            EscreverLogTxt($"[ROBÔ FUJI] {idExec} - ERRO: {ex.Message}");
            return Json(new { success = false, message = "Erro técnico ou de rede: " + ex.Message });
        }
    }


    [HttpGet]
    public IActionResult ObterArquivosDisponiveis()
    {
        string idExec = DateTime.Now.ToString("HHmmss");
        string path = @"\\172.19.100.2\Rejeitos";

        // Credenciais que sabemos que funcionam na rede
        var credentials = new NetworkCredential("ADMIN", "admin", "172.19.100.2");

        try
        {
            // Força a conexão de rede para o contexto do IIS
            using (new NetworkShareConnection(path, credentials))
            {
                var directory = new DirectoryInfo(path);

                // Agora o .Exists deve retornar True
                if (!directory.Exists)
                {
                    EscreverLogTxt($"[LISTAR] {idExec} - Pasta não encontrada mesmo com login.");
                    return Json(new List<string>());
                }

                var arquivos = directory.GetFiles("PUS*.xml")
                    .OrderByDescending(f => f.CreationTime)
                    .Select(f => f.Name)
                    .Take(20)
                    .ToList();

                EscreverLogTxt($"[LISTAR] {idExec} - Sucesso: {arquivos.Count} arquivos listados.");
                return Json(arquivos);
            }
        }
        catch (Exception ex)
        {
            EscreverLogTxt($"[LISTAR] {idExec} - Erro de conexão: {ex.Message}");
            return Json(new List<string> { "Erro de permissão" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ProcessarSelecaoModal(string nomeArquivo) // Removi 'linha' se não for usar
    {
        if (string.IsNullOrEmpty(nomeArquivo))
        {
            return Json(new { success = false, message = "Arquivo não selecionado." });
        }

        try
        {
            // AJUSTE AQUI: Passando apenas 1 argumento para o Service
            // O Service cuidará de gerar SMD6, 9 e 10 automaticamente
            bool sucesso = await _fujiExtractor.ExtrairArquivoSelecionadoAsync(nomeArquivo);

            if (sucesso)
            {
                return Json(new
                {
                    success = true,
                    message = "Relatórios (SMD6, 9 e 10) gerados com sucesso na rede!"
                });
            }
            else
            {
                return Json(new { success = false, message = "Erro ao processar arquivo na rede." });
            }
        }
        catch (Exception ex)
        {
            EscreverLogTxt($"[CONTROLLER] Erro: {ex.Message}");
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ProcessarExtracao(string arquivoSelecionado)
    {
        if (string.IsNullOrEmpty(arquivoSelecionado))
        {
            return Json(new { success = false, message = "Nenhum arquivo foi selecionado." });
        }

        try
        {
            // Agora chamamos o Service passando apenas o nome do arquivo.
            // O Service cuidará de abrir as conexões e gerar os 3 relatórios (6, 9 e 10).
            bool sucesso = await _fujiExtractor.ExtrairArquivoSelecionadoAsync(arquivoSelecionado);

            if (sucesso)
            {
                return Json(new
                {
                    success = true,
                    message = "Relatórios das linhas SMD6, SMD9 e SMD10 gerados com sucesso na rede!"
                });
            }

            return Json(new { success = false, message = "O processamento falhou. Verifique se o arquivo ainda existe na rede da Fuji." });
        }
        catch (Exception ex)
        {
            EscreverLogTxt($"[ERRO PROCESSAR] {arquivoSelecionado}: {ex.Message}");
            return Json(new { success = false, message = "Erro interno: " + ex.Message });
        }
    }


    // --- MÉTODOS PARA NEXIM (SMD1 e SMD8) ---

    [HttpGet]
    public IActionResult ObterArquivosNeximDisponiveis()
    {
        // Removida a subpasta \Auto para garantir que a conexão de rede na raiz funcione melhor
        string pathNexim = @"\\172.20.100.4\ReportsNEXIM\Auto";
        var credentials = new NetworkCredential("NEXIM-SS1", "NEXIM-SS1");

        try
        {
            // Tenta a conexão de rede
            using (new NetworkShareConnection(pathNexim, credentials))
            {
                return ListarArquivosNeximDireto(pathNexim);
            }
        }
        catch (Exception ex)
        {
            // Fallback: Se o Windows já tiver uma conexão aberta (Erro 1219), tenta ler direto
            try
            {
                if (Directory.Exists(pathNexim))
                {
                    return ListarArquivosNeximDireto(pathNexim);
                }
            }
            catch { }

            _logger.LogError($"Erro Nexim: {ex.Message}");
            // Retorna uma lista vazia ou com a mensagem de erro amigável
            return Json(new List<string> { "Erro de permissão: " + ex.Message });
        }
    }

    // Helper privado para evitar repetição
    private JsonResult ListarArquivosNeximDireto(string path)
    {
        var directory = new DirectoryInfo(path);
        if (!directory.Exists) return Json(new List<string>());

        var arquivos = directory.GetFiles("*.xlsx")
            .OrderByDescending(f => f.CreationTime)
            .Select(f => f.Name)
            .Take(30)
            .ToList();

        return Json(arquivos);
    }

    [HttpPost]
    public async Task<IActionResult> ProcessarExtracaoNexim(string arquivoSelecionado)
    {
        if (string.IsNullOrEmpty(arquivoSelecionado))
        {
            return Json(new { success = false, message = "Nenhum arquivo Nexim selecionado." });
        }

        try
        {
            // Chama o método assíncrono que processa as abas do Excel e salva na rede 1.8
            bool sucesso = await _neximExtractor.ExtrairArquivoSelecionadoNeximAsync(arquivoSelecionado);

            if (sucesso)
            {
                return Json(new
                {
                    success = true,
                    message = "Extração Nexim concluída! O relatório consolidado foi salvo na rede pública."
                });
            }

            return Json(new { success = false, message = "Falha ao processar o conteúdo do Excel do Nexim." });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro no processamento Nexim: {ex.Message}");
            return Json(new { success = false, message = "Erro interno: " + ex.Message });
        }
    }



    [HttpPost]
    public async Task<IActionResult> SalvarMeta(string linha, string produto, string lado, int metaHora, int panelizacao)
    {
        string idOp = DateTime.Now.ToString("HHmmss");
        try
        {
            // LOG 1: O que o JavaScript enviou?
            EscreverLogTxt($"[SALVAR META - ID:{idOp}] Recebido -> Linha: {linha}, Produto: {produto}, Lado: '{lado}', Meta: {metaHora}");

            if (string.IsNullOrEmpty(linha) || string.IsNullOrEmpty(produto) || string.IsNullOrEmpty(lado))
            {
                EscreverLogTxt($"[SALVAR META - ID:{idOp}] ERRO: Dados incompletos. Lado estava nulo ou vazio?");
                return Json(new { success = false, message = "Dados incompletos (Linha/Produto/Lado)." });
            }

            // LOG 2: Verificar a busca no banco
            var metaExistente = await _context.tbl_metasproducao
                .FirstOrDefaultAsync(m => m.Linha == linha &&
                                         m.Produto == produto &&
                                         m.Lado == lado);

            if (metaExistente != null)
            {
                EscreverLogTxt($"[SALVAR META - ID:{idOp}] AÇÃO: Atualizando registro ID {metaExistente.Id}");
                metaExistente.MetaHora = metaHora;
                metaExistente.Panelizacao = panelizacao > 0 ? panelizacao : 1;
                _context.tbl_metasproducao.Update(metaExistente);
            }
            else
            {
                EscreverLogTxt($"[SALVAR META - ID:{idOp}] AÇÃO: Criando NOVO registro para Lado {lado}");
                var novaMeta = new ProdutoMeta
                {
                    Linha = linha,
                    Produto = produto,
                    Lado = lado,
                    MetaHora = metaHora,
                    Panelizacao = panelizacao > 0 ? panelizacao : 1
                };
                _context.tbl_metasproducao.Add(novaMeta);
            }

            await _context.SaveChangesAsync();

            EscreverLogTxt($"[SALVAR META - ID:{idOp}] SUCESSO: Gravado no Postgres.");

            return Json(new { success = true, message = $"Meta salva para {produto} ({lado})" });
        }
        catch (Exception ex)
        {
            //EscreverLogTxt($"[SALVAR META - ID:{idOp}] EXCEÇÃO: {ex.Message}");
            // O Postgres costuma mandar o erro real na InnerException
            var mensagemDetalhada = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

            EscreverLogTxt($"[SALVAR META - ID:{idOp}] EXCEÇÃO REAL: {mensagemDetalhada}");
            return Json(new { success = false, message = "Erro ao gravar: " + mensagemDetalhada });
        }
    }

    private void EscreverLogTxt(string mensagem)
    {
        try
        {
            string caminhoDiretorio = @"C:\logs";
            // Cria um arquivo novo para cada dia: log_2026-02-10.txt
            string caminhoArquivo = Path.Combine(caminhoDiretorio, $"log_{DateTime.Now:yyyy-MM-dd}.txt");

            if (!Directory.Exists(caminhoDiretorio))
                Directory.CreateDirectory(caminhoDiretorio);

            string conteudo = $"[{DateTime.Now:HH:mm:ss}] {mensagem}{Environment.NewLine}";
            System.IO.File.AppendAllText(caminhoArquivo, conteudo);
        }
        catch { /* Evita que erro de escrita pare a aplicação */ }
    }

    [HttpPost]
    public async Task<IActionResult> SalvarJustificativa(int id, string motivo)
    {
        try
        {
            // Busca apenas pelo ID, independente se está ABERTA ou CONCLUIDA
            var registro = await _context.tbl_paradas_log.FindAsync(id);

            if (registro == null)
            {
                return Json(new { success = false, message = "Registro não encontrado no banco (ID: " + id + ")" });
            }

            // SOBREESCREVE O MOTIVO
            registro.Motivo = motivo;
            registro.Usuario = "op@ad.gbrsmtserver.local"; // Garante que o usuário da sessão seja gravado

            _context.tbl_paradas_log.Update(registro);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Motivo atualizado!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    [HttpPost]
    public async Task<IActionResult> RegistrarAberturaParada([FromBody] ParadaLog request)
    {
        var usuarioPadrao = "op@ad.gbrsmtserver.local";
        var agoraManaus = AppTime.Now;

        try
        {
            // --- CORREÇÃO AQUI ---
            // Verificamos apenas se a LINHA já possui qualquer parada ABERTA.
            // Removemos o p.Lado == request.Lado para evitar que o sistema abra
            // uma "Inatividade" para o lado B se já houver um "Setup" aberto no lado T.
            var registroAlvo = await _context.tbl_paradas_log
                .Where(p => p.Linha == request.Linha && p.Status == "ABERTA")
                .OrderByDescending(p => p.InicioParada)
                .FirstOrDefaultAsync();

            if (registroAlvo != null)
            {
                // Se já houver algo aberto na linha, retornamos o ID existente.
                return Json(new { success = true, idParada = registroAlvo.Id, acao = "Mantido Aberto" });
            }

            DateTime inicioRealDaParada = request.InicioParada ?? agoraManaus;

            var ultimoLogProducao = await _context.tbl_producaolog
                .AsNoTracking()
                .Where(l => l.Linha == request.Linha) // Busca o último log da linha em geral
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync();

            var novaParada = new ParadaLog
            {
                Linha = request.Linha,
                Lado = request.Lado, // Aqui mantém o lado que o Watchdog identificou
                Motivo = request.Motivo ?? "INATIVIDADE AUTOMÁTICA",
                Usuario = usuarioPadrao,
                InicioParada = DateTime.SpecifyKind(inicioRealDaParada, DateTimeKind.Unspecified),
                Status = "ABERTA",
                Produto = request.Produto ?? ultimoLogProducao?.Produto ?? "DESCONHECIDO",
                UltimaQuantidade = request.UltimaQuantidade > 0 ? request.UltimaQuantidade : 0
            };

            _context.tbl_paradas_log.Add(novaParada);
            await _context.SaveChangesAsync();

            EscreverLogTxt($"[WATCHDOG] Parada Criada | ID: {novaParada.Id} | Linha: {request.Linha}");

            return Json(new { success = true, idParada = novaParada.Id, acao = "Abertura" });
        }
        catch (Exception ex)
        {
            var erroMsg = ex.InnerException?.Message ?? ex.Message;
            return Json(new { success = false, message = erroMsg });
        }
    }

    [HttpPost]
    public JsonResult EditarJustificativa(int id, string novoMotivo)
    {
        try
        {
            // 1. Localize o registro no banco de dados
            var parada = _context.tbl_paradas_log.Find(id);

            if (parada == null)
                return Json(new { success = false, message = "Registro não encontrado." });

            // 2. Atualize o motivo e opcionalmente o usuário que editou
            parada.Motivo = novoMotivo;

            _context.SaveChanges();

            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> FinalizarParadaAutomatica([FromForm] int id, [FromForm] int quantidadeFinal)
    {
        var usuarioLogado = HttpContext.Session.GetString("UsuarioLogado") ?? "op@ad.gbrsmtserver.local";
        try
        {
            var parada = await _context.tbl_paradas_log.FindAsync(id);

            if (parada == null)
                return Json(new { success = false, message = "Parada não encontrada." });

            if (parada.Status == "CONCLUIDA")
                return Json(new { success = true, message = "Já estava concluída." });

            var agoraManaus = AppTime.Now;

            // 1. Tentar encontrar o momento exato da primeira placa após a parada
            var logRetomada = await _context.tbl_producaolog
                .AsNoTracking()
                .Where(l => l.Linha == parada.Linha &&
                            l.Lado == parada.Lado &&
                            l.Timestamp >= parada.InicioParada &&
                            l.Quantidade >= quantidadeFinal) // Busca o log que bate com o valor enviado
                .OrderBy(l => l.Timestamp)
                .FirstOrDefaultAsync();

            // Se encontrou o log, usa o tempo dele. Se não, usa o tempo enviado pelo sensor/agora.
            DateTime fimDaParadaReal = logRetomada?.Timestamp ?? agoraManaus;

            // 2. Garantir que o fim nunca seja menor que o início (evita bugs de duração negativa)
            if (parada.InicioParada.HasValue && fimDaParadaReal < parada.InicioParada.Value)
            {
                fimDaParadaReal = parada.InicioParada.Value.AddSeconds(1);
            }

            // 3. Atualização dos campos
            parada.FimParada = fimDaParadaReal;
            // Se sua tabela tiver o campo QuantidadeRetomada, seria melhor usar ele do que sobrescrever o UltimaQuantidade
            parada.UltimaQuantidade = quantidadeFinal;

            parada.Status = "CONCLUIDA";
            parada.Usuario = usuarioLogado;

            if (parada.InicioParada.HasValue)
            {
                parada.TempoTotalSegundos = (int)(fimDaParadaReal - parada.InicioParada.Value).TotalSeconds;
            }

            // 4. Persistência
            _context.Entry(parada).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            EscreverLogTxt($"[FECHAMENTO OK] ID: {id} | Linha: {parada.Linha} | Fim: {parada.FimParada} | Segundos: {parada.TempoTotalSegundos}");

            return Json(new { success = true, acao = "CONCLUIDA" });
        }
        catch (Exception ex)
        {
            EscreverLogTxt($"[ERRO CRÍTICO FECHAMENTO] ID: {id} | {ex.Message}");
            return Json(new { success = false, message = "Erro interno: " + ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ObterParadasPorHora(string line, string hora)
    {
        try
        {
            // Pega a hora clicada (ex: "08:00")
            int h = int.Parse(hora.Split(':')[0]);

            var fusoManaus = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time");
            //var agoraManaus = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, fusoManaus);
            var agoraManaus = AppTime.Now;

            // Define o início e fim da janela de 1 hora baseada no dia atual de Manaus
            var dataFocoInicio = new DateTime(agoraManaus.Year, agoraManaus.Month, agoraManaus.Day, h, 0, 0);
            var dataFocoFim = dataFocoInicio.AddHours(1);

            var listaBruta = _context.tbl_paradas_log
              .Where(p => p.Linha == line && p.InicioParada >= dataFocoInicio && p.InicioParada < dataFocoFim)
              .OrderBy(p => p.InicioParada)
              .ToList();

            var resultado = listaBruta.Select(p => {
                string duracaoFormatada = "--";
                if (p.InicioParada.HasValue && p.FimParada.HasValue)
                {
                    TimeSpan diff = (TimeSpan)(p.FimParada.Value - p.InicioParada.Value);
                    duracaoFormatada = $"{(int)diff.TotalMinutes}:{diff.Seconds:D2} min";
                }
                else if (p.Status == "ABERTA")
                {
                    duracaoFormatada = "EM ABERTO";
                }

                return new
                {
                    inicio = p.InicioParada?.ToString("HH:mm:ss"),
                    fim = p.FimParada?.ToString("HH:mm:ss"),
                    duracao = duracaoFormatada,
                    motivo = p.Motivo ?? "Aguardando Justificativa"
                };
            }).ToList();

            return Json(resultado);
        }
        catch (Exception ex) { return BadRequest(ex.Message); }
    }

    [HttpGet]
    public IActionResult GerarResumoPeriodo(string linha, DateTime inicio, DateTime fim)
    {
        try
        {
            // 1. Buscar paradas no período usando tbl_paradas_log
            var paradas = _context.tbl_paradas_log
        .Where(p => p.Linha == linha && p.InicioParada >= inicio && p.InicioParada <= fim)
        .OrderBy(p => p.InicioParada)
        .ToList();

            // 2. Calcular Tempo Total Parado
            // Usamos a coluna TempoTotalSegundos para as fechadas e DateTime.Now para as abertas
            double totalSegundos = paradas.Sum(p => p.FimParada.HasValue
        ? (p.FimParada.Value - p.InicioParada.Value).TotalSeconds
        : (DateTime.Now - p.InicioParada.Value).TotalSeconds);

            TimeSpan ts = TimeSpan.FromSeconds(totalSegundos);
            string tempoFormatado = $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";

            // 3. Principal Motivo (Frequência)
            var principalMotivo = paradas
        .Where(p => !string.IsNullOrEmpty(p.Motivo))
        .GroupBy(p => p.Motivo)
        .OrderByDescending(g => g.Count())
        .Select(g => g.Key)
        .FirstOrDefault() ?? "N/A";

            // 4. Cálculo de Produção (tbl_producaolog + tbl_metasproducao)
            var logFim = _context.tbl_producaolog
        .Where(l => l.Linha == linha && l.Timestamp <= fim)
        .OrderByDescending(l => l.Timestamp)
        .FirstOrDefault();

            var logInicio = _context.tbl_producaolog
              .Where(l => l.Linha == linha && l.Timestamp <= inicio)
              .OrderByDescending(l => l.Timestamp)
              .FirstOrDefault();

            int totalProduzido = 0;
            if (logFim != null && logInicio != null)
            {
                // Buscamos a panelização do produto atual para converter ciclos em placas
                var meta = _context.tbl_metasproducao
          .FirstOrDefault(m => m.Produto == logFim.Produto && m.Linha == linha);

                int fatorPanel = meta?.Panelizacao ?? 1;
                totalProduzido = (logFim.Quantidade - logInicio.Quantidade) * fatorPanel;
            }

            // 5. Retorno para a View
            return Json(new
            {
                totalProduzido = totalProduzido,
                tempoTotalParado = tempoFormatado,
                principalMotivo = principalMotivo,
                listaParadas = paradas.Select(p => new
                {
                    inicio = p.InicioParada?.ToString("dd/MM HH:mm:ss"),
                    fim = p.FimParada?.ToString("dd/MM HH:mm:ss"),
                    duracao = p.FimParada.HasValue
                    ? (p.FimParada.Value - p.InicioParada.Value).ToString(@"hh\:mm\:ss")
                    : (DateTime.Now - p.InicioParada.Value).ToString(@"hh\:mm\:ss"),
                    motivo = p.Motivo ?? "Aguardando Justificativa",
                    usuario = p.Usuario // Padrão op@ad.gbrsmtserver.local conforme sua Model
                })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }




    }
    public IActionResult Andon(string line)
    {
        // Passamos a linha via ViewBag para que a View saiba qual monitorar
        ViewBag.Linha = string.IsNullOrEmpty(line) ? "SMD1" : line;
        return View();
    }

    public IActionResult GerarBookMotivos()
    {
        // 1. Busca todos os motivos de parada ativos no Postgres
        var motivosDoBanco = _context.tbl_motivos_parada
            .Where(m => m.ativo)
            .OrderBy(m => m.categoria)
            .ToList();

        // 2. Usaremos uma lista temporária para passar para a View
        // Usando a classe auxiliar 'MotivoParada' que você criou no final do arquivo Models
        var listaParaView = new List<MotivoParada>();

        using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
        {
            foreach (var motivo in motivosDoBanco)
            {
                // O conteúdo do QR Code será o ID do motivo
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(motivo.descricao.ToString(), QRCodeGenerator.ECCLevel.Q);

                using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                {
                    byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(20);
                    string qrCodeBase64 = "data:image/png;base64," + Convert.ToBase64String(qrCodeAsPngByteArr);

                    listaParaView.Add(new MotivoParada
                    {
                        id = motivo.id,
                        descricao = motivo.descricao.ToUpper(),
                        categoria = motivo.categoria?.ToUpper(),
                    });
                }
            }
        }

        // Retorna a lista para a View
        return View(listaParaView);
    }
    // Adicionar novo motivo
    [HttpPost]
    public async Task<IActionResult> AdicionarMotivo(string descricao, string categoria)
    {
        if (string.IsNullOrEmpty(descricao))
            return RedirectToAction("BookMotivos");

        // Criamos apenas o registro com a descrição (o QR será gerado pelo navegador)
        var novoMotivo = new MotivoParada
        {
            descricao = descricao.ToUpper(),
            categoria = categoria?.ToUpper() // Salva a categoria em maiúsculo
        };

        _context.tbl_motivos_parada.Add(novoMotivo);
        await _context.SaveChangesAsync();

        return RedirectToAction("GerarBookMotivos");
    }

    // Deletar motivo
    [HttpPost]
    public async Task<JsonResult> DeletarMotivo(int id)
    {
        var motivo = await _context.tbl_motivos_parada.FindAsync(id);
        if (motivo == null) return Json(new { success = false, message = "Não encontrado" });

        _context.tbl_motivos_parada.Remove(motivo);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }


    [HttpGet]
    public async Task<IActionResult> ObterStatusProducaoCompleto(string line = "SMD1")
    {
        try
        {
            // 1. Busca Meta e Takt Teórico
            var meta = await _context.tbl_metasproducao
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Linha == line);

            double taktTeorico = (meta?.MetaHora > 0) ? 3600.0 / (meta.MetaHora / (meta.Panelizacao > 0 ? meta.Panelizacao : 1)) : 0;

            // 2. Query N-10 com LAG para Cycle Time Real
            var sqlCiclos = @"
            WITH UltimosRegistros AS (
                SELECT timestamp, quantidade,
                LAG(timestamp) OVER (ORDER BY timestamp ASC) as ts_anterior,
                LAG(quantidade) OVER (ORDER BY timestamp ASC) as qtd_anterior
                FROM tbl_producaolog
                WHERE linha = @line AND timestamp >= NOW() - INTERVAL '4 hours'
                ORDER BY timestamp DESC LIMIT 20
            )
            SELECT timestamp as Timestamp, 
                   EXTRACT(EPOCH FROM (timestamp - ts_anterior)) as SegundosCiclo
            FROM UltimosRegistros
            WHERE ts_anterior IS NOT NULL 
              AND quantidade > qtd_anterior
              AND EXTRACT(EPOCH FROM (timestamp - ts_anterior)) BETWEEN 5 AND 600
            LIMIT 10";

            var registros = await _context.Database
                .SqlQueryRaw<RegistroCicloRaw>(sqlCiclos, new NpgsqlParameter("@line", line))
                .ToListAsync();

            // 3. Verifica se há parada em aberto para esta linha
            var paradaAtiva = await _context.tbl_paradas_log
                .Where(p => p.Linha == line && p.FimParada == null)
                .Select(p => new { p.Id, p.Motivo })
                .FirstOrDefaultAsync();

            // 4. Busca a ÚLTIMA placa (independente de média) para o cronômetro visual
            var ultimaPlaca = await _context.tbl_producaolog
                .Where(l => l.Linha == line && l.Quantidade > 0)
                .OrderByDescending(l => l.Timestamp)
                .Select(l => l.Timestamp)
                .FirstOrDefaultAsync();

            double mediaReal = registros.Any() ? registros.Average(r => r.SegundosCiclo) : 0;

            return Json(new
            {
                success = true,
                taktTeorico = Math.Round(taktTeorico, 1),
                cicloRealMedio = Math.Round(mediaReal, 1),
                // Timestamp da última placa para o cronômetro do JS não "mentir"
                ultimoRegistroIso = ultimaPlaca.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                paradaEmAberto = paradaAtiva != null,
                idParada = paradaAtiva?.Id,
                motivoParada = paradaAtiva?.Motivo,
                statusConfianca = registros.Count >= 10 ? "ALTA" : "CALIBRANDO",
                quantidadeAmostras = registros.Count
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
    [HttpGet]
    public async Task<IActionResult> ObterRelatorioParadas(string line, DateTime inicio, DateTime fim)
    {
        // 1. Busca os dados brutos do banco e joga para a memória (.ToListAsync)
        var consulta = await _context.tbl_paradas_log
            .Where(p => p.Linha == line && p.InicioParada >= inicio && p.InicioParada <= fim)
            .OrderByDescending(p => p.InicioParada)
            .ToListAsync();

        // 2. Agora formatamos os dados com segurança fora da query SQL
        var dadosFormatados = consulta.Select(p => {

            // Cálculo de Duração (Garante que p.TempoTotalSegundos seja tratado como int)
            // Se o erro CS0019 persistir no ??, verifique se o campo no banco já é int (não nulo)
            int segundos = p.TempoTotalSegundos;

            TimeSpan t = TimeSpan.FromSeconds(segundos);
            string duracaoHms = string.Format("{0:D2}:{1:D2}:{2:D2}",
                                (int)t.TotalHours,
                                t.Minutes,
                                t.Seconds);

            return new
            {
                id = p.Id,
                // Formata o DateTime (Garantido que InicioParada não é nulo)
                inicio = p.InicioParada.ToString(),

                // CORREÇÃO DO ERRO CS1501: 
                // Para DateTime nulo (DateTime?), usamos p.Value para acessar o método ToString correto
                fim = p.FimParada.HasValue
                      ? p.FimParada.Value.ToString()
                      : "Em Aberto",
                segundosTotal = segundos,

                duracao = duracaoHms,
                motivo = p.Motivo ?? "Não Justificado",
                usuario = p.Usuario ?? "Sistema"
            };
        });

        return Json(dadosFormatados);
    }



    [HttpGet]
    public IActionResult CalcularCycleTimeAutomatico(string line)
    {
        // 1. Busca os logs reais usando sua classe ProducaoLog
        var logsBrutos = _context.tbl_producaolog
            .Where(l => l.Linha == line)
            .OrderByDescending(l => l.Timestamp)
            .Take(500)
            .ToList();

        if (!logsBrutos.Any()) return Json(new { success = false });

        string produtoAtual = logsBrutos.First().Produto;

        // 2. Busca a Meta (Importante: m.Produto == produtoAtual)
        var metaObj = _context.tbl_metasproducao
            .FirstOrDefault(m => m.Linha == line && m.Produto == produtoAtual);

        double metaHoraPlacas = metaObj?.MetaHora ?? 120;
        double pan = (metaObj?.Panelizacao > 0) ? metaObj.Panelizacao : 1;
        double taktTeorico = 3600.0 / (metaHoraPlacas / pan);

        // 3. Filtra os pontos de mudança de quantidade (Usando a classe completa)
        var pontosDeSaida = new List<ProducaoLog>();
        int? ultimaQuantidade = null;

        foreach (var log in logsBrutos)
        {
            if (ultimaQuantidade == null || log.Quantidade < ultimaQuantidade)
            {
                pontosDeSaida.Add(log);
                ultimaQuantidade = log.Quantidade;
            }
            if (pontosDeSaida.Count == 11) break;
        }

        // 4. CÁLCULO DO CICLO REAL NORMALIZADO
        var intervalos = new List<double>();
        for (int i = 0; i < pontosDeSaida.Count - 1; i++)
        {
            var atual = pontosDeSaida[i];
            var anterior = pontosDeSaida[i + 1];

            double diffSegundos = (atual.Timestamp - anterior.Timestamp).TotalSeconds;
            int pecasProduzidas = atual.Quantidade - anterior.Quantidade;

            if (pecasProduzidas > 0)
            {
                // Se demorou 481s e saíram 4 placas (com pan 1), o ciclo é 120.25s
                // Se o painel for 4 (pan=4), o ciclo seria 481s por painel.
                double paineisProduzidos = (double)pecasProduzidas;
                double cicloPorPainel = diffSegundos / paineisProduzidos;

                // Filtro de parada: descarta intervalos maiores que 15 min (900s)
                if (diffSegundos > 1 && diffSegundos < 900)
                {
                    intervalos.Add(cicloPorPainel);
                }
            }
        }

        if (intervalos.Count == 0) return Json(new { success = false, message = "Sem intervalos válidos" });

        // 5. Média Estabilizada (Remove o maior e o menor valor das amostras)
        double mediaReal;
        if (intervalos.Count >= 3)
        {
            mediaReal = intervalos.OrderBy(x => x).Skip(1).Take(intervalos.Count - 2).Average();
        }
        else
        {
            mediaReal = intervalos.Average();
        }

        // 6. Parada Automática (Baseado no Takt: 3x o Takt sem log = Eficiência 0)
        bool estaParado = (DateTime.Now - pontosDeSaida.First().Timestamp).TotalSeconds > (taktTeorico * 3);

        return Json(new
        {
            success = true,
            produto = produtoAtual,
            taktTeoricoSegundos = taktTeorico,
            taktTeoricoFormatado = TimeSpan.FromSeconds(taktTeorico).ToString(@"mm\:ss"),
            cicloRealMedioSegundos = mediaReal,
            cicloRealFormatado = TimeSpan.FromSeconds(mediaReal).ToString(@"mm\:ss"),
            eficiencia = estaParado ? 0 : Math.Round((taktTeorico / mediaReal) * 100, 1),
            amostras = intervalos.Count,
            pan = pan,
            status = estaParado ? "PARADA" : "PRODUZINDO"
        });
    }

    // DTO para mapear resultado da query SQL raw
    public class RegistroCicloRaw
    {
        public DateTime Timestamp { get; set; }
        public double SegundosCiclo { get; set; }
    }

    // Helper para formatar segundos em MM:SS (mais limpo para dashboard)
    private string FormatarTempoSimples(double segundos)
    {
        if (segundos <= 0 || double.IsNaN(segundos) || double.IsInfinity(segundos))
            return "--:--";

        var ts = TimeSpan.FromSeconds(segundos);

        // Se for menos de 1 minuto, mostra segundos.decimais (ex: 45.3s)
        if (ts.TotalSeconds < 60)
            return $"{ts.TotalSeconds:00.0}s";

        // Se for mais de 1 minuto, mostra MM:SS (ex: 01:30)
        return $"{ts.Minutes:00}:{ts.Seconds:00}";
    }

    // Método auxiliar para cálculo de desvio padrão (já existente, mantido para compatibilidade)
    private double CalcularDesvioPadrao(List<double> valores)
    {
        if (valores == null || valores.Count < 2) return 0;

        var media = valores.Average();
        var somaQuadrados = valores.Sum(v => Math.Pow(v - media, 2));
        return Math.Sqrt(somaQuadrados / (valores.Count - 1));
    }

    public class RegistroCiclo
    {
        public DateTime timestamp { get; set; }
        public double segundos_ciclo { get; set; }
    }

    // DTO simplificado
    public class RegistroMudanca
    {
        public DateTime timestamp { get; set; }
        public int quantidade { get; set; }
        public int? quantidade_anterior { get; set; }
        public double segundos_entre_mudancas { get; set; }
    }

    public class RegistroProducaoDebug
    {
        public DateTime timestamp { get; set; }
        public int quantidade { get; set; }
        public int? qtd_anterior { get; set; }
        public DateTime? ts_anterior { get; set; }
        public int qtd_diferenca { get; set; }
        public double segundos_intervalo { get; set; }
    }
    // DTO com propriedades em minúsculo para mapear corretamente
    public class RegistroProducao
    {
        public DateTime timestamp { get; set; }
        public int quantidade { get; set; }
        public int? quantidadeanterior { get; set; }
        public double segundosintervalo { get; set; }
        public int qtdproduzida { get; set; }
    }

    // DTO para receber resultado da query SQL
    public class IntervaloCycleTime
    {
        public DateTime Timestamp { get; set; }
        public int Quantidade { get; set; }
        public DateTime? TimestampAnterior { get; set; }
        public double SegundosIntervalo { get; set; }
    }


    // --- NOVOS MÉTODOS PARA VISÃO DE HORAS DISPONÍVEIS ---

    private (DateTime Inicio, DateTime Fim) ObterHorariosTurno(ConfiguracaoLinha config, DateTime agora)
    {
        // Fallback se não houver config
        if (config == null)
        {
            if (agora.Hour >= 6 && agora.Hour < 14) return (agora.Date.AddHours(6), agora.Date.AddHours(14));
            if (agora.Hour >= 14 && agora.Hour < 22) return (agora.Date.AddHours(14), agora.Date.AddHours(22));
            DateTime iniT3Padrao = (agora.Hour >= 22) ? agora.Date.AddHours(22) : agora.Date.AddDays(-1).AddHours(22);
            return (iniT3Padrao, iniT3Padrao.AddHours(8));
        }

        int minAtual = agora.Hour * 60 + agora.Minute;

        // CORREÇÃO: Pegando o TotalMinutes diretamente do TimeSpan? (Tratando nulos com GetValueOrDefault)
        int t1I = (int)config.T1Inicio.GetValueOrDefault().TotalMinutes;
        int t1F = (int)config.T1Fim.GetValueOrDefault().TotalMinutes;
        int t2I = (int)config.T2Inicio.GetValueOrDefault().TotalMinutes;
        int t2F = (int)config.T2Fim.GetValueOrDefault().TotalMinutes;
        int t3I = (int)config.T3Inicio.GetValueOrDefault().TotalMinutes;
        int t3F = (int)config.T3Fim.GetValueOrDefault().TotalMinutes;

        if (EstaNoIntervalo(minAtual, t1I, t1F))
            return (agora.Date.AddMinutes(t1I), t1F > t1I ? agora.Date.AddMinutes(t1F) : agora.Date.AddDays(1).AddMinutes(t1F));

        if (EstaNoIntervalo(minAtual, t2I, t2F))
            return (agora.Date.AddMinutes(t2I), t2F > t2I ? agora.Date.AddMinutes(t2F) : agora.Date.AddDays(1).AddMinutes(t2F));

        // Turno 3
        DateTime turno3Inicio = (minAtual >= t3I) ? agora.Date.AddMinutes(t3I) : agora.Date.AddDays(-1).AddMinutes(t3I);
        DateTime turno3Fim = (t3F < t3I) ? turno3Inicio.AddMinutes((1440 - t3I) + t3F) : turno3Inicio.AddMinutes(t3F - t3I);

        return (turno3Inicio, turno3Fim);
    }

    private double CalcularMinutosUteis((DateTime Inicio, DateTime Fim) janela, List<ParadaPlanejada> paradas, DateTime agora)
    {
        int duracaoTotal = (int)(janela.Fim - janela.Inicio).TotalMinutes;
        if (duracaoTotal < 0) duracaoTotal += 1440;
        return duracaoTotal - CalcularDescontosParadas(janela.Inicio, duracaoTotal, paradas, agora);
    }

    private double CalcularMinutosProduzidosAteAgora((DateTime Inicio, DateTime Fim) janela, List<ParadaPlanejada> paradas, DateTime agora)
    {
        int decorrido = (int)(agora - janela.Inicio).TotalMinutes;
        if (decorrido < 0) decorrido += 1440;
        int duracaoMax = (int)(janela.Fim - janela.Inicio).TotalMinutes;
        if (duracaoMax < 0) duracaoMax += 1440;
        decorrido = Math.Min(decorrido, duracaoMax);
        return Math.Max(0, decorrido - CalcularDescontosParadas(janela.Inicio, decorrido, paradas, agora));
    }

    private int CalcularDescontosParadas(DateTime inicioTurno, int duracaoAnalise, List<ParadaPlanejada> paradas, DateTime agora)
    {
        int desconto = 0;
        int tIniMins = inicioTurno.Hour * 60 + inicioTurno.Minute;
        string diaHoje = ObterDiaSemanaAbreviado(agora);

        foreach (var p in paradas)
        {
            if (!ChecarDiaAtivo(p, diaHoje)) continue;

            // CORREÇÃO CS1061: Removido .HasValue e .Value pois o tipo é TimeSpan
            int pIni = (int)p.HoraInicio.TotalMinutes;
            int pFim = (int)p.HoraFim.TotalMinutes;

            int pDur = pFim - pIni;
            if (pDur < 0) pDur += 1440;

            int[] offsets = { 0, 1440, -1440 };
            foreach (var offset in offsets)
            {
                int sMax = Math.Max(tIniMins, pIni + offset);
                int eMin = Math.Min(tIniMins + duracaoAnalise, pFim + offset);
                if (sMax < eMin) desconto += (eMin - sMax);
            }
        }
        return desconto;
    }

    private bool ChecarDiaAtivo(ParadaPlanejada p, string dia) => dia switch { "Seg" => p.Seg, "Ter" => p.Ter, "Qua" => p.Qua, "Qui" => p.Qui, "Sex" => p.Sex, "Sab" => p.Sab, "Dom" => p.Dom, _ => false };
    private string ObterDiaSemanaAbreviado(DateTime dt) => dt.DayOfWeek switch { DayOfWeek.Monday => "Seg", DayOfWeek.Tuesday => "Ter", DayOfWeek.Wednesday => "Qua", DayOfWeek.Thursday => "Qui", DayOfWeek.Friday => "Sex", DayOfWeek.Saturday => "Sab", DayOfWeek.Sunday => "Dom", _ => "" };

    private int TToM(string horaStr)
    {
        if (string.IsNullOrEmpty(horaStr)) return 0;
        try
        {
            var partes = horaStr.Split(':');
            return (int.Parse(partes[0]) * 60) + int.Parse(partes[1]);
        }
        catch { return 0; }
    }

    private bool EstaNoIntervalo(int atual, int inicio, int fim)
    {
        if (inicio < fim)
            return atual >= inicio && atual < fim;

        // Trata virada de meia-noite (ex: 22:00 às 06:00)
        return atual >= inicio || atual < fim;
    }

}