using Diario_de_Bordo.Models.Estoque;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Diario_de_Bordo.Models; // Para enxergar o ItemEstoque

namespace Diario_de_Bordo.Models // <--- ADICIONE ISSO
{

    [Table("tbl_solicitacoes_pecas")]
    public class SolicitacaoPeca
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("reportid")]
        public int? ReportId { get; set; }

        [Column("itemestoqueid")]
        public int? ItemEstoqueId { get; set; }

        // Propriedade de navegação para a sua tabela existente
        [ForeignKey("ItemEstoqueId")]
        public virtual ItemEstoque? ItemEstoque { get; set; }

        [Column("descricaoavulsa")]
        public string? DescricaoAvulsa { get; set; }

        [Column("fotopathavulsa")]
        public string? FotoPathAvulsa { get; set; }

        [Column("quantidade")]
        public int Quantidade { get; set; }

        [Column("datasolicitacao")]
        public DateTime DataSolicitacao { get; set; } = DateTime.Now;

        [Column("status")]
        public int Status { get; set; } = 0;

        [Column("tecnicoresponsavel")]
        public string? TecnicoResponsavel { get; set; }

        [Column("observacaogestor")]
        public string? ObservacaoGestor { get; set; }
    }
}