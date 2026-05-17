using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryRecallOrchestrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AnswerGateDecisionId",
                table: "CognitiveMemory_RecallTraces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContextPackId",
                table: "CognitiveMemory_RecallTraces",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecallMode",
                table: "CognitiveMemory_RecallTraces",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryChannelKind = table.Column<int>(type: "integer", nullable: false),
                    DecisionKind = table.Column<int>(type: "integer", nullable: false),
                    ExclusionReasonKind = table.Column<int>(type: "integer", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryKind = table.Column<int>(type: "integer", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayRankProjection = table.Column<double>(type: "double precision", nullable: true),
                    HasSourceDetail = table.Column<bool>(type: "boolean", nullable: false),
                    SourceRedacted = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedTokenCount = table.Column<int>(type: "integer", nullable: false),
                    SourceRefCount = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelTraceJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_Claims_Cla~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_ContextFra~",
                        column: x => x.ContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_EvidenceAn~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_RecallTrac~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_Records_Me~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_ScoreEvalu~",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_SourceItem~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_WorkspaceF~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallContextPacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterBudget = table.Column<int>(type: "integer", nullable: false),
                    RenderedCharacterCount = table.Column<int>(type: "integer", nullable: false),
                    SectionCount = table.Column<int>(type: "integer", nullable: false),
                    SourceRefCount = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallContextPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextPacks_CognitiveMemory_RecallTr~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextPacks_CognitiveMemory_Workspac~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallTraceStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    StageKind = table.Column<int>(type: "integer", nullable: false),
                    ChannelKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    SelectedCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedCount = table.Column<int>(type: "integer", nullable: false),
                    LimitingBudget = table.Column<int>(type: "integer", nullable: true),
                    ProviderTrace = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallTraceStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallTraceStages_CognitiveMemory_RecallTra~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallContextSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionKind = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    SectionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    EstimatedTokenCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallContextSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Claim~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Recal~",
                        column: x => x.ContextPackId,
                        principalTable: "CognitiveMemory_RecallContextPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Reca~1",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Recor~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Sourc~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallSourceRefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    QuoteHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    IncludedInContext = table.Column<bool>(type: "boolean", nullable: false),
                    ExclusionReasonKind = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallSourceRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_Claims_Cla~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_EvidenceAn~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_RecallCont~",
                        column: x => x.ContextPackId,
                        principalTable: "CognitiveMemory_RecallContextPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_RecallTrac~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_Records_Me~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_SourceItem~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_AnswerGateDecisionId",
                table: "CognitiveMemory_RecallTraces",
                column: "AnswerGateDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_ContextPackId",
                table: "CognitiveMemory_RecallTraces",
                column: "ContextPackId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_ProjectId_RecallMode_Outcome_S~",
                table: "CognitiveMemory_RecallTraces",
                columns: new[] { "ProjectId", "RecallMode", "Outcome", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ClaimId",
                table: "CognitiveMemory_RecallCandidates",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ContextFrameId",
                table: "CognitiveMemory_RecallCandidates",
                column: "ContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_EvidenceAnchorId",
                table: "CognitiveMemory_RecallCandidates",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_MemoryRecordId",
                table: "CognitiveMemory_RecallCandidates",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ProjectId_MemoryRecordId_D~",
                table: "CognitiveMemory_RecallCandidates",
                columns: new[] { "ProjectId", "MemoryRecordId", "DecisionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ProjectId_PrimaryChannelKi~",
                table: "CognitiveMemory_RecallCandidates",
                columns: new[] { "ProjectId", "PrimaryChannelKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_RecallTraceId_DecisionKind~",
                table: "CognitiveMemory_RecallCandidates",
                columns: new[] { "RecallTraceId", "DecisionKind", "PrimaryChannelKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ScoreEvaluationTraceId",
                table: "CognitiveMemory_RecallCandidates",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_SourceItemId",
                table: "CognitiveMemory_RecallCandidates",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_WorkspaceFrameId",
                table: "CognitiveMemory_RecallCandidates",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextPacks_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_RecallContextPacks",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextPacks_RecallTraceId",
                table: "CognitiveMemory_RecallContextPacks",
                column: "RecallTraceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextPacks_WorkspaceFrameId",
                table: "CognitiveMemory_RecallContextPacks",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_ClaimId",
                table: "CognitiveMemory_RecallContextSections",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_ContextPackId_Sequence",
                table: "CognitiveMemory_RecallContextSections",
                columns: new[] { "ContextPackId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_MemoryRecordId",
                table: "CognitiveMemory_RecallContextSections",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_ProjectId_SectionKind~",
                table: "CognitiveMemory_RecallContextSections",
                columns: new[] { "ProjectId", "SectionKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_RecallTraceId_Section~",
                table: "CognitiveMemory_RecallContextSections",
                columns: new[] { "RecallTraceId", "SectionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_SourceItemId",
                table: "CognitiveMemory_RecallContextSections",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_ClaimId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_ContextPackId_IncludedInCo~",
                table: "CognitiveMemory_RecallSourceRefs",
                columns: new[] { "ContextPackId", "IncludedInContext" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_EvidenceAnchorId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_MemoryRecordId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_ProjectId_SourceSystem_Inc~",
                table: "CognitiveMemory_RecallSourceRefs",
                columns: new[] { "ProjectId", "SourceSystem", "IncludedInContext" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_RecallTraceId_MemoryRecord~",
                table: "CognitiveMemory_RecallSourceRefs",
                columns: new[] { "RecallTraceId", "MemoryRecordId", "IncludedInContext" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_SourceItemId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraceStages_ProjectId_StageKind_Statu~",
                table: "CognitiveMemory_RecallTraceStages",
                columns: new[] { "ProjectId", "StageKind", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraceStages_RecallTraceId_StageKind_C~",
                table: "CognitiveMemory_RecallTraceStages",
                columns: new[] { "RecallTraceId", "StageKind", "ChannelKind" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallContextSections");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallSourceRefs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallTraceStages");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallContextPacks");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_RecallTraces_AnswerGateDecisionId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_RecallTraces_ContextPackId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_RecallTraces_ProjectId_RecallMode_Outcome_S~",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "AnswerGateDecisionId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "ContextPackId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "RecallMode",
                table: "CognitiveMemory_RecallTraces");
        }
    }
}
