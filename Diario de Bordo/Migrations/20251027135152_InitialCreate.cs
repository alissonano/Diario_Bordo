using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Diario_de_Bordo.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_cq",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date_field = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Cliente = table.Column<string>(type: "text", nullable: false),
                    Modelo = table.Column<string>(type: "text", nullable: false),
                    Programa = table.Column<string>(type: "text", nullable: false),
                    Fase = table.Column<string>(type: "text", nullable: false),
                    OP = table.Column<string>(type: "text", nullable: false),
                    Turno = table.Column<string>(type: "text", nullable: false),
                    Responsavel = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: true),
                    ComentarioGeral = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_cq", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_partrequest",
                columns: table => new
                {
                    request_id = table.Column<string>(type: "text", nullable: false),
                    partnumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    qty = table.Column<int>(type: "integer", nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_partrequest", x => x.request_id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_report",
                columns: table => new
                {
                    report_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date_field = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    linha = table.Column<string>(type: "text", nullable: false),
                    maquina = table.Column<string>(type: "text", nullable: false),
                    classificacao = table.Column<string>(type: "text", nullable: false),
                    inicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fim = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    technician = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_report", x => x.report_id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_stencil",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    num_stencil = table.Column<string>(type: "text", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    cod_fabricante = table.Column<string>(type: "text", nullable: false),
                    fabricante = table.Column<string>(type: "text", nullable: false),
                    data_fabricacao = table.Column<string>(type: "text", nullable: false),
                    cliente = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_stencil", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_cq_evidencia",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cq_id = table.Column<int>(type: "integer", nullable: false),
                    posto = table.Column<string>(type: "text", nullable: false),
                    item = table.Column<string>(type: "text", nullable: false),
                    resultado = table.Column<string>(type: "text", nullable: false),
                    evidencia = table.Column<string>(type: "text", nullable: false),
                    comentario = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_cq_evidencia", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_cq_evidencia_tbl_cq_cq_id",
                        column: x => x.cq_id,
                        principalTable: "tbl_cq",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_stencilcheck",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_stencil = table.Column<int>(type: "integer", nullable: false),
                    Stencilid = table.Column<int>(type: "integer", nullable: false),
                    check_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    comentario = table.Column<string>(type: "text", nullable: false),
                    evidence_unc = table.Column<string>(type: "text", nullable: false),
                    user = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_stencilcheck", x => x.id);
                    table.ForeignKey(
                        name: "FK_tbl_stencilcheck_tbl_stencil_Stencilid",
                        column: x => x.Stencilid,
                        principalTable: "tbl_stencil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tbl_stencilcheck_tbl_stencil_id_stencil",
                        column: x => x.id_stencil,
                        principalTable: "tbl_stencil",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_cq_evidencia_cq_id",
                table: "tbl_cq_evidencia",
                column: "cq_id");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_stencilcheck_id_stencil",
                table: "tbl_stencilcheck",
                column: "id_stencil");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_stencilcheck_Stencilid",
                table: "tbl_stencilcheck",
                column: "Stencilid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tbl_cq_evidencia");

            migrationBuilder.DropTable(
                name: "tbl_partrequest");

            migrationBuilder.DropTable(
                name: "tbl_report");

            migrationBuilder.DropTable(
                name: "tbl_stencilcheck");

            migrationBuilder.DropTable(
                name: "tbl_cq");

            migrationBuilder.DropTable(
                name: "tbl_stencil");
        }
    }
}
