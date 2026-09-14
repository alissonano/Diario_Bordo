using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diario_de_Bordo.Models.Estoque
{
    public class Fornecedor
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome fantasia é obrigatório")]
        public string? NomeFantasia { get; set; }

        public string? RazaoSocial { get; set; }

        [Required(ErrorMessage = "CNPJ é necessário para cadastro")]
        public string? Cnpj { get; set; }

        public string? Contato { get; set; }
        public string? Email { get; set; }

        [Column("telefone")]
        public string? Telefone { get; set; }

        [Column("escopodescricao")] // Força o EF a buscar o nome em minúsculo no Postgres
        public string? EscopoDescricao { get; set; }

        public virtual ICollection<ItemEstoque> Itens { get; set; } = new List<ItemEstoque>();
    }
}