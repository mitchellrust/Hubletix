using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hubletix.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class RefactorAfterRenamingTenantUserNav : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlatformUsers_Tenants_DefaultTenantId",
                table: "PlatformUsers");

            migrationBuilder.DropIndex(
                name: "IX_PlatformUsers_DefaultTenantId",
                table: "PlatformUsers");

            migrationBuilder.DropColumn(
                name: "DefaultTenantId",
                table: "PlatformUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultTenantId",
                table: "PlatformUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_DefaultTenantId",
                table: "PlatformUsers",
                column: "DefaultTenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlatformUsers_Tenants_DefaultTenantId",
                table: "PlatformUsers",
                column: "DefaultTenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
