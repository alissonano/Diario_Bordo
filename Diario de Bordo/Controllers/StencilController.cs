using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Diario_de_Bordo.Controllers
{
    public class StencilController : Controller
    {
        private readonly DiarioContext _context;

        public StencilController(DiarioContext context)
        {
            _context = context;
        }

        // GET: /Stencil
        public async Task<IActionResult> Index()
        {
            var stencils = _context.tbl_stencil
                .AsEnumerable() // executa o SELECT e depois ordena em memória
                .OrderBy(s =>
                {
                    if (int.TryParse(s.num_stencil, out int n))
                        return n;
                    return int.MaxValue; // coloca não numéricos no final
                })
                .ToList();

            return View(stencils);
        }

        // GET: /Stencil/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Stencil());
        }

        // POST: /Stencil/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Stencil model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 🔍 Verifica se já existe um stencil com o mesmo número
            var existeStencil = _context.tbl_stencil
                .Any(s => s.num_stencil.ToLower() == model.num_stencil.ToLower());

            if (existeStencil)
            {
                ModelState.AddModelError("num_stencil", "Este número de stencil já está cadastrado.");
                return View(model);
            }

            // ✅ Se não existir, salva normalmente
            _context.tbl_stencil.Add(model);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Stencil cadastrado com sucesso!";
            return RedirectToAction("Index");
        }

        // GET: /Stencil/Check/{id}
        public async Task<IActionResult> Check(int id)
        {
            var stencil = await _context.tbl_stencil
                                        .Include(s => s.Stencil_Checks)
                                        .FirstOrDefaultAsync(s => s.id == id);

            if (stencil == null)
                return NotFound();

            var vm = new StencilCheckViewModel
            {
                StencilId = stencil.id,
                StencilNumber = stencil.num_stencil,
                ChecksHistory = stencil.Stencil_Checks.OrderByDescending(c => c.check_date).ToList(),
                IsLider = HttpContext.Session.GetString("IsLider") == "true",
                IsEngenheiro = HttpContext.Session.GetString("IsEngenheiro") == "true",
                IsOperador = HttpContext.Session.GetString("IsOperador") == "true"
            };

            return View(vm);
        }

        // POST: Criar nova checagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterCheck(int StencilId)
        {
            var stencil = await _context.tbl_stencil.FindAsync(StencilId);
            if (stencil == null) return NotFound();

            var newCheck = new Stencil_Check
            {
                id_stencil = StencilId,
                status = "Aguardando Verificação",
                check_date = DateTime.UtcNow,
                log = "",
                evidence_unc = ""
            };

            _context.tbl_stencilcheck.Add(newCheck);
            await _context.SaveChangesAsync();

            // salva o ID da nova checagem para abrir modal
            TempData["OpenModalCheckId"] = newCheck.id;

            return RedirectToAction("Check", new { id = StencilId });
        }



        // POST: Registrar resultado da checagem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitCheck(StencilCheckViewModel vm)
        {
            var check = await _context.tbl_stencilcheck
                                      .FirstOrDefaultAsync(c => c.id == vm.CheckId);
            if (check == null) return NotFound();

            var userName = User.Identity?.Name ?? "Usuário Desconhecido";
            var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            var logEntry = $"{timestamp} {userName}: {(vm.Decision ?? "Comentário")} - {vm.Comentario}";

            // Preencher campo correto
            switch (vm.CheckType)
            {
                case "user":
                    // Operador adiciona comentário/evidência, mas não julga
                    break;
                case "check1":
                    if (string.IsNullOrEmpty(check.check1))
                        check.check1 = userName;
                    break;
                case "check2":
                    if (string.IsNullOrEmpty(check.check2))
                        check.check2 = userName;
                    break;
            }

            // Atualiza status apenas se for líder ou engenheiro
            if (vm.CheckType == "check1" || vm.CheckType == "check2")
            {
                if (vm.CheckType == "check2")
                {
                    // Se for engenharia, usa o julgamento diretamente
                    check.status = vm.Decision ?? "Aguardando Verificação";
                }
                else
                {
                    // Se for o líder, ainda está aguardando a engenharia
                    check.status = "Aguardando Verificação";
                }
            }


            // Atualiza log
            check.log = string.IsNullOrEmpty(check.log) ? logEntry : check.log + "\n" + logEntry;

            // Salva evidências evitando duplicatas e acumulando uploads anteriores
            if (vm.Files != null && vm.Files.Count > 0)
            {
                var basePath = $@"\\172.20.100.20\Programs\Inspecoes_Stencil\{check.id_stencil}\{DateTime.UtcNow:yyyyMMdd}\";
                Directory.CreateDirectory(basePath);

                // Começa com as evidências já existentes (se houver)
                var existingFiles = (check.evidence_unc ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                int nextIndex = existingFiles.Count + 1;
                foreach (var file in vm.Files)
                {
                    var filePath = Path.Combine(basePath, $"foto{nextIndex++}{Path.GetExtension(file.FileName)}");

                    using var stream = System.IO.File.Create(filePath);
                    await file.CopyToAsync(stream);

                    // Evita duplicar caminhos já existentes
                    if (!existingFiles.Contains(filePath))
                        existingFiles.Add(filePath);
                }

                // Junta tudo de volta em uma única string separada por ";"
                check.evidence_unc = string.Join(";", existingFiles);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Check registrado com sucesso!";

            return RedirectToAction("Check", new { id = check.id_stencil });
        }


        // Exibe imagem salva
        public IActionResult ViewImage(string path)
        {
            if (System.IO.File.Exists(path))
            {
                var bytes = System.IO.File.ReadAllBytes(path);
                return File(bytes, "image/jpeg");
            }
            return NotFound();
        }



        // GET: Stencils/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            // Verifica se é Engenheiro pela Session
            if (HttpContext.Session.GetString("IsEngenheiro") != "true")
            {
                // Define a mensagem de erro que será lida na View
                TempData["ErrorMessage"] = "Acesso Não Permitido: Por favor, procure o setor de Engenharia para realizar alterações.";
                return RedirectToAction(nameof(Index));
            }

            if (id == null) return NotFound();
            var stencil = await _context.tbl_stencil.FindAsync(id);
            if (stencil == null) return NotFound();

            return View(stencil);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("id,num_stencil,descricao,cod_fabricante,fabricante,data_fabricacao,cliente")] Stencil stencil)
        {
            if (id != stencil.id) return NotFound();

            // REMOVE validações de objetos relacionados que não estão no form
            ModelState.Remove("Stencil_Checks");

            if (ModelState.IsValid)
            {
                try
                {
                    // Força o Entity Framework a entender que este objeto foi modificado
                    _context.Entry(stencil).State = EntityState.Modified;

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Caso ocorra erro de banco (ex: coluna faltando), ele mostrará aqui
                    ModelState.AddModelError("", "Erro no banco de dados: " + ex.Message);
                }
            }

            // Se chegar aqui, houve erro de validação. 
            // Vamos adicionar um alerta visual para você saber o que falhou:
            var erros = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            ViewBag.ErroValidacao = "Verifique os campos. Erros: " + string.Join(", ", erros);

            return View(stencil);
        }

        private bool StencilExists(int id)
        {
            return _context.tbl_stencil.Any(e => e.id == id);
        }



        // =====================================================
        // IMPORTAÇÃO ÚNICA DE STENCILS A PARTIR DE ARQUIVO LOCAL
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ImportarArquivo()
        {
            string caminhoArquivo = @"C:\Imports\Stencils.txt"; // 👈 ajuste aqui o caminho do arquivo
            if (!System.IO.File.Exists(caminhoArquivo))
                return Content("❌ Arquivo não encontrado em: " + caminhoArquivo);

            var linhas = System.IO.File.ReadAllLines(caminhoArquivo);
            if (linhas.Length <= 1)
                return Content("⚠️ Arquivo vazio ou inválido.");

            char delimitador = linhas[0].Contains(';') ? ';' : '\t';
            int inseridos = 0;

            for (int i = 1; i < linhas.Length; i++)
            {
                var campos = linhas[i].Split(delimitador);
                if (campos.Length < 4)
                    continue;

                string cliente = campos[0].Trim();
                string numStencil = campos[1].Trim();
                string descricao = campos[2].Trim();
                string dataFabricacao = campos[3].Trim();

                if (string.IsNullOrWhiteSpace(numStencil))
                    continue;

                bool existe = _context.tbl_stencil.Any(s => s.num_stencil == numStencil && s.descricao == descricao);
                if (existe)
                    continue;

                var stencil = new Stencil
                {
                    cliente = cliente,
                    num_stencil = numStencil,
                    descricao = descricao,
                    data_fabricacao = dataFabricacao
                };

                _context.tbl_stencil.Add(stencil);
                inseridos++;
            }

            await _context.SaveChangesAsync();

            return Content($"✅ Importação finalizada — {inseridos} stencils adicionados com sucesso.");
        }
    }
}
