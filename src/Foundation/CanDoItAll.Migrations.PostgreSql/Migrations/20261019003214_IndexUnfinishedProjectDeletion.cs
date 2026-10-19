using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class IndexUnfinishedProjectDeletion : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_UnfinishedDeletion",
                table: "Workbench_ProjectCrossModuleMutations",
                column: "Id",
                filter: "\"MutationKind\" = 4 AND \"Status\" <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DeletionCompletions_Awaiting",
                table: "Projects_DeletionCompletions",
                column: "CompletionId",
                filter: "\"ReleaseState\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AF_ProjectAccessRevocations_Unfinished",
                table: "AgentFramework_ProjectAccessRevocations",
                column: "Id",
                filter: "\"Status\" <> 4");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_UnfinishedDeletion",
                table: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropIndex(
                name: "IX_Projects_DeletionCompletions_Awaiting",
                table: "Projects_DeletionCompletions");

            migrationBuilder.DropIndex(
                name: "IX_AF_ProjectAccessRevocations_Unfinished",
                table: "AgentFramework_ProjectAccessRevocations");
        }
    }
}
