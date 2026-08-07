using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JalcruzFirstClass.Api.Migrations
{
    /// <inheritdoc />
    public partial class AtribucionPorAnuncio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ad_id",
                table: "campaigns",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_ad_id",
                table: "campaigns",
                column: "ad_id",
                unique: true,
                filter: "ad_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_campaigns_ad_id",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "ad_id",
                table: "campaigns");
        }
    }
}
