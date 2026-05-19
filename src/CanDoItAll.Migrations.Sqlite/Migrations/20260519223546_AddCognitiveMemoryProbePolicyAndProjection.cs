using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryProbePolicyAndProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessLevel",
                table: "CognitiveMemory_ProbeSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRestrictedContent",
                table: "CognitiveMemory_ProbeSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingProfileId",
                table: "CognitiveMemory_ProbeSessions",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectionCollectionName",
                table: "CognitiveMemory_ProbeSessions",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectionProfileId",
                table: "CognitiveMemory_ProbeSessions",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RiskLevel",
                table: "CognitiveMemory_ProbeSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "CognitiveMemory_ProbeSessions");

            migrationBuilder.DropColumn(
                name: "AllowRestrictedContent",
                table: "CognitiveMemory_ProbeSessions");

            migrationBuilder.DropColumn(
                name: "EmbeddingProfileId",
                table: "CognitiveMemory_ProbeSessions");

            migrationBuilder.DropColumn(
                name: "ProjectionCollectionName",
                table: "CognitiveMemory_ProbeSessions");

            migrationBuilder.DropColumn(
                name: "ProjectionProfileId",
                table: "CognitiveMemory_ProbeSessions");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "CognitiveMemory_ProbeSessions");
        }
    }
}
