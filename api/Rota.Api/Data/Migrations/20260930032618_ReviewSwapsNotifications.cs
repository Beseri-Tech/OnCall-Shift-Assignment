using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewSwapsNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsInReview",
                table: "rota_runs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SwapDeadline",
                table: "rota_periods",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Link = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "swap_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToPersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_swap_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_swap_requests_people_FromPersonId",
                        column: x => x.FromPersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_swap_requests_people_ToPersonId",
                        column: x => x.ToPersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_swap_requests_rota_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "rota_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rota_runs_one_in_review_per_period",
                table: "rota_runs",
                column: "PeriodId",
                unique: true,
                filter: "\"IsInReview\"");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_AccountId_CreatedAt",
                table: "notifications",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_swap_requests_FromPersonId",
                table: "swap_requests",
                column: "FromPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_swap_requests_RunId_Status",
                table: "swap_requests",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_swap_requests_ToPersonId",
                table: "swap_requests",
                column: "ToPersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "swap_requests");

            migrationBuilder.DropIndex(
                name: "ix_rota_runs_one_in_review_per_period",
                table: "rota_runs");

            migrationBuilder.DropColumn(
                name: "IsInReview",
                table: "rota_runs");

            migrationBuilder.DropColumn(
                name: "SwapDeadline",
                table: "rota_periods");
        }
    }
}
