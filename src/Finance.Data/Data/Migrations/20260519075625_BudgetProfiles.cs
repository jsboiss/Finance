using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class BudgetProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BudgetProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WeeklyLimitMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false, defaultValue: "AUD"),
                    CategoryMatchers = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetProfileTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BudgetProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetProfileTags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BudgetProfiles_TenantId_Name",
                table: "BudgetProfiles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetProfileTags_TenantId_BudgetProfileId_TransactionTagId",
                table: "BudgetProfileTags",
                columns: new[] { "TenantId", "BudgetProfileId", "TransactionTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetProfileTags_TenantId_TransactionTagId",
                table: "BudgetProfileTags",
                columns: new[] { "TenantId", "TransactionTagId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetProfiles");

            migrationBuilder.DropTable(
                name: "BudgetProfileTags");
        }
    }
}
