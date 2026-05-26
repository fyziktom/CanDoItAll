using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class ProcessArtifactExplicitOutputMappings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubprocessChildArtifactExpectationId",
                table: "Processes_ArtifactExpectations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowOutputId",
                table: "Processes_ArtifactExpectations",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkflowOutputKind",
                table: "Processes_ArtifactExpectations",
                type: "character varying(48)",
                maxLength: 48,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowOutputName",
                table: "Processes_ArtifactExpectations",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactExpectations_SubprocessChildArtifactExpec~",
                table: "Processes_ArtifactExpectations",
                column: "SubprocessChildArtifactExpectationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processes_ArtifactExpectations_SubprocessChildArtifactExpec~",
                table: "Processes_ArtifactExpectations");

            migrationBuilder.DropColumn(
                name: "SubprocessChildArtifactExpectationId",
                table: "Processes_ArtifactExpectations");

            migrationBuilder.DropColumn(
                name: "WorkflowOutputId",
                table: "Processes_ArtifactExpectations");

            migrationBuilder.DropColumn(
                name: "WorkflowOutputKind",
                table: "Processes_ArtifactExpectations");

            migrationBuilder.DropColumn(
                name: "WorkflowOutputName",
                table: "Processes_ArtifactExpectations");
        }
    }
}
