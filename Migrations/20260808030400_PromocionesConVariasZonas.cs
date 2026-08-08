using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JalcruzFirstClass.Api.Migrations
{
    /// <inheritdoc />
    public partial class PromocionesConVariasZonas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ EL ORDEN IMPORTA. El andamiaje de EF pone el DropColumn ARRIBA de
            // todo, y con eso el traspaso se hace sobre una columna que ya no
            // existe: las promociones que tuvieran una zona cargada la perderían
            // en silencio. Por eso acá se crea primero, se copia, y recién al
            // final se borra la columna vieja.

            migrationBuilder.CreateTable(
                name: "context_entry_zones",
                columns: table => new
                {
                    context_entry_id = table.Column<int>(type: "integer", nullable: false),
                    zone_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_context_entry_zones", x => new { x.context_entry_id, x.zone_id });
                    table.ForeignKey(
                        name: "fk_context_entry_zones_context_entries_context_entry_id",
                        column: x => x.context_entry_id,
                        principalTable: "context_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_context_entry_zones_zones_zone_id",
                        column: x => x.zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_context_entry_zones_zone_id",
                table: "context_entry_zones",
                column: "zone_id");

            // Traspaso: cada promoción que estaba restringida a UNA zona pasa a
            // tener esa misma zona en la tabla de unión. Las que no tenían
            // ninguna quedan sin filas, que es exactamente lo que significa
            // "vale para todas" — el mismo comportamiento que tenían.
            migrationBuilder.Sql(@"
                INSERT INTO context_entry_zones (context_entry_id, zone_id)
                SELECT id, restricted_zone_id
                FROM context_entries
                WHERE restricted_zone_id IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "fk_context_entries_zones_restricted_zone_id",
                table: "context_entries");

            migrationBuilder.DropIndex(
                name: "ix_context_entries_restricted_zone_id",
                table: "context_entries");

            migrationBuilder.DropColumn(
                name: "restricted_zone_id",
                table: "context_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volver atrás PIERDE información y no hay forma de evitarlo: la
            // columna vieja aguanta una sola zona. Se conserva la de menor id y
            // el resto se descarta. Está escrito para que quien revierta lo sepa.
            migrationBuilder.AddColumn<int>(
                name: "restricted_zone_id",
                table: "context_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_context_entries_restricted_zone_id",
                table: "context_entries",
                column: "restricted_zone_id");

            migrationBuilder.AddForeignKey(
                name: "fk_context_entries_zones_restricted_zone_id",
                table: "context_entries",
                column: "restricted_zone_id",
                principalTable: "zones",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
                UPDATE context_entries c
                SET restricted_zone_id = (
                    SELECT MIN(z.zone_id) FROM context_entry_zones z
                    WHERE z.context_entry_id = c.id
                );");

            migrationBuilder.DropTable(
                name: "context_entry_zones");
        }
    }
}
