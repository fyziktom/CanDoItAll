using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessArtifactExpectationLinkage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArtifactExpectationId",
                table: "Processes_ArtifactRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_ArtifactExpectationId",
                table: "Processes_ArtifactRecords",
                column: "ArtifactExpectationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ArtifactRecords_Processes_ArtifactExpectations_Ar~",
                table: "Processes_ArtifactRecords",
                column: "ArtifactExpectationId",
                principalTable: "Processes_ArtifactExpectations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Processes_ArtifactRecords_Processes_ArtifactExpectations_Ar~",
                table: "Processes_ArtifactRecords");

            migrationBuilder.DropIndex(
                name: "IX_Processes_ArtifactRecords_ArtifactExpectationId",
                table: "Processes_ArtifactRecords");

            migrationBuilder.DropColumn(
                name: "ArtifactExpectationId",
                table: "Processes_ArtifactRecords");
        }
    }
}
