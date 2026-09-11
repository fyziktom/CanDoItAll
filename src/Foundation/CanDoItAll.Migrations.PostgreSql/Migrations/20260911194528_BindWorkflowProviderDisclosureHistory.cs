using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class BindWorkflowProviderDisclosureHistory : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {

        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE "AgentFramework_WorkflowEvents" IN ACCESS EXCLUSIVE MODE;
                DO $disclosure_guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AgentFramework_WorkflowEvents" WHERE "Kind" = 12) THEN
                        RAISE EXCEPTION 'Cannot downgrade while private Workflow provider-disclosure evidence is retained. Keep compatible readers and restore only a reviewed pre-disclosure backup if rollback is required.';
                    END IF;
                END
                $disclosure_guard$;
                """);

        }
    }
}
