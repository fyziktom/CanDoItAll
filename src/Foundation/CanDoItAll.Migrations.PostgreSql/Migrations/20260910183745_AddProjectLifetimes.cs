using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProjectLifetimes : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropIndex(
                name: "UX_AF_ProjectAccessRevocations_Project",
                table: "AgentFramework_ProjectAccessRevocations");

            migrationBuilder.AddColumn<bool>(
                name: "LegacyAgentAccessBindingEligible",
                table: "Projects_Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LifetimeId",
                table: "Projects_Projects",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.Sql("""
                UPDATE "Projects_Projects" SET "LegacyAgentAccessBindingEligible" = TRUE;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "DatabaseProfileId",
                table: "AgentFramework_ProjectAccessRevocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectLifetimeId",
                table: "AgentFramework_ProjectAccessRevocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Projects_ProjectRetirements",
                columns: table => new {
                    LifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RetiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Projects_ProjectRetirements", x => x.LifetimeId);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AF_ProjectAccessRevocations_Project",
                table: "AgentFramework_ProjectAccessRevocations",
                column: "ProjectId",
                unique: true,
                filter: "\"ProjectLifetimeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_AF_ProjectAccessRevocations_ProjectLifetime",
                table: "AgentFramework_ProjectAccessRevocations",
                columns: new[] { "ProjectId", "ProjectLifetimeId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AF_ProjectAccessRevocations_Lifetime",
                table: "AgentFramework_ProjectAccessRevocations",
                sql: "(\"DatabaseProfileId\" IS NULL AND \"ProjectLifetimeId\" IS NULL) OR (\"DatabaseProfileId\" IS NOT NULL AND \"ProjectLifetimeId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectRetirements_ProjectId_RetiredAtUtc",
                table: "Projects_ProjectRetirements",
                columns: new[] { "ProjectId", "RetiredAtUtc" });
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Projects_ProjectRetirements") OR
                        EXISTS (SELECT 1 FROM "AgentFramework_ProjectAccessRevocations" WHERE "ProjectLifetimeId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot remove retained project lifetime or access-recovery evidence. Keep this schema or restore a reviewed pre-admission backup without replaying effects.';
                    END IF;
                END
                $$;
                """);
            migrationBuilder.DropTable(
                name: "Projects_ProjectRetirements");

            migrationBuilder.DropIndex(
                name: "UX_AF_ProjectAccessRevocations_Project",
                table: "AgentFramework_ProjectAccessRevocations");

            migrationBuilder.DropIndex(
                name: "UX_AF_ProjectAccessRevocations_ProjectLifetime",
                table: "AgentFramework_ProjectAccessRevocations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AF_ProjectAccessRevocations_Lifetime",
                table: "AgentFramework_ProjectAccessRevocations");

            migrationBuilder.DropColumn(
                name: "LegacyAgentAccessBindingEligible",
                table: "Projects_Projects");

            migrationBuilder.DropColumn(
                name: "LifetimeId",
                table: "Projects_Projects");

            migrationBuilder.DropColumn(
                name: "DatabaseProfileId",
                table: "AgentFramework_ProjectAccessRevocations");

            migrationBuilder.DropColumn(
                name: "ProjectLifetimeId",
                table: "AgentFramework_ProjectAccessRevocations");

            migrationBuilder.CreateIndex(
                name: "UX_AF_ProjectAccessRevocations_Project",
                table: "AgentFramework_ProjectAccessRevocations",
                column: "ProjectId",
                unique: true);
        }
    }
}
