using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryProcedureSkillMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSimulations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OutputKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    IsSpeculative = table.Column<bool>(type: "INTEGER", nullable: false),
                    SpeculationLabel = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RiskBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayRiskScore = table.Column<double>(type: "REAL", nullable: true),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    RequiredValidationStepsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulations_CognitiveMemory_ScoreEvaluations_RiskScoreEvaluationTraceId",
                        column: x => x.RiskScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Maturity = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidationState = table.Column<int>(type: "INTEGER", nullable: false),
                    AccessLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceConsolidationCandidateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastSuccessfulEpisodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaturityScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MaturityBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayMaturityScore = table.Column<double>(type: "REAL", nullable: true),
                    PreconditionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PostconditionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredParticipantsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredToolKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    InputSchemaJson = table.Column<string>(type: "TEXT", nullable: false),
                    OutputSchemaJson = table.Column<string>(type: "TEXT", nullable: false),
                    StepCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureModeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidationEvidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AutomationBindingCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_ConsolidationCandidates_SourceConsolidationCandidateId",
                        column: x => x.SourceConsolidationCandidateId,
                        principalTable: "CognitiveMemory_ConsolidationCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_ScoreEvaluations_MaturityScoreEvaluationTraceId",
                        column: x => x.MaturityScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_TemporalEpisodes_LastSuccessfulEpisodeId",
                        column: x => x.LastSuccessfulEpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationEvidence_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationEvidence_CognitiveMemory_ProcedureSimulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "CognitiveMemory_ProcedureSimulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureAutomationBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BindingKind = table.Column<int>(type: "INTEGER", nullable: false),
                    BindingKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RejectionCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureAutomationBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureAutomationBindings_CognitiveMemory_ProcedureSkills_ProcedureSkillId",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureAutomationBindings_CognitiveMemory_ReviewItems_ReviewItemId",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FailureKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Condition = table.Column<string>(type: "TEXT", nullable: false),
                    DetectionSignal = table.Column<string>(type: "TEXT", nullable: false),
                    LikelyCause = table.Column<string>(type: "TEXT", nullable: false),
                    Mitigation = table.Column<string>(type: "TEXT", nullable: false),
                    RollbackOrCompensation = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModes_CognitiveMemory_ProcedureSkills_ProcedureSkillId",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSimulationSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SimulationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulationSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationSkills_CognitiveMemory_ProcedureSimulations_SimulationId",
                        column: x => x.SimulationId,
                        principalTable: "CognitiveMemory_ProcedureSimulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationSkills_CognitiveMemory_ProcedureSkills_ProcedureSkillId",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SequenceIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredInput = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationCheck = table.Column<string>(type: "TEXT", nullable: false),
                    FailureHandling = table.Column<string>(type: "TEXT", nullable: false),
                    ToolBindingKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    RetryLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    IsRollbackStep = table.Column<bool>(type: "INTEGER", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSteps_CognitiveMemory_ProcedureSkills_ProcedureSkillId",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureValidationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceRole = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureValidationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemory_ProcedureSkills_ProcedureSkillId",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemory_ReviewItems_ReviewItemId",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemory_TemporalEpisodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureFailureModeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModeEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModeEpisodes_CognitiveMemory_ProcedureFailureModes_ProcedureFailureModeId",
                        column: x => x.ProcedureFailureModeId,
                        principalTable: "CognitiveMemory_ProcedureFailureModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModeEpisodes_CognitiveMemory_TemporalEpisodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureFailureModeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModePredictionErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModePredictionErrors_CognitiveMemory_PredictionErrors_PredictionErrorId",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModePredictionErrors_CognitiveMemory_ProcedureFailureModes_ProcedureFailureModeId",
                        column: x => x.ProcedureFailureModeId,
                        principalTable: "CognitiveMemory_ProcedureFailureModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureStepEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureStepId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureStepEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureStepEvidence_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureStepEvidence_CognitiveMemory_ProcedureSteps_ProcedureStepId",
                        column: x => x.ProcedureStepId,
                        principalTable: "CognitiveMemory_ProcedureSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureAutomationBindings_ProcedureSkillId_BindingKind_BindingKey",
                table: "CognitiveMemory_ProcedureAutomationBindings",
                columns: new[] { "ProcedureSkillId", "BindingKind", "BindingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureAutomationBindings_ProjectId_State_BindingKind",
                table: "CognitiveMemory_ProcedureAutomationBindings",
                columns: new[] { "ProjectId", "State", "BindingKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureAutomationBindings_ReviewItemId",
                table: "CognitiveMemory_ProcedureAutomationBindings",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModeEpisodes_EpisodeId",
                table: "CognitiveMemory_ProcedureFailureModeEpisodes",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModeEpisodes_ProcedureFailureModeId_EpisodeId",
                table: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: new[] { "ProcedureFailureModeId", "EpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModeEpisodes_ProjectId_EpisodeId",
                table: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: new[] { "ProjectId", "EpisodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_PredictionErrorId",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_ProcedureFailureModeId_PredictionErrorId",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: new[] { "ProcedureFailureModeId", "PredictionErrorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_ProjectId_PredictionErrorId",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: new[] { "ProjectId", "PredictionErrorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModes_ProcedureSkillId_FailureKey",
                table: "CognitiveMemory_ProcedureFailureModes",
                columns: new[] { "ProcedureSkillId", "FailureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModes_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_ProcedureFailureModes",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureSimulationEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationEvidence_ProjectId_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationEvidence_SimulationId_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: new[] { "SimulationId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_ProjectId_OutputKind_RiskLevel",
                table: "CognitiveMemory_ProcedureSimulations",
                columns: new[] { "ProjectId", "OutputKind", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_ProjectId_Status_CreatedAtUtc",
                table: "CognitiveMemory_ProcedureSimulations",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_RiskScoreEvaluationTraceId",
                table: "CognitiveMemory_ProcedureSimulations",
                column: "RiskScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_ProcedureSkillId",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                column: "ProcedureSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_ProjectId_ProcedureSkillId",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                columns: new[] { "ProjectId", "ProcedureSkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_SimulationId_ProcedureSkillId",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                columns: new[] { "SimulationId", "ProcedureSkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_LastSuccessfulEpisodeId",
                table: "CognitiveMemory_ProcedureSkills",
                column: "LastSuccessfulEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_MaturityScoreEvaluationTraceId",
                table: "CognitiveMemory_ProcedureSkills",
                column: "MaturityScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_ProjectId_Maturity_ValidationState",
                table: "CognitiveMemory_ProcedureSkills",
                columns: new[] { "ProjectId", "Maturity", "ValidationState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_ProjectId_RiskLevel_Maturity",
                table: "CognitiveMemory_ProcedureSkills",
                columns: new[] { "ProjectId", "RiskLevel", "Maturity" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_SourceConsolidationCandidateId",
                table: "CognitiveMemory_ProcedureSkills",
                column: "SourceConsolidationCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureStepEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_ProcedureStepId_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureStepEvidence",
                columns: new[] { "ProcedureStepId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_ProjectId_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureStepEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSteps_ProcedureSkillId_SequenceIndex",
                table: "CognitiveMemory_ProcedureSteps",
                columns: new[] { "ProcedureSkillId", "SequenceIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSteps_ProcedureSkillId_StepKey",
                table: "CognitiveMemory_ProcedureSteps",
                columns: new[] { "ProcedureSkillId", "StepKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSteps_ProjectId_ToolBindingKey",
                table: "CognitiveMemory_ProcedureSteps",
                columns: new[] { "ProjectId", "ToolBindingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_EpisodeId",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_ProcedureSkillId_EvidenceRole_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                columns: new[] { "ProcedureSkillId", "EvidenceRole", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_ProjectId_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_ReviewItemId",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                column: "ReviewItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureAutomationBindings");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureFailureModeEpisodes");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureFailureModePredictionErrors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSimulationEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSimulationSkills");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureStepEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureValidationEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureFailureModes");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSimulations");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSteps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSkills");
        }
    }
}
