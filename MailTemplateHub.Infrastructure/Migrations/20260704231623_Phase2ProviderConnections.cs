using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailTemplateHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2ProviderConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connected_email_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_account_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email_address = table.Column<string>(type: "citext", nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    tenant_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    granted_scopes = table.Column<List<string>>(type: "text[]", nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    state_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_connected_email_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_connected_email_accounts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oauth_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pkce_verifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    return_to = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oauth_states", x => x.id);
                    table.ForeignKey(
                        name: "fk_oauth_states_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_provider_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connected_email_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    provider_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    retry_after_seconds = table.Column<int>(type: "integer", nullable: true),
                    detail = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_provider_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_provider_events_connected_email_accounts_connected_em",
                        column: x => x.connected_email_account_id,
                        principalTable: "connected_email_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "oauth_tokens",
                columns: table => new
                {
                    connected_email_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_token_ciphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                    access_token_nonce = table.Column<byte[]>(type: "bytea", nullable: false),
                    refresh_token_ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    refresh_token_nonce = table.Column<byte[]>(type: "bytea", nullable: true),
                    wrapped_dek = table.Column<byte[]>(type: "bytea", nullable: false),
                    kek_version = table.Column<int>(type: "integer", nullable: false),
                    access_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    refresh_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    refresh_failure_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_oauth_tokens", x => x.connected_email_account_id);
                    table.ForeignKey(
                        name: "fk_oauth_tokens_connected_email_accounts_connected_email_accou",
                        column: x => x.connected_email_account_id,
                        principalTable: "connected_email_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_connected_email_accounts_user_id_provider_provider_account_",
                table: "connected_email_accounts",
                columns: new[] { "user_id", "provider", "provider_account_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_connected_email_accounts_one_default",
                table: "connected_email_accounts",
                column: "user_id",
                unique: true,
                filter: "is_default AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_email_provider_events_connected_email_account_id_created_at",
                table: "email_provider_events",
                columns: new[] { "connected_email_account_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_oauth_states_expires_at",
                table: "oauth_states",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_states_user_id",
                table: "oauth_states",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_tokens_access_token_expires_at",
                table: "oauth_tokens",
                column: "access_token_expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_provider_events");

            migrationBuilder.DropTable(
                name: "oauth_states");

            migrationBuilder.DropTable(
                name: "oauth_tokens");

            migrationBuilder.DropTable(
                name: "connected_email_accounts");
        }
    }
}
