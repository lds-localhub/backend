using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEmailUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UQ_User_email",
                table: "User",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_User_email",
                table: "User");
        }
    }
}
