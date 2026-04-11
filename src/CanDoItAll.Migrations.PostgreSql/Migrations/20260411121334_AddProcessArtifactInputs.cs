using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessArtifactInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Processes_StepArtifactInputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactExpectationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepArtifactInputs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_ArtifactExpectationId",
                table: "Processes_StepArtifactInputs",
                column: "ArtifactExpectationId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId",
                table: "Processes_StepArtifactInputs",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId_ArtifactExpec~",
                table: "Processes_StepArtifactInputs",
                columns: new[] { "StepDefinitionId", "ArtifactExpectationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId_DisplayOrder",
                table: "Processes_StepArtifactInputs",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_StepArtifactInputs");
        }
    }
}
