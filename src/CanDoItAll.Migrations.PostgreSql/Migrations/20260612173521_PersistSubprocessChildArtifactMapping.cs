using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PersistSubprocessChildArtifactMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubprocessChildArtifactTitle",
                table: "Processes_ArtifactExpectations",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubprocessChildStepKey",
                table: "Processes_ArtifactExpectations",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubprocessChildArtifactTitle",
                table: "Processes_ArtifactExpectations");

            migrationBuilder.DropColumn(
                name: "SubprocessChildStepKey",
                table: "Processes_ArtifactExpectations");
        }
    }
}
