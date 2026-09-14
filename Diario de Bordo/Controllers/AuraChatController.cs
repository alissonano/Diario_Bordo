using Microsoft.AspNetCore.Mvc;

namespace Diario_de_Bordo.Controllers
{
    public class AuraChatController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
