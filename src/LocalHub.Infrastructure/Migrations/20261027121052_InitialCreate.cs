using System;
using LocalHub.Domain.Accounts;
using LocalHub.Domain.Services;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LocalHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
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
                .Annotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER");

            migrationBuilder.CreateTable(
                name: "ServiceCategory",
                columns: table => new
                {
                    serviceCategoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    status = table.Column<ActivationStatus>(type: "activation_status", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_ServiceCategory", x => x.serviceCategoryId);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    userId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    passwordHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<UserRole>(type: "user_role", nullable: false),
                    status = table.Column<ActivationStatus>(type: "activation_status", nullable: false),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_User", x => x.userId);
                });

            migrationBuilder.CreateTable(
                name: "ProfessionalProfile",
                columns: table => new
                {
                    professionalProfileId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    serviceProviderId = table.Column<int>(type: "integer", nullable: false),
                    introduction = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    contactDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    experience = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    averageRating = table.Column<decimal>(type: "numeric(2,1)", precision: 2, scale: 1, nullable: true),
                    serviceRadiusKm = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_ProfessionalProfile", x => x.professionalProfileId);
                    table.ForeignKey(
                        name: "fK_ProfessionalProfile_users_serviceProviderId",
                        column: x => x.serviceProviderId,
                        principalTable: "User",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Service",
                columns: table => new
                {
                    serviceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    clientId = table.Column<int>(type: "integer", nullable: false),
                    serviceCategoryId = table.Column<int>(type: "integer", nullable: false),
                    proposalModel = table.Column<ProposalModel>(type: "proposal_model", nullable: false),
                    title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requestedStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    requestedEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<ServiceStatus>(type: "service_status", nullable: false),
                    location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    creationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualCompletionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_Service", x => x.serviceId);
                    table.ForeignKey(
                        name: "fK_Service_ServiceCategory_serviceCategoryId",
                        column: x => x.serviceCategoryId,
                        principalTable: "ServiceCategory",
                        principalColumn: "serviceCategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fK_Service_users_clientId",
                        column: x => x.clientId,
                        principalTable: "User",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserSession",
                columns: table => new
                {
                    sessionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    userId = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_UserSession", x => x.sessionId);
                    table.ForeignKey(
                        name: "fK_UserSession_User_userId",
                        column: x => x.userId,
                        principalTable: "User",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceArea",
                columns: table => new
                {
                    serviceCategoryId = table.Column<int>(type: "integer", nullable: false),
                    professionalProfileId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_ServiceArea", x => new { x.serviceCategoryId, x.professionalProfileId });
                    table.ForeignKey(
                        name: "fK_ServiceArea_ProfessionalProfile_professionalProfileId",
                        column: x => x.professionalProfileId,
                        principalTable: "ProfessionalProfile",
                        principalColumn: "professionalProfileId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fK_ServiceArea_serviceCategories_serviceCategoryId",
                        column: x => x.serviceCategoryId,
                        principalTable: "ServiceCategory",
                        principalColumn: "serviceCategoryId",
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
                    table.PrimaryKey("pK_DirectRequest", x => x.directRequestId);
                    table.ForeignKey(
                        name: "fK_DirectRequest_professionalProfiles_professionalProfileId",
                        column: x => x.professionalProfileId,
                        principalTable: "ProfessionalProfile",
                        principalColumn: "professionalProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fK_DirectRequest_services_serviceId",
                        column: x => x.serviceId,
                        principalTable: "Service",
                        principalColumn: "serviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Proposal",
                columns: table => new
                {
                    proposalId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    serviceId = table.Column<int>(type: "integer", nullable: false),
                    professionalProfileId = table.Column<int>(type: "integer", nullable: false),
                    conditions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    obs = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    status = table.Column<ProposalStatus>(type: "proposal_status", nullable: false),
                    decisionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_Proposal", x => x.proposalId);
                    table.ForeignKey(
                        name: "fK_Proposal_ProfessionalProfile_professionalProfileId",
                        column: x => x.professionalProfileId,
                        principalTable: "ProfessionalProfile",
                        principalColumn: "professionalProfileId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fK_Proposal_services_serviceId",
                        column: x => x.serviceId,
                        principalTable: "Service",
                        principalColumn: "serviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Review",
                columns: table => new
                {
                    reviewId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    serviceId = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_Review", x => x.reviewId);
                    table.ForeignKey(
                        name: "fK_Review_services_serviceId",
                        column: x => x.serviceId,
                        principalTable: "Service",
                        principalColumn: "serviceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceStatusHistory",
                columns: table => new
                {
                    serviceStatusHistoryId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    serviceId = table.Column<int>(type: "integer", nullable: false),
                    previousStatus = table.Column<ServiceStatus>(type: "service_status", nullable: false),
                    newStatus = table.Column<ServiceStatus>(type: "service_status", nullable: false),
                    changedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pK_ServiceStatusHistory", x => x.serviceStatusHistoryId);
                    table.ForeignKey(
                        name: "fK_ServiceStatusHistory_Service_serviceId",
                        column: x => x.serviceId,
                        principalTable: "Service",
                        principalColumn: "serviceId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fK_ServiceStatusHistory_users_changedByUserId",
                        column: x => x.changedByUserId,
                        principalTable: "User",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "iX_DirectRequest_professionalProfileId_serviceId",
                table: "DirectRequest",
                columns: new[] { "professionalProfileId", "serviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "iX_DirectRequest_serviceId",
                table: "DirectRequest",
                column: "serviceId");

            migrationBuilder.CreateIndex(
                name: "iX_ProfessionalProfile_serviceProviderId",
                table: "ProfessionalProfile",
                column: "serviceProviderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "iX_Proposal_professionalProfileId_serviceId",
                table: "Proposal",
                columns: new[] { "professionalProfileId", "serviceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Proposal_serviceId_accepted",
                table: "Proposal",
                column: "serviceId",
                unique: true,
                filter: "\"status\" = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "iX_Review_serviceId",
                table: "Review",
                column: "serviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "iX_Service_clientId",
                table: "Service",
                column: "clientId");

            migrationBuilder.CreateIndex(
                name: "iX_Service_serviceCategoryId",
                table: "Service",
                column: "serviceCategoryId");

            migrationBuilder.CreateIndex(
                name: "iX_ServiceArea_professionalProfileId",
                table: "ServiceArea",
                column: "professionalProfileId");

            migrationBuilder.CreateIndex(
                name: "UX_ServiceCategory_name",
                table: "ServiceCategory",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "iX_ServiceStatusHistory_changedByUserId",
                table: "ServiceStatusHistory",
                column: "changedByUserId");

            migrationBuilder.CreateIndex(
                name: "iX_ServiceStatusHistory_serviceId",
                table: "ServiceStatusHistory",
                column: "serviceId");

            migrationBuilder.CreateIndex(
                name: "UX_User_email",
                table: "User",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "iX_UserSession_userId",
                table: "UserSession",
                column: "userId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectRequest");

            migrationBuilder.DropTable(
                name: "Proposal");

            migrationBuilder.DropTable(
                name: "Review");

            migrationBuilder.DropTable(
                name: "ServiceArea");

            migrationBuilder.DropTable(
                name: "ServiceStatusHistory");

            migrationBuilder.DropTable(
                name: "UserSession");

            migrationBuilder.DropTable(
                name: "ProfessionalProfile");

            migrationBuilder.DropTable(
                name: "Service");

            migrationBuilder.DropTable(
                name: "ServiceCategory");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
