using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryOperationalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_AutomationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SettingsKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ScheduleMode = table.Column<int>(type: "INTEGER", nullable: false),
                    NightlyLocalTime = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IdleMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduledLocalTimes = table.Column<string>(type: "TEXT", nullable: false),
                    AutoIngestProjectStructure = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoIngestProcessRuntime = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoConsolidateAfterIngestion = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedByActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AutomationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ExternalSourceIngestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Locator = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ContentLength = table.Column<long>(type: "INTEGER", nullable: false),
                    ProgressPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ExternalSourceIngestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_SourceManifests_SourceManifestId",
                        column: x => x.SourceManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AutomationSettings_SettingsKey",
                table: "CognitiveMemory_AutomationSettings",
                column: "SettingsKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_EvidenceAnchorId",
                table: "CognitiveMemory_ExternalSourceIngestions",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_ProjectId_SourceKind_CreatedAtUtc",
                table: "CognitiveMemory_ExternalSourceIngestions",
                columns: new[] { "ProjectId", "SourceKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_SourceItemId",
                table: "CognitiveMemory_ExternalSourceIngestions",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_SourceManifestId",
                table: "CognitiveMemory_ExternalSourceIngestions",
                column: "SourceManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_Status_UpdatedAtUtc",
                table: "CognitiveMemory_ExternalSourceIngestions",
                columns: new[] { "Status", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_AutomationSettings");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ExternalSourceIngestions");
        }
    }
}
