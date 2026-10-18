using LocalHub.Domain.Accounts;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LocalHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    table.PrimaryKey("PK_ServiceCategory", x => x.serviceCategoryId);
                    table.CheckConstraint("CK_ServiceCategory_name", "btrim(\"name\") <> ''");
                });

            // Unique name, ignoring upper and lower case (RF-F003-01, rule 2), as in database/01-esquema.sql.
            // EF Core cannot describe an index on an expression, so it is created with SQL.
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX \"UX_ServiceCategory_name\" ON \"ServiceCategory\" (lower(\"name\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"UX_ServiceCategory_name\";");

            migrationBuilder.DropTable(
                name: "ServiceCategory");
        }
    }
}
