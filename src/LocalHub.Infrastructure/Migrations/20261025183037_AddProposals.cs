using System;
using LocalHub.Domain.Services;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LocalHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:direct_request_status", "DECLINED,PENDING,PROPOSAL_SUBMITTED,VOID")
                .Annotation("Npgsql:Enum:proposal_model", "DIRECT_REQUEST,PUBLIC")
                .Annotation("Npgsql:Enum:proposal_status", "ACCEPTED,DECLINED,PENDING,VOID")
                .Annotation("Npgsql:Enum:service_status", "ASSIGNED,CANCELLED,COMPLETED,OPEN")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER")
                .OldAnnotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:direct_request_status", "DECLINED,PENDING,PROPOSAL_SUBMITTED,VOID")
                .OldAnnotation("Npgsql:Enum:proposal_model", "DIRECT_REQUEST,PUBLIC")
                .OldAnnotation("Npgsql:Enum:service_status", "ASSIGNED,CANCELLED,COMPLETED,OPEN")
                .OldAnnotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER");

            migrationBuilder.CreateTable(
                name: "Proposal",
                columns: table => new
                {
                    proposalId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    serviceId = table.Column<int>(type: "integer", nullable: false),
                    professionalProfileId = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    conditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    obs = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<ProposalStatus>(type: "proposal_status", nullable: false),
                    decisionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposal", x => x.proposalId);
                    table.CheckConstraint("CK_Proposal_amount", "\"amount\" > 0 AND \"amount\" <= 100000");
                    table.CheckConstraint("CK_Proposal_conditions", "btrim(\"conditions\") <> ''");
                    table.CheckConstraint("CK_Proposal_decisionDate", "(\"status\" = 'PENDING') = (\"decisionDate\" IS NULL) AND (\"decisionDate\" IS NULL OR \"decisionDate\" >= \"creationDate\")");
                    table.CheckConstraint("CK_Proposal_obs", "\"obs\" IS NULL OR btrim(\"obs\") <> ''");
                    table.ForeignKey(
                        name: "FK_Proposal_ProfessionalProfile",
                        column: x => x.professionalProfileId,
                        principalTable: "ProfessionalProfile",
                        principalColumn: "professionalProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proposal_Service",
                        column: x => x.serviceId,
                        principalTable: "Service",
                        principalColumn: "serviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proposal_serviceId",
                table: "Proposal",
                column: "serviceId");

            migrationBuilder.CreateIndex(
                name: "UQ_Proposal_professionalProfileId_serviceId",
                table: "Proposal",
                columns: new[] { "professionalProfileId", "serviceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Proposal");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:direct_request_status", "DECLINED,PENDING,PROPOSAL_SUBMITTED,VOID")
                .Annotation("Npgsql:Enum:proposal_model", "DIRECT_REQUEST,PUBLIC")
                .Annotation("Npgsql:Enum:service_status", "ASSIGNED,CANCELLED,COMPLETED,OPEN")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER")
                .OldAnnotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:direct_request_status", "DECLINED,PENDING,PROPOSAL_SUBMITTED,VOID")
                .OldAnnotation("Npgsql:Enum:proposal_model", "DIRECT_REQUEST,PUBLIC")
                .OldAnnotation("Npgsql:Enum:proposal_status", "ACCEPTED,DECLINED,PENDING,VOID")
                .OldAnnotation("Npgsql:Enum:service_status", "ASSIGNED,CANCELLED,COMPLETED,OPEN")
                .OldAnnotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER");
        }
    }
}
