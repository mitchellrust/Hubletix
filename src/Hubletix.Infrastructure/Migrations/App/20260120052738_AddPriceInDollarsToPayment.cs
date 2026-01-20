using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hubletix.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class AddPriceInDollarsToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AmountInCents",
                table: "Payments",
                newName: "PriceInCents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PriceInCents",
                table: "Payments",
                newName: "AmountInCents");
        }
    }
}
