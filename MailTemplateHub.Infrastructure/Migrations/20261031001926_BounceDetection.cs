using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailTemplateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BounceDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bounce_code",
                table: "email_send_recipients",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bounce_reason",
                table: "email_send_recipients",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "bounced_at",
                table: "email_send_recipients",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "processed_inbox_notices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connected_email_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_inbox_notices", x => x.id);
                    table.ForeignKey(
                        name: "fk_processed_inbox_notices_connected_email_accounts_connected_",
                        column: x => x.connected_email_account_id,
                        principalTable: "connected_email_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_processed_inbox_notices_connected_email_account_id_provider",
                table: "processed_inbox_notices",
                columns: new[] { "connected_email_account_id", "provider_message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_processed_inbox_notices_processed_at",
                table: "processed_inbox_notices",
                column: "processed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_inbox_notices");

            migrationBuilder.DropColumn(
                name: "bounce_code",
                table: "email_send_recipients");

            migrationBuilder.DropColumn(
                name: "bounce_reason",
                table: "email_send_recipients");

            migrationBuilder.DropColumn(
                name: "bounced_at",
                table: "email_send_recipients");
        }
    }
}
