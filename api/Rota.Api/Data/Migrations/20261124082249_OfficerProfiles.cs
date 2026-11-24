using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OfficerProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "officer_profiles",
                columns: table => new
                {
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IcNo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Jawatan = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gred = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TarikhPewartaanPakar = table.Column<DateOnly>(type: "date", nullable: true),
                    UnitTempatBertugas = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AlamatRumah = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    JarakKm = table.Column<decimal>(type: "numeric(7,1)", precision: 7, scale: 1, nullable: true),
                    NoGaji = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NoTelefonPejabat = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    NoAkaunBank = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NamaCawanganBank = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_officer_profiles", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_officer_profiles_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "officer_profiles");
        }
    }
}
