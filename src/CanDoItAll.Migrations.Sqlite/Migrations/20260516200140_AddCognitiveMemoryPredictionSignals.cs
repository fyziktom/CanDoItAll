using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryPredictionSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionExpectations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExpectationKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProbeSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ExpectedContextKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ExpectedSourceSufficiency = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumExpectedConfidence = table.Column<double>(type: "REAL", nullable: true),
                    MaximumExpectedConfidence = table.Column<double>(type: "REAL", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionExpectations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_AttentionDecisions_AttentionDecisionId",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionExpectationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ErrorKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProbeTurnId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SeverityScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SeverityBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplaySeverityProjection = table.Column<double>(type: "REAL", nullable: true),
                    SeverityComponentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedShapeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MissingRequiredDimensionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ObservationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CauseHypothesis = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedActionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    SuggestedAction = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedSignalCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_AttentionDecisions_AttentionDecisionId",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_PredictionExpectations_PredictionExpectationId",
                        column: x => x.PredictionExpectationId,
                        principalTable: "CognitiveMemory_PredictionExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_ScoreEvaluations_SeverityScoreEvaluationTraceId",
                        column: x => x.SeverityScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionExpectationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionExpectationEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectationEvidenceAnchors_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectationEvidenceAnchors_CognitiveMemory_PredictionExpectations_PredictionExpectationId",
                        column: x => x.PredictionExpectationId,
                        principalTable: "CognitiveMemory_PredictionExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrorEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorEvidenceAnchors_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorEvidenceAnchors_CognitiveMemory_PredictionErrors_PredictionErrorId",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Signals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SignalKind = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    AccessLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RedactionState = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PredictionErrorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProbeTurnId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SignalScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScoreSchemaVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NormalizationProfileId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ComponentCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedShapeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MissingRequiredDimensionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayMagnitudeProjection = table.Column<double>(type: "REAL", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Signals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_AttentionDecisions_AttentionDecisionId",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_PredictionErrors_PredictionErrorId",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_ScoreEvaluations_SignalScoreEvaluationTraceId",
                        column: x => x.SignalScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_SourceItems_SourceItemId",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_WorkspaceFrames_WorkspaceFrameId",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrorSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrorSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorSignals_CognitiveMemory_PredictionErrors_PredictionErrorId",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorSignals_CognitiveMemory_Signals_CognitiveSignalId",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SignalConsumerPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConsumerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumAccessLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresReviewBeforeAction = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanCreateTruthDirectly = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SignalConsumerPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalConsumerPolicies_CognitiveMemory_Signals_CognitiveSignalId",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SignalEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SignalEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalEvidenceAnchors_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalEvidenceAnchors_CognitiveMemory_Signals_CognitiveSignalId",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_EvidenceAnchorId",
                table: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_PredictionErrorId_EvidenceAnchorId",
                table: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                columns: new[] { "PredictionErrorId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_ProjectId_EvidenceAnchorId",
                table: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_AttentionDecisionId",
                table: "CognitiveMemory_PredictionErrors",
                column: "AttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_ClaimId",
                table: "CognitiveMemory_PredictionErrors",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_MemoryRecordId",
                table: "CognitiveMemory_PredictionErrors",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_PredictionExpectationId",
                table: "CognitiveMemory_PredictionErrors",
                column: "PredictionExpectationId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_ProjectId_ErrorKind_ObservedAtUtc",
                table: "CognitiveMemory_PredictionErrors",
                columns: new[] { "ProjectId", "ErrorKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_ProjectId_RequiresReview_ObservedAtUtc",
                table: "CognitiveMemory_PredictionErrors",
                columns: new[] { "ProjectId", "RequiresReview", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_SeverityScoreEvaluationTraceId",
                table: "CognitiveMemory_PredictionErrors",
                column: "SeverityScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_SourceItemId",
                table: "CognitiveMemory_PredictionErrors",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_WorkspaceFrameId",
                table: "CognitiveMemory_PredictionErrors",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorSignals_CognitiveSignalId",
                table: "CognitiveMemory_PredictionErrorSignals",
                column: "CognitiveSignalId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorSignals_PredictionErrorId_CognitiveSignalId",
                table: "CognitiveMemory_PredictionErrorSignals",
                columns: new[] { "PredictionErrorId", "CognitiveSignalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorSignals_ProjectId_CognitiveSignalId",
                table: "CognitiveMemory_PredictionErrorSignals",
                columns: new[] { "ProjectId", "CognitiveSignalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_EvidenceAnchorId",
                table: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_PredictionExpectationId_EvidenceAnchorId",
                table: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                columns: new[] { "PredictionExpectationId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_ProjectId_EvidenceAnchorId",
                table: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_AttentionDecisionId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "AttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_ClaimId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_MemoryRecordId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_ProjectId_ActorKind_ActorId",
                table: "CognitiveMemory_PredictionExpectations",
                columns: new[] { "ProjectId", "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_ProjectId_ExpectationKind_CreatedAtUtc",
                table: "CognitiveMemory_PredictionExpectations",
                columns: new[] { "ProjectId", "ExpectationKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_SourceItemId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_WorkspaceFrameId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalConsumerPolicies_CognitiveSignalId_ConsumerKind",
                table: "CognitiveMemory_SignalConsumerPolicies",
                columns: new[] { "CognitiveSignalId", "ConsumerKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalConsumerPolicies_ProjectId_ConsumerKind_CreatedAtUtc",
                table: "CognitiveMemory_SignalConsumerPolicies",
                columns: new[] { "ProjectId", "ConsumerKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_CognitiveSignalId_EvidenceAnchorId",
                table: "CognitiveMemory_SignalEvidenceAnchors",
                columns: new[] { "CognitiveSignalId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_EvidenceAnchorId",
                table: "CognitiveMemory_SignalEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_ProjectId_EvidenceAnchorId",
                table: "CognitiveMemory_SignalEvidenceAnchors",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_AttentionDecisionId",
                table: "CognitiveMemory_Signals",
                column: "AttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ClaimId",
                table: "CognitiveMemory_Signals",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_MemoryRecordId",
                table: "CognitiveMemory_Signals",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_PredictionErrorId",
                table: "CognitiveMemory_Signals",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_ActorKind_ActorId",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_RequiresReview_ObservedAtUtc",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "RequiresReview", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_SignalKind_ObservedAtUtc",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "SignalKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_SourceKind_ObservedAtUtc",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "SourceKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_WorkspaceFrameId_ObservedAtUtc",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "WorkspaceFrameId", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_SignalScoreEvaluationTraceId",
                table: "CognitiveMemory_Signals",
                column: "SignalScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_SourceItemId",
                table: "CognitiveMemory_Signals",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_WorkspaceFrameId",
                table: "CognitiveMemory_Signals",
                column: "WorkspaceFrameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionErrorEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionErrorSignals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionExpectationEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SignalConsumerPolicies");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SignalEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Signals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionErrors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionExpectations");
        }
    }
}
