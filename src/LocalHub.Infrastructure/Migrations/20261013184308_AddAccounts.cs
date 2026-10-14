using System;
using LocalHub.Domain.Accounts;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LocalHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:activation_status", "ACTIVE,INACTIVE")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,CLIENT,PROVIDER");

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    userId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    passwordHash =
                        table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<UserRole>(type: "user_role", nullable: false),
                    status = table.Column<ActivationStatus>(type: "activation_status", nullable: false),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.userId);
                    table.CheckConstraint("CK_User_coordinates", "(\"latitude\" IS NULL) = (\"longitude\" IS NULL)");
                    table.CheckConstraint("CK_User_latitude", "\"latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_User_longitude", "\"longitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_User_name", "btrim(\"name\") <> ''");
                });

            migrationBuilder.CreateTable(
                name: "UserSession",
                columns: table => new
                {
                    sessionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    userId = table.Column<int>(type: "integer", nullable: false),
                    createdAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSession", x => x.sessionId);
                    table.CheckConstraint("CK_UserSession_expiresAt",
                        "\"expiresAt\" > \"createdAt\" AND \"expiresAt\" <= \"createdAt\" + interval '8 hours'");
                    table.CheckConstraint("CK_UserSession_revokedAt",
                        "\"revokedAt\" IS NULL OR \"revokedAt\" >= \"createdAt\"");
                    table.ForeignKey(
                        name: "FK_UserSession_User",
                        column: x => x.userId,
                        principalTable: "User",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSession_userId",
                table: "UserSession",
                column: "userId");

            migrationBuilder.Sql("CREATE UNIQUE INDEX \"UX_User_email\" ON \"User\" (lower(\"email\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_User_email\";");

            migrationBuilder.DropTable(
                name: "UserSession");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.Sql("DROP TYPE IF EXISTS activation_status;");
            migrationBuilder.Sql("DROP TYPE IF EXISTS user_role;");
        }

    }
}