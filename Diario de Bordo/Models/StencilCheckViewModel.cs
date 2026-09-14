using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Diario_de_Bordo.Data;

namespace Diario_de_Bordo.Models
{
    public class StencilCheckViewModel
    {
        // Dados gerais do stencil
        public int StencilId { get; set; }
        public string StencilNumber { get; set; }
        public List<Stencil_Check> ChecksHistory { get; set; }

        public bool IsLider { get; set; }
        public bool IsEngenheiro { get; set; }
        public bool IsOperador { get; set; }


        // Dados do modal de checagem
        public int CheckId { get; set; }
        public string CheckType { get; set; }
        public string Decision { get; set; }
        public string Comentario { get; set; }
        public List<IFormFile> Files { get; set; }
    }
}
