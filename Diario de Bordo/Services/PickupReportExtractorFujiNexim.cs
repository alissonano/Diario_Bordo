using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using static Diario_de_Bordo.Services.PickupReportExtractorFuji;

public class PickupReportExtractorFujiNexim
{
    public class NeximPickupResult
    {
        public string NomeArquivo { get; set; }
        public string Linha { get; set; }
        public DateTime DataRelatorio { get; set; }
        public double TotalPickup { get; set; }
        public double VisionErrors { get; set; }
        public double PickupErrors { get; set; }
        public double RejeicaoTotal => VisionErrors + PickupErrors;
        public double Efficiency => TotalPickup > 0 ? 100 - ((RejeicaoTotal / TotalPickup) * 100) : 100;
    }



    public async Task<bool> ExtrairArquivoSelecionadoNeximAsync(string nomeArquivoCompleto)
    {
        string pathFujiNexim = @"\\172.20.100.4\ReportsNEXIM\Auto";
        string arquivoFonteCompleto = Path.Combine(pathFujiNexim, nomeArquivoCompleto);

        string servidorDestino = @"\\192.168.1.8\publica";
        string pastaBaseRede = @"\\192.168.1.8\publica\Sistema SMD\Relatorio Rejeito SMT\SMD1-8";

        var credFuji = new NetworkCredential("NEXIM-SS1", "NEXIM-SS1");
        var credPublica = new NetworkCredential("sistema.smd", "Gbr@2025", "ad.gbrcomponentes.com.br");

        try
        {
            using (new NetworkShareConnection(pathFujiNexim, credFuji))
            using (new NetworkShareConnection(servidorDestino, credPublica))
            {
                if (!File.Exists(arquivoFonteCompleto)) return false;

                string linha = nomeArquivoCompleto.Contains("_SMD1") ? "SMD1" :
                               nomeArquivoCompleto.Contains("_SMD8") ? "SMD8" : "Outra";

                string nomeFinalExcel = $"Performance_{linha}_{nomeArquivoCompleto}";
                string pathDestinoFinal = Path.Combine(pastaBaseRede, nomeFinalExcel);

                if (!Directory.Exists(pastaBaseRede)) Directory.CreateDirectory(pastaBaseRede);

                // Chama a versão robusta do processador
                return GerarExcelFiltradoNeximRobust(arquivoFonteCompleto, pathDestinoFinal, linha);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NEXIM SERVICE ERROR]: {ex.Message}");
            return false;
        }
    }

    private bool GerarExcelFiltradoNeximRobust(string neximExcelPath, string excelPathDestino, string nomeLinha)
    {
        try
        {
            var listaBruta = new List<dynamic>();
            string dataInicio = "N/A";
            string dataFim = "N/A";

            // 1. EXTRAÇÃO DAS DATAS PELO NOME DO ARQUIVO
            // Exemplo: ...ActionReport(20260415000000-20260415235959)...
            try
            {
                string nomeArquivo = Path.GetFileName(neximExcelPath);
                int inicioParentese = nomeArquivo.IndexOf('(');
                int fimParentese = nomeArquivo.IndexOf(')');

                if (inicioParentese != -1 && fimParentese != -1)
                {
                    string miolo = nomeArquivo.Substring(inicioParentese + 1, fimParentese - inicioParentese - 1);
                    string[] partes = miolo.Split('-'); // Separa as duas datas

                    if (partes.Length == 2)
                    {
                        // Formata de YYYYMMDDHHmmss para DD/MM/YYYY HH:mm
                        dataInicio = $"{partes[0].Substring(6, 2)}/{partes[0].Substring(4, 2)}/{partes[0].Substring(0, 4)} {partes[0].Substring(8, 2)}:{partes[0].Substring(10, 2)}";
                        dataFim = $"{partes[1].Substring(6, 2)}/{partes[1].Substring(4, 2)}/{partes[1].Substring(0, 4)} {partes[1].Substring(8, 2)}:{partes[1].Substring(10, 2)}";
                    }
                }
            }
            catch { /* Mantém N/A se falhar a conversão do nome */ }

            using (var workbookFonte = new XLWorkbook(neximExcelPath))
            {
                var ws = workbookFonte.Worksheet("Feeder Usage Tracking");

                // Captura período das células M2 e M3 (conforme seu arquivo real)
                dataInicio = ws.Cell("I2").GetValue<string>();
                dataFim = ws.Cell("I3").GetValue<string>();

                // Dados começam na linha 6
                var rows = ws.RowsUsed(r => r.RowNumber() >= 6);

                foreach (var row in rows)
                {
                    string partNumber = row.Cell("J").GetValue<string>().Trim();
                    if (string.IsNullOrEmpty(partNumber) || partNumber == "Part Number") continue;

                    // --- TRATAMENTO PARA SMD1 (Números Diretos) E SMD8 (Strings como 2-R) ---
                    string modRaw = row.Cell("C").GetValue<string>();
                    string slotRaw = row.Cell("F").GetValue<string>();

                    // Extrai apenas dígitos. Transforma "2-R" em 2, "11" em 11.
                    int modNum = int.TryParse(new string(modRaw.Where(char.IsDigit).ToArray()), out int m) ? m : 0;
                    int slotNum = int.TryParse(new string(slotRaw.Where(char.IsDigit).ToArray()), out int s) ? s : 0;

                    listaBruta.Add(new
                    {
                        Modelo = row.Cell("I").GetValue<string>().Trim(),   // Coluna I: Job
                        Maquina = row.Cell("B").GetValue<string>().Trim(),  // Coluna B: Machine Name
                        Modulo = modNum,                                    // Agora é INT garantido
                        Slot = slotNum,                                      // Agora é INT garantido
                        PartNumber = partNumber,                            // Coluna J: Part Number
                        Pickups = double.TryParse(row.Cell("K").Value.ToString(), out double p) ? (long)p : 0,
                        Erros = double.TryParse(row.Cell("T").Value.ToString(), out double e) ? (long)e : 0
                    });
                }
            }

            // CONSOLIDAÇÃO: Agrupar por Modelo e Part Number (Ignorando Slot/FIDL na chave)
            var dadosConsolidados = listaBruta
                .GroupBy(x => new { x.Modelo, x.PartNumber })
                .Select(g => new {
                    Modelo = g.Key.Modelo,
                    // Como você quer consolidar o mesmo Part Number, pegamos a máquina e módulo da primeira ocorrência
                    Maquina = g.First().Maquina,
                    Modulo = g.First().Modulo,
                    Slot = g.First().Slot, // Como solicitado, indica que unificou diferentes FIDLs
                    PartNumber = g.Key.PartNumber,
                    TotalPickups = g.Sum(s => (long)s.Pickups),
                    TotalErros = g.Sum(s => (long)s.Erros),
                    TaxaRejeito = g.Sum(s => (long)s.Pickups) > 0
                                  ? ((double)g.Sum(s => (long)s.Erros) / g.Sum(s => (long)s.Pickups)) * 100
                                  : 0
                })
                // ORDENAÇÃO: Programa > Máquina > Módulo
                .OrderBy(o => o.Modelo)
                .ThenBy(o => o.Maquina)
                .ThenBy(o => o.Modulo)
                .ThenBy(o => o.Slot)
                .ToList();

            if (!dadosConsolidados.Any()) return false;

            // GERAÇÃO DO EXCEL FINAL
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Performance");

                // Cabeçalho estilizado
                var rangeHeader = worksheet.Range(1, 1, 1, 9);
                rangeHeader.Merge().Value = $"PERFORMANCE FUJI - {nomeLinha} | PERÍODO: {dataInicio} ~ {dataFim}";
                rangeHeader.Style.Font.Bold = true;
                rangeHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
                rangeHeader.Style.Font.FontColor = XLColor.White;
                rangeHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                string[] headers = { "Modelo", "Máquina", "Módulo", "Slot", "Part Number", "Pickups", "Erros", "Rejeito %", "Status" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(3, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int rowIdx = 4;
                foreach (var d in dadosConsolidados)
                {
                    worksheet.Cell(rowIdx, 1).Value = d.Modelo;
                    worksheet.Cell(rowIdx, 2).Value = d.Maquina;
                    worksheet.Cell(rowIdx, 3).Value = d.Modulo;
                    worksheet.Cell(rowIdx, 4).Value = d.Slot;
                    worksheet.Cell(rowIdx, 5).Value = d.PartNumber;
                    worksheet.Cell(rowIdx, 6).Value = d.TotalPickups;
                    worksheet.Cell(rowIdx, 7).Value = d.TotalErros;

                    var cellRate = worksheet.Cell(rowIdx, 8);
                    cellRate.Value = d.TaxaRejeito / 100;
                    cellRate.Style.NumberFormat.Format = "0.000%";

                    var cellStatus = worksheet.Cell(rowIdx, 9);
                    cellStatus.Value = d.TaxaRejeito > 1.0 ? "ALTO REJEITO" : "OK";
                    cellStatus.Style.Font.FontColor = d.TaxaRejeito > 1.0 ? XLColor.Red : XLColor.Green;

                    rowIdx++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(excelPathDestino);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no processamento Nexim: {ex.Message}");
            return false;
        }
    }
}