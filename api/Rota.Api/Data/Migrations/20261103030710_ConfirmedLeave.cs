using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmedLeave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApprovalId",
                table: "leave_days",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "leave_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leave_approvals_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_leave_days_ApprovalId",
                table: "leave_days",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_leave_approvals_PersonId_StartDate",
                table: "leave_approvals",
                columns: new[] { "PersonId", "StartDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_leave_days_leave_approvals_ApprovalId",
                table: "leave_days",
                column: "ApprovalId",
                principalTable: "leave_approvals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_leave_days_leave_approvals_ApprovalId",
                table: "leave_days");

            migrationBuilder.DropTable(
                name: "leave_approvals");

            migrationBuilder.DropIndex(
                name: "IX_leave_days_ApprovalId",
                table: "leave_days");

            migrationBuilder.DropColumn(
                name: "ApprovalId",
                table: "leave_days");
        }
    }
}
