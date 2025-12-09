using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubManagement.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class UserMembershipPlanId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MembershipPlanId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_MembershipPlanId",
                table: "Users",
                column: "MembershipPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_MembershipPlans_MembershipPlanId",
                table: "Users",
                column: "MembershipPlanId",
                principalTable: "MembershipPlans",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_MembershipPlans_MembershipPlanId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_MembershipPlanId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MembershipPlanId",
                table: "Users");
        }
    }
}
