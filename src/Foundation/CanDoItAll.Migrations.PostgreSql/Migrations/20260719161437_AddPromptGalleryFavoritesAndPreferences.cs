using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptGalleryFavoritesAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_UpdatedAtUtc_Title_Id",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.AddColumn<bool>(
                name: "IsPreferred",
                table: "Prompts_PromptSupportedProviderModels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "Prompts_PromptArtifacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptSupportedProviderModels_PromptArtifactId",
                table: "Prompts_PromptSupportedProviderModels",
                column: "PromptArtifactId",
                unique: true,
                filter: "\"IsPreferred\"");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_IsFavorite_UpdatedAtUtc_~",
                table: "Prompts_PromptArtifacts",
                columns: new[] { "IsArchived", "IsFavorite", "UpdatedAtUtc", "Title", "Id" },
                descending: new[] { false, true, true, false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptSupportedProviderModels_PromptArtifactId",
                table: "Prompts_PromptSupportedProviderModels");

            migrationBuilder.DropIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_IsFavorite_UpdatedAtUtc_~",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.DropColumn(
                name: "IsPreferred",
                table: "Prompts_PromptSupportedProviderModels");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "Prompts_PromptArtifacts");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_UpdatedAtUtc_Title_Id",
                table: "Prompts_PromptArtifacts",
                columns: new[] { "IsArchived", "UpdatedAtUtc", "Title", "Id" },
                descending: new[] { false, true, false, false });
        }
    }
}
