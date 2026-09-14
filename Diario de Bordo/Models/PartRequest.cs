using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diario_de_Bordo.Models
{

    public class PartRequest
    {
        [Key]
        [Column("request_id")]
        public string RequestId { get; set; }

        [Column("partnumber")]
        [Required]
        [StringLength(50)]
        public string PartNumber { get; set; }

        [Column("qty")]
        [Required]
        public int Quantity { get; set; }

        [Column("descricao")]
        [StringLength(500)]
        public string Descricao { get; set; }

    }
}
