using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MerchantName = table.Column<string>(type: "text", nullable: false),
                    MerchantKey = table.Column<string>(type: "text", nullable: false),
                    PaymentManager = table.Column<string>(type: "text", nullable: false),
                    Cadence = table.Column<string>(type: "text", nullable: false),
                    ExpectedAmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    StatusOverride = table.Column<string>(type: "text", nullable: true),
                    FirstPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextExpectedPaymentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantName = table.Column<string>(type: "text", nullable: false),
                    MerchantKey = table.Column<string>(type: "text", nullable: false),
                    PaymentManager = table.Column<string>(type: "text", nullable: false, defaultValue: "direct"),
                    Cadence = table.Column<string>(type: "text", nullable: false),
                    ExpectedAmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    SampleTransactionIds = table.Column<string>(type: "text", nullable: false),
                    FirstPaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LastPaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NextExpectedPaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchConfidence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_MerchantKey",
                table: "Subscriptions",
                columns: new[] { "TenantId", "MerchantKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_StatusOverride",
                table: "Subscriptions",
                columns: new[] { "TenantId", "StatusOverride" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionSuggestions_TenantId_MerchantKey_Cadence_Expect~",
                table: "SubscriptionSuggestions",
                columns: new[] { "TenantId", "MerchantKey", "Cadence", "ExpectedAmountMinorUnits" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionSuggestions_TenantId_Status",
                table: "SubscriptionSuggestions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTransactions_TenantId_BankTransactionId",
                table: "SubscriptionTransactions",
                columns: new[] { "TenantId", "BankTransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTransactions_TenantId_SubscriptionId_BankTransa~",
                table: "SubscriptionTransactions",
                columns: new[] { "TenantId", "SubscriptionId", "BankTransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionSuggestions");

            migrationBuilder.DropTable(
                name: "SubscriptionTransactions");
        }
    }
}
