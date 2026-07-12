using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProcessWorkflowExecutorBinding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowId",
                table: "process_runtime_step_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowOutputMapping",
                table: "process_runtime_step_assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowVersionId",
                table: "process_runtime_step_assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_step_assignments_WorkflowId_WorkflowVersion~",
                table: "process_runtime_step_assignments",
                columns: new[] { "WorkflowId", "WorkflowVersionId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_process_runtime_step_assignments_WorkflowId_WorkflowVersion~",
                table: "process_runtime_step_assignments");

            migrationBuilder.DropColumn(
                name: "WorkflowId",
                table: "process_runtime_step_assignments");

            migrationBuilder.DropColumn(
                name: "WorkflowOutputMapping",
                table: "process_runtime_step_assignments");

            migrationBuilder.DropColumn(
                name: "WorkflowVersionId",
                table: "process_runtime_step_assignments");
        }
    }
}
