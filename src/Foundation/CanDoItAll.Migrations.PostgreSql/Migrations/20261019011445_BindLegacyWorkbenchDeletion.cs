using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class BindLegacyWorkbenchDeletion : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<Guid>(
                name: "CompletionId",
                table: "Workbench_ProjectCrossModuleMutations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletionNamespaceId",
                table: "Workbench_ProjectCrossModuleMutations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Workbench_ProjectMutation_CompletionBinding",
                table: "Workbench_ProjectCrossModuleMutations",
                sql: "(\"CompletionNamespaceId\" IS NULL AND \"CompletionId\" IS NULL) OR (\"CompletionNamespaceId\" IS NOT NULL AND \"CompletionId\" IS NOT NULL AND \"CompletionNamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CompletionId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"MutationKind\" = 4)");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Workbench_ProjectMutation_CompletionBinding",
                table: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropColumn(
                name: "CompletionId",
                table: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropColumn(
                name: "CompletionNamespaceId",
                table: "Workbench_ProjectCrossModuleMutations");
        }
    }
}
