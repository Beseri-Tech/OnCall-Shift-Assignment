using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    [DbContext(typeof(RotaDbContext))]
    [Migration("20261125020000_PeriodOpensAt")]
    public partial class PeriodOpensAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(name: "OpensAt", table: "rota_periods", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "DayReminderSentAt", table: "rota_periods", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "HourReminderSentAt", table: "rota_periods", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "OpenNoticeSentAt", table: "rota_periods", type: "timestamp with time zone", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "OpensAt", table: "rota_periods");
            migrationBuilder.DropColumn(name: "DayReminderSentAt", table: "rota_periods");
            migrationBuilder.DropColumn(name: "HourReminderSentAt", table: "rota_periods");
            migrationBuilder.DropColumn(name: "OpenNoticeSentAt", table: "rota_periods");
        }
    }
}
