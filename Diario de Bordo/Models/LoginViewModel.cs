namespace Diario_de_Bordo.Models
{
    public class LoginViewModel
    {

            public string Matricula { get; set; } = string.Empty;
            public string Senha { get; set; } = string.Empty;
            public string? MensagemErro { get; set; }
       
    }
}
