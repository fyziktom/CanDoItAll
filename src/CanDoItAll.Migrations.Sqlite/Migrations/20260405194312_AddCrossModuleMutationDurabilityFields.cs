using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossModuleMutationDurabilityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalState",
                table: "Workbench_ProjectCrossModuleMutations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "Workbench_ProjectCrossModuleMutations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAtUtc",
                table: "Workbench_ProjectCrossModuleMutations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAtUtc",
                table: "Workbench_ProjectCrossModuleMutations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ApprovalState_Status_UpdatedAtUtc",
                table: "Workbench_ProjectCrossModuleMutations",
                columns: new[] { "ProjectId", "ApprovalState", "Status", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ApprovalState_Status_UpdatedAtUtc",
                table: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropColumn(
                name: "ApprovalState",
                table: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropColumn(
                name: "LastAttemptAtUtc",
                table: "Workbench_ProjectCrossModuleMutations");
        }
    }
}
