using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class OptimizeDashboardActivityIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Projects_Projects_UpdatedAtUtc_Id",
                table: "Projects_Projects",
                columns: new[] { "UpdatedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_states_UpdatedAtUtc_RunId",
                table: "process_runtime_states",
                columns: new[] { "UpdatedAtUtc", "RunId" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowRuns_State_UpdatedAtUtc_RunId",
                table: "AgentFramework_WorkflowRuns",
                columns: new[] { "State", "UpdatedAtUtc", "RunId" },
                descending: new[] { false, true, true });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_Projects_UpdatedAtUtc_Id",
                table: "Projects_Projects");

            migrationBuilder.DropIndex(
                name: "IX_process_runtime_states_UpdatedAtUtc_RunId",
                table: "process_runtime_states");

            migrationBuilder.DropIndex(
                name: "IX_AgentFramework_WorkflowRuns_State_UpdatedAtUtc_RunId",
                table: "AgentFramework_WorkflowRuns");
        }
    }
}
