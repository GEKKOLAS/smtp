using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailTemplateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BrandKits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brand_kits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    heading_font = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    body_font = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    logo_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    voice = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    button = table.Column<string>(type: "jsonb", nullable: false),
                    colors = table.Column<string>(type: "jsonb", nullable: false),
                    radius = table.Column<string>(type: "jsonb", nullable: false),
                    spacing = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brand_kits", x => x.id);
                    table.ForeignKey(
                        name: "fk_brand_kits_assets_logo_asset_id",
                        column: x => x.logo_asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_brand_kits_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_brand_kits_logo_asset_id",
                table: "brand_kits",
                column: "logo_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_brand_kits_user_id",
                table: "brand_kits",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_brand_kits_user_default",
                table: "brand_kits",
                column: "user_id",
                unique: true,
                filter: "is_default");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brand_kits");
        }
    }
}
