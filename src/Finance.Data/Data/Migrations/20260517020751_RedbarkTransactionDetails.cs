using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class RedbarkTransactionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "BankTransactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountName",
                table: "BankTransactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MerchantCategoryCode",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantName",
                table: "BankTransactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PostedAt",
                table: "BankTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "BankTransactions",
                type: "text",
                nullable: false,
                defaultValue: "posted");

            migrationBuilder.Sql("""
                UPDATE "BankTransactions"
                SET
                    "Direction" = COALESCE("RawJson"->>'direction', ''),
                    "ExternalAccountName" = COALESCE("RawJson"->>'accountName', ''),
                    "MerchantCategoryCode" = NULLIF("RawJson"->>'merchantCategoryCode', ''),
                    "MerchantName" = NULLIF("RawJson"->>'merchantName', ''),
                    "PostedAt" = NULLIF("RawJson"->>'datetime', '')::timestamp with time zone,
                    "Status" = COALESCE(NULLIF("RawJson"->>'status', ''), 'posted');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Direction",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalAccountName",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "MerchantCategoryCode",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "MerchantName",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "BankTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "BankTransactions");
        }
    }
}
