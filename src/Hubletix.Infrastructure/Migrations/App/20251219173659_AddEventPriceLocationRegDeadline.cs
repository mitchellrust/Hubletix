using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubManagement.Infrastructure.Migrations.App
{
    /// <inheritdoc />
    public partial class AddEventPriceLocationRegDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Users_CoachId",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "CoachId",
                table: "Events",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Events_CoachId",
                table: "Events",
                newName: "IX_Events_UserId");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Events",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PriceInCents",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationDeadlineUtc",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Users_UserId",
                table: "Events",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Users_UserId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "PriceInCents",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RegistrationDeadlineUtc",
                table: "Events");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Events",
                newName: "CoachId");

            migrationBuilder.RenameIndex(
                name: "IX_Events_UserId",
                table: "Events",
                newName: "IX_Events_CoachId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Users_CoachId",
                table: "Events",
                column: "CoachId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
