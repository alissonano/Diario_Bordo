using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Diario_de_Bordo.Services
{
    public class PickupReportExtractor
    {
        private readonly ILogger<PickupReportExtractor> _logger;
        private readonly string _baseUrl = "https://172.20.100.140:9443/lws/";

        public class FeederPerformanceRecord
        {
            public string LotName { get; set; }
            public string Machine { get; set; }
            public string Table { get; set; }
            public string Feeder { get; set; }
            public string PartNumber { get; set; }
            public int PickupCount { get; set; }
            public int ErrorCount { get; set; }
            public double SpoilagePercent { get; set; }
        }

        public PickupReportExtractor(ILogger<PickupReportExtractor> logger)
        {
            _logger = logger;
        }
        public async Task<string> ExtrairParaExcelAsync(string destinoDiretorio, string dataInicioStr, string dataFimStr)
{
    string caminhoFinal = null;
    try
    {
        // 1. DEFINIR O CAMINHO FIXO DO NAVEGADOR (FUNDAMENTAL PARA IIS)
        string chromePath = @"C:\PlaywrightBrowsers\ms-playwright\chromium-1208\chrome-win64\chrome.exe";

        if (!File.Exists(chromePath))
        {
            throw new Exception($"Navegador não encontrado no servidor: {chromePath}");
        }

        using var playwright = await Playwright.CreateAsync();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            ExecutablePath = chromePath,
            Headless = true, 
            Args = new[] { "--no-sandbox", "--disable-gpu", "--disable-dev-shm-usage" }
        };

        await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);

        // Ignorar erros de SSL (importante para o IP 172.20.100.140)
        var context = await browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
        var page = await context.NewPageAsync();

        // --- TRATAMENTO DAS DATAS ---
        // O LWS espera o valor no formato "yyyyMMdd" no value do <select>
        // O modal costuma enviar "yyyy-MM-ddTHH:mm" ou "yyyy-MM-dd"
        DateTime dtInicio = DateTime.Parse(dataInicioStr);
        DateTime dtFim = DateTime.Parse(dataFimStr);

        string valueInicio = dtInicio.ToString("yyyyMMdd");
        string valueFim = dtFim.ToString("yyyyMMdd");

        // Navegação
        await page.GotoAsync(_baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.FillAsync("#userid", "pfsc");
        await page.FillAsync("#password", "1qaz2wsx");
        await page.ClickAsync("input[type='submit']");

        await page.WaitForSelectorAsync("#menu");
        await page.ClickAsync("#menu table tr td table tr:nth-child(3) td a img");
        await page.ClickAsync("text=Set time information");

        // --- SELEÇÃO DINÂMICA ---
        await page.Locator("#startdate").SelectOptionAsync(new[] { valueInicio });
        await page.Locator("#enddate").SelectOptionAsync(new[] { valueFim });

        var popupTask = context.WaitForPageAsync();
        await page.ClickAsync("input[value='Show Product Management Information']");
        var reportPage = await popupTask;

        await reportPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(2000);

        string textoBruto = await reportPage.InnerTextAsync("body");
        var resultados = ProcessarTextoLWS(textoBruto);

        if (resultados.Any())
        {
            if (!Directory.Exists(destinoDiretorio)) Directory.CreateDirectory(destinoDiretorio);
            string caminhoCompleto = Path.Combine(destinoDiretorio, $"Performance_SMD5_LWS_{valueInicio}_{valueFim}.xlsx");

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Performance_Data");

                // Cabeçalho formatado com o período real solicitado
                string cabeçalhoPeriodo = $"RELATÓRIO PANASONIC | DE: {dtInicio:dd/MM/yyyy HH:mm} ATÉ {dtFim:dd/MM/yyyy HH:mm}";
                var rangeHeader = worksheet.Range(1, 1, 1, 8);
                rangeHeader.Merge().Value = cabeçalhoPeriodo;

                var estiloRef = rangeHeader.Style;
                estiloRef.Font.Bold = true;
                estiloRef.Font.FontColor = XLColor.White;
                estiloRef.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
                estiloRef.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Headers da Tabela
                string[] headers = { "Lote", "Máquina", "Módulo", "Feeder", "Part Number", "Pickups", "Erros", "Rejeito %" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(3, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D3D3D3");
                }

                // Preenchimento dos Dados
                for (int i = 0; i < resultados.Count; i++)
                {
                    int r = i + 4;
                    var item = resultados[i];
                    worksheet.Cell(r, 1).Value = item.LotName;
                    worksheet.Cell(r, 2).Value = item.Machine;
                    worksheet.Cell(r, 3).Value = item.Table;
                    worksheet.Cell(r, 4).Value = item.Feeder;
                    worksheet.Cell(r, 5).Value = item.PartNumber;
                    worksheet.Cell(r, 6).Value = item.PickupCount;
                    worksheet.Cell(r, 7).Value = item.ErrorCount;

                    var cellPct = worksheet.Cell(r, 8);
                    cellPct.Value = item.SpoilagePercent;
                    cellPct.Style.NumberFormat.Format = "0.00%";

                    if (item.SpoilagePercent > 0.01) cellPct.Style.Font.FontColor = XLColor.Red;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(caminhoCompleto);
            }
            caminhoFinal = caminhoCompleto;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError($"[ERRO IIS LWS] {ex.Message}");
        throw;
    }
    return caminhoFinal;
}
    //    public async Task<string> ExtrairParaExcelAsync(string destinoDiretorio)
    //    {
    //        string caminhoFinal = null;
    //        try
    //        {
    //            // 1. DEFINIR O CAMINHO FIXO DO NAVEGADOR (FUNDAMENTAL PARA IIS)
    //            // Certifique-se de incluir o 'chrome.exe' no final do caminho
    //            string chromePath = @"C:\PlaywrightBrowsers\ms-playwright\chromium-1208\chrome-win64\chrome.exe";

    //            if (!File.Exists(chromePath))
    //            {
    //                throw new Exception($"Navegador não encontrado no servidor: {chromePath}");
    //            }

    //            using var playwright = await Playwright.CreateAsync();

    //            var launchOptions = new BrowserTypeLaunchOptions
    //            {
    //                ExecutablePath = @"C:\PlaywrightBrowsers\ms-playwright\chromium-1208\chrome-win64\chrome.exe",
    //                Headless = true, // Obrigatório para o IIS não tentar abrir janela invisível
    //                Args = new[] {
    //    "--no-sandbox",
    //    "--disable-gpu",
    //    "--disable-dev-shm-usage"
    //}
    //            };

    //            await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);

    //            // Ignorar erros de SSL (comum em IPs locais de máquinas SMT)
    //            var context = await browser.NewContextAsync(new BrowserNewContextOptions { IgnoreHTTPSErrors = true });
    //            var page = await context.NewPageAsync();

    //            string dataOntem = DateTime.Now.AddDays(-1).ToString("yyyyMMdd");
    //            string dataHoje = DateTime.Now.ToString("yyyyMMdd");

    //            // Navegação
    //            await page.GotoAsync(_baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    //            await page.FillAsync("#userid", "pfsc");
    //            await page.FillAsync("#password", "1qaz2wsx");
    //            await page.ClickAsync("input[type='submit']");

    //            await page.WaitForSelectorAsync("#menu");
    //            await page.ClickAsync("#menu table tr td table tr:nth-child(3) td a img");
    //            await page.ClickAsync("text=Set time information");

    //            await page.Locator("#startdate").SelectOptionAsync(new[] { dataOntem });
    //            await page.Locator("#enddate").SelectOptionAsync(new[] { dataHoje });

    //            var popupTask = context.WaitForPageAsync();
    //            await page.ClickAsync("input[value='Show Product Management Information']");
    //            var reportPage = await popupTask;

    //            // Esperar o carregamento completo do Pop-up
    //            await reportPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
    //            await Task.Delay(2000);

    //            string textoBruto = await reportPage.InnerTextAsync("body");
    //            var resultados = ProcessarTextoLWS(textoBruto);

    //            if (resultados.Any())
    //            {
    //                if (!Directory.Exists(destinoDiretorio)) Directory.CreateDirectory(destinoDiretorio);
    //                string caminhoCompleto = Path.Combine(destinoDiretorio, $"Performance_SMD5(PANASONIC)_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");

    //                using (var workbook = new XLWorkbook())
    //                {
    //                    var worksheet = workbook.Worksheets.Add("Performance_Data");

    //                    // Cabeçalho de referência
    //                    string inicio = DateTime.Now.AddDays(-1).ToString("dd/MM/yyyy 00:00");
    //                    string fim = DateTime.Now.ToString("dd/MM/yyyy 00:00");
    //                    var rangeHeader = worksheet.Range(1, 1, 1, 8);
    //                    rangeHeader.Merge().Value = $"RELATÓRIO PANASONIC | DE: {inicio} ATÉ {fim}";

    //                    var estiloRef = rangeHeader.Style;
    //                    estiloRef.Font.Bold = true;
    //                    estiloRef.Font.FontColor = XLColor.White;
    //                    estiloRef.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
    //                    estiloRef.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    //                    // Headers da Tabela
    //                    string[] headers = { "Lote", "Máquina", "Módulo", "Feeder", "Part Number", "Pickups", "Erros", "Rejeito %" };
    //                    for (int i = 0; i < headers.Length; i++)
    //                    {
    //                        var cell = worksheet.Cell(3, i + 1);
    //                        cell.Value = headers[i];
    //                        cell.Style.Font.Bold = true;
    //                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D3D3D3");
    //                    }

    //                    // Dados
    //                    for (int i = 0; i < resultados.Count; i++)
    //                    {
    //                        int r = i + 4;
    //                        var item = resultados[i];
    //                        worksheet.Cell(r, 1).Value = item.LotName;
    //                        worksheet.Cell(r, 2).Value = item.Machine;
    //                        worksheet.Cell(r, 3).Value = item.Table;
    //                        worksheet.Cell(r, 4).Value = item.Feeder;
    //                        worksheet.Cell(r, 5).Value = item.PartNumber;
    //                        worksheet.Cell(r, 6).Value = item.PickupCount;
    //                        worksheet.Cell(r, 7).Value = item.ErrorCount;

    //                        var cellPct = worksheet.Cell(r, 8);
    //                        cellPct.Value = item.SpoilagePercent;
    //                        cellPct.Style.NumberFormat.Format = "0.00%";

    //                        if (item.SpoilagePercent > 0.01) cellPct.Style.Font.FontColor = XLColor.Red;
    //                    }

    //                    worksheet.Columns().AdjustToContents();
    //                    workbook.SaveAs(caminhoCompleto);
    //                }
    //                caminhoFinal = caminhoCompleto;
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError($"[ERRO IIS] {ex.Message}");
    //            // Log adicional para rastrear onde o Playwright parou
    //            throw;
    //        }
    //        return caminhoFinal;
    //    }

        private List<FeederPerformanceRecord> ProcessarTextoLWS(string texto)
        {
            var lista = new List<FeederPerformanceRecord>();
            var linhas = texto.Split(new[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            string loteAtual = "N/A";
            bool dentroDaSecaoFeeder = false;

            foreach (var linha in linhas)
            {
                string linhaLimpa = linha.Trim();

                // Identificação do Lote
                if (linhaLimpa.Contains("Per Lot Information"))
                {
                    var match = Regex.Match(linhaLimpa, @"Lot\[(.*?)\]");
                    loteAtual = match.Success ? match.Groups[1].Value : "N/A";
                    dentroDaSecaoFeeder = false;
                    continue;
                }

                // Início da Tabela de Performance
                if (linhaLimpa.Contains("Pickup Count per Feeder"))
                {
                    dentroDaSecaoFeeder = true;
                    continue;
                }

                // Fim da Seção
                if (dentroDaSecaoFeeder && (linhaLimpa.Contains("Nozzle") || linhaLimpa.Contains("Production Information")))
                {
                    dentroDaSecaoFeeder = false;
                }

                if (dentroDaSecaoFeeder && loteAtual != "N/A")
                {
                    // Split por espaços duplos ou tabs
                    var colunas = Regex.Split(linhaLimpa, @"\s{2,}|\t").Select(c => c.Trim()).ToList();

                    // Valida se é uma linha de dados (M-Idx, T-Idx, etc)
                    if (colunas.Count >= 9 && int.TryParse(colunas[0], out int mIdx))
                    {
                        try
                        {
                            int.TryParse(colunas[1], out int tIdx);

                            // --- LÓGICA DO FEEDER (SLOT + LADO) ---
                            // 1. Endereço do Feeder (Ex: "101")
                            string feederBruto = colunas[2];

                            // 2. Extrai os 2 últimos caracteres (Ex: "01")
                            string slotCurto = feederBruto.Length >= 2
                                ? feederBruto.Substring(feederBruto.Length - 2)
                                : feederBruto;

                            // 3. Identificador de Lado (Está na COLUNA LOGO APÓS o endereço)
                            // Usamos colunas[3] porque é a vizinha imediata
                            string ladoS = colunas.Count > 3 ? colunas[3].ToUpper().Trim() : "";

                            string feederFinal = slotCurto;

                            // 4. Regra de Negócio: L e R concatenam, S ou vazio não.
                            if (ladoS == "L" || ladoS == "R")
                            {
                                feederFinal = slotCurto + ladoS;
                            }

                            // ... segue para montar o objeto FeederPerformanceRecord ...
                            feederFinal = feederFinal;
                            // Se for "S", mantém apenas o slotCurto (ex: "01")

                            // --- MAPEAMENTO DE MÁQUINA E TABELA ---
                            string nomeMaquina = mIdx == 1 ? "NPM1" : mIdx == 2 ? "NPM2" : $"M{mIdx}";
                            string nomeTabela = (mIdx == 1) ? (tIdx == 1 ? "TBL1" : "TBL2") : (tIdx == 1 ? "TBL3" : "TBL4");

                            // --- DADOS NUMÉRICOS ---
                            int pickups = int.Parse(Regex.Replace(colunas[6], @"[^\d]", ""));
                            int erros = int.Parse(Regex.Replace(colunas[7], @"[^\d]", ""));

                            if (pickups > 0)
                            {
                                lista.Add(new FeederPerformanceRecord
                                {
                                    LotName = loteAtual,
                                    Machine = nomeMaquina,
                                    Table = nomeTabela,
                                    Feeder = feederFinal,
                                    PartNumber = colunas[4],
                                    PickupCount = pickups,
                                    ErrorCount = erros,
                                    SpoilagePercent = (double)erros / pickups
                                });
                            }
                        }
                        catch { continue; }
                    }
                }
            }
            return lista;
        }
    }
}