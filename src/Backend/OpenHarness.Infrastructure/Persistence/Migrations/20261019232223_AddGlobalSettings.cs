using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenHarness.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "global_settings",
                schema: "harness",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    general = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_global_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_global_settings_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "harness",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_global_settings_scope",
                schema: "harness",
                table: "global_settings",
                column: "scope",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_global_settings_updated_by_user_id",
                schema: "harness",
                table: "global_settings",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "global_settings",
                schema: "harness");
        }
    }
}
