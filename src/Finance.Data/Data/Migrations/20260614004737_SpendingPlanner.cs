using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class SpendingPlanner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpendingPlannerItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false, defaultValue: "AUD"),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPurchased = table.Column<bool>(type: "boolean", nullable: false),
                    PurchasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpendingPlannerItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpendingPlannerItems_TenantId_IsPurchased",
                table: "SpendingPlannerItems",
                columns: new[] { "TenantId", "IsPurchased" });

            migrationBuilder.CreateIndex(
                name: "IX_SpendingPlannerItems_TenantId_TargetDate",
                table: "SpendingPlannerItems",
                columns: new[] { "TenantId", "TargetDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpendingPlannerItems");
        }
    }
}
