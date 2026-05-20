using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuratorSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuratorTurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedMemoryRecordIdsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsolidationCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    PriorityScore = table.Column<double>(type: "double precision", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CorrectionText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorCapturedImprovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CuratorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RuntimeMode = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    AllowRestrictedContent = table.Column<bool>(type: "boolean", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AgentChatSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlgorithmVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TurnCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CuratorTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuratorSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    RuntimeMode = table.Column<int>(type: "integer", nullable: false),
                    UserMessage = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CuratorResponse = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncludedMemoryRecordIdsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CaptureCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorTurns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ConsolidationCa~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "ConsolidationCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_CuratorTurnId_C~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "CuratorTurnId", "CaptureKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_MutationCommand~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "MutationCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_Captu~",
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
                name: "IX_CognitiveMemory_CuratorSessions_ProjectId_Status_UpdatedAtU~",
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
