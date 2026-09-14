using Diario_de_Bordo.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Diario_de_Bordo.Controllers
{
    [Authorize]
    [RequireLogin] // todas as actions aqui exigem login
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var nome = HttpContext.Session.GetString("UsuarioLogado");
            ViewBag.NomeUsuario = nome ?? "Visitante";
            return View();
        }
        public IActionResult Graficos()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }
    }
}
