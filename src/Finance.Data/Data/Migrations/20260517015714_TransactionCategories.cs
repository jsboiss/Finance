using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class TransactionCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "BankTransactions",
                type: "text",
                nullable: false,
                defaultValue: "Uncategorized");

            migrationBuilder.Sql("""
                UPDATE "BankTransactions"
                SET "Category" = COALESCE(NULLIF("RawJson"->>'category', ''), 'Uncategorized');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "BankTransactions");
        }
    }
}
