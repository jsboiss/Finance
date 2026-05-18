using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class RedbarkConnectionAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RedbarkConnectionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalConnectionId = table.Column<string>(type: "text", nullable: false),
                    InstitutionName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RedbarkConnectionAssignments", x => x.Id);
                });

            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS pgcrypto""");
            migrationBuilder.Sql("""
                INSERT INTO "RedbarkConnectionAssignments" ("Id", "TenantId", "ExternalConnectionId", "InstitutionName", "CreatedAt")
                SELECT gen_random_uuid(), x."TenantId", x."ExternalConnectionId", x."InstitutionName", NOW()
                FROM (
                    SELECT DISTINCT ON ("ExternalConnectionId") "TenantId", "ExternalConnectionId", "InstitutionName"
                    FROM "BankConnections"
                    ORDER BY "ExternalConnectionId", "TenantId"
                ) x
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RedbarkConnectionAssignments_ExternalConnectionId",
                table: "RedbarkConnectionAssignments",
                column: "ExternalConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RedbarkConnectionAssignments_TenantId_ExternalConnectionId",
                table: "RedbarkConnectionAssignments",
                columns: new[] { "TenantId", "ExternalConnectionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RedbarkConnectionAssignments");
        }
    }
}
