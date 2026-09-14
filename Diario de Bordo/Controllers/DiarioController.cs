using Diario_de_Bordo.Data;
using Diario_de_Bordo.Filters;
using Diario_de_Bordo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Globalization;

namespace Diario_de_Bordo.Controllers
{
    [RequireLogin]
    public class DiarioController : Controller
    {
        private readonly DiarioContext _context;

        public DiarioController(DiarioContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var reports = await _context.tbl_report
                .OrderByDescending(r => r.report_id)
                .Select(r => new Report
                {
                    report_id = r.report_id,
                    technician = r.technician,
                    linha = r.linha,
                    maquina = r.maquina,
                    classificacao = r.classificacao,
                    descricao = r.descricao,
                    // Ajusta o horário aqui para -4 horas antes de enviar para a View
                    inicio = r.inicio.AddHours(-1),
                    fim = r.fim.AddHours(-1)
                })
                .ToListAsync();

            return View(reports);
        }


        // GET: /Diario/Nova
        public async Task<IActionResult> Nova()
        {
            // 1. Obtém o usuário logado da sessão
            var usuarioLogado = HttpContext.Session.GetString("UsuarioLogado");
            ViewBag.UsuarioLogado = usuarioLogado;

            // 2. Carrega o estoque para a busca na modal
            ViewBag.Estoque = await _context.tbl_itens_estoque.ToListAsync();

            // 3. Busca apenas as atividades do técnico logado
            ViewBag.UltimasAtividades = await _context.tbl_report
                .Where(r => r.technician == usuarioLogado) // <-- FILTRO ADICIONADO AQUI
                .OrderByDescending(r => r.report_id)
                .Take(10)
                .ToListAsync();

            return View();
        }

        [HttpGet]
        public IActionResult ObterEvidencias(int id)
        {
            var evidencias = _context.tbl_report_evidencias
                .Where(e => e.report_id == id)
                .Select(e => new
                {
                    e.evidencia_id,
                    e.file_name,
                    file_url = Url.Action("VerEvidencia", "Diario", new { id = e.evidencia_id })
                })
                .ToList();

            return Json(evidencias);
        }

        [HttpGet]
        public IActionResult GetEvidencias(int id)
        {
            // Busque no banco os caminhos dos arquivos associados ao report_id = id
            var arquivos = _context.tbl_report_evidencias
                                   .Where(e => e.report_id == id)
                                   .Select(e => e.file_path) // Ex: "/uploads/foto1.jpg"
                                   .ToList();

            return Json(arquivos);
        }


        [HttpGet]
        public IActionResult VerEvidencia(int id)
        {
            var evidencia = _context.tbl_report_evidencias.FirstOrDefault(e => e.evidencia_id == id);
            if (evidencia == null || !System.IO.File.Exists(evidencia.file_path))
                return NotFound();

            string contentType = "application/octet-stream"; // Padrão genérico
            string ext = Path.GetExtension(evidencia.file_path)?.ToLowerInvariant();

            // Dicionário de extensões comuns
            var types = new Dictionary<string, string>
    {
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".pdf", "application/pdf" }
    };

            if (types.ContainsKey(ext))
            {
                contentType = types[ext];
            }

            var fileBytes = System.IO.File.ReadAllBytes(evidencia.file_path);

            // Para PDFs, isso abrirá em uma nova aba. Para imagens, também.
            return File(fileBytes, contentType);
        }

        // POST: /Diario/Nova
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Nova(Report model, List<IFormFile> evidencias, string pecasJson)
        {
            try
            {
                // 1. Preenchimento de segurança do modelo
                if (string.IsNullOrEmpty(model.technician))
                    model.technician = HttpContext.Session.GetString("UsuarioLogado");

                // Mantendo exatamente suas configurações de data
                // Mude de UtcNow para Now (Horário do Servidor)
                model.date_field = DateTime.UtcNow;

                // 2. Converter Início e Fim digitados para UTC antes de salvar
                // Isso resolve o erro de Kind=Local e mantém a integridade no Postgres
                model.inicio = TimeZoneInfo.ConvertTimeToUtc(model.inicio, TimeZoneInfo.Local);
                model.fim = TimeZoneInfo.ConvertTimeToUtc(model.fim, TimeZoneInfo.Local);

                // 2. Salva o Report Principal (Pai)
                _context.tbl_report.Add(model);
                await _context.SaveChangesAsync();

                // --- INÍCIO DA LÓGICA DE LOG EM C:\logs ---
                try
                {
                    string logDirectory = @"C:\logs";
                    if (!Directory.Exists(logDirectory)) Directory.CreateDirectory(logDirectory);

                    string logFile = Path.Combine(logDirectory, "log_pecas.txt");
                    using (StreamWriter sw = System.IO.File.AppendText(logFile))
                    {
                        sw.WriteLine($"ID: {model.report_id} | Data: {DateTime.Now} | Técnico: {model.technician}");
                        sw.WriteLine($"JSON Peças: {pecasJson}");
                        sw.WriteLine("-------------------------------------------------------------------------");
                    }
                }
                catch { /* Falha silenciosa no log para não travar a gravação principal */ }

                // 3. Processa Solicitação de Peças
                if (!string.IsNullOrEmpty(pecasJson) && pecasJson != "[]")
                {
                    var itensSolicitados = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ItemPecaDTO>>(pecasJson);

                    foreach (var item in itensSolicitados)
                    {
                        var novaRequisicao = new SolicitacaoPeca
                        {
                            ReportId = model.report_id,
                            TecnicoResponsavel = model.technician,
                            DataSolicitacao = DateTime.UtcNow,
                            Quantidade = item.quantidade,
                            Status = 0
                        };

                        if (item.id > 0)
                            novaRequisicao.ItemEstoqueId = item.id;
                        else
                            novaRequisicao.DescricaoAvulsa = item.descricao;

                        _context.tbl_solicitacoes_pecas.Add(novaRequisicao);
                    }
                }

                // 4. Processa os arquivos enviados (Evidências)
                if (evidencias != null && evidencias.Count > 0)
                {
                    string linhaLimpa = string.Concat((model.linha ?? "Geral").Split(Path.GetInvalidFileNameChars()));
                    string dataPasta = DateTime.Now.ToString("yyyyMMdd");
                    string basePath = Path.Combine(@"\\172.20.100.20\Programs\Diario_Evidencias", linhaLimpa, dataPasta);

                    if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

                    foreach (var file in evidencias)
                    {
                        if (file.Length > 0)
                        {
                            string fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
                            string fullPathUNC = Path.Combine(basePath, fileName);

                            using (var stream = new FileStream(fullPathUNC, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            var novaEvidencia = new ReportEvidencia
                            {
                                report_id = model.report_id,
                                file_name = file.FileName,
                                file_path = fullPathUNC,
                                data_upload = DateTime.UtcNow
                            };
                            _context.tbl_report_evidencias.Add(novaEvidencia);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Atividade e solicitações gravadas!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro no servidor: {ex.Message}");
            }
        }

        // DTO Auxiliar para receber a lista de peças via JSON
        public class ItemPecaDTO
        {
            public int id { get; set; }
            public string descricao { get; set; }
            public int quantidade { get; set; }
            public string tipo { get; set; } // Opcional: para diferenciar 'Estoque' de 'Avulso'
        }

        [HttpPost]
        public async Task<IActionResult> UploadEvidencia(int report_id, string linha, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            try
            {
                // Monta o diretório com base na linha e data
                string dataFolder = DateTime.UtcNow.ToString("yyyyddMM", CultureInfo.InvariantCulture);
                string basePath = @"\\172.20.100.20\Programs\Diario_Evidencias";
                string destino = Path.Combine(basePath, linha, dataFolder);

                // Garante que o diretório exista
                if (!Directory.Exists(destino))
                    Directory.CreateDirectory(destino);

                // Nome único
                string serialFoto = $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}";
                string extensao = Path.GetExtension(file.FileName);
                string fileName = $"{serialFoto}{extensao}";
                string filePath = Path.Combine(destino, fileName);

                // Salva o arquivo fisicamente
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Cria o registro no banco via EF
                var evidencia = new ReportEvidencia
                {
                    report_id = report_id,
                    file_name = fileName,
                    file_path = filePath
                };

                _context.tbl_report_evidencias.Add(evidencia);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Evidência salva com sucesso!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Erro ao salvar evidência: {ex.Message}");
            }
        }


    }
}
