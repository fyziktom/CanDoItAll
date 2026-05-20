using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryCuratorConversationDepth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConversationDepth",
                table: "CognitiveMemory_CuratorTurns",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ConversationDepth",
                table: "CognitiveMemory_CuratorSessions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ConversationDepth",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorSessions_ProjectId_RuntimeMode_Conve~",
                table: "CognitiveMemory_CuratorSessions",
                columns: new[] { "ProjectId", "RuntimeMode", "ConversationDepth", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_CuratorSessions_ProjectId_RuntimeMode_Conve~",
                table: "CognitiveMemory_CuratorSessions");

            migrationBuilder.DropColumn(
                name: "ConversationDepth",
                table: "CognitiveMemory_CuratorTurns");

            migrationBuilder.DropColumn(
                name: "ConversationDepth",
                table: "CognitiveMemory_CuratorSessions");

            migrationBuilder.DropColumn(
                name: "ConversationDepth",
                table: "CognitiveMemory_CuratorCapturedImprovements");
        }
    }
}
