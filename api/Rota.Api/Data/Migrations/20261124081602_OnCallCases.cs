using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OnCallCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "oncall_cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedById = table.Column<Guid>(type: "uuid", nullable: false),
                    OnCallDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeStart = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeFinish = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    PatientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PatientIdNo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Age = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Race = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ReferralSource = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Department = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Diagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Management = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Plan = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Taggers = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DsaName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DsaStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Confidential = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oncall_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_oncall_cases_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_oncall_cases_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_oncall_cases_people_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_oncall_cases_FacilityId_OnCallDate",
                table: "oncall_cases",
                columns: new[] { "FacilityId", "OnCallDate" });

            migrationBuilder.CreateIndex(
                name: "IX_oncall_cases_PersonId_OnCallDate",
                table: "oncall_cases",
                columns: new[] { "PersonId", "OnCallDate" });

            migrationBuilder.CreateIndex(
                name: "IX_oncall_cases_RecordedById",
                table: "oncall_cases",
                column: "RecordedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "oncall_cases");
        }
    }
}
