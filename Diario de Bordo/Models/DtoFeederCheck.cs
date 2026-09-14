using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diario_de_Bordo.Models
{
    public class DtoFeederCheck

    {
        public string Programa { get; set; } //Programa de Montagem
        public string Partnumber { get; set; } //Que está na etiqueta do rolo
        public string Maquina { get; set; }  // Qual máquina é, para o operador consultar qual feeder é qual, já que a posição mecânica não é fixa. Ex: Feeder 1 pode ser o P.No 1, mas em outra máquina o P.No 1 pode ser o Feeder 3.
        public string Slot { get; set; } //Onde o feeder vai encaixado na máquina, para o operador consultar qual feeder é qual, já que a posição mecânica não é fixa. Ex: Feeder 1 pode ser o P.No 1, mas em outra máquina o P.No 1 pode ser o Feeder 3.
    }

    public class FeederCheck
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("linha")]
        public string Linha { get; set; }

        [Column("programa")]// Ex: SMD1
        public string Programa { get; set; }

        [Column("partnumber")]// O "Nome do Produto"
        public string Partnumber { get; set; }

        [Column("maquina")]
        public string Maquina { get; set; }
        [Column("slot")]
        public string Slot { get; set; }
        [Column("data_criacao")]
        public DateTime DataCriacao { get; set; } = DateTime.Now;
    }

    public class ImportSetupRequest
    {
        public string Linha { get; set; }
        public string Programa { get; set; }
        public List<DtoFeederCheck> Dados { get; set; }
    }

    public class ValidacaoDto
    {
        public string Linha { get; set; }
        public string Programa { get; set; }
        public string Maquina { get; set; }
        public string Slot { get; set; }
        public string CodigoUnico { get; set; }
        public string Partnumber { get; set; }
    }

    [Table("tbl_feeder_validation")]
    public class FeederValidation
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("linha")]
        public string Linha { get; set; }

        [Column("programa")]
        public string Programa { get; set; }

        [Column("maquina")]
        public string Maquina { get; set; }

        [Column("slot")]
        public string Slot { get; set; }

        [Column("codigo_unico")]
        public string CodigoUnico { get; set; }

        [Column("partnumber")]
        public string Partnumber { get; set; }

        [Column("status_check")]
        public string StatusCheck { get; set; }

        [Column("usuario_id")]
        public string UsuarioId { get; set; }

        [Column("data_leitura")]
        public DateTime DataLeitura { get; set; }
    }
}