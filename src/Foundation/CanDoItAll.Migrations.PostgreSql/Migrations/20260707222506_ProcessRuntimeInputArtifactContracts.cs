using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ProcessRuntimeInputArtifactContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProducedArtifactSlotIds",
                table: "process_runtime_steps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequiredRuntimeToolNamesJson",
                table: "process_runtime_steps",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "process_runtime_input_artifacts",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerStepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Availability = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProducerStepInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_input_artifacts", x => new { x.RunId, x.ConsumerStepInstanceId, x.RequiredSlotId, x.ConnectionHash });
                    table.ForeignKey(
                        name: "FK_process_runtime_input_artifacts_process_runtime_states_RunId",
                        column: x => x.RunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_input_artifacts_RunId_ConsumerStepInstanceI~",
                table: "process_runtime_input_artifacts",
                columns: new[] { "RunId", "ConsumerStepInstanceId", "Availability" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_input_artifacts_RunId_ProducerStepInstanceId",
                table: "process_runtime_input_artifacts",
                columns: new[] { "RunId", "ProducerStepInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_input_artifacts_RunId_RequiredSlotId",
                table: "process_runtime_input_artifacts",
                columns: new[] { "RunId", "RequiredSlotId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "process_runtime_input_artifacts");

            migrationBuilder.DropColumn(
                name: "ProducedArtifactSlotIds",
                table: "process_runtime_steps");

            migrationBuilder.DropColumn(
                name: "RequiredRuntimeToolNamesJson",
                table: "process_runtime_steps");
        }
    }
}
