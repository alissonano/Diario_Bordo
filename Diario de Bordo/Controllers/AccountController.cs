using Diario_de_Bordo.Models;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Diario_de_Bordo.Controllers
{
    public class AccountController : Controller
    {
        private readonly LdapAuthService _ldap;

        public AccountController()
        {
            // Ajuste aqui se necessário
            //_ldap = new LdapAuthService("172.20.100.2", "DC=ad,DC=gbrsmtserver,DC=local");
            _ldap = new LdapAuthService("172.20.100.2", "CN=Users,DC=ad,DC=gbrsmtserver,DC=local", "ad.gbrsmtserver.local", 389);
        }

        [HttpGet]
        public IActionResult Login()
        {
            // Não precisa checar User.IsAuthenticated aqui, o pipeline já faz o redirecionamento
            return View(new LoginViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // Finaliza a sessão do cookie (logout)
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear(); // Limpa a Session (se ainda estiver usando)
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var nome = _ldap.AutenticarERetornarNome(model.Matricula, model.Senha);

            if (nome != null)
            {
                // Salva nome do usuário na sessão
                HttpContext.Session.SetString("UsuarioLogado", nome);

                // Cria claims e cookie de autenticação
                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, nome),
            new Claim(ClaimTypes.NameIdentifier, model.Matricula),
        };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity)
                );

                // Obtém grupos do usuário
                var grupos = _ldap.ObterTodosGruposDoUsuario(model.Matricula);

                // Define se é admin e salva na sessão
                bool isAdmin = grupos.Contains("CQManager");
                bool isLider = grupos.Contains("ProdLider");
                bool isEngenheiro = grupos.Contains("Engenharia");
                bool isOperador = grupos.Contains("Operador");
                HttpContext.Session.SetString("IsCQAdmin", isAdmin ? "true" : "false");
                HttpContext.Session.SetString("IsLider", grupos.Contains("Lider") ? "true" : "false");
                HttpContext.Session.SetString("IsEngenheiro", grupos.Contains("Engenheiro") ? "true" : "false");
                HttpContext.Session.SetString("IsOperador", grupos.Contains("Operador") ? "true" : "false");

                // Log opcional
                try
                {
                    System.IO.File.AppendAllText(@"C:\inetpub\wwwroot\ldap_debug.log",
                        $"{DateTime.Now}: Usuário {nome} {(isAdmin ? "PODE" : "NÃO PODE")} aprovar. Grupos: {string.Join(", ", grupos)}\n");
                }
                catch
                {
                    // Ignora erros de log
                }

                return RedirectToAction("Index", "Home");
            }

            model.MensagemErro = "Usuário ou senha inválidos!";
            return View(model);
        }

    }
}
