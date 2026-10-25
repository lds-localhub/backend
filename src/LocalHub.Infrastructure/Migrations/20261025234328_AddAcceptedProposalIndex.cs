using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LocalHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcceptedProposalIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Proposal",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "DirectRequest",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "UX_Proposal_serviceId_accepted",
                table: "Proposal",
                column: "serviceId",
                unique: true,
                filter: "\"status\" = 'ACCEPTED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Proposal_serviceId_accepted",
                table: "Proposal");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Proposal");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "DirectRequest");
        }
    }
}
