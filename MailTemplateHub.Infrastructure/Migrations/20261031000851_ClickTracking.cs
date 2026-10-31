using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailTemplateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClickTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "track_clicks",
                table: "email_send_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "utm_campaign",
                table: "email_send_jobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "tracked_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    send_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    target_url = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tracked_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_tracked_links_email_send_jobs_send_job_id",
                        column: x => x.send_job_id,
                        principalTable: "email_send_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tracked_links_email_send_recipients_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "email_send_recipients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "link_clicks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tracked_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    classification = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_link_clicks", x => x.id);
                    table.ForeignKey(
                        name: "fk_link_clicks_tracked_links_tracked_link_id",
                        column: x => x.tracked_link_id,
                        principalTable: "tracked_links",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_link_clicks_occurred_at",
                table: "link_clicks",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_link_clicks_tracked_link_id",
                table: "link_clicks",
                column: "tracked_link_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracked_links_recipient_id",
                table: "tracked_links",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracked_links_send_job_id",
                table: "tracked_links",
                column: "send_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_tracked_links_token",
                table: "tracked_links",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "link_clicks");

            migrationBuilder.DropTable(
                name: "tracked_links");

            migrationBuilder.DropColumn(
                name: "track_clicks",
                table: "email_send_jobs");

            migrationBuilder.DropColumn(
                name: "utm_campaign",
                table: "email_send_jobs");
        }
    }
}
