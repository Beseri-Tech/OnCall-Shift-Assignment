using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MergeAccountsIntoPeople : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_accounts_UploadedByAccountId",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_feedback_accounts_AccountId",
                table: "feedback");

            migrationBuilder.DropForeignKey(
                name: "FK_feedback_accounts_ResolvedByAccountId",
                table: "feedback");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_accounts_AccountId",
                table: "notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "FacilityId",
                table: "people",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "people",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "people",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAt",
                table: "people",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "people",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "people",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResetTokenExpiresAt",
                table: "people",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResetTokenHash",
                table: "people",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "people",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "people",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TempPasswordExpiresAt",
                table: "people",
                type: "timestamp with time zone",
                nullable: true);

            // Officers and logins become one record. Officers with a login take its email, password and role; supervisors and
            // masters (logins with no officer) become people with their login's id, so nothing pointing at them changes.
            // Everything that pointed at an officer's login is re-pointed to the officer. Officers without a login keep no
            // email: they stay on the rota and the Officers tab asks for one ("email needed").
            migrationBuilder.Sql("""
                UPDATE people SET "Role" = 'Officer', "Enabled" = true, "SecurityStamp" = replace(gen_random_uuid()::text, '-', '');

                UPDATE people p SET "Email" = a."Email", "PasswordHash" = a."PasswordHash", "Role" = a."Role", "Enabled" = a."Enabled",
                    "MustChangePassword" = a."MustChangePassword", "TempPasswordExpiresAt" = a."TempPasswordExpiresAt",
                    "ResetTokenHash" = a."ResetTokenHash", "ResetTokenExpiresAt" = a."ResetTokenExpiresAt",
                    "SecurityStamp" = a."SecurityStamp", "LastLoginAt" = a."LastLoginAt"
                FROM accounts a WHERE a."PersonId" = p."Id";

                INSERT INTO people ("Id", "Code", "Name", "FacilityId", "Status", "SortOrder", "BlockedWeekdays", "ExtraShift",
                    "PreferWeekendHoliday", "WeekendWeight", "CreatedAt", "UpdatedAt", "Email", "PasswordHash", "Role", "Enabled",
                    "MustChangePassword", "TempPasswordExpiresAt", "ResetTokenHash", "ResetTokenExpiresAt", "SecurityStamp", "LastLoginAt")
                SELECT a."Id", 'S' || upper(substr(replace(a."Id"::text, '-', ''), 25, 8)), a."Email", a."FacilityId", 'OnCall', 0, 0, false,
                    false, 1, a."CreatedAt", a."CreatedAt", a."Email", a."PasswordHash", a."Role", a."Enabled",
                    a."MustChangePassword", a."TempPasswordExpiresAt", a."ResetTokenHash", a."ResetTokenExpiresAt", a."SecurityStamp", a."LastLoginAt"
                FROM accounts a WHERE a."PersonId" IS NULL;

                UPDATE notifications t SET "AccountId" = a."PersonId" FROM accounts a WHERE t."AccountId" = a."Id" AND a."PersonId" IS NOT NULL;
                UPDATE feedback t SET "AccountId" = a."PersonId" FROM accounts a WHERE t."AccountId" = a."Id" AND a."PersonId" IS NOT NULL;
                UPDATE feedback t SET "ResolvedByAccountId" = a."PersonId" FROM accounts a
                    WHERE t."ResolvedByAccountId" = a."Id" AND a."PersonId" IS NOT NULL;
                UPDATE documents t SET "UploadedByAccountId" = a."PersonId" FROM accounts a
                    WHERE t."UploadedByAccountId" = a."Id" AND a."PersonId" IS NOT NULL;
                UPDATE change_log t SET "AccountId" = a."PersonId" FROM accounts a WHERE t."AccountId" = a."Id" AND a."PersonId" IS NOT NULL;
                UPDATE leave_approvals t SET "CreatedByAccountId" = a."PersonId" FROM accounts a
                    WHERE t."CreatedByAccountId" = a."Id" AND a."PersonId" IS NOT NULL;
                UPDATE leave_request_ranges t SET "DecidedByAccountId" = a."PersonId" FROM accounts a
                    WHERE t."DecidedByAccountId" = a."Id" AND a."PersonId" IS NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.CreateIndex(
                name: "IX_people_Email",
                table: "people",
                column: "Email",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_people_facility",
                table: "people",
                sql: "\"Role\" = 'Master' OR \"FacilityId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_documents_people_UploadedByAccountId",
                table: "documents",
                column: "UploadedByAccountId",
                principalTable: "people",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_feedback_people_AccountId",
                table: "feedback",
                column: "AccountId",
                principalTable: "people",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_feedback_people_ResolvedByAccountId",
                table: "feedback",
                column: "ResolvedByAccountId",
                principalTable: "people",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_people_AccountId",
                table: "notifications",
                column: "AccountId",
                principalTable: "people",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_people_UploadedByAccountId",
                table: "documents");

            migrationBuilder.DropForeignKey(
                name: "FK_feedback_people_AccountId",
                table: "feedback");

            migrationBuilder.DropForeignKey(
                name: "FK_feedback_people_ResolvedByAccountId",
                table: "feedback");

            migrationBuilder.DropForeignKey(
                name: "FK_notifications_people_AccountId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_people_Email",
                table: "people");

            migrationBuilder.DropCheckConstraint(
                name: "ck_people_facility",
                table: "people");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "people");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "people");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "people");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "people");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "people");

            migrationBuilder.DropColumn(
                name: "ResetTokenExpiresAt",
                table: "people");

            migrationBuilder.DropColumn(
                name: "ResetTokenHash",
                table: "people");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "people");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "people");

            migrationBuilder.DropColumn(
                name: "TempPasswordExpiresAt",
                table: "people");

            migrationBuilder.AlterColumn<Guid>(
                name: "FacilityId",
                table: "people",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    ResetTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResetTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SecurityStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TempPasswordExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.CheckConstraint("ck_accounts_facility", "\"Role\" = 'Master' OR \"FacilityId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_accounts_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_people_PersonId",
                        column: x => x.PersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_Email",
                table: "accounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_FacilityId",
                table: "accounts",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_PersonId",
                table: "accounts",
                column: "PersonId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_accounts_UploadedByAccountId",
                table: "documents",
                column: "UploadedByAccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_feedback_accounts_AccountId",
                table: "feedback",
                column: "AccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_feedback_accounts_ResolvedByAccountId",
                table: "feedback",
                column: "ResolvedByAccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_accounts_AccountId",
                table: "notifications",
                column: "AccountId",
                principalTable: "accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
