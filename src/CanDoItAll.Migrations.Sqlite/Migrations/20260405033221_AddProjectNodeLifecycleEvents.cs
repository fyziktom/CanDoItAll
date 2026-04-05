using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectNodeLifecycleEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workbench_ProjectNodeLifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectObjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TransitionMode = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceFamily = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetFamily = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceObjectType = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceObjectSubtype = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TargetObjectType = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetObjectSubtype = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    TargetSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectNodeLifecycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workbench_ProjectNodeLifecycleEvents_Workbench_ProjectObjects_ProjectObjectId",
                        column: x => x.ProjectObjectId,
                        principalTable: "Workbench_ProjectObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeLifecycleEvents_ProjectId_NodeKey_OccurredAtUtc",
                table: "Workbench_ProjectNodeLifecycleEvents",
                columns: new[] { "ProjectId", "NodeKey", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeLifecycleEvents_ProjectObjectId",
                table: "Workbench_ProjectNodeLifecycleEvents",
                column: "ProjectObjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workbench_ProjectNodeLifecycleEvents");
        }
    }
}
