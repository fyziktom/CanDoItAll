using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessDecisionPolicyAdmission : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "DecisionDataPolicyJson",
                table: "process_runtime_step_assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_runtime_step_assignments_decision_policy",
                table: "process_runtime_step_assignments",
                sql: "\"DecisionDataPolicyJson\" IS NULL OR octet_length(\"DecisionDataPolicyJson\") BETWEEN 1 AND 32768");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE process_runtime_step_assignments IN ACCESS EXCLUSIVE MODE;
                DO $policy$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_runtime_step_assignments WHERE "DecisionDataPolicyJson" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot downgrade a retained Process decision policy.';
                    END IF;
                END $policy$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_process_runtime_step_assignments_decision_policy",
                table: "process_runtime_step_assignments");

            migrationBuilder.DropColumn(
                name: "DecisionDataPolicyJson",
                table: "process_runtime_step_assignments");
        }
    }
}
