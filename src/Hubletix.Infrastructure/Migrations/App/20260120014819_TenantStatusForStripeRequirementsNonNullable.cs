using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hubletix.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class TenantStatusForStripeRequirementsNonNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StripeAccountRequirementsStatus",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "StripeAccountRequirementsStatus",
                table: "Tenants",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
