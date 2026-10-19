using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LocalHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessionalProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    table.PrimaryKey("PK_ProfessionalProfile", x => x.professionalProfileId);
                    table.CheckConstraint("CK_ProfessionalProfile_averageRating", "\"averageRating\" BETWEEN 1.0 AND 5.0");
                    table.CheckConstraint("CK_ProfessionalProfile_contactDetails", "btrim(\"contactDetails\") <> ''");
                    table.CheckConstraint("CK_ProfessionalProfile_experience", "btrim(\"experience\") <> ''");
                    table.CheckConstraint("CK_ProfessionalProfile_introduction", "btrim(\"introduction\") <> ''");
                    table.CheckConstraint("CK_ProfessionalProfile_serviceRadiusKm", "\"serviceRadiusKm\" BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_ProfessionalProfile_User",
                        column: x => x.serviceProviderId,
                        principalTable: "User",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
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
                    table.PrimaryKey("PK_ServiceArea", x => new { x.serviceCategoryId, x.professionalProfileId });
                    table.ForeignKey(
                        name: "FK_ServiceArea_ProfessionalProfile",
                        column: x => x.professionalProfileId,
                        principalTable: "ProfessionalProfile",
                        principalColumn: "professionalProfileId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceArea_ServiceCategory",
                        column: x => x.serviceCategoryId,
                        principalTable: "ServiceCategory",
                        principalColumn: "serviceCategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_ProfessionalProfile_serviceProviderId",
                table: "ProfessionalProfile",
                column: "serviceProviderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceArea_professionalProfileId",
                table: "ServiceArea",
                column: "professionalProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceArea");

            migrationBuilder.DropTable(
                name: "ProfessionalProfile");
        }
    }
}
