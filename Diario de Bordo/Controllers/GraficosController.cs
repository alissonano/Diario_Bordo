using Diario_de_Bordo.Data;
using Diario_de_Bordo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;

namespace Diario_de_Bordo.Controllers
{
    public class GraficosController : Controller
    {
        private readonly DiarioContext _context;

        public GraficosController(DiarioContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? dataInicio, DateTime? dataFim, string tecnico = null, string turno = null)
        {
            var usuarioLogado = HttpContext.Session.GetString("UsuarioLogado") ?? "Desconhecido";

            // Mantendo a lógica original que funciona: ToUniversalTime()
            DateTime inicio = (dataInicio ?? DateTime.Today.AddDays(-30)).ToUniversalTime();
            DateTime fim = (dataFim ?? DateTime.Now).ToUniversalTime();

            var registrosQuery = _context.tbl_report
                .Where(r => r.date_field >= inicio && r.date_field <= fim)
                .AsQueryable();

            if (!string.IsNullOrEmpty(tecnico))
                registrosQuery = registrosQuery.Where(r => r.technician == tecnico);

            // Filtro de Turno (Lógica Original)
            if (!string.IsNullOrEmpty(turno))
            {
                if (turno == "1T")
                    registrosQuery = registrosQuery.Where(r => r.inicio.TimeOfDay >= TimeSpan.FromHours(6) && r.inicio.TimeOfDay <= TimeSpan.FromHours(16));
                else if (turno == "2T")
                    registrosQuery = registrosQuery.Where(r => r.inicio.TimeOfDay > TimeSpan.FromHours(16) || r.inicio.TimeOfDay < TimeSpan.FromHours(6));
            }

            var registros = await registrosQuery.ToListAsync();

            // --- RELATÓRIO GERENCIAL (Ajustando +4h para exibição correta) ---
            var dadosProcessados = registros.Select(r => new {
                r.technician,
                r.linha,
                r.maquina,
                r.classificacao,
                r.date_field,
                r.descricao,
                // Duração é absoluta, não depende de timezone
                Horas = (r.fim - r.inicio).TotalHours,
                // Início corrigido para Manaus (+4h) para o relatório não "pular o dia"
                InicioLocal = r.inicio.AddHours(4)
            }).ToList();

            // 1. Relatório por Técnico
            ViewBag.RelatorioTecnico = dadosProcessados.GroupBy(x => x.technician)
                .Select(g => new { Nome = g.Key, Qtd = g.Count(), TotalHrs = g.Sum(x => x.Horas) })
                .OrderByDescending(x => x.TotalHrs).ToList();

            // 2. Relatório por Linha
            ViewBag.RelatorioLinha = dadosProcessados.GroupBy(x => x.linha)
                .Select(g => new { Linha = g.Key, Qtd = g.Count(), TotalHrs = g.Sum(x => x.Horas) })
                .OrderByDescending(x => x.TotalHrs).ToList();

            // 3. Relatório por Equipamento
            ViewBag.RelatorioMaquina = dadosProcessados.GroupBy(x => new { x.maquina, x.linha })
                .Select(g => new { g.Key.maquina, g.Key.linha, Qtd = g.Count(), TotalHrs = g.Sum(x => x.Horas) })
                .OrderByDescending(x => x.TotalHrs).ToList();

            // --- Dados para Gráficos ---
            ViewBag.TempoPorLinha = dadosProcessados.GroupBy(x => x.linha)
                .Select(g => new { linha = g.Key, minutos = g.Sum(x => x.Horas * 60) }).ToList();

            ViewBag.EvolucaoDiaria = dadosProcessados.GroupBy(x => new { x.linha, Dia = x.date_field.ToString("yyyy-MM-dd") })
                .Select(g => new { linha = g.Key.linha, dia = g.Key.Dia, minutos = g.Sum(x => x.Horas * 60) })
                .OrderBy(x => x.dia).ToList();

            ViewBag.TempoUsuario = dadosProcessados.Where(x => x.technician == (tecnico ?? usuarioLogado)).Sum(x => x.Horas);

            // ViewBags de Filtro
            ViewBag.Tecnicos = await _context.tbl_report.Select(r => r.technician).Distinct().ToListAsync();
            ViewBag.UsuarioLogado = usuarioLogado;
            ViewBag.DataInicio = inicio.ToString("yyyy-MM-dd");
            ViewBag.DataFim = fim.ToString("yyyy-MM-dd");
            ViewBag.TecnicoSelecionado = tecnico;
            ViewBag.TurnoSelecionado = turno;

            return View();
        }
    }
}
