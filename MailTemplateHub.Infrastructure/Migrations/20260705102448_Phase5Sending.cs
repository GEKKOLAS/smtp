using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailTemplateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5Sending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "recipient_id",
                table: "email_provider_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "send_job_id",
                table: "email_provider_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "email_send_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connected_email_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_test = table.Column<bool>(type: "boolean", nullable: false),
                    subject_snapshot = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    variable_values = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    failure_message = table.Column<string>(type: "text", nullable: true),
                    rendered_snapshot_key = table.Column<string>(type: "text", nullable: true),
                    total_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_send_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_send_jobs_connected_email_accounts_connected_email_ac",
                        column: x => x.connected_email_account_id,
                        principalTable: "connected_email_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_email_send_jobs_email_template_versions_template_version_id",
                        column: x => x.template_version_id,
                        principalTable: "email_template_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_send_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    send_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disposition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    filename_override = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_send_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_send_attachments_assets_asset_id",
                        column: x => x.asset_id,
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_email_send_attachments_email_send_jobs_send_job_id",
                        column: x => x.send_job_id,
                        principalTable: "email_send_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_send_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    send_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email_address = table.Column<string>(type: "citext", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    variable_overrides = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "text", nullable: true),
                    provider_thread_id = table.Column<string>(type: "text", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    failure_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_send_recipients", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_send_recipients_email_send_jobs_send_job_id",
                        column: x => x.send_job_id,
                        principalTable: "email_send_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_provider_events_send_job_id_created_at",
                table: "email_provider_events",
                columns: new[] { "send_job_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_email_send_attachments_asset_id",
                table: "email_send_attachments",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ux_send_attachments_unique",
                table: "email_send_attachments",
                columns: new[] { "send_job_id", "asset_id", "disposition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_send_jobs_connected_email_account_id",
                table: "email_send_jobs",
                column: "connected_email_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_send_jobs_template_version_id",
                table: "email_send_jobs",
                column: "template_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_send_jobs_user_id_status_created_at",
                table: "email_send_jobs",
                columns: new[] { "user_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_send_jobs_due",
                table: "email_send_jobs",
                column: "scheduled_at",
                filter: "status = 'Scheduled'");

            migrationBuilder.CreateIndex(
                name: "ux_send_jobs_idempotency",
                table: "email_send_jobs",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_email_send_recipients_email_address",
                table: "email_send_recipients",
                column: "email_address");

            migrationBuilder.CreateIndex(
                name: "ix_email_send_recipients_send_job_id_status",
                table: "email_send_recipients",
                columns: new[] { "send_job_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_send_recipients_job_email",
                table: "email_send_recipients",
                columns: new[] { "send_job_id", "email_address" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_send_attachments");

            migrationBuilder.DropTable(
                name: "email_send_recipients");

            migrationBuilder.DropTable(
                name: "email_send_jobs");

            migrationBuilder.DropIndex(
                name: "ix_email_provider_events_send_job_id_created_at",
                table: "email_provider_events");

            migrationBuilder.DropColumn(
                name: "recipient_id",
                table: "email_provider_events");

            migrationBuilder.DropColumn(
                name: "send_job_id",
                table: "email_provider_events");
        }
    }
}
