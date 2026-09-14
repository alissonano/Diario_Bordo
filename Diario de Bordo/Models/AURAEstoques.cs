using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Diario_de_Bordo.Models

{
    public class AURAEstoques
    {
    }

    [Table("tbl_estoque_saldos")]
    public class EstoqueSaldo
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SubConjunto { get; set; } // O nome do produto/fase (Ex: "4905616T SUBCONJUNTO TOP")

        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantidadeAtual { get; set; } // Saldo físico no estoque

        public DateTime DataUltimaAtualizacao { get; set; } = DateTime.Now;
    }

    [Table("tbl_parametros_mrp")]
    public class ParametroMrp
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string SubConjunto { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ConsumoMedioDiario { get; set; } // Quantas peças a fábrica consome por dia em média

        public int LeadTimeDias { get; set; } // Opcional: Tempo que leva para fabricar/comprar

        [Column(TypeName = "decimal(18,4)")]
        public decimal EstoqueSeguranca { get; set; } // Opcional: Estoque mínimo exigido
    }
}
