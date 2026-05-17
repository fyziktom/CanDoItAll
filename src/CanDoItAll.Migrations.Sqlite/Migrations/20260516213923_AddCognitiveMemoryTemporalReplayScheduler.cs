using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryTemporalReplayScheduler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobKind = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    PriorityScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PriorityBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "REAL", nullable: true),
                    QueuePriority = table.Column<int>(type: "INTEGER", nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExpectedOutputSchema = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    LeaseToken = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobs_CognitiveMemory_ScoreEvaluations_PriorityScoreEvaluationTraceId",
                        column: x => x.PriorityScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_TemporalEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EpisodeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Goal = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    ActualOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    OutcomeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FirstStepAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastStepAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StepCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LinkCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_TemporalEpisodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayJobPredictionErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayJobPredictionErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobPredictionErrors_CognitiveMemory_PredictionErrors_PredictionErrorId",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobPredictionErrors_CognitiveMemory_ReplayJobs_ReplayJobId",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayJobSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayJobSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobSignals_CognitiveMemory_ReplayJobs_ReplayJobId",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobSignals_CognitiveMemory_Signals_CognitiveSignalId",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayJobTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetKind = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    RequiredInputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayJobTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobTargets_CognitiveMemory_ReplayJobs_ReplayJobId",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayOutputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OutputKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProjectionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayOutputs_CognitiveMemory_MutationCommands_MutationCommandId",
                        column: x => x.MutationCommandId,
                        principalTable: "CognitiveMemory_MutationCommands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayOutputs_CognitiveMemory_ReplayJobs_ReplayJobId",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayOutputs_CognitiveMemory_ReviewItems_ReviewItemId",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayWorkerResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkerId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OutputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    OutputSchema = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ResultStorageReference = table.Column<string>(type: "TEXT", nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayWorkerResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayWorkerResults_CognitiveMemory_ReplayJobs_ReplayJobId",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EpisodeSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SequenceIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ActorKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ActionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ToolOrPluginKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EpisodeSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeSteps_CognitiveMemory_TemporalEpisodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_TemporalEpisodeLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LinkKind = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_TemporalEpisodeLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_TemporalEpisodeLinks_CognitiveMemory_TemporalEpisodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EpisodeCausalLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LinkKind = table.Column<int>(type: "INTEGER", nullable: false),
                    FromStepId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ToStepId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ClaimId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PredictionErrorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EpisodeCausalLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_EpisodeSteps_FromStepId",
                        column: x => x.FromStepId,
                        principalTable: "CognitiveMemory_EpisodeSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_EpisodeSteps_ToStepId",
                        column: x => x.ToStepId,
                        principalTable: "CognitiveMemory_EpisodeSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_PredictionErrors_PredictionErrorId",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_TemporalEpisodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EpisodeStepEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceRole = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EpisodeStepEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeStepEvidence_CognitiveMemory_EpisodeSteps_StepId",
                        column: x => x.StepId,
                        principalTable: "CognitiveMemory_EpisodeSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeStepEvidence_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_ClaimId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_EpisodeId_LinkKind_FromStepId_ToStepId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                columns: new[] { "EpisodeId", "LinkKind", "FromStepId", "ToStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_EvidenceAnchorId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_FromStepId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "FromStepId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_PredictionErrorId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_ProjectId_ClaimId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                columns: new[] { "ProjectId", "ClaimId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_ProjectId_PredictionErrorId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                columns: new[] { "ProjectId", "PredictionErrorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_ToStepId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "ToStepId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeStepEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_EpisodeStepEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeStepEvidence_ProjectId_EvidenceAnchorId",
                table: "CognitiveMemory_EpisodeStepEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeStepEvidence_StepId_EvidenceRole_EvidenceAnchorId",
                table: "CognitiveMemory_EpisodeStepEvidence",
                columns: new[] { "StepId", "EvidenceRole", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeSteps_EpisodeId_SequenceIndex",
                table: "CognitiveMemory_EpisodeSteps",
                columns: new[] { "EpisodeId", "SequenceIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeSteps_ProjectId_ActorKind_ActorId",
                table: "CognitiveMemory_EpisodeSteps",
                columns: new[] { "ProjectId", "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeSteps_ProjectId_OccurredAtUtc",
                table: "CognitiveMemory_EpisodeSteps",
                columns: new[] { "ProjectId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobPredictionErrors_PredictionErrorId",
                table: "CognitiveMemory_ReplayJobPredictionErrors",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobPredictionErrors_ProjectId_PredictionErrorId",
                table: "CognitiveMemory_ReplayJobPredictionErrors",
                columns: new[] { "ProjectId", "PredictionErrorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobPredictionErrors_ReplayJobId_PredictionErrorId",
                table: "CognitiveMemory_ReplayJobPredictionErrors",
                columns: new[] { "ReplayJobId", "PredictionErrorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobs_PriorityScoreEvaluationTraceId",
                table: "CognitiveMemory_ReplayJobs",
                column: "PriorityScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobs_ProjectId_JobKind_InputHash",
                table: "CognitiveMemory_ReplayJobs",
                columns: new[] { "ProjectId", "JobKind", "InputHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobs_ProjectId_JobKind_QueuePriority",
                table: "CognitiveMemory_ReplayJobs",
                columns: new[] { "ProjectId", "JobKind", "QueuePriority" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobs_ProjectId_State_ScheduledAtUtc",
                table: "CognitiveMemory_ReplayJobs",
                columns: new[] { "ProjectId", "State", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobSignals_CognitiveSignalId",
                table: "CognitiveMemory_ReplayJobSignals",
                column: "CognitiveSignalId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobSignals_ProjectId_CognitiveSignalId",
                table: "CognitiveMemory_ReplayJobSignals",
                columns: new[] { "ProjectId", "CognitiveSignalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobSignals_ReplayJobId_CognitiveSignalId",
                table: "CognitiveMemory_ReplayJobSignals",
                columns: new[] { "ReplayJobId", "CognitiveSignalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobTargets_ProjectId_TargetKind_TargetId",
                table: "CognitiveMemory_ReplayJobTargets",
                columns: new[] { "ProjectId", "TargetKind", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobTargets_ReplayJobId_TargetKind_TargetId_TargetKey",
                table: "CognitiveMemory_ReplayJobTargets",
                columns: new[] { "ReplayJobId", "TargetKind", "TargetId", "TargetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayOutputs_MutationCommandId",
                table: "CognitiveMemory_ReplayOutputs",
                column: "MutationCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayOutputs_ProjectId_OutputKind_Status",
                table: "CognitiveMemory_ReplayOutputs",
                columns: new[] { "ProjectId", "OutputKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayOutputs_ReplayJobId",
                table: "CognitiveMemory_ReplayOutputs",
                column: "ReplayJobId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayOutputs_ReviewItemId",
                table: "CognitiveMemory_ReplayOutputs",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayWorkerResults_ProjectId_Status_SubmittedAtUtc",
                table: "CognitiveMemory_ReplayWorkerResults",
                columns: new[] { "ProjectId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayWorkerResults_ReplayJobId_WorkerId_SubmittedAtUtc",
                table: "CognitiveMemory_ReplayWorkerResults",
                columns: new[] { "ReplayJobId", "WorkerId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_TemporalEpisodeLinks_EpisodeId_LinkKind_TargetId_TargetKey",
                table: "CognitiveMemory_TemporalEpisodeLinks",
                columns: new[] { "EpisodeId", "LinkKind", "TargetId", "TargetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_TemporalEpisodeLinks_ProjectId_LinkKind_TargetId",
                table: "CognitiveMemory_TemporalEpisodeLinks",
                columns: new[] { "ProjectId", "LinkKind", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_TemporalEpisodes_ProjectId_EndedAtUtc",
                table: "CognitiveMemory_TemporalEpisodes",
                columns: new[] { "ProjectId", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_TemporalEpisodes_ProjectId_EpisodeKind_StartedAtUtc",
                table: "CognitiveMemory_TemporalEpisodes",
                columns: new[] { "ProjectId", "EpisodeKind", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_EpisodeCausalLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EpisodeStepEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayJobPredictionErrors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayJobSignals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayJobTargets");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayOutputs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayWorkerResults");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_TemporalEpisodeLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EpisodeSteps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayJobs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_TemporalEpisodes");
        }
    }
}
