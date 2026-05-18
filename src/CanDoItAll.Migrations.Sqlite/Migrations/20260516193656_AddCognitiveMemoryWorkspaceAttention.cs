using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryWorkspaceAttention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AnswerPostureDecisionId",
                table: "CognitiveMemory_RecallTraces",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AttentionDecisionId",
                table: "CognitiveMemory_RecallTraces",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InhibitedCandidateCount",
                table: "CognitiveMemory_RecallTraces",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LimitingBudget",
                table: "CognitiveMemory_RecallTraces",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SelectedClaimCount",
                table: "CognitiveMemory_RecallTraces",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SelectedEvidenceAnchorCount",
                table: "CognitiveMemory_RecallTraces",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SelfRegulationAssessmentId",
                table: "CognitiveMemory_RecallTraces",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceFrameId",
                table: "CognitiveMemory_RecallTraces",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceFrames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FrameKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    OwnerAgentId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessStepId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProbeSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LearningTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContextBudgetTokenLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    ContextBudgetSectionLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    ContextBudgetDetailLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentTokenEstimate = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentSectionEstimate = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentDetailEstimate = table.Column<int>(type: "INTEGER", nullable: false),
                    BudgetExhausted = table.Column<bool>(type: "INTEGER", nullable: false),
                    LimitingBudget = table.Column<int>(type: "INTEGER", nullable: true),
                    CognitiveLoadScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CognitiveLoadBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayCognitiveLoadScore = table.Column<double>(type: "REAL", nullable: true),
                    LastAttentionDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastSelfRegulationAssessmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastAnswerPostureDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFrames_CognitiveMemory_ScoreEvaluations_CognitiveLoadScoreEvaluationTraceId",
                        column: x => x.CognitiveLoadScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_AttentionDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecisionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ReasonKind = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RequestPreview = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RoutingScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoutingBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "REAL", nullable: true),
                    MatchedShapeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MissingRequiredDimensionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredNextActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AttentionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_AttentionDecisions_CognitiveMemory_ScoreEvaluations_RoutingScoreEvaluationTraceId",
                        column: x => x.RoutingScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_AttentionDecisions_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentGoalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GoalKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceGoals_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceInhibitedCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CandidateKind = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalCandidateKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ReasonKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    InhibitionScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    InhibitionBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayRelevanceScore = table.Column<double>(type: "REAL", nullable: true),
                    DisplayInhibitionStrength = table.Column<double>(type: "REAL", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceInhibitedCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemory_ScoreEvaluations_InhibitionScoreEvaluationTraceId",
                        column: x => x.InhibitionScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemory_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceOpenQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuestionText = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceOpenQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceOpenQuestions_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceFocusSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SlotKind = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProbeTurnId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkflowArtifactId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OpenQuestionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExternalPlaceholderKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    AttentionScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AttentionBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayAttentionScore = table.Column<double>(type: "REAL", nullable: true),
                    SourceSufficiency = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfidenceBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    StalenessBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    InclusionReasonKind = table.Column<int>(type: "INTEGER", nullable: false),
                    InclusionReason = table.Column<string>(type: "TEXT", nullable: false),
                    RelationToActiveGoal = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CompressionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    EstimatedTokenCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedSectionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedDetailCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceFocusSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_RecallTraces_RecallTraceId",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_ScoreEvaluations_AttentionScoreEvaluationTraceId",
                        column: x => x.AttentionScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_WorkspaceOpenQuestions_OpenQuestionId",
                        column: x => x.OpenQuestionId,
                        principalTable: "CognitiveMemory_WorkspaceOpenQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceSlotId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceSlotEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceSlotEvidenceAnchors_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceSlotEvidenceAnchors_CognitiveMemory_WorkspaceFocusSlots_WorkspaceSlotId",
                        column: x => x.WorkspaceSlotId,
                        principalTable: "CognitiveMemory_WorkspaceFocusSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceSlotEvidenceAnchors_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_AnswerPostureDecisionId",
                table: "CognitiveMemory_RecallTraces",
                column: "AnswerPostureDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_AttentionDecisionId",
                table: "CognitiveMemory_RecallTraces",
                column: "AttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_SelfRegulationAssessmentId",
                table: "CognitiveMemory_RecallTraces",
                column: "SelfRegulationAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_WorkspaceFrameId",
                table: "CognitiveMemory_RecallTraces",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_ProjectId_DecisionKind_CreatedAtUtc",
                table: "CognitiveMemory_AttentionDecisions",
                columns: new[] { "ProjectId", "DecisionKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_ProjectId_WorkspaceFrameId_CreatedAtUtc",
                table: "CognitiveMemory_AttentionDecisions",
                columns: new[] { "ProjectId", "WorkspaceFrameId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_RequestHash",
                table: "CognitiveMemory_AttentionDecisions",
                column: "RequestHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_RoutingScoreEvaluationTraceId",
                table: "CognitiveMemory_AttentionDecisions",
                column: "RoutingScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_WorkspaceFrameId_DecisionKind",
                table: "CognitiveMemory_AttentionDecisions",
                columns: new[] { "WorkspaceFrameId", "DecisionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_AttentionScoreEvaluationTraceId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "AttentionScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_ClaimId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_MemoryRecordId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_OpenQuestionId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "OpenQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_RecallTraceId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_SourceItemId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_WorkspaceFrameId_SlotKind_CreatedAtUtc",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                columns: new[] { "WorkspaceFrameId", "SlotKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_CognitiveLoadScoreEvaluationTraceId",
                table: "CognitiveMemory_WorkspaceFrames",
                column: "CognitiveLoadScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_LastAttentionDecisionId",
                table: "CognitiveMemory_WorkspaceFrames",
                column: "LastAttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_FrameKind_Status_ExpiresAtUtc",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "FrameKind", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_LearningTaskId_Status",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "LearningTaskId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_OwnerAgentId_Status",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "OwnerAgentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_OwnerUserId_Status",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_ProbeSessionId_Status",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "ProbeSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_ProcessRunId_ProcessStepId_Status",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "ProcessRunId", "ProcessStepId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_ReviewSessionId_Status",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "ReviewSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_WorkflowRunId_Status",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "WorkflowRunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceGoals_ProjectId_GoalKey",
                table: "CognitiveMemory_WorkspaceGoals",
                columns: new[] { "ProjectId", "GoalKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceGoals_WorkspaceFrameId_Sequence",
                table: "CognitiveMemory_WorkspaceGoals",
                columns: new[] { "WorkspaceFrameId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_ClaimId",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_InhibitionScoreEvaluationTraceId",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                column: "InhibitionScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_MemoryRecordId",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_SourceItemId",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_WorkspaceFrameId_ReasonKind_CreatedAtUtc",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                columns: new[] { "WorkspaceFrameId", "ReasonKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceOpenQuestions_ProjectId_Status",
                table: "CognitiveMemory_WorkspaceOpenQuestions",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceOpenQuestions_WorkspaceFrameId_Status_CreatedAtUtc",
                table: "CognitiveMemory_WorkspaceOpenQuestions",
                columns: new[] { "WorkspaceFrameId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceSlotEvidenceAnchors_EvidenceAnchorId",
                table: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceSlotEvidenceAnchors_ProjectId_EvidenceAnchorId",
                table: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceSlotEvidenceAnchors_WorkspaceFrameId_EvidenceAnchorId",
                table: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                columns: new[] { "WorkspaceFrameId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceSlotEvidenceAnchors_WorkspaceSlotId_EvidenceAnchorId",
                table: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                columns: new[] { "WorkspaceSlotId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_RecallTraces_CognitiveMemory_AttentionDecisions_AttentionDecisionId",
                table: "CognitiveMemory_RecallTraces",
                column: "AttentionDecisionId",
                principalTable: "CognitiveMemory_AttentionDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_RecallTraces_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                table: "CognitiveMemory_RecallTraces",
                column: "WorkspaceFrameId",
                principalTable: "CognitiveMemory_WorkspaceFrames",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_RecallTraces_CognitiveMemory_AttentionDecisions_AttentionDecisionId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_RecallTraces_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_AttentionDecisions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceGoals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceInhibitedCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceSlotEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceFocusSlots");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceOpenQuestions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceFrames");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_RecallTraces_AnswerPostureDecisionId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_RecallTraces_AttentionDecisionId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_RecallTraces_SelfRegulationAssessmentId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_RecallTraces_WorkspaceFrameId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "AnswerPostureDecisionId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "AttentionDecisionId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "InhibitedCandidateCount",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "LimitingBudget",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "SelectedClaimCount",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "SelectedEvidenceAnchorCount",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "SelfRegulationAssessmentId",
                table: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropColumn(
                name: "WorkspaceFrameId",
                table: "CognitiveMemory_RecallTraces");
        }
    }
}
