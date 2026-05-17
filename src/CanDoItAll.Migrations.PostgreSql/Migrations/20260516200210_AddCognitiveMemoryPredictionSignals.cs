using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectationKind = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedContextKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ExpectedSourceSufficiency = table.Column<int>(type: "integer", nullable: false),
                    MinimumExpectedConfidence = table.Column<double>(type: "double precision", nullable: true),
                    MaximumExpectedConfidence = table.Column<double>(type: "double precision", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionExpectations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Atte~",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Clai~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Reco~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Sour~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Work~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionExpectationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeverityScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeverityBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplaySeverityProjection = table.Column<double>(type: "double precision", nullable: true),
                    SeverityComponentCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedShapeCount = table.Column<int>(type: "integer", nullable: false),
                    MissingRequiredDimensionCount = table.Column<int>(type: "integer", nullable: false),
                    ObservationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CauseHypothesis = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedActionKind = table.Column<int>(type: "integer", nullable: false),
                    SuggestedAction = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresReview = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedSignalCount = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_AttentionD~",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_Claims_Cla~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_Prediction~",
                        column: x => x.PredictionExpectationId,
                        principalTable: "CognitiveMemory_PredictionExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_Records_Me~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_ScoreEvalu~",
                        column: x => x.SeverityScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_SourceItem~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_WorkspaceF~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionExpectationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionExpectationEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectationEvidenceAnchors_Cognit~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectationEvidenceAnchors_Cogni~1",
                        column: x => x.PredictionExpectationId,
                        principalTable: "CognitiveMemory_PredictionExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrorEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorEvidenceAnchors_CognitiveMem~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorEvidenceAnchors_CognitiveMe~1",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Signals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignalKind = table.Column<int>(type: "integer", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    RequiresReview = table.Column<bool>(type: "boolean", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignalScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreSchemaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizationProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ComponentCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedShapeCount = table.Column<int>(type: "integer", nullable: false),
                    MissingRequiredDimensionCount = table.Column<int>(type: "integer", nullable: false),
                    DisplayMagnitudeProjection = table.Column<double>(type: "double precision", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Signals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_AttentionDecisions_~",
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
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_PredictionErrors_Pr~",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_Records_MemoryRecor~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_ScoreEvaluations_Si~",
                        column: x => x.SignalScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_SourceItems_SourceI~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_WorkspaceFrames_Wor~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrorSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrorSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorSignals_CognitiveMemory_Pred~",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorSignals_CognitiveMemory_Sign~",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SignalConsumerPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerKind = table.Column<int>(type: "integer", nullable: false),
                    MaximumAccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RequiresReviewBeforeAction = table.Column<bool>(type: "boolean", nullable: false),
                    CanCreateTruthDirectly = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SignalConsumerPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalConsumerPolicies_CognitiveMemory_Sign~",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SignalEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SignalEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalEvidenceAnchors_CognitiveMemory_Evide~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalEvidenceAnchors_CognitiveMemory_Signa~",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_EvidenceAnch~",
                table: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_PredictionEr~",
                table: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                columns: new[] { "PredictionErrorId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_ProjectId_Ev~",
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
                name: "IX_CognitiveMemory_PredictionErrors_ProjectId_ErrorKind_Observ~",
                table: "CognitiveMemory_PredictionErrors",
                columns: new[] { "ProjectId", "ErrorKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_ProjectId_RequiresReview_O~",
                table: "CognitiveMemory_PredictionErrors",
                columns: new[] { "ProjectId", "RequiresReview", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_SeverityScoreEvaluationTra~",
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
                name: "IX_CognitiveMemory_PredictionErrorSignals_PredictionErrorId_Co~",
                table: "CognitiveMemory_PredictionErrorSignals",
                columns: new[] { "PredictionErrorId", "CognitiveSignalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorSignals_ProjectId_CognitiveS~",
                table: "CognitiveMemory_PredictionErrorSignals",
                columns: new[] { "ProjectId", "CognitiveSignalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_Eviden~",
                table: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_Predic~",
                table: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                columns: new[] { "PredictionExpectationId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_Projec~",
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
                name: "IX_CognitiveMemory_PredictionExpectations_ProjectId_ActorKind_~",
                table: "CognitiveMemory_PredictionExpectations",
                columns: new[] { "ProjectId", "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_ProjectId_Expectatio~",
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
                name: "IX_CognitiveMemory_SignalConsumerPolicies_CognitiveSignalId_Co~",
                table: "CognitiveMemory_SignalConsumerPolicies",
                columns: new[] { "CognitiveSignalId", "ConsumerKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalConsumerPolicies_ProjectId_ConsumerKi~",
                table: "CognitiveMemory_SignalConsumerPolicies",
                columns: new[] { "ProjectId", "ConsumerKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_CognitiveSignalId_Evi~",
                table: "CognitiveMemory_SignalEvidenceAnchors",
                columns: new[] { "CognitiveSignalId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_EvidenceAnchorId",
                table: "CognitiveMemory_SignalEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_ProjectId_EvidenceAnc~",
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
                name: "IX_CognitiveMemory_Signals_ProjectId_RequiresReview_ObservedAt~",
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
                name: "IX_CognitiveMemory_Signals_ProjectId_WorkspaceFrameId_Observed~",
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
