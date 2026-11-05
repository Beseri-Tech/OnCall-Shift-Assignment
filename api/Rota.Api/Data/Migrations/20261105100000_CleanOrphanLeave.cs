using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <summary>
    /// Leave and preferred days an officer entered are always saved against a period. When a period was deleted they were left
    /// behind and later showed up as "already marked" leave. Removes the ones no remaining period covers (confirmed leave stays).
    /// </summary>
    [DbContext(typeof(RotaDbContext))]
    [Migration("20261105100000_CleanOrphanLeave")]
    public partial class CleanOrphanLeave : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM leave_days l
                WHERE l."ApprovalId" IS NULL
                  AND NOT EXISTS (SELECT 1 FROM rota_periods p WHERE l."Date" BETWEEN p."StartDate" AND p."EndDate");
                DELETE FROM preferred_dates d
                WHERE NOT EXISTS (SELECT 1 FROM rota_periods p WHERE d."Date" BETWEEN p."StartDate" AND p."EndDate");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
