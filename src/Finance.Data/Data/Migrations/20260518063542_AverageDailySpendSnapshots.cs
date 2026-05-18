using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class AverageDailySpendSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OverviewMetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeKey = table.Column<string>(type: "text", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AverageDailySpendMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OverviewMetricSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OverviewMetricSnapshots_TenantId_BankAccountId_SnapshotDate",
                table: "OverviewMetricSnapshots",
                columns: new[] { "TenantId", "BankAccountId", "SnapshotDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OverviewMetricSnapshots_TenantId_ScopeKey_SnapshotDate",
                table: "OverviewMetricSnapshots",
                columns: new[] { "TenantId", "ScopeKey", "SnapshotDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OverviewMetricSnapshots");
        }
    }
}
