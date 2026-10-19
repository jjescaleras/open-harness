using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace OpenHarness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialHarnessSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "harness");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "users",
                schema: "harness",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "harness",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "harness",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_policies",
                schema: "harness",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    root_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    sandbox_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    approval_policy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rules_config = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_policies_users_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "harness",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "agent_sessions",
                schema: "harness",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    total_input_tokens = table.Column<long>(type: "bigint", nullable: false),
                    total_output_tokens = table.Column<long>(type: "bigint", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    model_config = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_sessions_agent_sessions_parent_session_id",
                        column: x => x.parent_session_id,
                        principalSchema: "harness",
                        principalTable: "agent_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_agent_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "harness",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_agent_sessions_workspace_policies_workspace_policy_id",
                        column: x => x.workspace_policy_id,
                        principalSchema: "harness",
                        principalTable: "workspace_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversation_turns",
                schema: "harness",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    thinking_content = table.Column<string>(type: "text", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    token_usage = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_turns", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversation_turns_agent_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "harness",
                        principalTable: "agent_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tool_executions",
                schema: "harness",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_id = table.Column<Guid>(type: "uuid", nullable: false),
                    call_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    tool_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    arguments = table.Column<string>(type: "jsonb", nullable: false),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tool_executions", x => x.id);
                    table.ForeignKey(
                        name: "fk_tool_executions_agent_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "harness",
                        principalTable: "agent_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tool_executions_conversation_turns_turn_id",
                        column: x => x.turn_id,
                        principalSchema: "harness",
                        principalTable: "conversation_turns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tool_executions_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalSchema: "harness",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_sessions_parent_session_id",
                schema: "harness",
                table: "agent_sessions",
                column: "parent_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_sessions_user_id",
                schema: "harness",
                table: "agent_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_sessions_workspace_policy_id",
                schema: "harness",
                table: "agent_sessions",
                column: "workspace_policy_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_turns_embedding",
                schema: "harness",
                table: "conversation_turns",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_turns_session_id_sequence_number",
                schema: "harness",
                table: "conversation_turns",
                columns: new[] { "session_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                schema: "harness",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "harness",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                schema: "harness",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_executions_approved_by_user_id",
                schema: "harness",
                table: "tool_executions",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_executions_arguments",
                schema: "harness",
                table: "tool_executions",
                column: "arguments")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_tool_executions_result",
                schema: "harness",
                table: "tool_executions",
                column: "result")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_tool_executions_session_id",
                schema: "harness",
                table: "tool_executions",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_tool_executions_turn_id",
                schema: "harness",
                table: "tool_executions",
                column: "turn_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "harness",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_policies_owner_id",
                schema: "harness",
                table: "workspace_policies",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "harness");

            migrationBuilder.DropTable(
                name: "tool_executions",
                schema: "harness");

            migrationBuilder.DropTable(
                name: "conversation_turns",
                schema: "harness");

            migrationBuilder.DropTable(
                name: "agent_sessions",
                schema: "harness");

            migrationBuilder.DropTable(
                name: "workspace_policies",
                schema: "harness");

            migrationBuilder.DropTable(
                name: "users",
                schema: "harness");
        }
    }
}
