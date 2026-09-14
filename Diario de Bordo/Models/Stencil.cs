using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diario_de_Bordo.Models
{
    [Table("tbl_stencil")]
    public class Stencil
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Display(Name = "Número do Stencil")]
        [Required]
        public string? num_stencil { get; set; }

        [Display(Name = "Descrição")]
        public string? descricao { get; set; }

        [Display(Name = "Código do Fabricante")]
        public string? cod_fabricante { get; set; }

        [Display(Name = "Fabricante")]
        public string? fabricante { get; set; }

        [Display(Name = "Data de Fabricação")]
        public string? data_fabricacao { get; set; }

        [Display(Name = "Cliente")]
        public string? cliente { get; set; }

        public ICollection<Stencil_Check> Stencil_Checks { get; set; } = new List<Stencil_Check>();
    }

    [Table("tbl_stencilcheck")]
    public class Stencil_Check
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        [Column("id_stencil")]
        [ForeignKey(nameof(Stencil))]
        public int id_stencil { get; set; }

        [Display(Name = "Data da Verificação")]
        public DateTime check_date { get; set; } = DateTime.UtcNow;

        [Display(Name = "Comentário")]
        public string? comentario { get; set; }

        [Display(Name = "Evidência (UNC)")]
        public string? evidence_unc { get; set; }

        [Display(Name = "Usuário")]
        public string? user { get; set; }

        [Display(Name = "Status")]
        public string? status { get; set; }

        // Novas colunas para aprovações
        public string? check1 { get; set; }
        public string? check2 { get; set; }

        public string? log { get; set; }  
    }
}
