using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "After",
                table: "change_log",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Before",
                table: "change_log",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Entity",
                table: "change_log",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "change_log",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Earlier entries: the kind of thing comes from the action, and tally edits kept before/after in their detail.
            migrationBuilder.Sql("""
                UPDATE change_log SET "Entity" = split_part("Action", '.', 1);
                UPDATE change_log SET "Before" = "Detail"->'before', "After" = "Detail"->'after', "Detail" = NULL
                    WHERE "Action" = 'tally.update' AND "Detail" ? 'before';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_change_log_AccountId_At",
                table: "change_log",
                columns: new[] { "AccountId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_change_log_Entity_At",
                table: "change_log",
                columns: new[] { "Entity", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_change_log_PersonId_At",
                table: "change_log",
                columns: new[] { "PersonId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_change_log_AccountId_At",
                table: "change_log");

            migrationBuilder.DropIndex(
                name: "IX_change_log_Entity_At",
                table: "change_log");

            migrationBuilder.DropIndex(
                name: "IX_change_log_PersonId_At",
                table: "change_log");

            migrationBuilder.DropColumn(
                name: "After",
                table: "change_log");

            migrationBuilder.DropColumn(
                name: "Before",
                table: "change_log");

            migrationBuilder.DropColumn(
                name: "Entity",
                table: "change_log");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "change_log");
        }
    }
}
