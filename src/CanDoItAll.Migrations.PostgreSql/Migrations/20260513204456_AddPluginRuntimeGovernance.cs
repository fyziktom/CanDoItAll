using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginRuntimeGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plugins_CapabilityGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Capability = table.Column<int>(type: "integer", nullable: false),
                    RecipeId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ScopeKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RiskKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_CapabilityGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins_Connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HealthStatus = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_Connections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_CapabilityGrants_PluginId_Capability_RecipeId_Scope~",
                table: "Plugins_CapabilityGrants",
                columns: new[] { "PluginId", "Capability", "RecipeId", "ScopeKind", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_CapabilityGrants_PluginId_State_UpdatedAtUtc",
                table: "Plugins_CapabilityGrants",
                columns: new[] { "PluginId", "State", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Connections_PluginId_ConnectionKey",
                table: "Plugins_Connections",
                columns: new[] { "PluginId", "ConnectionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Connections_PluginId_ConnectionKey_DisplayName",
                table: "Plugins_Connections",
                columns: new[] { "PluginId", "ConnectionKey", "DisplayName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plugins_CapabilityGrants");

            migrationBuilder.DropTable(
                name: "Plugins_Connections");
        }
    }
}
