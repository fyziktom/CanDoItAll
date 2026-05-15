using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginRuntimeLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Plugins_Logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PluginId = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    PackageId = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    WorkflowExecutorId = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    StreamKind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    OperationKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_Logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Logs_PackageId_CreatedAtUtc",
                table: "Plugins_Logs",
                columns: new[] { "PackageId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Logs_StreamKind_PluginId_CreatedAtUtc",
                table: "Plugins_Logs",
                columns: new[] { "StreamKind", "PluginId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Plugins_Logs");
        }
    }
}
