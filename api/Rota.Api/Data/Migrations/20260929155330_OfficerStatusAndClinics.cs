using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OfficerStatusAndClinics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClinicId",
                table: "people",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExcludedUntil",
                table: "people",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "people",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "people",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "OnCall");

            // Existing inactive people become Excluded (reason unknown); the admin can mark them Left.
            migrationBuilder.Sql("UPDATE people SET \"Status\" = CASE WHEN \"Active\" THEN 'OnCall' ELSE 'Excluded' END;");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "people");

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                table: "people",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clinics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Area = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clinics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_people_ClinicId",
                table: "people",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_clinics_Name",
                table: "clinics",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_people_clinics_ClinicId",
                table: "people",
                column: "ClinicId",
                principalTable: "clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_people_clinics_ClinicId",
                table: "people");

            migrationBuilder.DropTable(
                name: "clinics");

            migrationBuilder.DropIndex(
                name: "IX_people_ClinicId",
                table: "people");

            migrationBuilder.DropColumn(
                name: "ClinicId",
                table: "people");

            migrationBuilder.DropColumn(
                name: "ExcludedUntil",
                table: "people");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "people");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "people",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("UPDATE people SET \"Active\" = \"Status\" = 'OnCall';");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "people");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                table: "people");
        }
    }
}
