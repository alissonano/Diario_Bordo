using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Spreadsheet;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkiaSharp;
using System.Net;
using A = DocumentFormat.OpenXml.Drawing;
using Drawing = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
namespace Diario_de_Bordo.Controllers
{
    public class CQController : Controller
    {
        private readonly DiarioContext _context;
        private readonly string baseEvidenciasPath = @"\\172.20.100.20\Programs\Inspecoes_CQ\Evidencias";

        public CQController(DiarioContext context)
        {
            _context = context;
        }

        // ========================= INDEX =========================
        public async Task<IActionResult> Index()
        {
            ViewBag.Usuario = HttpContext.Session.GetString("UsuarioLogado") ?? "Desconhecido";
            var inspecoes = await _context.tbl_cq
                .OrderByDescending(c => c.Date_field)
                .ToListAsync();

            return View(inspecoes);
        }

        // ========================= GET CREATE =========================
        [HttpGet]
        public async Task<IActionResult> Create(int id)
        {
            var inspecao = await _context.tbl_cq.FirstOrDefaultAsync(c => c.Id == id);
            if (inspecao == null)
                return NotFound();

            bool isCQAdmin = HttpContext.Session.GetString("IsCQAdmin") == "true";

            // Carrega evidências existentes dessa inspeção
            var evidenciasExistentes = await _context.tbl_cq_evidencia
                .Where(e => e.cq_id == id)
                .ToListAsync();

            // Monta checklist com resultados
            var postos = GetPostosChecklist();

            foreach (var posto in postos)
            {
                foreach (var item in posto.Itens)
                {
                    // Tenta achar uma evidência existente para este item
                    var ev = evidenciasExistentes
                        .Where(e => e.posto == posto.Posto && e.item == item.Nome)
                        .OrderByDescending(e => e.id)
                        .FirstOrDefault();

                    // Se achou, aplica o resultado; se não, marca como N/A
                    item.Resultado = ev?.resultado ?? "N/A";
                }
            }

            // ===== Seleciona o último status corretamente =====
            var statusAtual = string.IsNullOrEmpty(inspecao.Status) ? "Aguardando Confirmação" : inspecao.Status;

            // Prepara ViewBag para dropdown
            ViewBag.StatusGeral = new SelectList(
                new[] { "Aguardando Confirmação", "Aprovado", "Reprovado" },
                statusAtual // seleciona automaticamente o último status
            );

            var model = new CQViewModel
            {
                Inspecao = inspecao,
                Postos = postos,
                IsCQAdmin = isCQAdmin
            };

            return View(model);
        }




    // ========================= POST NOVA INSPEÇÃO =========================
    [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NovaInspecao(CQViewModel model)
        {
            bool isCQAdmin = HttpContext.Session.GetString("IsCQAdmin") == "true";
            Console.WriteLine($"IsCQAdmin: {isCQAdmin}");

            if (model?.Inspecao == null)
                return Json(new { sucesso = false, mensagem = "Dados da inspeção não recebidos." });

            try
            {
                model.Inspecao.Status = "Aguardando Confirmação";
                model.Inspecao.ComentarioGeral ??= string.Empty;
                model.Inspecao.Responsavel = HttpContext.Session.GetString("UsuarioLogado") ?? "Desconhecido";
                model.Inspecao.Date_field = DateTime.UtcNow;

                _context.tbl_cq.Add(model.Inspecao);
                await _context.SaveChangesAsync();

                var redirectUrl = Url.Action("Create", "CQ", new { id = model.Inspecao.Id });
                return Json(new { sucesso = true, redirectUrl });
            }
            catch (Exception ex)
            {
                var mensagemErro = ex.InnerException?.Message ?? ex.Message;
                return Json(new { sucesso = false, mensagem = $"Erro ao salvar inspeção: {mensagemErro}" });
            }
        }


        // ========================= EVIDÊNCIAS =========================
        public async Task<IActionResult> Evidencias(int id)
        {
            bool isCQAdmin = HttpContext.Session.GetString("IsCQAdmin") == "true";
            Console.WriteLine($"IsCQAdmin: {isCQAdmin}");

            var inspecao = await _context.tbl_cq.FirstOrDefaultAsync(c => c.Id == id);
            if (inspecao == null) return NotFound();

            bool isAdmin = HttpContext.Session.GetString("IsCQAdmin") == "true";
            ViewBag.Usuario = HttpContext.Session.GetString("Usuario") ?? "Desconhecido";

            var postosChecklist = GetPostosChecklist();
            var evidencias = await _context.tbl_cq_evidencia
                .Where(e => e.cq_id == id)
                .ToListAsync();

            var vm = new CQEvidenciasViewModel
            {
                CQId = inspecao.Id,
                Cliente = inspecao.Cliente,
                Modelo = inspecao.Modelo,
                OP = inspecao.OP,
                Fase = inspecao.Fase,
                Turno = inspecao.Turno,
                Status = inspecao.Status ?? "N/A",
                isCQAdmin = isAdmin,
                ComentarioGeral = inspecao.ComentarioGeral ?? "",
                Postos = postosChecklist.Select(posto => new PostoChecklistViewModel
                {
                    Posto = posto.Posto,
                    Itens = posto.Itens.Select(item =>
                    {
                        var evidenciasDoItem = evidencias
                            .Where(e => e.posto == posto.Posto && e.item == item.Nome)
                            .Select(e => new EvidenciaViewModel
                            {
                                Id = e.id,
                                Caminho = e.evidencia,
                                Comentarios = string.IsNullOrEmpty(e.comentario)
                                    ? new List<string>()
                                    : e.comentario.Split('\n').ToList()
                            }).ToList();

                        return new ChecklistItemViewModel
                        {
                            Nome = item.Nome,
                            Resultado = evidencias
                                        .Where(e => e.posto == posto.Posto && e.item == item.Nome)
                                        .Select(e => e.resultado)
                                        .FirstOrDefault() ?? "N/A",
                            Comentario = evidencias
                                        .Where(e => e.posto == posto.Posto && e.item == item.Nome)
                                        .Select(e => e.comentario)
                                        .FirstOrDefault(),
                            Evidencias = evidenciasDoItem,
                            NovasEvidencias = new List<IFormFile>()
                        };
                    }).ToList()
                }).ToList()
            };

            return View(vm);
        }
        // ========================= UPLOAD EVIDÊNCIAS INTERNO =========================
        private async Task UploadEvidenciaInterna(CQ inspecao, string posto, ChecklistItemViewModel item)
        {
            string pastaFinal = Path.Combine(baseEvidenciasPath, inspecao.Cliente, inspecao.Modelo, inspecao.OP, $"{inspecao.Id}_{posto}");
            if (!Directory.Exists(pastaFinal))
                Directory.CreateDirectory(pastaFinal);

            var usuario = HttpContext.User.Identity?.Name ?? "Desconhecido";

            if (item.NovasEvidencias != null && item.NovasEvidencias.Any())
            {
                foreach (var file in item.NovasEvidencias)
                {
                    if (file.Length > 0)
                    {
                        string nomeArquivo = $"{DateTime.Now:yyyyMMdd_HHmmss}_{SanitizeFileName(file.FileName)}";
                        string caminhoCompleto = Path.Combine(pastaFinal, nomeArquivo);

                        using var stream = new FileStream(caminhoCompleto, FileMode.Create);
                        await file.CopyToAsync(stream);

                        _context.tbl_cq_evidencia.Add(new CQEvidencia
                        {
                            cq_id = inspecao.Id,
                            posto = posto,
                            item = item.Nome,
                            evidencia = caminhoCompleto,
                            comentario = ""
                        });
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(item.NovoComentario))
            {
                string comentarioNovo = $"{usuario} ({DateTime.Now:dd/MM/yyyy HH:mm}): {item.NovoComentario}";

                var evidenciasDoItem = _context.tbl_cq_evidencia
                    .Where(e => e.cq_id == inspecao.Id && e.posto == posto && e.item == item.Nome)
                    .ToList();

                if (!evidenciasDoItem.Any())
                {
                    _context.tbl_cq_evidencia.Add(new CQEvidencia
                    {
                        cq_id = inspecao.Id,
                        posto = posto,
                        item = item.Nome,
                        comentario = comentarioNovo
                    });
                }
                else
                {
                    foreach (var ev in evidenciasDoItem)
                    {
                        ev.comentario = string.IsNullOrEmpty(ev.comentario)
                            ? comentarioNovo
                            : ev.comentario + "\n" + comentarioNovo;
                        _context.tbl_cq_evidencia.Update(ev);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }
        [HttpPost]
        public async Task<JsonResult> UploadEvidencias(
            int cqId,
            string posto,
            string itemNome,
            string comentario,
            string resultado,
            List<IFormFile> arquivos,
            List<int> imagensParaExcluir)
        {
            var inspecao = await _context.tbl_cq.FirstOrDefaultAsync(c => c.Id == cqId);
            if (inspecao == null)
                return Json(new { sucesso = false, mensagem = "Inspeção não encontrada." });

            // ===================== EXCLUSÃO DE EVIDÊNCIAS =====================
            if (imagensParaExcluir != null && imagensParaExcluir.Any())
            {
                foreach (var id in imagensParaExcluir)
                {
                    var ev = await _context.tbl_cq_evidencia.FirstOrDefaultAsync(e => e.id == id);
                    if (ev != null)
                    {
                        if (System.IO.File.Exists(ev.evidencia))
                            System.IO.File.Delete(ev.evidencia);
                        _context.tbl_cq_evidencia.Remove(ev);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // ===================== CRIAÇÃO DE PASTA FINAL =====================
            var usuario = HttpContext.User.Identity?.Name ?? "Desconhecido";
            string pastaFinal = Path.Combine(baseEvidenciasPath, inspecao.Cliente, inspecao.Modelo, inspecao.OP, $"{inspecao.Id}_{posto}");
            if (!Directory.Exists(pastaFinal))
                Directory.CreateDirectory(pastaFinal);

            // 1. Atualiza todas as evidências existentes com o novo resultado
            var evidenciasExistentes = _context.tbl_cq_evidencia
                .Where(e => e.cq_id == cqId && e.posto == posto && e.item == itemNome)
                .ToList();

            foreach (var ev in evidenciasExistentes)
            {
                ev.resultado = string.IsNullOrEmpty(resultado) ? "N/A" : resultado;
            }

            // ===================== SALVAR NOVAS EVIDÊNCIAS =====================
            if (!string.IsNullOrWhiteSpace(comentario) || (arquivos != null && arquivos.Any()))
            {
                foreach (var file in arquivos ?? Enumerable.Empty<IFormFile>())
                {
                    if (file.Length > 0)
                    {
                        string nomeArquivo = $"{DateTime.Now:yyyyMMdd_HHmmss}_{SanitizeFileName(file.FileName)}";
                        string caminhoCompleto = Path.Combine(pastaFinal, nomeArquivo);

                        using var stream = new FileStream(caminhoCompleto, FileMode.Create);
                        await file.CopyToAsync(stream);

                        _context.tbl_cq_evidencia.Add(new CQEvidencia
                        {
                            cq_id = inspecao.Id,
                            posto = posto,
                            item = itemNome,
                            resultado = string.IsNullOrEmpty(resultado) ? "N/A" : resultado,
                            evidencia = caminhoCompleto,
                            comentario = !string.IsNullOrWhiteSpace(comentario)
                                ? $"{usuario} ({DateTime.UtcNow:dd/MM/yyyy HH:mm}): {comentario}"
                                : ""
                        });
                    }
                }

                // Se não houver arquivos, mas houver comentário, cria uma linha só para comentário
                if ((arquivos == null || !arquivos.Any()) && !string.IsNullOrWhiteSpace(comentario))
                {
                    _context.tbl_cq_evidencia.Add(new CQEvidencia
                    {
                        cq_id = inspecao.Id,
                        posto = posto,
                        item = itemNome,
                        resultado = string.IsNullOrEmpty(resultado) ? "N/A" : resultado,
                        evidencia = "",
                        comentario = $"{usuario} ({DateTime.UtcNow:dd/MM/yyyy HH:mm}): {comentario}"
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { sucesso = true });
        }


        public async Task<IActionResult> MostrarEvidencia(int id)
        {
            var evidencia = await _context.tbl_cq_evidencia.FirstOrDefaultAsync(e => e.id == id);
            if (evidencia == null || string.IsNullOrEmpty(evidencia.evidencia))
                return NotFound();

            // Ler arquivo em byte[]
            var fileBytes = await System.IO.File.ReadAllBytesAsync(evidencia.evidencia);
            var contentType = "image/jpeg"; // ou "image/png" se preferir, pode deduzir do Path.GetExtension
            return File(fileBytes, contentType);
        }



        // ========================= SALVAR COMENTÁRIO NA EVIDÊNCIA =========================
        [HttpPost]
        public async Task<IActionResult> AdicionarComentario(int evidenciaId, string comentario)
        {
            try
            {
                // ✅ Captura o usuário logado da sessão (definido quando ele faz login)
                var usuario = HttpContext.Session.GetString("UsuarioLogado") ?? "Desconhecido";

                // ✅ Busca a evidência no banco
                var ev = await _context.tbl_cq_evidencia.FirstOrDefaultAsync(e => e.id == evidenciaId);
                if (ev == null)
                    return Json(new { sucesso = false, mensagem = "Evidência não encontrada." });

                // ✅ Monta o comentário com data e usuário no servidor
                string timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                string novoComentario = $"{timestamp} - {usuario}: {comentario}";

                // ✅ Atualiza o campo no banco
                if (string.IsNullOrWhiteSpace(ev.comentario))
                    ev.comentario = novoComentario;
                else
                    ev.comentario += "\n" + novoComentario;

                // ✅ Persiste no banco
                _context.tbl_cq_evidencia.Update(ev);
                await _context.SaveChangesAsync();

                // ✅ Retorna lista atualizada para a modal
                var comentariosList = ev.comentario.Split('\n').ToList();
                return Json(new { sucesso = true, comentarios = comentariosList });
            }
            catch (Exception ex)
            {
                return Json(new { sucesso = false, mensagem = "Erro ao salvar comentário: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetComentarios(int evidenciaId)
        {
            var evidencia = await _context.tbl_cq_evidencia.FirstOrDefaultAsync(e => e.id == evidenciaId);
            if (evidencia == null)
                return Json(new List<string>());

            var lista = (evidencia.comentario ?? string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            return Json(lista);
        }


        [HttpGet]
        public async Task<JsonResult> ListarEvidencias(int cqId, string posto, string item)
        {
            var evidencias = await _context.tbl_cq_evidencia
                .Where(e => e.cq_id == cqId && e.posto == posto && e.item == item)
                .ToListAsync();

            if (!evidencias.Any())
                return Json(new { evidencias = new List<object>(), ultimoResultado = "N/A" });

            var lista = evidencias.Select(e => new
            {
                id = e.id,
                temArquivo = !string.IsNullOrEmpty(e.evidencia),
                comentario = e.comentario,
            });

            var ultimoResultado = evidencias.LastOrDefault()?.resultado ?? "N/A";

            return Json(new
            {
                evidencias = lista,
                ultimoResultado
            });
        }
        [HttpGet]
        public async Task<IActionResult> AtualizarTabelaChecklist(int cqId)
        {
            var inspecao = await _context.tbl_cq.FirstOrDefaultAsync(c => c.Id == cqId);
            if (inspecao == null)
                return Content("<tr><td colspan='4' class='text-danger'>Inspeção não encontrada.</td></tr>", "text/html");

            // aqui você reconstrói a estrutura básica dos postos e itens
            var postos = await _context.tbl_cq_evidencia
                .Where(e => e.cq_id == cqId)
                .GroupBy(e => e.posto)
                .Select(g => new
                {
                    Posto = g.Key,
                    Itens = g.GroupBy(i => i.item).Select(i => new
                    {
                        Nome = i.Key,
                        Resultado = i.OrderByDescending(x => x.id).FirstOrDefault().resultado ?? "N/A"
                    }).ToList()
                }).ToListAsync();

            var html = new System.Text.StringBuilder();
            foreach (var posto in postos)
            {
                var itemCount = posto.Itens.Count;
                for (int i = 0; i < itemCount; i++)
                {
                    var item = posto.Itens[i];
                    html.Append("<tr>");
                    if (i == 0)
                        html.Append($"<td rowspan='{itemCount}'><strong>{posto.Posto}</strong></td>");
                    html.Append($"<td>{item.Nome}</td>");
                    html.Append($"<td><span class='badge {(item.Resultado == "OK" ? "bg-success" : item.Resultado == "NG" ? "bg-danger" : "bg-secondary")}'>{item.Resultado}</span></td>");
                    html.Append("<td><button class='btn btn-sm btn-primary w-100 abrir-modal-btn' data-cq='" + cqId + "' data-posto='" + posto.Posto + "' data-item='" + item.Nome + "' data-resultado='" + item.Resultado + "'>Evidências / Comentários</button></td>");
                    html.Append("</tr>");
                }
            }

            return Content(html.ToString(), "text/html");
        }



        [HttpGet]
        public async Task<IActionResult> ListarComentarios(int cqId, string posto, string item)
        {
            var comentarios = await _context.tbl_cq_evidencia
                .Where(e => e.cq_id == cqId && e.posto == posto && e.item == item && e.comentario != null)
                .Select(e => e.comentario)
                .ToListAsync();

            return Json(comentarios);
        }

        [HttpDelete]
        public async Task<IActionResult> ExcluirEvidencia(int id)
        {
            var ev = await _context.tbl_cq_evidencia.FindAsync(id);
            if (ev == null)
                return Json(new { sucesso = false, mensagem = "Evidência não encontrada" });

            _context.tbl_cq_evidencia.Remove(ev);
            await _context.SaveChangesAsync();
            return Json(new { sucesso = true });
        }

        // ========================= SALVAR CHECKLIST =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarChecklist(CQViewModel model)
        {
            if (model == null || model.Inspecao == null)
                return BadRequest("Dados inválidos.");

            try
            {
                foreach (var posto in model.Postos)
                {
                    foreach (var item in posto.Itens)
                    {
                        // Recupera todas as evidências existentes para esse posto/item
                        var evidencias = await _context.tbl_cq_evidencia
                            .Where(e => e.cq_id == model.Inspecao.Id &&
                                        e.posto == posto.Posto &&
                                        e.item == item.Nome)
                            .ToListAsync();

                        // Atualiza o resultado para todas as linhas
                        foreach (var ev in evidencias)
                        {
                            ev.resultado = item.Resultado ?? "N/A";
                            _context.tbl_cq_evidencia.Update(ev);
                        }
                    }
                }

                // Atualiza status e comentário geral da inspeção
                var inspecao = await _context.tbl_cq.FindAsync(model.Inspecao.Id);
                if (inspecao != null)
                {
                    inspecao.Status = model.Inspecao.Status;
                    inspecao.ComentarioGeral = model.Inspecao.ComentarioGeral;
                    _context.tbl_cq.Update(inspecao);
                }

                await _context.SaveChangesAsync();

                TempData["Sucesso"] = "Checklist salvo com sucesso!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar checklist: {ex}");
                TempData["Erro"] = "Erro ao salvar checklist.";
                return RedirectToAction("Create", new { id = model.Inspecao.Id });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            bool isCQAdmin = HttpContext.Session.GetString("IsCQAdmin") == "true";
            Console.WriteLine($"IsCQAdmin: {isCQAdmin}");
            var inspecao = await _context.tbl_cq.FindAsync(id);

            if (inspecao == null)
                return NotFound();

            // Remove o registro
            _context.tbl_cq.Remove(inspecao);
            await _context.SaveChangesAsync();
            ViewBag.IsAdmin = isCQAdmin;

            return RedirectToAction(nameof(Index));
        }






        // ========================= MÉTODOS AUXILIARES =========================
        private List<PostoChecklist> GetPostosChecklist()
        {
            return new List<PostoChecklist>
    {
        new PostoChecklist
        {
            Posto = "PCB",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "As PCI's estão embaladas a vácuo, com sílica e Cartão de Umidade?" },
                new ChecklistItem { Nome = "O código e versão da PCB está de acordo com a FOLHA DE SETUP?" },
                new ChecklistItem { Nome = "A etiqueta está de acordo com o modelo em SETUP e posicionamento correto?" }
            }
        },
        new PostoChecklist
        {
            Posto = "SOLDA",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "A Pasta de solda/adesivo está de acordo com a FOLHA DE SETUP?" },
                new ChecklistItem { Nome = "A etiqueta (GBR-ETI-015/02) está devidamente preenchida e seguindo o tempo de descanso?" }
            }
        },
                new PostoChecklist
        {
            Posto = "STENCIL",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "A Identificação/Número do Stencil está conforme descrito na FOLHA DE SETUP?" }
            }
        },
                new PostoChecklist
        {
            Posto = "PRINTER",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "O programa carregado na PRINTER está conforme descrito na FOLHA DE SETUP?" },
                new ChecklistItem { Nome = "Uso do Backup-Pin / Base está de acordo com a FOLHA DE SETUP" }
            }
        },
                new PostoChecklist
        {
            Posto = "SPI",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "O programa carregado na SPI está conforme descrito na FOLHA DE SETUP?" }
            }
        },
                new PostoChecklist
        {
            Posto = "PLACEMENT",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "O programa carregado na NXT, OPAL, TOPAZ e/ou AX está conforme descrito na FOLHA DE SETUP?" },
                new ChecklistItem { Nome = "Os Tapes alimentados estão devidamente assinados paelos operadores?" },
                new ChecklistItem { Nome = "A etiqueta MSL (GBR-ETI-036/00) está sendo preenchida corretamente?" },
                new ChecklistItem { Nome = "Para produtos HIKIVISION; foi feita a validação do Software de memória?" },
                new ChecklistItem { Nome = "Pressão do ar-comprimido está de acordo com o especificado (0.4 ~ 0.8 MPa)" },
                new ChecklistItem { Nome = "As instruções de trabalho estão disponíveis no processo? Os operadores estão homologados?" }
            }
        },
                new PostoChecklist
        {
            Posto = "AOI-Pre",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "O programa carregado na AOI está conforme descrito na FOLHA DE SETUP?" },
                new ChecklistItem { Nome = "A IT disponível corresponde ao modelo em SETUP? Os operadores estão homologados?" }

            }
        },

                new PostoChecklist
        {
            Posto = "FORNO",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "O programa carregado na FORNO está conforme descrito na FOLHA DE SETUP?" },
                new ChecklistItem { Nome = "Foi aferido o perfil de temperatura e documento impresso?" },
                new ChecklistItem { Nome = "As Zonas do perfil impresso correspondem as apresentadas no monitor do Forno?" },
                new ChecklistItem { Nome = "A central do forno (RCS) está de acordo com a FOLHA DE SETUP?" },

            }
        },

            new PostoChecklist
        {
            Posto = "AOI-Pos",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "O programa carregado na AOI está conforme descrito na FOLHA DE SETUP?" },
                new ChecklistItem { Nome = "A IT disponível corresponde ao modelo em SETUP? Os operadores estão homologados?" }

            }
        },
            new PostoChecklist
        {
            Posto = "REVISÃO FINAL",
            Itens = new List<ChecklistItem>
            {
                new ChecklistItem { Nome = "A IT disponível corresponde ao modelo em SETUP? Os operadores estão Homologaos?" }

            }
        }
    };
        }

        [HttpGet]
        public async Task<IActionResult> ExportarXLSX(int id)
        {
            var inspecao = await _context.tbl_cq.FirstOrDefaultAsync(c => c.Id == id);
            if (inspecao == null)
                return NotFound();

            var evidencias = _context.tbl_cq_evidencia
                .Where(e => e.cq_id == id)
                .OrderBy(e => e.id)
                .ToList();

            byte[] excelBytes;

            using (var stream = new MemoryStream())
            {
                using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
                {
                    var workbookPart = doc.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();

                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    var sheetData = new SheetData();
                    worksheetPart.Worksheet = new Worksheet(sheetData);

                    var sheets = doc.WorkbookPart.Workbook.AppendChild(new Sheets());
                    sheets.Append(new Sheet
                    {
                        Id = doc.WorkbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = "Relatório"
                    });

                    int rowIndex = 1;

                    AddText(sheetData, rowIndex++, 1, $"OP {inspecao.OP} – {inspecao.Modelo}");
                    AddText(sheetData, rowIndex++, 1, $"Inspetor: {inspecao.Responsavel}");
                    rowIndex++;

                    var grupos = evidencias
                        .GroupBy(e => new { e.posto, e.item })
                        .OrderBy(g => g.Min(x => x.id));

                    foreach (var grupo in grupos)
                    {
                        var item = grupo.First();

                        AddText(sheetData, rowIndex++, 1, $"{item.item} – Resultado: {item.resultado ?? "N/A"}");
                        AddText(sheetData, rowIndex++, 1, $"{item.posto}");
                        rowIndex++;

                        foreach (var ev in grupo.Where(e => !string.IsNullOrEmpty(e.evidencia)))
                        {
                            if (System.IO.File.Exists(ev.evidencia))
                            {
                                InsertImage(worksheetPart, ev.evidencia, 1, rowIndex);

                                using var img = System.Drawing.Image.FromFile(ev.evidencia);
                                int rowsUsed = Math.Max(4, img.Height / 250);

                                rowIndex += rowsUsed;
                            }
                        }

                        var comentarios = grupo
                            .Where(x => !string.IsNullOrWhiteSpace(x.comentario))
                            .Select(x => x.comentario);

                        if (comentarios.Any())
                        {
                            AddText(sheetData, rowIndex++, 1, "Comentários:");
                            foreach (var c in comentarios)
                                AddText(sheetData, rowIndex++, 1, c);
                        }

                        rowIndex += 2;
                    }

                    workbookPart.Workbook.Save();
                }

                excelBytes = stream.ToArray();
            }

            // ============================================================
            // SALVAR NA REDE + LOG DETALHADO
            // ============================================================
            try
            {
                Console.WriteLine("=== INICIANDO SALVAMENTO NA REDE ===");

                string basePath = @"\\192.168.1.8\publica\Sistema SMD\Relatorios CQ";
                string folderName = $"{DateTime.Now:yyyy-MM-dd}-OP{inspecao.OP}";
                string fullFolder = Path.Combine(basePath, folderName);

                Console.WriteLine($"Caminho base: {basePath}");
                Console.WriteLine($"Pasta final: {fullFolder}");

                Console.WriteLine("Conectando ao compartilhamento...");

                using (new NetworkShareConnection(
                    @"\\192.168.1.8\publica",
                    new NetworkCredential("sistema.smd", "Gbr@2025", "ad.gbrcomponentes.com.br")
                ))
                {
                    Console.WriteLine("Conexão OK.");

                    if (!Directory.Exists(fullFolder))
                    {
                        Console.WriteLine("Pasta não existe. Criando...");
                        Directory.CreateDirectory(fullFolder);
                        Console.WriteLine("Pasta criada.");
                    }
                    else
                    {
                        Console.WriteLine("Pasta já existe.");
                    }

                    string excelPath = Path.Combine(fullFolder, $"Relatorio_OP{inspecao.OP}.xlsx");
                    Console.WriteLine($"Salvando arquivo em: {excelPath}");

                    System.IO.File.WriteAllBytes(excelPath, excelBytes);

                    Console.WriteLine("Arquivo salvo com sucesso!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("=== ERRO AO SALVAR NA REDE ===");
                Console.WriteLine("Mensagem: " + ex.Message);
                Console.WriteLine("StackTrace: " + ex.StackTrace);
            }

            // ============================================================
            // DOWNLOAD PARA O USUÁRIO
            // ============================================================
            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Relatorio_CQ_OP{inspecao.OP}.xlsx"
            );
        }




        private Stylesheet CreateStyleSheet()
        {
            return new Stylesheet(
                new Fonts(
                    // 0 – normal
                    new Font(new FontSize { Val = 11 }),

                    // 1 – título grande
                    new Font(new Bold(), new FontSize { Val = 16 }),

                    // 2 – subtítulo
                    new Font(new Bold(), new FontSize { Val = 12 })
                ),
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 })
                ),
                new Borders(
                    new Border(), // sem borda
                    new Border(
                        new LeftBorder(new Color() { Auto = true }) { Style = BorderStyleValues.Thin },
                        new RightBorder(new Color() { Auto = true }) { Style = BorderStyleValues.Thin },
                        new TopBorder(new Color() { Auto = true }) { Style = BorderStyleValues.Thin },
                        new BottomBorder(new Color() { Auto = true }) { Style = BorderStyleValues.Thin },
                        new DiagonalBorder())
                ),
                new CellFormats(
                    new CellFormat(),                                  // 0 – normal
                    new CellFormat { FontId = 1, ApplyFont = true },   // 1 – título
                    new CellFormat { FontId = 2, ApplyFont = true },   // 2 – subtítulo
                    new CellFormat { BorderId = 1, ApplyBorder = true } // 3 – bordas
                )
            );
        }

        private void AddText(SheetData sheetData, int rowIndex, int columnIndex, string text, uint style = 0)
        {
            var row = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex == rowIndex)
                      ?? new Row() { RowIndex = (uint)rowIndex };

            if (!sheetData.Elements<Row>().Any(r => r.RowIndex == row.RowIndex))
                sheetData.Append(row);

            var cell = new Cell
            {
                CellReference = $"{GetColumnLetter(columnIndex)}{rowIndex}",
                DataType = CellValues.String,
                CellValue = new CellValue(text),
                StyleIndex = style
            };

            row.Append(cell);
        }

        private string GetColumnLetter(int column)
        {
            var letter = "";
            while (column > 0)
            {
                var modulo = (column - 1) % 26;
                letter = Convert.ToChar(65 + modulo) + letter;
                column = (column - modulo) / 26;
            }
            return letter;
        }

        private void InsertImage(WorksheetPart worksheetPart, string imagePath, int col, int row)
        {
            DrawingsPart drawingsPart = worksheetPart.DrawingsPart;
            WorksheetDrawing wsDr;

            // 1 — Garante DrawingsPart
            if (drawingsPart == null)
            {
                drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
                wsDr = new WorksheetDrawing();
                drawingsPart.WorksheetDrawing = wsDr;

                worksheetPart.Worksheet.Append(
                    new DocumentFormat.OpenXml.Spreadsheet.Drawing
                    {
                        Id = worksheetPart.GetIdOfPart(drawingsPart)
                    }
                );
            }
            else
            {
                wsDr = drawingsPart.WorksheetDrawing;
            }

            // 2 — Cria o ImagePart
            ImagePart imgPart = drawingsPart.AddImagePart(ImagePartType.Jpeg);

            byte[] imgBytes = ProcessarImagemParaExcel(imagePath);

            using (var ms = new MemoryStream(imgBytes))
            {
                imgPart.FeedData(ms);
            }


            string imgId = drawingsPart.GetIdOfPart(imgPart);

            // 3 — Define posição (TwoCellAnchor)
            var anchor = new Xdr.TwoCellAnchor(
                new Xdr.FromMarker(
                    new Xdr.ColumnId((col - 1).ToString()),
                    new Xdr.ColumnOffset("0"),
                    new Xdr.RowId((row - 1).ToString()),
                    new Xdr.RowOffset("0")
                ),
                new Xdr.ToMarker(
                    new Xdr.ColumnId((col + 3).ToString()),     // largura aproximada
                    new Xdr.ColumnOffset("0"),
                    new Xdr.RowId((row + 10).ToString()),       // altura aproximada
                    new Xdr.RowOffset("0")
                )
            );

            // 4 — Configura o desenho
            uint picId = (uint)(wsDr.ChildElements.Count + 1);

            var picture = new Xdr.Picture(
                new Xdr.NonVisualPictureProperties(
                    new Xdr.NonVisualDrawingProperties
                    {
                        Id = picId,
                        Name = "img" + picId
                    },
                    new Xdr.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })
                ),
                new Xdr.BlipFill(
                    new A.Blip { Embed = imgId },
                    new A.Stretch(new A.FillRectangle())
                ),
                new Xdr.ShapeProperties(
                    new A.Transform2D(
                        new A.Offset { X = 0, Y = 0 },
                        new A.Extents { Cx = 1000000, Cy = 750000 }
                    ),
                    new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle }
                )
            );

            anchor.Append(picture);
            anchor.Append(new Xdr.ClientData());

            wsDr.Append(anchor);
        }



        private DocumentFormat.OpenXml.Drawing.Spreadsheet.Picture CreateImageElement(
    ImagePart imagePart,
    DrawingsPart drawingsPart,
    int col,
    int row)
        {
            string imagePartId = drawingsPart.GetIdOfPart(imagePart);

            var pic = new DocumentFormat.OpenXml.Drawing.Spreadsheet.Picture(
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.NonVisualPictureProperties(
                    new DocumentFormat.OpenXml.Drawing.Spreadsheet.NonVisualDrawingProperties
                    {
                        Id = (UInt32Value)1U,
                        Name = "Picture"
                    },
                    new DocumentFormat.OpenXml.Drawing.Spreadsheet.NonVisualPictureDrawingProperties()),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.BlipFill(
                    new DocumentFormat.OpenXml.Drawing.Blip
                    {
                        Embed = imagePartId,
                        CompressionState = DocumentFormat.OpenXml.Drawing.BlipCompressionValues.Print
                    },
                    new DocumentFormat.OpenXml.Drawing.Stretch(
                        new DocumentFormat.OpenXml.Drawing.FillRectangle())
                ),
                new DocumentFormat.OpenXml.Drawing.Spreadsheet.ShapeProperties(
                    new DocumentFormat.OpenXml.Drawing.Transform2D(
                        new DocumentFormat.OpenXml.Drawing.Offset { X = col * 20_000, Y = row * 20_000 },
                        new DocumentFormat.OpenXml.Drawing.Extents { Cx = 200_0000, Cy = 200_0000 })
                )
            );

            return pic;
        }


        public IActionResult PreparandoDownload(int id)
        {
            ViewBag.Id = id;
            return View();
        }
        /// <summary>
        /// Salva o arquivo Excel no servidor externo 192.168.1.8
        /// </summary>
        private void SalvarExcelEmRede(byte[] excelBytes, string opNumber)
        {
            try
            {
                string basePath = @"\\192.168.1.8\publica\Sistema SMD\Relatorios CQ";
                string folderName = $"{DateTime.Now:yyyy-MM-dd}-{opNumber}";
                string fullFolder = Path.Combine(basePath, folderName);

                if (!Directory.Exists(fullFolder))
                    Directory.CreateDirectory(fullFolder);

                string excelPath = Path.Combine(fullFolder, $"Relatorio_{opNumber}.xlsx");

                using (new NetworkShareConnection(
                    @"\\192.168.1.8\publica",
                    new NetworkCredential("sistema.smd", "Gbr@2025", "")))
                {
                    System.IO.File.WriteAllBytes(excelPath, excelBytes);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao salvar o Excel no compartilhamento de rede: " + ex.Message, ex);
            }
        }


        private byte[] ProcessarImagemParaExcel(string caminho, int maxWidth = 1600, long qualidade = 92)
        {
            using (var img = System.Drawing.Image.FromFile(caminho))
            {
                // Se já é pequena, retorna como está
                if (img.Width <= maxWidth)
                    return System.IO.File.ReadAllBytes(caminho);

                double ratio = (double)maxWidth / img.Width;
                int novaLargura = maxWidth;
                int novaAltura = (int)(img.Height * ratio);

                using (var bmp = new System.Drawing.Bitmap(novaLargura, novaAltura))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

                    g.DrawImage(img, 0, 0, novaLargura, novaAltura);

                    using (var ms = new MemoryStream())
                    {
                        var encoder = System.Drawing.Imaging.ImageCodecInfo.GetImageDecoders()
                            .First(c => c.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);

                        var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                        encoderParams.Param[0] = new System.Drawing.Imaging.EncoderParameter(
                            System.Drawing.Imaging.Encoder.Quality, qualidade
                        );

                        bmp.Save(ms, encoder, encoderParams);
                        return ms.ToArray();
                    }
                }
            }
        }



        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }
    }




}
