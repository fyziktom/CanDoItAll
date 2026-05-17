using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingsKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ScheduleMode = table.Column<int>(type: "integer", nullable: false),
                    NightlyLocalTime = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IdleMinutes = table.Column<int>(type: "integer", nullable: false),
                    ScheduledLocalTimes = table.Column<string>(type: "TEXT", nullable: false),
                    AutoIngestProjectStructure = table.Column<bool>(type: "boolean", nullable: false),
                    AutoIngestProcessRuntime = table.Column<bool>(type: "boolean", nullable: false),
                    AutoConsolidateAfterIngestion = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AutomationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ExternalSourceIngestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    StatusMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ExternalSourceIngestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_Ev~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_So~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_S~1",
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
                name: "IX_CognitiveMemory_ExternalSourceIngestions_ProjectId_SourceKi~",
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
