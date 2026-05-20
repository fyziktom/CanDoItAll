using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryCuratorConversation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CuratorCapturedImprovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CuratorSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CuratorTurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaptureKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContextPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AffectedMemoryRecordIdsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConsolidationCandidateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AppliedMemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ConfidenceScore = table.Column<double>(type: "REAL", nullable: false),
                    PriorityScore = table.Column<double>(type: "REAL", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CorrectionText = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorCapturedImprovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CuratorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RuntimeMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    AccessLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowRestrictedContent = table.Column<bool>(type: "INTEGER", nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    AgentChatSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TurnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CuratorTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CuratorSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    RuntimeMode = table.Column<int>(type: "INTEGER", nullable: false),
                    UserMessage = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CuratorResponse = table.Column<string>(type: "TEXT", maxLength: 12000, nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContextPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IncludedMemoryRecordIdsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModelId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    CaptureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorTurns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ConsolidationCandidateId",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "ConsolidationCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_CuratorTurnId_CaptureKind",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "CuratorTurnId", "CaptureKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_MutationCommandId",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "MutationCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_CaptureKind_Status_CreatedAtUtc",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "ProjectId", "CaptureKind", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_RecallTraceId",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorSessions_AgentChatSessionId",
                table: "CognitiveMemory_CuratorSessions",
                column: "AgentChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorSessions_ProjectId_RuntimeMode_Status",
                table: "CognitiveMemory_CuratorSessions",
                columns: new[] { "ProjectId", "RuntimeMode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorSessions_ProjectId_Status_UpdatedAtUtc",
                table: "CognitiveMemory_CuratorSessions",
                columns: new[] { "ProjectId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorTurns_CuratorSessionId_Sequence",
                table: "CognitiveMemory_CuratorTurns",
                columns: new[] { "CuratorSessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorTurns_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_CuratorTurns",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorTurns_RecallTraceId",
                table: "CognitiveMemory_CuratorTurns",
                column: "RecallTraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CuratorSessions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CuratorTurns");
        }
    }
}
