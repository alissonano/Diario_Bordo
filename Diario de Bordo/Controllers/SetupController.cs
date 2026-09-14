using Diario_de_Bordo.Models;
using Diario_de_Bordo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;         
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Diario_de_Bordo.Data; // Certifique-se de ter o namespace correto para o seu DbContext    



namespace Diario_de_Bordo.Controllers
{
    public class SetupController : Controller
    {
        private readonly SetupExtractorService _extractor;
        private readonly ILogger<SetupController> _logger;
        private readonly DiarioContext _context;

        public SetupController(SetupExtractorService extractor, ILogger<SetupController> logger, DiarioContext context)
        {
            _extractor = extractor;
            _logger = logger;
            _context = context;
        }
        public IActionResult Index() => View();

        public IActionResult FolhasAlimentacao() => View();

        public IActionResult FeederCheck()
        {
            // Aqui você buscaria os dados atuais do setup para o operador conferir
            return View();
        }
        // Action para abrir a View de Operação
        public async Task<IActionResult> PDAAssembleon(string linha)
        {
            // Busca o nome do programa mais recente para esta linha
            var programaAtivo = await _context.tbl_feedercheck
                .Where(x => x.Linha == linha)
                .OrderByDescending(x => x.DataCriacao)
                .Select(x => x.Programa)
                .FirstOrDefaultAsync();

            // Passamos para a View
            ViewBag.Linha = linha;
            ViewBag.Programa = programaAtivo ?? "Nenhum programa ativo encontrado";

            return View();
        }


        [HttpPost]
        public IActionResult UploadArquivoUnico(IFormFile file, string maquinaTipo)
        {
            if (file == null || file.Length == 0) return BadRequest("Arquivo inválido");

            try

            {
                _logger.LogInformation("Iniciando importação do arquivo {FileName} na máquina {Maquina}", file.FileName, maquinaTipo);
                var dados = _extractor.ProcessarArquivo(file, maquinaTipo);
                return Json(new { total = dados.Count, dados = dados });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar o arquivo {FileName} na máquina {Maquina}", file.FileName, maquinaTipo);
                _logger.LogError(ex, "Erro crítico ao processar o setup de feeders para {Maquina}", maquinaTipo);
                return BadRequest(ex.Message);
            }
        }


        [HttpPost]
        public async Task<IActionResult> ConfirmarImportacao([FromBody] ImportSetupRequest request)
        {
            // Verificação de segurança: Se o JSON vier mal formatado, o request será null
            if (request == null || request.Dados == null)
                return BadRequest("Dados inválidos ou vazios.");

            try
            {
                await _extractor.SalvarSetup(request.Linha, request.Programa, request.Dados);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest("Erro ao salvar no banco: " + ex.Message);
            }
        }

        // 1. Listar Setups existentes (agrupados por Linha e Programa)
        [HttpGet]
        public async Task<IActionResult> ListarSetups()
        {
            var setups = await _context.tbl_feedercheck
                .GroupBy(x => new { x.Linha, x.Programa })
                .Select(g => new { g.Key.Linha, g.Key.Programa, DataCriacao = g.Max(x => x.DataCriacao) })
                .OrderByDescending(x => x.DataCriacao)
                .ToListAsync();

            return Json(setups);
        }

        // 2. Excluir um Setup completo
        [HttpPost]
        public async Task<IActionResult> ExcluirSetup(string linha, string programa)
        {
            try
            {
                var registros = _context.tbl_feedercheck
                    .Where(x => x.Linha == linha && x.Programa == programa);

                _context.tbl_feedercheck.RemoveRange(registros);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest("Erro ao excluir: " + ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDetalhesSetup(string linha, string programa)
        {
            var detalhes = await _context.tbl_feedercheck
                .Where(x => x.Linha == linha && x.Programa == programa)
                .OrderBy(x => x.Maquina)
                .ThenBy(x => x.Slot)
                .ThenBy(x => x.Partnumber)
                .ToListAsync();

            return Json(detalhes);
        }

        [HttpGet]
        public async Task<IActionResult> GetLinhasDisponiveis()
        {
            // Busca as linhas que possuem registros na tabela de setup
            var linhas = await _context.tbl_feedercheck
                .Select(x => x.Linha)
                .Distinct()
                .ToListAsync();

            return Json(linhas);
        }

        public async Task<IActionResult> ValidarFeeder([FromBody] ValidacaoDto dto)
        {
            // Pega o nome do usuário logado (ex: "DOMINIO\usuario" ou o e-mail)
            // Se o User estiver nulo, usamos um valor padrão ou tratamos o erro
            string usuarioLogado = User.Identity?.Name ?? "Desconhecido";

            // 1. Busca o que deveria estar naquele slot no setup ativo
            var setupCorreto = await _context.tbl_feedercheck
                .FirstOrDefaultAsync(x => x.Linha == dto.Linha
                                       && x.Slot == dto.Slot); // Filtramos por Linha e Slot

            bool isValid = (setupCorreto != null && setupCorreto.Partnumber == dto.Partnumber);

            // 2. Grava o log com a identidade do usuário
            var log = new FeederValidation
            {
                Linha = dto.Linha,
                Programa = dto.Programa,
                Maquina = dto.Maquina,
                Slot = dto.Slot,
                CodigoUnico = dto.CodigoUnico,
                Partnumber = dto.Partnumber,
                StatusCheck = isValid ? "VALIDO" : "INVALIDO",
                UsuarioId = usuarioLogado, // <--- O usuário está sendo capturado aqui!
                DataLeitura = DateTime.Now
            };

            _context.tbl_feeder_validation.Add(log);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = isValid,
                message = isValid ? "Componente Correto!" : "ERRO: Partnumber divergente!"
            });
        }

        public async Task<IActionResult> GerarEtiquetas()
        {
            // Busca apenas nomes de máquinas únicos da tabela
            var maquinas = await _context.tbl_feedercheck
                .Select(x => x.Maquina)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return View(maquinas);
        }

        public IActionResult PDAAudit(string linha, string programa)
        {
            ViewBag.Linha = linha;
            ViewBag.Programa = programa;
            return View();
        }

    }
}