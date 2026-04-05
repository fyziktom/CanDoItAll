using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkbenchProjectionLayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workbench_ProjectProjectionLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    PositionX = table.Column<double>(type: "REAL", nullable: false),
                    PositionY = table.Column<double>(type: "REAL", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectProjectionLayouts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectProjectionLayouts_ProjectId_NodeKey",
                table: "Workbench_ProjectProjectionLayouts",
                columns: new[] { "ProjectId", "NodeKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workbench_ProjectProjectionLayouts");
        }
    }
}
