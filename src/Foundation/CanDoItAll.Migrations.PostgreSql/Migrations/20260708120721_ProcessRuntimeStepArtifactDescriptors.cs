using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ProcessRuntimeStepArtifactDescriptors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtifactDescriptorsJson",
                table: "process_runtime_steps",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "SubprocessArtifactMappingsJson",
                table: "process_runtime_steps",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtifactDescriptorsJson",
                table: "process_runtime_steps");

            migrationBuilder.DropColumn(
                name: "SubprocessArtifactMappingsJson",
                table: "process_runtime_steps");
        }
    }
}
