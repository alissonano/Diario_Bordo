using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Diario_de_Bordo.Services
{
    public class PickupReportExtractorFuji
    {
        private readonly ILogger<PickupReportExtractorFuji> _logger;
        //private readonly string _caminhoRedeXml = @"\\FLEXA-CLIENT04\ReportDiario";
        private readonly string _caminhoRedeXml = @"\\172.19.100.2\Rejeitos";

        public PickupReportExtractorFuji(ILogger<PickupReportExtractorFuji> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ExtrairArquivoSelecionadoAsync(string nomeArquivoCompleto)
        {
            string pathFuji = @"\\172.19.100.2\Rejeitos";
            string xmlPathCompleto = Path.Combine(pathFuji, nomeArquivoCompleto);

            string servidorDestino = @"\\192.168.1.8\publica";
            string pastaBaseRede = @"\\192.168.1.8\publica\Sistema SMD\Relatorio Rejeito SMT\SMD6-9-10";

            // Lista das linhas para gerar os 3 arquivos
            string[] todasAsLinhas = { "SMD6", "SMD9", "SMD10" };

            var credFuji = new NetworkCredential("ADMIN", "admin", "172.19.100.2");
            var credPublica = new NetworkCredential("sistema.smd", "Gbr@2025", "ad.gbrcomponentes.com.br");

            try
            {
                using (new NetworkShareConnection(pathFuji, credFuji))
                {
                    using (new NetworkShareConnection(servidorDestino, credPublica))
                    {
                        if (!File.Exists(xmlPathCompleto)) return false;

                        foreach (var linha in todasAsLinhas)
                        {
                            List<string> maquinasDaLinha = GetMaquinasPorLinha(linha);
                            if (maquinasDaLinha.Count == 0) continue;

                            string nomeExcel = $"Performance_Fuji_{linha}_{Path.GetFileNameWithoutExtension(nomeArquivoCompleto)}.xlsx";
                            string excelPathFinal = Path.Combine(pastaBaseRede, nomeExcel);

                            // Chama seu método original
                            GerarExcelFiltrado(xmlPathCompleto, excelPathFinal, linha, maquinasDaLinha);
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> ExtrairTodosRelatoriosAsync(string nomeArquivoCompleto)
        {
            // 1. Configurações de Rede
            string pathFuji = @"\\172.19.100.2\Rejeitos";
            string xmlPathCompleto = Path.Combine(pathFuji, nomeArquivoCompleto);
            string servidorDestino = @"\\192.168.1.8\publica";
            string pastaBaseRede = @"\\192.168.1.8\publica\Sistema SMD\Relatorio Rejeito SMT\SMD6-9-10";

            // 2. Definição das linhas que serão processadas automaticamente
            string[] todasAsLinhas = { "SMD6", "SMD9", "SMD10" };

            // 3. Credenciais
            var credFuji = new NetworkCredential("ADMIN", "admin", "172.19.100.2");
            var credPublica = new NetworkCredential("sistema.smd", "Gbr@2025", "ad.gbrcomponentes.com.br");

            try
            {
                using (new NetworkShareConnection(pathFuji, credFuji))
                {
                    using (new NetworkShareConnection(servidorDestino, credPublica))
                    {
                        if (!File.Exists(xmlPathCompleto)) return false;

                        foreach (var linha in todasAsLinhas)
                        {
                            // Obtém as máquinas específicas desta linha (NXTAL6, etc)
                            List<string> maquinasDaLinha = GetMaquinasPorLinha(linha);

                            if (maquinasDaLinha.Count == 0) continue;

                            // Monta o nome do Excel específico (Ex: Performance_Fuji_SMD6_...)
                            string nomeExcel = $"Performance_Fuji_{linha}_{Path.GetFileNameWithoutExtension(nomeArquivoCompleto)}.xlsx";
                            string excelPathFinal = Path.Combine(pastaBaseRede, nomeExcel);

                            // CHAMADA DO SEU MÉTODO ORIGINAL
                            // Ele será executado 3 vezes, uma para cada linha, gerando 3 arquivos Excel
                            GerarExcelFiltrado(xmlPathCompleto, excelPathFinal, linha, maquinasDaLinha);
                        }

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FUJI] Erro no processamento em lote: {ex.Message}");
                return false;
            }
        }
        public async Task<string> ExtrairParaExcelAsync(string destinoDiretorio, string linhaAlvo, string dataSelecionada)
        {
            try
            {
                // 1. Converter a data vinda do Modal (yyyy-MM-dd...) para o padrão do arquivo (yyyyMMdd)
                // Ex: "2026-04-15T06:00" -> "20260415"
                DateTime dtParsed = DateTime.Parse(dataSelecionada);
                string sufixoData = dtParsed.ToString("yyyyMMdd");

                var diretorioRede = new DirectoryInfo(_caminhoRedeXml);

                // 2. Busca específica: Arquivos que começam com PUS e contêm a data no nome
                // Ex padrão: PUS20260415060000000.XML
                var arquivoXml = diretorioRede.GetFiles($"PUS{sufixoData}*.xml")
                                             .FirstOrDefault();

                if (arquivoXml == null)
                {
                    _logger.LogWarning($"[FUJI] Relatório não encontrado para a data: {sufixoData}");
                    return null;
                }

                List<string> maquinasDaLinha = GetMaquinasPorLinha(linhaAlvo);

                // Nome do Excel agora inclui a data do relatório para organização
                string nomeExcel = $"Performance_Fuji_{linhaAlvo}_{sufixoData}.xlsx";
                string caminhoFinal = Path.Combine(destinoDiretorio, nomeExcel);

                // 3. Processa o XML encontrado
                GerarExcelFiltrado(arquivoXml.FullName, caminhoFinal, linhaAlvo, maquinasDaLinha);

                return caminhoFinal;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FUJI ERRO] Falha ao processar linha {linhaAlvo}: {ex.Message}");
                return null;
            }
        }

        private List<string> GetMaquinasPorLinha(string linha)
        {
            // Mapeamento manual: Ajuste aqui os nomes conforme aparecem no XML (<Name>)
            return linha switch
            {
                "SMD6" => new List<string> { "NXTAL6_", "NXTBL6_" },
                "SMD10" => new List<string> { "NXTAL10", "NXTBL10" },
                "SMD9" => new List<string> { "NXTAL9", "NXTBL9" },
                _ => new List<string>()
            };
        }



        private void GerarExcelFiltrado(string xmlPath, string excelPath, string nomeLinha, List<string> maquinasFiltro)
        {
            var xml = XDocument.Load(xmlPath);

            var searchNode = xml.Element("Report")?.Element("Search");
            string dataInicio = searchNode?.Element("FromDate")?.Value ?? "N/A";
            string dataFim = searchNode?.Element("ToDate")?.Value ?? "N/A";

            var listaItens = new List<FujiDataRow>();
            var filtrosNormalizados = maquinasFiltro.Select(f => f.Trim().ToUpper()).ToList();

            var maquinasNoXml = xml.Descendants("Machine")
                                   .Where(m => filtrosNormalizados.Contains(m.Element("Name")?.Value?.Trim().ToUpper()));

            foreach (var maq in maquinasNoXml)
            {
                string nomeMaq = maq.Element("Name")?.Value;
                foreach (var rec in maq.Descendants("Recipe"))
                {
                    // Captura o nome do Modelo (Tag Name dentro de Recipe)
                    string nomeModelo = rec.Element("Name")?.Value ?? "N/A";

                    foreach (var item in rec.Descendants("Item"))
                    {
                        int.TryParse(item.Element("Stage")?.Value, out int modulo);
                        int.TryParse(item.Element("Pos")?.Value, out int slot);
                        long.TryParse(item.Element("PickupCount")?.Value, out long pickups);
                        long.TryParse(item.Element("ErrorParts")?.Value, out long erros);

                        listaItens.Add(new FujiDataRow
                        {
                            Modelo = nomeModelo,
                            Maquina = nomeMaq,
                            Modulo = modulo,
                            Slot = slot,
                            PartNumber = item.Element("PartName")?.Value,
                            Pickups = pickups,
                            Erros = erros
                        });
                    }
                }
            }

            var dadosProcessados = listaItens
                .GroupBy(x => new { x.Modelo, x.Maquina, x.Modulo, x.Slot, x.PartNumber })
                .Select(g => new {
                    g.Key.Modelo,
                    g.Key.Maquina,
                    g.Key.Modulo,
                    g.Key.Slot,
                    g.Key.PartNumber,
                    TotalPickups = g.Sum(s => s.Pickups),
                    TotalErros = g.Sum(s => s.Erros),
                    TaxaRejeito = g.Sum(s => s.Pickups) > 0
                                  ? ((double)g.Sum(s => s.Erros) / g.Sum(s => s.Pickups)) * 100
                                  : 0
                })
                .OrderBy(o => o.Modelo).ThenBy(o => o.Maquina).ThenBy(o => o.Modulo).ThenBy(o => o.Slot).ToList();

            if (!dadosProcessados.Any()) return;

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Performance");

                // --- CABEÇALHO (9 Colunas) ---
                var rangeHeader = worksheet.Range(1, 1, 1, 9);
                rangeHeader.Merge().Value = $"PERFORMANCE FUJI - {nomeLinha} | PERÍODO: {dataInicio} ~ {dataFim}";
                rangeHeader.Style.Font.Bold = true;
                rangeHeader.Style.Font.FontColor = XLColor.White;
                rangeHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
                rangeHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var rangeSubHeader = worksheet.Range(2, 1, 2, 9);
                rangeSubHeader.Merge().Value = $"Relatório extraído em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                rangeSubHeader.Style.Font.Italic = true;
                rangeSubHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Títulos das Colunas (Modelo agora é a 1ª)
                string[] colunas = { "Modelo", "Máquina", "Módulo", "Slot", "Part Number", "Pickups", "Erros", "Rejeito %", "Status" };
                for (int i = 0; i < colunas.Length; i++)
                {
                    var cell = worksheet.Cell(3, i + 1);
                    cell.Value = colunas[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D3D3D3");
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                int rowIdx = 4;
                foreach (var d in dadosProcessados)
                {
                    worksheet.Cell(rowIdx, 1).Value = d.Modelo;     // Coluna A
                    worksheet.Cell(rowIdx, 2).Value = d.Maquina;    // Coluna B
                    worksheet.Cell(rowIdx, 3).Value = d.Modulo;     // Coluna C
                    worksheet.Cell(rowIdx, 4).Value = d.Slot;       // Coluna D
                    worksheet.Cell(rowIdx, 5).Value = d.PartNumber; // Coluna E
                    worksheet.Cell(rowIdx, 6).Value = d.TotalPickups;
                    worksheet.Cell(rowIdx, 7).Value = d.TotalErros;

                    var cellRate = worksheet.Cell(rowIdx, 8);
                    cellRate.Value = d.TaxaRejeito / 100;
                    cellRate.Style.NumberFormat.Format = "0.000%";

                    var cellStatus = worksheet.Cell(rowIdx, 9);
                    if (d.TaxaRejeito > 1.0)
                    {
                        cellRate.Style.Font.FontColor = XLColor.Red;
                        cellRate.Style.Font.Bold = true;
                        cellStatus.Value = "ALTO REJEITO";
                        cellStatus.Style.Font.FontColor = XLColor.Red;
                    }
                    else
                    {
                        cellStatus.Value = "OK";
                        cellStatus.Style.Font.FontColor = XLColor.Green;
                    }
                    rowIdx++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(excelPath);
            }
        }

        // ATUALIZE TAMBÉM A CLASSE AUXILIAR
        public class FujiDataRow
        {
            public string Maquina { get; set; }
            public string Modelo { get; set; } // <--- Adicionado
            public int Modulo { get; set; }
            public int Slot { get; set; }
            public string PartNumber { get; set; }
            public long Pickups { get; set; }
            public long Erros { get; set; }
        }
    }
}