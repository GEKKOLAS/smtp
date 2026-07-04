using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailTemplateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Assets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    public_url = table.Column<string>(type: "text", nullable: true),
                    access = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<byte[]>(type: "bytea", nullable: true),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    upload_state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_assets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assets_pending",
                table: "assets",
                columns: new[] { "upload_state", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_assets_storage_key",
                table: "assets",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assets_user_id_kind",
                table: "assets",
                columns: new[] { "user_id", "kind" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_assets_user_checksum",
                table: "assets",
                columns: new[] { "user_id", "checksum_sha256" },
                unique: true,
                filter: "deleted_at IS NULL AND upload_state = 'Ready'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assets");
        }
    }
}
