using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocationHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_states", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "districts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_districts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_districts_states_StateId",
                        column: x => x.StateId,
                        principalTable: "states",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Starting data: Perlis with Kangar and Arau. Each clinic's free-text area becomes a district of Perlis.
            migrationBuilder.Sql($"""
                INSERT INTO states ("Id", "Name") VALUES ('{Perlis}', 'Perlis');
                INSERT INTO districts ("Id", "StateId", "Name") VALUES ('{Kangar}', '{Perlis}', 'Kangar'), ('{Arau}', '{Perlis}', 'Arau');
                INSERT INTO districts ("Id", "StateId", "Name")
                    SELECT gen_random_uuid(), '{Perlis}', a.name
                    FROM (SELECT DISTINCT ON (lower(btrim("Area"))) btrim("Area") AS name FROM clinics ORDER BY lower(btrim("Area"))) a
                    WHERE a.name <> '' AND lower(a.name) NOT IN ('kangar', 'arau');
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "DistrictId",
                table: "clinics",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql($"""
                UPDATE clinics c SET "DistrictId" = d."Id" FROM districts d WHERE lower(d."Name") = lower(btrim(c."Area"));
                UPDATE clinics SET "DistrictId" = '{Kangar}' WHERE "DistrictId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "DistrictId",
                table: "clinics",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "Area",
                table: "clinics");

            migrationBuilder.CreateIndex(
                name: "IX_clinics_DistrictId",
                table: "clinics",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_districts_StateId_Name",
                table: "districts",
                columns: new[] { "StateId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_states_Name",
                table: "states",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_clinics_districts_DistrictId",
                table: "clinics",
                column: "DistrictId",
                principalTable: "districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private const string Perlis = "01999a00-0000-7000-8000-000000000001";
        private const string Kangar = "01999a00-0000-7000-8000-000000000011";
        private const string Arau = "01999a00-0000-7000-8000-000000000012";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clinics_districts_DistrictId",
                table: "clinics");

            migrationBuilder.DropIndex(
                name: "IX_clinics_DistrictId",
                table: "clinics");

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "clinics",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""UPDATE clinics c SET "Area" = d."Name" FROM districts d WHERE d."Id" = c."DistrictId";""");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "clinics");

            migrationBuilder.DropTable(
                name: "districts");

            migrationBuilder.DropTable(
                name: "states");
        }
    }
}
