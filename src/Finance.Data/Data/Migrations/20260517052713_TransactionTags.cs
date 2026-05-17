using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class TransactionTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankTransactionTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransactionTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MerchantTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantName = table.Column<string>(type: "text", nullable: false),
                    MerchantKey = table.Column<string>(type: "text", nullable: false),
                    TransactionTagId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MerchantTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionTags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionTags_TenantId_BankTransactionId_TransactionT~",
                table: "BankTransactionTags",
                columns: new[] { "TenantId", "BankTransactionId", "TransactionTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransactionTags_TenantId_TransactionTagId",
                table: "BankTransactionTags",
                columns: new[] { "TenantId", "TransactionTagId" });

            migrationBuilder.CreateIndex(
                name: "IX_MerchantTags_TenantId_MerchantKey_TransactionTagId",
                table: "MerchantTags",
                columns: new[] { "TenantId", "MerchantKey", "TransactionTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MerchantTags_TenantId_TransactionTagId",
                table: "MerchantTags",
                columns: new[] { "TenantId", "TransactionTagId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTags_TenantId_Name",
                table: "TransactionTags",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTransactionTags");

            migrationBuilder.DropTable(
                name: "MerchantTags");

            migrationBuilder.DropTable(
                name: "TransactionTags");
        }
    }
}
