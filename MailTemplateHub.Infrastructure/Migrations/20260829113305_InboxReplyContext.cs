using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailTemplateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InboxReplyContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reply_to_internet_message_id",
                table: "email_send_jobs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reply_to_provider_message_id",
                table: "email_send_jobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reply_to_references",
                table: "email_send_jobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reply_to_thread_id",
                table: "email_send_jobs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reply_to_internet_message_id",
                table: "email_send_jobs");

            migrationBuilder.DropColumn(
                name: "reply_to_provider_message_id",
                table: "email_send_jobs");

            migrationBuilder.DropColumn(
                name: "reply_to_references",
                table: "email_send_jobs");

            migrationBuilder.DropColumn(
                name: "reply_to_thread_id",
                table: "email_send_jobs");
        }
    }
}
