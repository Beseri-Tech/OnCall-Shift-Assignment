using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiFacility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_public_holidays",
                table: "public_holidays");

            migrationBuilder.DropIndex(
                name: "IX_people_Code",
                table: "people");

            migrationBuilder.DropIndex(
                name: "IX_people_Name",
                table: "people");

            migrationBuilder.DropPrimaryKey(
                name: "PK_peak_days",
                table: "peak_days");

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "rota_periods",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StateId",
                table: "public_holidays",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "people",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "StateId",
                table: "peak_days",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "feedback",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "change_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FacilityId",
                table: "accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_public_holidays",
                table: "public_holidays",
                columns: new[] { "StateId", "Date" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_peak_days",
                table: "peak_days",
                columns: new[] { "StateId", "Date" });

            migrationBuilder.CreateTable(
                name: "facilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DistrictId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_facilities_districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Everything so far belongs to Hospital Tuanku Fauziah (Kangar, Perlis); the holiday lists are Perlis's.
            // The system account becomes the first master; everyone signs in again so their cookie carries their hospital.
            migrationBuilder.Sql($"""
                INSERT INTO facilities ("Id", "Name", "Code", "DistrictId")
                VALUES ('{Facility.HtfId}', 'Hospital Tuanku Fauziah', 'HTF', '{Kangar}');
                UPDATE people SET "FacilityId" = '{Facility.HtfId}';
                UPDATE rota_periods SET "FacilityId" = '{Facility.HtfId}';
                UPDATE accounts SET "FacilityId" = '{Facility.HtfId}';
                UPDATE change_log SET "FacilityId" = '{Facility.HtfId}';
                UPDATE feedback SET "FacilityId" = '{Facility.HtfId}';
                UPDATE documents SET "FacilityId" = '{Facility.HtfId}' WHERE "Kind" <> 'Manual';
                UPDATE public_holidays SET "StateId" = '{Perlis}';
                UPDATE peak_days SET "StateId" = '{Perlis}';
                UPDATE accounts SET "Role" = 'Master', "FacilityId" = NULL, "PersonId" = NULL
                    WHERE "Email" = 'rota.oc.system@gmail.com';
                UPDATE accounts SET "SecurityStamp" = replace(gen_random_uuid()::text, '-', '');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_rota_periods_FacilityId",
                table: "rota_periods",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_people_FacilityId_Code",
                table: "people",
                columns: new[] { "FacilityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_people_FacilityId_Name",
                table: "people",
                columns: new[] { "FacilityId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feedback_FacilityId",
                table: "feedback",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_documents_FacilityId",
                table: "documents",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_change_log_FacilityId_At",
                table: "change_log",
                columns: new[] { "FacilityId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_FacilityId",
                table: "accounts",
                column: "FacilityId");

            migrationBuilder.AddCheckConstraint(
                name: "ck_accounts_facility",
                table: "accounts",
                sql: "\"Role\" = 'Master' OR \"FacilityId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_facilities_Code",
                table: "facilities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_facilities_DistrictId",
                table: "facilities",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_facilities_Name",
                table: "facilities",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_facilities_FacilityId",
                table: "accounts",
                column: "FacilityId",
                principalTable: "facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_feedback_facilities_FacilityId",
                table: "feedback",
                column: "FacilityId",
                principalTable: "facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_peak_days_states_StateId",
                table: "peak_days",
                column: "StateId",
                principalTable: "states",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_people_facilities_FacilityId",
                table: "people",
                column: "FacilityId",
                principalTable: "facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_public_holidays_states_StateId",
                table: "public_holidays",
                column: "StateId",
                principalTable: "states",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rota_periods_facilities_FacilityId",
                table: "rota_periods",
                column: "FacilityId",
                principalTable: "facilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private const string Perlis = "01999a00-0000-7000-8000-000000000001";
        private const string Kangar = "01999a00-0000-7000-8000-000000000011";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_facilities_FacilityId",
                table: "accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_feedback_facilities_FacilityId",
                table: "feedback");

            migrationBuilder.DropForeignKey(
                name: "FK_peak_days_states_StateId",
                table: "peak_days");

            migrationBuilder.DropForeignKey(
                name: "FK_people_facilities_FacilityId",
                table: "people");

            migrationBuilder.DropForeignKey(
                name: "FK_public_holidays_states_StateId",
                table: "public_holidays");

            migrationBuilder.DropForeignKey(
                name: "FK_rota_periods_facilities_FacilityId",
                table: "rota_periods");

            migrationBuilder.DropTable(
                name: "facilities");

            migrationBuilder.DropIndex(
                name: "IX_rota_periods_FacilityId",
                table: "rota_periods");

            migrationBuilder.DropPrimaryKey(
                name: "PK_public_holidays",
                table: "public_holidays");

            migrationBuilder.DropIndex(
                name: "IX_people_FacilityId_Code",
                table: "people");

            migrationBuilder.DropIndex(
                name: "IX_people_FacilityId_Name",
                table: "people");

            migrationBuilder.DropPrimaryKey(
                name: "PK_peak_days",
                table: "peak_days");

            migrationBuilder.DropIndex(
                name: "IX_feedback_FacilityId",
                table: "feedback");

            migrationBuilder.DropIndex(
                name: "IX_documents_FacilityId",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_change_log_FacilityId_At",
                table: "change_log");

            migrationBuilder.DropIndex(
                name: "IX_accounts_FacilityId",
                table: "accounts");

            migrationBuilder.DropCheckConstraint(
                name: "ck_accounts_facility",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "rota_periods");

            migrationBuilder.DropColumn(
                name: "StateId",
                table: "public_holidays");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "people");

            migrationBuilder.DropColumn(
                name: "StateId",
                table: "peak_days");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "feedback");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "change_log");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "accounts");

            migrationBuilder.AddPrimaryKey(
                name: "PK_public_holidays",
                table: "public_holidays",
                column: "Date");

            migrationBuilder.AddPrimaryKey(
                name: "PK_peak_days",
                table: "peak_days",
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
        }
    }
}
