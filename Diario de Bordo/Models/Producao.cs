using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Diario_de_Bordo.Models
{
    [Table("tbl_producaolog")]
    public class ProducaoLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Column("linha")]
        public string Linha { get; set; }

        [Column("maquina")]
        public string Maquina { get; set; }

        [Column("produto")]
        public string Produto { get; set; }

        [Column("side")]
        public string Lado { get; set; } = "--";

        [Column("quantidade")]
        public int Quantidade { get; set; }

        [Column("status")]
        public string Status { get; set; }

    }

    [Table("tbl_metasproducao")]
    public class ProdutoMeta
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("produto")]
        public string Produto { get; set; }

        [Column("linha")]
        public string Linha { get; set; }

        [Column("side")]

        public string Lado { get; set; } = "--";

        [Column("meta_hora")]
        public int MetaHora { get; set; }

        [Column("panelizacao")] // Nova coluna
        public int Panelizacao { get; set; } = 1; // Padrão 1 se não informado
    }

    [Table("tbl_paradas_log")]
    public class ParadaLog
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("inicio_parada")]
        public DateTime ? InicioParada { get; set; }

        [Column("fim_parada")]
        public DateTime ? FimParada { get; set; }

        [Column("tempo_total_segundos")]
        public int TempoTotalSegundos { get; set; }

        [Column("takt_alvo_momento")]
        public double TaktAlvoMomento { get; set; }

        [Column("linha")]
        public string Linha { get; set; }

        [Column("produto")]
        public string Produto { get; set; }

        [Column("lado")]
        public string Lado { get; set; }

        [Column("usuario")]
        public string Usuario { get; set; } = "op@ad.gbrsmtserver.local";

        [Column("status")]
        public string Status { get; set; }

        [Column("motivo")]
        public string Motivo { get; set; }

        // Se você não usa essa coluna no C#, pode ignorar ou mapear se quiser
        [Column("timestamp_registro")]
        public DateTime? TimestampRegistro { get; set; }

        [Column("ultima_quantidade")]
        public int UltimaQuantidade { get; set; } // Qtd de placas no momento da parada
    }

    [Table("tbl_motivos_parada")]
    public class MotivosParada
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("descricao")]
        public string Descricao { get; set; }

        [Column("categoria")]
        public string Categoria { get; set; }

        [Column("ativo")]
        public bool Ativo { get; set; }
    }
    public class MotivoParada
    {
        public int id { get; set; }
        public string descricao { get; set; }
        public string categoria { get; set; }
        public bool ativo { get; set; } = true;

    }

    [Table("tbl_configuracao_linhas")]
    public class ConfiguracaoLinha
    {
        [Key]
        [Column("linha")]
        public string? Linha { get; set; }

        [Column("taxa_hora")]
        public decimal? TaxaHora { get; set; }

        [Column("tolerancia_takt")]
        public int? ToleranciaTakt { get; set; }

        [Column("meta_oee")]
        public decimal? MetaOee { get; set; }

        [Column("turno1_ativo")]
        public bool Turno1Ativo { get; set; }

        [Column("t1_inicio")]
        public TimeSpan? T1Inicio { get; set; }

        [Column("t1_fim")]
        public TimeSpan? T1Fim { get; set; }

        [Column("turno2_ativo")]
        public bool Turno2Ativo { get; set; }

        [Column("t2_inicio")]
        public TimeSpan? T2Inicio { get; set; }

        [Column("t2_fim")]
        public TimeSpan? T2Fim { get; set; }

        [Column("turno3_ativo")]
        public bool Turno3Ativo { get; set; }

        [Column("t3_inicio")]
        public TimeSpan? T3Inicio { get; set; }

        [Column("t3_fim")]
        public TimeSpan ? T3Fim { get; set; }

        [Column("ultima_atualizacao")]
        public DateTime ? UltimaAtualizacao { get; set; }
    }

    public class ParadaPlanejada
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Linha { get; set; } // FK com tbl_configuracao_linhas

        [Required]
        public string Descricao { get; set; } // Ex: "Refeição 1T", "Limpeza Nozzles"

        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFim { get; set; }

        public bool Ativo { get; set; } = true;

        // Novos campos para os dias da semana
        public bool Seg { get; set; }
        public bool Ter { get; set; }
        public bool Qua { get; set; }
        public bool Qui { get; set; }
        public bool Sex { get; set; }
        public bool Sab { get; set; }
        public bool Dom { get; set; }
    }

    public class ConfiguracaoLinhaViewModel
    {
        public ConfiguracaoLinha Configuracao { get; set; }
        public List<ParadaPlanejada> ParadasPlanejadas { get; set; }
    }

}