using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LeavePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PointsBudget",
                table: "rota_periods",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "peak_days",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_peak_days", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "point_grants",
                columns: table => new
                {
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_point_grants", x => new { x.PeriodId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_point_grants_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_point_grants_rota_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "rota_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_point_grants_PersonId",
                table: "point_grants",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "peak_days");

            migrationBuilder.DropTable(
                name: "point_grants");

            migrationBuilder.DropColumn(
                name: "PointsBudget",
                table: "rota_periods");
        }
    }
}
