using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diario_de_Bordo.Models
{
    public class Report
    {
        [Key] // <- Indica que é a chave primária
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // <- auto increment
        public int report_id { get; set; }

        
        [Display(Name = "Data da Atividade")]
        public DateTime date_field { get; set; }

        [Required]
        [Display(Name = "Linha")]
        public string linha { get; set; }

        [Required]
        [Display(Name = "Máquina")]
        public string maquina { get; set; }

        [Required]
        [Display(Name = "Classificação")]
        public string classificacao { get; set; }

        [Required]
        [Display(Name = "Início")]
        public DateTime inicio { get; set; }

        [Required]
        [Display(Name = "Fim")]
        public DateTime fim { get; set; }

        [Display(Name = "Descrição")]
        public string descricao { get; set; }

        [Required]
        [Display(Name = "Técnico")]
        public string technician { get; set; }
    }

    [Table("tbl_report_evidencias")]
    public class ReportEvidencia
    {
        [Key]
        [Column("id")] // aqui você indica o nome real no banco
        public int evidencia_id { get; set; }

        [Required]
        public int report_id { get; set; }

        [ForeignKey("report_id")]
        public Report Report { get; set; }

        [Required]
        [MaxLength(255)]
        public string file_name { get; set; }

        [Required]
        [MaxLength(500)]
        public string file_path { get; set; }

        public DateTime data_upload { get; set; } = DateTime.UtcNow;
    }
}
