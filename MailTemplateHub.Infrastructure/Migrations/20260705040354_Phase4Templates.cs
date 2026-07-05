using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailTemplateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Templates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_template_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    preheader = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    mjml_source = table.Column<string>(type: "text", nullable: true),
                    grapes_project = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    html_body = table.Column<string>(type: "text", nullable: false),
                    text_body = table.Column<string>(type: "text", nullable: true),
                    variables_schema = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    editor_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_template_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_templates", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_templates_email_template_versions_current_version_id",
                        column: x => x.current_version_id,
                        principalTable: "email_template_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_email_templates_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "template_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_assets", x => x.id);
                    table.ForeignKey(
                        name: "fk_template_assets_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_template_assets_email_template_versions_template_version_id",
                        column: x => x.template_version_id,
                        principalTable: "email_template_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_template_versions_number",
                table: "email_template_versions",
                columns: new[] { "template_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_templates_current_version_id",
                table: "email_templates",
                column: "current_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_templates_user_id_is_archived",
                table: "email_templates",
                columns: new[] { "user_id", "is_archived" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_email_templates_user_name",
                table: "email_templates",
                columns: new[] { "user_id", "name" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_template_assets_asset_id",
                table: "template_assets",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ux_template_assets_unique",
                table: "template_assets",
                columns: new[] { "template_version_id", "asset_id", "usage" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_email_template_versions_email_templates_template_id",
                table: "email_template_versions",
                column: "template_id",
                principalTable: "email_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_template_versions_email_templates_template_id",
                table: "email_template_versions");

            migrationBuilder.DropTable(
                name: "template_assets");

            migrationBuilder.DropTable(
                name: "email_templates");

            migrationBuilder.DropTable(
                name: "email_template_versions");
        }
    }
}
