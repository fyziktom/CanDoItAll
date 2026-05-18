using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryModelAccessSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedProviderProfileIds",
                table: "CognitiveMemory_AutomationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAgentId",
                table: "CognitiveMemory_AutomationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultProviderProfileId",
                table: "CognitiveMemory_AutomationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModelAccessMode",
                table: "CognitiveMemory_AutomationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedProviderProfileIds",
                table: "CognitiveMemory_AutomationSettings");

            migrationBuilder.DropColumn(
                name: "DefaultAgentId",
                table: "CognitiveMemory_AutomationSettings");

            migrationBuilder.DropColumn(
                name: "DefaultProviderProfileId",
                table: "CognitiveMemory_AutomationSettings");

            migrationBuilder.DropColumn(
                name: "ModelAccessMode",
                table: "CognitiveMemory_AutomationSettings");
        }
    }
}
