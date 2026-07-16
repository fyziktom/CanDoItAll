using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProjectPlanQueryIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "Workbench_ProjectObjects"
                SET "ObjectSubtype" = 'task'
                WHERE "ObjectType" = {(int)CanDoItAll.SharedKernel.ProjectObjectType.WorkItem}
                  AND LOWER(BTRIM("ObjectSubtype")) = 'task';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectObjects_ProjectId_ObjectType_ObjectSubtype~",
                table: "Workbench_ProjectObjects",
                columns: new[] { "ProjectId", "ObjectType", "ObjectSubtype", "IsSystemManaged" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectObjects_ProjectId_ParentNodeKey_ObjectType~",
                table: "Workbench_ProjectObjects",
                columns: new[] { "ProjectId", "ParentNodeKey", "ObjectType", "IsSystemManaged" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectObjectLinks_ProjectId_LinkKind_IsSystemMan~",
                table: "Workbench_ProjectObjectLinks",
                columns: new[] { "ProjectId", "LinkKind", "IsSystemManaged" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_ProjectId_AssignmentKind_Node~",
                table: "CrmHr_ProjectPartyAssignments",
                columns: new[] { "ProjectId", "AssignmentKind", "NodeKey" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workbench_ProjectObjects_ProjectId_ObjectType_ObjectSubtype~",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropIndex(
                name: "IX_Workbench_ProjectObjects_ProjectId_ParentNodeKey_ObjectType~",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropIndex(
                name: "IX_Workbench_ProjectObjectLinks_ProjectId_LinkKind_IsSystemMan~",
                table: "Workbench_ProjectObjectLinks");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_ProjectId_AssignmentKind_Node~",
                table: "CrmHr_ProjectPartyAssignments");
        }
    }
}
