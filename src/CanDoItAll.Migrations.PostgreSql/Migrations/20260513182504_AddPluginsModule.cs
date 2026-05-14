using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plugins_Installations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PackageId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DisplayNameSnapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ManifestSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InstalledBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    InstalledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_Installations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Installations_IsEnabled_UpdatedAtUtc",
                table: "Plugins_Installations",
                columns: new[] { "IsEnabled", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Installations_PluginId",
                table: "Plugins_Installations",
                column: "PluginId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plugins_Installations");
        }
    }
}
