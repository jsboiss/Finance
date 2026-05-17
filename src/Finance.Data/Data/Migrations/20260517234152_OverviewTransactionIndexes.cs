using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class OverviewTransactionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TenantId_BankAccountId_PostedDate",
                table: "BankTransactions",
                columns: new[] { "TenantId", "BankAccountId", "PostedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TenantId_BankAccountId_Status_PostedDate",
                table: "BankTransactions",
                columns: new[] { "TenantId", "BankAccountId", "Status", "PostedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TenantId_PostedDate",
                table: "BankTransactions",
                columns: new[] { "TenantId", "PostedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_TenantId_Status_PostedDate",
                table: "BankTransactions",
                columns: new[] { "TenantId", "Status", "PostedDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_TenantId_BankAccountId_PostedDate",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_TenantId_BankAccountId_Status_PostedDate",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_TenantId_PostedDate",
                table: "BankTransactions");

            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_TenantId_Status_PostedDate",
                table: "BankTransactions");
        }
    }
}
