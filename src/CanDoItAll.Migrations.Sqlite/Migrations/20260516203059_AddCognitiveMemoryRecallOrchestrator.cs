using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContextPackId",
                table: "CognitiveMemory_RecallTraces",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecallMode",
                table: "CognitiveMemory_RecallTraces",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PrimaryChannelKind = table.Column<int>(type: "INTEGER", nullable: false),
                    DecisionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ExclusionReasonKind = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemoryKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContextFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScoreBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayRankProjection = table.Column<double>(type: "REAL", nullable: true),
                    HasSourceDetail = table.Column<bool>(type: "INTEGER", nullable: false),
                    SourceRedacted = table.Column<bool>(type: "INTEGER", nullable: false),
                    EstimatedTokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceRefCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelTraceJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_ContextFrames_ContextFrameId",
                        column: x => x.ContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_RecallTraces_RecallTraceId",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_ScoreEvaluations_ScoreEvaluationTraceId",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallContextPacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterBudget = table.Column<int>(type: "INTEGER", nullable: false),
                    RenderedCharacterCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceRefCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallContextPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextPacks_CognitiveMemory_RecallTraces_RecallTraceId",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextPacks_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallTraceStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StageKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ChannelKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CandidateCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SelectedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ExcludedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LimitingBudget = table.Column<int>(type: "INTEGER", nullable: true),
                    ProviderTrace = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallTraceStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallTraceStages_CognitiveMemory_RecallTraces_RecallTraceId",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallContextSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContextPackId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SectionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    SectionKey = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AccessLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RedactionState = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedTokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallContextSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_RecallContextPacks_ContextPackId",
                        column: x => x.ContextPackId,
                        principalTable: "CognitiveMemory_RecallContextPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_RecallTraces_RecallTraceId",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallSourceRefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContextPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceSystem = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Locator = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    QuoteHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RedactionState = table.Column<int>(type: "INTEGER", nullable: false),
                    IncludedInContext = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExclusionReasonKind = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallSourceRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_RecallContextPacks_ContextPackId",
                        column: x => x.ContextPackId,
                        principalTable: "CognitiveMemory_RecallContextPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_RecallTraces_RecallTraceId",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_SourceItems_SourceItemId",
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
                name: "IX_CognitiveMemory_RecallTraces_ProjectId_RecallMode_Outcome_StartedAtUtc",
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
                name: "IX_CognitiveMemory_RecallCandidates_ProjectId_MemoryRecordId_DecisionKind",
                table: "CognitiveMemory_RecallCandidates",
                columns: new[] { "ProjectId", "MemoryRecordId", "DecisionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ProjectId_PrimaryChannelKind_CreatedAtUtc",
                table: "CognitiveMemory_RecallCandidates",
                columns: new[] { "ProjectId", "PrimaryChannelKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_RecallTraceId_DecisionKind_PrimaryChannelKind",
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
                name: "IX_CognitiveMemory_RecallContextSections_ProjectId_SectionKind_CreatedAtUtc",
                table: "CognitiveMemory_RecallContextSections",
                columns: new[] { "ProjectId", "SectionKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_RecallTraceId_SectionKind",
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
                name: "IX_CognitiveMemory_RecallSourceRefs_ContextPackId_IncludedInContext",
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
                name: "IX_CognitiveMemory_RecallSourceRefs_ProjectId_SourceSystem_IncludedInContext",
                table: "CognitiveMemory_RecallSourceRefs",
                columns: new[] { "ProjectId", "SourceSystem", "IncludedInContext" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_RecallTraceId_MemoryRecordId_IncludedInContext",
                table: "CognitiveMemory_RecallSourceRefs",
                columns: new[] { "RecallTraceId", "MemoryRecordId", "IncludedInContext" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_SourceItemId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraceStages_ProjectId_StageKind_Status_StartedAtUtc",
                table: "CognitiveMemory_RecallTraceStages",
                columns: new[] { "ProjectId", "StageKind", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraceStages_RecallTraceId_StageKind_ChannelKind",
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
                name: "IX_CognitiveMemory_RecallTraces_ProjectId_RecallMode_Outcome_StartedAtUtc",
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
