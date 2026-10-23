using System;
using LocalHub.Domain.Services;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LocalHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:direct_request_status", "DECLINED,PENDING,PROPOSAL_SUBMITTED,VOID")
                .Annotation("Npgsql:Enum:proposal_model", "DIRECT_REQUEST,PUBLIC")
                .Annotation("Npgsql:Enum:service_status", "ASSIGNED,CANCELLED,COMPLETED,OPEN")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER")
                .OldAnnotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER");

            migrationBuilder.CreateTable(
                name: "Service",
                columns: table => new
                {
                    serviceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    clientId = table.Column<int>(type: "integer", nullable: false),
                    serviceCategoryId = table.Column<int>(type: "integer", nullable: false),
                    proposalModel = table.Column<ProposalModel>(type: "proposal_model", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requestedStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    requestedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<ServiceStatus>(type: "service_status", nullable: false),
                    location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    creationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualCompletionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Service", x => x.serviceId);
                    table.CheckConstraint("CK_Service_actualCompletionDate", "(\"status\" = 'COMPLETED') = (\"actualCompletionDate\" IS NOT NULL) AND (\"actualCompletionDate\" IS NULL OR \"actualCompletionDate\" >= \"creationDate\")");
                    table.CheckConstraint("CK_Service_description", "btrim(\"description\") <> ''");
                    table.CheckConstraint("CK_Service_latitude", "\"latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_Service_location", "btrim(\"location\") <> ''");
                    table.CheckConstraint("CK_Service_longitude", "\"longitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_Service_requestedEndDate", "\"requestedEndDate\" IS NULL OR \"requestedEndDate\" >= \"requestedStartDate\"");
                    table.CheckConstraint("CK_Service_requestedStartDate", "\"requestedStartDate\" >= (\"creationDate\" AT TIME ZONE 'Europe/Lisbon')::date");
                    table.ForeignKey(
                        name: "FK_Service_ServiceCategory",
                        column: x => x.serviceCategoryId,
                        principalTable: "ServiceCategory",
                        principalColumn: "serviceCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Service_User",
                        column: x => x.clientId,
                        principalTable: "User",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DirectRequest",
                columns: table => new
                {
                    directRequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    serviceId = table.Column<int>(type: "integer", nullable: false),
                    professionalProfileId = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<DirectRequestStatus>(type: "direct_request_status", nullable: false),
                    responseDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectRequest", x => x.directRequestId);
                    table.CheckConstraint("CK_DirectRequest_responseDate", "(\"status\" IN ('PROPOSAL_SUBMITTED', 'DECLINED')) = (\"responseDate\" IS NOT NULL) AND (\"responseDate\" IS NULL OR \"responseDate\" >= \"creationDate\")");
                    table.ForeignKey(
                        name: "FK_DirectRequest_ProfessionalProfile",
                        column: x => x.professionalProfileId,
                        principalTable: "ProfessionalProfile",
                        principalColumn: "professionalProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DirectRequest_Service",
                        column: x => x.serviceId,
                        principalTable: "Service",
                        principalColumn: "serviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DirectRequest_serviceId",
                table: "DirectRequest",
                column: "serviceId");

            migrationBuilder.CreateIndex(
                name: "UQ_DirectRequest_professionalProfileId_serviceId",
                table: "DirectRequest",
                columns: new[] { "professionalProfileId", "serviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Service_clientId",
                table: "Service",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "IX_Service_serviceCategoryId_status",
                table: "Service",
                columns: new[] { "serviceCategoryId", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectRequest");

            migrationBuilder.DropTable(
                name: "Service");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER")
                .OldAnnotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .OldAnnotation("Npgsql:Enum:direct_request_status", "DECLINED,PENDING,PROPOSAL_SUBMITTED,VOID")
                .OldAnnotation("Npgsql:Enum:proposal_model", "DIRECT_REQUEST,PUBLIC")
                .OldAnnotation("Npgsql:Enum:service_status", "ASSIGNED,CANCELLED,COMPLETED,OPEN")
                .OldAnnotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER");
        }
    }
}
