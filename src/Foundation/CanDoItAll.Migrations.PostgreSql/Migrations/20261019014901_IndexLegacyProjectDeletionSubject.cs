using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class IndexLegacyProjectDeletionSubject : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectDeletion_Subject",
                table: "Workbench_ProjectCrossModuleMutations",
                columns: new[] { "ProjectId", "Id" },
                filter: "\"MutationKind\" = 4");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropIndex(
                name: "IX_Workbench_ProjectDeletion_Subject",
                table: "Workbench_ProjectCrossModuleMutations");
        }
    }
}
