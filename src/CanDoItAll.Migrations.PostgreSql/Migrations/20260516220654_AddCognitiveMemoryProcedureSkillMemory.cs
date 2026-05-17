using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    IsSpeculative = table.Column<bool>(type: "boolean", nullable: false),
                    SpeculationLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayRiskScore = table.Column<double>(type: "double precision", nullable: true),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RequiredValidationStepsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulations_CognitiveMemory_ScoreE~",
                        column: x => x.RiskScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Maturity = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    ValidationState = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    SourceConsolidationCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSuccessfulEpisodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaturityScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaturityBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayMaturityScore = table.Column<double>(type: "double precision", nullable: true),
                    PreconditionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PostconditionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredParticipantsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredToolKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    InputSchemaJson = table.Column<string>(type: "TEXT", nullable: false),
                    OutputSchemaJson = table.Column<string>(type: "TEXT", nullable: false),
                    StepCount = table.Column<int>(type: "integer", nullable: false),
                    FailureModeCount = table.Column<int>(type: "integer", nullable: false),
                    ValidationEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    AutomationBindingCount = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_Consolidati~",
                        column: x => x.SourceConsolidationCandidateId,
                        principalTable: "CognitiveMemory_ConsolidationCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_ScoreEvalua~",
                        column: x => x.MaturityScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_TemporalEpi~",
                        column: x => x.LastSuccessfulEpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationEvidence_CognitiveMemory~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationEvidence_CognitiveMemor~1",
                        column: x => x.SimulationId,
                        principalTable: "CognitiveMemory_ProcedureSimulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureAutomationBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    BindingKind = table.Column<int>(type: "integer", nullable: false),
                    BindingKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureAutomationBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureAutomationBindings_CognitiveMemory~",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureAutomationBindings_CognitiveMemor~1",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FailureKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Condition = table.Column<string>(type: "TEXT", nullable: false),
                    DetectionSignal = table.Column<string>(type: "TEXT", nullable: false),
                    LikelyCause = table.Column<string>(type: "TEXT", nullable: false),
                    Mitigation = table.Column<string>(type: "TEXT", nullable: false),
                    RollbackOrCompensation = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModes_CognitiveMemory_Proce~",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSimulationSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulationSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationSkills_CognitiveMemory_P~",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationSkills_CognitiveMemory_~1",
                        column: x => x.SimulationId,
                        principalTable: "CognitiveMemory_ProcedureSimulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SequenceIndex = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredInput = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationCheck = table.Column<string>(type: "TEXT", nullable: false),
                    FailureHandling = table.Column<string>(type: "TEXT", nullable: false),
                    ToolBindingKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    RetryLimit = table.Column<int>(type: "integer", nullable: false),
                    IsRollbackStep = table.Column<bool>(type: "boolean", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSteps_CognitiveMemory_ProcedureSki~",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureValidationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceRole = table.Column<int>(type: "integer", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureValidationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemory~",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemor~1",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemor~2",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemor~3",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureFailureModeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModeEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModeEpisodes_CognitiveMemor~",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModeEpisodes_CognitiveMemo~1",
                        column: x => x.ProcedureFailureModeId,
                        principalTable: "CognitiveMemory_ProcedureFailureModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureFailureModeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModePredictionErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModePredictionErrors_Cognit~",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModePredictionErrors_Cogni~1",
                        column: x => x.ProcedureFailureModeId,
                        principalTable: "CognitiveMemory_ProcedureFailureModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureStepEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureStepEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureStepEvidence_CognitiveMemory_Evide~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureStepEvidence_CognitiveMemory_Proce~",
                        column: x => x.ProcedureStepId,
                        principalTable: "CognitiveMemory_ProcedureSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureAutomationBindings_ProcedureSkillI~",
                table: "CognitiveMemory_ProcedureAutomationBindings",
                columns: new[] { "ProcedureSkillId", "BindingKind", "BindingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureAutomationBindings_ProjectId_State~",
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
                name: "IX_CognitiveMemory_ProcedureFailureModeEpisodes_ProcedureFailu~",
                table: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: new[] { "ProcedureFailureModeId", "EpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModeEpisodes_ProjectId_Epis~",
                table: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: new[] { "ProjectId", "EpisodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_Predic~",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_Proced~",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: new[] { "ProcedureFailureModeId", "PredictionErrorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_Projec~",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: new[] { "ProjectId", "PredictionErrorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModes_ProcedureSkillId_Fail~",
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
                name: "IX_CognitiveMemory_ProcedureSimulationEvidence_ProjectId_Evide~",
                table: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationEvidence_SimulationId_Ev~",
                table: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: new[] { "SimulationId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_ProjectId_OutputKind_R~",
                table: "CognitiveMemory_ProcedureSimulations",
                columns: new[] { "ProjectId", "OutputKind", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_ProjectId_Status_Creat~",
                table: "CognitiveMemory_ProcedureSimulations",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_RiskScoreEvaluationTra~",
                table: "CognitiveMemory_ProcedureSimulations",
                column: "RiskScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_ProcedureSkillId",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                column: "ProcedureSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_ProjectId_Procedu~",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                columns: new[] { "ProjectId", "ProcedureSkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_SimulationId_Proc~",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                columns: new[] { "SimulationId", "ProcedureSkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_LastSuccessfulEpisodeId",
                table: "CognitiveMemory_ProcedureSkills",
                column: "LastSuccessfulEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_MaturityScoreEvaluationTrac~",
                table: "CognitiveMemory_ProcedureSkills",
                column: "MaturityScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_ProjectId_Maturity_Validati~",
                table: "CognitiveMemory_ProcedureSkills",
                columns: new[] { "ProjectId", "Maturity", "ValidationState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_ProjectId_RiskLevel_Maturity",
                table: "CognitiveMemory_ProcedureSkills",
                columns: new[] { "ProjectId", "RiskLevel", "Maturity" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_SourceConsolidationCandidat~",
                table: "CognitiveMemory_ProcedureSkills",
                column: "SourceConsolidationCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureStepEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_ProcedureStepId_Evide~",
                table: "CognitiveMemory_ProcedureStepEvidence",
                columns: new[] { "ProcedureStepId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_ProjectId_EvidenceAnc~",
                table: "CognitiveMemory_ProcedureStepEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSteps_ProcedureSkillId_SequenceInd~",
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
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_ProcedureSkillI~",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                columns: new[] { "ProcedureSkillId", "EvidenceRole", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_ProjectId_Evide~",
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
