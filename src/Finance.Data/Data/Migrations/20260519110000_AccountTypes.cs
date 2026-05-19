using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260519110000_AccountTypes")]
    public partial class AccountTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountType",
                table: "BankAccounts",
                type: "text",
                nullable: false,
                defaultValue: "Everyday");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "BankAccounts");
        }
    }
}
