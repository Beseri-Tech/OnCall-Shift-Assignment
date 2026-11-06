using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rota.Api.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(RotaDbContext))]
    [Migration("20261106070000_Documents")]
    public partial class Documents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerPersonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ParentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_documents_accounts_UploadedByAccountId",
                        column: x => x.UploadedByAccountId,
                        principalTable: "accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_documents_people_OwnerPersonId",
                        column: x => x.OwnerPersonId,
                        principalTable: "people",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(name: "IX_documents_Kind_Status", table: "documents", columns: new[] { "Kind", "Status" });
            migrationBuilder.CreateIndex(name: "IX_documents_ObjectKey", table: "documents", column: "ObjectKey", unique: true);
            migrationBuilder.CreateIndex(name: "IX_documents_OwnerPersonId", table: "documents", column: "OwnerPersonId");
            migrationBuilder.CreateIndex(name: "IX_documents_ParentType_ParentId", table: "documents", columns: new[] { "ParentType", "ParentId" });
            migrationBuilder.CreateIndex(name: "IX_documents_UploadedByAccountId", table: "documents", column: "UploadedByAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "documents");
        }
    }
}
