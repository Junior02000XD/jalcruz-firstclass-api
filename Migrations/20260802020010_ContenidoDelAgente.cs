using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JalcruzFirstClass.Api.Migrations
{
    /// <inheritdoc />
    public partial class ContenidoDelAgente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "context_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    restricted_zone_id = table.Column<int>(type: "integer", nullable: true),
                    conditions_text = table.Column<string>(type: "text", nullable: true),
                    next_action = table.Column<string>(type: "text", nullable: true),
                    handoff_to_user_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_context_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_context_entries_users_handoff_to_user_id",
                        column: x => x.handoff_to_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_context_entries_zones_restricted_zone_id",
                        column: x => x.restricted_zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "text", nullable: false),
                    url_r2 = table.Column<string>(type: "text", nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    transcript = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    phone_number_id = table.Column<string>(type: "text", nullable: false),
                    style_guide = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_personas", x => x.id);
                    table.ForeignKey(
                        name: "fk_personas_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "context_entry_media",
                columns: table => new
                {
                    context_entry_id = table.Column<int>(type: "integer", nullable: false),
                    media_asset_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_context_entry_media", x => new { x.context_entry_id, x.media_asset_id });
                    table.ForeignKey(
                        name: "fk_context_entry_media_context_entries_context_entry_id",
                        column: x => x.context_entry_id,
                        principalTable: "context_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_context_entry_media_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_messages_media_asset_id",
                table: "messages",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_context_entries_active_type",
                table: "context_entries",
                columns: new[] { "active", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_context_entries_handoff_to_user_id",
                table: "context_entries",
                column: "handoff_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_context_entries_restricted_zone_id",
                table: "context_entries",
                column: "restricted_zone_id");

            migrationBuilder.CreateIndex(
                name: "ix_context_entry_media_media_asset_id",
                table: "context_entry_media",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_personas_phone_number_id",
                table: "personas",
                column: "phone_number_id",
                unique: true,
                filter: "active");

            migrationBuilder.CreateIndex(
                name: "ix_personas_user_id",
                table: "personas",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_messages_media_assets_media_asset_id",
                table: "messages",
                column: "media_asset_id",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_messages_media_assets_media_asset_id",
                table: "messages");

            migrationBuilder.DropTable(
                name: "context_entry_media");

            migrationBuilder.DropTable(
                name: "personas");

            migrationBuilder.DropTable(
                name: "context_entries");

            migrationBuilder.DropTable(
                name: "media_assets");

            migrationBuilder.DropIndex(
                name: "ix_messages_media_asset_id",
                table: "messages");
        }
    }
}
