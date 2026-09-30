using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AccountsAndTally : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep history entered as a single total: whatever isn't weekend/PH counts as weekday.
            migrationBuilder.Sql("""
                UPDATE people SET "OpeningWeekday" = "OpeningTotal" - COALESCE("OpeningWeekendHoliday", 0)
                WHERE "OpeningWeekday" IS NULL AND "OpeningTotal" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "OpeningTotal",
                table: "people");

            migrationBuilder.RenameColumn(
                name: "OpeningWeekendHoliday",
                table: "people",
                newName: "TallyWeekendHolidayAdjust");

            migrationBuilder.RenameColumn(
                name: "OpeningWeekday",
                table: "people",
                newName: "TallyWeekdayAdjust");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "change_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    TempPasswordExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResetTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResetTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SecurityStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Email",
                table: "accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_PersonId",
                table: "accounts",
                column: "PersonId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "change_log");

            migrationBuilder.RenameColumn(
                name: "TallyWeekendHolidayAdjust",
                table: "people",
                newName: "OpeningWeekendHoliday");

            migrationBuilder.RenameColumn(
                name: "TallyWeekdayAdjust",
                table: "people",
                newName: "OpeningWeekday");

            migrationBuilder.AddColumn<int>(
                name: "OpeningTotal",
                table: "people",
                type: "integer",
                nullable: true);
        }
    }
}
