using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
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
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowRestrictedContent",
                table: "CognitiveMemory_ProbeSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingProfileId",
                table: "CognitiveMemory_ProbeSessions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectionCollectionName",
                table: "CognitiveMemory_ProbeSessions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectionProfileId",
                table: "CognitiveMemory_ProbeSessions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RiskLevel",
                table: "CognitiveMemory_ProbeSessions",
                type: "integer",
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
