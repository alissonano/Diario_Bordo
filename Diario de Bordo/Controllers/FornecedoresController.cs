using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models.Estoque;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Diario_de_Bordo.Controllers
{
    public class FornecedoresController : Controller
    {
        private readonly DiarioContext _context;

        public FornecedoresController(DiarioContext context)
        {
            _context = context;
        }
        // GET: /Fornecedores/Listagem
        public async Task<IActionResult> Index()
        {
            var fornecedores = await _context.tbl_fornecedores
                .OrderBy(f => f.NomeFantasia)
                .AsNoTracking()
                .ToListAsync();

            return View(fornecedores);
        }

        // GET: /Fornecedores/Editar/5
        public async Task<IActionResult> Editar(int id)
        {
            var fornecedor = await _context.tbl_fornecedores.FindAsync(id);
            if (fornecedor == null) return NotFound();

            return View(fornecedor);
        }

        // POST: /Fornecedores/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int id, Fornecedor fornecedor)
        {
            if (id != fornecedor.Id) return NotFound();

            string logFile = @"C:\logs\log_fornecedores.txt";
            void EscreverLog(string msg) => System.IO.File.AppendAllText(logFile, $"[{DateTime.Now:dd/MM HH:mm}] - {msg}{Environment.NewLine}");

            // Remove validação de Itens (relacionamento) para evitar erro no ModelState
            ModelState.Remove("Itens");

            if (ModelState.IsValid)
            {
                try
                {
                    // O EF vai pegar as strings concatenadas (Nome1 | Nome2) 
                    // vindas da View e atualizar no banco.
                    _context.Update(fornecedor);
                    await _context.SaveChangesAsync();

                    EscreverLog($"SUCESSO EDIÇÃO: ID {fornecedor.Id} | Empresa: {fornecedor.NomeFantasia} | Contatos Atualizados: {fornecedor.Contato}");

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    EscreverLog($"ERRO EDIÇÃO ID {id}: {ex.Message} | Inner: {ex.InnerException?.Message}");
                    ModelState.AddModelError("", "Erro ao atualizar no banco de dados. Verifique o log.");
                }
            }
            else
            {
                // Log de erro de validação se houver
                var erros = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                EscreverLog($"FALHA VALIDAÇÃO EDIÇÃO: {erros}");
            }

            return View(fornecedor);
        }

        // POST: /Fornecedores/Excluir/5
        [HttpPost]
        public async Task<IActionResult> Excluir(int id)
        {
            var fornecedor = await _context.tbl_fornecedores.FindAsync(id);
            if (fornecedor != null)
            {
                _context.tbl_fornecedores.Remove(fornecedor);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Cadastrar()
        {
            // RETORNE UMA INSTÂNCIA NOVA PARA A VIEW NÃO FICAR NULA
            return View(new Fornecedor());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cadastrar(Fornecedor fornecedor)
        {
            // Configuração de Log para monitoramento
            string logPath = @"C:\logs";
            string logFile = Path.Combine(logPath, "log_fornecedores.txt");

            void EscreverLog(string mensagem)
            {
                try
                {
                    if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
                    System.IO.File.AppendAllText(logFile, $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] - {mensagem}{Environment.NewLine}");
                }
                catch { }
            }

            // Removemos a validação da lista de navegação
            ModelState.Remove("Itens");

            EscreverLog("=== NOVO CADASTRO (MÚLTIPLOS CONTATOS) ===");
            EscreverLog($"Fantasia: {fornecedor.NomeFantasia} | Contatos Serializados: {fornecedor.Contato}");

            if (ModelState.IsValid)
            {
                try
                {
                    // O CNPJ já vem com máscara da View, mantemos conforme seu desejo
                    // O EF salvará as strings de Contato, Email e Telefone com o separador "|"

                    _context.tbl_fornecedores.Add(fornecedor);
                    await _context.SaveChangesAsync();

                    EscreverLog($"SUCESSO: Fornecedor {fornecedor.NomeFantasia} cadastrado com ID {fornecedor.Id}.");
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    EscreverLog($"ERRO DE BANCO: {ex.Message}");
                    if (ex.InnerException != null) EscreverLog($"DETALHE POSTGRES: {ex.InnerException.Message}");

                    ModelState.AddModelError("", "Erro técnico ao salvar. Verifique se as colunas no banco suportam o tamanho dos dados.");
                }
            }
            else
            {
                // Se cair aqui, logamos quais campos falharam na validação
                var erros = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                EscreverLog($"FALHA DE VALIDAÇÃO: {erros}");
            }

            return View(fornecedor);
        }
    }
}