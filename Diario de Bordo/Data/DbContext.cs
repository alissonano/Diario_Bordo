using Diario_de_Bordo.Models;
using Diario_de_Bordo.Models.Estoque;
using Microsoft.EntityFrameworkCore;

namespace Diario_de_Bordo.Data
{
    public class DiarioContext : DbContext
    {
        public DiarioContext(DbContextOptions<DiarioContext> options) : base(options) { }

        public DbSet<Report> tbl_report { get; set; }
        public DbSet<ReportEvidencia> tbl_report_evidencias { get; set; }
        public DbSet<PartRequest> tbl_partrequest { get; set; }
        public DbSet<CQ> tbl_cq { get; set; }
        public DbSet<CQEvidencia> tbl_cq_evidencia { get; set; }
        public DbSet<Stencil> tbl_stencil { get; set; }
        public DbSet<Stencil_Check> tbl_stencilcheck { get; set; }
        // Use nomes padronizados para facilitar a chamada no Controller
        public DbSet<ItemEstoque> tbl_itens_estoque { get; set; }
        public DbSet<Fornecedor> tbl_fornecedores { get; set; } // Verifique se não está tbl_itens_fornecedores

        public DbSet<Diario_de_Bordo.Models.SolicitacaoPeca> tbl_solicitacoes_pecas { get; set; }

        public DbSet<ProducaoLog> tbl_producaolog { get; set; }
        public DbSet<ProdutoMeta> tbl_metasproducao { get; set; }

        public DbSet<ParadaLog> tbl_paradas_log { get; set; }
        public DbSet<MotivoParada> tbl_motivos_parada { get; set; }

        public DbSet<ConfiguracaoLinha> tbl_configuracao_linhas { get; set; }
        public DbSet<ParadaPlanejada> tbl_paradas_planejadas { get; set; }
        public DbSet<FeederCheck> tbl_feedercheck { get; set; }
        public DbSet<FeederValidation> tbl_feeder_validation { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConfiguracaoLinha>(entity =>
            {
                entity.ToTable("tbl_configuracao_linhas");
                entity.HasKey(e => e.Linha);
                entity.Property(e => e.TaxaHora).HasColumnName("taxa_hora");
                entity.Property(e => e.ToleranciaTakt).HasColumnName("tolerancia_takt");
                entity.Property(e => e.MetaOee).HasColumnName("meta_oee");

                // Turnos
                entity.Property(e => e.Turno1Ativo).HasColumnName("turno1_ativo");
                entity.Property(e => e.T1Inicio).HasColumnName("t1_inicio");
                entity.Property(e => e.T1Fim).HasColumnName("t1_fim");

                entity.Property(e => e.Turno2Ativo).HasColumnName("turno2_ativo");
                entity.Property(e => e.T2Inicio).HasColumnName("t2_inicio");
                entity.Property(e => e.T2Fim).HasColumnName("t2_fim");

                entity.Property(e => e.Turno3Ativo).HasColumnName("turno3_ativo");
                entity.Property(e => e.T3Inicio).HasColumnName("t3_inicio");
                entity.Property(e => e.T3Fim).HasColumnName("t3_fim");
            });

            modelBuilder.Entity<ParadaPlanejada>(entity =>
            {
                entity.ToTable("tbl_paradas_planejadas");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Linha).HasColumnName("linha");
                entity.Property(e => e.Descricao).HasColumnName("descricao");
                entity.Property(e => e.HoraInicio).HasColumnName("hora_inicio");
                entity.Property(e => e.HoraFim).HasColumnName("hora_fim");
                entity.Property(e => e.Ativo).HasColumnName("ativo");
            });
            modelBuilder.Entity<SolicitacaoPeca>(entity =>
            {
                entity.ToTable("tbl_solicitacoes_pecas");
                entity.Property(e => e.ItemEstoqueId).HasColumnName("itemestoqueid");
                entity.Property(e => e.ReportId).HasColumnName("reportid");
                entity.Property(e => e.Status).HasColumnName("status");
            });

            // Configuração ItemEstoque
            modelBuilder.Entity<ItemEstoque>(entity =>
            {
                entity.ToTable("tbl_itens_estoque");

                // Converte Enums para String no banco de dados
                entity.Property(e => e.Tipo).HasConversion<string>();
                entity.Property(e => e.Unidade).HasConversion<string>();

                // Garante precisão decimal para balanças ou frações
                entity.Property(e => e.QuantidadeAtual).HasPrecision(18, 2);
                entity.Property(e => e.EstoqueMinimo).HasPrecision(18, 2);
            });

            // Configuração Fornecedor
            modelBuilder.Entity<Fornecedor>(entity =>
            {
                entity.ToTable("tbl_fornecedores");
            });

            // CQ
            modelBuilder.Entity<CQ>(entity =>
            {
                entity.ToTable("tbl_cq");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Date_field).HasColumnName("date_field");
                entity.Property(e => e.Cliente).HasColumnName("cliente");
                entity.Property(e => e.Modelo).HasColumnName("modelo");
                entity.Property(e => e.Programa).HasColumnName("programa");
                entity.Property(e => e.Fase).HasColumnName("fase");
                entity.Property(e => e.OP).HasColumnName("op");
                entity.Property(e => e.Turno).HasColumnName("turno");
                entity.Property(e => e.Responsavel).HasColumnName("responsavel");
                entity.Property(e => e.Status).HasColumnName("status");
                entity.Property(e => e.ComentarioGeral).HasColumnName("comentario_geral");


                entity.HasMany(e => e.Evidencias)
                      .WithOne(e => e.CQ)
                      .HasForeignKey(e => e.cq_id)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // CQEvidencia
            modelBuilder.Entity<CQEvidencia>(entity =>
            {
                entity.ToTable("tbl_cq_evidencia");
                entity.HasKey(e => e.id);

                entity.Property(e => e.cq_id).HasColumnName("cq_id");
                entity.Property(e => e.posto).HasColumnName("posto");
                entity.Property(e => e.evidencia).HasColumnName("evidencia");
            });

            // -----------------------------
            // Configuração da tabela Stencil
            // -----------------------------
            modelBuilder.Entity<Stencil>(entity =>
            {
                entity.ToTable("tbl_stencil");
                entity.HasKey(e => e.id);

                entity.Property(e => e.num_stencil).HasColumnName("num_stencil");
                entity.Property(e => e.descricao).HasColumnName("descricao");
                entity.Property(e => e.cod_fabricante).HasColumnName("cod_fabricante");
                entity.Property(e => e.fabricante).HasColumnName("fabricante");
                entity.Property(e => e.data_fabricacao).HasColumnName("data_fabricacao");
                entity.Property(e => e.cliente).HasColumnName("cliente");





                entity.Property(e => e.num_stencil).IsRequired(false);
                entity.Property(e => e.descricao).IsRequired(false);
                entity.Property(e => e.cod_fabricante).IsRequired(false);
                entity.Property(e => e.fabricante).IsRequired(false);
                entity.Property(e => e.data_fabricacao).IsRequired(false);
                entity.Property(e => e.cliente).IsRequired(false);

                entity.HasMany(e => e.Stencil_Checks)
                      .WithOne()
                      .HasForeignKey(c => c.id_stencil)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // -----------------------------
            // Configuração da tabela Stencil_Check
            // -----------------------------
            modelBuilder.Entity<Stencil_Check>(entity =>
            {
                entity.ToTable("tbl_stencilcheck");
                entity.HasKey(e => e.id);

                entity.Property(e => e.id_stencil).HasColumnName("id_stencil");
                entity.Property(e => e.check_date).HasColumnName("check_date");
                entity.Property(e => e.comentario).HasColumnName("comentario");
                entity.Property(e => e.evidence_unc).HasColumnName("evidence_unc");
                entity.Property(e => e.user).HasColumnName("user");
                entity.Property(e => e.status).HasColumnName("status");
                entity.Property(e => e.check1).HasColumnName("check1");
                entity.Property(e => e.check2).HasColumnName("check2");
                entity.Property(e => e.log).HasColumnName("log");
            });

            // Configuração Produção Log
            modelBuilder.Entity<ProducaoLog>(entity =>
            {
                entity.ToTable("tbl_producaolog");
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Timestamp).HasColumnName("timestamp");
                entity.Property(e => e.Linha).HasColumnName("linha");
                entity.Property(e => e.Maquina).HasColumnName("maquina");
                entity.Property(e => e.Produto).HasColumnName("produto");
                entity.Property(e => e.Quantidade).HasColumnName("quantidade");
                entity.Property(e => e.Status).HasColumnName("status");
            });

            // Configuração Produto Meta
            modelBuilder.Entity<ProdutoMeta>(entity =>
            {
                entity.ToTable("tbl_metasproducao"); // Mapeado conforme seu DbSet
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Produto).HasColumnName("produto");
                entity.Property(e => e.Linha).HasColumnName("linha");
                entity.Property(e => e.MetaHora).HasColumnName("meta_hora");

                // UNIQUE composto para evitar duplicidade de meta para o mesmo produto na mesma linha
                entity.HasIndex(e => new { e.Produto, e.Linha }).IsUnique();
            });
        }
    }
}