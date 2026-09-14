using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diario_de_Bordo.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStencilCheckColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tbl_stencilcheck_tbl_stencil_Stencilid",
                table: "tbl_stencilcheck");

            migrationBuilder.DropIndex(
                name: "IX_tbl_stencilcheck_Stencilid",
                table: "tbl_stencilcheck");

            migrationBuilder.DropColumn(
                name: "Stencilid",
                table: "tbl_stencilcheck");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "tbl_stencilcheck",
                newName: "check2");

            migrationBuilder.AddColumn<string>(
                name: "check1",
                table: "tbl_stencilcheck",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "check21",
                table: "tbl_stencilcheck",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "check1",
                table: "tbl_stencilcheck");

            migrationBuilder.DropColumn(
                name: "check21",
                table: "tbl_stencilcheck");

            migrationBuilder.RenameColumn(
                name: "check2",
                table: "tbl_stencilcheck",
                newName: "status");

            migrationBuilder.AddColumn<int>(
                name: "Stencilid",
                table: "tbl_stencilcheck",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_stencilcheck_Stencilid",
                table: "tbl_stencilcheck",
                column: "Stencilid");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_stencilcheck_tbl_stencil_Stencilid",
                table: "tbl_stencilcheck",
                column: "Stencilid",
                principalTable: "tbl_stencil",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
