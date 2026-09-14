using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diario_de_Bordo.Models.Estoque
{
    public class ItemEstoque
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "codigoean")]
        public string? codigoean { get; set; } // Adicione este campo

        [Required]
        [Display(Name = "Part Number / Código")]
        public string PartNumber { get; set; }

        [Required]
        [Display(Name = "Descrição do Item")]
        public string Descricao { get; set; }

        [Required]
        [Display(Name = "Tipo de Equipamento")]
        public TipoEquipamento Tipo { get; set; }

        [Display(Name = "Fabricante (Máquina)")]
        public string FabricanteEquipamento { get; set; } // Ex: ASM, Fuji, Koh Young

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal QuantidadeAtual { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal EstoqueMinimo { get; set; }

        [Required]
        public UnidadeMedida Unidade { get; set; }

        [Display(Name = "Localização (Prateleira/Gaveta)")]
        public string Localizacao { get; set; }

        [Display(Name = "Caminho da Foto (UNC)")]
        public string? FotoPath { get; set; }

        [Column("valorunitario")]
        [Display(Name = "Valor Unitário")]
        public decimal? ValorUnitario { get; set; }

        // --- RELACIONAMENTO COM FORNECEDOR ---
        [Required(ErrorMessage = "Selecione um fornecedor")]
        public int FornecedorId { get; set; }

        [ForeignKey("FornecedorId")]
        public virtual Fornecedor? Fornecedor { get; set; } // O OBJETO deve ser anulável para o Form

        // --- PROPRIEDADE CALCULADA (Não vai para o banco) ---
        [NotMapped]
        public string StatusEstoque
        {
            get
            {
                if (QuantidadeAtual <= 0) return "FALTA";
                if (QuantidadeAtual <= EstoqueMinimo) return "BAIXO";
                return "OK";
            }
        }
    }
}