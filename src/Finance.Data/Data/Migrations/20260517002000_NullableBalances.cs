using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Data.Data.Migrations
{
    /// <inheritdoc />
    public partial class NullableBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "CurrentMinorUnits",
                table: "Balances",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.Sql("""
                UPDATE "Balances"
                SET "CurrentMinorUnits" = NULL
                WHERE "RawJson"->>'currentBalance' IS NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Balances"
                SET "CurrentMinorUnits" = 0
                WHERE "CurrentMinorUnits" IS NULL
                """);

            migrationBuilder.AlterColumn<long>(
                name: "CurrentMinorUnits",
                table: "Balances",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
