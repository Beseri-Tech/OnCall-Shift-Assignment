using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class CaseClinicalNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaseType",
                table: "oncall_cases",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Investigations",
                table: "oncall_cases",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Prescription",
                table: "oncall_cases",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Presentation",
                table: "oncall_cases",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeenBy",
                table: "oncall_cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpokenTo",
                table: "oncall_cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaseType",
                table: "oncall_cases");

            migrationBuilder.DropColumn(
                name: "Investigations",
                table: "oncall_cases");

            migrationBuilder.DropColumn(
                name: "Prescription",
                table: "oncall_cases");

            migrationBuilder.DropColumn(
                name: "Presentation",
                table: "oncall_cases");

            migrationBuilder.DropColumn(
                name: "SeenBy",
                table: "oncall_cases");

            migrationBuilder.DropColumn(
                name: "SpokenTo",
                table: "oncall_cases");
        }
    }
}
