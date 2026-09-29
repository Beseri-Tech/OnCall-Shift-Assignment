using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "change_log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "jsonb", nullable: true),
                    Ip = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_change_log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ExtraShift = table.Column<bool>(type: "boolean", nullable: false),
                    PreferWeekendHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    WeekendWeight = table.Column<int>(type: "integer", nullable: false),
                    OpeningTotal = table.Column<int>(type: "integer", nullable: true),
                    OpeningWeekday = table.Column<int>(type: "integer", nullable: true),
                    OpeningWeekendHoliday = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "public_holidays",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_public_holidays", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "rota_periods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LeaveDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rota_periods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "leave_days",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_days", x => new { x.PersonId, x.Date });
                    table.ForeignKey(
                        name: "FK_leave_days_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "preferred_dates",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_preferred_dates", x => new { x.PersonId, x.Date });
                    table.ForeignKey(
                        name: "FK_preferred_dates_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rota_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    Objective = table.Column<double>(type: "double precision", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Warnings = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rota_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rota_runs_rota_periods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "rota_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shift_assignments",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsWeekendHoliday = table.Column<bool>(type: "boolean", nullable: false),
                    IsManual = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_assignments", x => new { x.RunId, x.Date });
                    table.ForeignKey(
                        name: "FK_shift_assignments_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_assignments_rota_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "rota_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_change_log_At",
                table: "change_log",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_leave_days_Date",
                table: "leave_days",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_people_Code",
                table: "people",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_people_Name",
                table: "people",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rota_runs_one_published_per_period",
                table: "rota_runs",
                column: "PeriodId",
                unique: true,
                filter: "\"IsPublished\"");

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignments_PersonId",
                table: "shift_assignments",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "change_log");

            migrationBuilder.DropTable(
                name: "leave_days");

            migrationBuilder.DropTable(
                name: "preferred_dates");

            migrationBuilder.DropTable(
                name: "public_holidays");

            migrationBuilder.DropTable(
                name: "shift_assignments");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "rota_runs");

            migrationBuilder.DropTable(
                name: "rota_periods");
        }
    }
}
