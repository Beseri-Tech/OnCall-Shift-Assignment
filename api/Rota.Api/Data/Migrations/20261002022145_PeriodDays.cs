using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PeriodDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "period_days",
                columns: table => new
                {
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period_days", x => new { x.PeriodId, x.Kind, x.Date });
                    table.ForeignKey(
                        name: "FK_period_days_rota_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "rota_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Existing periods keep the days they had: the master lists within each period's dates.
            migrationBuilder.Sql("""
                INSERT INTO period_days ("PeriodId", "Date", "Kind", "Name")
                    SELECT p."Id", h."Date", 'Holiday', h."Name" FROM rota_periods p
                    JOIN public_holidays h ON h."Date" BETWEEN p."StartDate" AND p."EndDate";
                INSERT INTO period_days ("PeriodId", "Date", "Kind", "Name")
                    SELECT p."Id", d."Date", 'Peak', d."Name" FROM rota_periods p
                    JOIN peak_days d ON d."Date" BETWEEN p."StartDate" AND p."EndDate";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "period_days");
        }
    }
}
