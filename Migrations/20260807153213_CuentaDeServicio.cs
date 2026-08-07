using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JalcruzFirstClass.Api.Migrations
{
    /// <inheritdoc />
    public partial class CuentaDeServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_service_account",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_service_account",
                table: "users");
        }
    }
}
