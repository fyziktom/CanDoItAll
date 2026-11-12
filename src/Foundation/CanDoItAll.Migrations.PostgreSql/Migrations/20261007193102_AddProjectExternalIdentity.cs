using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProjectExternalIdentity : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "ExternalKey",
                table: "Projects_Projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalNamespace",
                table: "Projects_Projects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Projects_ExternalIdentity",
                table: "Projects_Projects",
                columns: new[] { "ExternalNamespace", "ExternalKey" },
                unique: true,
                filter: "\"ExternalNamespace\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_ExternalIdentity",
                table: "Projects_Projects",
                sql: "(\"ExternalNamespace\" IS NULL AND \"ExternalKey\" IS NULL) OR\n(\"ExternalNamespace\" IS NOT NULL AND \"ExternalKey\" IS NOT NULL AND\n \"ExternalNamespace\" COLLATE \"C\" ~ '^[a-z0-9]([a-z0-9._-]{0,98}[a-z0-9])?$' AND\n \"ExternalKey\" COLLATE \"C\" ~ '^[a-z0-9]([a-z0-9._-]{0,98}[a-z0-9])?$')");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    LOCK TABLE "Projects_Projects" IN ACCESS EXCLUSIVE MODE;
                    IF EXISTS (SELECT 1 FROM "Projects_Projects" WHERE "ExternalNamespace" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot remove project external identity columns while assigned identities exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "UX_Projects_ExternalIdentity",
                table: "Projects_Projects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_ExternalIdentity",
                table: "Projects_Projects");

            migrationBuilder.DropColumn(
                name: "ExternalKey",
                table: "Projects_Projects");

            migrationBuilder.DropColumn(
                name: "ExternalNamespace",
                table: "Projects_Projects");
        }
    }
}
