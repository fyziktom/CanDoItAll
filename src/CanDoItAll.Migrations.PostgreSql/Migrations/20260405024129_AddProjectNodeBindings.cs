using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectNodeBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workbench_ProjectNodeBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Route = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    ExternalArtifactKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExternalArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaRelativePath = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    MediaContentType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MediaOriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StorageObjectReferenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectNodeBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workbench_ProjectNodeBindings_Workbench_ProjectObjects_Proj~",
                        column: x => x.ProjectObjectId,
                        principalTable: "Workbench_ProjectObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectNodeReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceKind = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectNodeReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workbench_ProjectNodeReferences_Workbench_ProjectObjects_Pr~",
                        column: x => x.ProjectObjectId,
                        principalTable: "Workbench_ProjectObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeBindings_ProjectObjectId",
                table: "Workbench_ProjectNodeBindings",
                column: "ProjectObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceK~1",
                table: "Workbench_ProjectNodeReferences",
                columns: new[] { "ProjectObjectId", "ReferenceKind", "ReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKi~",
                table: "Workbench_ProjectNodeReferences",
                columns: new[] { "ProjectObjectId", "ReferenceKind", "OrderIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workbench_ProjectNodeBindings");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectNodeReferences");
        }
    }
}
