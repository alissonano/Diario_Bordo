using Diario_de_Bordo.Models.Estoque;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diario_de_Bordo.Models
{
    public class EstoqueViewModel
    {
        public IEnumerable<ItemEstoque> Itens { get; set; } = new List<ItemEstoque>();
        public string? Filtro { get; set; }
        public bool? ApenasBaixos { get; set; }
        public IEnumerable<SolicitacaoPeca> SolicitacoesPendentes { get; set; }
    }

    public class RequisicaoViewModel
    {
        // Use o nome completo aqui para evitar o erro CS0029
        public List<Diario_de_Bordo.Models.SolicitacaoPeca> Pendentes { get; set; }
        public List<Diario_de_Bordo.Models.SolicitacaoPeca> Historico { get; set; }
    }

    public class RequisicaoPeca
    {
        [Key]
        public int id { get; set; }

        public int report_id { get; set; } // Chave Estrangeira para o Report

        public string tecnico { get; set; }

        public DateTime data_solicitacao { get; set; }

        public string descricao_peca { get; set; }

        public int quantidade { get; set; }

        public string tipo { get; set; } // ESTOQUE ou AVULSO

        public string status { get; set; } // PENDENTE, APROVADO, NEGADO
    }


}