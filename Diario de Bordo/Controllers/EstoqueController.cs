using ClosedXML.Excel;
using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using Diario_de_Bordo.Models.Estoque;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Diario_de_Bordo.Controllers
{
    public class EstoqueController : Controller
    {
        private readonly DiarioContext _context;
        // Caminho UNC definido como constante para facilitar manutenção
        private const string PASTA_FOTOS = @"\\172.20.100.20\Programs\Estoque Manutencao\Image-Items";

        // Certifique-se de que existe APENAS UM método Index no Controller
        public async Task<IActionResult> Index()
        {
            var itens = await _context.tbl_itens_estoque.ToListAsync();

            // O segredo está nesta linha: .Include(s => s.ItemEstoque)
            var pendentes = await _context.tbl_solicitacoes_pecas
                .Include(s => s.ItemEstoque) // <--- ISSO PUXA O SALDO DO ITEM
                .Where(s => s.Status == 0)
                .OrderByDescending(s => s.DataSolicitacao)
                .ToListAsync();

            var viewModel = new EstoqueViewModel
            {
                Itens = itens,
                SolicitacoesPendentes = pendentes
            };

            return View(viewModel);
        }

        public EstoqueController(DiarioContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> CadastrarItem()
        {
            // Carrega os fornecedores cadastrados para o dropdown
            var fornecedores = await _context.tbl_fornecedores.OrderBy(f => f.NomeFantasia).ToListAsync();
            ViewBag.Fornecedores = new SelectList(fornecedores, "Id", "NomeFantasia");
            return View();
        }


        // --- CADASTRO --- //
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CadastrarItem(ItemEstoque item, IFormFile FotoArquivo)
        {
            string logPath = @"C:\logs\log_estoque.txt";
            if (!Directory.Exists(@"C:\logs")) Directory.CreateDirectory(@"C:\logs");

            using (StreamWriter sw = System.IO.File.AppendText(logPath))
            {
                sw.WriteLine($"\n--- NOVO CADASTRO: {DateTime.Now} ---");
                sw.WriteLine($"EAN: {item.codigoean} | PN: {item.PartNumber}");

                if (FotoArquivo != null && FotoArquivo.Length > 0)
                {
                    try
                    {
                        string partNumberLimpo = item.PartNumber.Replace("/", "-").Replace("\\", "-");
                        if (!Directory.Exists(PASTA_FOTOS)) Directory.CreateDirectory(PASTA_FOTOS);

                        string fileName = $"{partNumberLimpo}_{Guid.NewGuid().ToString().Substring(0, 8)}{Path.GetExtension(FotoArquivo.FileName)}";
                        string fullPath = Path.Combine(PASTA_FOTOS, fileName);

                        using (var stream = new FileStream(fullPath, FileMode.Create))
                        {
                            await FotoArquivo.CopyToAsync(stream);
                        }
                        item.FotoPath = fullPath;
                    }
                    catch (Exception ex) { sw.WriteLine($"❌ ERRO FOTO: {ex.Message}"); }
                }

                ModelState.Remove("Fornecedor");
                ModelState.Remove("FotoPath");
                ModelState.Remove("FotoArquivo");

                if (ModelState.IsValid)
                {
                    _context.Add(item);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(ListagemGeral));
                }
            }
            return View(item);
        }



        // --- MÉTODO DE EXIBIÇÃO (Obrigatório para caminhos UNC) ---
        [HttpGet]
        public IActionResult VerFoto(string path)
        {
            if (string.IsNullOrEmpty(path)) return File("~/img/placeholder-item.png", "image/png");

            try
            {
                if (System.IO.File.Exists(path))
                {
                    var imageBytes = System.IO.File.ReadAllBytes(path);
                    string contentType = Path.GetExtension(path).ToLower() == ".png" ? "image/png" : "image/jpeg";
                    return File(imageBytes, contentType);
                }
            }
            catch { }

            return File("~/img/placeholder-item.png", "image/png");
        }


        // GET: /Estoque/ListagemGeral
        [HttpGet]
        public async Task<IActionResult> ListagemGeral()
        {
            var itens = await _context.tbl_itens_estoque
                .Include(i => i.Fornecedor)
                .OrderBy(i => i.PartNumber)
                .AsNoTracking()
                .ToListAsync();

            return View(itens);
        }

        // GET: /Estoque/ExportarExcel
        [HttpGet]
        public async Task<IActionResult> ExportarExcel()
        {
            var itens = await _context.tbl_itens_estoque
                .Include(i => i.Fornecedor)
                .OrderBy(i => i.PartNumber)
                .AsNoTracking()
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Inventário Geral");

            worksheet.Cell(1, 1).Value = "Código EAN/Barcode";
            worksheet.Cell(1, 2).Value = "Part Number";
            worksheet.Cell(1, 3).Value = "Descrição do Material";
            worksheet.Cell(1, 4).Value = "Tipo de Item";
            worksheet.Cell(1, 5).Value = "Equipamento (Máquina)";
            worksheet.Cell(1, 6).Value = "Fornecedor";
            worksheet.Cell(1, 7).Value = "Localização no Almoxarifado";
            worksheet.Cell(1, 8).Value = "QTD Atual";
            worksheet.Cell(1, 9).Value = "QTD Mínima";
            worksheet.Cell(1, 10).Value = "Unidade";
            worksheet.Cell(1, 11).Value = "Valor Unitário";

            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;

            var linha = 2;
            foreach (var item in itens)
            {
                worksheet.Cell(linha, 1).Value = item.codigoean;
                worksheet.Cell(linha, 2).Value = item.PartNumber;
                worksheet.Cell(linha, 3).Value = item.Descricao;
                worksheet.Cell(linha, 4).Value = item.Tipo.ToString();
                worksheet.Cell(linha, 5).Value = item.FabricanteEquipamento;
                worksheet.Cell(linha, 6).Value = item.Fornecedor?.NomeFantasia;
                worksheet.Cell(linha, 7).Value = item.Localizacao;
                worksheet.Cell(linha, 8).Value = item.QuantidadeAtual;
                worksheet.Cell(linha, 9).Value = item.EstoqueMinimo;
                worksheet.Cell(linha, 10).Value = item.Unidade.ToString();
                worksheet.Cell(linha, 11).Value = item.ValorUnitario;
                linha++;
            }

            var totalRow = worksheet.Row(linha);
            totalRow.Style.Font.Bold = true;
            worksheet.Cell(linha, 7).Value = "Total em Estoque";
            worksheet.Cell(linha, 8).Value = itens.Sum(i => i.QuantidadeAtual);

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"InventarioGeral_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }


        // GET: /Estoque/Editar/5
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var item = await _context.tbl_itens_estoque.FindAsync(id);
            if (item == null) return NotFound();

            ViewBag.Fornecedores = new SelectList(_context.tbl_fornecedores, "Id", "NomeFantasia", item.FornecedorId);
            return View(item);
        }

        // --- EDIÇÃO (Onde estava o problema) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(ItemEstoque item, IFormFile FotoArquivo)
        {
            string logPath = @"C:\logs\log_estoque_editar.txt";
            if (!Directory.Exists(@"C:\logs")) Directory.CreateDirectory(@"C:\logs");

            using (StreamWriter sw = System.IO.File.AppendText(logPath))
            {
                sw.WriteLine($"\n--- EDIÇÃO: {DateTime.Now} ---");

                ModelState.Remove("Fornecedor");
                ModelState.Remove("FotoPath");
                ModelState.Remove("FotoArquivo");

                if (ModelState.IsValid)
                {
                    try
                    {
                        var itemNoBanco = await _context.tbl_itens_estoque.AsNoTracking().FirstOrDefaultAsync(i => i.Id == item.Id);
                        if (itemNoBanco == null) return NotFound();

                        if (FotoArquivo != null && FotoArquivo.Length > 0)
                        {
                            string partNumberLimpo = item.PartNumber.Replace("/", "-").Replace("\\", "-");
                            string fileName = $"{partNumberLimpo}_{Guid.NewGuid().ToString().Substring(0, 8)}{Path.GetExtension(FotoArquivo.FileName)}";
                            string fullPath = Path.Combine(PASTA_FOTOS, fileName);

                            using (var stream = new FileStream(fullPath, FileMode.Create))
                            {
                                await FotoArquivo.CopyToAsync(stream);
                            }
                            item.FotoPath = fullPath;

                            // Opcional: Deletar a foto antiga do servidor para não acumular lixo
                            if (!string.IsNullOrEmpty(itemNoBanco.FotoPath) && System.IO.File.Exists(itemNoBanco.FotoPath))
                            {
                                System.IO.File.Delete(itemNoBanco.FotoPath);
                            }
                        }
                        else
                        {
                            item.FotoPath = itemNoBanco.FotoPath;
                        }

                        _context.Update(item);
                        await _context.SaveChangesAsync();
                        return RedirectToAction(nameof(ListagemGeral));
                    }
                    catch (Exception ex) { sw.WriteLine($"🔥 ERRO: {ex.Message}"); }
                }
            }
            ViewBag.Fornecedores = new SelectList(_context.tbl_fornecedores, "Id", "NomeFantasia", item.FornecedorId);
            return View(item);
        }

        // POST: /Estoque/Excluir/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Excluir(int id)
        {
            // Iniciamos uma transação para garantir que ou apaga tudo ou não apaga nada
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Localiza o item principal
                    var item = await _context.tbl_itens_estoque.FindAsync(id);
                    if (item == null) return NotFound();

                    // 2. Apaga os registros na tabela estrangeira (Solicitações/Histórico)
                    // Aqui buscamos todos os registros que possuem o ID deste item
                    var registrosVinculados = _context.tbl_solicitacoes_pecas
                        .Where(s => s.ItemEstoqueId == id);

                    if (await registrosVinculados.AnyAsync())
                    {
                        _context.tbl_solicitacoes_pecas.RemoveRange(registrosVinculados);
                    }

                    // 3. Agora que os filhos foram removidos, podemos apagar o pai
                    _context.tbl_itens_estoque.Remove(item);

                    // 4. Persiste as mudanças no banco
                    await _context.SaveChangesAsync();

                    // 5. Confirma a transação no Postgres
                    await transaction.CommitAsync();

                    return RedirectToAction(nameof(ListagemGeral));
                }
                catch (Exception ex)
                {
                    // Se der qualquer erro (ex: outra tabela que não sabíamos), desfaz o delete
                    await transaction.RollbackAsync();
                    TempData["Erro"] = "Erro ao limpar registros vinculados: " + ex.Message;
                    return RedirectToAction(nameof(ListagemGeral));
                }
            }
        }



        // GET: /Estoque/Requisicoes
        [HttpGet]
        public async Task<IActionResult> Requisicoes()
        {
            // 1. Buscamos as solicitações com Status 0 (Pendentes)
            // O .Include(s => s.ItemEstoque) é o que evita o erro de "ItemEstoque null" na View
            var listaPendentes = await _context.tbl_solicitacoes_pecas
                .Include(s => s.ItemEstoque)
                .Where(s => s.Status == 0)
                .OrderByDescending(s => s.DataSolicitacao)
                .ToListAsync();

            // 2. Buscamos as últimas 20 concluídas para o histórico
            var listaHistorico = await _context.tbl_solicitacoes_pecas
                .Include(s => s.ItemEstoque)
                .Where(s => s.Status != 0)
                .OrderByDescending(s => s.DataSolicitacao)
                .Take(20)
                .ToListAsync();

            // 3. Alimentamos o ViewModel
            var viewModel = new Diario_de_Bordo.Models.RequisicaoViewModel
            {
                Pendentes = listaPendentes,
                Historico = listaHistorico
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarSolicitacao(int id)
        {
            try
            {
                var solicitacao = await _context.tbl_solicitacoes_pecas.FindAsync(id);

                if (solicitacao == null)
                    return Json(new { success = false, message = "Não encontrada." });

                if (solicitacao.Status != 0)
                    return Json(new { success = false, message = "Já processada." });

                // Altera apenas o Status
                solicitacao.Status = 2;

                // IMPORTANTE: Se houver campos de data, force-os para UTC ou garanta que não mudem
                // Se a sua model tiver um campo de 'DataProcessamento' ou similar:
                // solicitacao.DataConclusao = DateTime.UtcNow;

                await _context.SaveChangesAsync(); // O FindAsync já "rastreia" o objeto, não precisa de Entry().State

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // O log vai te mostrar exatamente qual campo causou o conflito
                return Json(new { success = false, message = "Erro Postgres: " + ex.Message });
            }
        }



        [HttpGet]
        public async Task<IActionResult> CriarRequisicao()
        {
            // Carrega os itens do estoque para o técnico poder selecionar
            var itens = await _context.tbl_itens_estoque.OrderBy(i => i.PartNumber).ToListAsync();
            return View(itens);
        }

        [HttpPost]
        public async Task<IActionResult> BaixarSolicitacao(int solicitacaoId)
        {
            // Usamos uma transação para garantir que a baixa e o status ocorram juntos
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // 1. Busca a solicitação
                    var solicitacao = await _context.tbl_solicitacoes_pecas
                        .FirstOrDefaultAsync(s => s.Id == solicitacaoId);

                    if (solicitacao == null)
                        return Json(new { success = false, message = "Solicitação não encontrada." });

                    if (solicitacao.Status != 0)
                        return Json(new { success = false, message = "Esta solicitação já foi processada anteriormente." });

                    // 2. Busca o item no estoque usando a Model que você enviou
                    var itemEstoque = await _context.tbl_itens_estoque
                        .FirstOrDefaultAsync(i => i.Id == solicitacao.ItemEstoqueId);

                    if (itemEstoque == null)
                        return Json(new { success = false, message = "Item não localizado no estoque mestre." });

                    // 3. Validação de Saldo
                    // Como QuantidadeAtual é decimal e Quantidade da solicitação é int, o C# converte automaticamente
                    if (itemEstoque.QuantidadeAtual < solicitacao.Quantidade)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Saldo insuficiente. Estoque atual: {itemEstoque.QuantidadeAtual}, Solicitado: {solicitacao.Quantidade}"
                        });
                    }

                    // 4. DESCONTO NO ESTOQUE (Nome da propriedade da sua Model: QuantidadeAtual)
                    itemEstoque.QuantidadeAtual -= solicitacao.Quantidade;

                    // 5. ATUALIZA STATUS DA SOLICITAÇÃO (0 = Pendente, 1 = Pago/Baixado)
                    solicitacao.Status = 1;

                    // Salva as alterações no banco (Npgsql cuidará do timestamp without time zone se houver)
                    await _context.SaveChangesAsync();

                    // Confirma a transação
                    await transaction.CommitAsync();

                    return Json(new { success = true, message = "Baixa de estoque realizada com sucesso!" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Erro ao processar baixa: " + ex.Message });
                }
            }
        }

    }


}