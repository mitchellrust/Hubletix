using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hubletix.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class TenantStatusForStripeRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeAccountRequirementsStatus",
                table: "Tenants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StripeAccountRequirementsStatus",
                table: "Tenants");
        }
    }
}
